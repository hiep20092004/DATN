using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Lofelt.NiceVibrations;
using WaterFlow.Framework.Helper;
using WaterFlow.Framework.Utils;

namespace WaterFlow.Game
{
    public class ExpandPowerUpBehavior : PowerUpBehavior
    {
        private const string TIMER_UNIQUE_NAME = "Expand";
        private const float BoundChangeEpsilon = 0.001f;
        private const float DrillRotationYFromRightToLeft = 0f;
        private const float DrillRotationYFromBottomToTop = 90f;
        private const float DrillRotationYFromLeftToRight = 180f;
        private const float DrillRotationYFromTopToBottom = 270f;
        private const float ShakeAfterRepositionDelay = 0.05f;

        private IntDataPref delayCallHaptic;
        
        [SerializeField] private GameObject drillModel;
        [SerializeField] private SpriteRenderer mask;
        
        private ExpandPowerUpConfig expandConfig;
        private List<GameObject> targetVisuals = new List<GameObject>();

        // Owned handles so an in-flight expand can be cancelled if the level ends mid-animation
        // (e.g. skip-to-next-level), preventing stale callbacks from touching the destroyed level.
        private Tween expandBorderCall;
        private Tween finishAnimationCall;
        
        public override void Init()
        {
            expandConfig = (ExpandPowerUpConfig) PowerUpConfig; 
            SetPauseGameplayTimerWhenUse(true);
            drillModel.SetActive(false);
            delayCallHaptic = new IntDataPref("ExpandPowerUpBehavior_DelayCallHaptic", 250);
        }

        public override bool Activate()
        {
            return false;
        }

        public override bool IsHasAnyTarget()
        {
            LevelRepresentation levelRepresentation = LevelController.Instance?.LevelRepresentation;
            if (levelRepresentation?.LevelElements == null)
                return false;

            LevelEnvironmentSpawner environmentSpawner = levelRepresentation.EnvironmentSpawner;
            if (environmentSpawner == null)
                return false;

            LevelElementData[] elements = levelRepresentation.LevelElements;
            for (int i = 0; i < elements.Length; i++)
            {
                if (!(elements[i] is BorderLevelElementData border) || !border.IsExtendable)
                    continue;

                if (environmentSpawner.CanExpandExtendableBorder(border.Position))
                    return true;
            }

            return false;
        }

        public override bool OnSelected(out string notifyWhenNotFoundTarget)
        {
            SpawnTargetVisuals();
            bool hasTarget = targetVisuals.Count > 0;

            if (!hasTarget)
            {
                notifyWhenNotFoundTarget = config.GetOutOfTargetText();
                ResumeGameplayTimerOnUse();
                IsSelected = false;
                isDirty = true;
                if (mask)
                {
                    mask.DOKill();
                    Color maskColor = mask.color;
                    maskColor.a = 0f;
                    mask.color = maskColor;
                    mask.gameObject.SetActive(false);
                }
                ClearTargetVisuals();
                return false;
            }

            base.OnSelected(out notifyWhenNotFoundTarget);
            if (mask && hasTarget)
            {
                mask.gameObject.SetActive(true);
                Color maskColor = mask.color;
                maskColor.a = 0f;
                mask.color = maskColor;
                mask.DOFade(expandConfig.AlphaOnSelect, expandConfig.MaskFadeDuration).SetUpdate(true);
            }
            return hasTarget;
        }

        public override void OnDeselected()
        {
            base.OnDeselected();
            if (mask)
            {
                mask.DOKill();
                Color maskColor = mask.color;
                maskColor.a = 0f;
                mask.color = maskColor;
            }

            ClearTargetVisuals();
        }

        public override bool ApplyToElement(IClickableObject clickableObject, Vector3 clickPosition)
        {
            if (clickableObject is not ExpandTargetVisual expandTargetVisual)
                return false;

            var levelRepresentation = LevelController.Instance.LevelRepresentation;
            LevelEnvironmentSpawner environmentSpawner = levelRepresentation.EnvironmentSpawner;
            if (environmentSpawner == null)
                return false;

            if (!environmentSpawner.CanExpandExtendableBorder(expandTargetVisual.Position))
                return false;

            drillModel.SetActive(true);
            IsBusy = true;
            RaycastController.Disable("ExpandPowerUp");
            LevelController.Instance.GameplayTimer.Pause(TIMER_UNIQUE_NAME);
            
            UpdateDrillModel(expandTargetVisual);
            
            if (expandConfig.ActivateSound)
            {
                Services.AudioService.PlayAudio(string.Empty, expandConfig.ActivateSound);
            }

            FrameworkUtils.DelayCall(delayCallHaptic.Value / 1000f, () => 
            WaterFlow.Core.HapticFeedback.Play(WaterFlow.Core.HapticType.Expand));
            
            expandBorderCall = DOVirtual.DelayedCall(expandConfig.DelayExpandBorder, () =>
            {
                // The level may have been swapped/torn down while the drill was animating
                // (e.g. skip-to-next-level); spawning borders now would build them against the
                // old, destroyed level transform and leak orphaned tiles into the new level.
                if (LevelController.Instance == null ||
                    LevelController.Instance.LevelRepresentation != levelRepresentation ||
                    levelRepresentation.LevelTransform == null)
                    return;

                Bounds boundsBeforeExpand = levelRepresentation.LevelBounds;
                bool expanded = environmentSpawner.TryExpandExtendableBorder(expandTargetVisual.Position);
                if (!expanded)
                    return;
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);

                Bounds boundsAfterExpand = levelRepresentation.LevelBounds;
                bool isChangeBound = IsBoundChanged(boundsBeforeExpand, boundsAfterExpand);
                _ = levelRepresentation.RepositionCamera();

                if (!isChangeBound)
                {
                    var camera = LevelController.Instance.CameraController;
                    camera.StartShake(expandConfig.ShakeDuration, expandConfig.ShakeStrength);
                }

            },false);
            finishAnimationCall = DOVirtual.DelayedCall(expandConfig.AnimationDuration, FinishExpandAnimation, false);
            return true;
        }

