Shader "Hand2/SpectralSky"
{
    Properties { _Intensity("Intensity", Range(0,3)) = 1.2 _NoiseSeed("Noise Seed",Float)=13.37 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };
            float _Intensity, _NoiseSeed;
            V vert(A i) { V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            half4 frag(V i):SV_Target
            {
                float rawTime=_Time.y*0.11;
                float segment=floor(rawTime);
                float f=frac(rawTime); f=f*f*(3.0-2.0*f);
                float na=frac(sin((segment+_NoiseSeed)*78.233)*43758.5453);
                float nb=frac(sin((segment+1.0+_NoiseSeed)*78.233)*43758.5453);
                float noiseTime=lerp(na,nb,f);
                float t=_Time.y*(0.025+noiseTime*0.075)+noiseTime*9.0;
                float drift=sin(i.uv.y*9.0+t*2.0)*0.055+sin(i.uv.y*31.0-t)*0.018;
                float y=saturate(i.uv.y+drift);
                float hue=frac(noiseTime*1.73 + y*0.58 + _Time.y*(0.008+na*0.018));
                half3 col=saturate(abs(frac(hue+half3(0.0,0.6667,0.3333))*6.0-3.0)-1.0);
                float hue2=frac(hue+0.24+nb*0.35);
                half3 second=saturate(abs(frac(hue2+half3(0.0,0.6667,0.3333))*6.0-3.0)-1.0);
                col=lerp(col,second,smoothstep(0.25,0.82,y));
                col=pow(max(col,0.001h),0.72h)*1.32h;
                float band=0.82+0.18*sin((i.uv.y+t)*64.0);
                float scan=0.94+0.06*sin(i.uv.y*720.0+t*8.0);
                float split=step(0.985,frac(i.uv.y*13.0+t))*0.35;
                col.rb += half2(split,-split*0.4);
                float stateTime=_Time.y*0.16+_NoiseSeed;
                float stateCell=floor(stateTime);
                float stateFrac=frac(stateTime);
                float stateA=step(0.24,frac(sin((stateCell+_NoiseSeed)*43.133)*9137.713));
                float stateB=step(0.24,frac(sin((stateCell+1.0+_NoiseSeed)*43.133)*9137.713));
                float visibility=lerp(stateA,stateB,smoothstep(0.68,0.98,stateFrac));
                return half4(col*band*scan*_Intensity,visibility*0.94);
            }
            ENDHLSL
        }
    }
}
