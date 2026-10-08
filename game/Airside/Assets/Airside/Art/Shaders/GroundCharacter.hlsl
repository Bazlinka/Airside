#ifndef AIRSIDE_GROUND_CHARACTER_INCLUDED
#define AIRSIDE_GROUND_CHARACTER_INCLUDED

// Original, metre-scaled surface character. Call with absolute runway-local XZ
// (positionWS + flight origin), never camera coordinates or animation time.
float AirsideGroundHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float AirsideGroundNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f*f*(3.0-2.0*f);
    return lerp(lerp(AirsideGroundHash(i), AirsideGroundHash(i+float2(1,0)), f.x),
                lerp(AirsideGroundHash(i+float2(0,1)), AirsideGroundHash(i+1), f.x), f.y);
}

float3 AirsideNaturalGround(float3 albedo, float2 xz, float cameraDistance, float strength)
{
    // Irregular dry/matted islands at 7–30 m scale survive the overview. The
    // smaller texture grain fades before it can shimmer; no extra texture taps.
    float broad = AirsideGroundNoise(xz/29.0 + 61.3);
    float patch = AirsideGroundNoise(xz/7.3 + broad*2.1);
    float bare = smoothstep(.56,.82,patch) * smoothstep(.35,.70,broad);
    float damp = smoothstep(.60,.85,AirsideGroundNoise(xz/17.0 + 113.7));
    float fine = (AirsideGroundNoise(xz/1.7)-.5)
        * (1.0-smoothstep(35.0,120.0,cameraDistance));
    float3 character = float3(1.0,1.0,1.0)
        + bare*float3(.16,.035,-.11) - damp*float3(.11,.06,.10) + fine*.13;
    return albedo * lerp(float3(1.0,1.0,1.0), character, saturate(strength));
}

// Returns R=repair mask, G=tar/joint mask, B=local surface-age variation.
float3 AirsidePavementCharacter(float2 xz, float cameraDistance, float concrete)
{
    float2 cell = floor(xz/float2(9.7,14.3));
    float2 f = frac(xz/float2(9.7,14.3));
    float age = AirsideGroundHash(cell+37.1);
    // Isolated repairs occupy part of selected cells, never a checkerboard.
    float2 inset = float2(.12,.16)+AirsideGroundHash(cell+71.7)*.13;
    float2 edge = min(f,1.0-f)-inset;
    float repairEdge = min(edge.x,edge.y);
    float repair = smoothstep(0.0,max(.025,fwidth(repairEdge)),repairEdge) * step(.83,age);
    float fineFade = 1.0-smoothstep(65.0,230.0,cameraDistance);
    // Occasional narrow sealed cracks meander within their own cell. Filter at
    // pixel scale and fade out before overview aliasing creates dark stripes.
    float wander = .48 + .13*sin(f.y*9.0+age*17.0) + .035*sin(f.y*31.0);
    float crackDistance = abs(f.x-wander)*9.7;
    float aa = max(fwidth(crackDistance),.014);
    float sealed = (1.0-smoothstep(.035,.035+aa,crackDistance))
        * smoothstep(.08,.22,f.y)*(1.0-smoothstep(.73,.92,f.y))*step(.72,age);
    // Offset concrete slab rows and slightly different slab ages. These are
    // visual joints only; the physical airport surface remains level.
    float row = floor(xz.y/5.8);
    float2 slabXZ = float2(xz.x+fmod(abs(row),2.0)*2.6,xz.y);
    float2 slab = frac(slabXZ/float2(5.2,5.8));
    float jointDistance = min(min(slab.x,1.0-slab.x)*5.2,min(slab.y,1.0-slab.y)*5.8);
    float jointAA = max(fwidth(jointDistance),.012);
    float joint = 1.0-smoothstep(.018,.018+jointAA,jointDistance);
    float slabAge = AirsideGroundHash(floor(slabXZ/float2(5.2,5.8))+19.3)-.5;
    float surfaceAge = lerp((age-.5)*repair,slabAge*.45,concrete);
    return float3(repair,lerp(sealed,joint*.60+sealed*.25,concrete)*fineFade,surfaceAge);
}
#endif
