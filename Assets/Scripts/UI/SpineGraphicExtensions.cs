using Spine;
using Spine.Unity;

namespace WaterFlow.Game
{
    /// <summary>
    /// spine-unity 4.3 split <see cref="SkeletonGraphic"/> into a pure renderer plus a sibling
    /// animation component, so the AnimationState no longer hangs off the graphic itself.
    /// Resolved in one place instead of scattering GetComponent lookups through the UI code.
    /// </summary>
    public static class SpineGraphicExtensions
    {
        public static AnimationState GetAnimationState(this SkeletonGraphic graphic)
        {
            if (!graphic) return null;

            IAnimationStateComponent animationComponent = graphic.GetComponent<IAnimationStateComponent>();
            return animationComponent?.AnimationState;
        }
    }
}
