// The meadow floor: several of the Meadow Forest terrain textures, blended by patches in world
// space so the ground reads as painted rather than as a grid of stamps
// (docs/design/38-meadow-overhaul.md §17, the look pass).
//
// **Why world space and not the cell's own UV.** Today's ground is one instanced box per cell with
// one unit of UV on each face, so a texture tiles once per 2.5 m cell and every cell shows the same
// patch of it — at the play camera that is a visible lattice. Projecting from the world position
// instead lets a texture repeat at its own scale (4 m, the pack's own terrain-layer tiling) and lets
// every layer be laid over the board in patches tens of metres across, which is what a painted
// Unity terrain gives Synty's reference shots. Neighbouring cells are continuous by construction:
// they sample the same world.
//
// **Why noise and not a splat map.** The simulation has no notion of clover or flowers, and none is
// wanted — this is drawing. A value-noise field is a pure function of position: no texture to
// build, nothing to re-mesh, nothing in a save or the hash, and identical on every machine.
//
// **Why the coarser layers branch.** Most of a meadow is plain grass, so the clover, flower and
// litter layers are only sampled where their weight is above zero. The gradients are taken once,
// outside the branches, because a texture sampled inside a divergent branch has no derivatives to
// choose a mip with (d-17 §4).
//
// Vertical faces — the riser of a terrace step, the side of a dug cell — take the earth texture
// projected on the face, so a bank reads as soil under turf rather than as grass on a wall. Slopes
// up to a bank's 50° stay grass.
//
// The tint the renderer resolves (depth shade, tilled, stored, the zone washes) multiplies the
// finished albedo through _BaseColor, exactly as it did the stock material this replaces.
//
// **Every natural terrain is drawn by this shader, not only grass** (the owner's first look,
// design 38 §17c). With _Single on it is one texture — earth, gravel, mud, sand, rock — projected
// from world position like the grass, and it exists for one reason besides the lattice: the
// DepthNormals pass marks the pixel as terrain in the normals texture's spare channel, and the ink
// line reads that mark and leaves terrain alone (OdysseyOutline.shader). The pack's own terrain
// materials cannot write the mark, so terrain that stayed on them kept its ink.
Shader "Odyssey/MeadowGround"
{
    Properties
    {
        [MainColor] _BaseColor("Base colour", Color) = (1, 1, 1, 1)

        _GrassA("Grass A", 2D) = "white" {}
        _GrassB("Grass B", 2D) = "white" {}
        _Clover("Clover", 2D) = "white" {}
        _Flowers("Flowers", 2D) = "white" {}
        _Leaves("Leaf litter", 2D) = "white" {}
        _Earth("Earth", 2D) = "white" {}

        // Metres one repeat of a grass texture covers. The pack's own terrain layers use 4.
        _TileMetres("Tile (m)", Float) = 4
        // Metres across a patch of the second grass; clover, flowers and litter scale from it.
        _PatchMetres("Patch (m)", Float) = 26
        _CloverAmount("Clover", Range(0, 1)) = 0.7
        _FlowerAmount("Flowers", Range(0, 1)) = 0.5
        _LeafAmount("Leaf litter", Range(0, 1)) = 0.18
        // A slow drift of brightness and warmth across the whole board, the painted variation.
        _MacroMetres("Macro (m)", Float) = 70
        _MacroStrength("Macro strength", Range(0, 0.5)) = 0.14
        _Warmth("Warm patches", Range(0, 1)) = 0.45
        _Smoothness("Smoothness", Range(0, 1)) = 0.05

        // One texture rather than the meadow's blend: _GrassA on the tops, _Earth on the faces.
        // How earth, gravel, sand and rock are drawn, so that they carry the terrain mark too.
        _Single("Single texture", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _GrassA_ST;
            float _TileMetres;
            float _PatchMetres;
            float _CloverAmount;
            float _FlowerAmount;
            float _LeafAmount;
            float _MacroMetres;
            float _MacroStrength;
            float _Warmth;
            float _Smoothness;
            float _Single;
        CBUFFER_END

        // What the DepthNormals pass writes into the normals texture's alpha: this pixel's visible
        // surface is terrain, so the ink line leaves it alone. Everything else writes 0 there — the
        // URP and Shader Graph passes do, and so do ours. Read by OdysseyOutline.shader.
        #define ODYSSEY_TERRAIN_MARK 1.0

        TEXTURE2D(_GrassA);  SAMPLER(sampler_GrassA);
        TEXTURE2D(_GrassB);
        TEXTURE2D(_Clover);
        TEXTURE2D(_Flowers);
        TEXTURE2D(_Leaves);
        TEXTURE2D(_Earth);

        float MeadowHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float MeadowNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = MeadowHash(i);
            float b = MeadowHash(i + float2(1, 0));
            float c = MeadowHash(i + float2(0, 1));
            float d = MeadowHash(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // Two octaves: enough to break a blob into a patch, cheap enough to take four times.
        float MeadowField(float2 p)
        {
            return MeadowNoise(p) * 0.65 + MeadowNoise(p * 2.13 + 17.1) * 0.35;
        }

        float2 Turn(float2 p, float angle)
        {
            float s = sin(angle), c = cos(angle);
            return float2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        float3 Sample(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 dx, float2 dy)
        {
            return SAMPLE_TEXTURE2D_GRAD(tex, samp, uv, dx, dy).rgb;
        }

        // The painted albedo at a world position with a world normal, before any tint.
        float3 MeadowAlbedo(float3 positionWS, float3 normalWS)
        {
            float2 xz = positionWS.xz;
            float tile = max(_TileMetres, 0.01);
            float patch = max(_PatchMetres, 0.01);

            // Each layer at its own scale and angle, so no two repeat on the same lattice.
            float2 uvA = xz / tile;
            float2 uvB = Turn(xz, 0.61) / (tile * 1.37);
            float2 uvC = Turn(xz, 1.93) / (tile * 0.83);
            float2 uvF = Turn(xz, 2.71) / (tile * 0.71);
            float2 uvL = Turn(xz, 4.02) / (tile * 1.11);
            float2 dxA = ddx(uvA), dyA = ddy(uvA);
            float2 dxB = ddx(uvB), dyB = ddy(uvB);
            float2 dxC = ddx(uvC), dyC = ddy(uvC);
            float2 dxF = ddx(uvF), dyF = ddy(uvF);
            float2 dxL = ddx(uvL), dyL = ddy(uvL);

            float3 grass = Sample(TEXTURE2D_ARGS(_GrassA, sampler_GrassA), uvA, dxA, dyA);

            // One texture, with only the slow drift over it: earth, gravel, sand, rock.
            [branch] if (_Single > 0.5)
            {
                float drift = MeadowField(xz / max(_MacroMetres, 0.01) + 5.3);
                grass *= lerp(1.0 - 0.5 * _MacroStrength, 1.0 + _MacroStrength, drift);
            }
            else
            {
            float wB = smoothstep(0.35, 0.65, MeadowField(xz / patch));
            [branch] if (wB > 0.001)
                grass = lerp(grass, Sample(TEXTURE2D_ARGS(_GrassB, sampler_GrassA), uvB, dxB, dyB), wB);

            float wC = smoothstep(0.50, 0.66, MeadowField(xz / (patch * 0.55) + 31.7)) * _CloverAmount;
            [branch] if (wC > 0.001)
                grass = lerp(grass, Sample(TEXTURE2D_ARGS(_Clover, sampler_GrassA), uvC, dxC, dyC), wC);

            float wF = smoothstep(0.63, 0.75, MeadowField(xz / (patch * 0.42) + 71.3)) * _FlowerAmount;
            [branch] if (wF > 0.001)
                grass = lerp(grass, Sample(TEXTURE2D_ARGS(_Flowers, sampler_GrassA), uvF, dxF, dyF), wF);

            float wL = smoothstep(0.70, 0.82, MeadowField(xz / (patch * 0.8) + 11.9)) * _LeafAmount;
            [branch] if (wL > 0.001)
                grass = lerp(grass, Sample(TEXTURE2D_ARGS(_Leaves, sampler_GrassA), uvL, dxL, dyL), wL);

            // The painted drift: brighter and darker, and warmer in places, over tens of metres.
            float macro = MeadowField(xz / max(_MacroMetres, 0.01) + 5.3);
            // Biased up: the drift lifts more than it darkens, or a patch reads as shade.
            grass *= lerp(1.0 - 0.5 * _MacroStrength, 1.0 + _MacroStrength, macro);
            float warm = smoothstep(0.55, 0.80, MeadowField(xz / 45.0 + 2.2)) * _Warmth;
            grass = lerp(grass, grass * float3(1.08, 1.03, 0.82), warm);
            }

            // Faces that stand up take the earth texture on their own plane. The gradients are
            // taken out here with the others, for the same reason.
            float3 n = normalize(normalWS);
            float top = smoothstep(0.30, 0.55, n.y);
            float2 sideUV = (abs(n.x) > abs(n.z) ? positionWS.zy : positionWS.xy) / tile;
            float2 dxS = ddx(sideUV), dyS = ddy(sideUV);
            [branch] if (top < 0.999)
            {
                float3 side = Sample(TEXTURE2D_ARGS(_Earth, sampler_GrassA), sideUV, dxS, dyS);
                grass = lerp(side, grass, top);
            }
            return grass;
        }
        ENDHLSL

        Pass
        {
            Name "MeadowGroundForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                half3  vertexSH   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                output.vertexSH = SampleSH(output.normalWS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = MeadowAlbedo(input.positionWS, normalWS) * _BaseColor.rgb;
                surface.alpha = 1;
                surface.metallic = 0;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = input.vertexSH;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 colour = UniversalFragmentPBR(inputData, surface);
                colour.rgb = MixFog(colour.rgb, inputData.fogCoord);
                colour.a = 1;
                return colour;
            }
            ENDHLSL
        }

        // In the depth texture, because the outline pass inks from it and the fog and SSAO read it.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma target 3.5

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma target 3.5

            struct NormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(NormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // The terrain mark in the spare channel: see ODYSSEY_TERRAIN_MARK.
                return half4(NormalizeNormalPerPixel(input.normalWS), ODYSSEY_TERRAIN_MARK);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
