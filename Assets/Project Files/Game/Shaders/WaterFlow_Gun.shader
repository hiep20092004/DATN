Shader "Custom/WaterFlow_Gun"
{
    Properties
    {
        _MainColor ("Water Color", Color) = (0.2, 0.5, 1, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        
        [Header(Fill Settings)]
        _FillAmount ("Fill Amount Top (0-1)", Range(0, 1)) = 1.0
        _FillAmountBottom ("Fill Amount Bottom (0-1)", Range(0, 1)) = 0.0
        _FoamThickness ("Foam Thickness", Float) = 0.02
        
        [Header(Dissolve Settings)]
        _DissolveSize ("Dissolve Edge Size", Range(0, 0.5)) = 0.1
        _DissolveSmoothness ("Dissolve Smoothness", Range(0, 1)) = 0.5
        _DissolveStretchX ("Dissolve Stretch X", Float) = 1.0

        [Header(Surface Settings)]
        _Distortion ("Distortion", Float) = 0.03
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _DistortionVertex ("DistortionVertex", Float) = 0.04
        [Normal] _NormalMap ("NormalMap", 2D) = "bump" {}
        _NormalStrength ("NormalStrength", Float) = 1.0
        _SpecularFloat ("SpecularFloat", Float) = 2.0
        _NormalTiling ("NormalTiling", Float) = 1.0

        [Header(Caustics Settings)]
        _WaterPatternTex ("Water Pattern (Caustics)", 2D) = "white" {}
        _WaterPatternScale ("Water Pattern Scale", Float) = 1.0
        _WaterPatternSpeed ("Water Pattern Speed", Float) = 0.2
        _WaterPatternStrength ("Water Pattern Strength", Float) = 0.7

        [Header(Vertex Animation)]
        _VertexWaveFrequency ("Vertex Wave Frequency", Float) = 5.0
        _VertexWaveSpeed ("Vertex Wave Speed", Float) = 1.5
        _FlowDirection ("Flow Direction (XYZ)", Vector) = (0, -1, 0, 0)
        _WaveFalloff ("Wave Falloff", Float) = 2.0
        
        [Header(Backface Settings)]
        _BackfaceDarkness ("Backface Darkness", Range(0, 1)) = 0.5
        
        [Header(Rimlight Settings)]
        _RimColor ("Rimlight Color (Dark)", Color) = (0.1, 0.2, 0.4, 1)
        _RimPower ("Rimlight Power", Range(0.1, 10)) = 2.0
        _RimIntensity ("Rimlight Intensity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

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
                float2 uv : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            float4 _MainColor, _FoamColor;
            float _FillAmount, _FillAmountBottom, _FoamThickness;
            float _Distortion, _Smoothness, _DistortionVertex, _NormalStrength, _SpecularFloat, _NormalTiling, _DissolveSize;
            float _DissolveSmoothness, _DissolveStretchX;
            sampler2D _NormalMap, _WaterPatternTex;
            float _WaterPatternScale, _WaterPatternSpeed, _WaterPatternStrength;
            float _VertexWaveFrequency, _VertexWaveSpeed, _WaveFalloff;
            float3 _FlowDirection;
            float _BackfaceDarkness;
            float4 _RimColor;
            float _RimPower, _RimIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                float vertWave = sin(v.uv.y * _VertexWaveFrequency + _Time.y * _VertexWaveSpeed) * _DistortionVertex;
                float waveMask = saturate(pow(v.uv.y, _WaveFalloff));
                v.vertex.xyz += normalize(_FlowDirection) * vertWave * waveMask;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                // 1. LẤY NOISE TỪ NORMAL MAP (Kênh R) - KÉO DÃN THEO TRỤC X
                float2 normalUV = i.uv * _NormalTiling;
                // Kéo dãn UV theo trục X để tạo hiệu ứng stretch
                normalUV.x += i.worldPos.x * _DissolveStretchX;
                float4 normalSample = tex2D(_NormalMap, normalUV);
                float dissolveNoise = (normalSample.r - 0.5) * _DissolveSize;

                // 2. DUAL FILL LOGIC VỚI SMOOTH DISSOLVE
                // WaterMaskTop: Dương nếu i.uv.y nằm TRÊN _FillAmount (Sẽ bị clip phần > 0)
                float waterMaskTop = i.uv.y - _FillAmount + dissolveNoise;
                
                // WaterMaskBottom: Dương nếu i.uv.y nằm DƯỚI _FillAmountBottom (Sẽ bị clip phần > 0)
                // Lưu ý: Đảo ngược dấu để cắt từ dưới lên
                float waterMaskBottom = _FillAmountBottom - i.uv.y + dissolveNoise;

                // SMOOTH DISSOLVE EDGE - Sử dụng smoothstep để làm mịn cạnh
                float dissolveEdge = _DissolveSize * (1.0 - _DissolveSmoothness);
                float smoothTop = smoothstep(-dissolveEdge, dissolveEdge, -waterMaskTop);
                float smoothBottom = smoothstep(-dissolveEdge, dissolveEdge, -waterMaskBottom);
                float dissolveAlpha = min(smoothTop, smoothBottom);

                // Cắt với alpha threshold
                clip(dissolveAlpha - 0.001);

                // 3. NORMAL MAP & LIGHTING
                float3 normalData = UnpackNormal(normalSample);
                normalData.xy *= _NormalStrength;
                float3 worldNormal = normalize(normalData);

                // 4. DISTORTION & CAUSTICS
                float2 patternUV = i.worldPos.xz * _WaterPatternScale + worldNormal.xy * _Distortion;
                patternUV += _Time.y * _WaterPatternSpeed;
                float pattern = tex2D(_WaterPatternTex, patternUV).r;
                pattern = saturate((pattern - 0.4) * 3.0);

                // 5. SPECULAR
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(worldNormal, halfDir)), _Smoothness * 128.0 + 1.0);
                float3 specColor = spec * _SpecularFloat * _LightColor0.rgb;

                // 5.5. RIMLIGHT (Dark rim)
                float3 worldNormalFromVertex = normalize(i.worldNormal);
                float rim = 1.0 - saturate(dot(viewDir, worldNormalFromVertex));
                rim = pow(rim, _RimPower);
                float rimFactor = rim * _RimIntensity;

                // 6. FOAM CHO CẢ HAI ĐẦU TIẾP GIÁP
                float foamTop = smoothstep(_FoamThickness, 0, abs(waterMaskTop));
                float foamBottom = smoothstep(_FoamThickness, 0, abs(waterMaskBottom));
                float foamFactor = saturate(foamTop + foamBottom);

                fixed4 finalColor = lerp(_MainColor, _FoamColor, foamFactor);

                // 7. FINAL MIX
                float bodyMask = (1.0 - foamFactor);
                finalColor.rgb += pattern * bodyMask * _WaterPatternStrength * _MainColor.rgb;
                finalColor.rgb += specColor * bodyMask;
                // Apply dark rimlight (lerp to darker color at edges)
                finalColor.rgb = lerp(finalColor.rgb, _RimColor.rgb, rimFactor);
                finalColor.a = (_MainColor.a + foamFactor * 0.4) * dissolveAlpha;

                // 8. LÀM TỐI MẶT TRONG (BACKFACE)
                // facing > 0 là mặt ngoài, facing < 0 là mặt trong
                float backfaceFactor = facing > 0 ? 1.0 : _BackfaceDarkness;
                finalColor.rgb *= backfaceFactor;

                return finalColor;
            }
            ENDCG
        }
    }
}