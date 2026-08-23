using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    public class FreezeTimerPowerUpBehavior : PowerUpBehavior
    {
        public static float TIMER_DELAY = 0.4f;
        private const string TIMER_UNIQUE_NAME = "freezeTime";
        
        private PUTimer timer;
        private TweenCase startDelayTweenCase;

        private FreezeTimerPowerUpConfig timerPowerUpConfig;

        private TimerVisualiser timerVisualiser;
        private bool updateTimeVisual = false;
        
        public override void Init()
        {
            timerPowerUpConfig = (FreezeTimerPowerUpConfig)PowerUpConfig;
            
            timer = null;
            
            SetPauseGameplayTimerWhenUse(true);
        }

        public override bool IsActive()
        {
            return true;
        }

        public override bool Activate()
        {
            UIGame uiGame = UIController.GetPage<UIGame>();
            if (!uiGame)
                return false;

            LevelController levelController = LevelController.Instance;
            if (levelController?.GameplayTimer == null)
                return false;

            IsBusy = true;
            timerVisualiser = uiGame.TimerVisualiser;

            startDelayTweenCase?.KillActive();
            startDelayTweenCase = null;

            levelController.GameplayTimer.Pause(TIMER_UNIQUE_NAME);
            timer = new PUTimer(timerPowerUpConfig.TimeFreezeDuration, OnFreezeTimerCompleted);
            timer.Pause();
            updateTimeVisual = false;
            startDelayTweenCase = Tween.DelayedCall(TIMER_DELAY, OnStartDelayCompleted);

            if (timerPowerUpConfig.ActivateSound)
            {
                Services.AudioService.PlayAudio(string.Empty, timerPowerUpConfig.ActivateSound);
            }

            return true;
        }

        private void OnStartDelayCompleted()
        {
            startDelayTweenCase = null;
            if (timer == null)
                return;

            timer.Resume();
            updateTimeVisual = true;
        }

        private void OnFreezeTimerCompleted()
        {
            timer = null;
            IsBusy = false;
            startDelayTweenCase?.KillActive();
            startDelayTweenCase = null;

            LevelController levelController = LevelController.Instance;
            levelController?.GameplayTimer?.Resume(TIMER_UNIQUE_NAME);
        }

        public override void OnTimerTick()
        {
            if (!timerVisualiser) return;

            if (timer != null && updateTimeVisual)
            {
                timerVisualiser.SetFreezeFillAmount(1 - timer.State);
            }
            else
            {
                timerVisualiser.SetFreezeFillAmount(0);
            }
        }

        public override void OnLevelLoaded(int level)
        {
            base.OnLevelLoaded(level);
            if (timerVisualiser)
                timerVisualiser.SetFreezeFillAmount(0);
        }

        public override void OnLevelEnded()
        {
            ResetBehavior();
            if (timerVisualiser)
                timerVisualiser.SetFreezeFillAmount(0);
        }

        public override void ResetBehavior()
        {
            startDelayTweenCase?.KillActive();
            startDelayTweenCase = null;

            if (timer != null)
            {
                timer.Disable();
                timer = null;
            }

            IsBusy = false;
            updateTimeVisual = false;

            LevelController levelController = LevelController.Instance;
            levelController?.GameplayTimer?.Resume(TIMER_UNIQUE_NAME);
        }

        public override PUTimer GetTimer()
        {
            return timer;
        }

        public override bool IsSelectTargetType()
        {
            return false;
        }

        public override bool ApplyToElement(IClickableObject clickableObject, Vector3 clickPosition) => false;
    }
}