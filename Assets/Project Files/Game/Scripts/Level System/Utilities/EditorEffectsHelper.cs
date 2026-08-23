using System.Collections.Generic;
using WaterFlow.Core;
using UnityEditor;

namespace WaterFlow.Game
{
    public class EditorEffectsHelper
    {
#if UNITY_EDITOR
        private static Dictionary<BlockEffectType, int> blockEffectsMap = new Dictionary<BlockEffectType, int>();
#endif

        public static int GetSortingOrder(BlockEffectType effectType)
        {
            int sortingOrder = 0;

#if UNITY_EDITOR
            blockEffectsMap.TryGetValue(effectType, out sortingOrder);
#endif

            return sortingOrder;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        public static void Init()
        {
            LevelDatabase levelDatabase = EditorUtils.GetAsset<LevelDatabase>();
            if (levelDatabase == null) return;

            LevelBlockEffectData[] effects = levelDatabase.Effects;
            if (effects.IsNullOrEmpty()) return;

            blockEffectsMap = new Dictionary<BlockEffectType, int>();
            foreach (LevelBlockEffectData effect in effects)
            {
                blockEffectsMap.Add(effect.Type, effect.EffectSortingOrder);
            }
        }
#endif
    }
}