Shader "Airside/WeatherCeiling"
{
    Properties { _BaseColor ("Cloud ceiling tint", Color) = (0.5,0.55,0.6,0.8) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            float4 _AirsideWeatherWind;
            float _AirsideWeatherTime;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; };
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            Varyings vert(Attributes i)
            {
                Varyings o; o.world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world); o.uv=i.uv;return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 p=(i.world.xz-_AirsideWeatherWind.xz*_AirsideWeatherTime)*0.0008;
                float n=noise(p)*0.55+noise(p*2.13)*0.3+noise(p*4.7)*0.15;
                float edge=smoothstep(0,0.15,min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y)));
                return half4(_BaseColor.rgb*lerp(0.65,1.15,n),_BaseColor.a*lerp(0.65,1,n)*edge);
            }
            ENDHLSL
        }
    }
}
