Shader "Hand2/SpectralEnergyLine"
{
    Properties { [HDR]_BaseColor("Color",Color)=(0,2,3,1) _PulseSpeed("Pulse Speed",Float)=5 _BandScale("Band Scale",Float)=18 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            half4 _BaseColor; float _PulseSpeed,_BandScale;
            V vert(A i){V o;o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(V i):SV_Target
            {
                float core=saturate(1.0-abs(i.uv.y-0.5)*2.0);
                float contour=0.48+0.52*step(0.52,sin(i.uv.x*_BandScale-_Time.y*_PulseSpeed));
                float pulse=0.78+0.22*sin(_Time.y*_PulseSpeed+i.uv.x*22.0);
                return half4(_BaseColor.rgb*i.color.rgb*contour*pulse, _BaseColor.a*i.color.a*core);
            }
            ENDHLSL
        }
    }
}
