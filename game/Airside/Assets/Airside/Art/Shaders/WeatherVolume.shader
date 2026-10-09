Shader "Airside/WeatherVolume"
{
    Properties
    {
        _BaseColor ("Weather tint and visibility", Color) = (1,1,1,1)
        _Seed ("Cloud shape seed", Float) = 0
        _Storm ("Thunderstorm development", Range(0,1)) = 0
        _Stratus ("Stratiform bank", Range(0,1)) = 0
        _Cirrus ("High ice wisps", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-5" }
        Blend One OneMinusSrcAlpha
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
                float _Stratus;
                float _Cirrus;
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
                // This cube is a ray-march proxy. Its exit face can lie beyond the
                // far plane while the cloud in front remains visible. Retain the proxy
                // face there; scene depth still limits the integrated cloud in metres.
                #if UNITY_REVERSED_Z
                    o.positionCS.z=max(o.positionCS.z,0.00001*o.positionCS.w);
                #else
                    o.positionCS.z=min(o.positionCS.z,0.99999*o.positionCS.w);
                #endif
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
            // fine=false is the cheap form used for the sun-shadow probes: one noise octave and no
            // lobe/streak detail. Shadows only need the broad lobes; the view ray keeps the full detail.
            float cloudDensityLod(float3 p, bool fine)
            {
                // A rounded bank with broken lobes, eroded by true 3D noise, never a billboard.
                float body=1-length((p-float3(0,-0.10,0))*float3(2.5,4.4,2.7));
                float crown=1-length((p-float3(-0.14,0.07,-0.04))*float3(4.2,3.3,4.0));
                float shoulder=1-length((p-float3(0.18,0.03,0.04))*float3(4.6,4.1,3.8));
                float fairShape=max(body,max(crown,shoulder));
                float bank=1-length((p-float3(0,-0.04,0))*float3(2.2,3.5,2.3));
                float wisp=1-length(p*float3(2.1,4.5,2.5));
                fairShape=lerp(fairShape,bank,_Stratus);
                fairShape=lerp(fairShape,wisp,_Cirrus);
                float tower=1-length((p-float3(0,-0.06,0))*float3(3.4,2.4,3.5));
                float anvil=1-length((p-float3(0.08,0.32,0))*float3(2.1,6.5,2.2));
                float shape=lerp(fairShape,max(tower,anvil),_Storm);
                if (shape < -0.4) return 0;
                float3 drift=float3(_AirsideWeatherTime*0.013,0,_AirsideWeatherTime*0.008);
                float n; float lobes=0;
                if (fine)
                {
                    n=noise(p*7+_Seed+drift)*0.7+noise(p*17+_Seed*3+drift)*0.3;
                    lobes=0.08*sin(p.x*23+_Seed)*sin(p.z*19+_Seed);
                }
                else
                    n=noise(p*7+_Seed+drift)*0.7+0.15;
                float density=saturate((shape+lobes+(n-0.52)*0.32)*4);
                if (fine && _Cirrus > 0.001)
                {
                    float streaks=noise(float3(p.x*3,p.y*18,p.z*32)+_Seed+drift);
                    density*=lerp(1,saturate((streaks-0.30)*2)*0.35,_Cirrus);
                }
                // Ease the eroded silhouette without hardening thin ice wisps. The same
                // density is used for view absorption and sun shadowing.
                return density*smoothstep(0,0.12,density);
            }
            float cloudDensity(float3 p) { return cloudDensityLod(p,true); }
            float cloudShadowDensity(float3 p) { return cloudDensityLod(p,false); }
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
                // Short chords (grazing edges, thin banks) need fewer samples than a full crossing.
                float chord=end-start;
                int steps=(int)clamp(ceil(chord/180),6,16);
                float stepLength=chord/steps;
                float transmittance=1;
                float3 colour=0;
                Light sun=GetMainLight();
                float3 lightOS=mul((float3x3)unity_WorldToObject,sun.direction);
                lightOS*=rsqrt(max(dot(lightOS,lightOS),0.000001));
                float sunLuminance=dot(sun.color,float3(0.2126,0.7152,0.0722));
                float sunStrength=saturate(sunLuminance);
                // Preserve the existing weather/day tint, borrowing only a restrained
                // amount of the directional light's hue (warm dusk, cool night).
                float3 sunTint=lerp(float3(1,1,1),
                    clamp(sun.color/max(sunLuminance,0.001),0.55,1.6),0.35);
                float skyLuminance=dot(max(SampleSH(float3(0,1,0)),0),
                    float3(0.2126,0.7152,0.0722));
                float ambient=lerp(0.20,0.48,saturate(skyLuminance));
                // Forward scattering brightens thin cloud when looking toward the
                // light. Bound the phase response so it cannot become a white bloom slab.
                float cosine=dot(directionWS,sun.direction);
                const float anisotropy=0.55;
                float phase=(1-anisotropy*anisotropy)/
                    pow(max(1+anisotropy*anisotropy-2*anisotropy*cosine,0.05),1.5);
                float silverLining=min(phase*0.18,0.65)*sunStrength;
                // Reveal/wrap fades change optical depth, rather than making an opaque
                // body into a translucent flat card after integration.
                float extinction=length(direction)*lerp(13,24,_Storm)*saturate(_BaseColor.a);
                float jitter=lerp(0.35,0.65,hash(float3(floor(i.positionCS.xy),_Seed)));
                [loop] for(int s=0;s<steps;s++)
                {
                    float t=start+(s+jitter)*stepLength;
                    float3 p=origin+direction*t;
                    float density=cloudDensity(p);
                    if(density<0.001) continue;
                    // Two fixed probes along the sun ray give both local lobe relief
                    // and broader body shadowing; no nested light-marching loop.
                    float sunDepth=cloudShadowDensity(p+lightOS*0.09)*1.2;
                    // A sample already deep in shadow gains nothing from the second, farther probe.
                    if(sunDepth<2.5) sunDepth+=cloudShadowDensity(p+lightOS*0.23)*2.2;
                    sunDepth*=lerp(1,1.35,_Storm);
                    float sunTransmission=exp(-sunDepth);
                    float heightFill=lerp(0.65,1.0,saturate(p.y+0.5));
                    float skyFill=ambient*heightFill*lerp(1,0.72,_Storm);
                    float edgeLight=silverLining*(1-density)*sunTransmission;
                    float directLight=sunStrength*sunTransmission*0.85+edgeLight;
                    float3 sampleColour=_BaseColor.rgb*
                        (skyFill+sunTint*directLight);
                    if(_AirsideLightningFlash>0.001)
                    {
                        float3 sampleWS=originWS+directionWS*t;
                        float glow=exp(-distance(sampleWS,_AirsideLightningPosition.xyz)/2200)*_AirsideLightningFlash;
                        sampleColour+=float3(0.6,0.68,0.85)*glow;
                    }
                    density*=extinction;
                    float opacity=1-exp(-density*stepLength);
                    colour+=transmittance*opacity*sampleColour;
                    transmittance*=1-opacity;
                    if(transmittance<0.015) break;
                }
                float alpha=1-transmittance;
                clip(alpha-0.002);
                // Integration already produces premultiplied radiance. Composite it
                // directly without dividing by near-zero edge opacity.
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
