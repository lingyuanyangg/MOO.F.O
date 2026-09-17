Shader "Hand2/PopArtToonOutline"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.24,0.08,0.38,1)
        _ToonSteps ("Toon Steps", Range(2,4)) = 3
        _UseDuotone ("Use Duotone", Range(0,1)) = 0
        _DarkColor ("Duotone Dark", Color) = (0.08,0.015,0.16,1)
        _LightColor ("Duotone Light", Color) = (1,0.82,0.92,1)
        _UseGradient ("Use Distance Gradient", Range(0,1)) = 0
        [HDR] _NearColor ("Near Color", Color) = (0.2,1.1,0.08,1)
        [HDR] _FarColor ("Far Color", Color) = (1.1,1.25,0.04,1)
        _GradientStart ("Gradient Start", Float) = 6
        _GradientEnd ("Gradient End", Float) = 38
        [HDR] _FresnelColor ("Fresnel Color", Color) = (0,0,0,1)
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 3
        _FresnelStrength ("Fresnel Strength", Range(0,3)) = 0
        _OutlineColor ("Outline Color", Color) = (0.055,0.01,0.11,1)
        _OutlineWidth ("Outline Width", Range(0,0.05)) = 0.009
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fogFactor : TEXCOORD3; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _ShadowColor, _DarkColor, _LightColor;
                half4 _NearColor, _FarColor, _FresnelColor, _OutlineColor;
                float _ToonSteps, _UseDuotone, _UseGradient;
                float _GradientStart, _GradientEnd, _FresnelPower, _FresnelStrength, _OutlineWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sampleColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half luminance = dot(sampleColor.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 duotone = lerp(_DarkColor.rgb, _LightColor.rgb, smoothstep(0.22h, 0.78h, luminance));
                half3 baseColor = lerp(sampleColor.rgb, duotone, saturate(_UseDuotone)) * _BaseColor.rgb;

                float cameraDistance = distance(input.positionWS.xz, _WorldSpaceCameraPos.xz);
                half distanceBlend = saturate((cameraDistance - _GradientStart) / max(0.01, _GradientEnd - _GradientStart));
                half3 gradientTint = lerp(_NearColor.rgb, _FarColor.rgb, distanceBlend);
                baseColor *= lerp(half3(1,1,1), gradientTint, saturate(_UseGradient));
                float2 gridUV = input.positionWS.xz;
                float checker = fmod(floor(gridUV.x * 0.72) + floor(gridUV.y * 0.72), 2.0);
                float radial = sin(length(gridUV - float2(sin(_Time.y*0.12)*3.0, cos(_Time.y*0.1)*3.0)) * 4.5 - _Time.y * 1.25);
                float moire = sin(gridUV.x * 8.0 + sin(gridUV.y * 2.1 + _Time.y*0.35) * 2.0);
                half3 patternColor = lerp(half3(0.04,0.7,0.82), half3(0.92,0.03,0.62), checker);
                float pattern = (smoothstep(0.72,0.96,radial) + smoothstep(0.82,0.99,moire)) * 0.12;
                baseColor += patternColor * pattern * saturate(_UseGradient);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half steps = max(2.0h, _ToonSteps);
                half band = floor(ndl * steps) / max(1.0h, steps - 1.0h);
                // Retain the spectral light hue while keeping a neutral floor in every channel.
                // This prevents complementary-colour phases from crushing whole objects to black.
                half3 spectralLight = lerp(half3(1.0h, 1.0h, 1.0h), mainLight.color, 0.72h);
                half3 lighting = lerp(_ShadowColor.rgb, spectralLight, band);
                lighting += SampleSH(normalWS) * 0.48h;

                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirection)), _FresnelPower) * _FresnelStrength;
                half3 finalColor = baseColor * lighting + _FresnelColor.rgb * fresnel;
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, sampleColor.a * _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PopOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; };
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _ShadowColor, _DarkColor, _LightColor;
                half4 _NearColor, _FarColor, _FresnelColor, _OutlineColor;
                float _ToonSteps, _UseDuotone, _UseGradient;
                float _GradientStart, _GradientEnd, _FresnelPower, _FresnelStrength, _OutlineWidth;
            CBUFFER_END

            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                float3 expanded = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionHCS = TransformObjectToHClip(expanded);
                return output;
            }
            half4 OutlineFrag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    FallBack Off
}
