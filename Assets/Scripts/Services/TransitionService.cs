using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.UIModule;
using WaterFlow.Framework.Systems.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "TransitionService", menuName = "Services/Transition/Service")]
public class TransitionService : SceneService
{
    private bool isTransitioning;

    // The boot scene (Loading -> Home) is loaded by GameLoading, not by this service, so a cached
    // placement field would stay stale at Loading for the whole Home session. Read the active scene.
    public override GamePlacement GetCurrentGamePlacement()
    {
        return SceneManager.GetActiveScene().name.FromBuildSceneName();
    }

    public override void SwitchScene(GamePlacement newPlacement, bool force = false, Action callback = null)
    {
        // Guard early to avoid races from multiple quick clicks
        if (isTransitioning) return;

        // GamePlacement order must mirror the scene build list, so a placement whose scene is absent is a
        // build-settings bug. Fail loudly instead of rerouting, which would hide the misconfiguration.
        string requestedSceneName = newPlacement.ToBuildSceneName();
        if (!Application.CanStreamedLevelBeLoaded(requestedSceneName))
        {
            Debug.LogError($"TransitionService: scene '{requestedSceneName}' for {newPlacement} is not in Build Settings. Aborting transition.");
            return;
        }

        isTransitioning = true; // set here so subsequent clicks are blocked immediately
        PanelManager.Instance?.CloseAllPanel();

        // Start the async work; it'll clear isTransitioning in finally
        ChangeSceneAsync(newPlacement, force, callback).Forget();
    }

    private async UniTask ChangeSceneAsync(GamePlacement newPlacement, bool force, Action callback)
    {
        try
        {
            string targetSceneName = newPlacement.ToBuildSceneName();
            GamePlacement currentPlacement = GetCurrentGamePlacement();

            if (!BaseTransition.Instance)
            {
                Debug.LogError("TransitionService: BaseTransition instance is not found (before FadeIn). Aborting transition.");
                return;
            }

            // First-ever scene switch is the boot transition (Loading -> Game/Home);
            // skip the cover panel there since GameLoading's own loading UI already covers the screen.
            bool skipTransitionVisuals = currentPlacement == GamePlacement.Loading;

            // Start loading but don't activate yet
            var asyncOperation = SceneManager.LoadSceneAsync(targetSceneName);
            asyncOperation.allowSceneActivation = false;

            // Fade in (cover)
            if (!skipTransitionVisuals)
            {
                await BaseTransition.Instance.FadeIn(currentPlacement, newPlacement);
            }

            // Allow scene activation and wait completion
            asyncOperation.allowSceneActivation = true;
            await asyncOperation;

            // Small delay to let scene objects Awake/Start run
            await UniTask.Delay(200);

            // Wait for the new scene's BaseTransition instance to be ready.
            // Give it a reasonable timeout (e.g. 40 * 50ms = 2s)
            int attempts = 0;
            const int maxAttempts = 40;
            while (BaseTransition.Instance == null && attempts++ < maxAttempts)
            {
                await UniTask.Delay(150);
            }

            if (BaseTransition.Instance == null)
            {
                Debug.LogWarning("TransitionService: BaseTransition instance not found after scene load — skipping FadeOut.");
            }
            else if (!skipTransitionVisuals)
            {
                await BaseTransition.Instance.FadeOut();
            }

            callback?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            // Always release the lock even if we returned early or had an error
            isTransitioning = false;
        }
    }
}
