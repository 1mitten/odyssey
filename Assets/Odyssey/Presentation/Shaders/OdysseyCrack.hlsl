// The crack pattern (design 57 §4), shared by the crack overlay (Odyssey/Crack) and the pieces a
// broken wall or rock face falls apart into (Odyssey/Shard), so a piece carries the cracks the
// wall had the instant before it came apart.
//
// **Cracks burst from a point, they do not tile.** The first look was the borders of a Voronoi
// tiling, and the owner read the heavier stages as "shapes" (2026-09-26): a tiling closes every
// line into a cell, and a surface covered in closed cells looks like paving. What a blow leaves
// is a few long cracks running out from where it landed, each tapering to nothing, wandering as
// it goes, with finer cracks forking off them. So each cell gets an impact point of its own —
// hashed from the cell, so it never moves — and the pattern is:
//
// - **Rays** from that point, a handful, each with its own length, width and wander, wide at the
//   root and tapering to a hair at the tip. More of them, and longer, as it gets worse.
// - **Branches**: the level set of a smooth noise, which runs as meandering open lines, shown
//   only near a ray until the late levels, when they web the whole face.
// - **A crushed patch** at the point itself and a **grime** over the whole surface at the late
//   levels, which is what still reads when the camera is too far off for a line to.
//
// Everything is a function of world position, so it never swims, and one severity in 0..1 drives
// all of it: a higher level grows the same cracks further rather than drawing new ones.
//
// Antialiased by the pixel's footprint in metres, measured once per fragment (fwidth of the world
// position, outside any branch), and a line thinner than a pixel is faded rather than let shimmer.
#ifndef ODYSSEY_CRACK_INCLUDED
#define ODYSSEY_CRACK_INCLUDED

#define ODYSSEY_CRACK_TWO_PI 6.28318530718
#define ODYSSEY_CRACK_RAYS 7

float OdysseyCrackHash1(float2 p)
{
    return frac(sin(dot(p, float2(41.3, 289.1))) * 43758.5453);
}

float3 OdysseyCrackHash3(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453);
}

