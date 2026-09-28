Shader "Airside/SuburbBuildings"
{
    // ADR 0159 — the houses, shops and sheds around the airfield, extruded from OpenStreetMap.
    // Walls take their vertex colour (render, brick, bluestone). Roofs (vertex alpha 1) take the
    // Sentinel-2 image at their own x/z, so every roof is the colour the satellite saw on that
    // spot and the extruded suburb sits on the imagery without a colour step. Lit like
    // Airside/Surroundings, with the same fog and horizon fade.
    Properties
    {
        _SatelliteAlbedo ("Adelaide Sentinel-2 albedo", 2D) = "gray" {}
        _SatelliteExtent ("Satellite half extent metres", Float) = 12000
        _SatelliteTint ("Satellite exposure tint", Color) = (0.56, 0.58, 0.56, 1)
        _RoofGain ("Roof brightness over the ground image", Float) = 1.1
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SatelliteAlbedo); SAMPLER(sampler_SatelliteAlbedo);

            CBUFFER_START(UnityPerMaterial)
                float4 _SatelliteTint;
                float _SatelliteExtent;
                float _RoofGain;
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

                float2 satelliteUv = saturate(input.positionWS.xz / (2.0 * max(_SatelliteExtent, 1.0)) + 0.5);
                float3 roof = SAMPLE_TEXTURE2D(_SatelliteAlbedo, sampler_SatelliteAlbedo, satelliteUv).rgb
                    * _SatelliteTint.rgb * _RoofGain;
                float3 albedo = lerp(input.color.rgb, roof, saturate(input.color.a));
                float3 color = albedo * (mainLight.color * (mainLight.shadowAttenuation * NdotL) + SampleSH(normalWS));

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
