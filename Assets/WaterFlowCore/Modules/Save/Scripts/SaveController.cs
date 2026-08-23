using System;
using System.Collections;
using UnityEngine;
using System.Threading;
using Object = UnityEngine.Object;

namespace WaterFlow.Core
{
    [StaticUnload]
    public static class SaveController
    {
        private const string SAVE_FILE_NAME = "save";

        private static GlobalSave globalSave;

        private static bool isSaveLoaded;
        public static bool IsSaveLoaded => isSaveLoaded;

        private static bool isSaveRequired;

        // Tracks whether a background save thread is already running. If true,
        // a new Save() call skips spawning a second thread: the data will be
        // written by the thread already in flight (Flush is done before the
        // thread is spawned, so the latest state is always captured first).
        private static volatile bool isSaveThreadRunning;

        public static float GameTime => globalSave.GameTime;
        public static long AccountCreatedTime => globalSave.AccountCreatedTime;
        public static DateTime LastExitTime => globalSave.LastExitTime;

        public static event SimpleCallback OnSaveLoaded;

        public static void Init(float autoSaveDelay, bool clearSave = false, float overrideTime = -1f)
        {
            Serializer.Init();
            SaveDebugLog.Log($"Init(autoSaveDelay={autoSaveDelay}, clearSave={clearSave}, overrideTime={overrideTime}) | persistentDataPath='{Application.persistentDataPath}'");

            GameObject saveCallbackReciever = new GameObject("[SAVE CALLBACK RECIEVER]");
            saveCallbackReciever.hideFlags = HideFlags.HideInHierarchy;

            Object.DontDestroyOnLoad(saveCallbackReciever);

            UnityCallbackReciever unityCallbackReciever = saveCallbackReciever.AddComponent<UnityCallbackReciever>();

            if (clearSave)
            {
                InitClear(overrideTime != -1f ? overrideTime : Time.time);
            }
            else
            {
                Load(overrideTime != -1f ? overrideTime : Time.time);
            }

            if (autoSaveDelay > 0)
            {
                // Enable auto-save coroutine
                unityCallbackReciever.StartCoroutine(AutoSaveCoroutine(autoSaveDelay));
            }
        }

        public static void UpdateTime(float time)
        {
            globalSave.Time = time;
        }

        public static T GetSaveObject<T>(int hash) where T : ISaveObject, new()
        {
            if (!isSaveLoaded)
            {
                Debug.LogError("Save controller has not been initialized");
                return default;
            }

            return globalSave.GetSaveObject<T>(hash);
        }

        public static T GetSaveObject<T>() where T : ISaveObject, new()
        {
            if (!isSaveLoaded)
            {
                Debug.LogError("Save controller has not been initialized");
                return default;
            }
            return globalSave.GetSaveObject<T>();
        }

        public static T GetSaveObject<T>(string uniqueName) where T : ISaveObject, new()
        {
            if (!isSaveLoaded)
            {
                Debug.LogError("Save controller has not been initialized");
                return default;
            }
            return globalSave.GetSaveObject<T>(uniqueName);
        }

        private static void InitClear(float time)
        {
            globalSave = new GlobalSave();
            globalSave.Init(time);

            Debug.Log("[Save Controller]: Created clear save!");
            SaveDebugLog.Warn("InitClear: starting with fresh GlobalSave (clean start enabled).");

            isSaveLoaded = true;
        }

        private static void Load(float time)
        {
            if (isSaveLoaded)
                return;

            SaveDebugLog.Log("Load: reading save file 'save' via active wrapper.");
            // Try to read and deserialize file or create new one. The wrapper
            // is responsible for backing up corrupt files before returning a
            // fresh GlobalSave – we never want to silently overwrite the only
            // copy of a user’s progress.
            globalSave = BaseSaveWrapper.ActiveWrapper.Load(SAVE_FILE_NAME);

            globalSave.Init(time);

            Debug.Log("[Save Controller]: Save is loaded!");
            SaveDebugLog.Log($"Load: GlobalSave initialized. AccountCreatedTime={globalSave.AccountCreatedTime} LastExitTime={globalSave.LastExitTime}");

            isSaveLoaded = true;

            OnSaveLoaded?.Invoke();
        }

