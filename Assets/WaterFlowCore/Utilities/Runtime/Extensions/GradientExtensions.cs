using UnityEngine;

namespace WaterFlow.Core
{
    public static class GradientExtensions
    {
        /// <summary>
        /// Recolors a gradient by replacing the hue of all color keys
        /// while preserving their original saturation and value.
        ///
        /// Intended for monochromatic gradients
        /// (e.g. blue -> light blue -> cyan).
        ///
        /// WARNING:
        /// This method will modify ALL color keys.
        /// If the gradient contains multiple intentional colors
        /// (e.g. blue -> white -> yellow),
        /// the resulting colors may not be artistically correct.
        ///
        /// For production VFX with multi-color gradients,
        /// prefer swapping a pre-authored Gradient instead.
        /// </summary>
        public static Gradient Recolor(
            this Gradient source,
            Color targetColor)
        {
            var sourceKeys = source.colorKeys;
            var resultKeys = new GradientColorKey[sourceKeys.Length];

            Color.RGBToHSV(
                targetColor,
                out float targetHue,
                out _,
                out _);

            for (int i = 0; i < sourceKeys.Length; i++)
            {
                Color.RGBToHSV(
                    sourceKeys[i].color,
                    out _,
                    out float saturation,
                    out float value);

                Color recolored =
                    Color.HSVToRGB(
                        targetHue,
                        saturation,
                        value);

                resultKeys[i] =
                    new GradientColorKey(
                        recolored,
                        sourceKeys[i].time);
            }

            var gradient = new Gradient();

            gradient.SetKeys(
                resultKeys,
                source.alphaKeys);

            return gradient;
        }
    }
}