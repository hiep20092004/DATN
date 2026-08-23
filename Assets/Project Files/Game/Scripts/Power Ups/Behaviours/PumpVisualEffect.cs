using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Handles all visual effects for the Pump Power Up.
    /// Manages material setup, appear/disappear animations, water flow effects, and positioning.
    /// </summary>
    public class PumpVisualEffect : MonoBehaviour
    {
        private const float PUMP_APPEAR_DURATION = 0.3f;
        private const float PUMP_DISAPPEAR_DURATION = 0.25f;
        
        // Shader property IDs for WaterFlow_DualFill_Dissolve shader
        private static readonly int FILL_AMOUNT_ID = Shader.PropertyToID("_FillAmount");
        private static readonly int FILL_AMOUNT_BOTTOM_ID = Shader.PropertyToID("_FillAmountBottom");
        private static readonly int MAIN_COLOR_ID = Shader.PropertyToID("_MainColor");
        private static readonly int TOP_COLOR_ID = Shader.PropertyToID("_TopColor");
        private static readonly int TINT_ID = Shader.PropertyToID("_Tint");
        private static readonly int FOAM_COLOR_ID = Shader.PropertyToID("_FoamColor");

        [Header("Pump Body")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private MeshRenderer pumpMeshRenderer;
        [SerializeField] private MeshFilter pumpMeshFilter;
        [SerializeField] private int waterMaterialIndex = 1;
        [SerializeField] private Vector3 visualScale = new Vector3(1.3f, 1.3f, 1.3f);
        
        [Header("Water Effect")]
        [SerializeField] private ParticleSystem pumpHeadParticles;
        [SerializeField] private ParticleSystem pumpCornerParticles;
        
        [Header("Water Flow Components")]
        [SerializeField] private Transform pumpNozzle; // The end point of pump where water comes out
        [SerializeField] private Transform cylinderPipe;
        [SerializeField] private MeshRenderer cylinderPipeMeshRenderer;
        [SerializeField] private Transform cylinderConer;
        [SerializeField] private MeshRenderer cylinderConerMeshRenderer;
        
        [Header("Water Flow Settings")]
        [SerializeField] private float waterFlowDurationUnit = 0.5f;
        [SerializeField] private float waterTailDelayUnit = 0.15f;
        [SerializeField] private float conerFillDuration = 0.2f;
        [SerializeField] private float conerDisappearDuration = 0.1f;
        [SerializeField] private float pipeBaseScaleZ = 1f;

        private Material instanceWaterMaterial;
        private Material instancePipeMaterial;
        private Material instanceConerMaterial;
        private Material defaultPipeMaterial;
        private Material defaultConerMaterial;
        
        private TweenCase currentScaleTween;
        private TweenCase pumpWaterTween;
        private TweenCase pipeFillTopTween;
        private TweenCase pipeFillBottomTween;
        private TweenCase conerFillTween;
        private TweenCase conerDisappearTween;
        
        private Vector3 cachedTargetPosition;
        private Vector3 originalPipeScale;
        private float tailDelay;
        private float flowDuration;
        
        /// <summary>
        /// Initialize visual effect - hide pump initially
        /// </summary>
        public void Init()
        {
            CacheWaterFlowSourceMaterials();

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.zero;
            }
            
            // Hide water flow components
            if (cylinderPipe != null)
            {
                originalPipeScale = cylinderPipe.localScale;
                cylinderPipe.gameObject.SetActive(false);
            }
            
            if (cylinderConer != null)
            {
                cylinderConer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Setup pump visual at origin and look towards target block
        /// </summary>
        /// <param name="targetBlock">The block to fill water</param>
        public void Setup(LevelBlockBehavior targetBlock)
        {
            if (targetBlock == null) return;

            // Position pump at origin (0, 0, 0)
            transform.position = Vector3.zero;

            // Calculate target position (block center)
            cachedTargetPosition = targetBlock.transform.position +
                                   targetBlock.Figure.GetHorizontalCenterBounds().center;
            tailDelay = targetBlock.AvailablePoint * waterTailDelayUnit;
            float distance = Vector3.Distance(pumpNozzle.position, cachedTargetPosition);
            flowDuration = waterFlowDurationUnit * distance;
            
            // Look towards target block (only horizontal rotation)
            LookAtTarget(cachedTargetPosition);

            // Setup water material based on block color
            SetupWaterMaterial(targetBlock);
            
            // Setup water flow components
            SetupWaterFlowComponents(targetBlock);
        }

        /// <summary>
        /// Make pump look at target position (horizontal only)
        /// </summary>
        private void LookAtTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0; // Keep horizontal only

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = targetRotation;
            }
        }

        /// <summary>
        /// Setup water material with block's color and mesh bounds
        /// </summary>
        private void SetupWaterMaterial(LevelBlockBehavior block)
        {
            if (pumpMeshRenderer == null) return;

            // Get block's active color and corresponding water material
            BlockColor blockColor = block.GetActiveBlockColor();
            BlockColorData colorData = LevelController.Instance.GetBlockColorData(blockColor);

            if (colorData == null || colorData.WaterMaterial == null) return;

            // Get materials array - change the water material at specified index
            Material[] materials = pumpMeshRenderer.materials;
            if (materials.Length <= waterMaterialIndex) return;

            // Cleanup previous instance material
            CleanupInstanceMaterials();

            // Create instance of water material
            instanceWaterMaterial = new Material(colorData.WaterMaterial);
            materials[waterMaterialIndex] = instanceWaterMaterial;
            pumpMeshRenderer.materials = materials;

            // Setup fill bounds based on pump mesh z-axis bounds
            SetupFillBoundsFromMesh();
            
            // Initialize pump water fill to 1 (full)
            if (instanceWaterMaterial != null)
            {
                instanceWaterMaterial.SetFloat(ShaderId.FILL_AMOUNT_SHADER_ID, 1f);
            }
        }

        private void SetupWaterFlowPosition()
        {
            // Calculate distance from pump nozzle to target
            Vector3 nozzlePosition = pumpNozzle != null ? pumpNozzle.position : transform.position;
            cachedTargetPosition.y = nozzlePosition.y;
            float cornerOffset = 0.3f; 
            float cachedPipeDistance = Vector3.Distance(nozzlePosition, cachedTargetPosition) - cornerOffset;
            
            if (cylinderPipe && cylinderPipeMeshRenderer)
            {
                // Position pipe at center of nozzle and cachedTargetPosition
                cylinderPipe.position = (nozzlePosition + cachedTargetPosition - cornerOffset * (cachedTargetPosition - nozzlePosition).normalized) / 2f;
                cylinderPipe.LookAt(cachedTargetPosition);
                
                // Scale pipe Z axis based on distance
                float scaleZ = cachedPipeDistance / pipeBaseScaleZ;
                cylinderPipe.localScale = new Vector3(originalPipeScale.x, originalPipeScale.y, scaleZ / 2f);
            }
            
            // Setup CylinderConer
            if (cylinderConer != null && cylinderConerMeshRenderer != null)
            {
                // Position coner at target (block center)
                cylinderConer.position = cachedTargetPosition;
                cylinderConer.LookAt(nozzlePosition);
            }
        }

        private void CacheWaterFlowSourceMaterials()
        {
            if (cylinderPipeMeshRenderer != null && defaultPipeMaterial == null)
            {
                defaultPipeMaterial = cylinderPipeMeshRenderer.sharedMaterial;
            }

            if (cylinderConerMeshRenderer != null && defaultConerMaterial == null)
            {
                defaultConerMaterial = cylinderConerMeshRenderer.sharedMaterial;
            }
        }

        /// <summary>
        /// Setup water flow pipe and coner components
        /// </summary>
        private void SetupWaterFlowComponents(LevelBlockBehavior block)
        {
            CacheWaterFlowSourceMaterials();

            // Get block color for water material
            BlockColor blockColor = block.GetActiveBlockColor();
            BlockColorData colorData = LevelController.Instance.GetBlockColorData(blockColor);
            
            if (colorData == null) return;
            bool hasWaterColor = TryGetWaterEffectColor(colorData, out Color waterColor);
            if (hasWaterColor)
            {
                SetParticlesStartColor(pumpHeadParticles, waterColor);
                SetParticlesStartColor(pumpCornerParticles, waterColor);
            }
            
            // Setup CylinderPipe
            if (cylinderPipe != null && cylinderPipeMeshRenderer != null)
            {
                Material pipeSourceMaterial = defaultPipeMaterial != null
                    ? defaultPipeMaterial
                    : cylinderPipeMeshRenderer.sharedMaterial;

                if (pipeSourceMaterial != null)
                {
                    // Create instance material for pipe
                    instancePipeMaterial = new Material(pipeSourceMaterial);
                    cylinderPipeMeshRenderer.sharedMaterial = instancePipeMaterial;

                    // Set water color
                    if (hasWaterColor)
                    {
                        instancePipeMaterial.SetColor(MAIN_COLOR_ID, waterColor);
                        instancePipeMaterial.SetColor(FOAM_COLOR_ID, waterColor);
                    }

                    // Initialize fill amounts (hidden)
                    instancePipeMaterial.SetFloat(FILL_AMOUNT_ID, 0f);
                    instancePipeMaterial.SetFloat(FILL_AMOUNT_BOTTOM_ID, 0f);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(PumpVisualEffect)}] Missing source material for cylinder pipe mesh renderer.");
                }
                
                cylinderPipe.gameObject.SetActive(false);
            }
            
            // Setup CylinderConer
            if (cylinderConer != null && cylinderConerMeshRenderer != null)
            {
                // Position coner at target (block center)
                cylinderConer.position = cachedTargetPosition;

                Material conerSourceMaterial = defaultConerMaterial != null
                    ? defaultConerMaterial
                    : cylinderConerMeshRenderer.sharedMaterial;

                if (conerSourceMaterial != null)
                {
                    // Create instance material for coner
                    instanceConerMaterial = new Material(conerSourceMaterial);
                    cylinderConerMeshRenderer.sharedMaterial = instanceConerMaterial;

                    // Set water color
                    if (hasWaterColor)
                    {
                        instanceConerMaterial.SetColor(MAIN_COLOR_ID, waterColor);
                        instanceConerMaterial.SetColor(FOAM_COLOR_ID, waterColor);
                    }

                    // Initialize fill amounts
                    instanceConerMaterial.SetFloat(FILL_AMOUNT_ID, 1f);
                    instanceConerMaterial.SetFloat(FILL_AMOUNT_BOTTOM_ID, 1f);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(PumpVisualEffect)}] Missing source material for cylinder corner mesh renderer.");
                }
                
                cylinderConer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Set Start Color for all particle systems under the pump head hierarchy.
        /// This keeps all Water Gun Head effects consistent with the active block color.
        /// </summary>
        private void SetParticlesStartColor(ParticleSystem particleRoot , Color color)
        {
            if (particleRoot == null) return;

            ParticleSystem[] childParticleSystems = particleRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in childParticleSystems)
            {
                if (!particleSystem) continue;

                var main = particleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(color);
            }
        }

        /// <summary>
        /// Resolve the color used by pump water VFX/materials.
        /// </summary>
        private bool TryGetWaterEffectColor(BlockColorData colorData, out Color color)
        {
            color = Color.white;
            if (colorData == null || colorData.WaterMaterial == null)
            {
                return false;
            }

            Material waterMaterial = colorData.WaterMaterial;
            if (waterMaterial.HasProperty(TINT_ID))
            {
                color = waterMaterial.GetColor(TINT_ID);
                return true;
            }

            if (waterMaterial.HasProperty(MAIN_COLOR_ID))
            {
                color = waterMaterial.GetColor(MAIN_COLOR_ID);
                return true;
            }

            if (waterMaterial.HasProperty(TOP_COLOR_ID))
            {
                color = waterMaterial.GetColor(TOP_COLOR_ID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Set MinMax Fill Amount shader properties based on pump mesh z-axis bounds
        /// </summary>
        private void SetupFillBoundsFromMesh()
        {
            if (instanceWaterMaterial == null) return;

            Mesh mesh = null;
            if (pumpMeshFilter != null)
            {
                mesh = pumpMeshFilter.sharedMesh;
            }

            if (mesh == null && pumpMeshRenderer != null)
            {
                var meshFilter = pumpMeshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    mesh = meshFilter.sharedMesh;
                }
            }

            if (mesh != null)
            {
                Bounds bounds = mesh.bounds;
                float fillMin = bounds.min.z;
                float fillMax = bounds.max.z;

                instanceWaterMaterial.SetFloat(ShaderId.FILL_BOUNDS_MIN_SHADER_ID, fillMin);
                instanceWaterMaterial.SetFloat(ShaderId.FILL_BOUNDS_MAX_SHADER_ID, fillMax);
            }
        }

        /// <summary>
        /// Play pump model appear animation
        /// </summary>
        public void PlayAppearAnimation(SimpleCallback onWaterComeToTarget = null, SimpleCallback onComplete = null)
        {
            if (visualRoot == null)
            {
                onWaterComeToTarget?.Invoke();
                onComplete?.Invoke();
                return;
            }

            currentScaleTween?.Kill();
            visualRoot.localScale = Vector3.zero;

            currentScaleTween = visualRoot.DOScale(visualScale, PUMP_APPEAR_DURATION)
                .SetEasing(Ease.Type.BackOut)
                .OnComplete(() =>
                {
                    // Start water flow animation after pump appears
                    PlayWaterFlowAnimation(onWaterComeToTarget, onComplete);
                });
        }
        
        /// <summary>
        /// Play the water flow animation sequence
        /// </summary>
        private void PlayWaterFlowAnimation(SimpleCallback onWaterComeToTarget = null, SimpleCallback onComplete = null)
        {
            SetupWaterFlowPosition();
            
            // Kill any existing tweens
            KillWaterFlowTweens();
            
            // Show pipe
            if (cylinderPipe != null)
            {
                cylinderPipe.gameObject.SetActive(true);
            }
            if (cylinderConer != null && instanceConerMaterial != null)
            {
                instanceConerMaterial.SetFloat(FILL_AMOUNT_ID, 0.5f);
                cylinderConer.gameObject.SetActive(true);
            }
            pumpHeadParticles?.Play();
            pumpCornerParticles?.Play();
            
            // 1. Pump water decreases from 1 to 0
            // if (instanceWaterMaterial != null)
            // {
            //     pumpWaterTween = instanceWaterMaterial.DoFloat(ShaderId.FILL_AMOUNT_SHADER_ID, 0f, flowDuration);
            // }
            
            // 2. Pipe _FillAmount increases from 0 to 1
            if (instancePipeMaterial != null)
            {
                pipeFillTopTween = instancePipeMaterial.DoFloat(FILL_AMOUNT_ID, 1f, flowDuration)
                    .OnComplete( () =>
                    {
                        ShowCylinderConer(onWaterComeToTarget);
                    });
                
                // 3. Pipe _FillAmountBottom delayed increases from 0 to 1 (tail)
                Tween.DelayedCall(tailDelay, () =>
                {
                    if (instancePipeMaterial != null)
                    {
                        pumpHeadParticles?.Stop();
                        pipeFillBottomTween = instancePipeMaterial.DoFloat(FILL_AMOUNT_BOTTOM_ID, 1f, flowDuration)
                            .OnComplete(() =>
                            {
                                instancePipeMaterial.SetFloat(FILL_AMOUNT_ID, 0f); // fully invisible
                                // When pipe bottom fill reaches 1, hide coner and complete
                                HideCylinderConer(onComplete);
                            });
                    }
                });
            }
            else
            {
                onWaterComeToTarget?.Invoke();
                onComplete?.Invoke();
            }
        }
        
        /// <summary>
        /// Show CylinderConer with full fill
        /// </summary>
        private void ShowCylinderConer(SimpleCallback onWaterComeToTarget = null)
        {
            if (cylinderConer == null || instanceConerMaterial == null) return;
            
            // Set initial values - full fill
            instanceConerMaterial.SetFloat(FILL_AMOUNT_ID, 1f);
            instanceConerMaterial.SetFloat(FILL_AMOUNT_BOTTOM_ID, 1f);
                
            cylinderConer.gameObject.SetActive(true);

            conerFillTween = instanceConerMaterial.DoFloat(FILL_AMOUNT_BOTTOM_ID, 0f, conerDisappearDuration)
                .OnComplete(() =>
                {
                    onWaterComeToTarget?.Invoke();
                });
        }
        
        /// <summary>
        /// Hide CylinderConer with fast disappear animation
        /// </summary>
        private void HideCylinderConer(SimpleCallback onComplete = null)
        {
            if (cylinderConer == null || instanceConerMaterial == null)
            {
                // Hide pipe
                if (cylinderPipe != null)
                {
                    cylinderPipe.gameObject.SetActive(false);
                }
                pumpCornerParticles?.Stop();
                onComplete?.Invoke();
                return;
            }
            
            // Fast decrease _FillAmount from 1 to 0
            conerDisappearTween = instanceConerMaterial.DoFloat(FILL_AMOUNT_ID, 0f, conerDisappearDuration)
                .OnComplete(() =>
                {
                    // Hide both coner and pipe
                    if (cylinderConer != null)
                    {
                        cylinderConer.gameObject.SetActive(false);
                    }
                    if (cylinderPipe != null)
                    {
                        cylinderPipe.gameObject.SetActive(false);
                    }
                    pumpCornerParticles?.Stop();
                    onComplete?.Invoke();
                });
        }
        
        /// <summary>
        /// Kill all water flow tweens
        /// </summary>
        private void KillWaterFlowTweens()
        {
            pumpWaterTween?.Kill();
            pumpWaterTween = null;
            
            pipeFillTopTween?.Kill();
            pipeFillTopTween = null;
            
            pipeFillBottomTween?.Kill();
            pipeFillBottomTween = null;
            
            conerFillTween?.Kill();
            conerFillTween = null;
            
            conerDisappearTween?.Kill();
            conerDisappearTween = null;
        }

        /// <summary>
        /// Play pump model disappear animation
        /// </summary>
        public void PlayDisappearAnimation(SimpleCallback onComplete = null)
        {
            if (visualRoot == null)
            {
                onComplete?.Invoke();
                return;
            }

            currentScaleTween?.Kill();

            currentScaleTween = visualRoot.DOScale(Vector3.zero, PUMP_DISAPPEAR_DURATION)
                .SetEasing(Ease.Type.BackIn)
                .OnComplete(onComplete);
        }

        /// <summary>
        /// Cleanup all instance materials to prevent memory leak
        /// </summary>
        private void CleanupInstanceMaterials()
        {
            if (instanceWaterMaterial != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(instanceWaterMaterial);
                else
                    Object.DestroyImmediate(instanceWaterMaterial);
                instanceWaterMaterial = null;
            }
            
            if (instancePipeMaterial != null)
            {
                if (cylinderPipeMeshRenderer != null && cylinderPipeMeshRenderer.sharedMaterial == instancePipeMaterial)
                {
                    cylinderPipeMeshRenderer.sharedMaterial = defaultPipeMaterial;
                }

                if (Application.isPlaying)
                    Object.Destroy(instancePipeMaterial);
                else
                    Object.DestroyImmediate(instancePipeMaterial);
                instancePipeMaterial = null;
            }
            
            if (instanceConerMaterial != null)
            {
                if (cylinderConerMeshRenderer != null && cylinderConerMeshRenderer.sharedMaterial == instanceConerMaterial)
                {
                    cylinderConerMeshRenderer.sharedMaterial = defaultConerMaterial;
                }

                if (Application.isPlaying)
                    Object.Destroy(instanceConerMaterial);
                else
                    Object.DestroyImmediate(instanceConerMaterial);
                instanceConerMaterial = null;
            }
        }

        /// <summary>
        /// Clean up all visual resources
        /// </summary>
        public void Cleanup()
        {
            currentScaleTween?.Kill();
            currentScaleTween = null;
            
            KillWaterFlowTweens();

            CleanupInstanceMaterials();

            // Reset visual scale
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.zero;
            }
            
            // Hide water flow components
            if (cylinderPipe != null)
            {
                cylinderPipe.gameObject.SetActive(false);
            }
            
            if (cylinderConer != null)
            {
                cylinderConer.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            CleanupInstanceMaterials();
        }
    }
}
