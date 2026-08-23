using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Game
{
    /// <summary>
    /// Strategy slot for BackgroundTransition: each provider decides for itself whether it has
    /// something to show for a given scene transition, and owns its own UI visibility.
    /// Add a new provider and drop it into BackgroundTransition's list to extend the loading
    /// tooltip (e.g. a future LiveOps tooltip) without touching BackgroundTransition itself.
    /// </summary>
    public abstract class TransitionTooltipProvider : MonoBehaviour
    {
        public abstract bool TryShow(GamePlacement from, GamePlacement to);

        public abstract void Hide();
    }
}
