using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Core
{
    public class LoadingGraphics : MonoBehaviour
    {
        [SerializeField] Camera loadingCamera;
        [SerializeField] CanvasScaler canvasScaler;
        [SerializeField] CanvasGroup canvasGroup;

        [Space]
        [SerializeField] Image backgroundImage;
        [SerializeField] Image loadingImage;
        [SerializeField] TextMeshProUGUI loadingMessageText;
        [SerializeField] TextMeshProUGUI loadingText;
        [SerializeField] GameObject loadingbarObject;
        [SerializeField] Button retryButton;

        private GameLoading loadingController;
        private const int dotIntervalMs = 500;
        private const string LOADING_LABEL = "Loading";

        public bool isLoading = false;


        public void Init(GameLoading loadingController)
        {
            this.loadingController = loadingController;

            // Keep the boot loading screen alive across the first scene activation
            // (Loading -> Home/Game) so it covers the scene-activation GC hitch.
            // Dismissed by GameLoading once the destination scene is active.
            DontDestroyOnLoad(gameObject);

            canvasScaler.MatchSize();

            retryButton.onClick.AddListener(OnRetryButtonClicked);
            retryButton.gameObject.SetActive(false);

            loadingbarObject.SetActive(true);
            loadingText.text = LOADING_LABEL;

            SetLoadingState(0.0f);
            StartLoadingBar().Forget();
            StartLoadingText().Forget();
        }

        private async UniTask StartLoadingBar()
        {
            isLoading = true;

            CancellationToken cts = this.GetCancellationTokenOnDestroy();

            // Phase 1: 0% -> 40%
            float duration1 = 1.5f;
            float target1 = 0.4f;
            float elapsed = 0f;
            float progress;

            while (elapsed < duration1)
            {
                elapsed += Time.deltaTime;
                progress = Mathf.Lerp(0f, target1, elapsed / duration1);
                SetLoadingState(progress);
                await UniTask.Yield(cts);
            }

            SetLoadingState(target1);
            progress = target1;

            // Phase 2: creep to 99% while boot work is still running.
            float maxProgress = 0.99f;
            float slowSpeed = 0.4f; // progress per second

            while (progress < maxProgress)
            {
                progress = Mathf.MoveTowards(progress, maxProgress, slowSpeed * Time.deltaTime);
                SetLoadingState(progress);
                await UniTask.Yield(cts);
            }

            SetLoadingState(1f);

            await UniTask.Delay(100, cancellationToken: cts);

            OnLoadingFinished();
        }

        public void ShowErrorMessage(string message)
        {
            loadingbarObject.SetActive(false);
            retryButton.gameObject.SetActive(true);

            loadingMessageText.text = message;
        }

        public void HideErrorMessage()
        {
            loadingbarObject.SetActive(true);
            retryButton.gameObject.SetActive(false);

            loadingMessageText.text = "Loading...";
        }

        private void OnRetryButtonClicked()
        {
            loadingController.RetryConnection();
        }

        public void SetLoadingState(float state)
        {
            loadingImage.fillAmount = state;
        }

        public void OnLoadingFinished()
        {
            // Release the scene switch (GameLoading waits on !isLoading) but keep the
            // screen fully opaque — it must stay up through scene activation to hide
            // the GC hitch. Fading happens in Dismiss(), called after the new scene loads.
            isLoading = false;
        }

        public void Dismiss()
        {
            canvasGroup.DOFade(0.0f, 0.6f, unscaledTime: true)
                .OnComplete(() => Destroy(gameObject));
        }

        private async UniTask StartLoadingText()
        {
            CancellationToken cts = this.GetCancellationTokenOnDestroy();

            int phase = 0;

            while (isLoading)
            {
                await UniTask.Delay(dotIntervalMs, cancellationToken: cts);
                phase = (phase + 1) % 4;
                loadingText.text = phase == 0 ? LOADING_LABEL : LOADING_LABEL + new string('.', phase);
            }
        }
    }
}
