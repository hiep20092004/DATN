using BorderSpawnModule;
using DG.Tweening;
using UnityEngine;

namespace WaterFlow.Game
{
    [CreateAssetMenu(fileName = "Environment Data", menuName = "Data/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        [System.Serializable]
        public struct ExtraLayerHandlerEntry
        {
            public ExtraLayerType type;
            public ExtraLayerHandlerBehavior prefab;
        }

        [System.Serializable]
        public class SpawnTweenConfig
        {
            [SerializeField] private bool enabled = true;
            [SerializeField, Min(0f)] private float duration = 0.5f;
            [SerializeField, Min(0f)] private float delay = 0.4f;
            [SerializeField] private Ease ease = Ease.OutQuart;

            public bool Enabled => enabled;
            public float Duration => duration;
            public float Delay => delay;
            public Ease Ease => ease;
            public float TotalTime => enabled ? delay + duration : 0f;
        }

        [Header("Spawn Toggles")]
        [SerializeField] bool spawnBorders = true;
        [SerializeField] bool spawnGates = true;

        [Header("Prefabs")]
        [SerializeField] GameObject gatePrefab;
        [SerializeField] GameObject innerObstaclePrefab;
        [SerializeField] GameObject innerTile1Prefab;
        [SerializeField] GameObject innerTile2Prefab;
        [SerializeField] GameObject borderInnerGround;
        [SerializeField] GameObject generatorPrefab;

        [Header("Extra Layer")]
        [Tooltip("Handler prefab per extra-layer type (e.g. Lift). Instantiated at runtime when a level has that extra layer.")]
        [SerializeField] ExtraLayerHandlerEntry[] extraLayerHandlers = System.Array.Empty<ExtraLayerHandlerEntry>();

        [Header("Border Rule Config")]
        [SerializeField] BorderRuleConfig borderRuleConfig;
        [SerializeField] BorderRuleConfig obstacleRuleConfig;

        [Header("Spawn Tween Config")]
        [SerializeField] SpawnTweenConfig innerTileSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig innerObstacleSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig interactiveObjectSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig gateSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig borderInnerGroundSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig borderSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig blockSpawnTween = new SpawnTweenConfig();
        [SerializeField] SpawnTweenConfig liftGroupVisualSpawnTween = new SpawnTweenConfig();

        public GameObject GatePrefab => gatePrefab;
        public GameObject GeneratorPrefab => generatorPrefab;
        public GameObject InnerObstaclePrefab => innerObstaclePrefab;
        public GameObject InnerTilePrefab(int index) => index % 2 == 0 ? innerTile1Prefab : innerTile2Prefab;
        public GameObject BorderInnerGround => borderInnerGround;

        /// <summary>Handler prefab for the given extra-layer type, or null if none is configured.</summary>
        public ExtraLayerHandlerBehavior GetExtraLayerHandlerPrefab(ExtraLayerType type)
        {
            if (extraLayerHandlers == null)
                return null;

            for (int i = 0; i < extraLayerHandlers.Length; i++)
            {
                if (extraLayerHandlers[i].prefab && extraLayerHandlers[i].type == type)
                    return extraLayerHandlers[i].prefab;
            }

            return null;
        }
        public bool SpawnBorders => spawnBorders;
        public bool SpawnGates => spawnGates;
        public BorderRuleConfig BorderRuleConfig => borderRuleConfig;
        public BorderRuleConfig ObstacleRuleConfig => obstacleRuleConfig;
        public SpawnTweenConfig InnerTileSpawnTween => innerTileSpawnTween;
        public SpawnTweenConfig InnerObstacleSpawnTween => innerObstacleSpawnTween;
        public SpawnTweenConfig InteractiveObjectSpawnTween => interactiveObjectSpawnTween;
        public SpawnTweenConfig GateSpawnTween => gateSpawnTween;
        public SpawnTweenConfig BorderInnerGroundSpawnTween => borderInnerGroundSpawnTween;
        public SpawnTweenConfig BorderSpawnTween => borderSpawnTween;
        public SpawnTweenConfig BlockSpawnTween => blockSpawnTween;
        public SpawnTweenConfig LiftGroupVisualSpawnTween => liftGroupVisualSpawnTween;

        public float GetTotalAnimationTime()
        {
            float time = 0f;
            time = Mathf.Max(time, innerTileSpawnTween.TotalTime);
            time = Mathf.Max(time, gateSpawnTween.TotalTime);
            time = Mathf.Max(time, borderSpawnTween.TotalTime);
            time = Mathf.Max(time, blockSpawnTween.TotalTime);
            return time;
        }
    }
}