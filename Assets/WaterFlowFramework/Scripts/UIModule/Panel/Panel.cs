using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.EventBus;
using UnityEngine;

namespace WaterFlow.Framework.UIModule
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class Panel : View
    {
        public TweenData[] openTween;
        public TweenData[] closeTween;
        protected CanvasGroup panelCanvasGroup;


        protected virtual void Reset()
        {
            panelCanvasGroup = GetComponent<CanvasGroup>();
        }

        public override void OnSetup()
        {
            if (panelCanvasGroup == null) panelCanvasGroup = GetComponent<CanvasGroup>();
        }

        public override void Open(UIData uiData)
        {
            this.uiData = uiData;
            gameObject.SetActive(true);
            panelCanvasGroup.interactable = false;
            if (pauseGame)
            {
                EventBus<GamePausedEvent>.Raise(new GamePausedEvent()
                {
                    paused =  true,
                    reason = GetType().Name
                });
            }
            PlayTweens(openTween, OnOpenCompleted).Forget();
        }

        public override void OnOpenCompleted()
        {
            if(panelCanvasGroup)
            {
                panelCanvasGroup.interactable = true;
            }
        }

        //TODO: override this method must call OnCloseCompleted() at the end
        public override void Close()
        {
            if (panelCanvasGroup)
            {
                panelCanvasGroup.interactable = false;
                PlayTweens(closeTween, OnCloseCompleted).Forget();
            }
        }

        protected override void OnCloseCompleted()
        {
            base.OnCloseCompleted();
            if (pauseGame)
            {
                EventBus<GamePausedEvent>.Raise(new GamePausedEvent()
                {
                    paused =  false,
                    reason = GetType().Name
                });
            }
            if (uiData != null && uiData.TryGet<Action>(UIDataKey.CallBackOnClose, out var callback))
                callback?.Invoke();
        }

        public override void OnFocus()
        {
        }

        public override void OnFocusLost()
        {
        }

        private async UniTask PlayTweens(TweenData[] tweenDatas, Action callback)
        {
            if (tweenDatas == null)
            {
                callback?.Invoke();
                return;
            }

            float maxTime = 0;
            for (var i = 0; i < tweenDatas.Length; i++)
            {
                UITween.Play(tweenDatas[i]).Forget();
                if (tweenDatas[i].config.delay + tweenDatas[i].config.duration > maxTime)
                    maxTime = tweenDatas[i].config.delay + tweenDatas[i].config.duration;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(maxTime));
            callback?.Invoke();
        }

        public void OnGamePause()
        {
            
        }

        public void OnGameResume()
        {
            
        }
    }
}