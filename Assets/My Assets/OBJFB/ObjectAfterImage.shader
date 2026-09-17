Shader "Custom/ObjectAfterImage"
{
    Properties
    {
        _Color ("Ghost Color", Color) = (0.2,0.8,1,1)

        _Alpha ("Alpha", Range(0,1)) = 0.15

        _Emission ("Emission", Range(0,5)) = 2

        _RGBOffset ("RGB Offset", Range(0,0.05)) = 0.01

        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 2
    }


    SubShader
    {

        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }


        Blend One One
        ZWrite Off
        Cull Back



        Pass
        {

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"



            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };



            CBUFFER_START(UnityPerMaterial)

            float4 _Color;

            float _Alpha;

            float _Emission;

            float _RGBOffset;

            float _FresnelPower;


            CBUFFER_END




            Varyings vert(Attributes IN)
            {

                Varyings OUT;


                float3 worldPos =
                    TransformObjectToWorld(
                        IN.positionOS.xyz
                    );


                OUT.positionCS =
                    TransformWorldToHClip(
                        worldPos
                    );


                OUT.normalWS =
                    TransformObjectToWorldNormal(
                        IN.normalOS
                    );


                OUT.viewDirWS =
                    GetWorldSpaceViewDir(
                        worldPos
                    );


                return OUT;
            }




            half4 frag(Varyings IN) : SV_Target
            {


                float3 normal =
                    normalize(IN.normalWS);


                float3 viewDir =
                    normalize(IN.viewDirWS);



                // Fresnel edge
                float fresnel =
                    pow(
                    1-dot(normal,viewDir),
                    _FresnelPower
                    );



                // RGB glitch separation

                float3 color;


                color.r =
                    _Color.r
                    +
                    _RGBOffset;


                color.g =
                    _Color.g;


                color.b =
                    _Color.b
                    -
                    _RGBOffset;



                float intensity =
                    fresnel *
                    _Emission;



                color *= intensity;



                return float4(
                    color,
                    _Alpha*fresnel
                );

            }


            ENDHLSL

        }
    }
}