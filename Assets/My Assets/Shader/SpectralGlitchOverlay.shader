Shader "Hand2/SpectralGlitchOverlay"
{
    Properties { _Strength("Strength", Range(0,1))=0.15 _HitStrength("Hit Strength",Range(0,1))=0 _HitCenter("Hit Center",Vector)=(0.5,0.5,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };
            float _Strength,_HitStrength; float4 _HitCenter;
            V vert(A i){V o;o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            half4 frag(V i):SV_Target
            {
                float t=floor(_Time.y*5.0);
                float2 cell=floor(i.uv*float2(14,9));
                float n=hash(cell+t);
                float burst=step(0.91,n)*step(0.72,frac(_Time.y*0.67+n));
                float scan=0.5+0.5*sin(i.uv.y*900.0+_Time.y*11.0);
                float bar=step(0.965,frac(i.uv.y*23.0-_Time.y*0.22));
                half3 rgb=lerp(half3(0.02,0.95,1.0),half3(1.0,0.02,0.62),step(0.5,n));
                float hitBand=saturate(1.0-abs(i.uv.y-_HitCenter.y)*5.5);
                float hitCells=step(0.34,hash(cell+floor(_Time.y*31.0)+17.0))*hitBand*_HitStrength;
                float pixelShift=step(0.5,hash(float2(cell.y,floor(_Time.y*24.0))))*hitCells;
                rgb=lerp(rgb,half3(1.0,0.05,0.72),pixelShift);
                float alpha=(burst*0.13+bar*0.055+scan*0.018)*_Strength + hitCells*0.42;
                float edge=pow(abs(i.uv.x-0.5)*2.0,5.0)*0.035*_Strength;
                return half4(rgb,alpha+edge);
            }
            ENDHLSL
        }
    }
}
