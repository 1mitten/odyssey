// Water: translucent, lit, and cheap enough to cover a board with.
//
// Everything here is arithmetic. There is no texture, no extra pass, no render target and no
// second camera, because water is drawn by the ordinary chunk machinery — one instanced draw per
// chunk, exactly like the ground beside it — and the moment it needs a pass of its own it stops
// being a terrain and becomes a system. The ripples are two scrolling sine waves perturbing the
// normal; the reflection is the reflection probe every scene already has; the shore softens
// against the camera depth texture the outline feature already asks URP for. Nothing is added to
// the frame that was not already being paid for.
//
// Opacity comes from `_BaseColor.a`, which the palette sets per depth: shallow water is clear
// enough to read the bed through, deep water nearly is not. That is deliberate and is the whole
// of how depth is communicated — a channel is one cell deep whatever its depth, because making
// deep water two layers down would put neighbouring surface cells two layers apart and break the
// invariant that keeps the board walkable. Depth is a rendering problem; this is the renderer
// solving it.
//
// The shore fade earns its keep twice. It hides the hard line where a water tile meets the bank,
// which is the single thing that most makes tiled water read as tiles; and it makes shallow water
// *look* shallow at the edges for free, because the fade is driven by how far the bed is behind
// the surface.
Shader "Odyssey/Water"
{
    Properties
    {
        [MainColor] _BaseColor("Colour and opacity", Color) = (0.16, 0.36, 0.44, 0.78)
        _EmissionColor("Emission", Color) = (0, 0, 0, 0)

        // 1.2 waves per metre is a wavelength just under a metre, which at a 2.5 m cell puts
        // two or three crests across a tile. The first value tried was 0.35 - an eighteen-metre
        // swell - and it was invisible at every distance the game is played at, which is the
        // sort of thing only a photograph tells you.
        _RippleScale("Ripple scale (waves per metre)", Float) = 1.2
        _RippleSpeed("Ripple speed", Float) = 0.3
        _RippleStrength("Ripple strength", Range(0, 1)) = 0.3

        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        // Fresnel at 4 gave almost nothing back even at the shallowest pitch the rig allows:
        // the reflection is the one cue that separates water from coloured glass, so it is
        // weighted to appear well before the grazing angles.
        _ReflectionStrength("Reflection strength", Range(0, 1)) = 0.66
        _FresnelPower("Fresnel power", Range(1, 8)) = 2.5

        // Wide, because this is the only thing standing between a cell grid and a shoreline
        // that looks like one. The water meets its bank at a right angle every 2.5 m, and what
        // stops that reading as a staircase is the edge dissolving over a distance comparable to
        // a cell rather than over a few centimetres.
        //
        // Two metres and not more, and the ceiling is arithmetic rather than taste: the fade is
        // driven by the depth of water the eye looks through, and that is at most the height of
        // the surface above its bed - 2.16 m at the default ChunkMesher.WaterSurface. A fade set
        // wider than that never reaches one anywhere, so it stops being an edge treatment and
        // quietly makes the whole body more transparent.
        _ShoreFade("Shore fade (metres)", Float) = 2.0
        // With the shoreline on (design 38 §24) the ground slopes through the water line instead
        // of stopping at a wall on the cell's edge, so the depth fade traces a line of its own and
        // can be short: a soft edge a metre wide, and opaque water beyond it.
        _FieldShoreFade("Shore fade with the shoreline (metres)", Float) = 0.7
        // With the shoreline on: level water is at least this opaque. The reference's water is
        // a murky green, not a window on to a sand bed.
        _FieldOpacity("Opacity with the shoreline", Range(0, 1)) = 0.96
        // The palette's two depths, set on the one base material every water tile is cloned from
        // (MaterialCache.WaterBase) so they go through exactly the colour-space conversion
        // _BaseColor does. As globals they did not, and the ratio below came out a different
        // constant in each depth's material - which drew the deep cross back as a sharp step.
        _WaterShallow("Shallow (palette)", Color) = (0.21, 0.41, 0.39, 0.62)
        _WaterDeep("Deep (palette)", Color) = (0.08, 0.21, 0.24, 0.90)

        // How far the waterline is pushed in and out by the ripple field, in metres. A fade of
        // any width still follows the cells exactly if it is a pure function of depth; warping
        // it with the waves already being computed costs nothing and is what turns the corners
        // round, because the edge no longer agrees with the grid it was cut on.
        _ShoreWobble("Shore wobble (metres)", Float) = 0.55
        _SunGlint("Sun glint", Range(0, 4)) = 1.1

        // A falling sheet has rock a few centimetres behind it, so the depth of water the eye
        // looks through is nearly zero and the shore rule above - which exists to dissolve a
        // shoreline - erased the whole waterfall instead. A face is given a thickness of its own
        // rather than the measured one. 1.4 m against a 2.0 m fade leaves it substantial but not
        // opaque: a fall you can still see the rock through, which is what a thin sheet of moving
        // water does.
        _FallThickness("Falling sheet thickness (metres)", Float) = 1.4

        // Downward scroll of the streaks, in metres a second. Fast enough to read as falling at
        // the play camera rather than as a pattern sliding, and it is a *speed* and not a
        // frequency so it does not change with the streak scale.
        _FallSpeed("Falling sheet speed (metres/second)", Float) = 2.6

        _FallStreak("Falling sheet streaking", Range(0, 1)) = 0.45

        // Whitening at the foot of the drop, where the sheet breaks up on whatever it lands in.
        // Over the bottom third of the sheet, which is why the mesh carries a UV at all.
        _FallFoam("Falling sheet foam at the foot", Range(0, 2)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            // Past the opaque range, so the depth texture the outline pass reads has the *bed* in
            // it and not the water: water is never inked, for the same reason grass is not.
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // Depth is tested but not written. Two water tiles never overlap — one cell holds one
            // surface — so there is nothing to sort between, and not writing keeps the water out
            // of everything that reads depth afterwards.
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.5

            // No shadow keywords, so no shadow coordinate, no cascade selection and no soft
            // shadow taps. That is a deliberate saving *and* the right picture. Water does not
            // write depth and is drawn after the opaques, so it is already outside the shadowed
            // world; a tree shadow landing on a moving surface reads as dirt on it rather than as
            // shade, and the reference art shades its water by colour, not by cast shadow.
            //
            // Measured, since it was not obvious how much it was worth: with shadows, the wooded
            // board ran 1.28 ms a frame against 0.68 before water existed. This is where most of
            // that went — a transparent surface covering a tenth of the board, sampling a shadow
            // cascade per pixel on top of a cubemap and a depth fetch.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleStrength;
                float _Smoothness;
                float _ReflectionStrength;
                float _FresnelPower;
                float _ShoreFade;
                float _ShoreWobble;
                float _SunGlint;
                float _FallThickness;
                float _FallSpeed;
                float _FallStreak;
                float _FallFoam;
                float _FieldOpacity;
                float _FieldShoreFade;
                float4 _WaterShallow;
                float4 _WaterDeep;
            CBUFFER_END

            // The ground field (design 38 §24, GroundField.cs): one texel per column of the board,
            // A = the water there is deep. Globals, not material properties, so every water
            // material reads the same field and the shallow and the deep blend across a boundary
            // instead of meeting at it.
            TEXTURE2D(_OdysseyGroundField); SAMPLER(sampler_OdysseyGroundField);
            float4 _OdysseyGroundFieldST;
            float _OdysseyGroundFieldOn;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // Only the falling sheet reads this, and it reads only v: WaterMesh.Fall runs it
                // 0 at the foot of the drop to 1 at the lip, whatever the sheet is scaled to, so
                // "how far down this fall am I" is exact and costs no arithmetic.
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 screenPos   : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                output.uv = input.uv;
                return output;
            }

            // Two crossed waves at different scales and angles. Crossed rather than parallel
            // because a single direction reads as a conveyor belt, and the second wave is at an
            // irrational-ish ratio to the first so the pattern does not visibly repeat at board
            // distance. The result perturbs the normal only — the surface stays flat, which is
            // what keeps this a terrain tile and not a mesh that has to agree with its neighbours.
            // Returns the perturbed normal, and hands back the wave height as well: the shoreline
            // is warped by the same field that makes the ripples, so the edge and the surface
            // agree with each other and neither needs a field of its own.
            float3 RippleNormal(float3 normalWS, float3 positionWS, out float wave)
            {
                float t = _Time.y * _RippleSpeed;
                float2 p = positionWS.xz * _RippleScale;

                float a = sin(p.x * 1.00 + p.y * 0.35 + t * 1.00);
                float b = sin(p.x * -0.55 + p.y * 1.30 + t * 1.37);
                float c = sin(p.x * 1.90 + p.y * 1.70 + t * 0.61) * 0.35;

                // The gradient of that sum, near enough: the derivative of each wave scaled by its
                // own frequency, which is cheaper than finite differences and indistinguishable.
                float2 slope = float2(a * 1.00 - b * 0.55 + c * 1.90,
                                      a * 0.35 + b * 1.30 + c * 1.70) * _RippleStrength * 0.25;

                // A slower, longer version of the same waves for the shoreline. The ripple scale
                // is tuned to put two or three crests on a cell, which as a waterline would be a
                // frill rather than a bay, so this is sampled a quarter as often.
                float2 q = positionWS.xz * (_RippleScale * 0.25);
                wave = sin(q.x * 1.00 + q.y * 0.70 + t * 0.35) * 0.65 +
                       sin(q.x * -0.80 + q.y * 1.10 - t * 0.21) * 0.35;

                return normalize(normalWS + float3(slope.x, 0.0, slope.y));
            }

            half3 EnvironmentReflection(float3 reflectWS, float perceptualRoughness)
            {
                // The reflection probe the scene already has, sampled straight rather than through
                // GlossyEnvironmentReflection: the macro's signature has moved between URP
                // versions and this has not. With no probe in the scene it falls back to the
                // skybox, which is the gradient sky — the right answer for open water anyway.
                float mip = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness) * UNITY_SPECCUBE_LOD_STEPS;
                half4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectWS, mip);
                return DecodeHDREnvironment(encoded, unity_SpecCube0_HDR);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // **There was a top-face-only clip here, and taking it out is the point.**
                //
                // Water used to be drawn with the shared unit cube squashed to a 0.15 m slab, so
                // every cell had four side faces and an underside that nothing wanted. Opaque
                // geometry gets away with that because a neighbouring tile hides them; water
                // writes no depth, so the coincident side faces of two tiles either side of a
                // shared edge each added their own alpha and the board came out ruled into dark
                // squares along every cell boundary. `clip(normalWS.y - 0.5)` removed them for one
                // instruction, and the comment here called that cheaper than "adding a mesh shape
                // that nothing else would use".
                //
                // It also made a vertical face of water undrawable, which is what a lip and a
                // cascade both need (owner, 2026-09-17: water "in mid air"). So the mesh shape got
                // added after all — `WaterMesh` is two sheets, and a sheet has no spurious sides
                // to clip away. The dark-square fault is now answered by not building the faces
                // rather than by discarding them, and `WaterContributor` never puts a face between
                // two water cells, which is the only place two could ever be coincident.
                float3 viewWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float wave;
                float3 normalWS = RippleNormal(normalize(input.normalWS), input.positionWS, wave);

                // How much water the eye is looking through, in metres, from the depth of whatever
                // was drawn behind this pixel. The bed is opaque and drawn before us, so this is
                // the distance from the surface down to it.
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float through = max(sceneEye - surfaceEye, 0.0);

                // A shore is where there is barely any water between the surface and the bed, so
                // the edge dissolves instead of ending in a line. This is also why shallow water
                // looks shallow without being told that it is.
                //
                // The wobble is what stops it being a *square* line. A fade driven by depth alone
                // is a pure function of the geometry, so however wide it is made it still traces
                // the cell boundaries exactly; pushing the waterline in and out with the wave
                // field breaks the edge off the grid it was cut on, and the corners round
                // themselves. Smoothstep rather than a linear ramp, so the water thins into the
                // bank instead of arriving at it.
                // **Which way this fragment's own geometry faces**, taken before the ripple
                // perturbs anything — the same geometric normal the old top-face clip used. A
                // surface points up; a falling sheet does not.
                //
                // Sharpened with a smoothstep rather than used raw, and that is not fussiness: the
                // drape shears the surface by up to about five degrees, so a level tile's normal
                // is 0.997 and not 1, and a raw classifier would bleed a few thousandths of the
                // face treatment into every square metre of flat water. At 0.5..0.9 a sheared
                // surface is exactly 1 and a vertical face exactly 0, so **everything below leaves
                // the flat water bit-identical**, which matters because the flat water is the part
                // the owner has already looked at and accepted.
                float upness = smoothstep(0.5, 0.9, input.normalWS.y);

                // **A falling sheet is not a shoreline, though the arithmetic could not tell.**
                // `through` is the depth of water between this pixel and whatever was drawn behind
                // it, and behind a fall is the rock face it is pouring over — a few centimetres.
                // So the shore rule, which exists to dissolve the hard line where water meets its
                // bank, was instead erasing every waterfall on the board and leaving a pale pane
                // of glass (owner, 2026-09-17). A face is therefore given a thickness of its own
                // and keeps the measured one only where that is actually deeper.
                float fade = max(_ShoreFade, 1e-3);
                float thickness = lerp(max(through, _FallThickness), through, upness);
                float shore = smoothstep(0.0, 1.0, saturate((thickness + wave * _ShoreWobble) / fade));

                // **The shoreline** (design 38 §24). The ground now slopes through the water line
                // on a line of its own (BankLayout.ShoreFan) rather than stopping at a wall on the
                // cell's edge, so the depth fade traces that line and can be short. The ground
                // field is sampled here for the colour below: B = water, A = deep. Level water only.
                float4 groundField = 0;
                bool fieldOn = _OdysseyGroundFieldOn > 0.5 && upness > 0.5;
                [branch] if (fieldOn)
                {
                    float2 p = input.positionWS.xz;
                    float2 warp = float2(sin(p.y * 0.37) + sin(p.x * 0.21 + 1.3),
                                         sin(p.x * 0.41) + sin(p.y * 0.19 + 2.1)) * 0.6
                                + wave * _ShoreWobble;
                    float2 fuv = (p + warp) * _OdysseyGroundFieldST.xy;
                    groundField = SAMPLE_TEXTURE2D_LOD(_OdysseyGroundField, sampler_OdysseyGroundField, fuv, 0);
                    shore = smoothstep(0.0, 1.0,
                        saturate((through + wave * _ShoreWobble) / max(_FieldShoreFade, 1e-3)));
                }

                half4 base = _BaseColor;

                // **Shallow and deep, blended by the field rather than chosen by the cell**
                // (design 38 §24). A pond's deep middle used to be a cross of darker squares inside
                // the shallow, because each cell's material carried its own depth's colour. Every
                // water material now works out which depth it was made for from its own alpha (the
                // palette gives the two depths different alphas and the slice shading leaves alpha
                // alone), and rescales its colour towards the depth the field says is here — so the
                // slice's dimming survives and two neighbouring cells arrive at the same colour at
                // their shared edge. The warp breaks the blend off the grid it was sampled on.
                // Level water only: a falling sheet keeps its own.
                [branch] if (fieldOn)
                {
                    float deep = smoothstep(0.15, 0.85, groundField.a);
                    half span = _WaterDeep.a - _WaterShallow.a;
                    half self = abs(span) > 1e-3 ? saturate((base.a - _WaterShallow.a) / span) : 0.0;
                    half3 own = lerp(_WaterShallow.rgb, _WaterDeep.rgb, self);
                    half3 want = lerp(_WaterShallow.rgb, _WaterDeep.rgb, deep);
                    base.rgb *= want / max(own, half3(1e-3, 1e-3, 1e-3));
                    // Opaque enough that the bed's own grid — the pale sand floor meeting the bank's
                    // wall at the cell's edge — does not read through and square the shore again.
                    // The reference's water is a solid teal too.
                    base.a = max(lerp(_WaterShallow.a, _WaterDeep.a, deep), _FieldOpacity);
                }
                half3 colour = base.rgb;

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));

                // Diffuse, kept deliberately flat: water is not a lambertian surface and a strong
                // n.l over a big flat plane just makes it look like painted concrete.
                half3 lit = colour * (0.55 + 0.45 * ndotl) * mainLight.color;
                lit += colour * unity_AmbientSky.rgb * 0.35;

                // Sun glint. Blinn-Phong rather than anything physical, because what sells water
                // at this camera angle is a moving highlight, not an accurate BRDF.
                float3 halfWS = normalize(mainLight.direction + viewWS);
                half gloss = exp2(_Smoothness * 11.0 + 1.0);
                half spec = pow(saturate(dot(normalWS, halfWS)), gloss) * _SunGlint;
                lit += mainLight.color * spec;

                // Environment reflection, weighted by Fresnel so the water is glassy where it is
                // seen edge-on and clear where it is looked straight down into — which is the one
                // cue that makes a flat plane read as a liquid at a fixed camera pitch.
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), _FresnelPower);
                half3 reflection = EnvironmentReflection(reflect(-viewWS, normalWS), 1.0 - _Smoothness);
                half reflectAmount = _ReflectionStrength * fresnel;
                lit = lerp(lit, reflection, saturate(reflectAmount));

                lit += _EmissionColor.rgb;

                // Opacity: the palette's own alpha, opened up at the shore and closed towards
                // grazing angles, where in life you see the sky and not the bottom.
                half alpha = saturate(base.a * shore + reflectAmount);
                // With the field the shore takes the reflection with it: past the soft edge the
                // sheet lies over sunken bank, and a sky reflection left there drew pale glassy
                // triangles along every shore at the play camera.
                if (fieldOn) alpha = saturate((base.a + reflectAmount) * shore);

                // ---- the falling sheet -------------------------------------------------------
                //
                // **The ripple field cannot see a waterfall.** It is a function of
                // `positionWS.xz`, and on a vertical sheet xz is constant all the way down, so a
                // fall had no variation whatever along its own length — which is exactly why it
                // read as a pane of glass rather than as water coming down.
                //
                // **This is added to colour and not to the normal, deliberately.** At the play
                // camera's 48 degree pitch the view sits 42 degrees off a level surface, where the
                // Fresnel above returns about two per cent; the shading was tuned on the 20 degree
                // grazing contact sheet, where it returns ten times that. Anything carried by the
                // normal is therefore invisible at precisely the angle the game is played at, and
                // only something that moves the *colour* can be seen.
                //
                // `along` adds x and z rather than choosing between them because the sheet is
                // axis-aligned: one of the two is constant across it, so the sum varies along the
                // sheet and nothing has to know which way it is facing. `down` gains time so a
                // given crest sits lower each frame — negate it and the water falls upwards, which
                // is a thing worth stating because it looks plausible either way in a still.
                float fallAmount = 1.0 - upness;
                float along = input.positionWS.x + input.positionWS.z;
                float down  = input.positionWS.y + _Time.y * _FallSpeed;

                float streak = (sin(along * 2.7) * 0.5 + 0.5) *
                               (sin(down * 3.1 + along * 1.3) * 0.5 + 0.5);

                // The foot of the drop, where the sheet breaks up on what it lands in. The mesh's
                // v runs 0 at the bottom to 1 at the lip whatever the sheet is scaled to, so this
                // is the bottom third of *this* fall and not a fixed number of metres.
                float foot = 1.0 - saturate(input.uv.y * 3.0);
                float aerate = saturate(streak * _FallStreak + foot * _FallFoam) * fallAmount;

                lit = lerp(lit, half3(1.0, 1.0, 1.0), aerate * 0.75);
                alpha = saturate(alpha + aerate * 0.35);

                lit = MixFog(lit, input.fogFactor);
                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }

    // No shadow caster and no depth pass, on purpose. Water that wrote depth would be in the
    // texture the outline pass reads and would be inked around every tile edge; water that cast
    // shadows would shade its own bed.
    Fallback Off
}
