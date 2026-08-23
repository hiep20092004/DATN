using UnityEngine;

namespace WaterFlow.Game
{
    public class MovingGateLockEffectBehavior : GateEffectBehavior
    {
        public MovingGateLockVisual lockVisual;
        public MovingGateLockVisual lockTopVisual;

        private GateBehavior currentGate;
        private int currentGateBlockId;
        private bool isClockwise;
        private bool isUsingTopVisual;
        private int lastProcessedFilledFrame = -1;
        private MovingGateLockConfig config;
        
        public override void OnCreated(GateBehavior gateBehavior)
        {
            currentGate = gateBehavior;
            currentGateBlockId = gateBehavior.Data?.LevelElementData?.BlockId ?? 0;
            isClockwise = data is MovingLockGateEffectData movingData && movingData.isClockwise;
            isUsingTopVisual = IsTopGate(currentGate);

            config = GetConfig<MovingGateLockConfig>();

            if (lockVisual)
            {
                lockVisual = Instantiate(lockVisual, gateBehavior.PipeTransform);
                lockVisual.Init(currentGate, isClockwise, config, !isUsingTopVisual);
                lockVisual.gameObject.SetActive(!isUsingTopVisual);
            }

            if (lockTopVisual)
            {
                lockTopVisual = Instantiate(lockTopVisual, gateBehavior.PipeTransform);
                lockTopVisual.Init(currentGate, isClockwise, config, isUsingTopVisual);
                lockTopVisual.gameObject.SetActive(isUsingTopVisual);
            }
        }

        public override BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            BlockColor gateColor = linkedGate != null ? linkedGate.GetActiveColor() : BlockColor.None;
            var matchGateColor = levelBlockBehavior.CanMatchGateColor(gateColor);
            return matchGateColor == BlockGateState.Enterable
                ? BlockGateState.GateMovingLockMatchColor
                : BlockGateState.GateMovingLock;
        }

        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if (loseReason != LoseReason.MovingGateLockStuck)
                return;
            TryAdvanceToNextGate();
        }

        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            if (!currentGate)
                return;

            // This callback is invoked by iterating all gates. If we transfer during the iteration,
            // the same effect could be hit again under the next gate in the same frame.
            if (lastProcessedFilledFrame == Time.frameCount)
                return;
            lastProcessedFilledFrame = Time.frameCount;

            TryAdvanceToNextGate();
        }

        private void TryAdvanceToNextGate()
        {
            var cache = LevelController.Instance != null ? LevelController.Instance.RuntimePrecomputedCache : null;
            if (cache == null)
                return;

            if (!cache.TryGetNextGateInPerimeterOrder(currentGateBlockId, isClockwise, out GateBehavior nextGate))
                return;

            if (!nextGate || nextGate == currentGate)
                return;

            bool nextIsTop = IsTopGate(nextGate);

            // Move logic immediately so target gate state is correct before visuals arrive.
            TransferTo(nextGate);

            if (nextIsTop == isUsingTopVisual)
            {
                GetActiveVisual()?.ChangeToNewGate(nextGate);
            }
            else
            {
                MovingGateLockVisual oldVisual = GetActiveVisual();
                MovingGateLockVisual newVisual = nextIsTop ? lockTopVisual : lockVisual;

                oldVisual?.ReleaseGateVisuals();

                if (newVisual)
                {
                    newVisual.gameObject.SetActive(true);
                    // Start at the old gate position without toggling arrow again (oldVisual already restored it).
                    newVisual.SnapToGate(currentGate, affectGateVisuals: false);
                    newVisual.ChangeToNewGate(nextGate);
                }

                if (oldVisual)
                    oldVisual.gameObject.SetActive(false);

                isUsingTopVisual = nextIsTop;
            }

            currentGate = nextGate;
            int nextId = nextGate.Data?.LevelElementData?.BlockId ?? 0;
            if (nextId != 0)
                currentGateBlockId = nextId;
        }

        private MovingGateLockVisual GetActiveVisual()
        {
            return isUsingTopVisual ? lockTopVisual : lockVisual;
        }

        private static bool IsTopGate(GateBehavior gateBehavior)
        {
            return gateBehavior != null && gateBehavior.Data != null &&
                   gateBehavior.Data.GateDirectionType == GateDirection.Type.Top;
        }
    }
}

