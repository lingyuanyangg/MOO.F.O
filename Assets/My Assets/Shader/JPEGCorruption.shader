Shader "Custom/JPEGVariableBlockCorruption"
{

Properties
{

    _BlockSizeMin(
        "Minimum Block Size",
        Range(4,32)
    ) = 8


    _BlockSizeMax(
        "Maximum Block Size",
        Range(8,80)
    ) = 32



    _Corruption(
        "Corruption Amount",
        Range(0,1)
    ) = 0.25



    _Intensity(
        "Glitch Intensity",
        Range(0,1)
    ) = 1



    _BlockOffset(
        "Block Displacement",
        Range(0,0.2)
    ) = 0.05



    _Quantization(
        "JPEG Quantization",
        Range(1,64)
    ) = 16



    _ChromaShift(
        "Chroma Shift",
        Range(0,0.05)
    ) = 0.01



    _NoiseAmount(
        "Block Noise",
        Range(0,1)
    ) = 0.1



    _SizeRandomness(
        "Block Size Randomness",
        Range(0,1)
    ) = 1



    _Speed(
        "Glitch Speed",
        Range(1,30)
    ) = 5

}



SubShader
{

Tags
{
    "RenderPipeline"="UniversalPipeline"
}



Pass
{

Name "JPEG Variable Block Corruption"



HLSLPROGRAM


#pragma vertex Vert
#pragma fragment Frag


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"




TEXTURE2D_X(_BlitTexture);

SAMPLER(sampler_BlitTexture);




float _BlockSizeMin;

float _BlockSizeMax;

float _Corruption;

float _Intensity;

float _BlockOffset;

float _Quantization;

float _ChromaShift;

float _NoiseAmount;

float _SizeRandomness;

float _Speed;





struct Attributes
{
    uint vertexID : SV_VertexID;
};



struct Varyings
{
    float4 positionCS : SV_POSITION;

    float2 uv:TEXCOORD0;
};








Varyings Vert(
Attributes input
)
{

    Varyings output;


    output.positionCS =
    GetFullScreenTriangleVertexPosition(
        input.vertexID
    );


    output.uv =
    GetFullScreenTriangleTexCoord(
        input.vertexID
    );


    return output;

}







float random(float2 p)
{

    return frac(
        sin(
            dot(
                p,
                float2(
                    12.9898,
                    78.233
                )
            )
        )
        *
        43758.5453
    );

}








half4 Frag(
Varyings input
)
:SV_Target
{


    float2 uv =
    input.uv;



    float frame =
    floor(
        _Time.y *
        _Speed
    );





    //==================================
    // Original Image
    //==================================


    float3 originalColor =
    SAMPLE_TEXTURE2D_X(
        _BlitTexture,
        sampler_BlitTexture,
        uv
    )
    .rgb;



    float3 color =
    originalColor;







    //==================================
    // Variable JPEG Block
    //==================================


    float2 coarseGrid =
    floor(
        uv *
        32
    );



    float sizeSeed =
    random(
        coarseGrid +
        frame
    );



    float blockSize =
    lerp(
        _BlockSizeMin,
        _BlockSizeMax,
        sizeSeed *
        _SizeRandomness
    );



    if(_SizeRandomness < 0.01)
    {

        blockSize =
        _BlockSizeMin;

    }






    float2 blockID =
    floor(
        uv *
        blockSize
    );









    //==================================
    // Corruption Mask
    //==================================


    float corruptionSeed =
    random(
        blockID +
        frame*10
    );



    float corrupted =
    step(
        corruptionSeed,
        _Corruption
    );








    //==================================
    // Block displacement
    //==================================


    float2 sampleUV =
    uv;



    if(corrupted > 0.5)
    {


        float x =
        random(
            blockID+
            frame*20
        );



        float y =
        random(
            blockID+
            frame*30
        );



        sampleUV +=
        float2(
            x-0.5,
            y-0.5
        )
        *
        _BlockOffset;


    }








    color =
    SAMPLE_TEXTURE2D_X(
        _BlitTexture,
        sampler_BlitTexture,
        sampleUV
    )
    .rgb;








    //==================================
    // JPEG Quantization
    //==================================


    if(corrupted > 0.5)
    {


        color =
        floor(
            color *
            _Quantization
        )
        /
        _Quantization;


    }








    //==================================
    // Chroma Shift
    //==================================


    if(corrupted > 0.5)
    {


        float red =
        SAMPLE_TEXTURE2D_X(
            _BlitTexture,
            sampler_BlitTexture,
            sampleUV +
            float2(
                _ChromaShift,
                0
            )
        )
        .r;



        float blue =
        SAMPLE_TEXTURE2D_X(
            _BlitTexture,
            sampler_BlitTexture,
            sampleUV -
            float2(
                _ChromaShift,
                0
            )
        )
        .b;



        color.r =
        red;



        color.b =
        blue;


    }









    //==================================
    // Block Noise
    //==================================


    float noiseSeed =
    random(
        blockID+
        frame*100
    );



    if(
        corrupted > 0.5 &&
        noiseSeed < _NoiseAmount
    )
    {


        float3 noiseColor =
        float3(
            random(blockID+1),
            random(blockID+20),
            random(blockID+40)
        );



        color =
        lerp(
            color,
            noiseColor,
            0.8
        );


    }









    //==================================
    // Global Intensity
    //==================================


    color =
    lerp(
        originalColor,
        color,
        _Intensity
    );








    return float4(
        color,
        1
    );



}



ENDHLSL

}

}

}