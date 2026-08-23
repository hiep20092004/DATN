using System;
using System.Collections.Generic;
using DG.Tweening;
using Lofelt.NiceVibrations;
using WaterFlow.Core;
using TMPro;
using UnityEngine;

namespace WaterFlow.Game
{
    public class GateBehavior : MonoBehaviour
    {
        [SerializeField] MeshRenderer gateMeshRenderer;
        [SerializeField] MeshRenderer pipeMeshRenderer;
        [SerializeField] MeshRenderer pipeShadowMeshRenderer;
        [SerializeField] Transform pipeTransform;
        [SerializeField] MeshFilter gateMeshFilter;
        [SerializeField] MeshFilter pipeMeshFilter;
        [SerializeField] MeshFilter arrowMeshFilter;
        [SerializeField] private GameObject waterPrefab;
        [SerializeField] private GameObject waterTopPrefab;
        [SerializeField] private GameObject waterLeftPrefab;
        [SerializeField] private GameObject waterRightPrefab;
        [SerializeField] private GameObject debugGameObject;
        [SerializeField] private TextMeshProUGUI waterCountText;
        [SerializeField] private ParticleSystem pipeBubbleEffect;
        [SerializeField] private ParticleSystem pipeBubbleFillEffect;

        [SerializeField] Mesh topGateOverrideMesh;
        [SerializeField] Mesh topArrowOverrideMesh;

        private float waterBlockSize = 2f;
        private float waterConsumeSpeed = 0.2f;
        [SerializeField] private int initialEmptyBlockCount = 3;

        private BorderData data;
        private GateDirection gateDirection;
        private List<WaterBlock> waterBlocks = new List<WaterBlock>();
        private WaterBlock emptyWater;
        private WaterFlow waterFlow;
        private LevelBlockBehavior currentWaterFlowTargetBlock;
        private List<GateEffectBehavior> effects;
        private Material gateRuntimeMaterialInstance;

        private readonly List<(WaterBlock block, int index, int consumeAmount)> _targetBlocksBuffer = new();
        private readonly List<WaterBlock> _blocksToRemoveBuffer = new();
        private readonly Dictionary<int, float> _moveOffsetsBuffer = new();
        private DG.Tweening.Tween _waterFillHapticEnvelopeTween;
        private DG.Tweening.Tween _waterFillHapticStopTween;
        private DG.Tweening.Tween _waterFillHapticStartDelayTween;

#if UNITY_EDITOR
        private int _cachedActivePoint = -1;
#endif

        public BorderData Data => data;
        public List<GateEffectBehavior> Effects => effects;
        public MeshRenderer GateMeshRenderer => gateMeshRenderer;
        public MeshRenderer PipeMeshRenderer => pipeMeshRenderer;
        public MeshFilter GateMeshFilter => gateMeshFilter;
        public MeshFilter PipeMeshFilter => pipeMeshFilter;
        public GateDirection GateDirection => gateDirection;
        public Transform ArrowTransform => arrowMeshFilter.transform;
        public Transform PipeTransform => pipeTransform;

        public void Init(BorderData borderData)
        {
            data = borderData;
            if (borderData.GateDirectionType == GateDirection.Type.None)
            {
#if UNITY_EDITOR
                Debug.LogError($"[GateBehavior] Invalid gate direction for gate at position {borderData.Position}");
#endif
                return;
            }
            gateDirection = GateDirection.DIRECTIONS[(int)borderData.GateDirectionType];
            switch (borderData.GateDirectionType)
            {
                case GateDirection.Type.Top:
                    gateMeshFilter.mesh = topGateOverrideMesh;
                    arrowMeshFilter.mesh = topArrowOverrideMesh;
                    arrowMeshFilter.transform.localPosition = new Vector3(-0.24f, 0, 0);
                    break;
                case GateDirection.Type.Left:
                    arrowMeshFilter.transform.localPosition = new Vector3(0, 0, 0.11f);
                    break;
                case GateDirection.Type.Right:
                    arrowMeshFilter.transform.localPosition = new Vector3(0, 0, -0.11f);
                    break;
            }
            effects = new List<GateEffectBehavior>();
            InitPipeHead();
            InitWaterFlow();
            InitWaterPipes(borderData.LevelElementData);
            var currentWater = CurrentWaterBlock();
            if (currentWater)
            {
                ChangeGateColor(currentWater.BlockColor);
            }
            LevelGeneralConfigData levelConfig = LevelController.Instance.LevelGeneralConfig;
            waterBlockSize = levelConfig.WaterPipeSize;
            waterConsumeSpeed = levelConfig.FillingWaterSpeed;
            InitDebug();
        }

