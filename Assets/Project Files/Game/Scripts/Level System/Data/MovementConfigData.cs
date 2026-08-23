namespace WaterFlow.Game
{
    public struct MovementConfigData
    {
        public float RaycastDistance;
        public float MovementSpeed;
        public float MovementBoostThreshold;
        public float MovementBoostFactor;
        public float MovementSnapDuration;
        public float MovementSnapStopDistance;
        public float PickedModelY;
        public float ReleasedModelY;
        public float ModelYLerpSpeed;
        public float VisualFollowSpeed;
        public float WaterFrequencyOnRotate;
        public float WaterAmplitudeOnRotate;
        public float WaterRotateToRightMax;
        public float WaterRotateToLeftMax;
        public float WaterRotationSpeed;
        public float MovementActiveSpeedThreshold;
        public float HorizontalDirectionThreshold;
        public float WaterAmplitudeLerpSpeed;
        public float WaterFrequencyLerpSpeed;

        public static MovementConfigData From(BlockConfig config)
        {
            if (!config)
            {
                return new MovementConfigData
                {
                    RaycastDistance = 100f,
                    MovementSpeed = 47f,
                    MovementBoostThreshold = 0.32f,
                    MovementBoostFactor = 0.0015f,
                    MovementSnapDuration = 0.15f,
                    MovementSnapStopDistance = 0.02f,
                    PickedModelY = 0.4f,
                    ReleasedModelY = 0f,
                    ModelYLerpSpeed = 15f,
                    VisualFollowSpeed = 25f,
                    WaterFrequencyOnRotate = 3f,
                    WaterAmplitudeOnRotate = 0.03f,
                    WaterRotateToRightMax = 182.5f,
                    WaterRotateToLeftMax = 172.5f,
                    WaterRotationSpeed = 100f,
                    MovementActiveSpeedThreshold = 0.5f,
                    HorizontalDirectionThreshold = 0.1f,
                    WaterAmplitudeLerpSpeed = 0.5f,
                    WaterFrequencyLerpSpeed = 5f,
                };
            }

            return new MovementConfigData
            {
                RaycastDistance = config.RaycastDistance,
                MovementSpeed = config.MovementSpeed,
                MovementBoostThreshold = config.MovementBoostThreshold,
                MovementBoostFactor = config.MovementBoostFactor,
                MovementSnapDuration = config.MovementSnapDuration,
                MovementSnapStopDistance = config.MovementSnapStopDistance,
                PickedModelY = config.PickedModelY,
                ReleasedModelY = config.ReleasedModelY,
                ModelYLerpSpeed = config.ModelYLerpSpeed,
                VisualFollowSpeed = config.VisualFollowSpeed,
                WaterFrequencyOnRotate = config.WaterFrequencyOnRotate,
                WaterAmplitudeOnRotate = config.WaterAmplitudeOnRotate,
                WaterRotateToRightMax = config.WaterRotateToRightMax,
                WaterRotateToLeftMax = config.WaterRotateToLeftMax,
                WaterRotationSpeed = config.WaterRotationSpeed,
                MovementActiveSpeedThreshold = config.MovementActiveSpeedThreshold,
                HorizontalDirectionThreshold = config.HorizontalDirectionThreshold,
                WaterAmplitudeLerpSpeed = config.WaterAmplitudeLerpSpeed,
                WaterFrequencyLerpSpeed = config.WaterFrequencyLerpSpeed,
            };
        }
    }
}
