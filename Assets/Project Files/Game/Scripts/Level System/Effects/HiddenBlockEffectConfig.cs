using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(menuName = "WaterFlow/Block Effects/Hidden Block Effect Config")]
    public class HiddenBlockEffectConfig : BaseBlockEffectConfig
    {
        [Header("Reveal (pick up)")]
        [SerializeField] private float revealPunchStrength = 0.15f;
        [SerializeField] private float revealPunchDuration = 0.25f;
        [SerializeField] private int revealPunchVibrato = 6;
        [SerializeField] private float revealPunchElasticity = 0.5f;

        [Header("Hide (release)")]
        [SerializeField] private float hideDelay = 0.12f;
        [SerializeField] private float hidePunchStrength = 0.1f;
        [SerializeField] private float hidePunchDuration = 0.2f;
        [SerializeField] private int hidePunchVibrato = 4;
        [SerializeField] private float hidePunchElasticity = 0.4f;

        [Header("Glass Material UV")]
        [SerializeField] private HiddenGlassVisualsData[] hiddenGlassVisualsDatas;

        public float RevealPunchStrength => revealPunchStrength;
        public float RevealPunchDuration => revealPunchDuration;
        public int RevealPunchVibrato => revealPunchVibrato;
        public float RevealPunchElasticity => revealPunchElasticity;

        public float HideDelay => hideDelay;
        public float HidePunchStrength => hidePunchStrength;
        public float HidePunchDuration => hidePunchDuration;
        public int HidePunchVibrato => hidePunchVibrato;
        public float HidePunchElasticity => hidePunchElasticity;

        public bool TryGetGlassVisualsData(BlockType blockType, out HiddenGlassVisualsData data)
        {
            if (hiddenGlassVisualsDatas != null)
            {
                for (int i = 0; i < hiddenGlassVisualsDatas.Length; i++)
                {
                    if (hiddenGlassVisualsDatas[i].BlockType == blockType)
                    {
                        data = hiddenGlassVisualsDatas[i];
                        return true;
                    }
                }
            }

            data = null;
            return false;
        }
    }
    
    [System.Serializable]
    public class HiddenGlassVisualsData
    {
        [SerializeField] BlockType blockType;
        [SerializeField] Vector2 tiling = Vector2.one;
        [SerializeField] Vector2 offset = Vector2.zero;

        public BlockType BlockType => blockType;
        public Vector2 Tiling => tiling;
        public Vector2 Offset => offset;
    }
}
