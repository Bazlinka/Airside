Shader "Airside/CloudAtlas"
{
    Properties
    {
        _BaseMap ("Cloud atlas", 2D) = "white" {}
        _BaseColor ("Weather tint", Color) = (1, 1, 1, 1)
        _AtlasRect ("Atlas scale and offset", Vector) = (0.25, 0.25, 0, 0)
        _AlphaFloor ("Artifact alpha floor", Range(0, 0.5)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudAtlas"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _AtlasRect;
                float _AlphaFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _AtlasRect.xy + _AtlasRect.zw;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 cloud = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                // The generator can leave saturated RGB in pixels whose alpha is effectively
                // zero. Reject that invisible fringe, then keep the authored soft edge above it.
                half alpha = smoothstep(_AlphaFloor, _AlphaFloor + 0.18h, cloud.a) * _BaseColor.a;
                clip(alpha - 0.01h);
                half luma = dot(cloud.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 neutralCloud = lerp(luma.xxx, cloud.rgb, 0.18h) * _BaseColor.rgb;
                return half4(MixFog(neutralCloud, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
