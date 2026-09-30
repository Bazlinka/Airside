Shader "Airside/HeightFog"
{
    Properties { _BaseColor ("Fog tint and density", Color) = (0.8,0.8,0.8,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-2" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            float4 _AirsideWeatherWind;
            float _AirsideWeatherTime;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o; }
            float hash(float2 p) { p=frac(p*float2(0.1031,0.1030));p+=dot(p,p.yx+33.33);return frac((p.x+p.y)*p.x); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 origin=GetCameraPositionWS();
                float3 surface=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float distanceToSurface=length(surface-origin);
                float3 ray=(surface-origin)/max(distanceToSurface,0.001);
                // Clip only the ray's vertical interval, independent of the proxy mesh's
                // far faces and hardware depth/occlusion rejection. The camera may be inside.
                float invY=rcp(abs(ray.y)<0.00001 ? 0.00001 : ray.y);
                float a=(-6-origin.y)*invY,b=(114-origin.y)*invY;
                float start=max(0,min(a,b));
                float end=min(min(distanceToSurface,18000),max(a,b));
                if(end<=start) return 0;
                const float falloff=4.0/120.0;
                float startHeight=origin.y+ray.y*start+6;
                float endHeight=origin.y+ray.y*end+6;
                // Exact exponential height integral: only the drifting horizontal variation
                // needs sampling. This avoids twelve expensive full-screen density steps.
                float heightIntegral=abs(ray.y)>0.001
                    ? (exp(-startHeight*falloff)-exp(-endHeight*falloff))/(falloff*ray.y)
                    : (end-start)*exp(-startHeight*falloff);
                float variation=0,weights=0;
                [unroll] for(int s=0;s<3;s++)
                {
                    float3 world=origin+ray*lerp(start,end,(s+0.5)/3.0);
                    float weight=exp(-max(0,world.y+6)*falloff);
                    float2 fromField=abs((world.xz-float2(300,150))/float2(8000,7000));
                    float edge=1-smoothstep(0.6,1,max(fromField.x,fromField.y));
                    float n=noise((world.xz-_AirsideWeatherWind.xz*_AirsideWeatherTime*0.13)*0.008);
                    variation+=weight*edge*lerp(0.55,1.4,n);
                    weights+=weight;
                }
                float opticalDepth=max(0,heightIntegral)*variation/max(weights,0.001)*0.006*_BaseColor.a;
                return half4(_BaseColor.rgb,1-exp(-opticalDepth));
            }
            ENDHLSL
        }
    }
}
