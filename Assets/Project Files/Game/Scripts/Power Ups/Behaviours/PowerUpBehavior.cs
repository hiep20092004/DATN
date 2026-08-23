using DG.Tweening;
using UnityEngine;

namespace WaterFlow.Game
{
    public abstract class PowerUpBehavior : MonoBehaviour
    {
        private bool pauseGameplayTimerWhenUse;
        protected BoosterConfig config;
        public BoosterConfig Config => config;
        public BasePowerUpConfig PowerUpConfig => config.GetPowerUpConfig();
        
        private bool isBusy;
        public bool IsBusy 
        {
            get => isBusy;
            protected set 
            { 
                isBusy = value; 
                isDirty = true;
            }
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            protected set
            {
                isSelected = value;
                isDirty = true;

                SelectStateChanged?.Invoke(isSelected);
            }
        }

        protected bool isDirty = true;
        public bool IsDirty => isDirty;

        public event SimpleBoolCallback SelectStateChanged;

        public void InjectConfig(BoosterConfig config)
        {
            this.config = config;
        }

        public abstract void Init();
        public abstract bool Activate();

        public virtual bool ApplyToElement(IClickableObject clickableObject, Vector3 clickPosition) { return false; }

        public virtual void OnLevelLoaded(int level)
        {
            if (Services.BoosterService.ShouldShowUnlockNotifyPopup(Config, level))
            {
                DOVirtual.DelayedCall(LevelController.Instance.LevelRepresentation.EnvironmentData.GetTotalAnimationTime(), () =>
                {
                    PowerUpUnlockNotifyPopup.Show(Config);
                }, false);
            }
        }
        public virtual void OnLevelEnded() { }

        public virtual bool OnSelected(out string notifyWhenNotFoundTarget)
        {
            isSelected = true;
            isDirty = true;

            SelectStateChanged?.Invoke(true);
            PauseGameplayTimerOnUse();
            notifyWhenNotFoundTarget = config.GetOutOfTargetText();
            return true;
        }

        public virtual void OnDeselected()
        {
            isSelected = false;
            isDirty = true;

            SelectStateChanged?.Invoke(false);
            ResumeGameplayTimerOnUse();
        }

        public virtual bool IsActive() => true;
        public virtual bool IsSelectTargetType() => true;

        /// <summary>Whether the level currently offers at least one valid use for this booster (navigation / hints).</summary>
        public virtual bool IsHasAnyTarget() => true;

        // public virtual string GetFloatingMessage()
        // {
        //     return settings.FloatingMessage;
        // }

        public virtual PUTimer GetTimer()
        {
            return null;
        }

        public virtual void OnTimerTick()
        {

        }

        public virtual void ResetBehavior()
        {

        }

        public void SetDirty()
        {
            isDirty = true;
        }

        public void OnRedrawn()
        {
            isDirty = false;
        }
        
        protected void SetPauseGameplayTimerWhenUse(bool shouldPause)
        {
            pauseGameplayTimerWhenUse = shouldPause;
        }

        protected void PauseGameplayTimerOnUse()
        {
            if (!pauseGameplayTimerWhenUse)
            {
                return;
            }

            LevelController.Instance.GameplayTimer.Pause(GetPauseTimerKey());
        }

        protected void ResumeGameplayTimerOnUse()
        {
            if (!pauseGameplayTimerWhenUse)
            {
                return;
            }

            LevelController.Instance.GameplayTimer.Resume(GetPauseTimerKey());
        }

        private string GetPauseTimerKey()
        {
            return $"PowerUp_{Config.type}";
        }
    }
}