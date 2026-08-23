using UnityEngine;

namespace WaterFlow.Game
{
    public static class ShaderId
    {
        public static readonly int FILL_AMOUNT_SHADER_ID = Shader.PropertyToID("_FillAmount");
        public static readonly int FILL_BOUNDS_MIN_SHADER_ID = Shader.PropertyToID("_FillBoundsMin");
        public static readonly int FILL_BOUNDS_MAX_SHADER_ID = Shader.PropertyToID("_FillBoundsMax");
        public static readonly int ROTALE_ANGLE = Shader.PropertyToID("_TiltAngle");
        public static readonly int AMPLITUDE = Shader.PropertyToID("_Amplitude");
        public static readonly int FREQUENCY = Shader.PropertyToID("_Frequency");
        public static readonly int WAVE_SPEED = Shader.PropertyToID("_WaveSpeed");
        
        public static readonly int BASE_MAP_SHADER_ID = Shader.PropertyToID("_BaseMap");
        public static readonly int MAIN_TEXTURE_ID = Shader.PropertyToID("_Main_Texture");
        
        public static readonly int COLOR_SHADER_ID = Shader.PropertyToID("_BaseColor");
        public static readonly int EMISION_COLOR_SHADER_ID = Shader.PropertyToID("_EmissionColor");
        public static readonly int HEIGHT_CUTOFF_SHADER_ID = Shader.PropertyToID("_HeightCutoff");
        
        public static readonly int FLOAT_FILL_START_ID = Shader.PropertyToID("_Fill_Start_Offset");
        public static readonly int FLOAT_FILL_END_ID = Shader.PropertyToID("_Fill_End_Offset");
        
    }
}