        public static void Save(bool forceSave = false, bool useThreads = true)
        {
            if (!forceSave && !isSaveRequired) return;
            if (globalSave == null) return;

            SaveDebugLog.Log($"Save(forceSave={forceSave}, useThreads={useThreads}) | thread={System.Threading.Thread.CurrentThread.ManagedThreadId}");

            // Flush must happen on the calling (main) thread so all save-object
            // state is captured before we hand off to a background thread.
            globalSave.Flush(true);
            isSaveRequired = false;
            SaveDebugLog.Log($"Save: flushed. LastExitTime={globalSave.LastExitTime} GameTime={globalSave.GameTime}");

            BaseSaveWrapper saveWrapper = BaseSaveWrapper.ActiveWrapper;
            if (useThreads && saveWrapper.UseThreads())
            {
                // If a save thread is already running, it will write the data
                // we just flushed (Serializer uses a lock internally), so
                // there is no need to spawn a second thread that would race on
                // the same .tmp file.
                if (isSaveThreadRunning)
                {
                    Debug.Log("[Save Controller]: Save deferred – thread already running.");
                    SaveDebugLog.Warn("Save: deferred (background save already running).");
                    return;
                }

                isSaveThreadRunning = true;
                Thread saveThread = new Thread(() =>
                {
                    try
                    {
                        BaseSaveWrapper.ActiveWrapper.Save(globalSave, SAVE_FILE_NAME);
                        SaveDebugLog.Log("Save: background write finished.");
                    }
                    finally
                    {
                        isSaveThreadRunning = false;
                    }
                });
                saveThread.IsBackground = true;
                saveThread.Start();
            }
            else
            {
                BaseSaveWrapper.ActiveWrapper.Save(globalSave, SAVE_FILE_NAME);
                SaveDebugLog.Log("Save: synchronous write finished.");
            }

            Debug.Log("[Save Controller]: Game is saved!");
        }

        public static void SaveCustom(GlobalSave globalSave)
        {
            if (globalSave != null)
            {
                globalSave.Flush(false);

                BaseSaveWrapper.ActiveWrapper.Save(globalSave, SAVE_FILE_NAME);
            }
        }

        public static void MarkAsSaveIsRequired()
        {
            isSaveRequired = true;
        }

        private static IEnumerator AutoSaveCoroutine(float saveDelay)
        {
            WaitForSeconds waitForSeconds = new WaitForSeconds(saveDelay);

            while (true)
            {
                yield return waitForSeconds;

                // NOTE: historically this called Save() (non-forced) which is a
                // no-op because no production code path ever calls
                // MarkAsSaveIsRequired(). The result was that the only saves
                // that actually hit disk were the explicit Save(true) calls on
                // level completion / settings popups – meaning a crash or
                // process kill in between could lose minutes of progress.
                // Auto-save is cheap and idempotent, so we now always force.
                Save(forceSave: true);
            }
        }

        public static void PresetsSave(string fullFileName)
        {
            globalSave.Flush(false);

            BaseSaveWrapper.ActiveWrapper.Save(globalSave, fullFileName);
        }

        public static void Info()
        {
            globalSave.Info();
        }

        public static void DeleteSaveFile()
        {
            BaseSaveWrapper.ActiveWrapper.Delete(SAVE_FILE_NAME);
        }

        public static GlobalSave GetGlobalSave()
        {
            GlobalSave tempGlobalSave = BaseSaveWrapper.ActiveWrapper.Load(SAVE_FILE_NAME);

            tempGlobalSave.Init(Time.time);

            return tempGlobalSave;
        }

        private static void UnloadStatic()
        {
            globalSave = null;

            isSaveLoaded = false;
            isSaveRequired = false;
            isSaveThreadRunning = false;

            OnSaveLoaded = null;
        }

        private class UnityCallbackReciever : MonoBehaviour
        {
            private void OnDestroy()
            {
#if UNITY_EDITOR
                SaveController.Save(true);
#endif
            }

            private void OnApplicationFocus(bool focus)
            {
#if !UNITY_EDITOR
                // Force-save when the app loses focus: the previous behaviour
                // (Save() without forceSave) was always a no-op, see comment
                // in AutoSaveCoroutine above.
                SaveDebugLog.Log($"OnApplicationFocus(focus={focus})");
                if (!focus) SaveController.Save(forceSave: true);
#endif
            }

            private void OnApplicationPause(bool pause)
            {
#if !UNITY_EDITOR
                // On Android, OnApplicationFocus is unreliable when the user
                // hits the home button – OnApplicationPause is the canonical
                // last-chance save signal. Forcing here as well costs nothing.
                SaveDebugLog.Log($"OnApplicationPause(pause={pause})");
                if (pause) SaveController.Save(forceSave: true);
#endif
            }
        }
    }
}
