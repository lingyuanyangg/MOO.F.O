Shader "Hidden/TD_Feedback"
{
    Properties
    {
        _MainTex ("Current Frame", 2D) = "white" {}
        _PrevTex ("Previous Frame", 2D) = "white" {}
        _Decay ("Decay Speed", Range(0.8, 0.999)) = 0.96
        _Zoom ("Zoom Factor", Range(0.95, 1.05)) = 1.002
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            Texture2D _PrevTex;
            SamplerState sampler_PrevTex;

            float _Decay;
            float _Zoom;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float2 ZoomUV(float2 uv, float zoom)
            {
                return (uv - 0.5) / zoom + 0.5;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 current = _MainTex.Sample(sampler_MainTex, input.uv);
                
                // 对上一帧图像微调放缩，产生延伸感
                float2 prevUV = ZoomUV(input.uv, _Zoom);
                float4 prev = _PrevTex.Sample(sampler_PrevTex, prevUV);

                // 历史帧衰减
                prev *= _Decay;

                // 取当前帧与上一帧的最大值叠加
                return max(current, prev);
            }
            ENDHLSL
        }
    }
}