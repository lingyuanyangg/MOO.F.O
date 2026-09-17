Shader "Hand2/StylizedWindGrass"
{
    Properties
    {
        _BottomColor ("Root Color", Color) = (0.035, 0.18, 0.025, 1)
        _TopColor ("Tip Color", Color) = (0.28, 0.72, 0.12, 1)
        [HDR] _NearTint ("Near Fluorescent Tint", Color) = (0.42, 1.35, 0.06, 1)
        [HDR] _FarTint ("Far Lemon Tint", Color) = (1.15, 1.35, 0.04, 1)
        _DistanceGradientStart ("Distance Gradient Start", Float) = 7
        _DistanceGradientEnd ("Distance Gradient End", Float) = 34
        _ToonSteps ("Toon Light Steps", Range(2, 4)) = 3
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.35, 0)
        _WindStrength ("Wind Strength", Range(0, 1.5)) = 0.42
        _WindSpeed ("Wind Speed", Range(0, 8)) = 2.2
        _WindScale ("Wind Wave Scale", Range(0.05, 3)) = 0.55
        _Cutoff ("Blade Cutoff", Range(0, 0.45)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200
        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float variation : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BottomColor;
                half4 _TopColor;
                half4 _NearTint;
                half4 _FarTint;
                float _DistanceGradientStart;
                float _DistanceGradientEnd;
                float _ToonSteps;
                float4 _WindDirection;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _Cutoff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float tipWeight = saturate(input.uv.y);
                float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0001));

                float spatialNoise = sin(positionWS.x * 1.73 + positionWS.z * 2.11) * 1.35;
                float phase = dot(positionWS.xz, windDir) * _WindScale + _Time.y * _WindSpeed + spatialNoise;
                float broadWave = sin(phase) * 0.68;
                float fineWave = sin(phase * 1.91 + positionWS.x * 0.37) * 0.22;
                float gust = broadWave + fineWave;
                float bend = gust * _WindStrength * tipWeight * tipWeight;

                positionWS.xz += windDir * bend;
                positionWS.y -= abs(bend) * 0.07 * tipWeight;

                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.variation = input.color.r;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float centeredX = abs(input.uv.x - 0.5);
                float bladeHalfWidth = lerp(0.48, _Cutoff, smoothstep(0.0, 1.0, input.uv.y));
                clip(bladeHalfWidth - centeredX);

                half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0h : -1.0h);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half steps = max(2.0h, _ToonSteps);
                diffuse = floor(diffuse * steps) / max(1.0h, steps - 1.0h);
                diffuse = diffuse * 0.52h + 0.48h;
                half3 baseColor = lerp(_BottomColor.rgb, _TopColor.rgb, input.uv.y);
                baseColor *= lerp(0.78h, 1.18h, input.variation);
                float cameraDistance = distance(input.positionWS.xz, _WorldSpaceCameraPos.xz);
                float distanceBlend = saturate((cameraDistance - _DistanceGradientStart) /
                    max(0.01, _DistanceGradientEnd - _DistanceGradientStart));
                baseColor *= lerp(_NearTint.rgb, _FarTint.rgb, distanceBlend);
                float2 warped = input.positionWS.xz;
                warped += float2(sin(warped.y * 0.21 + _Time.y * 0.035), cos(warped.x * 0.18 - _Time.y * 0.028)) * 2.15;
                float2 regionCell = floor(warped / 4.2);
                float regionNoise = frac(sin(dot(regionCell, float2(127.1,311.7))) * 43758.5453);
                float regionIndex = floor(regionNoise * 5.0);
                half3 currentColor = baseColor;
                half3 regionColor = regionIndex < 1.0 ? half3(0.010h,1.90h,0.018h) :
                    regionIndex < 2.0 ? half3(2.05h,0.68h,0.004h) :
                    regionIndex < 3.0 ? half3(0.010h,1.12h,2.05h) :
                    regionIndex < 4.0 ? half3(0.62h,2.08h,0.004h) : half3(2.05h,0.018h,0.92h);
                float fluorescentPulse = 1.0 + sin(_Time.y * 1.4 + regionNoise * 9.0) * 0.12;
                baseColor = regionColor * fluorescentPulse * 0.92h + currentColor * 0.20h;
                half3 spectralLight = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.68h);
                half3 litColor = baseColor * (spectralLight * diffuse + SampleSH(normalWS) * 0.45h);
                litColor = MixFog(litColor, input.fogFactor);
                return half4(litColor, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
