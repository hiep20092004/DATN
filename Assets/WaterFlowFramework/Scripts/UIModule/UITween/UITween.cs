using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Framework.UIModule
{
    public static class UITween
    {
        public static async UniTask Play(TweenData tweenData, CancellationToken cancellationToken = default)
        {
            if (tweenData == null) return;
            tweenData.SetupData();
            if (tweenData.config == null || !tweenData.target) return;

            try
            {
                switch (tweenData.config.tweenType)
                {
                    case UITweenType.Scale:
                        await PlayTweenScale(tweenData, cancellationToken);
                        break;
                    case UITweenType.Fade:
                        await PlayTweenFade(tweenData, cancellationToken);
                        break;
                    case UITweenType.Move:
                        await PlayTweenMove(tweenData, cancellationToken);
                        break;
                    case UITweenType.LocalMove:
                        await PlayTweenLocalMove(tweenData, cancellationToken);
                        break;
                    case UITweenType.RectLocalMove:
                        await PlayTweenRectLocalMove(tweenData, cancellationToken);
                        break;
                    case UITweenType.FadeGroup:
                        await PlayTweenFadeGroup(tweenData, cancellationToken);
                        break;
                    case UITweenType.Active:
                        await PlayTweenActive(tweenData, cancellationToken);
                        break;
                    case UITweenType.Inactive:
                        await PlayTweenInActive(tweenData, cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async UniTask PlayTweenScale(TweenData tweenData, CancellationToken cancellationToken)
        {
            var target = tweenData.target;
            target.localScale = Vector3.one * tweenData.config.from;
            await target.DOScale(tweenData.config.to, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay)
                .SetLink(target.gameObject)
                .OnComplete(() => { tweenData.OnCompleted?.Invoke(); })
                .ToUniTask(cancellationToken: cancellationToken);
        }

        private static async UniTask PlayTweenFade(TweenData tweenData, CancellationToken cancellationToken)
        {
            var target = tweenData.target.GetComponent<Graphic>();
            if (!target) return;
            var color = target.color;
            color.a = tweenData.config.from;
            target.color = color;
            await target.DOFade(tweenData.config.to, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay)
                .SetLink(target.gameObject)
                .OnComplete(() => { tweenData.OnCompleted?.Invoke(); })
                .ToUniTask(cancellationToken: cancellationToken);
        }

        private static async UniTask PlayTweenMove(TweenData tweenData, CancellationToken cancellationToken)
        {
            var target = tweenData.target;
            target.position = tweenData.config.mFrom;
            await target.DOMove(tweenData.config.mTo, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay)
                .SetLink(target.gameObject)
                .OnComplete(() => { tweenData.OnCompleted?.Invoke(); })
                .ToUniTask(cancellationToken: cancellationToken);
        }

        private static async UniTask PlayTweenLocalMove(TweenData tweenData, CancellationToken cancellationToken)
        {
            var target = tweenData.target;
            target.localPosition = tweenData.config.mFrom;
            await target.DOLocalMove(tweenData.config.mTo, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay)
                .SetLink(target.gameObject)
                .OnComplete(() => { tweenData.OnCompleted?.Invoke(); })
                .ToUniTask(cancellationToken: cancellationToken);
        }

        private static async UniTask PlayTweenRectLocalMove(TweenData tweenData, CancellationToken cancellationToken)
        {
            var target = (RectTransform)tweenData.target;
            target.anchoredPosition = tweenData.config.mFrom;
            await target.DOAnchorPos(tweenData.config.mTo, tweenData.config.duration).SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay)
                .SetLink(target.gameObject)
                .OnComplete(() => { tweenData.OnCompleted?.Invoke(); })
                .ToUniTask(cancellationToken: cancellationToken);
        }


        private static async UniTask PlayTweenFadeGroup(TweenData tweenData, CancellationToken cancellationToken)
        {
            var target = tweenData.target.GetComponent<CanvasGroup>();
            if (!target) return;
            target.alpha = tweenData.config.from;
            await target.DOFade(tweenData.config.to, tweenData.config.duration)
                .SetEase(tweenData.config.curve)
                .SetDelay(tweenData.config.delay)
                .SetLink(target.gameObject)
                .OnComplete(() => { tweenData.OnCompleted?.Invoke(); })
                .ToUniTask(cancellationToken: cancellationToken);
        }

        private static async UniTask PlayTweenActive(TweenData tweenData, CancellationToken cancellationToken)
        {
            tweenData.target.gameObject.SetActive(false);
            var target = tweenData.target;
            await DOVirtual.DelayedCall(tweenData.config.delay, () =>
                {
                    if (target) target.gameObject.SetActive(true);
                })
                .SetLink(target.gameObject)
                .ToUniTask(cancellationToken: cancellationToken);
        }
        
        private static async UniTask PlayTweenInActive(TweenData tweenData, CancellationToken cancellationToken)
        {
            tweenData.target.gameObject.SetActive(true);
            var target = tweenData.target;
            await DOVirtual.DelayedCall(tweenData.config.delay, () =>
                {
                    if (target) target.gameObject.SetActive(false);
                })
                .SetLink(target.gameObject)
                .ToUniTask(cancellationToken: cancellationToken);
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
