using UnityEngine;
using UnityEngine.UI;

namespace WaterFlow.Core
{
    public static class UIUtils
    {
        public static bool IsWideScreen(Camera camera)
        {
            return camera.aspect > (9f / 16f);
        }

        public static void MatchSize(this CanvasScaler canvasScaler)
        {
            canvasScaler.matchWidthOrHeight = ((float)Screen.width / Screen.height) > (9f / 16f) ? 1.0f : 0.0f;
        }

        public static float GetDeviceDiagonalSizeInInches()
        {
            float screenWidth = Screen.width / Screen.dpi;
            float screenHeight = Screen.height / Screen.dpi;
            float diagonalInches = Mathf.Sqrt(Mathf.Pow(screenWidth, 2) + Mathf.Pow(screenHeight, 2));

            return diagonalInches;
        }

        public static float GetDeviceDiagonalSizeInInches(Camera camera)
        {
            float screenWidth = camera.pixelWidth / Screen.dpi;
            float screenHeight = camera.pixelHeight / Screen.dpi;
            float diagonalInches = Mathf.Sqrt(Mathf.Pow(screenWidth, 2) + Mathf.Pow(screenHeight, 2));

            return diagonalInches;
        }

        public static float GetAspectRatio(Camera camera)
        {
            float width = camera.pixelWidth;
            float height = camera.pixelHeight;

            return Mathf.Max(width, height) / Mathf.Min(width, height);
        }
    }
}
