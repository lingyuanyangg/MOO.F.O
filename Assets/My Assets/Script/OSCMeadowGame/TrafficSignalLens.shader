Shader "Hand2/TrafficSignalLens"
{
    Properties
    {
        _SignalIndex ("Active: Red 0, Yellow 1, Green 2", Float) = 2
        _Brightness ("Glow", Range(0, 4)) = 1.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float x : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float _SignalIndex;
            float _Brightness;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.x = v.positionOS.x;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float index = i.x < -0.5 ? 0 : (i.x > 0.5 ? 2 : 1);
                half3 color = index < 0.5 ? half3(1,0.015,0.025) :
                    (index < 1.5 ? half3(1,0.65,0.005) : half3(0.025,1,0.08));
                float lit = 1 - step(0.5, abs(index - _SignalIndex));
                return half4(color * lerp(0.035, _Brightness, lit), 1);
            }
            ENDHLSL
        }
    }
}
