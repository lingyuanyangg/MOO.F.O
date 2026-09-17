Shader "Custom/RandomHeightDatamosh"
{

Properties
{

_Strength(
"Tear Strength",
Range(0,0.3)
)=0.05


_Slices(
"Slice Amount",
Range(3,30)
)=12


_Probability(
"Glitch Probability",
Range(0,1)
)=0.2


_Speed(
"Change Speed",
Range(1,20)
)=5


_Intensity(
"Intensity",
Range(0,1)
)=1

}


SubShader
{

Tags
{
"RenderPipeline"="UniversalPipeline"
}


Pass
{


HLSLPROGRAM


#pragma vertex Vert
#pragma fragment Frag


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"



TEXTURE2D_X(_BlitTexture);

SAMPLER(sampler_BlitTexture);



float _Strength;

float _Slices;

float _Probability;

float _Speed;

float _Intensity;



struct Attributes
{
uint vertexID:SV_VertexID;
};



struct Varyings
{
float4 positionCS:SV_POSITION;
float2 uv:TEXCOORD0;
};



Varyings Vert(Attributes input)
{

Varyings o;


o.positionCS =
GetFullScreenTriangleVertexPosition(
input.vertexID);


o.uv =
GetFullScreenTriangleTexCoord(
input.vertexID);


return o;

}



float random(float x)
{

return frac(
sin(x*91.345)
*
47453.5453
);

}





half4 Frag(Varyings input)
:SV_Target
{


float2 originalUV = input.uv;

float2 uv = originalUV;



// 当前glitch frame

float frame =
floor(
_Time.y*_Speed
);



float accumulated = 0;


float sliceIndex = 0;



// 找当前Y属于哪个随机slice

for(int i=0;i<30;i++)
{

if(i >= _Slices)
break;



float h =
random(
i + frame*100
);



h =
lerp(
0.01,
0.15,
h
);



if(
uv.y >= accumulated &&
uv.y < accumulated+h
)
{

sliceIndex=i;

break;

}



accumulated += h;

}




// 当前slice随机决定是否移动

float chance =
random(
sliceIndex
+
frame*200
);



float offset=0;



if(chance < _Probability)
{


float move =
random(
sliceIndex+50
+
frame*300
);


offset =
(move-0.5)
*
_Strength;


}



// glitch后的UV

float2 glitchUV = uv;


glitchUV.x += offset;



// 根据Intensity混合

uv = lerp(
originalUV,
glitchUV,
_Intensity
);



return SAMPLE_TEXTURE2D_X(
_BlitTexture,
sampler_BlitTexture,
uv
);



}



ENDHLSL


}

}

}