        public float PlayInitAnimation(float delay = 0f)
        {
            if (!Application.isPlaying)
                return 0f;

            float clampedDelay = Mathf.Max(0f, delay);
            float consumeTime = Mathf.Max(0, initialEmptyBlockCount) * waterConsumeSpeed;
            if (clampedDelay <= 0f)
            {
                RemoveWaterInPipe(initialEmptyBlockCount, BlockColor.None);
                return consumeTime;
            }

            DOVirtual.DelayedCall(clampedDelay, () =>
            {
                if (!this)
                    return;

                RemoveWaterInPipe(initialEmptyBlockCount, BlockColor.None);
            }, false);

            return clampedDelay + consumeTime;
        }

        private void InitDebug()
        {
            if (!debugGameObject) return;
            debugGameObject.SetActive(IsShowDebug());
            var pos = debugGameObject.transform.localPosition;
            pos.x *= gateDirection.DirectionScale;
            debugGameObject.transform.localPosition = pos;
            debugGameObject.transform.localRotation = Quaternion.Inverse(this.transform.localRotation);
        }

        private bool IsShowDebug()
        {
#if UNITY_EDITOR
            return Application.isPlaying;
#else
            return false;
#endif
        }

        private void InitPipeHead()
        {
            pipeTransform.localPosition = pipeTransform.localPosition.MultX(gateDirection.DirectionScale);
            pipeTransform.localRotation = Quaternion.Euler(0, gateDirection.PipeRotation, 0);
        }

        private void InitWaterPipes(LevelElementData levelElementData)
        {
            float offset = 0;
            if (Application.isPlaying)
            {
                int clampedInitialEmptyCount = Mathf.Max(0, initialEmptyBlockCount);
                if (clampedInitialEmptyCount > 0)
                {
                    ColorData initialEmptyColorData = new ColorData()
                    {
                        color = BlockColor.None,
                        colorCount = clampedInitialEmptyCount
                    };

                    WaterBlock initialEmptyBlock = Instantiate(waterPrefab, transform).GetComponent<WaterBlock>();
                    initialEmptyBlock.transform.localPosition = new Vector3(-offset * gateDirection.DirectionScale, 0, 0);
                    initialEmptyBlock.transform.localRotation = Quaternion.Euler(0, gateDirection.PipeRotation, 0);
                    initialEmptyBlock.transform.localScale = new Vector3(initialEmptyColorData.colorCount, 1, 1);
                    initialEmptyBlock.Init(initialEmptyColorData);
                    waterBlocks.Add(initialEmptyBlock);
                    offset += waterBlockSize * initialEmptyColorData.colorCount;
                }
            }

            if (levelElementData is GateLevelElementData gateElem && gateElem.GateData != null)
            {
                foreach (var colorData in gateElem.GateData)
                {
                    WaterBlock waterBlock = Instantiate(waterPrefab, transform).GetComponent<WaterBlock>();
                    waterBlock.transform.localPosition = new Vector3(-offset * gateDirection.DirectionScale, 0, 0);
                    waterBlock.transform.localRotation = Quaternion.Euler(0, gateDirection.PipeRotation, 0);
                    waterBlock.transform.localScale = new Vector3(colorData.colorCount, 1, 1);
                    offset += waterBlockSize * colorData.colorCount;
                    waterBlock.Init(colorData);
                    waterBlocks.Add(waterBlock);
                }
            }
            WaterBlock blankBlock = Instantiate(waterPrefab, transform).GetComponent<WaterBlock>();
            blankBlock.transform.localPosition = new Vector3(-offset * gateDirection.DirectionScale, 0, 0);
            blankBlock.transform.localRotation = Quaternion.Euler(0, gateDirection.PipeRotation, 0);
            blankBlock.transform.localScale = new Vector3(6, 1, 1);
            ColorData emptyColorData = new ColorData()
            {
                color = BlockColor.None,
                colorCount = 6
            };
            blankBlock.Init(emptyColorData);
            emptyWater = blankBlock;

        }
#if UNITY_EDITOR
        private void Update()
        {
            if (IsShowDebug())
            {
                int activePoint = GetActivePoint();
                if (activePoint != _cachedActivePoint)
                {
                    _cachedActivePoint = activePoint;
                    waterCountText.SetText("{0}", activePoint);
                }
            }
        }
#endif


