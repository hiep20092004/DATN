using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Framework.UIModule
{
    public static class UITween
    {
        public static async UniTask Play(TweenData tweenData)
        {
            tweenData.SetupData();
            if (tweenData.config == null) return;
            switch (tweenData.config.tweenType)
            {
                case UITweenType.Scale:
                    await PlayTweenScale(tweenData);
                    break;
                case UITweenType.Fade:
                    await PlayTweenFade(tweenData);
                    break;
                case UITweenType.Move:
                    await PlayTweenMove(tweenData);
                    break;
                case UITweenType.LocalMove:
                    await PlayTweenLocalMove(tweenData);
                    break;
                case UITweenType.RectLocalMove:
                    await PlayTweenRectLocalMove(tweenData);
                    break;
                case UITweenType.FadeGroup:
                    await PlayTweenFadeGroup(tweenData);
                    break;
                case UITweenType.Active:
                    await PlayTweenActive(tweenData);
                    break;
                case UITweenType.Inactive:
                    await PlayTweenInActive(tweenData);
                    break;
            }
        }

        private static async UniTask PlayTweenScale(TweenData tweenData)
        {
            var target = tweenData.target;
            target.localScale = Vector3.one * tweenData.config.from;
            await target.DOScale(tweenData.config.to, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay).OnComplete(() => { tweenData.OnCompleted?.Invoke(); }).ToUniTask();
        }

        private static async UniTask PlayTweenFade(TweenData tweenData)
        {
            var target = tweenData.target.GetComponent<Graphic>();
            var color = target.color;
            color.a = tweenData.config.from;
            target.color = color;
            await target.DOFade(tweenData.config.to, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay).OnComplete(() => { tweenData.OnCompleted?.Invoke(); }).ToUniTask();
        }

        private static async UniTask PlayTweenMove(TweenData tweenData)
        {
            var target = tweenData.target;
            target.position = tweenData.config.mFrom;
            await target.DOMove(tweenData.config.mTo, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay).OnComplete(() => { tweenData.OnCompleted?.Invoke(); }).ToUniTask();
        }

        private static async UniTask PlayTweenLocalMove(TweenData tweenData)
        {
            var target = tweenData.target;
            target.localPosition = tweenData.config.mFrom;
            await target.DOLocalMove(tweenData.config.mTo, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay).OnComplete(() => { tweenData.OnCompleted?.Invoke(); });
        }

        private static async UniTask PlayTweenRectLocalMove(TweenData tweenData)
        {
            var target = (RectTransform)tweenData.target;
            target.anchoredPosition = tweenData.config.mFrom;
            await target.DOAnchorPos(tweenData.config.mTo, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay).OnComplete(() => { tweenData.OnCompleted?.Invoke(); });
        }


        private static async UniTask PlayTweenFadeGroup(TweenData tweenData)
        {
            var target = tweenData.target.GetComponent<CanvasGroup>();
            target.alpha = tweenData.config.from;
            Sequence sequence = DOTween.Sequence();
            sequence.Pause();
            sequence.Append(target.DOFade(tweenData.config.to, tweenData.config.duration).SetEase(tweenData.config.curve));
            sequence.SetDelay(tweenData.config.delay).OnComplete(() => { tweenData.OnCompleted?.Invoke(); });
            await sequence.Play();
        }

        private static async UniTask PlayTweenActive(TweenData tweenData)
        {
            tweenData.target.gameObject.SetActive(false);
            await DOVirtual.DelayedCall(tweenData.config.delay, () => { tweenData.target.gameObject.SetActive(true); }).ToUniTask();
        }
        
        private static async UniTask PlayTweenInActive(TweenData tweenData)
        {
            tweenData.target.gameObject.SetActive(true);
            await DOVirtual.DelayedCall(tweenData.config.delay, () => { tweenData.target.gameObject.SetActive(false); }).ToUniTask();
        }
    }


    public enum UITweenType
    {
        None,
        Scale,
        Fade,
        Move,
        LocalMove,
        RectLocalMove,
        FadeGroup,
        Active,
        Inactive,
    }
}