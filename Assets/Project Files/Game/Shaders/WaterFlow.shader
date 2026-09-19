Shader "Custom/WaterFlow"
{
    Properties
    {
        _MainColor ("Water Color", Color) = (0.2, 0.5, 1, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FillAmount ("Fill Amount", Float) = 0.5
        _WaveSpeed ("Wave Speed", Float) = 2.0
        _WaveHeight ("Wave Height", Float) = 0.05
        _FoamThickness ("Foam Thickness", Float) = 0.1
        _FillDirection ("Fill Direction", Vector) = (0, 1, 0, 0)
        _EdgeNoiseTex ("Edge Noise", 2D) = "gray" {}
        _EdgeNoiseScale ("Edge Noise Scale", Float) = 1.0
        _EdgeNoiseAmplitude ("Edge Noise Amplitude", Float) = 0.05

        // === VÂN NƯỚC / CAUSTIC TEX ===
        _WaterPatternTex ("Water Pattern (Caustics)", 2D) = "white" {}
        _WaterPatternScale ("Water Pattern Scale", Float) = 1.0
        _WaterPatternSpeed ("Water Pattern Speed", Float) = 0.2
        _WaterPatternStrength ("Water Pattern Strength", Float) = 0.7

        // === VERTEX WAVE ===
        _VertexWaveAmplitude ("Vertex Wave Amplitude", Float) = 0.02
        _VertexWaveFrequency ("Vertex Wave Frequency", Float) = 5.0
        _VertexWaveSpeed ("Vertex Wave Speed", Float) = 1.5
        _FlowDirection ("Flow Direction (XYZ)", Vector) = (0, -1, 0, 0)

        _FixedHeightTop ("Fixed Height Top", Float) = 0.5
        _FixedHeightBottom ("Fixed Height Bottom", Float) = -0.5
        _WaveFalloff ("Wave Falloff", Float) = 2.0 // Độ mềm chuyển tiếp
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 objPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            float4 _MainColor;
            float4 _FoamColor;
            float _FillAmount;
            float _WaveSpeed;
            float _WaveHeight;
            float _FoamThickness;
            float3 _FillDirection;
            sampler2D _EdgeNoiseTex;
            float4 _EdgeNoiseTex_ST;
            float _EdgeNoiseScale;
            float _EdgeNoiseAmplitude;

            // === VERTEX WAVE ===
            float _VertexWaveAmplitude;
            float _VertexWaveFrequency;
            float _VertexWaveSpeed;
            float3 _FlowDirection;

            // === VÂN NƯỚC / CAUSTIC TEX ===
            sampler2D _WaterPatternTex;
            float4 _WaterPatternTex_ST;
            float _WaterPatternScale;
            float _WaterPatternSpeed;
            float _WaterPatternStrength;
            float _FixedHeightTop;
            float _FixedHeightBottom;
            float _WaveFalloff;

            v2f vert(appdata v)
            {
                v2f o;

                // === VERTEX WAVE - Sóng chảy xuống ===
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Tạo 2 sóng chéo nhau
                float wave1 = sin(worldPos.y * _VertexWaveFrequency + _Time.y * _VertexWaveSpeed)
                    * cos(worldPos.x * _VertexWaveFrequency * 0.7);

                float wave2 = sin(worldPos.x * _VertexWaveFrequency * 0.5 + _Time.y * _VertexWaveSpeed * 0.8)
                    * cos(worldPos.z * _VertexWaveFrequency * 0.6);

                float waveOffset = (wave1 + wave2) * 0.5 * _VertexWaveAmplitude;

           // === MASK GIỮ CẢ TOP VÀ BOTTOM CỐ ĐỊNH ===
float centerY = (_FixedHeightTop + _FixedHeightBottom) * 0.5;
float rangeY = _FixedHeightTop - _FixedHeightBottom;

// Khoảng cách từ vertex đến center (normalize về 0-1)
float distFromCenter = abs(v.vertex.y - centerY) / (rangeY * 0.5);

// Mask: 1 ở center, 0 ở rìa
float waveMask = 1.0 - saturate(pow(distFromCenter, _WaveFalloff));

// Nhân amplitude với mask
waveOffset *= waveMask;

                // Offset vertex theo hướng flow
                float3 flowDir = normalize(_FlowDirection);
                v.vertex.xyz += flowDir * waveOffset;

                // Thêm chút offset perpendicular
                float3 perpendicular = float3(-flowDir.z, 0, flowDir.x);
                v.vertex.xyz += perpendicular * waveOffset * 0.3;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objPos = v.vertex.xyz;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Fill theo direction
                float fillDir = dot(i.objPos, normalize(_FillDirection));

                // Sóng nước
                float wave = sin(i.worldPos.x * 3.0 + _Time.y * _WaveSpeed) *
                    cos(i.worldPos.z * 3.0 + _Time.y * _WaveSpeed * 0.7) * _WaveHeight;

                float fillLevel = _FillAmount * 2.0 - 1.0;

                // === TÁCH RIÊNG CHO PATTERN ===
                float waterLevelNoWave = fillDir - fillLevel; // Không có wave
                float waterLevel = waterLevelNoWave + wave; // Có wave cho foam/clip

                // Noise dissolve mép
                float2 noiseUV = i.worldPos.xz * _EdgeNoiseScale;
                noiseUV = noiseUV * _EdgeNoiseTex_ST.xy + _EdgeNoiseTex_ST.zw;
                float edgeNoise = tex2D(_EdgeNoiseTex, noiseUV).r * 2.0 - 1.0;

                float edgeMask = saturate(1.0 - abs(waterLevel) / (_FoamThickness * 2.0));
                edgeNoise *= edgeMask * _EdgeNoiseAmplitude;

                float noisyWaterLevel = waterLevel + edgeNoise;

                // Cắt phần ngoài nước
                clip(-noisyWaterLevel);

                // Foam
                float foamFactor = smoothstep(_FoamThickness, 0, abs(noisyWaterLevel));

                // Màu nước cơ bản
                fixed4 finalColor = lerp(_MainColor, _FoamColor, foamFactor);

                // === VÂN NƯỚC / CAUSTIC - DÙNG LEVEL KHÔNG CÓ WAVE ===
                // UV dùng worldPos.xz để vân nằm cố định trong không gian
                float2 patternUV = i.worldPos.xz * _WaterPatternScale;
                patternUV = patternUV * _WaterPatternTex_ST.xy + _WaterPatternTex_ST.zw;

                // Scroll nhẹ cho vân chuyển động
                patternUV += _Time.y * _WaterPatternSpeed;

                // Texture grayscale caustic
                float pattern = tex2D(_WaterPatternTex, patternUV).r;

                // Tăng contrast – giữ lại vệt sáng
                pattern = saturate((pattern - 0.4) * 3.0);

                // Xác định vùng "bên trong nước" - DÙNG waterLevelNoWave
                float waterMask = saturate(-waterLevelNoWave * 5.0);

                // Nếu muốn vân ít dính foam, nhân thêm (1 - foamFactor)
                float nonFoamMask = 1.0 - foamFactor;

                // mask tổng cho vân nước
                float causticMask = waterMask * nonFoamMask;

                // Màu vân – dùng màu sáng gần trắng nhưng pha theo màu nước
                float3 causticColor = pattern * causticMask * _WaterPatternStrength *
                    lerp(_MainColor.rgb, _FoamColor.rgb, 0.5);

                // Cộng thêm lên màu nước
                finalColor.rgb += causticColor;

                // Alpha
                finalColor.a = _MainColor.a + foamFactor * 0.3;

                return finalColor;
            }
            ENDCG
        }
    }
}