        private void InitWaterFlow()
        {
            if (!Application.isPlaying) return;
            switch (data.GateDirectionType)
            {
                case GateDirection.Type.Top:
                    waterFlow = Instantiate(waterTopPrefab, transform).GetComponent<WaterFlow>();
                    break;
                case GateDirection.Type.Left:
                    waterFlow = Instantiate(waterLeftPrefab, transform).GetComponent<WaterFlow>();
                    break;
                case GateDirection.Type.Right:
                    waterFlow = Instantiate(waterRightPrefab, transform).GetComponent<WaterFlow>();
                    break;
            }
            if (waterFlow)
            {
                waterFlow.Init();
            }
        }


        /// <summary>
        /// Checks if a block can go through this gate.
        /// Block must match the gate color AND have available points for that color.
        /// This prevents blocks from being released when the matching color is already full.
        /// </summary>
        public BlockGateState CanGoThroughGate(LevelBlockBehavior levelBlockBehavior)
        {
            if (IsOutOfWater()) return BlockGateState.GateEmpty;

            if (!effects.IsNullOrEmpty())
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    if (!effect.IsActive) continue;
                    var state = effect.CanGoThroughGate(levelBlockBehavior);
                    if (state != BlockGateState.Enterable)
                        return state;
                }
            }

            BlockColor gateColor = GetActiveColor();

            // Check if block matches the gate color
            var canMatchGateColor = levelBlockBehavior.CanMatchGateColor(gateColor);
            if (canMatchGateColor != BlockGateState.Enterable)
                return canMatchGateColor;

            int availablePoints = levelBlockBehavior.GetAvailablePoint(gateColor);
            if (availablePoints == 0) return BlockGateState.BlockFullFilled;

