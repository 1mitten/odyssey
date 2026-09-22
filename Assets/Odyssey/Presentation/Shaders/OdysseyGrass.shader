// A clump of grass blades: coloured root to tip, bowed by the wind, leaned towards the camera.
//
// **Why a shader of our own rather than the pack's foliage.** Synty's grass is a cut-out: a
// texture of painted blades on a card, alpha-clipped. Three things follow and all three are
// problems here. The silhouette is theirs and cannot be changed. The clip is the worst kind of
// overdraw at a camera that looks down a meadow at 48 degrees, because every pixel of every card
// behind every other card is shaded and then thrown away. And nothing draws at all on a machine
// without `Assets/Synty`, which includes the build runner — so the largest visible thing in the
// game was the one thing no test could see. Our blades are geometry. There is no texture, no
// cutout and no clip anywhere in this file, which is why it has no `_Alpha_Clip_Threshold`.
//
// **What the vertex colour carries.** Red is the height along the blade, black at the root and
// full at the tip; green is a per-blade constant. See `GrassMesh`. Red drives the colour ramp,
// the bow, the lean towards the camera and the shading at the base; green offsets the wind phase
// so the blades of one clump do not move as a rigid body, which is the single thing that makes
// cheap grass look cheap.
//
// **The wind bows the blade; it does not drag its tip.** Every vertex is rotated about the
// clump's own root, by an angle that grows with height, so the blade keeps its length and its
// base stays planted. Translating the tip instead — which is what this did first, and what most
// of the cheap grass on the internet does — stretches the blade as the wind rises, and a field
// of grass that grows when it is windy is the sort of thing you see without being able to say
// why it looks wrong.
//
// **Every pass deforms identically, through `GrassBend`.** If the depth and depth-normals passes
// bowed differently the ink outline and the ambient occlusion would be drawn against grass
// standing somewhere the picture does not show it. The one deliberate exception is the shadow
// caster: it runs from the light, where `_WorldSpaceCameraPos` is the light's position and the
// camera lean would be nonsense, so it is passed zero. Grass casts no shadows by default
// (`ChunkRenderer.FoliageCastsShadows`), so that costs nothing today and is right the day
// somebody switches them on.
//
// **The normals are a lie, on purpose.** `GrassMesh` leans each blade's normal most of the way
// towards vertical so a clump lights as one mass rather than as a dozen facets, and the bow does
// not re-tilt them. Both sides of a blade are drawn with the same normal, because the authored
// normal is chosen for how it reads rather than for where the surface points.
Shader "Odyssey/Grass"
{
    Properties
    {
        // The tint the material cache writes: the foliage palette entry times the slice's depth
        // shade, exactly as every other bucket in the world receives it.
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)

        // Kept in step with GrassLook, which is what writes them at runtime; these are what a
        // material built without it draws, which is a contact sheet or a test.
        _RootColour("Root colour", Color) = (0.290, 0.416, 0.212, 1)
        _TipColour("Tip colour", Color) = (0.616, 0.780, 0.376, 1)

        // How much of the ramp is spent low down. Above one the root colour hangs on and the
        // blade reads as a dark clump with bright ends, which is the illustrated look.
        _RampBias("Ramp bias", Range(0.25, 4)) = 1.6

        // How hard the ramp steps. Zero is a gradient; one is two flat bands with a line between
        // them. The Look setting drives it, so one shader serves both rungs.
        _Banding("Banding", Range(0, 1)) = 0
        _BandHeight("Band height", Range(0, 1)) = 0.45

        // Darkening at the foot of the blade, and how far up it reaches. This is the contact
        // shading that makes a clump sit *in* the ground rather than on it, and at our camera it
        // does more for the read than anything happening at the tip.
        // Gentler than it first was. From directly above you see tips and the ground between
        // blades and very little base, so contrast spent down here is contrast nobody sees;
        // the occlusion that would read belongs on the ground itself, which is a different
        // material and a later unit (b-botw-grass.md, transfer item 6).
        _BaseShade("Shade at the base", Range(0, 1)) = 0.80
        _BaseShadeHeight("How far up the shade reaches", Range(0.05, 1)) = 0.35

        // Patch-to-patch colour drift, from the clump's own world position. Two rotated sine
        // waves rather than a texture: no fetch, and at these wavelengths the interference reads
        // as patches rather than as stripes.
        _PatchVariation("Patch variation", Range(0, 0.5)) = 0.14

        // The blade lighting up when the sun is behind it. Cheap fake translucency, and the thing
        // that makes a field read as alive rather than as painted cardboard.
        // Halved after the research: at one or two pixels a blade a moving highlight is an
        // aliasing generator, and at 48 degrees looking down we are almost never viewing
        // blades against the sun. Kept because the day cycle puts the sun low at both ends
        // and the rig pitches to about 20, which is exactly when it fires — so it is a
        // dawn-and-dusk effect rather than a default one.
        _Sheen("Backlight sheen", Range(0, 3)) = 0.45
        _SheenSharpness("Backlight tightness", Range(1, 32)) = 7
        [HDR] _SheenColour("Backlight colour", Color) = (0.72, 0.84, 0.38, 1)

        // How far the tip leans towards the camera. A blade is a thin upright card and a camera
        // looking down sees its top edge; leaning it over turns its face up towards the lens.
        // This is the setting that decides whether a meadow reads as grass or as grey fuzz.
        _FaceCamera("Lean towards the camera", Range(0, 1)) = 0.3

        // Blades widen with distance, holding roughly constant coverage as they thin out in
        // pixels. Not decoration: a one-pixel triangle wastes four to eight times over on 2x2
        // quad shading, and a field of them shimmers as the camera moves. The research calls
        // this the place a frame budget is won or lost (b-botw-grass.md, transfer item 3).
        _WidenStart("Widening starts, metres", Float) = 25
        _WidenEnd("Widening is full by, metres", Float) = 110
        _WidenAmount("How much wider, as a multiple", Range(0, 4)) = 1.6

        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _RootColour;
            float4 _TipColour;
            float4 _SheenColour;
            float _RampBias;
            float _Banding;
            float _BandHeight;
            float _BaseShade;
            float _BaseShadeHeight;
            float _PatchVariation;
            float _Sheen;
            float _SheenSharpness;
            float _FaceCamera;
            float _WidenStart;
            float _WidenEnd;
            float _WidenAmount;
            float _Metallic;
            float _Smoothness;
        CBUFFER_END

        // Globals, written once a frame by WindDirector. All-zero is a valid state and means
        // still air, so grass drawn before anything sets them — a contact sheet, a test harness,
        // the first frame — stands up straight instead of disappearing.
        //
        // xyz is the wind's horizontal direction times its strength, as a bow angle in radians at
        // the tip; w is the phase the director accumulates from the game clock rather than from
        // wall time, so a paused game holds the frame it is on.
        float4 _OdysseyWind;

        // How quickly the gust wave crosses the ground, in radians a metre. A global so one
        // number governs the whole board and two shaders could never disagree about how large a
        // gust is.
        float _OdysseyWindWavelength;

        /// The furthest any vertex may be moved. GrassMesh.MaxSway grows the mesh bounds by the
        /// same figure and GrassTests is what stops the two drifting apart.
        #define ODYSSEY_GRASS_MAX_BOW 0.60

        // Widen a blade with distance, in object space and before anything else touches it.
        //
        // The spread vector in UV2 points from the blade's spine out to this vertex, so a
        // multiple of it widens the blade and moves nothing else — spine, tip and root all stay
        // exactly where they were. Distance is taken from the clump's root rather than the
        // vertex, so a whole clump widens together and no blade shears against its neighbour.
        float3 GrassWiden(float3 positionOS, float2 spread, float3 rootWS)
        {
            float span = distance(_WorldSpaceCameraPos.xyz, rootWS);
            float t = saturate((span - _WidenStart) / max(_WidenEnd - _WidenStart, 1e-3));
            return positionOS + float3(spread.x, 0, spread.y) * (t * _WidenAmount);
        }

        float3 RotateAbout(float3 v, float3 axis, float angle)
        {
            float s, c;
            sincos(angle, s, c);
            return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
        }

        // Bow a blade about its clump's root. positionWS is where the vertex stands in still air,
        // rootWS is the clump's origin, heightT is the vertex colour's red and bladePhase its
        // green.
        float3 GrassBend(float3 positionWS, float3 rootWS, float heightT, float bladePhase, float faceCamera)
        {
            // Two waves rather than one, at unrelated rates, so the field never settles into a
            // pulse; plus a slow swell that gives it lulls and surges instead of a constant chop.
            // This is the whole difference between "there is wind" and "something is vibrating".
            // **The per-blade phase is a nudge, not a randomisation, and that is the whole
            // difference between wind and shimmer.** It was a full turn of 2*pi, which
            // decorrelates every blade in a clump: close up that is pleasing detail, and at one
            // or two pixels a blade it is noise crawling over the field. The signal that reads
            // from tens of metres is the *world-space* term, because that is the one whose
            // wavelength is measured in metres (b-botw-grass.md, transfer item 2).
            float travel = (positionWS.x + positionWS.z) * _OdysseyWindWavelength;
            float fast = sin(_OdysseyWind.w + travel + bladePhase * 0.9);
            float slow = sin(_OdysseyWind.w * 0.43 + travel * 0.31 + bladePhase * 0.5 + 1.7);
            float gust = fast * 0.62 + slow * 0.38;
            float swell = 0.62 + 0.38 * sin(_OdysseyWind.w * 0.17 + travel * 0.11);

            float strength = length(_OdysseyWind.xyz);
            float3 push = strength > 1e-5
                ? (_OdysseyWind.xyz / strength) * (strength * swell * (0.66 + 0.34 * gust))
                : float3(0, 0, 0);

            // The lean is horizontal and towards the camera, and it goes into the same bow rather
            // than being a second rotation: one sincos, and the two never fight each other.
            float3 toCamera = _WorldSpaceCameraPos.xyz - positionWS;
            toCamera.y = 0;
            float span = length(toCamera);
            if (span > 1e-4) push += (toCamera / span) * faceCamera;

            float amount = length(push);
            if (amount < 1e-5) return positionWS;

            float3 direction = push / amount;
            // Squared in height, so the base stays planted and the bow happens up the blade.
            float angle = min(amount, ODYSSEY_GRASS_MAX_BOW) * heightT * heightT;
            float3 axis = normalize(cross(float3(0, 1, 0), direction));

            return rootWS + RotateAbout(positionWS - rootWS, axis, angle);
        }

        // Patch-to-patch drift, from the clump's root so a whole clump moves together: per blade
        // it would read as noise, and per cell it would read as the grid.
        //
        // This is the highest-value thing in the whole shader at our camera. Breath of the Wild
        // paints grass colour AND height as a 64x64 map over each terrain area — hue and value
        // both, which is why one vista runs from yellow-green through olive — and a patch-scale
        // signal is the one whose wavelength is large enough to survive a blade being two pixels
        // wide. Two rotated sines stand in for a painted map until there is one; a map is the
        // upgrade, not a rewrite (b-botw-grass.md §4, transfer item 1).
        float GrassPatch(float3 rootWS)
        {
            float a = sin(dot(rootWS.xz, float2(0.0731, 0.0517)) + 1.3);
            float b = sin(dot(rootWS.xz, float2(-0.0413, 0.0629)) + 4.1);
            return saturate((a * 0.5 + b * 0.5) * 0.5 + 0.5);
        }

        // Root to tip, either as a gradient or as flat bands, then the contact shade at the foot
        // and the patch drift over the whole clump.
        float3 GrassAlbedo(float heightT, float patch)
        {
            float t = pow(saturate(heightT), _RampBias);
            t = lerp(t, step(_BandHeight, t), _Banding);
            float3 colour = lerp(_RootColour.rgb, _TipColour.rgb, t) * _BaseColor.rgb;

            float shade = lerp(_BaseShade, 1.0, saturate(heightT / max(_BaseShadeHeight, 1e-3)));
            // Value and hue, not value alone: a field that varies only in brightness reads as
            // one green under passing cloud, where one that also swings between yellow-green and
            // blue-green reads as different grass growing in different places.
            float drift = lerp(1.0 - _PatchVariation, 1.0 + _PatchVariation, patch);
            float3 hue = float3(1.0 + (patch - 0.5) * _PatchVariation,
                                1.0,
                                1.0 - (patch - 0.5) * _PatchVariation);
            return colour * (shade * drift) * hue;
        }
        ENDHLSL

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            // Both sides. A blade is a flat strip and half the meadow faces away at any moment;
            // culling would leave a field of holes that changes as the camera turns.
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            // Without this the instanced path draws every clump at the origin, and it fails
            // silently rather than magenta.
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float2 spread     : TEXCOORD2;
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 colour     : COLOR;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                // x is the height along the blade, y the clump's patch drift. Packed into one
                // interpolator because the second is constant over a clump and costs nothing to
                // carry beside the first.
                half2  blade      : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(
                    GrassWiden(input.positionOS.xyz, input.spread, rootWS));
                positionWS = GrassBend(positionWS, rootWS, input.colour.r, input.colour.g, _FaceCamera);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.blade = half2(input.colour.r, GrassPatch(rootWS));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = GrassAlbedo(input.blade.x, input.blade.y);
                surface.alpha = 1;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // Backlight, as emission rather than as a light term. A blade is thin enough to
                // pass light, and looking into the sun across a field is when grass stops being
                // green and starts being lit. Weighted by height, because the base is buried in
                // the clump and nothing gets through it, and by the shadow so a field under a
                // building does not glow.
                Light main = GetMainLight(inputData.shadowCoord);
                float back = saturate(-dot(inputData.viewDirectionWS, main.direction));
                float sheen = pow(back, _SheenSharpness) * input.blade.x * _Sheen * main.shadowAttenuation;
                surface.emission = _SheenColour.rgb * main.color * sheen;

                half4 colour = UniversalFragmentPBR(inputData, surface);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                colour.a = 1;
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float2 spread     : TEXCOORD2;
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 colour     : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(
                    GrassWiden(input.positionOS.xyz, input.spread, rootWS));

                // The wind, but not the camera lean: this pass runs from the light, where
                // _WorldSpaceCameraPos is the light's own position and leaning towards it would
                // bow the grass somewhere the picture never shows it.
                positionWS = GrassBend(positionWS, rootWS, input.colour.r, input.colour.g, 0);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma target 3.5

            struct DepthAttributes
            {
                float2 spread     : TEXCOORD2;
                float4 positionOS : POSITION;
                float4 colour     : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(
                    GrassWiden(input.positionOS.xyz, input.spread, rootWS));
                positionWS = GrassBend(positionWS, rootWS, input.colour.r, input.colour.g, _FaceCamera);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals as well, and it is not optional. The renderer runs a depth-normals prepass
        // every frame for screen-space ambient occlusion, and anything missing from it is a hole
        // in the occlusion and, once the outline pass reads normals, a hole in the ink.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct NormalsAttributes
            {
                float2 spread     : TEXCOORD2;
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 colour     : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            NormalsVaryings DepthNormalsVertex(NormalsAttributes input)
            {
                NormalsVaryings output = (NormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(
                    GrassWiden(input.positionOS.xyz, input.spread, rootWS));
                positionWS = GrassBend(positionWS, rootWS, input.colour.r, input.colour.g, _FaceCamera);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(NormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
