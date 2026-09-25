// The weather every Odyssey shader reads: how hard it is raining, how wet the ground has got, and
// where the sky reaches. Prototype for the rain look (claude/rain-look); see
// docs/research/d-20-rain-rendering.md and the weather design's §7.
//
// **One map answers every "is this under cover" question on the GPU.** _OdysseySkyTex holds, per
// board column, the height in metres at which rain stops — the top of the highest roof slab,
// solid cell, water surface or tree canopy over it — built on the CPU by SkyHeightMap from the
// render mirror. The streaks die at it, the splashes stand on it and the wetness is masked by it,
// so the three can never disagree about where a roof is. G carries what the rain lands on.
//
// **All-zero is a valid state.** Unset globals mean no rain and a dry world, so everything that
// draws the world without a RainDirector — portraits, contact sheets, the first frame — is
// unchanged.
#ifndef ODYSSEY_WEATHER_INCLUDED
#define ODYSSEY_WEATHER_INCLUDED

// x rain intensity 0..1, y ground wetness 0..1, z rain clock in seconds (tick-driven), w puddles 0..1
float4 _OdysseyRain;
// x = 1 / (board width in metres), y = 1 / (board depth in metres), z = 1 when the map is valid
float4 _OdysseySkyParams;
// x how wet ground is drawn: 0 richer and a little darker, 1 gloss only (owner's choice by eye, 2026-09-25)
float4 _OdysseyRainLook;
TEXTURE2D(_OdysseySkyTex);
SAMPLER(sampler_OdysseySkyPointClamp);

// What the rain lands on, in the sky map's G channel (stored as kind / 4).
#define ODYSSEY_SKY_GROUND 0.0
#define ODYSSEY_SKY_WATER 1.0
#define ODYSSEY_SKY_CANOPY 2.0
#define ODYSSEY_SKY_BUILT 3.0

// x: the height rain stops at over this point, in metres. y: what it lands on (see above).
float2 OdysseySkyAt(float2 xz)
{
    if (_OdysseySkyParams.z < 0.5) return float2(-1e5, ODYSSEY_SKY_GROUND);
    float2 uv = xz * _OdysseySkyParams.xy;
    float4 s = SAMPLE_TEXTURE2D_LOD(_OdysseySkyTex, sampler_OdysseySkyPointClamp, uv, 0);
    return float2(s.r, round(s.g * 4.0));
}

// How wet the surface at this point is, 0..1: the ground's wetness where the sky reaches it,
// nothing where it does not. Faces that stand up take a little; tops take all of it.
//
// A pixel is exposed when it is at or above its column's rain-stop height. Natural ground is
// forgiven up to one layer below its top, because a terrace ramp and the riser under a lip are
// drawn in the column below the top they belong to and are still out in the rain.
float OdysseyWetAt(float3 positionWS, float3 normalWS)
{
    float wet = _OdysseyRain.y;
    if (wet <= 0.001) return 0.0;
    float2 sky = OdysseySkyAt(positionWS.xz);
    float slack = sky.y == ODYSSEY_SKY_GROUND ? 3.0 : 0.15;
    float exposed = step(sky.x - slack, positionWS.y);
    float facing = lerp(0.35, 1.0, saturate(normalWS.y));
    return wet * exposed * facing;
}

// Wet colour, the owner's way (2026-09-25: "we want to be colourful when it rains"): a wet
// surface keeps its colour. The two candidates are photographed against each other, and
// _OdysseyRainLook.x picks one:
//   0, richer: deeper and more saturated, darkened only to 0.8 - water fills the pores, so a
//      wet surface scatters less white and its own colour shows through;
//   1, gloss only: the colour untouched, the wet read from the shine and the puddles alone.
float3 OdysseyWetColour(float3 albedo, float wet)
{
    float glossOnly = _OdysseyRainLook.x;
    float luma = dot(albedo, float3(0.2126, 0.7152, 0.0722));
    float3 richer = max(0.0, luma + (albedo - luma) * 1.3) * 0.8;
    return lerp(albedo, lerp(richer, albedo, glossOnly), wet);
}

// Rain on a porous surface: its colour deepened (above) and glossier. The puddle term floods the
// flattest, lowest-noise patches once the ground is soaked.
void OdysseyWetten(inout float3 albedo, inout float smoothness, float wet, float3 positionWS, float3 normalWS)
{
    if (wet <= 0.001) return;
    albedo = OdysseyWetColour(albedo, wet);
    smoothness = lerp(smoothness, lerp(0.62, 0.72, _OdysseyRainLook.x), wet * saturate(normalWS.y));

    float puddles = _OdysseyRain.w;
    [branch] if (puddles > 0.001 && normalWS.y > 0.92)
    {
        // Cheap value noise at a few metres: puddles gather in patches, not cells.
        float2 p = positionWS.xz / 3.7;
        float2 i = floor(p), f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
        float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
        float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
        float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
        float n = lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        float pool = smoothstep(0.66, 0.74, n) * saturate((wet - 0.45) * 2.0) * puddles;
        albedo = lerp(albedo, albedo * 0.35 + float3(0.02, 0.025, 0.03), pool);
        smoothness = lerp(smoothness, 0.95, pool);
    }
}

#endif
