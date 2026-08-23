using System;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Game
{
    
    public class ChainGateEffectBehavior : GateEffectBehavior, IChainElement, IKeyMovementAction
    {
        [SerializeField] private ParticleSystem lockCollideFx;
        [SerializeField] private ParticleSystem chainUnlockFx;
        
        private int keysAmount;
        private int visualKeysAmount;
        
        private bool selfDestruct;
        private ChainVisualsBehavior chainVisualsBehavior;
        private ChainGateEffectConfig config;
        
        public override void OnCreated(GateBehavior gateBehavior)
        {
            keysAmount = ((ChainGateEffectData)data).keysAmount;
            visualKeysAmount = keysAmount;

            if (Application.isPlaying && keysAmount <= 0)
            {
                selfDestruct = true;
                DisableEffect();
                return;
            }
            
            config = GetConfig<ChainGateEffectConfig>();
            GateDirection.Type currentGateDirection = gateBehavior.Data.GateDirectionType;
            var chainVisual = config.GetChainVisual(currentGateDirection);
            if (chainVisual)
            {
                chainVisualsBehavior = Instantiate(chainVisual, this.transform);
                chainVisualsBehavior.transform.SetParent(gateBehavior.transform, true);
            }
            else
            {
                throw new Exception($"ChainVisualsBehavior component not found!.");
            }
            
            chainVisualsBehavior.AmountText.text = keysAmount.ToString();
            ChainManager.RegisterElement(this);
            
            if(lockCollideFx) lockCollideFx.gameObject.SetActive(false);
            if(chainUnlockFx) chainUnlockFx.gameObject.SetActive(false);
        }

        public override void OnDisabled(GateBehavior gateBehavior)
        {
            if(selfDestruct) return;
            ChainManager.UnregisterElement(this);
            if (keysAmount > 0)
            {
                int count = 0;
                var activeBlocks = LevelController.Instance.LevelRepresentation.ActiveBlocks;
                foreach (LevelBlockBehavior block in activeBlocks)
                {
                    foreach (BlockEffectBehavior effect in block.Effects)
                    {
                        if(count >= keysAmount) break;
                        if (effect.IsActive && effect is KeyMultiBlockEffectBehavior)
                        {
                            effect.DisableEffect();
                            count++;
                        }
                    }
                }
                Services.AudioService.PlaySound(AudioId.Obstacle_LocknKey_Unlock);
            }
            
            if (chainUnlockFx)
            {
                var o = chainUnlockFx.gameObject;
                o.SetActive(true);
                o.transform.position = IsTargetAvailable ? TargetPosition + new Vector3(0, 0.5f, 0) : Vector3.zero;
                o.transform.rotation = Quaternion.Euler(Vector3.zero);
                o.transform.SetParent(null, true);
                chainUnlockFx.Play();
                Destroy(o, 1f);
            }
            
            if (chainVisualsBehavior.gameObject)
            {
                Destroy(chainVisualsBehavior.gameObject, config.TimeDelayDestroyVisual);
            }
            
        }

        public override BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            return keysAmount > 0 ? BlockGateState.GateChainLocked : BlockGateState.Enterable;
        }
        
        public override void OnRevived(LoseReason loseReason, int seconds)
        {
            if (loseReason != LoseReason.ChainGateLockFailed)
                return;
            DisableEffect();
        }

        public override GateEffectData GetCurrentEffectData()
        {
            // Keys already linked (in flight) have consumed their key blocks; snapshot the linked-adjusted count.
            return new ChainGateEffectData { keysAmount = keysAmount };
        }

        public int KeysLeft => keysAmount;
        public bool IsTargetAvailable => chainVisualsBehavior;
        public Vector3 TargetPosition => chainVisualsBehavior.LockCenter.position;
        public void OnKeyLinked()
        {
            keysAmount--;
        }

        public void OnKeyReached()
        {
            Services.AudioService.PlaySound(AudioId.Obstacle_LocknKey);
            
            if (lockCollideFx)
            {
                var o = lockCollideFx.gameObject;
                o.SetActive(true);
                o.transform.position = TargetPosition + new Vector3(0, 0.5f, 0);
                lockCollideFx.Play();
            }
        }

        public void OnKeyFinished()
        {
            visualKeysAmount--;

            chainVisualsBehavior.AmountText.text = visualKeysAmount.ToString();

            if (visualKeysAmount <= 0)
            {
                Services.AudioService.PlaySound(AudioId.Obstacle_LocknKey_Unlock);
                DisableEffect();
            }
        }
    }
}