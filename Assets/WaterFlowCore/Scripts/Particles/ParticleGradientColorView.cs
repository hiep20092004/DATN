using UnityEngine;

namespace WaterFlow.Core
{
    public class ParticleGradientColorView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystem;

        public void SetColor(Color color)
        {
            foreach (var p in particleSystem)
            {
                if(!p) continue;
                var colorOverLifetime = p.colorOverLifetime;

                if (!colorOverLifetime.enabled)
                    return;

                var gradient = colorOverLifetime.color.gradient;

                Gradient recoloredGradient = gradient.Recolor(color);

                colorOverLifetime.color =
                    new ParticleSystem.MinMaxGradient(
                        recoloredGradient);
            }
        }
    }
}