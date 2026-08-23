using Cysharp.Threading.Tasks;
using DG.Tweening;
using WaterFlow.Enums;
using UnityEngine;
using UnityEngine.UI;

public class CircleTransition : BaseTransition
{
    public GameObject transitionPanel;
    public float duration = 0.25f;
    public CanvasGroup canvasGroup;
    public Image circleImage;
    public AnimationCurve fadeInCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private void Start()
    {
        transitionPanel.SetActive(false);
        canvasGroup.alpha = 0;
    }

    public override async UniTask FadeIn(GamePlacement from, GamePlacement to)
    {
        transitionPanel.SetActive(true);
        _ = canvasGroup.DOFade(1, duration).From(0).SetEase(Ease.Linear);
        _ = circleImage.transform.DOScale(0, duration).From(25).SetEase(fadeInCurve);
        await UniTask.WaitForSeconds(duration);
    }

    public override async UniTask FadeOut()
    {
        _ = canvasGroup.DOFade(0, duration).From(1).SetEase(Ease.Linear);
        _ = circleImage.transform.DOScale(25, duration).From(0).SetEase(fadeOutCurve);
        await UniTask.WaitForSeconds(duration);
        transitionPanel.SetActive(false);
    }
}
