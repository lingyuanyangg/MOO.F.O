Shader "Custom/ThresholdDuotone"
{

    Properties
    {
        _Threshold("Threshold", Range(0,1)) = 0.5

        _DarkColor("Dark Color", Color) = (0,0,0,1)

        _LightColor("Light Color", Color) = (1,0,0,1)
        _Intensity("Intensity",Range(0,1))=1
    }


    SubShader
    {

        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }


        Pass
        {

            Name "ThresholdDuotone"


            HLSLPROGRAM


            #pragma vertex Vert
            #pragma fragment Frag


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            TEXTURE2D_X(_BlitTexture);

            SAMPLER(sampler_BlitTexture);



            float _Threshold;

            float4 _DarkColor;

            float4 _LightColor;

            float _Intensity;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float2 uv : TEXCOORD0;
            };



            Varyings Vert(Attributes input)
            {

                Varyings output;


                output.positionCS =
                GetFullScreenTriangleVertexPosition(input.vertexID);


                output.uv =
                GetFullScreenTriangleTexCoord(input.vertexID);


                return output;

            }



            half4 Frag(Varyings input)
                : SV_Target
            {


                float4 color =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_BlitTexture,
                    input.uv
                );



                float luminance =
                dot(
                    color.rgb,
                    float3(
                    0.2126,
                    0.7152,
                    0.0722)
                );



                float mask =
                step(
                    _Threshold,
                    luminance
                );



             float3 duotoneColor =
            lerp(
            _DarkColor.rgb,
            _LightColor.rgb,
            mask
            );


            // intensity混合
            float3 result =
            lerp(
                color.rgb,
                duotoneColor,
                _Intensity
            );


            return float4(result,1);


            }


            ENDHLSL

        }

    }

}