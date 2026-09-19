Shader "Unlit/FX/Liquid2"
{
    Properties
    {
        [Header(Main)]
        [HDR] _Tint ("Tint", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _TopColor ("Top Color", Color) = (1,1,1,1)

        [Header(Foam)]
        [HDR] _FoamColor ("Foam Line Color", Color) = (1,1,1,1)
        _Line ("Foam Line Width", Range(0, 0.2)) = 0.04
        _LineSmooth ("Foam Line Smoothness", Range(0, 0.2)) = 0.05

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0, 10)) = 3

        [Header(Wave Motion)]
        _WobbleX ("Wobble X", Range(0, 1)) = 0.5
        _WobbleZ ("Wobble Z", Range(0, 1)) = 0.5
        _Freq ("Frequency", Range(0, 10)) = 3
        _Amplitude ("Amplitude", Range(0, 0.5)) = 0.08
        _WaveRotation ("Wave Rotation", Range(0, 360)) = 0

        [Header(Fill Settings)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 0.5
        _FillBoundsMin ("Fill Bounds Min Z", Float) = 0
        _FillBoundsMax ("Fill Bounds Max Z", Float) = 2

        [Header(Bubbles)]
        _BubbleTex ("Bubble Texture", 2D) = "black" {}
        [HDR] _BubbleColor ("Bubble Color", Color) = (1,1,1,1)
        _BubbleScale ("Bubble Scale", Range(0.1, 20)) = 5
        _BubbleSpeed ("Bubble Speed", Range(0, 5)) = 1
        _BubbleIntensity ("Bubble Intensity", Range(0, 2)) = 1
        _BubbleSurfaceConcentration ("Bubble Surface Concentration", Range(0.01, 1)) = 0.15
        _BubbleDepthFalloff ("Bubble Depth Falloff", Range(1, 20)) = 8

        _TiltAngle ("Water Tilt Angle", Range(-45, 45)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        LOD 200
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float3 normal : TEXCOORD3;
                float fillEdge : TEXCOORD4;
                float2 bubbleUV : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BubbleTex;
            float4 _BubbleTex_ST;

            float4 _Tint;
            float4 _TopColor;
            float4 _FoamColor;
            float4 _RimColor;
            float4 _BubbleColor;

            float _Line;
            float _LineSmooth;
            float _RimPower;

            float _WobbleX;
            float _WobbleZ;
            float _Freq;
            float _Amplitude;
            float _FillAmount;
            float _FillBoundsMin;
            float _FillBoundsMax;
            float _WaveRotation;

            float _BubbleScale;
            float _BubbleSpeed;
            float _BubbleIntensity;
            float _BubbleSurfaceConcentration;
            float _BubbleDepthFalloff;

            float _TiltAngle;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(UnityWorldSpaceViewDir(o.worldPos));

                // Tính toán sóng wobble dựa trên world position
                float3 worldPos = o.worldPos;
                float time = _Time.y;

                // Xoay vector position theo góc rotation (trong world space)
                float rad = radians(_WaveRotation);
                float cosRot = cos(rad);
                float sinRot = sin(rad);

                float rotatedX = worldPos.x * cosRot - worldPos.z * sinRot;
                float rotatedZ = worldPos.x * sinRot + worldPos.z * cosRot;

                // Wave calculation với multi-octave cho mượt hơn
                float wave1 = sin(time * _Freq + rotatedX * 2.0) * 0.5;
                float wave2 = sin(time * _Freq * 1.5 + rotatedX * 3.0) * 0.3;
                float wave3 = sin(time * _Freq * 0.7 + rotatedZ * 2.5) * 0.2;

                // Fade out sóng khi fillAmount gần 0 hoặc 1
                float wobbleFade = saturate(1.0 - abs(_FillAmount * 2.0 - 1.0));

                float wobbleX = (wave1 + wave2) * _Amplitude * _WobbleX * wobbleFade;
                float wobbleZ = (wave3 + wave1 * 0.5) * _Amplitude * _WobbleZ * wobbleFade;

                // Lấy vị trí Z của object trong world space
                float objectWorldZ = unity_ObjectToWorld._m23;
                float objectWorldX = unity_ObjectToWorld._m03; // Thêm X position

                // Tính world position relative với object
                float relativeZ = worldPos.z - objectWorldZ;
                float relativeX = worldPos.x - objectWorldX; // Thêm relative X

                // Normalize về range -1..1 dựa trên bounds
                float fillRange = _FillBoundsMax - _FillBoundsMin;
                fillRange = (abs(fillRange) < 1e-6) ? 1e-6 : fillRange;
                float normalizedZ = (relativeZ - _FillBoundsMin) / fillRange * 2.0 - 1.0;
                float normalizedX = (relativeX - _FillBoundsMin) / fillRange * 2.0 - 1.0; // Normalize X

                // Tính tilt factor từ góc
                float tiltFactor = tan(radians(_TiltAngle));

                // Remap fillAmount
                float remappedFill = _FillAmount * 2.0 - 1.0;

                // fillEdge với tilt
                o.fillEdge = -normalizedZ + remappedFill + wobbleX + wobbleZ + (normalizedX * tiltFactor);

                // Bubble UV dùng world position XZ (thay vì XY) để phù hợp với fill theo Z
                o.bubbleUV = float2(worldPos.x, worldPos.z) * _BubbleScale;
                o.bubbleUV.y -= time * _BubbleSpeed;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Tint;

                // Tính fill level
                float fillLevel = i.fillEdge;

                // Discard pixels below fill level
                if (fillLevel <= 0)
                {
                    discard;
                }

                // Top color blend
                float topBlend = saturate(fillLevel * 10);
                col = lerp(col, _TopColor, topBlend * 0.5);

                // Foam line với gradient mượt hơn
                float foamEdge = smoothstep(0, _Line, fillLevel);
                float foamFade = smoothstep(_Line, _Line + _LineSmooth, fillLevel);
                float foamAmount = foamEdge * (1.0 - foamFade);
                col = lerp(col, _FoamColor, foamAmount);

                // Bubbles - sample với 2 layer offset
                float2 bubbleUV1 = i.bubbleUV;
                float2 bubbleUV2 = i.bubbleUV * 0.7 + float2(0.3, _Time.y * _BubbleSpeed * 0.5);

                fixed4 bubble1 = tex2D(_BubbleTex, bubbleUV1);
                fixed4 bubble2 = tex2D(_BubbleTex, bubbleUV2);

                // Dùng luminance
                fixed bubble1Val = dot(bubble1.rgb, fixed3(0.299, 0.587, 0.114));
                fixed bubble2Val = dot(bubble2.rgb, fixed3(0.299, 0.587, 0.114));
                fixed bubbleAmount = saturate((bubble1Val + bubble2Val) * _BubbleIntensity);

                // === BUBBLE SURFACE CONCENTRATION ===
                // Bubble tập trung ở bề mặt (fillLevel nhỏ = gần surface)
                // Dùng exponential falloff để bubble giảm dần khi đi sâu vào liquid
                float surfaceDistance = fillLevel / _BubbleSurfaceConcentration;
                float bubbleSurfaceMask = exp(-surfaceDistance * _BubbleDepthFalloff);
                
                // Giữ lại một ít bubble ở sâu để không mất hoàn toàn
                bubbleSurfaceMask = saturate(bubbleSurfaceMask + 0.05);
                
                // Apply mask to bubble amount
                bubbleAmount *= bubbleSurfaceMask;

                col.rgb += _BubbleColor.rgb * bubbleAmount;

                // Rim lighting
                float3 normal = normalize(i.normal);
                float rim = 1.0 - saturate(dot(i.viewDir, normal));
                rim = pow(rim, _RimPower);
                col.rgb += _RimColor.rgb * rim;

                col.a = _Tint.a;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Transparent/VertexLit"
}
