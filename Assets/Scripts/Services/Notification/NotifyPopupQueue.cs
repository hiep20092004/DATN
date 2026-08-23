using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using WaterFlow.Framework.UIModule;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Serialises notify popups so only one is visible at a time.
    /// Call <see cref="Enqueue"/> or <see cref="EnqueuePanel{T}"/> from any caller;
    /// the queue opens each popup only after the previous one has fully closed.
    /// </summary>
    public class NotifyPopupQueue : Singleton<NotifyPopupQueue>
    {
        private readonly Queue<NotifyPopupRequest> pendingRequests = new();
        private readonly HashSet<string> keysInQueue = new();

        private string currentKey;
        private bool isProcessing;

        protected override void OnAwake()
        {
            base.OnAwake();
            ProcessQueueAsync().Forget();
        }

        // ─── Public API ───────────────────────────────────────────────────

        public bool IsBusy => isProcessing;

        public bool IsIdle => !isProcessing && pendingRequests.Count == 0;

        /// <summary>Enqueue a request. Requests with the same non-null key are deduplicated.</summary>
        public void Enqueue(NotifyPopupRequest request)
        {
            if (request == null) return;

            if (request.Key != null)
            {
                // Skip if same key is already queued or currently being shown.
                if (keysInQueue.Contains(request.Key) || request.Key == currentKey)
                    return;

                keysInQueue.Add(request.Key);
            }

            pendingRequests.Enqueue(request);
        }

        /// <summary>
        /// Convenience helper: enqueues opening a <typeparamref name="T"/> Panel via PanelManager.
        /// A <see cref="UIDataKey.CallBackOnClose"/> callback is automatically injected so the queue
        /// knows when the popup has closed.
        /// </summary>
        public void EnqueuePanel<T>(UIData uiData = null, string dedupeKey = null) where T : Panel
        {
            var key = dedupeKey;

            var request = new NotifyPopupRequest(
                openAsync: async () =>
                {
                    var tcs = new UniTaskCompletionSource();

                    // Inject close callback into uiData (Set overwrites, preserving any pre-existing callback).
                    UIData data = uiData ?? new UIData();
                    Action existingCallback = null;
                    data.TryGet<Action>(UIDataKey.CallBackOnClose, out existingCallback);

                    Action closeCallback = () =>
                    {
                        existingCallback?.Invoke();
                        tcs.TrySetResult();
                    };
                    data.Set(UIDataKey.CallBackOnClose, closeCallback);

                    View panel = await PanelManager.Instance.OpenPanelAsync<T>(data);

                    if (panel == null || panel.gameObject == null)
                    {
                        tcs.TrySetResult();
                    }

                    await tcs.Task;
                    return panel;
                },
                key: key
            );

            Enqueue(request);
        }

        /// <summary>
        /// Clear all pending requests. Does NOT close the currently open popup.
        /// Call this when transitioning to a new session (Replay / auto-next level).
        /// </summary>
        public void Clear()
        {
            pendingRequests.Clear();
            keysInQueue.Clear();
        }

        // ─── Internal Loop ────────────────────────────────────────────────

        private async UniTaskVoid ProcessQueueAsync()
        {
            while (true)
            {
                // Wait until there is work to do
                await UniTask.WaitUntil(() => pendingRequests.Count > 0,
                    cancellationToken: this.GetCancellationTokenOnDestroy());

                NotifyPopupRequest request = pendingRequests.Dequeue();

                if (request.Key != null)
                    keysInQueue.Remove(request.Key);

                currentKey = request.Key;
                isProcessing = true;

                try
                {
                    await request.OpenAsync();
                }
                catch (OperationCanceledException)
                {
                    // Object destroyed mid-await — exit loop cleanly
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NotifyPopupQueue] Exception while showing popup: {ex}");
                }
                finally
                {
                    currentKey = null;
                    isProcessing = false;
                }
            }
        }
    }
}
