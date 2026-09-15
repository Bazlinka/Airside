Shader "Airside/AdelaideGround"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.92, 0.94, 0.88, 1)
        _DryAlbedo ("Dry Grass", 2D) = "white" {}
        _GreenAlbedo ("Green Grass", 2D) = "white" {}
        _DirtAlbedo ("Worn Dirt", 2D) = "white" {}
        _DryNormal ("Dry Normal", 2D) = "bump" {}
        _GreenNormal ("Green Normal", 2D) = "bump" {}
        _DirtNormal ("Dirt Normal", 2D) = "bump" {}
        _DryTile ("Dry Tile Metres", Float) = 47
        _GreenTile ("Green Tile Metres", Float) = 37
        _DirtTile ("Dirt Tile Metres", Float) = 29
        _BumpScale ("Bump Scale", Range(0, 2)) = 0.55
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _MacroScale ("Macro Variation Metres", Float) = 240
        _MacroStrength ("Macro Brightness", Range(0, 0.5)) = 0.14
        _FarBlendStart ("Far Detail Start Metres", Float) = 120
        _FarBlendEnd ("Far Detail End Metres", Float) = 900
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            // High quality: a second, larger, rotated sample of each layer mixes in with
            // distance so the tile grid stops reading from the overview.
            #pragma multi_compile_local _ _GROUND_FAR_DETAIL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DryAlbedo);    SAMPLER(sampler_DryAlbedo);
            TEXTURE2D(_GreenAlbedo);  SAMPLER(sampler_GreenAlbedo);
            TEXTURE2D(_DirtAlbedo);   SAMPLER(sampler_DirtAlbedo);
            TEXTURE2D(_DryNormal);    SAMPLER(sampler_DryNormal);
            TEXTURE2D(_GreenNormal);  SAMPLER(sampler_GreenNormal);
            TEXTURE2D(_DirtNormal);   SAMPLER(sampler_DirtNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _DryTile;
                float _GreenTile;
                float _DirtTile;
                float _BumpScale;
                float _Smoothness;
                float _MacroScale;
                float _MacroStrength;
                float _FarBlendStart;
                float _FarBlendEnd;
            CBUFFER_END

            // Deterministic value noise: no texture, same result every frame and every build.
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

            // Two octaves, 0..1, centred near 0.5.
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.color = input.color;
                output.shadowCoord = GetShadowCoord(pos);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            float3 SampleLayer(TEXTURE2D_PARAM(albedoTex, albedoSamp),
                               TEXTURE2D_PARAM(normalTex, normalSamp),
                               float2 worldXZ, float tileMetres, float3 baseNormal, float farMix, inout float3 albedo)
            {
                float2 uv = worldXZ / max(tileMetres, 1.0);
                float3 color = SAMPLE_TEXTURE2D(albedoTex, albedoSamp, uv).rgb;
            #if defined(_GROUND_FAR_DETAIL)
                // Rotated ~37 degrees and 4.3x larger, so the two grids never line up.
                float2 farUv = mul(float2x2(0.8, -0.6, 0.6, 0.8), worldXZ) / max(tileMetres * 4.3, 1.0);
                float3 farColor = SAMPLE_TEXTURE2D(albedoTex, albedoSamp, farUv).rgb;
                color = lerp(color, (color + farColor) * 0.5, farMix);
            #endif
                // Normals shimmer as tiny noise from far away; ease them flat with distance.
                float3 tangentNormal = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(normalTex, normalSamp, uv), _BumpScale * (1.0 - 0.7 * farMix));
                // Cheap world-XZ bump: remap tangent XY onto XZ while keeping up.
                float3 n = normalize(float3(
                    baseNormal.x + tangentNormal.x,
                    baseNormal.y,
                    baseNormal.z + tangentNormal.y));
                albedo = color;
                return n;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 w = input.color.rgb;
                float sum = max(1e-4, w.r + w.g + w.b);
                w /= sum;

                float2 xz = input.positionWS.xz;
                float farMix = smoothstep(_FarBlendStart, _FarBlendEnd, distance(input.positionWS, GetCameraPositionWS()));
                float3 aDry, aGreen, aDirt;
                float3 nDry = SampleLayer(TEXTURE2D_ARGS(_DryAlbedo, sampler_DryAlbedo),
                    TEXTURE2D_ARGS(_DryNormal, sampler_DryNormal), xz, _DryTile, input.normalWS, farMix, aDry);
                float3 nGreen = SampleLayer(TEXTURE2D_ARGS(_GreenAlbedo, sampler_GreenAlbedo),
                    TEXTURE2D_ARGS(_GreenNormal, sampler_GreenNormal), xz, _GreenTile, input.normalWS, farMix, aGreen);
                float3 nDirt = SampleLayer(TEXTURE2D_ARGS(_DirtAlbedo, sampler_DirtAlbedo),
                    TEXTURE2D_ARGS(_DirtNormal, sampler_DirtNormal), xz, _DirtTile, input.normalWS, farMix, aDirt);

                float3 albedo = (aDry * w.r + aGreen * w.g + aDirt * w.b) * _Tint.rgb;
                // Large soft light/dark patches, slightly warmer where lighter, as sun-dried
                // and irrigated ground varies across a real airfield.
                float macro = MacroNoise(xz) - 0.5;
                albedo *= 1.0 + macro * 2.0 * _MacroStrength;
                albedo.r *= 1.0 + macro * 0.5 * _MacroStrength;
                float3 normalWS = normalize(nDry * w.r + nGreen * w.g + nDirt * w.b);

                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                // Sun plus sky ambient, as URP Lit does. The old "+0.28" was multiplied by the
                // ~2.0 daytime sun and bleached the ground once this shader reached builds.
                float3 lighting = mainLight.color * (mainLight.shadowAttenuation * NdotL) + SampleSH(normalWS);
                float3 color = albedo * lighting;
                // Tiny specular so asphalt-adjacent dirt does not look plastic.
                float3 halfDir = normalize(mainLight.direction + GetWorldSpaceNormalizeViewDir(input.positionWS));
                float spec = pow(saturate(dot(normalWS, halfDir)), lerp(8.0, 48.0, _Smoothness)) * _Smoothness * 0.2;
                color += mainLight.color * spec * mainLight.shadowAttenuation;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
