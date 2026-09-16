Shader "Airside/Surroundings"
{
    // Vertex-coloured land, beach and sea around the airfield. Colour comes from the
    // vertex (alpha = water sheen), lit like Airside/AdelaideGround so the two meet
    // without a seam, with scene fog plus a fade to the fog colour before the far clip
    // so the gulf never ends in a hard line against the sky.
    Properties
    {
        _HorizonFadeStart ("Horizon Fade Start", Float) = 6500
        _HorizonFadeEnd ("Horizon Fade End", Float) = 9600
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            // Without this the soft-shadow variant is never compiled for this shader, so
            // the coastal plain took hard-edged shadows while the airfield beside it (which
            // does declare it, in Airside/AdelaideGround) took soft ones.
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HorizonFadeStart;
                float _HorizonFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 color = input.color.rgb * (mainLight.color * (mainLight.shadowAttenuation * NdotL) + SampleSH(normalWS));

                // Water sheen: a broad sun glint, strongest looking into the light.
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float glint = pow(saturate(dot(normalWS, halfDir)), 64.0) * input.color.a * 0.6;
                color += mainLight.color * glint;

                color = MixFog(color, input.fogFactor);
                float distanceWS = length(input.positionWS - GetCameraPositionWS());
                float fade = smoothstep(_HorizonFadeStart, _HorizonFadeEnd, distanceWS);
                color = lerp(color, unity_FogColor.rgb, fade);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
