#ifndef AIRSIDE_WORLD_SURFACE_LIGHTING_INCLUDED
#define AIRSIDE_WORLD_SURFACE_LIGHTING_INCLUDED
// Ground, draped terrain, roads and pavement share URP's diffuse energy,
// shadows, sky probes, local lights and screen-space occlusion.
half3 AirsideWorldLighting(float3 albedo, float3 normalWS, float3 positionWS,
    float4 positionCS, float smoothness, float occlusion)
{
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.metallic = 0;
    surface.smoothness = smoothness;
    surface.normalTS = float3(0, 0, 1);
    surface.occlusion = occlusion;
    surface.alpha = 1;
    InputData data = (InputData)0;
    data.positionWS = positionWS;
    data.normalWS = normalize(normalWS);
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    data.shadowCoord = TransformWorldToShadowCoord(positionWS);
    data.bakedGI = SampleSH(data.normalWS);
    data.vertexLighting = VertexLighting(positionWS, data.normalWS);
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    data.shadowMask = half4(1, 1, 1, 1);
    return UniversalFragmentPBR(data, surface).rgb;
}
// Existing mesh generators deliberately store linear vertex palettes.
// The project renders in Linear. Keep the Gamma conversion for comparison/fallback builds;
// sRGB colour textures and Color material properties are decoded by Unity.
float3 AirsideWorldVertexColour(float3 linearColour)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return LinearToSRGB(max(linearColour, 0));
#else
    return linearColour;
#endif
}
#endif
