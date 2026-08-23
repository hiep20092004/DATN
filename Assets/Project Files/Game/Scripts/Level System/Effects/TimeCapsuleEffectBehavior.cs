using WaterFlow.Core;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class TimeCapsuleEffectBehavior : BlockEffectBehavior<TimeCapsuleBlockEffectData>
    {
        [SerializeField] TimeCapsuleUIView capsuleUIPrefab;
        [SerializeField] Transform capsuleVisuals;
        [SerializeField] TextMeshProUGUI bonusText;
        [SerializeField] float offsetY = 0.5f;

        private bool timeGranted;

        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            Bounds bounds = blockBehavior.Figure.GetHorizontalCenterBounds();
            transform.position = blockBehavior.transform.position + bounds.center
                                 + new Vector3(0, offsetY * orderID, 0);

            if (capsuleVisuals)
                capsuleVisuals.SetParent(blockBehavior.ModelParentTransform, true);

            if (bonusText)
                bonusText.text = $"+{Data.timeBonus}";

            timeGranted = false;
        }

        public override void OnBlockFullAfterAnimationFilled()
        {
            if (timeGranted || Data == null)
                return;
            timeGranted = true;

            int bonus = Data.timeBonus;
            UIGame uiGame = UIController.GetPage<UIGame>();
            RectTransform timerTarget = uiGame.TimerVisualiser ? uiGame.TimerVisualiser.TimerTargetRect : null;
            if (capsuleUIPrefab && capsuleVisuals && timerTarget)
            {
                Vector2 startLocalPos = uiGame.GetItemOverlaysLocalPoint(capsuleVisuals.position);
                Vector2 targetLocalPos = uiGame.GetItemOverlaysLocalPointFromUI(timerTarget);
                
                TimeCapsuleUIView capsuleView = Instantiate(capsuleUIPrefab, uiGame.ItemOverlays);
                capsuleView.SetBonusText(bonus);
                capsuleView.PlayFlyTo(startLocalPos,  targetLocalPos, () => LevelController.Instance.AddTime(bonus));
                
                capsuleVisuals.gameObject.SetActive(false);
            }
            else
            {
                LevelController.Instance.AddTime(bonus);
            }
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            if (disableSource == DisableSource.Hammer)
                timeGranted = true;

            if (capsuleVisuals)
                Destroy(capsuleVisuals.gameObject);
        }

        protected override void ApplyVisualState(bool visible)
        {
            base.ApplyVisualState(visible);
            if (capsuleVisuals)
                capsuleVisuals.gameObject.SetActive(visible);
        }
        
        public override BlockEffectData GetCurrentEffectData()
        {
            int bonus = Data?.timeBonus ?? 0;
            return new TimeCapsuleBlockEffectData { timeBonus = bonus };
        }
    }
}
