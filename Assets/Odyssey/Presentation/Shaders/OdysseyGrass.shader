// A clump of grass blades: coloured root to tip, bent by the wind, leaned towards the camera.
//
// **Why a shader of our own rather than the pack's foliage.** Synty's grass is a cut-out: a
// texture of painted blades on a card, alpha-clipped. Three things follow from that and all three
// are problems here. The silhouette is theirs and cannot be changed. The clip is the worst kind of
// overdraw at a camera that looks down a meadow at 48 degrees, because every pixel of every card
// behind every other card is shaded and then thrown away. And nothing draws at all on a machine
// without `Assets/Synty`, which includes the build runner — so the largest visible thing in the
// game was the one thing no test could see. Our blades are geometry. There is no texture, no
// cutout and no clip anywhere in this file, which is also why it has no `_Alpha_Clip_Threshold`.
//
// **What the vertex colour carries.** Red is the height along the blade, black at the root and
// full at the tip; green is a per-blade constant. See `GrassMesh` for why they are there. Red
// drives the colour ramp, the wind bend and the camera lean; green offsets the wind phase so the
// blades of one clump do not sway as a rigid body, which is the thing that makes cheap grass look
// cheap.
//
// **Every pass deforms identically, through `GrassDeform`.** The bend happens in the vertex
// shader, so if the depth and depth-normals passes did not bend the same way the ink outline and
// the ambient occlusion would be drawn against grass standing somewhere the picture does not show
// it. The one deliberate exception is the shadow caster: it runs from the light rather than the
// camera, where `_WorldSpaceCameraPos` is the light's position and the camera lean would be
// nonsense. Grass casts no shadows by default (`ChunkRenderer.FoliageCastsShadows`), so this
// costs nothing, and leaving the lean in would have been a bug waiting for somebody to switch
// shadows on.
//
// **The normals are a lie, on purpose.** `GrassMesh` leans each blade's normal most of the way
// towards vertical so a clump lights as one mass rather than as a dozen facets, and the bend does
// not re-tilt them. Both sides of a blade are drawn with the same normal, because the authored
// normal is already chosen for how it reads rather than for where the surface points.
Shader "Odyssey/Grass"
{
    Properties
    {
        // The tint the material cache writes: the foliage palette entry times the slice's depth
        // shade, exactly as every other bucket in the world receives it.
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)

        // Kept in step with GrassLook, which is what actually writes them at runtime; these
        // are what a material built without it draws, which is a contact sheet or a test.
        _RootColour("Root colour", Color) = (0.290, 0.416, 0.212, 1)
        _TipColour("Tip colour", Color) = (0.616, 0.780, 0.376, 1)

        // How much of the ramp is spent low down. Above one the root colour hangs on and the
        // blade reads as a dark clump with bright ends, which is the illustrated look; at one it
        // is a plain gradient.
        _RampBias("Ramp bias", Range(0.25, 4)) = 1.6

        // How hard the ramp steps. Zero is a smooth gradient; one is two flat bands with a hard
        // line between them, which is what the concept renders and the comic reference both do.
        // The Look setting drives it, so one shader serves both.
        _Banding("Banding", Range(0, 1)) = 0

        // Where the band falls, as a height along the blade.
        _BandHeight("Band height", Range(0, 1)) = 0.45

        // How far the tip leans towards the camera, as a fraction of the blade's length. A blade
        // is a thin upright card and a camera looking down sees its top edge; leaning it over
        // turns its face up towards the lens. This is the single setting that decides whether a
        // meadow reads as grass or as grey fuzz from the play camera, and it is the one idea
        // worth taking from the commercial stylised-grass shaders.
        _FaceCamera("Lean towards the camera", Range(0, 1)) = 0.3

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
            float _RampBias;
            float _Banding;
            float _BandHeight;
            float _FaceCamera;
            float _Metallic;
            float _Smoothness;
        CBUFFER_END

        // Globals, written once a frame by WindDirector. All-zero is a valid state and means
        // still air, so grass drawn before anything sets them — a contact sheet, a test harness,
        // the first frame — stands up straight instead of disappearing.
        //
        // xyz is the wind's horizontal direction times its strength in metres of tip travel per
        // metre of blade; w is the phase the director accumulates from the game clock rather than
        // from wall time, so a paused game holds the frame it is on.
        float4 _OdysseyWind;

        // How quickly the gust wave crosses the ground, in radians per metre. On the global so a
        // single number governs the whole board and two shaders could never disagree about how
        // large a gust is.
        float _OdysseyWindWavelength;

        /// The furthest any vertex may be moved. GrassMesh.MaxSway grows the mesh bounds by the
        /// same figure, and GrassMeshTests is what stops the two drifting apart.
        #define ODYSSEY_GRASS_MAX_SWAY 0.35

        // Bend a blade. positionWS is where the vertex would stand in still air; heightT is the
        // vertex colour's red; bladePhase is its green; reach is how far up the blade this vertex
        // is, in world metres, which already carries the instance's scale.
        float3 GrassDeform(float3 positionWS, float heightT, float bladePhase, float reach, float faceCamera)
        {
            float amount = reach * heightT;

            // One travelling wave over the ground plus a per-blade offset. Sampling world x and z
            // rather than the instance's origin is what makes a gust cross the field instead of
            // every clump breathing in time.
            float travel = (positionWS.x + positionWS.z) * _OdysseyWindWavelength;
            float gust = sin(_OdysseyWind.w + travel + bladePhase * 6.2831853);

            // Never fully slack: a blade that stops dead at the bottom of the wave reads as a
            // stutter. Two thirds of the bend is constant lean and a third is the wave.
            float3 offset = _OdysseyWind.xyz * (amount * (0.66 + 0.34 * gust));

            // The lean is horizontal and towards the camera, so it turns the blade's face up
            // without shortening it in plan any more than the wind already does.
            float3 toCamera = _WorldSpaceCameraPos.xyz - positionWS;
            toCamera.y = 0;
            // Named "span" rather than "distance", which is an HLSL intrinsic: shadowing one
            // is legal and compiles here, and is the kind of thing that stops compiling on
            // somebody else.
            float span = length(toCamera);
            offset += (span > 1e-4 ? toCamera / span : float3(0, 0, 0)) * (amount * faceCamera);

            float travelled = length(offset);
            if (travelled > ODYSSEY_GRASS_MAX_SWAY) offset *= ODYSSEY_GRASS_MAX_SWAY / travelled;
            return offset;
        }

        // How far up the blade a vertex is, in world metres. The object-to-world matrix's first
        // column is the scaled x axis, and a clump is only ever scaled uniformly by the mesher,
        // so its length is the instance's scale.
        float GrassReach(float positionOSy)
        {
            // Through the accessor rather than by indexing unity_ObjectToWorld: under
            // instancing that name is a macro that expands to an expression, and taking a
            // member of it is the sort of thing that compiles on one platform and not the
            // next. Transforming a unit x axis asks the same question and cannot be wrong.
            float scale = length(mul((float3x3)GetObjectToWorldMatrix(), float3(1, 0, 0)));
            return positionOSy * scale;
        }

        // Root to tip, either as a gradient or as flat bands. The bias is applied before the
        // banding so that moving the ramp does not also move the line the band falls on.
        float3 GrassAlbedo(float heightT)
        {
            float t = pow(saturate(heightT), _RampBias);
            float banded = step(_BandHeight, t);
            t = lerp(t, banded, _Banding);
            return lerp(_RootColour.rgb, _TipColour.rgb, t) * _BaseColor.rgb;
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
                half   heightT    : TEXCOORD2;
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += GrassDeform(positionWS, input.colour.r, input.colour.g,
                    GrassReach(input.positionOS.y), _FaceCamera);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.heightT = input.colour.r;
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
                surface.albedo = GrassAlbedo(input.heightT);
                surface.alpha = 1;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.emission = half3(0, 0, 0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // The wind, but not the camera lean: this pass runs from the light, where
                // _WorldSpaceCameraPos is the light's own position and leaning towards it would
                // bend the grass somewhere the picture never shows it.
                positionWS += GrassDeform(positionWS, input.colour.r, input.colour.g,
                    GrassReach(input.positionOS.y), 0);

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DepthAttributes
            {
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += GrassDeform(positionWS, input.colour.r, input.colour.g,
                    GrassReach(input.positionOS.y), _FaceCamera);
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += GrassDeform(positionWS, input.colour.r, input.colour.g,
                    GrassReach(input.positionOS.y), _FaceCamera);
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
