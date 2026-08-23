using DG.Tweening;
using WaterFlow.Enums;
using WaterFlow.Core;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WaterFlow.Game
{
    public sealed class IceGateEffectBehavior : GateEffectBehavior
    {
        [SerializeField] private Transform visualTransform;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI turnsShadowText;
        [SerializeField] private Material iceMaterial;
        [SerializeField] private Material icePipeMaterial;
        [SerializeField] private ParticleSystem iceBreakParticles;
            
        [SerializeField] private Mesh iceTopGateOverrideMesh;
        [SerializeField] private Mesh iceSideGateOverrideMesh;
        [SerializeField] private Mesh icePipeOverrideMesh;
        
        [Header("Turn counter punch")]
        [SerializeField] float turnPunchScale = 0.3f;
        [SerializeField] float turnPunchDuration = 0.3f;
        
        private Material storedMaterial;
        private Material storedPipeMaterial;
        private Material gateMaterial;
        private Material pipeMaterial;
        private Mesh storedGateMesh;
        private Mesh storedPipeMesh;
        private bool didStoreGateMesh;
        private bool didStorePipeMesh;
        private int remainingTurns;
        private Tweener punchTween;
        
        private AudioId[] iceBreakAudio = new[]
            { AudioId.Obstacle_Ice_break_01, AudioId.Obstacle_Ice_break_03 };
        private static int lastTimePlayAudio = -1;
        public override bool BlocksGateVisualColorSync => true;
        
        public override void OnCreated(GateBehavior gateBehavior)
        {
            this.transform.position = gateBehavior.ArrowTransform.position;
            
            var direction = gateBehavior.Data.GateDirectionType;
            if(direction == GateDirection.Type.Top || direction == GateDirection.Type.Bottom)
            {
                turnsShadowText.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            if (iceBreakParticles)
            {
                var e = iceBreakParticles.transform.localEulerAngles;
                e.y = direction == GateDirection.Type.Top || direction == GateDirection.Type.Bottom ? -90f : 0f;
                iceBreakParticles.transform.localEulerAngles = e;

                iceBreakParticles.transform.SetParent(gateBehavior.transform, true);
            }

            gateBehavior.SetActiveArrow(false);
            gateBehavior.SetActivePipeShadow(false);
            gateBehavior.SetActivePipeBubbleFill(false);
            var gateMeshRenderer = gateBehavior.GateMeshRenderer;
            var pipeMeshRenderer = gateBehavior.PipeMeshRenderer;
            var gateDir = direction;
            bool useTopMeshes = gateDir == GateDirection.Type.Top;

            ApplyIceMeshesIfAny(gateBehavior, useTopMeshes);
            
            gateMaterial = gateMeshRenderer.sharedMaterial;
            pipeMaterial = pipeMeshRenderer.sharedMaterial;
            storedMaterial = gateMaterial;
            storedPipeMaterial = pipeMaterial;
            gateMeshRenderer.material = iceMaterial;
            pipeMeshRenderer.material = icePipeMaterial;

            remainingTurns = Mathf.Max(0, ((IceGateEffectData)data).iceTurnsAmount);
            turnText.text = remainingTurns.ToString();
            turnsShadowText.text = remainingTurns.ToString();
            
            var t = visualTransform.localPosition;
            t.x *= gateBehavior.GateDirection.DirectionScale;
            visualTransform.localPosition= t;
        }

        public override void OnDisabled(GateBehavior gateBehavior)
        {
            RestoreIceMeshes(gateBehavior);
            gateBehavior.SyncGateVisualWithActiveColor();
            gateBehavior.ChangePipeMaterial(storedPipeMaterial);
            gateBehavior.SetActiveArrow(true);
            gateBehavior.SetActivePipeShadow(true);
            gateBehavior.SetActivePipeBubbleFill(true);
            punchTween?.Kill();
            punchTween = null;
            if (visualTransform)
                visualTransform.localScale = Vector3.one;
            if (Time.frameCount != lastTimePlayAudio)
            {
                lastTimePlayAudio = Time.frameCount;
                Services.AudioService.PlaySound(AudioId.Obstacle_Ice_break_02);
            }
            iceBreakParticles.Play();
        }

        private void ApplyIceMeshesIfAny(GateBehavior gateBehavior, bool useTopMeshes)
        {
            Mesh gateMesh = useTopMeshes ? iceTopGateOverrideMesh : iceSideGateOverrideMesh;

            var gateFilter = gateBehavior.GateMeshFilter;
            var pipeFilter = gateBehavior.PipeMeshFilter;

            if (gateFilter && gateMesh)
            {
                storedGateMesh = gateFilter.sharedMesh;
                gateFilter.mesh = gateMesh;
                didStoreGateMesh = true;
            }

            if (pipeFilter && icePipeOverrideMesh)
            {
                storedPipeMesh = pipeFilter.sharedMesh;
                pipeFilter.mesh = icePipeOverrideMesh;
                didStorePipeMesh = true;
            }
        }

        private void RestoreIceMeshes(GateBehavior gateBehavior)
        {
            var gateFilter = gateBehavior.GateMeshFilter;
            var pipeFilter = gateBehavior.PipeMeshFilter;

            if (didStoreGateMesh && gateFilter)
                gateFilter.mesh = storedGateMesh;
            if (didStorePipeMesh && pipeFilter)
                pipeFilter.mesh = storedPipeMesh;

            didStoreGateMesh = false;
            didStorePipeMesh = false;
        }

        public override void OnBlockFullFilledAfterAnimationGlobal(LevelBlockBehavior levelBlockBehavior, BlockColor filledColor)
        {
            remainingTurns = Mathf.Max(0, remainingTurns - 1);

            if (remainingTurns <= 0)
            {
                DisableEffect();
            }
            else
            {
                turnText.text = remainingTurns.ToString();
                turnsShadowText.text = remainingTurns.ToString();
                
                if (visualTransform)
                {
                    punchTween?.Kill();
                    visualTransform.localScale = Vector3.one;
                    punchTween = visualTransform.DOPunchScale(Vector3.one * turnPunchScale, turnPunchDuration)
                        .SetEase(DG.Tweening.Ease.OutSine);
                }
                if (Time.frameCount != lastTimePlayAudio)
                {
                    lastTimePlayAudio = Time.frameCount;
                    AudioId pickedAudio = iceBreakAudio[Random.Range(0, iceBreakAudio.Length)];
                    Services.AudioService.PlaySound(pickedAudio);
                }
            }
        }

        public override void OnNewEffectAddedToGate(GateEffectBehavior effect)
        {
            if (!effect) return;
            if (remainingTurns == 0) return;

            // Turn off visuals of the effect that was added to the block
            effect.gameObject.SetActive(false);
        }

        public override BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            return remainingTurns > 0 ? BlockGateState.GateFrozen : BlockGateState.Enterable;
        }

        public override GateEffectData GetCurrentEffectData()
        {
            return new IceGateEffectData { iceTurnsAmount = remainingTurns };
        }
    }
}