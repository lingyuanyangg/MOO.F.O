Shader "Hand2/PopArtSpectralGlass"
{
    Properties
    {
        [MainTexture] _BaseMap("Glass Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Texture Tint", Color) = (1,1,1,1)
        _GlassTint("Glass Tint", Color) = (0.08,0.85,1,1)
        [HDR] _FresnelColor("Fresnel Glow", Color) = (2.6,0.04,1.5,1)
        _GlassOpacity("Glass Opacity", Range(0,1)) = 0.2
        _TextureOpacity("Texture Alpha Strength", Range(0,1)) = 0.82
        _FresnelPower("Fresnel Power", Range(0.25,8)) = 2.6
        _FresnelStrength("Fresnel Strength", Range(0,5)) = 0.8
        _FresnelOpacity("Fresnel Opacity", Range(0,1)) = 0.24
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SpectralGlass"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _GlassTint;
                half4 _FresnelColor;
                half _GlassOpacity;
                half _TextureOpacity;
                half _FresnelPower;
                half _FresnelStrength;
                half _FresnelOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 textureSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), _FresnelPower);

                // Preserve the source texture so the opaque alien/pilot details remain crisp.
                half sourceMask = saturate(textureSample.a * _TextureOpacity);
                half3 clearGlass = lerp(textureSample.rgb, _GlassTint.rgb, 0.22h);
                half3 color = clearGlass + _FresnelColor.rgb * fresnel * _FresnelStrength;
                half alpha = saturate(max(_GlassOpacity, sourceMask) + fresnel * _FresnelOpacity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