        private void FinishExpandAnimation()
        {
            expandBorderCall = null;
            finishAnimationCall = null;

            if (drillModel)
                drillModel.SetActive(false);
            IsBusy = false;
            RaycastController.Enable("ExpandPowerUp");
            LevelController.Instance?.GameplayTimer?.Resume(TIMER_UNIQUE_NAME);
        }

        public override void OnLevelEnded()
        {
            base.OnLevelEnded();

            // Level ended (e.g. skip to next level) while an expand may still be animating.
            // Kill the pending delayed calls so they can't run against the torn-down level,
            // then restore raycast/timer/busy state the AnimationDuration callback would have.
            if (expandBorderCall != null || finishAnimationCall != null || IsBusy)
            {
                expandBorderCall?.Kill();
                expandBorderCall = null;
                finishAnimationCall?.Kill();
                finishAnimationCall = null;
                FinishExpandAnimation();
            }

            ClearTargetVisuals();
        }

        private void UpdateDrillModel(ExpandTargetVisual expandTargetVisual)
        {
            LevelEnvironmentSpawner environmentSpawner = LevelController.Instance.LevelRepresentation?.EnvironmentSpawner;

            Vector2Int targetPosition = expandTargetVisual.Position;
            Vector3 drillPosition = new Vector3(targetPosition.x, 0.5f, targetPosition.y + expandConfig.DrillSpawnOffSet);
            float drillRotationY = DrillRotationYFromTopToBottom;

            if (environmentSpawner != null)
            {
                if (!environmentSpawner.HasAdjacentBorder(targetPosition, Vector2Int.down))
                {
                    drillPosition = new Vector3(targetPosition.x, 0.5f, targetPosition.y - expandConfig.DrillSpawnOffSet);
                    drillRotationY = DrillRotationYFromBottomToTop;
                }
                else if (!environmentSpawner.HasAdjacentBorder(targetPosition, Vector2Int.up))
                {
                    drillPosition = new Vector3(targetPosition.x, 0.5f, targetPosition.y + expandConfig.DrillSpawnOffSet);
                    drillRotationY = DrillRotationYFromTopToBottom;
                }
                else if (!environmentSpawner.HasAdjacentBorder(targetPosition, Vector2Int.left))
                {
                    drillPosition = new Vector3(targetPosition.x - expandConfig.DrillSpawnOffSet, 0.5f, targetPosition.y);
                    drillRotationY = DrillRotationYFromLeftToRight;
                }
                else if (!environmentSpawner.HasAdjacentBorder(targetPosition, Vector2Int.right))
                {
                    drillPosition = new Vector3(targetPosition.x + expandConfig.DrillSpawnOffSet, 0.5f, targetPosition.y);
                    drillRotationY = DrillRotationYFromRightToLeft;
                }
            }

            drillModel.transform.position = drillPosition;
            Vector3 localEulerAngles = drillModel.transform.localEulerAngles;
            localEulerAngles.y = drillRotationY;
            drillModel.transform.localEulerAngles = localEulerAngles;
        }
        
        private void SpawnTargetVisuals()
        {
            ClearTargetVisuals();
            if (!expandConfig.TargetVisual)
                return;

            LevelRepresentation levelRepresentation = LevelController.Instance.LevelRepresentation;
            if (levelRepresentation?.LevelElements == null)
                return;

            LevelEnvironmentSpawner environmentSpawner = levelRepresentation.EnvironmentSpawner;
            if (environmentSpawner == null)
                return;

            LevelElementData[] elements = levelRepresentation.LevelElements;
            for (int i = 0; i < elements.Length; i++)
            {
                if (!(elements[i] is BorderLevelElementData border) || !border.IsExtendable)
                    continue;

                if (!environmentSpawner.CanExpandExtendableBorder(border.Position))
                    continue;

                Vector3 worldPosition = new Vector3(border.Position.x, 0f, border.Position.y);
                GameObject targetVisualObject = Object.Instantiate(expandConfig.TargetVisual, worldPosition,
                    Quaternion.identity, levelRepresentation.LevelTransform);
                targetVisuals.Add(targetVisualObject);

                ExpandTargetVisual targetVisual = targetVisualObject.GetComponent<ExpandTargetVisual>();
                if (targetVisual == null)
                {
                    targetVisual = targetVisualObject.GetComponentInChildren<ExpandTargetVisual>(true);
                }

                if (targetVisual == null)
                {
                    targetVisual = targetVisualObject.AddComponent<ExpandTargetVisual>();
                }

                targetVisual.Init(border.Position);
            }
        }

        private void ClearTargetVisuals()
        {
            for (int i = 0; i < targetVisuals.Count; i++)
            {
                GameObject targetVisual = targetVisuals[i];
                if (!targetVisual)
                    continue;

                Object.Destroy(targetVisual);
            }

            targetVisuals.Clear();
        }

        private bool IsBoundChanged(Bounds oldBounds, Bounds newBounds)
        {
            return Vector3.SqrMagnitude(oldBounds.center - newBounds.center) > BoundChangeEpsilon ||
                   Vector3.SqrMagnitude(oldBounds.size - newBounds.size) > BoundChangeEpsilon;
        }

    }
}