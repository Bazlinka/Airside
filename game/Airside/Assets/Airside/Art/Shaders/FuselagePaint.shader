Shader "Airside/FuselagePaint"
{
    // Painted fuselage titles and registrations (ADR 0112). The glyph atlas is a TextMesh
    // font texture whose coverage lives in alpha; the paint is opaque, alpha-tested,
    // depth-tested and depth-writing, and lit by the same sun and sky as the airframe, so
    // a wing, engine or building in front of the title hides it like real paint. It is a
    // project shader on GraphicsSettings' Always Included list, so a packaged build never
    // falls back to the font shader's always-on-top path.
    Properties
    {
        _BaseMap ("Glyph atlas (alpha)", 2D) = "white" {}
        _BaseColor ("Paint colour", Color) = (1, 1, 1, 1)
        _Cutoff ("Glyph edge", Range(0.05, 0.95)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        // The paint lies in the label's local XY plane and faces local -Z (TextMesh's
        // readable side), so its normal is known without mesh normals.
        float3 PaintNormalWS()
        {
            return normalize(TransformObjectToWorldDir(float3(0.0, 0.0, -1.0)));
        }

        void ClipGlyph(float2 uv)
        {
            half coverage = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            clip(coverage - _Cutoff);
        }
        ENDHLSL

        Pass
        {
            Name "FuselagePaint"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Off
            // A constant nudge toward the camera for the 2.5 cm the paint stands off the
            // skin; no slope term, so it can never pull a title through a nearby wing.
            Offset 0, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.normalWS = PaintNormalWS();
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ClipGlyph(input.uv);
                float3 normalWS = normalize(input.normalWS);
                Light sun = GetMainLight();
                half diffuse = saturate(dot(normalWS, sun.direction));
                half3 light = sun.color * diffuse + SampleSH(normalWS);
                half3 colour = _BaseColor.rgb * light;
                return half4(MixFog(colour, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            Offset 0, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                ClipGlyph(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off
            Offset 0, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.normalWS = PaintNormalWS();
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ClipGlyph(input.uv);
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
