using System.Collections.Generic;
using System.Linq;
using WaterFlow.Enums;
using WaterFlow.Core;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class RopeEffectBehavior : BlockEffectBehavior<RopesBlockEffectData>
    {
        [SerializeField] ParticleSystem cutParticle;

        private List<RopeBehavior> ropes = new List<RopeBehavior>();
        public List<RopeBehavior> Ropes => ropes;


        public override void OnCreated(LevelBlockBehavior blockBehavior)
        {
            if (!ValidateDataOrDisable())
                return;

            RopeEffectConfig config = GetConfig<RopeEffectConfig>();
            if (config == null)
            {
                Debug.LogError($"[{nameof(RopeEffectBehavior)}] Missing {nameof(RopeEffectConfig)} on {gameObject.name}.", gameObject);
                DisableEffect();
                return;
            }

            BlockColor[] ropesColors = Data.ropesColors;

            RopePositionData positionData = config.GetPositionData(linkedBlock.BlockConfig.Type);
            if (positionData == null)
            {
                DisableEffect();
                return;
            }

            if (ropesColors.Length > positionData.TransformDatas.Length)
            {
                Debug.LogError($"Ropes amount is more than available positions for element type: {linkedBlock.BlockConfig.Type} in {gameObject.name}.", gameObject);

                DisableEffect();

                return;
            }

            Bounds bounds = blockBehavior.Figure.GetHorizontalCenterBounds();
            transform.position = blockBehavior.transform.position + bounds.center;

            for (int i = 0; i < ropesColors.Length; i++)
            {
                GameObject ropeObject = Instantiate(config.RopePrefab, transform);

                RopeBehavior ropeBehavior = ropeObject.GetComponent<RopeBehavior>();
                ropeBehavior.Init(this, positionData.TransformDatas[i], ropesColors[i]);

                ropes.Add(ropeBehavior);
            }

            this.transform.SetParent(blockBehavior.ModelParentTransform, true);
            RopesManager.RegisterElement(this);
        }

        public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
        {
            RopesManager.UnregisterElement(this);
        }

        public override bool IsClickable()
        {
            return false;
        } 
        
        public void OnRopeCut(RopeBehavior ropeBehavior)
        {
            if (cutParticle)
            {
                ParticleSystem.MainModule mainParticle = cutParticle.main;
                Color particleStartColor = ropeBehavior.ColorData.Material.color;
                mainParticle.startColor = particleStartColor;
                
                // update position
                var transformPosition = ropeBehavior.transform.position;
                transformPosition.y = cutParticle.transform.position.y;
                cutParticle.transform.position = transformPosition;
                cutParticle.gameObject.SetActive(true);
                cutParticle.PlayCase().Disabled += () =>
                {
                    cutParticle.gameObject.SetActive(false);
                };
            }
            
            Services.AudioService.PlaySound(AudioId.Obstacle_Rope_cut);
        }
        
        public void OnRopeCutAfterAnim(RopeBehavior ropeBehavior)
        {
            ropes.Remove(ropeBehavior);

            if (ropes.Count == 0)
            {
                DisableEffect();
            }
        }
        
        public override AudioId GetOverrideClickAudioId()
        {
            return AudioId.Click_obs_rope;
        }
        
        
        public override BlockEffectData GetCurrentEffectData()
        {
            BlockColor[] currentRopesColors = Data.ropesColors;
            if (currentRopesColors.Length != ropes.Count)
                currentRopesColors = ropes.Select(x => x.RopeColor).ToArray();

            return new RopesBlockEffectData { ropesColors = currentRopesColors };
        }
    }
}