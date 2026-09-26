// Butterflies (design 52), drawn entirely from a structured buffer: one Graphics.RenderPrimitives
// call for every butterfly on screen, by day and by night.
//
// **No mesh.** The body and the four wing panels are tables in this file, and a vertex is chosen
// by its id: BUTTERFLY_VERTS corners an instance, ButterflyDirector.VertsPerButterfly says the same
// number, and ButterflyDirectorTests reads this file to hold the two together.
//
// **The model says where and how; the flap is here.** ButterflyMeadow (Odyssey.Hud) writes four
// float4s a butterfly — (x, y, z, heading), (ground, flap, rest, scale), (seed, bank, pitch, beats a
// second), (bask, 0, 0, 0) — and this shader turns them into strokes: an asymmetric beat (42 % down,
// e-13 §6) with the hindwing lagging, a glide pose, the wings closed upright at rest or opening
// slowly to bask, the body bobbing against the stroke, a bank into the turn and a nose up in a climb.
//
// **Every colour is a uniform** from ButterflyPalette (Odyssey.Hud): the species' four colours and
// switches, and the four night glows. This file writes none of its own, so the colour-blind test in
// the fast tier is a test of what is drawn.
//
// **At night the butterfly itself is the light** (owner, 2026-09-26, after the first look: "it just
// needs to colour the butterflies a illuminating colour and they move around - not those big glowing
// saucers"). The whole wing takes its glow hue, the pattern brightest and the veins dimmest, and the
// wings are drawn out to the camera's full reach, so a far night is coloured specks moving over the
// meadow. The halo and the pool of light that were a second pass are gone (design 52 §5a).
Shader "Odyssey/Butterfly"
{
    Properties
    {
        // Metres across the wings of an ordinary butterfly: four to six times life (e-13 §12).
        _Span("Span", Float) = 0.36
        // By day, x where the wings start to shrink out with distance and y where they are gone
        // (metres); z and w the same at night, when a lit wing is drawn to the full zoom.
        _WingFade("Wing fade", Vector) = (80, 110, 170, 200)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+501"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ButterflyForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define BUTTERFLY_VERTS 96

            CBUFFER_START(UnityPerMaterial)
                float _Span;
                float4 _WingFade;
            CBUFFER_END

            // Four float4s a butterfly, written by ButterflyDirector each frame.
            StructuredBuffer<float4> _ButterflyData;

            // Set by ButterflyDirector: x the ambient clock (real seconds while the world runs),
            // y how far into the night it is (0 day, 1 full dark), z the wing's glow ceiling,
            // w unused.
            float4 _ButterflyClock;

            // ButterflyPalette.All, four colours each (linear): ground, dark, accent, eye.
            float4 _ButterflySpecies[24];
            // x veins, y margin, z eyespot chance, w band (0 none, 1 dark, 2 accent).
            float4 _ButterflyShape[6];
            // x marginal spots (0 or 1).
            float4 _ButterflyShape2[6];
            // ButterflyPalette.Glows: rgb the colour with its gain (linear), a the running weight.
            float4 _ButterflyGlow[4];
            // x hue drift (radians), y slowest drift period, z quickest, w the pulse's floor.
            float4 _ButterflyDrift;
            // x slowest breath (Hz), y quickest.
            float4 _ButterflyBreath;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 polar      : TEXCOORD1;   // (r * theta, r), exact across a fan triangle
                nointerpolation float4 info : TEXCOORD2;   // seed, hind, body, 0
                nointerpolation float4 glow : TEXCOORD3;   // the night colour, pulsed, times the night
                half fogFactor    : TEXCOORD4;
            };

            uint Pcg(uint v)
            {
                uint state = v * 747796405u + 2891336453u;
                uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
                return (word >> 22u) ^ word;
            }

            float Rand(uint v) { return Pcg(v) * (1.0 / 4294967296.0); }

            Varyings Culled()
            {
                Varyings o = (Varyings)0;
                o.positionCS = float4(2, 2, 2, 1);
                return o;
            }

            // ------------------------------------------------------------ the shape
            // Wing-local metres for a butterfly one metre across: x outboard, y forward.

            static const float2 ForeRoot = float2(0.02, 0.02);
            static const float2 Fore[7] =
            {
                float2(0.02, 0.06), float2(0.16, 0.20), float2(0.34, 0.27), float2(0.48, 0.24),
                float2(0.50, 0.14), float2(0.40, 0.02), float2(0.06, -0.02),
            };
            static const float2 HindRoot = float2(0.03, -0.03);
            static const float2 Hind[7] =
            {
                float2(0.06, -0.02), float2(0.30, -0.04), float2(0.40, -0.12), float2(0.38, -0.24),
                float2(0.26, -0.32), float2(0.12, -0.28), float2(0.03, -0.12),
            };
            // Head, tail, left, right, top, bottom: eight faces of a long diamond.
            static const float3 Body[6] =
            {
                float3(0, 0, 0.13), float3(0, 0, -0.26), float3(-0.035, 0, 0),
                float3(0.035, 0, 0), float3(0, 0.03, 0), float3(0, -0.025, 0),
            };
            static const uint BodyTris[24] = { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 };

            // The stroke: up to 62 degrees, down to -48, the downstroke 42 % of the beat.
            float Stroke(float phase)
            {
                const float Down = 0.42;
                const float Up = radians(62.0), Low = radians(-48.0);
                if (phase < Down) return lerp(Up, Low, smoothstep(0.0, 1.0, phase / Down));
                return lerp(Low, Up, smoothstep(0.0, 1.0, (phase - Down) / (1.0 - Down)));
            }

            float3 HueRotate(float3 c, float a)
            {
                const float3 k = float3(0.57735, 0.57735, 0.57735);
                float ca = cos(a);
                return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
            }

            // A breath: a quick rise and a slow fall, never below the floor (e-14 §7).
            float Breath(float x)
            {
                float b = x < 0.3 ? smoothstep(0.0, 1.0, x / 0.3) : 1.0 - smoothstep(0.0, 1.0, (x - 0.3) / 0.7);
                return lerp(_ButterflyDrift.w, 1.0, b);
            }

            // The night colour a butterfly glows in this instant: its dealt hue, wandering and breathing.
            float3 NightColour(uint seed, float t)
            {
                float pick = Rand(seed * 17u + 5u);
                float3 c = _ButterflyGlow[3].rgb;
                [unroll] for (int i = 3; i >= 0; i--)
                    if (pick < _ButterflyGlow[i].a) c = _ButterflyGlow[i].rgb;

                float period = lerp(_ButterflyDrift.y, _ButterflyDrift.z, Rand(seed * 19u + 7u));
                float drift = _ButterflyDrift.x * sin(t * 6.2831853 / period + Rand(seed * 23u) * 6.2831853);
                c = max(HueRotate(c, drift), 0.0);

                float hz = lerp(_ButterflyBreath.x, _ButterflyBreath.y, Rand(seed * 29u + 3u));
                float brightness = lerp(0.8, 1.2, Rand(seed * 41u + 9u));   // not by hue alone (e-14 §6)
                return c * Breath(frac(t * hz + Rand(seed * 31u))) * brightness;
            }

            // ------------------------------------------------------------ wings

            Varyings Wing(uint vid, uint iid)
            {
                float4 d0 = _ButterflyData[iid * 4u];
                float4 d1 = _ButterflyData[iid * 4u + 1u];
                float4 d2 = _ButterflyData[iid * 4u + 2u];
                float4 d3 = _ButterflyData[iid * 4u + 3u];
                if (d1.w <= 0.0) return Culled();

                uint seed = (uint)d2.x;
                float t = _ButterflyClock.x;
                float flap = d1.y, rest = d1.z;

                // By day the wings shrink out past _WingFade.xy; at night a lit wing is worth drawing
                // to the full zoom, so the fade moves out to _WingFade.zw as the dark comes on.
                float night = _ButterflyClock.y;
                float distance = length(_WorldSpaceCameraPos.xyz - d0.xyz);
                float2 fade = lerp(_WingFade.xy, _WingFade.zw, night);
                float wingFade = 1.0 - smoothstep(fade.x, fade.y, distance);
                if (wingFade <= 0.0) return Culled();

                // The beat, each butterfly its own phase; the hindwing a little behind the fore.
                float phase = frac(t * d2.w + Rand(seed * 3u + 1u));
                float glide = radians(12.0);
                float restAngle;
                if (d3.x > 0.5)
                {
                    float open = 0.5 + 0.5 * sin(t * 6.2831853 / lerp(5.0, 9.0, Rand(seed * 5u)) + Rand(seed * 7u) * 6.2831853);
                    restAngle = lerp(radians(86.0), radians(8.0), open);
                }
                else restAngle = radians(86.0) + radians(4.0) * sin(t * 1.3 + Rand(seed * 11u) * 6.2831853);
                float foreAngle = lerp(lerp(glide, Stroke(phase), flap), restAngle, rest);
                float hindAngle = lerp(lerp(glide, Stroke(frac(phase - 0.07)), flap), restAngle, rest);

                uint tri = vid / 3u, corner = vid % 3u;
                float3 p;
                float2 polar = float2(0, 0);
                float hind = 0.0, body = 0.0;
                if (tri < 24u)
                {
                    uint w = tri / 6u, seg = tri % 6u;
                    hind = w >= 2u ? 1.0 : 0.0;
                    float side = (w & 1u) != 0u ? -1.0 : 1.0;
                    float2 root = hind > 0.5 ? HindRoot : ForeRoot;
                    float2 q = root;
                    if (corner != 0u)
                    {
                        uint k = seg + corner - 1u;
                        q = hind > 0.5 ? Hind[k] : Fore[k];
                        // Each butterfly's outline its own, within eight per cent (e-14).
                        q = root + (q - root) * (1.0 + (Rand(seed * 64u + w * 8u + k) - 0.5) * 0.16);
                        polar = float2(k / 6.0, 1.0);
                    }
                    float a = hind > 0.5 ? hindAngle : foreAngle;
                    p = float3(side * q.x * cos(a), q.x * sin(a), q.y);
                }
                else
                {
                    body = 1.0;
                    p = Body[BodyTris[(tri - 24u) * 3u + corner]];
                }

                // Close in, life-ish; zoomed out, a little larger so a wing still reads (the birds'
                // rule, design 50 §4) - and larger again at night, when a lit speck is the whole picture.
                float grow = lerp(1.0, lerp(1.6, 2.4, night), smoothstep(30.0, 120.0, distance));
                float size = _Span * lerp(0.85, 1.15, Rand(seed * 13u + 2u)) * d1.w * grow * wingFade;
                p *= size;

                float bank = d2.y, pitch = d2.z, heading = d0.w;
                float cb = cos(bank), sb = sin(bank);
                p = float3(p.x * cb - p.y * sb, p.x * sb + p.y * cb, p.z);
                float cp = cos(pitch), sp = sin(pitch);
                p = float3(p.x, p.y * cp + p.z * sp, -p.y * sp + p.z * cp);
                float cy = cos(heading), sy = sin(heading);
                p = float3(p.x * cy + p.z * sy, p.y, -p.x * sy + p.z * cy);

                // The body rises on the downstroke and falls on the up (e-13 §4).
                float bob = 0.09 * size * sin(6.2831853 * phase + 0.6) * flap * (1.0 - rest);

                Varyings o = (Varyings)0;
                o.positionWS = d0.xyz + p + float3(0, bob, 0);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.polar = polar;
                o.info = float4(seed, hind, body, 0);
                o.glow = float4(NightColour(seed, t) * _ButterflyClock.y, 0);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // The ground plan (e-14 §1): cells between veins, a band, eyespots, a margin with spots.
            // Returns the albedo; glow says how much of the night colour this part of the wing carries.
            float3 Pattern(float2 polar, uint seed, float hind, out float glow)
            {
                float r = polar.y;
                float th = polar.x / max(r, 1e-4);
                uint s = Pcg(seed) % 6u;
                float3 ground = _ButterflySpecies[s * 4u].rgb * lerp(0.9, 1.1, Rand(seed * 43u));
                float3 dark = _ButterflySpecies[s * 4u + 1u].rgb;
                float3 accent = _ButterflySpecies[s * 4u + 2u].rgb;
                float3 eye = _ButterflySpecies[s * 4u + 3u].rgb;
                float4 shape = _ButterflyShape[s];
                float spots = _ButterflyShape2[s].x;

                float n = 5.0 + (float)(Pcg(seed * 3u) % 3u);
                float c = th * n;
                float cell = floor(c), f = frac(c);

                float3 col = ground;
                glow = 0.2;

                if (r < 0.14) { col = dark * 1.3; glow = 0.05; }

                if (shape.w > 0.5)
                {
                    float centre = 0.42 + (Rand(seed * 47u) - 0.5) * 0.12;
                    if (abs(r - centre + 0.04 * sin(th * 9.0)) < 0.075)
                    {
                        bool bright = shape.w > 1.5;
                        col = bright ? accent : dark;
                        glow = bright ? 1.0 : 0.05;
                    }
                }

                float chance = shape.z * (hind > 0.5 ? 1.3 : 0.7);
                if (Rand(seed * 53u + (uint)cell * 7u + (uint)hind * 101u) < chance)
                {
                    float size = lerp(0.08, 0.15, Rand(seed * 59u + (uint)cell));
                    float d = length(float2((f - 0.5) * r * 1.57 / n, r - 0.70));
                    if (d < size) { col = dark; glow = 0.05; }
                    if (d < size * 0.72) { col = eye; glow = 1.0; }
                    if (d < size * 0.3) { col = accent; glow = 1.0; }
                }

                float margin = shape.y * lerp(0.8, 1.2, Rand(seed * 61u));
                if (r > 1.0 - margin)
                {
                    col = dark;
                    glow = 0.05;
                    if (spots > 0.5)
                    {
                        float dy = (r - (1.0 - margin * 0.5)) / max(margin, 0.01);
                        if (length(float2((f - 0.5) * 1.2, dy)) < 0.3) { col = accent; glow = 1.0; }
                    }
                }

                // The veins last, through everything, and dark at night: the wing reads as stained glass.
                if (shape.x > 0.0 && r > 0.12 && abs(f - 0.5) > 0.5 - shape.x / max(r, 0.25))
                {
                    col = dark;
                    glow = 0.0;
                }
                return col;
            }

            half4 WingFragment(Varyings input)
            {
                uint seed = (uint)input.info.x;
                float glowMask;
                float3 albedo;
                if (input.info.z > 0.5)
                {
                    albedo = _ButterflySpecies[(Pcg(seed) % 6u) * 4u + 1u].rgb;
                    glowMask = 0.35;
                }
                else albedo = Pattern(input.polar, seed, input.info.y, glowMask);

                // Flat shading from the screen-space derivative, as the birds do (design 50 §3): a
                // bent wing shades as bent, and a face is one colour, which is the Synty look.
                float3 normal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                Light sun = GetMainLight();
                float facing = abs(dot(normal, sun.direction));
                float3 ambient = lerp(unity_AmbientGround.rgb, unity_AmbientSky.rgb, 0.5 + 0.5 * abs(normal.y));
                float3 lit = albedo * (ambient + sun.color * (0.35 + 0.65 * facing));

                // At night the whole wing is lit in its hue (design 52 §5a): the pattern brightest, the
                // ground a little under, the veins and the margin dimmest so it still reads as a wing.
                // The day's colour fades out under it, or moonlit albedo muddies the hue. Never over
                // the ceiling: a wing a few pixels across that crosses bloom's threshold shimmers.
                float night = _ButterflyClock.y;
                float3 emit = min(input.glow.rgb * lerp(0.35, 1.0, saturate(glowMask)), _ButterflyClock.z);
                return half4(MixFog(lit * (1.0 - 0.75 * night) + emit, input.fogFactor), 1);
            }

            // ------------------------------------------------------------ entry points

            Varyings Vertex(uint vid : SV_VertexID, uint iid : SV_InstanceID) { return Wing(vid, iid); }

            half4 Fragment(Varyings input) : SV_Target { return WingFragment(input); }
            ENDHLSL
        }
    }
}
