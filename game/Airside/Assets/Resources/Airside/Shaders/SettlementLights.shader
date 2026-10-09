Shader "Airside/SettlementLights"
{
    Properties
    {
        _HorizonFadeStart ("Horizon fade start", Float) = 6500
        _HorizonFadeEnd ("Horizon fade end", Float) = 9600
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float _AirsideSettlementNight;
            float _AirsideHorizonScale;
            CBUFFER_START(UnityPerMaterial)
                float _HorizonFadeStart;
                float _HorizonFadeEnd;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 mode:NORMAL; float2 uv:TEXCOORD0; float4 colour:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 colour:TEXCOORD1; float mode:TEXCOORD2; float fog:TEXCOORD3; float distanceWS:TEXCOORD4; };
            V vert(A a)
            {
                V o;float3 p=TransformObjectToWorld(a.positionOS.xyz);
                if(a.mode.y==1)
                {
                    float distanceWS=length(p-GetCameraPositionWS());
                    // A small, capped luminous source; roads never become aircraft-sized flares.
                    float radius=min(a.mode.z,max(a.mode.x,distanceWS/max(_ScreenParams.y,1)*.65));
                    p+=UNITY_MATRIX_I_V._m00_m10_m20*a.uv.x*radius
                      +UNITY_MATRIX_I_V._m01_m11_m21*a.uv.y*radius;
                }
                o.positionCS=TransformWorldToHClip(p);o.uv=a.uv;o.colour=a.colour.rgb;
                o.mode=a.mode.y;o.fog=ComputeFogFactor(o.positionCS.z);
                o.distanceWS=length(p-GetCameraPositionWS());return o;
            }
            half4 frag(V i):SV_Target
            {
                float radial=saturate(1-dot(i.uv,i.uv));
                float weight=i.mode<-.5 ? 1 : radial*radial;
                float strength=_AirsideSettlementNight*weight
                    *(1-smoothstep(_HorizonFadeStart,_HorizonFadeEnd,i.distanceWS/max(1,_AirsideHorizonScale)));
                if(i.mode>1.5) strength=.3; // Restrained pole silhouette; no emissive metal.
                float3 colour=i.colour*strength;
                colour=MixFogColor(colour,half3(0,0,0),i.fog);
                return half4(colour,i.mode>1.5 ? 1 : 0);
            }
            ENDHLSL
        }
    }
}
