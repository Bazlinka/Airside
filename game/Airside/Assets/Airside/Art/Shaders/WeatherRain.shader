Shader "Airside/WeatherRain"
{
    Properties { _BaseColor ("Rain tint", Color) = (0.77,0.83,0.88,0.32) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float eyeDepth : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o; float3 world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world); o.uv=i.uv;
                o.eyeDepth=-TransformWorldToView(world).z; return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float depth=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)),_ZBufferParams);
                float edge=pow(saturate(1-abs(i.uv.x*2-1)),2);
                float tail=sin(i.uv.y*PI);
                return half4(_BaseColor.rgb,_BaseColor.a*edge*tail*saturate((depth-i.eyeDepth)*0.7));
            }
            ENDHLSL
        }
    }
}
