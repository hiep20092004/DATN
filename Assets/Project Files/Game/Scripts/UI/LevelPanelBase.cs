using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Common contract for the top-of-screen level panel. UIGame spawns the concrete implementation
    /// (<see cref="LevelPanel"/>) rather than hard-wiring one panel into the UIGame prefab, so the
    /// panel can be swapped without touching UIGame.
    /// </summary>
    public abstract class LevelPanelBase : MonoBehaviour
    {
        [SerializeField] protected TimerVisualiser timeVisualiser;
        
        public TimerVisualiser TimeVisualiser => timeVisualiser;
        /// <summary>Initialize the panel for the level at <paramref name="levelIndex"/> (0-based).</summary>
        public abstract void Init();

        /// <summary>Refresh the panel in-place when the next level loads without re-spawning it.</summary>
        public void RefreshForNextLevel(int levelIndex)
        {
            if (timeVisualiser)
                timeVisualiser.Refresh();
            OnRefreshForNextLevel(levelIndex);
        }

        public abstract void OnRefreshForNextLevel(int levelIndex);
    }
}
