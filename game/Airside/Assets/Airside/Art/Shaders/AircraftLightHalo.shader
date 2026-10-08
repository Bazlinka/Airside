Shader "Airside/AircraftLightHalo"
{
    Properties
    {
        [HDR] _BaseColor ("Light colour and strength", Color) = (1, 1, 1, 1)
        _HazeExponent ("Light-source haze transmission", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _HazeExponent;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float haze : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.corner = input.uv * 2.0 - 1.0;
                // The same source-light transmission as AirfieldLightPoint. Surface MixFog
                // instead mixes an additive lamp into dark fog, erasing it on night final.
                float f = unity_FogParams.x * distance(world, _WorldSpaceCameraPos);
                output.haze = exp2(-f * f * _HazeExponent);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half r2 = dot(input.corner, input.corner);
                clip(1.0 - r2);
                half core = exp(-r2 * 9.0);
                half skirt = saturate(1.0 - r2) * 0.22;
                return half4(_BaseColor.rgb * (core + skirt) * input.haze, 0);
            }
            ENDHLSL
        }
    }
}
