using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterFlow.Core
{
    [StaticUnload]
    public class GameLoading : MonoBehaviour
    {
        private static GameLoading gameLoading;

        [SerializeField] Initializer initializer;
        [SerializeField] LoadingGraphics loadingGraphics;

        private static AsyncOperation loadingOperation;
        public static bool IsSceneLoaded => loadingOperation != null && loadingOperation.isDone;

        private static string loadingMessage;
        private static List<LoadingTask> loadingTasks = new List<LoadingTask>();

        private CancellationTokenSource cts;
        private bool isLoading;

        public static int LoadingSceneBuildIndex = -1;

        /// <summary>
        /// Raised once the initializer and every loading task finished, right before the first
        /// scene is loaded. Game code hooks its own post-boot work here.
        /// </summary>
        public static event Action onLoadingFinished;

        private void Awake()
        {
            gameLoading = this;

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Input.multiTouchEnabled = false;
            loadingGraphics.Init(this);
            initializer.Init();
            StartLoading();
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        public void RetryConnection()
        {
            if (!isLoading)
            {
                StartLoading();
            }
        }

        private void StartLoading()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            LoadingAsync(cts.Token).Forget();
        }

        private async UniTaskVoid LoadingAsync(CancellationToken cancellationToken)
        {
            isLoading = true;

            try
            {
                loadingGraphics.HideErrorMessage();

                initializer.InitModules();

                int taskIndex = 0;
                while (taskIndex < loadingTasks.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!loadingTasks[taskIndex].IsActive)
                        loadingTasks[taskIndex].Activate();

                    if (loadingTasks[taskIndex].IsFinished)
                    {
                        taskIndex++;
                    }

                    await UniTask.Yield(cancellationToken);
                }

                int sceneIndex = LoadingSceneBuildIndex;
                if (sceneIndex == -1)
                {
                    sceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
                    if (SceneManager.sceneCount < sceneIndex)
                        Debug.LogError("[Loading]: First scene is missing!");
                }

                await UniTask.WaitUntil(() => !loadingGraphics.isLoading, cancellationToken: cancellationToken);

                onLoadingFinished?.Invoke();

                loadingOperation = SceneManager.LoadSceneAsync(sceneIndex);
                while (!loadingOperation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(cancellationToken);
                }

                loadingGraphics.Dismiss();
                Destroy(gameObject);
            }
            catch (OperationCanceledException)
            {
                // Cancelled, do nothing
            }
            finally
            {
                isLoading = false;
            }
        }

        public static void SetLoadingMessage(string message)
        {
            loadingMessage = message;
        }

        public static void AddTask(LoadingTask loadingTask)
        {
            loadingTasks.Add(loadingTask);
        }

        private static void UnloadStatic()
        {
            loadingTasks.Clear();
            onLoadingFinished = null;
        }

        public delegate void LoadingCallback(float state, string message);
    }
}
