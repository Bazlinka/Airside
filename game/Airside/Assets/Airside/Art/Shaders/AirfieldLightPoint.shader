Shader "Airside/AirfieldLightPoint"
{
    // ADR 0162: an airfield lamp as a point source. Every fixture is four vertices at the lens
    // centre; the vertex shader opens them into a camera-facing quad that never shrinks below
    // _MinPixels on screen, dims softly with distance (never below a floor) and reaches through
    // haze further than surfaces do. Guard lights alternate their pair (uv1.y = flash phase).
    // Constants mirror AirfieldFixture's PointDistanceFactor, PointHaze and FlashOn.
    Properties
    {
        [HDR] _BaseColor ("Light colour x strength", Color) = (1, 1, 1, 1)
        _MinPixels ("Smallest diameter in pixels", Float) = 2.5
        _HalfMetres ("Distance at half brightness", Float) = 2200
        _DistanceFloor ("Dimmest with distance", Float) = 0.3
        _HazeExponent ("Haze transmission exponent", Float) = 0.35
        _FlashHz ("Guard flash rate", Float) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "AirfieldLightPoint"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _MinPixels;
                float _HalfMetres;
                float _DistanceFloor;
                float _HazeExponent;
                float _FlashHz;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0; // -1..1
                float2 sizePhase : TEXCOORD1; // world diameter, flash phase (-1 steady)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                half brightness : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 centreWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 centreVS = TransformWorldToView(centreWS);
                float dist = max(0.05, -centreVS.z);

                // Metres per pixel at this depth (perspective; orthographic falls back to the world size).
                float metresPerPixel = 2.0 * dist / max(1.0, _ScreenParams.y * abs(UNITY_MATRIX_P._m11));
                float size = max(input.sizePhase.x, _MinPixels * metresPerPixel);

                // Pull the quad towards the camera by its own size so the ground does not clip its
                // lower half at a grazing angle, then rescale so the on-screen size is unchanged.
                float pull = min(size, dist * 0.5);
                float scale = (dist - pull) / dist;
                float3 posVS = centreVS * scale;
                posVS.xy += input.corner * (size * 0.5 * scale);
                output.positionCS = TransformWViewToHClip(posVS);
                output.corner = input.corner;

                float q = dist / _HalfMetres;
                half distanceFactor = max(_DistanceFloor, 1.0 / (1.0 + q * q));
                // Scene fog is exponential squared (ADR 0143): surface transmission exp2(-(x·d)²).
                float f = unity_FogParams.x * dist;
                half haze = pow(saturate(exp2(-f * f)), _HazeExponent);
                half flash = 1.0;
                if (input.sizePhase.y >= 0.0)
                    flash = frac(_Time.y * _FlashHz + input.sizePhase.y) < 0.5 ? 1.0 : 0.0;
                output.brightness = distanceFactor * haze * flash;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half r2 = dot(input.corner, input.corner);
                clip(1.0 - r2);
                // Hot core and a short soft skirt: a lamp, not a blob.
                half core = exp(-r2 * 9.0);
                half skirt = saturate(1.0 - r2) * 0.22;
                half v = (core + skirt) * input.brightness;
                return half4(_BaseColor.rgb * v, 0);
            }
            ENDHLSL
        }
    }
}
