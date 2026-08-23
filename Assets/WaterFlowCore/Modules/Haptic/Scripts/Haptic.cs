using UnityEngine;

namespace WaterFlow.Core
{
    [StaticUnload]
    public static class Haptic
    {
        public static readonly HapticData HAPTIC_LIGHT = new HapticData(0.05f, 0.08f);
        public static readonly HapticData HAPTIC_MEDIUM = new HapticData(0.08f, 0.2f);
        public static readonly HapticData HAPTIC_HARD = new HapticData(0.14f, 0.4f);

        public static readonly HapticPattern PATTERN_LIGHT = new HapticPattern("light", new HapticEvent[] { new HapticEvent() { Duration = 0.3f, Intensity = 1.0f, Sharpness = 0.0f, StartTime = 0.0f } });
        public static readonly HapticPattern PATTERN_WATER_FLOW_SMOOTH = new HapticPattern("water_flow_smooth", new HapticEvent[] 
        { 
            new HapticEvent() { Duration = 0.08f, Intensity = 0.3f, Sharpness = 0.1f, StartTime = 0.0f },
            new HapticEvent() { Duration = 0.08f, Intensity = 0.25f, Sharpness = 0.1f, StartTime = 0.12f },
            new HapticEvent() { Duration = 0.08f, Intensity = 0.3f, Sharpness = 0.1f, StartTime = 0.24f },
        });

        public static readonly HapticPattern PATTERN_WATER_FLOW_MEDIUM = new HapticPattern("water_flow_medium", new HapticEvent[] 
        { 
            new HapticEvent() { Duration = 0.1f, Intensity = 0.35f, Sharpness = 0.15f, StartTime = 0.0f },
            new HapticEvent() { Duration = 0.1f, Intensity = 0.3f, Sharpness = 0.15f, StartTime = 0.15f },
            new HapticEvent() { Duration = 0.1f, Intensity = 0.35f, Sharpness = 0.15f, StartTime = 0.3f },
        });

        public static readonly HapticPattern PATTERN_WATER_COMPLETE = new HapticPattern("water_complete", new HapticEvent[] 
        { 
            new HapticEvent() { Duration = 0.05f, Intensity = 0.5f, Sharpness = 0.3f, StartTime = 0.0f },
        });
        
        public static readonly HapticPattern PATTERN_WIN = new HapticPattern("win", new HapticEvent[] 
        { 
            new HapticEvent() { Duration = 0.06f, Intensity = 0.1f, Sharpness = 0.15f, StartTime = 0.0f },
            new HapticEvent() { Duration = 0.06f, Intensity = 0.1f, Sharpness = 0.15f, StartTime = 0.12f },
            new HapticEvent() { Duration = 0.06f, Intensity = 0.1f, Sharpness = 0.15f, StartTime = 0.24f },
            new HapticEvent() { Duration = 0.12f, Intensity = 0.3f, Sharpness = 0.2f, StartTime = 0.4f },
        });

        public static readonly HapticPattern PATTERN_BOOSTER_DENIED = new HapticPattern("booster_denied", new HapticEvent[] 
        { 
            new HapticEvent() { Duration = 0.06f, Intensity = 0.25f, Sharpness = 0.2f, StartTime = 0.0f },
            new HapticEvent() { Duration = 0.06f, Intensity = 0.2f, Sharpness = 0.15f, StartTime = 0.12f },
        });

        public static readonly HapticPattern PATTERN_BUTTON_CLICK = new HapticPattern("button_click", new HapticEvent[] 
        { 
            new HapticEvent() { Duration = 0.04f, Intensity = 0.4f, Sharpness = 0.5f, StartTime = 0.0f },
        });

        
        private static bool isActive;
        public static bool IsActive
        {
            get { return isActive; }
            set
            {
                isActive = value;

                save.IsActive = value;

                SaveController.Save();

                if (VerboseLogging)
                    Debug.Log($"[Haptic]: Haptic state changed: {(isActive ? "Active" : "Disabled")}");

                StateChanged?.Invoke(value);
            }
        }

        public static bool IsInitialized { get; private set; }
        public static bool VerboseLogging { get; private set; }

        private static readonly BaseHapticWrapper WRAPPER = GetPlatformWrapper();

        private static HapticSave save;

        public static event SimpleBoolCallback StateChanged;

        public static void Init()
        {
            // Get saved state
            save = SaveController.GetSaveObject<HapticSave>("haptic");

            // Set saved state
            isActive = save.IsActive;

            if (WRAPPER == null)
            {
                Debug.LogWarning("[Haptic]: Unsupported platform");

                return;
            }

            // Mark as Initialized
            IsInitialized = true;

            // Initialize platform handler
            WRAPPER.Init();

            // Register default patterns
            WRAPPER.RegisterPattern(PATTERN_LIGHT);
            WRAPPER.RegisterPattern(PATTERN_WATER_FLOW_SMOOTH);
            WRAPPER.RegisterPattern(PATTERN_WATER_FLOW_MEDIUM);
            WRAPPER.RegisterPattern(PATTERN_WATER_COMPLETE);
            WRAPPER.RegisterPattern(PATTERN_WIN);
            WRAPPER.RegisterPattern(PATTERN_BOOSTER_DENIED);
            WRAPPER.RegisterPattern(PATTERN_BUTTON_CLICK);
        }

        public static void RegisterPattern(HapticPattern hapticPattern)
        {
            if (WRAPPER == null) return;

            WRAPPER.RegisterPattern(hapticPattern);
        }

        public static void Play(HapticData hapticData)
        {
            Play(hapticData.Duration, hapticData.Intensity);
        }

        public static void Play(float duration, float intensity = 1.0f)
        {
            if (!IsActive) return;

            if (WRAPPER == null) return;

            if (duration <= 0) return;

            WRAPPER.Play(duration, intensity);
        }

        public static void Play(HapticPattern pattern)
        {
            if (!IsActive) return;

            if (WRAPPER == null) return;

            WRAPPER.Play(pattern.ID);
        }

        public static void Play(string patternID)
        {
            if (!IsActive) return;

            if (WRAPPER == null) return;

            WRAPPER.Play(patternID);
        }

        public static void EnableVerboseLogging()
        {
            VerboseLogging = true;
        }

        private static BaseHapticWrapper GetPlatformWrapper()
        {
#if UNITY_EDITOR
            return new EditorHapticWrapper();
#elif UNITY_ANDROID
            return new AndroidHapticWrapper();
#elif UNITY_WEBGL
            return new WebGLHapticWrapper();
#else
            return null;
#endif
        }

        private static void UnloadStatic()
        {
            isActive = false;

            IsInitialized = false;
            VerboseLogging = false;

            save = null;

            StateChanged = null;
        }
    }
}
