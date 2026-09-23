Shader "Custom/WaterFlow_Vertex_Texture_Driven"
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
        _DissolveSize ("Dissolve Edge Size", Range(0, 2)) = 0.1
        _DissolveSmoothness ("Dissolve Smoothness", Range(0, 1)) = 0.5
        _DissolveStretchX ("Dissolve Stretch X", Float) = 1.0

        [Header(Surface Settings)]
        _Distortion ("Distortion", Float) = 0.03
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [Normal] _NormalMap ("NormalMap", 2D) = "bump" {}
        _NormalStrength ("NormalStrength", Float) = 1.0
        _SpecularFloat ("SpecularFloat", Float) = 2.0
        _NormalTiling ("NormalTiling", Float) = 1.0

        [Header(Caustics Settings)]
        _WaterPatternTex ("Water Pattern (Caustics)", 2D) = "white" {}
        _WaterPatternScale ("Water Pattern Scale", Float) = 1.0
        _WaterPatternSpeed ("Water Pattern Speed", Float) = 0.2
        _WaterPatternStrength ("Water Pattern Strength", Float) = 0.7

        [Header(Vertex Texture Displacement)]
        _NoiseTex ("Vertex Noise Texture (R)", 2D) = "white" {}
        _GradientNoiseScale ("Noise Scale", Float) = 2.0
        _VertexWaveSpeed ("Noise Scroll Speed", Float) = 0.5
        _Amplitude ("Displacement Strength", Float) = 0.1
        _FlowDirection ("Displacement Direction (XYZ)", Vector) = (0, 1, 0, 0)
        _WaveFalloff ("Wave Falloff (UV.Y)", Float) = 2.0
        
        [Header(Backface and Lighting)]
        _BackfaceDarkness ("Backface Darkness", Range(0, 1)) = 0.5
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

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            float4 _MainColor, _FoamColor, _RimColor;
            float _FillAmount, _FillAmountBottom, _FoamThickness, _DissolveSize, _DissolveSmoothness, _DissolveStretchX;
            float _Distortion, _Smoothness, _NormalStrength, _SpecularFloat, _NormalTiling;
            sampler2D _NormalMap, _WaterPatternTex, _NoiseTex;
            float _WaterPatternScale, _WaterPatternSpeed, _WaterPatternStrength;
            float _VertexWaveSpeed, _WaveFalloff, _Amplitude, _GradientNoiseScale;
            float3 _FlowDirection;
            float _BackfaceDarkness, _RimPower, _RimIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                // Lấy World Position để texture noise bám theo không gian thế giới (tránh trượt khi object scale)
                float3 worldPosBase = mul(unity_ObjectToWorld, v.vertex).xyz;

                // --- LOGIC VERTEX TEXTURE DISPLACEMENT (KHÔNG DÙNG SIN) ---
                
                // 1. Tạo UV cho Noise dựa trên vị trí thế giới để đảm bảo sự ngẫu nhiên giữa các object
                float2 noiseUV = worldPosBase.xz / max(0.1, _GradientNoiseScale);
                
                // 2. Di chuyển UV theo thời gian để tạo hiệu ứng cuộn
                noiseUV += _Time.y * _VertexWaveSpeed;
                
                // 3. Đọc giá trị từ texture (sử dụng tex2Dlod để vertex shader có thể hiểu)
                // Ta dùng kênh R của texture làm độ cao
                float noiseVal = tex2Dlod(_NoiseTex, float4(noiseUV, 0, 0)).r;
                
                // Đưa giá trị về khoảng -0.5 đến 0.5 để có chỗ lồi chỗ lõm thay vì chỉ đẩy lên
                float displacement = (noiseVal - 0.5) * _Amplitude;
                
                // 4. Mask dựa trên UV.y: Giúp phần gốc của dòng nước không bị biến dạng quá nhiều
                float waveMask = saturate(pow(v.uv.y, _WaveFalloff));
                
                // 5. Áp dụng biến đổi trực tiếp vào Vertex
                float3 dir = length(_FlowDirection) > 0 ? normalize(_FlowDirection) : float3(0,1,0);
                v.vertex.xyz += dir * displacement * waveMask;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                // Logic Fragment giữ nguyên để đảm bảo visual (Fill/Dissolve/Rimlight)
                float2 normalUV = i.uv * _NormalTiling;
                normalUV.x += i.worldPos.x * _DissolveStretchX;
                float4 normalSample = tex2D(_NormalMap, normalUV);
                
                float dissolveNoise = (normalSample.r - 0.5) * _DissolveSize;
                float maskTop = i.uv.y - _FillAmount + dissolveNoise;
                float maskBottom = _FillAmountBottom - i.uv.y + dissolveNoise;
                float dEdge = _DissolveSize * (1.0 - _DissolveSmoothness);
                
                float smoothT = smoothstep(-dEdge, dEdge, -maskTop);
                float smoothB = smoothstep(-dEdge, dEdge, -maskBottom);
                float dAlpha = min(smoothT, smoothB);

                clip(dAlpha - 0.001);

                float3 worldNormal = normalize(UnpackNormal(normalSample));
                worldNormal.xy *= _NormalStrength;
                worldNormal = normalize(worldNormal);

                float2 patternUV = i.worldPos.xz * _WaterPatternScale + worldNormal.xy * _Distortion;
                patternUV += _Time.y * _WaterPatternSpeed;
                float pattern = tex2D(_WaterPatternTex, patternUV).r;
                pattern = saturate((pattern - 0.4) * 3.0);

                float3 lightDir = _WorldSpaceLightPos0.xyz;
                float3 halfDir = normalize(lightDir + i.viewDir);
                float spec = pow(saturate(dot(worldNormal, halfDir)), _Smoothness * 128.0 + 1.0);
                float3 specColor = spec * _SpecularFloat * _LightColor0.rgb;

                float foamT = smoothstep(_FoamThickness, 0, abs(maskTop));
                float foamB = smoothstep(_FoamThickness, 0, abs(maskBottom));
                float foamF = saturate(foamT + foamB);

                fixed4 finalColor = lerp(_MainColor, _FoamColor, foamF);
                finalColor.rgb += pattern * (1.0 - foamF) * _WaterPatternStrength * _MainColor.rgb;
                finalColor.rgb += specColor * (1.0 - foamF);

                float rim = pow(1.0 - saturate(dot(i.viewDir, normalize(i.worldNormal))), _RimPower);
                finalColor.rgb = lerp(finalColor.rgb, _RimColor.rgb, rim * _RimIntensity);
                
                finalColor.rgb *= (facing > 0 ? 1.0 : _BackfaceDarkness);
                finalColor.a = _MainColor.a * dAlpha;

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}