Shader "Custom/RGBSplit_Individual"
{

    Properties
    {
        _ROffset("Red Offset X", Range(-0.05,0.05)) = 0.005

        _GOffset("Green Offset X", Range(-0.05,0.05)) = 0

        _BOffset("Blue Offset X", Range(-0.05,0.05)) = -0.005


        _RVertical("Red Offset Y", Range(-0.05,0.05)) = 0

        _GVertical("Green Offset Y", Range(-0.05,0.05)) = 0

        _BVertical("Blue Offset Y", Range(-0.05,0.05)) = 0
    }


    SubShader
    {

        Tags
        {
            "RenderPipeline"="UniversalPipeline"
        }


        Pass
        {

            Name "RGB Split Individual"


            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"



            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);



            float _ROffset;
            float _GOffset;
            float _BOffset;


            float _RVertical;
            float _GVertical;
            float _BVertical;



            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv:TEXCOORD0;
            };



            Varyings Vert(Attributes input)
            {

                Varyings output;

                output.positionCS =
                GetFullScreenTriangleVertexPosition(
                input.vertexID);


                output.uv =
                GetFullScreenTriangleTexCoord(
                input.vertexID);


                return output;
            }




            half4 Frag(Varyings input)
            :SV_Target
            {


                float2 uv=input.uv;



                float2 rUV =
                uv +
                float2(
                _ROffset,
                _RVertical
                );



                float2 gUV =
                uv +
                float2(
                _GOffset,
                _GVertical
                );



                float2 bUV =
                uv +
                float2(
                _BOffset,
                _BVertical
                );



                float r =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_BlitTexture,
                    rUV
                ).r;



                float g =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_BlitTexture,
                    gUV
                ).g;



                float b =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_BlitTexture,
                    bUV
                ).b;



                return float4(
                r,
                g,
                b,
                1
                );

            }


            ENDHLSL
        }

    }
}