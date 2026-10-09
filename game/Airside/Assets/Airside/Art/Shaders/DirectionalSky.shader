Shader "Airside/DirectionalSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.1,0.2,0.4,1)
        _Horizon ("Horizon", Color) = (0.5,0.6,0.7,1)
        _Sunset ("Sun-facing horizon", Color) = (1,0.5,0.2,1)
        _TwilightRose ("Opposing twilight band", Color) = (0.58,0.36,0.43,1)
        _Twilight ("Twilight", Range(0,1)) = 0
        _SunDirection ("Sun direction", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Zenith, _Horizon, _Sunset, _TwilightRose, _SunDirection;
                float _Twilight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float height = max(0.0, ray.y);
                float gradient = 1.0 - exp(-height * 3.5);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, gradient);
                // Horizontal dot avoids a discontinuity as the sun crosses the horizon.
                float2 sun = _SunDirection.xz / max(length(_SunDirection.xz), 0.0001);
                float2 viewAzimuth = ray.xz / max(length(ray.xz), 0.0001);
                float towardSun = dot(viewAzimuth, sun);
                float facing = smoothstep(-0.2, 0.95, towardSun);
                // Keep amber below the upper sky. A narrow opposing rose band sits
                // above the cooler horizon, suggesting the rising/falling earth shadow.
                float lowBand = exp(-height * 14.0);
                float awayFromSun = smoothstep(0.05, 0.9, -towardSun);
                float roseBand = smoothstep(0.0, 0.04, height)
                    * (1.0 - smoothstep(0.08, 0.24, height));
                sky = lerp(sky, _TwilightRose.rgb, _Twilight * awayFromSun * roseBand * 0.32);
                sky = lerp(sky, _Sunset.rgb, _Twilight * facing * lowBand * 0.88);
                // Broad soft scattering, separate from the existing physical sun disc.
                float aureole = pow(saturate(dot(ray, normalize(_SunDirection.xyz))), 24.0);
                sky += _Sunset.rgb * (_Twilight * aureole * 0.08);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
