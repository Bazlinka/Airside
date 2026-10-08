Shader "Airside/WeatherVolume"
{
    Properties
    {
        _BaseColor ("Weather tint and visibility", Color) = (1,1,1,1)
        _Seed ("Cloud shape seed", Float) = 0
        _Storm ("Thunderstorm development", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-5" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Seed;
                float _Storm;
            CBUFFER_END
            float _AirsideWeatherTime;
            float4 _AirsideLightningPosition;
            float _AirsideLightningFlash;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            float hash(float3 p)
            {
                p=frac(p*0.1031); p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),
                                 lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),
                                 lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            float cloudDensity(float3 p)
            {
                // A rounded bank with broken lobes, eroded by true 3D noise, never a billboard.
                float body=1-length((p-float3(0,-0.10,0))*float3(2.5,4.4,2.7));
                float crown=1-length((p-float3(-0.14,0.07,-0.04))*float3(4.2,3.3,4.0));
                float shoulder=1-length((p-float3(0.18,0.03,0.04))*float3(4.6,4.1,3.8));
                float fairShape=max(body,max(crown,shoulder));
                float tower=1-length((p-float3(0,-0.06,0))*float3(3.4,2.4,3.5));
                float anvil=1-length((p-float3(0.08,0.32,0))*float3(2.1,6.5,2.2));
                float shape=lerp(fairShape,max(tower,anvil),_Storm);
                if (shape < -0.4) return 0;
                float3 drift=float3(_AirsideWeatherTime*0.013,0,_AirsideWeatherTime*0.008);
                float n=noise(p*7+_Seed+drift)*0.7+noise(p*17+_Seed*3+drift)*0.3;
                float lobes=0.08*sin(p.x*23+_Seed)*sin(p.z*19+_Seed);
                return saturate((shape+lobes+(n-0.52)*0.32)*4);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 originWS=GetCameraPositionWS();
                float3 directionWS=normalize(i.positionWS-originWS);
                float3 origin=TransformWorldToObject(originWS);
                float3 direction=mul((float3x3)unity_WorldToObject,directionWS);
                // Slab intersection works below, above and inside a volume. Distances stay in metres.
                float3 inv=rcp(direction+float3(1e-8,1e-8,1e-8));
                float3 a=(-0.5-origin)*inv, b=(0.5-origin)*inv;
                float3 nearPlane=min(a,b), farPlane=max(a,b);
                float start=max(0,max(nearPlane.x,max(nearPlane.y,nearPlane.z)));
                float end=min(farPlane.x,min(farPlane.y,farPlane.z));
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 sceneWS=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
                end=min(end,distance(originWS,sceneWS));
                clip(end-start-0.001);
                const int steps=16;
                float stepLength=(end-start)/steps;
                float transmittance=1;
                float3 colour=0;
                Light sun=GetMainLight();
                float3 lightOS=normalize(mul((float3x3)unity_WorldToObject,sun.direction));
                float jitter=lerp(0.35,0.65,hash(float3(floor(i.positionCS.xy),_Seed)));
                [loop] for(int s=0;s<steps;s++)
                {
                    float t=start+(s+jitter)*stepLength;
                    float3 p=origin+direction*t;
                    float density;
                    float3 sampleColour=_BaseColor.rgb;
                    density=cloudDensity(p);
                    // Shade the lower body and the side hidden from the sun, keeping silver tops.
                    float shade=exp(-cloudDensity(p+lightOS*0.09)*1.7);
                    float topLight=saturate((p.y-0.05)*3)*_Storm;
                    sampleColour=lerp(sampleColour, min(float3(0.95,0.97,1.0),sampleColour*1.8+0.15),topLight);
                    sampleColour*=lerp(0.48,1.10,saturate(shade*0.65+(p.y+0.5)*0.55));
                    float3 sampleWS=originWS+directionWS*t;
                    float glow=exp(-distance(sampleWS,_AirsideLightningPosition.xyz)/2200)*_AirsideLightningFlash;
                    sampleColour+=float3(0.6,0.68,0.85)*glow;
                    density*=length(direction)*lerp(13,24,_Storm);
                    float opacity=1-exp(-density*stepLength);
                    colour+=transmittance*opacity*sampleColour;
                    transmittance*=1-opacity;
                    if(transmittance<0.015) break;
                }
                float alpha=1-transmittance;
                clip(alpha-0.002);
                return half4(colour/max(alpha,0.001),alpha*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
