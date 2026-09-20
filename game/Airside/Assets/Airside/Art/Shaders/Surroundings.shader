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
        _AirfieldAlbedo ("Airfield edge albedo", 2D) = "white" {}
        _SatelliteAlbedo ("Adelaide Sentinel-2 albedo", 2D) = "gray" {}
        _SatelliteExtent ("Satellite half extent metres", Float) = 12000
        _SatelliteStrength ("Satellite blend", Range(0, 1)) = 0.92
        _SatelliteTint ("Satellite exposure tint", Color) = (0.56, 0.58, 0.56, 1)
        _AirfieldTint ("Airfield edge tint", Color) = (0.59, 0.61, 0.55, 1)
        _AirfieldHalfX ("Airfield half width X", Float) = 1950
        _AirfieldHalfZ ("Airfield half width Z", Float) = 1400
        _EdgeTextureBlend ("Edge texture blend metres", Float) = 1050
        _DryTile ("Dry tile metres", Float) = 47
        _MacroScale ("Macro variation metres", Float) = 240
        _MacroStrength ("Macro brightness", Float) = 0.08
        _FarBlendStart ("Far detail start metres", Float) = 120
        _FarBlendEnd ("Far detail end metres", Float) = 900
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

            TEXTURE2D(_AirfieldAlbedo); SAMPLER(sampler_AirfieldAlbedo);
            TEXTURE2D(_SatelliteAlbedo); SAMPLER(sampler_SatelliteAlbedo);

            CBUFFER_START(UnityPerMaterial)
                float _HorizonFadeStart;
                float _HorizonFadeEnd;
                float4 _AirfieldTint;
                float4 _SatelliteTint;
                float _SatelliteExtent;
                float _SatelliteStrength;
                float _AirfieldHalfX;
                float _AirfieldHalfZ;
                float _EdgeTextureBlend;
                float _DryTile;
                float _MacroScale;
                float _MacroStrength;
                float _FarBlendStart;
                float _FarBlendEnd;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float MacroNoise(float2 xz)
            {
                float2 p = xz / max(_MacroScale, 1.0);
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.7 + 17.3) * 0.35;
            }

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
                float2 xz = input.positionWS.xz;
                float2 outsideAxis = max(abs(xz) - float2(_AirfieldHalfX, _AirfieldHalfZ), 0.0);
                float outside = length(outsideAxis);
                float edgeBlend = 1.0 - smoothstep(0.0, max(_EdgeTextureBlend, 1.0), outside);

                // Carry the airfield's dry-grass detail beyond the rectangular mesh edge,
                // then dissolve it into the OSM land-cover palette. AdelaideGround at the
                // same rectangle is already satelliteStrength satellite, so the grass
                // weight here starts at (1 - strength) or the join is a bright hairline.
                float2 uv = xz / max(_DryTile, 1.0);
                float3 edgeAlbedo = SAMPLE_TEXTURE2D(_AirfieldAlbedo, sampler_AirfieldAlbedo, uv).rgb;
                float2 farUv = mul(float2x2(0.8, -0.6, 0.6, 0.8), xz) / max(_DryTile * 4.3, 1.0);
                float3 farAlbedo = SAMPLE_TEXTURE2D(_AirfieldAlbedo, sampler_AirfieldAlbedo, farUv).rgb;
                float farMix = smoothstep(_FarBlendStart, _FarBlendEnd,
                    distance(input.positionWS, GetCameraPositionWS()));
                edgeAlbedo = lerp(edgeAlbedo, (edgeAlbedo + farAlbedo) * 0.5, farMix) * _AirfieldTint.rgb;
                float macro = MacroNoise(xz) - 0.5;
                edgeAlbedo *= 1.0 + macro * 2.0 * _MacroStrength;
                edgeAlbedo.r *= 1.0 + macro * 0.5 * _MacroStrength;

                // The source image has already been rotated into Airside's runway-local x/z
                // frame. It replaces the coarse map palette at overview distance while the
                // detailed dry-grass material continues smoothly past the airfield edge.
                float2 satelliteUv = saturate(xz / (2.0 * max(_SatelliteExtent, 1.0)) + 0.5);
                float3 satellite = SAMPLE_TEXTURE2D(_SatelliteAlbedo, sampler_SatelliteAlbedo, satelliteUv).rgb
                    * _SatelliteTint.rgb;
                // Sea keeps the purpose-built water shading; the satellite composite is used
                // for land and the real beach only. This also avoids offshore source-tile gaps.
                float satelliteBlend = _SatelliteStrength * (1.0 - saturate(input.color.a));
                float3 broadAlbedo = lerp(input.color.rgb, satellite, satelliteBlend);
                float grassAtJoin = 1.0 - _SatelliteStrength;
                float3 albedo = lerp(broadAlbedo, edgeAlbedo, edgeBlend * grassAtJoin);
                float3 color = albedo * (mainLight.color * (mainLight.shadowAttenuation * NdotL) + SampleSH(normalWS));

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