// Smooth value noise, 0 to 1.
float OdysseyCrackNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = OdysseyCrackHash1(i);
    float b = OdysseyCrackHash1(i + float2(1, 0));
    float c = OdysseyCrackHash1(i + float2(0, 1));
    float d = OdysseyCrackHash1(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Two octaves: enough wander for a crack, cheap enough to take three times for a gradient.
float OdysseyCrackFbm(float2 p)
{
    return 0.65 * OdysseyCrackNoise(p) + 0.35 * OdysseyCrackNoise(p * 2.7 + 11.3);
}

// How much of a line of half-width `w` metres is at distance `d` metres, with `px` metres a pixel.
float OdysseyCrackLine(float d, float w, float px)
{
    float stroke = 1.0 - smoothstep(w - px, w + px, d);
    return stroke * saturate(w / px * 1.5);
}

// The cracks in one projection plane. `p` is the point, `o` the impact point, both in metres in
// the plane; `seed` keeps the three planes from repeating one another.
float OdysseyCrackPlane(float2 p, float2 o, float seed, float severity, float px, out float nearRoot)
{
    float2 q = p - o;
    float r = length(q);
    float a = atan2(q.y, q.x);

    // How far the damage has spread from the point, in metres: a fist-sized star at the first
    // level, the whole face at the last.
    float reach = lerp(0.45, 2.8, severity);
    float width = lerp(0.006, 0.026, severity);
    float shown = lerp(0.35, 1.0, severity);      // share of the rays drawn at all

    float s = (a / ODYSSEY_CRACK_TWO_PI + 0.5) * ODYSSEY_CRACK_RAYS;
    float id = floor(s);
    float rays = 0.0;
    float rayDistance = 8.0;
    [unroll] for (int k = -1; k <= 1; k++)
    {
        float ray = id + k;
        float slot = fmod(ray + ODYSSEY_CRACK_RAYS, ODYSSEY_CRACK_RAYS) + seed * 17.0;
        if (OdysseyCrackHash1(float2(slot, 29.0)) > shown) continue;

        float along = ray + 0.15 + 0.7 * OdysseyCrackHash1(float2(slot, 3.0));
        float da = (s - along) / ODYSSEY_CRACK_RAYS * ODYSSEY_CRACK_TWO_PI;
        if (cos(da) <= 0.0) continue;

        // Each ray wanders sideways as a function of how far along it is, so its path is jagged
        // and its own; the wander grows from nothing at the root.
        float wander = (OdysseyCrackFbm(float2(r * 2.2, slot * 7.3)) - 0.5) * 0.55 * saturate(r * 0.9);
        float side = abs(r * sin(da) - wander);

        float rayLength = reach * (0.4 + 0.6 * OdysseyCrackHash1(float2(slot, 13.0)));
        float taper = pow(saturate(1.0 - r / rayLength), 0.6);
        float w = width * (0.55 + 0.9 * OdysseyCrackHash1(float2(slot, 7.0))) * taper;
        rays = max(rays, OdysseyCrackLine(side, w, px) * step(r, rayLength));
        rayDistance = min(rayDistance, r < rayLength ? side : 8.0);
    }

    // Branches: open meandering lines, the level set of a smooth noise, drawn near a ray — so they
    // fork off it — and, late on, across the whole face.
    float2 bp = p * 1.3 + seed * 5.1;
    float n = OdysseyCrackFbm(bp);
    float e = 0.02;
    float2 grad = float2(OdysseyCrackFbm(bp + float2(e, 0)) - n, OdysseyCrackFbm(bp + float2(0, e)) - n) / e * 1.3;
    float branchDistance = abs(n - 0.5) / max(length(grad), 0.05);
    float nearRay = 1.0 - smoothstep(0.08, 0.55, rayDistance);
    float web = smoothstep(0.55, 0.95, severity);
    float region = 1.0 - smoothstep(reach * 0.55, reach * 0.95, r * (0.8 + 0.4 * OdysseyCrackNoise(p * 0.9 + seed)));
    float branchMask = max(nearRay * smoothstep(0.1, 0.35, severity), web) * region;
    float branches = OdysseyCrackLine(branchDistance, width * 0.45, px) * branchMask;

    nearRoot = 1.0 - smoothstep(0.0, reach, r);
    return max(rays, branches * 0.85);
}

// The multiply a surface takes at this point: 1 untouched, darker along a crack. `severity` 0..1.
float OdysseyCrackMultiply(float3 positionWS, float3 normalWS, float severity)
{
    if (severity <= 0.0) return 1.0;

    // The cell this surface belongs to: a step inward along the normal puts a point on a cell's
    // boundary face inside the solid that owns it. 2.5 x 3.0 x 2.5 m, ADR 0002.
    const float3 cellSize = float3(2.5, 3.0, 2.5);
    float3 cell = floor((positionWS - normalWS * 0.05) / cellSize);
    float3 impact = (cell + 0.5) * cellSize + (OdysseyCrackHash3(cell) - 0.5) * float3(1.3, 1.6, 1.3);
    float seed = OdysseyCrackHash3(cell + 7.0).x;

    float3 w = pow(abs(normalWS), 4.0);
    w /= max(w.x + w.y + w.z, 1e-5);
    float px = max(length(fwidth(positionWS)) * 0.6, 1e-5);

    float3 p = positionWS;
    float rootX, rootY, rootZ;
    float crack = w.x * OdysseyCrackPlane(p.zy, impact.zy, seed, severity, px, rootX)
                + w.y * OdysseyCrackPlane(p.xz, impact.xz, seed + 0.31, severity, px, rootY)
                + w.z * OdysseyCrackPlane(p.xy, impact.xy, seed + 0.67, severity, px, rootZ);
    float root = w.x * rootX + w.y * rootY + w.z * rootZ;

    // The crushed patch where it was struck, late on, with a ragged edge.
    float crushed = smoothstep(0.62, 0.9, root + 0.25 * (OdysseyCrackNoise(p.xz * 3.1 + p.y * 2.3) - 0.5))
                  * smoothstep(0.5, 1.0, severity);
    float grime = 0.2 * severity * severity * (0.6 + 0.8 * root);

    float dark = lerp(0.5, 0.14, severity);
    return (1.0 - grime) * (1.0 - 0.3 * crushed) * lerp(1.0, dark, saturate(crack));
}

#endif
