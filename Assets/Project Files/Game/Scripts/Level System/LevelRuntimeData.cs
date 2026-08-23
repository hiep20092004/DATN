using System.Collections.Generic;
using WaterFlow.Enums;
using WaterFlow.Core;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Runtime-only container for the current level play session. NOT persisted — lives in
    /// memory and is wiped between plays. Begins when the level starts (first interaction),
    /// and is cleared both when a new level loads and when the current level ends.
    /// Use this as the single place to stash per-play state (counts, timings, flags) instead
    /// of scattering fields across controllers.
    /// </summary>
    [StaticUnload]
    public class LevelRuntimeData
    {
        private static LevelRuntimeData current;
        public static LevelRuntimeData Current => current ??= new LevelRuntimeData();

        /// <summary>True between <see cref="Begin"/> and <see cref="Clear"/> (i.e. while the player is actively playing).</summary>
        public bool IsActive { get; private set; }

        /// <summary>1-based level number currently being played.</summary>
        public int Level { get; private set; }

        /// <summary>Level data for the level currently being played (may be null before <see cref="Begin"/>).</summary>
        public LevelData LevelData { get; private set; }

        /// <summary>Seconds of active play time since <see cref="Begin"/> (excludes paused time — only advanced via <see cref="Tick"/>).</summary>
        public float ElapsedPlayTime { get; private set; }

        /// <summary>Number of blocks destroyed/collected during this play.</summary>
        public int BlocksDestroyed { get; private set; }

        /// <summary>Number of block moves performed during this play.</summary>
        public int Moves { get; private set; }

        /// <summary>Number of revives used during this play.</summary>
        public int Revives { get; private set; }

        /// <summary>Order in which blocks were destroyed (block ids).</summary>
        public IReadOnlyList<int> BlockDestroyOrder => blockDestroyOrder;
        private readonly List<int> blockDestroyOrder = new List<int>();

        /// <summary>Count of each booster used during this play.</summary>
        public IReadOnlyDictionary<GameResource, int> BoostersUsed => boostersUsed;
        private readonly Dictionary<GameResource, int> boostersUsed = new Dictionary<GameResource, int>();

        /// <summary>Free-form bag for ad-hoc per-play values without bloating this class.</summary>
        private readonly Dictionary<string, object> customData = new Dictionary<string, object>();

        /// <summary>
        /// Start a fresh play session for <paramref name="level"/>. Wipes any previous data first,
        /// so this doubles as the "clear on start" step.
        /// </summary>
        public void Begin(int level, LevelData levelData)
        {
            Clear();
            Level = level;
            LevelData = levelData;
            IsActive = true;
        }

        /// <summary>
        /// Mark the session as ended (stops further recording/ticking) while keeping the
        /// collected data readable for end-of-level consumers. Data is wiped on the next
        /// <see cref="Begin"/> / <see cref="Clear"/>.
        /// </summary>
        public void End() => IsActive = false;

        /// <summary>Reset all per-play data. Called on new-level load and on (re)start.</summary>
        public void Clear()
        {
            IsActive = false;
            Level = 0;
            LevelData = null;
            ElapsedPlayTime = 0f;
            BlocksDestroyed = 0;
            Moves = 0;
            Revives = 0;
            blockDestroyOrder.Clear();
            boostersUsed.Clear();
            customData.Clear();
        }

        /// <summary>Advance the active play timer. Call only while gameplay is running (skip while paused).</summary>
        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            ElapsedPlayTime += deltaTime;
        }

        public void RecordBlockDestroyed(int blockId)
        {
            if (!IsActive) return;
            BlocksDestroyed++;
            blockDestroyOrder.Add(blockId);
        }

        public void RecordMove()
        {
            if (!IsActive) return;
            Moves++;
        }

        public void RecordRevive()
        {
            if (!IsActive) return;
            Revives++;
        }

        public void RecordBoosterUsed(GameResource booster)
        {
            if (!IsActive) return;
            boostersUsed.TryGetValue(booster, out int count);
            boostersUsed[booster] = count + 1;
        }

        public void SetCustom(string key, object value) => customData[key] = value;

        public T GetCustom<T>(string key, T fallback = default)
            => customData.TryGetValue(key, out object value) && value is T typed ? typed : fallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => current = null;

        private static void UnloadStatic() => current = null;
    }
}
