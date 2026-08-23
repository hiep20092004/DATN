using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace WaterFlow.Core
{
    public static class Serializer
    {
        private const string TEMP_FILE_SUFFIX = ".tmp";
        private const string BACKUP_FILE_SUFFIX = ".bak";

        private static string persistentDataPath;

        // Ensures only one thread writes to disk at a time. Without this,
        // OnApplicationFocus(false) and OnApplicationPause(true) both fire on
        // Android when the user presses Home, spawning two save threads that
        // race to write the same .tmp path and cause a sharing violation.
        private static readonly object FileLock = new object();

        public static void Init()
        {
            persistentDataPath = Application.persistentDataPath;
            // Read the PlayerPrefs debug flag while still on the main thread so
            // background save threads can check SaveDebugLog.Enabled safely.
            SaveDebugLog.Refresh();
        }

        /// <summary>
        /// Deserializes file located at Persistent Data Path.
        /// </summary>
        public static T Deserialize<T>(string fileName, bool logIfFileNotExists = false) where T : new()
        {
            string absolutePath = Path.Combine(GetPersistentDataPath(), fileName);
            string tempPath = absolutePath + TEMP_FILE_SUFFIX;
            string backupPath = absolutePath + BACKUP_FILE_SUFFIX;
            bool hasRecoveryCandidate = File.Exists(tempPath) || File.Exists(backupPath);

            if (TryDeserializeFile(absolutePath, out T deserializedObject, out Exception mainError))
            {
                SaveDebugLog.Log($"Deserialize<{typeof(T).Name}>: success '{fileName}'");
                return deserializedObject;
            }

            if (mainError != null)
            {
                ReportDeserializeFailure<T>(fileName, absolutePath, mainError);
                BackupCorruptFile(absolutePath);
            }
            else
            {
                if (logIfFileNotExists)
                    Debug.LogWarning($"File at path : \"{absolutePath}\" does not exist.");
                SaveDebugLog.Log($"Deserialize<{typeof(T).Name}>: file missing '{fileName}'");
            }

            if (TryRecoverFile(tempPath, absolutePath, out deserializedObject))
            {
                SaveDebugLog.Warn($"Deserialize<{typeof(T).Name}>: recovered '{fileName}' from temp file.");
                return deserializedObject;
            }

            if (TryRecoverFile(backupPath, absolutePath, out deserializedObject))
            {
                SaveDebugLog.Warn($"Deserialize<{typeof(T).Name}>: recovered '{fileName}' from backup file.");
                return deserializedObject;
            }

            if (mainError != null || hasRecoveryCandidate)
                SaveDebugLog.Warn($"Deserialize<{typeof(T).Name}>: no valid primary, temp, or backup file. Returning defaults.");
            else
                SaveDebugLog.Log($"Deserialize<{typeof(T).Name}>: no save files found. Returning defaults for a new user.");
            return new T();
        }

        /// <summary>
        /// Serializes object to Persistent Data Path.
        /// Uses a .tmp file + swap strategy so a mid-write crash cannot corrupt
        /// the existing save. Serializes under a lock so concurrent calls from
        /// different threads queue up instead of racing on the .tmp file.
        /// </summary>
        public static void Serialize<T>(T objectToSerialize, string fileName)
        {
            string absolutePath = Path.Combine(GetPersistentDataPath(), fileName);
            string tempPath = absolutePath + TEMP_FILE_SUFFIX;
            string backupPath = absolutePath + BACKUP_FILE_SUFFIX;

            lock (FileLock)
            {
                bool tempWriteCompleted = false;
                try
                {
                    SaveDebugLog.Log($"Serialize<{typeof(T).Name}>: writing tmp='{tempPath}' then swap to '{absolutePath}'");
                    BinaryFormatter bf = new BinaryFormatter();

                    // Write fully to .tmp first so that a crash mid-write
                    // never leaves a half-written save file.
                    using (FileStream file = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        bf.Serialize(file, objectToSerialize);
                        file.Flush();
                    }
                    tempWriteCompleted = true;

                    if (File.Exists(absolutePath))
                    {
                        File.Copy(absolutePath, backupPath, true);
                        File.Delete(absolutePath);
                    }

                    File.Move(tempPath, absolutePath);
                    SaveDebugLog.Log($"Serialize<{typeof(T).Name}>: success '{fileName}'");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Serializer] Failed to serialize '{fileName}': {ex}");
                    SaveDebugLog.Error($"Serialize<{typeof(T).Name}> failed for '{fileName}': {ex.GetType().Name}");

                    // A complete temp is a valid recovery candidate if promotion failed after the old
                    // primary was removed. Only discard files that failed during serialization itself.
                    if (!tempWriteCompleted)
                    {
                        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
                    }
                }
            }
        }

        private static bool TryDeserializeFile<T>(string absolutePath, out T value, out Exception error)
        {
            value = default;
            error = null;
            if (!File.Exists(absolutePath)) return false;

            try
            {
                SaveDebugLog.Log($"Deserialize<{typeof(T).Name}>: opening '{absolutePath}'");
                using (FileStream file = File.Open(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var formatter = new BinaryFormatter();
                    value = (T)formatter.Deserialize(file);
                }

                return value != null;
            }
            catch (Exception ex)
            {
                error = ex;
                value = default;
                return false;
            }
        }

        private static bool TryRecoverFile<T>(string candidatePath, string primaryPath, out T value)
        {
            if (!TryDeserializeFile(candidatePath, out value, out Exception error))
            {
                if (error != null)
                    SaveDebugLog.Error($"Recovery candidate '{candidatePath}' is invalid: {error.GetType().Name}");
                return false;
            }

            try
            {
                File.Copy(candidatePath, primaryPath, true);
                if (candidatePath.EndsWith(TEMP_FILE_SUFFIX, StringComparison.Ordinal))
                    File.Delete(candidatePath);
            }
            catch (Exception ex)
            {
                // The in-memory data is still valid. A later save can persist it even if repairing the
                // primary file is temporarily blocked by the platform or storage state.
                Debug.LogError($"[Serializer] Recovered save data but failed to repair '{primaryPath}': {ex.Message}");
                SaveDebugLog.Error($"Failed to repair primary save from '{candidatePath}': {ex.GetType().Name}");
            }

            return true;
        }

        private static void ReportDeserializeFailure<T>(string fileName, string absolutePath, Exception error)
        {
            long fileSize = -1;
            try { fileSize = new FileInfo(absolutePath).Length; } catch { /* ignore */ }

            Debug.LogError(
                $"[Serializer] Failed to deserialize '{fileName}' (size={fileSize} bytes) as {typeof(T).Name}. " +
                $"Trying recovery files before returning defaults. Exception: {error}");
            SaveDebugLog.Error($"Deserialize<{typeof(T).Name}> failed for '{fileName}' (size={fileSize}). Trying recovery files.");
        }

        /// <summary>
        /// Renames a corrupt save to "save.corrupt.&lt;unix-timestamp&gt;" so it
        /// can be sent back for debugging and is never silently overwritten.
        /// </summary>
        private static void BackupCorruptFile(string absolutePath)
        {
            try
            {
                if (!File.Exists(absolutePath)) return;

                long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string backupPath = absolutePath + ".corrupt." + unix;

                int counter = 1;
                while (File.Exists(backupPath))
                    backupPath = absolutePath + ".corrupt." + unix + "." + counter++;

                File.Move(absolutePath, backupPath);
                Debug.LogWarning($"[Serializer] Corrupt save backed up to: {backupPath}");
                SaveDebugLog.Warn($"Backed up corrupt save: '{backupPath}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Serializer] Failed to back up corrupt save '{absolutePath}': {ex.Message}");
                SaveDebugLog.Error($"BackupCorruptFile failed: {ex.GetType().Name} {ex.Message}");
            }
        }

        public static bool FileExistsAtPDP(string fileName)
        {
            return File.Exists(Path.Combine(GetPersistentDataPath(), fileName));
        }

        public static bool FileExistsAtPath(string absolutePath)
        {
            return File.Exists(absolutePath);
        }

        public static bool FileExistsAtPath(string directoryPath, string fileName)
        {
            return File.Exists(Path.Combine(directoryPath, fileName));
        }

        public static void DeleteFileAtPDP(string fileName)
        {
            string absolutePath = Path.Combine(GetPersistentDataPath(), fileName);
            lock (FileLock)
            {
                File.Delete(absolutePath);
                File.Delete(absolutePath + TEMP_FILE_SUFFIX);
                File.Delete(absolutePath + BACKUP_FILE_SUFFIX);
            }
        }

        public static void DeleteFileAtPath(string absolutePath)
        {
            File.Delete(absolutePath);
        }

        public static void DeleteFileAtPath(string directoryPath, string fileName)
        {
            File.Delete(Path.Combine(directoryPath, fileName));
        }

        private static string GetPersistentDataPath()
        {
            if (string.IsNullOrEmpty(persistentDataPath))
                persistentDataPath = Application.persistentDataPath;
            return persistentDataPath;
        }
    }
}