            return BlockGateState.Enterable;
        }

        public BlockColor GetActiveColor()
        {
            return IsOutOfWater() ? BlockColor.None : waterBlocks[0].BlockColor;
        }

        public int GetActivePoint()
        {
            return IsOutOfWater() ? 0 : waterBlocks[0].ColorCount;
        }

        public bool IsOutOfWater()
        {
            return waterBlocks.Count == 0;
        }

        private bool IsMatchColor(BlockColor color)
        {
            if (waterBlocks.Count == 0) return false;
            return waterBlocks[0].BlockColor == color;
        }

        private WaterBlock CurrentWaterBlock()
        {
            return waterBlocks.Count == 0 ? null : waterBlocks[0];
        }

        /// <summary>
        /// Snapshot of the remaining water queue as <see cref="ColorData"/>, front-to-back.
        /// Skips the leading <see cref="BlockColor.None"/> padding block (consumed by <see cref="PlayInitAnimation"/>).
        /// </summary>
        public List<ColorData> GetRemainingGateData()
        {
            var result = new List<ColorData>(waterBlocks.Count);
            for (int i = 0; i < waterBlocks.Count; i++)
            {
                WaterBlock block = waterBlocks[i];
                if (block.BlockColor == BlockColor.None || block.ColorCount <= 0)
                    continue;

                result.Add(new ColorData { color = block.BlockColor, colorCount = block.ColorCount });
            }

            return result;
        }

        /// <summary>
        /// Fills the caller-supplied list with (amount, queueIndex) for all water blocks of the given color.
        /// The list is cleared before use.
        /// </summary>
        public void GetWaterInfoForColor(BlockColor color, List<(int amount, int queueIndex)> result)
        {
            result.Clear();
            for (int i = 0; i < waterBlocks.Count; i++)
            {
                if (waterBlocks[i].BlockColor == color)
                {
                    result.Add((waterBlocks[i].ColorCount, i));
                }
            }
        }

        public void OnBlockEntered(LevelBlockBehavior pickedBlock)
        {

        }

        public void OnBlockDestructed(LevelBlockBehavior destructedBlock)
        {
            if (!destructedBlock || destructedBlock != currentWaterFlowTargetBlock) return;
            waterFlow?.DisableWaterFlowImmediately();
            currentWaterFlowTargetBlock = null;
        }

        public void SetActiveArrow(bool isActive)
        {
            arrowMeshFilter.gameObject.SetActive(isActive);
        }

        public void SetActivePipeShadow(bool isActive)
        {
            pipeShadowMeshRenderer.gameObject.SetActive(isActive);
        }

        public void SetActivePipeBubbleFill(bool isActive)
        {
            pipeBubbleFillEffect.gameObject.SetActive(isActive);
        }

        public void RemoveWaterInPipe(int totalConsumePoint, BlockColor color)
        {
            if (waterBlocks.Count == 0 || totalConsumePoint == 0) return;

            _targetBlocksBuffer.Clear();
            int remainingToConsume = totalConsumePoint;

            for (int i = 0; i < waterBlocks.Count && remainingToConsume > 0; i++)
            {
                if (waterBlocks[i].BlockColor == color)
                {
                    int consumeFromThis = Mathf.Min(waterBlocks[i].ColorCount, remainingToConsume);
                    _targetBlocksBuffer.Add((waterBlocks[i], i, consumeFromThis));
                    remainingToConsume -= consumeFromThis;
                }
            }

            if (_targetBlocksBuffer.Count == 0) return;
            int consumedPoint = 0;
            for (int i = 0; i < _targetBlocksBuffer.Count; i++)
            {
                consumedPoint += _targetBlocksBuffer[i].consumeAmount;
            }

            if (consumedPoint == 0) return;

            float consumeTime = consumedPoint * waterConsumeSpeed;
            if (color != BlockColor.None)
            {
                PlayWaterFillHaptic(consumedPoint);
                pipeBubbleFillEffect.Play();
            }

            _blocksToRemoveBuffer.Clear();
            _moveOffsetsBuffer.Clear();

            foreach (var (targetBlock, targetIndex, consumeAmount) in _targetBlocksBuffer)
            {
                targetBlock.OnRemoveColor(consumeAmount);
                targetBlock.transform.DOScaleX(targetBlock.ColorCount, consumeTime);

                if (targetBlock.ColorCount <= 0)
                {
                    _blocksToRemoveBuffer.Add(targetBlock);
                }

                for (int i = targetIndex + 1; i < waterBlocks.Count; i++)
                {
                    _moveOffsetsBuffer.TryAdd(i, 0);
                    _moveOffsetsBuffer[i] += waterBlockSize * consumeAmount * gateDirection.DirectionScale;
                }
            }

            foreach (var kvp in _moveOffsetsBuffer)
            {
                int blockIndex = kvp.Key;
                float offset = kvp.Value;

                if (blockIndex < waterBlocks.Count)
                {
                    float resultValue = waterBlocks[blockIndex].transform.localPosition.x + offset;
                    waterBlocks[blockIndex].transform.DOLocalMoveX(resultValue, consumeTime);
                }
            }

            float totalOffset = waterBlockSize * consumedPoint * gateDirection.DirectionScale;
            emptyWater.transform.DOLocalMoveX(
                emptyWater.transform.localPosition.x + totalOffset, consumeTime);

            if (_blocksToRemoveBuffer.Count > 0)
            {
                var blocksToRemoveSnapshot = new List<WaterBlock>(_blocksToRemoveBuffer);
                bool needUpdateColor = false;
                List<GameObject> blocksGameObjectToRemove = new();
                foreach (var block in blocksToRemoveSnapshot)
                {
                    int index = waterBlocks.IndexOf(block);
                    if (index == 0) needUpdateColor = true;
                    if (index < 0) continue;
                    waterBlocks.RemoveAt(index);
                    blocksGameObjectToRemove.Add(block.gameObject);
                }

                DOVirtual.DelayedCall(consumeTime, () =>
                {
                    foreach (var block in blocksGameObjectToRemove)
                    {
                        if (block) Destroy(block);
                    }

                    if (needUpdateColor)
                    {
                        FillNextColor();
                    }
                }, false);
            }
        }

        private void PlayWaterFillHaptic(int floorCount)
        {
            switch (floorCount)
            {
                case 1:
                    global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.WaterFlowShort);
                    break;
                case 2:
                    global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.WaterFlowMedium);
                    break;
                case 3:
                    global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.WaterFlowLong);
                    break;
                case 4:
                    global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.WaterFlowSuperLong);
                    break;
                default:
                    global::WaterFlow.Core.HapticFeedback.Play(global::WaterFlow.Core.HapticType.WaterFlowMax);
                    break;
            }
        }

        public void ShowWaterFlow(BlockColor color, int consumePoint, int blockDepth, LevelBlockBehavior targetBlock, Action onFillWaterVisual)
        {
            float consumeTime = consumePoint * waterConsumeSpeed;
            currentWaterFlowTargetBlock = targetBlock;
            if (!waterFlow)
            {
                onFillWaterVisual?.Invoke();
                return;
            }
            int safeBlockDepth = Mathf.Max(1, blockDepth);
            waterFlow.ShowWaterFlow(safeBlockDepth - 1, color, out float flowDuration);
            if (flowDuration < 0.016f)
            {
                onFillWaterVisual?.Invoke();
            }
            else
            {
                DOVirtual.DelayedCall(Mathf.Max(flowDuration - 0.016f, 0f), () => { onFillWaterVisual?.Invoke(); }, false);
                // Debug.Log($"[Start] Delayed call for water start fill: {startTime}");
            }
            float delayEndTime = consumeTime;
            if (delayEndTime > 0)
            {
                //Debug.Log($"[Gate] Delayed call for water flow end: {delayEndTime}");
                DOVirtual.DelayedCall(delayEndTime, () => { waterFlow?.DisableWaterFlow(); }, false);
            }
            else
            {
                waterFlow.DisableWaterFlow();
            }
        }


        private void FillNextColor()
        {
            if (waterBlocks.Count == 0)
            {
                ChangeGateColor(BlockColor.None);
                pipeBubbleEffect.gameObject.SetActive(false);
                return;
            }
            BlockColor color = CurrentWaterBlock().BlockColor;
            ChangeGateColor(color);
        }

        private void ChangeGateColor(BlockColor color)
        {
            if (IsGateVisualColorSyncBlockedByActiveEffect())
            {
                return;
            }

            var colorData = LevelController.Instance.GetBlockColorData(color);
            if (colorData != null)
            {
                var material = colorData.PipeMaterial;
                ChangeGateColorLerp(material);
            }
        }

        public void SyncGateVisualWithActiveColor()
        {
            BlockColor activeColor = GetActiveColor();
            if (IsGateVisualColorSyncBlockedByActiveEffect())
            {
                return;
            }
            ChangeGateColor(activeColor);
        }

        private void ChangeGateColorLerp(Material material)
        {
            if (!gateMeshRenderer || !material) return;

            if (Application.isPlaying)
            {
                var runtimeMaterial = EnsureUniqueGateMaterial(material);
                if (!runtimeMaterial)
                {
                    return;
                }

                // If shaders differ, copy/lerp by property is unreliable. Switch material immediately.
                if (runtimeMaterial.shader != material.shader)
                {
                    ChangeGateMaterial(material);
                    return;
                }

                DOTween.Kill(runtimeMaterial);
                const float lerpDuration = 0.2f;
                bool hasTween = false;

                if (runtimeMaterial.HasProperty(ShaderId.COLOR_SHADER_ID) && material.HasProperty(ShaderId.COLOR_SHADER_ID))
                {
                    var targetBaseColor = material.GetColor(ShaderId.COLOR_SHADER_ID);
                    DOTween.To(
                            () => runtimeMaterial.GetColor(ShaderId.COLOR_SHADER_ID),
                            value => runtimeMaterial.SetColor(ShaderId.COLOR_SHADER_ID, value),
                            targetBaseColor,
                            lerpDuration)
                        .SetEase(DG.Tweening.Ease.Linear)
                        .SetTarget(runtimeMaterial);
                    hasTween = true;
                }

                if (runtimeMaterial.HasProperty(ShaderId.EMISION_COLOR_SHADER_ID) &&
                    material.HasProperty(ShaderId.EMISION_COLOR_SHADER_ID))
                {
                    var targetEmissionColor = material.GetColor(ShaderId.EMISION_COLOR_SHADER_ID);
                    bool shouldEnableEmission = targetEmissionColor.maxColorComponent > 0.0001f;
                    runtimeMaterial.EnableKeyword("_EMISSION");
                    DOTween.To(
                            () => runtimeMaterial.GetColor(ShaderId.EMISION_COLOR_SHADER_ID),
                            value => runtimeMaterial.SetColor(ShaderId.EMISION_COLOR_SHADER_ID, value),
                            targetEmissionColor,
                            lerpDuration)
                        .SetEase(DG.Tweening.Ease.Linear)
                        .SetTarget(runtimeMaterial)
                        .OnComplete(() =>
                        {
                            if (shouldEnableEmission)
                            {
                                runtimeMaterial.EnableKeyword("_EMISSION");
                            }
                            else
                            {
                                runtimeMaterial.DisableKeyword("_EMISSION");
                            }
                        });
                    hasTween = true;
                }

                if (!hasTween)
                {
                    runtimeMaterial.CopyPropertiesFromMaterial(material);
                }
            }
            else
            {
                gateMeshRenderer.sharedMaterial = material;
            }
        }

        public void ChangeGateMaterial(Material material)
        {
            if (!gateMeshRenderer || !material) return;

            if (Application.isPlaying)
            {
                var runtimeMaterial = EnsureUniqueGateMaterial(material);
                if (!runtimeMaterial)
                {
                    return;
                }

                DOTween.Kill(runtimeMaterial);
                gateMeshRenderer.material = material;
            }
            else
            {
                gateMeshRenderer.sharedMaterial = material;
            }
        }

        private Material EnsureUniqueGateMaterial(Material sourceMaterial)
        {
            if (!gateMeshRenderer || !sourceMaterial) return null;

            if (!gateRuntimeMaterialInstance)
            {
                gateRuntimeMaterialInstance = new Material(sourceMaterial);
                gateMeshRenderer.material = gateRuntimeMaterialInstance;
            }
            else if (gateMeshRenderer.sharedMaterial != gateRuntimeMaterialInstance)
            {
                gateMeshRenderer.material = gateRuntimeMaterialInstance;
            }

            return gateRuntimeMaterialInstance;
        }

        private bool IsGateVisualColorSyncBlockedByActiveEffect()
        {
            if (effects.IsNullOrEmpty())
                return false;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect || !effect.IsActive)
                    continue;

                if (!effect.BlocksGateVisualColorSync)
                    continue;

                return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            _waterFillHapticEnvelopeTween?.Kill();
            _waterFillHapticStopTween?.Kill();
            _waterFillHapticStartDelayTween?.Kill();
            DOTween.Kill(gateRuntimeMaterialInstance);

            if (!gateRuntimeMaterialInstance) return;

            if (Application.isPlaying)
            {
                Destroy(gateRuntimeMaterialInstance);
            }
            else
            {
                DestroyImmediate(gateRuntimeMaterialInstance);
            }

            gateRuntimeMaterialInstance = null;
        }

        public void ChangePipeMaterial(Material material)
        {
            if (Application.isPlaying)
            {
                pipeMeshRenderer.material = material;
            }
            else
            {
                pipeMeshRenderer.sharedMaterial = material;
            }
        }

        public void ApplyEffect(GateEffectBehavior effect)
        {
            if (!effect)
                return;

            effects ??= new List<GateEffectBehavior>();

            effect.OnCreated(this);
            AttachEffect(effect);
        }

        public void AttachEffect(GateEffectBehavior effect)
        {
            if (!effect)
                return;

            effects ??= new List<GateEffectBehavior>();

            if (effects.Contains(effect))
                return;

            int effectsCount = effects.Count;
            effect.SetOrder(effectsCount);
            effects.Add(effect);

            if (effectsCount <= 0)
                return;

            var snapshot = new GateEffectBehavior[effectsCount];
            for (int i = 0; i < effectsCount; i++)
                snapshot[i] = effects[i];

            for (int i = 0; i < snapshot.Length; i++)
            {
                if (!snapshot[i])
                    continue;

                snapshot[i].OnNewEffectAddedToGate(effect);
            }
        }

        public void RemoveEffect(GateEffectBehavior effect)
        {
            if (!effect)
                return;

            if (effects == null || effects.Count == 0)
                return;

            int removedIndex = effects.IndexOf(effect);
            if (removedIndex < 0)
                return;

            effects.RemoveAt(removedIndex);

            // Re-index remaining effects
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i])
                    effects[i].SetOrder(i);
            }

            // Notify remaining effects (snapshot)
            if (effects.Count <= 0)
                return;

            var snapshot = effects.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (!snapshot[i])
                    continue;

                snapshot[i].OnEffectRemovedFromGate(effect);
            }
        }
    }
}