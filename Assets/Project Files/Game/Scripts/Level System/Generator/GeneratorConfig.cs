using System;
using System.Collections.Generic;
using DG.Tweening;
using WaterFlow.Core;
using UnityEngine;
using Ease = DG.Tweening.Ease;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Generator Config", menuName = "Data/Generator Config")]
    public class GeneratorConfig : ScriptableObject
    {
        [Header("Visual / Preview")]
        [SerializeField, Min(0f)] private float visualEdgeOffset = 0.13f;
        [SerializeField, Min(0f)] private float previewBlockScale = 0.4f;
        [SerializeField, Min(0f)] private float editorPreviewStackSpacing = 0.5f;

        [Header("Spawn motion")]
        [SerializeField, Min(0f)] private float spawnTravelDuration = 0.35f;
        [SerializeField] private Ease spawnTravelEase = Ease.OutCubic;

        [Header("Count text")]
        [Tooltip("0 = không tween khi đổi số.")]
        [SerializeField, Min(0f)] private float countTextPopupDuration = 0.28f;
        [SerializeField, Min(0f)] private float countTextPunchStrength = 0.22f;

        [Header("Toggle visual")]
        [SerializeField] private Material toggleReadyMaterial;
        [SerializeField] private Material toggleBusyMaterial;
        [SerializeField, Min(0f)] private float toggleAnimDuration = 0.25f;
        [SerializeField] private Ease toggleAnimEase = Ease.OutCubic;
        [SerializeField] private float toggleLocalPos = 0.22f;
        
        public Material ToggleReadyMaterial => toggleReadyMaterial;
        public Material ToggleBusyMaterial => toggleBusyMaterial;
        public float ToggleAnimDuration => toggleAnimDuration;
        public Ease ToggleAnimEase => toggleAnimEase;

        public Vector3 GetToggleLocalPos(bool isReady)
        {
            return isReady ? new Vector3(toggleLocalPos, 0, 0) : new Vector3(-toggleLocalPos, 0, 0);
        }

        [Header("Door motion")]
        [Tooltip("Local scale Z of the doors. Open = block can pass through, Closed = sealed.")]
        [SerializeField] private float doorOpenLocalZ = 0f;
        [SerializeField] private float doorClosedLocalZ = 1f;
        [SerializeField, Min(0f)] private float doorOpenDuration = 0.2f;
        [SerializeField, Min(0f)] private float doorCloseDuration = 0.2f;
        [SerializeField] private Ease doorOpenEase = Ease.OutCubic;
        [SerializeField] private Ease doorCloseEase = Ease.InCubic;

        public float DoorOpenLocalZ => doorOpenLocalZ;
        public float DoorClosedLocalZ => doorClosedLocalZ;
        public float DoorOpenDuration => doorOpenDuration;
        public float DoorCloseDuration => doorCloseDuration;
        public Ease DoorOpenEase => doorOpenEase;
        public Ease DoorCloseEase => doorCloseEase;

        [Header("Block spawn presets")]
        [Tooltip("Spawn cell (figure center) in the 3×3 watched grid per generator direction, per block type.")]
        [SerializeField] private BlockSpawnPreset[] blockSpawnPresets = Array.Empty<BlockSpawnPreset>();

        [Header("Generator queue block effects")]
        [Tooltip("Empty = all block effect types are allowed on generator queue entries.")]
        [SerializeField] private BlockEffectType[] allowedBlockEffectsForQueue = Array.Empty<BlockEffectType>();

        public float VisualEdgeOffset => visualEdgeOffset;
        public float PreviewBlockScale => previewBlockScale;
        public float EditorPreviewStackSpacing => editorPreviewStackSpacing;
        public float SpawnTravelDuration => spawnTravelDuration;
        public Ease SpawnTravelEase => spawnTravelEase;

        public float CountTextPopupDuration => countTextPopupDuration;
        public float CountTextPunchStrength => countTextPunchStrength;

        public BlockEffectType[] AllowedBlockEffectsForQueue => allowedBlockEffectsForQueue;

        public IReadOnlyList<BlockSpawnPreset> BlockSpawnPresets => blockSpawnPresets;

        public BlockSpawnPreset GetSpawnPreset(BlockType blockType)
        {
            if (blockSpawnPresets == null || blockSpawnPresets.Length == 0)
                return null;

            for (int i = 0; i < blockSpawnPresets.Length; i++)
            {
                BlockSpawnPreset p = blockSpawnPresets[i];
                if (p != null && p.BlockType == blockType)
                    return p;
            }

            return null;
        }

        /// <summary>
        /// Ensures one preset per <see cref="BlockType"/> (enum order). Keeps existing presets; adds missing types with default cell (1,0).
        /// </summary>
        [ContextMenu("Initialize All Block Presets")]
        public void InitializeAllBlockPresets()
        {
            var byType = new Dictionary<BlockType, BlockSpawnPreset>();
            if (blockSpawnPresets != null)
            {
                for (int i = 0; i < blockSpawnPresets.Length; i++)
                {
                    BlockSpawnPreset p = blockSpawnPresets[i];
                    if (p == null)
                        continue;
                    byType[p.BlockType] = p;
                }
            }

            BlockType[] all = (BlockType[])Enum.GetValues(typeof(BlockType));
            var merged = new BlockSpawnPreset[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                BlockType t = all[i];
                if (byType.TryGetValue(t, out BlockSpawnPreset existing))
                {
                    existing.EnsureArrayLength();
                    merged[i] = existing;
                }
                else
                {
                    merged[i] = new BlockSpawnPreset(t);
                }
            }

            blockSpawnPresets = merged;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (blockSpawnPresets == null)
                return;
            for (int i = 0; i < blockSpawnPresets.Length; i++)
                blockSpawnPresets[i]?.EnsureArrayLength();
        }
#endif

        public bool RestrictsGeneratorBlockEffects =>
            allowedBlockEffectsForQueue != null && allowedBlockEffectsForQueue.Length > 0;

        public bool IsEffectAllowedForGeneratorQueue(BlockEffectType type)
        {
            if (type == BlockEffectType.None)
                return true;
            if (!RestrictsGeneratorBlockEffects)
                return true;
            for (int i = 0; i < allowedBlockEffectsForQueue.Length; i++)
            {
                if (allowedBlockEffectsForQueue[i] == type)
                    return true;
            }

            return false;
        }

    }
}
