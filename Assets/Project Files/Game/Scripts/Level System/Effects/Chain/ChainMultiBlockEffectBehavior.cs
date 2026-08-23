using System;
using System.Collections.Generic;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Game
{
    public sealed class ChainMultiBlockEffectBehavior : BlockEffectBehavior<ChainBlockEffectData> , IChainElement, IKeyMovementAction
    {
        [SerializeField] private ParticleSystem lockCollideFx;
        [SerializeField] private ParticleSystem chainUnlockFx;
        
        private int keysAmount;
        private int visualKeysAmount;
        private ChainVisualsBehavior chainVisualsBehavior;
        private GameObject chainVisuals;
        private ChainMultiBlockEffectConfig config;
        
        public int KeysLeft => keysAmount;
        
        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            config = GetConfig<ChainMultiBlockEffectConfig>();
            if (!config)
            {
                Debug.LogError($"Chain multi block effect config is not assigned on {gameObject.name}.", gameObject);
                DisableEffect();
                return;
            }

            keysAmount = Data.keysAmount;
            visualKeysAmount = keysAmount;
            
            BlockType blockType = blockBehavior.BlockConfig.Type;
            if (!config.TryGetChainVisualsData(blockType, out ChainVisualsData visualsData))
            {
                Debug.LogError($"Chain visuals data not found for element type: {blockType} in {gameObject.name}.", gameObject);
                DisableEffect();
                return;
            }
            
            chainVisuals = Instantiate(visualsData.VisualsPrefab, blockBehavior.ModelParentTransform);
            chainVisuals.transform.localPosition = new Vector3(0, config.OffsetY * orderID, 0);
            
            chainVisualsBehavior = chainVisuals.GetComponent<ChainVisualsBehavior>();
            if (!chainVisualsBehavior)
            {
                throw new Exception($"ChainVisualsBehavior component not found in {visualsData.VisualsPrefab.name} prefab.");
            }
            chainVisualsBehavior.AmountText.text = keysAmount.ToString();
            ChainManager.RegisterElement(this);
            
            if(lockCollideFx) lockCollideFx.gameObject.SetActive(false);
            if(chainUnlockFx) chainUnlockFx.gameObject.SetActive(false);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            ChainManager.UnregisterElement(this);

            if (config == null || !chainVisualsBehavior)
            {
                if (chainVisuals)
                    Destroy(chainVisuals, 0.1f);
                return;
            }
            

            if (keysAmount > 0)
            {
                int count = 0;
                List<LevelBlockBehavior> activeBlocks = LevelController.Instance.LevelRepresentation.ActiveBlocks;
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
                o.transform.position = TargetPosition + new Vector3(0, config.OffsetY, 0);
                o.transform.SetParent(blockBehavior.transform, true);
                chainUnlockFx.Play();
                Destroy(o, 1f);
            }
            
            if (chainVisuals)
            {
                Destroy(chainVisuals, 0.1f);
            }
        }

        public override bool IsClickable()
        {
            return false;
        }

        public override AudioId GetOverrideClickAudioId()
        {
            return AudioId.Click_obs_locked;
        }

        public override BlockEffectData GetCurrentEffectData()
        {
            // Keys already linked (in flight) have consumed their key blocks; snapshot the linked-adjusted count.
            return new ChainBlockEffectData { keysAmount = keysAmount };
        }

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
                o.transform.position = TargetPosition + new Vector3(0, config.OffsetY, 0);
                o.transform.SetParent(linkedBlock.transform, true);
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
