Shader "Airside/SurfaceStain"
{
    Properties { _BaseColor("Stain colour / strength",Color)=(.42,.37,.31,.4) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END
            struct A {float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float fog:TEXCOORD1;};
            V vert(A i) {V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float radius=length(p);
                float ragged=.045*sin(p.x*19+p.y*11)+.025*sin(p.y*31-p.x*13);
                float feather=1-smoothstep(.45,.96,radius+ragged);
                float density=.72+.16*sin(p.x*23)*sin(p.y*29);
                float strength=feather*density*_BaseColor.a;
                // Multiply the lit pavement: grain, joints and wetness remain visible.
                float3 multiplier=lerp(1,_BaseColor.rgb,strength);
                multiplier=lerp(1,multiplier,ComputeFogIntensity(i.fog));
                return half4(multiplier,1);
            }
            ENDHLSL
        }
    }
}
