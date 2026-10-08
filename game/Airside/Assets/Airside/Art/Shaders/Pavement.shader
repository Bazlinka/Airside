Shader "Airside/Pavement"
{
    Properties
    {
        [MainColor] _BaseColor ("Colour", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Scanned albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Scanned normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = .55
        _MetallicGlossMap ("Metallic / smoothness", 2D) = "white" {}
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion strength", Range(0,1)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = .15
        _ScanMean ("Scan neutral colour", Color) = (.62,.63,.63,1)
        _ScanContrast ("Scan contrast", Range(0,1)) = .3
        _SurfaceWetness ("Wetness", Range(0,1)) = 0
        _TileMetres ("Scan size metres", Float) = 3
        _MacroStrength ("Large surface variation", Range(0,.2)) = .045
        _PatchStrength ("Resurfacing variation", Range(0,.2)) = .025
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _ScanMean;
                float _BumpScale, _Metallic, _Smoothness, _OcclusionStrength;
                float _TileMetres, _MacroStrength, _PatchStrength, _ScanContrast, _SurfaceWetness;
            CBUFFER_END
            float4 _AirsideFlightOrigin;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1; float fog:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output);
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS; output.positionWS=p.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(p.positionCS.z); return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 xz=input.positionWS.xz+_AirsideFlightOrigin.xz;
                float2 uv=xz/max(_TileMetres,.1);
                // Two incommensurate scales hide scan repetition; fine relief eases away
                // at overview while painted geometry and separately placed wear remain crisp.
                float far=smoothstep(35,180,distance(GetCameraPositionWS(),input.positionWS));
                float2 other=mul(float2x2(.8,-.6,.6,.8),uv)/3.71+float2(.37,.19);
                float3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                float3 second=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,other).rgb;
                albedo=lerp(albedo,(albedo+second)*.5,far*.7);
                albedo=lerp(_ScanMean.rgb,albedo,_ScanContrast)*_BaseColor.rgb;
                float macro=(Noise(xz/43)-.5)*2*_MacroStrength;
                float patch=(Noise(xz/11.3+7.1)-.5)*2*_PatchStrength;
                albedo*=1+macro+patch;
                float3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale*(1-far*.8));
                float3 n=normalize(input.normalWS+float3(detail.x,0,detail.y));
                float smooth=lerp(SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,uv).a,1,_SurfaceWetness)*_Smoothness;
                float ao=lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).g,_OcclusionStrength);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo; surface.metallic=0; surface.smoothness=smooth;
                surface.normalTS=float3(0,0,1); surface.occlusion=ao; surface.alpha=1;
                InputData data=(InputData)0;
                data.positionWS=input.positionWS; data.normalWS=n;
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord=input.fog; data.bakedGI=SampleSH(n);
                data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(data,surface);
                color.rgb=MixFog(color.rgb,input.fog); return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(A i):SV_POSITION { UNITY_SETUP_INSTANCE_ID(i); return TransformObjectToHClip(i.positionOS.xyz); }
            half4 frag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V vert(A i) { UNITY_SETUP_INSTANCE_ID(i); V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
