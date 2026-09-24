// Meadow foliage drawn our way: the pack's meshes and textures, this project's wind, clearing and
// fade (docs/design/38-meadow-overhaul.md §4, M3).
//
// **Why not the pack's own shader.** Three things the game needs are out of its reach. Its wind
// runs on `_Time`, so a paused world goes on waving and a world at speed 3 waves at speed 1; here
// the phase is the game tick, written by `WindDirector`, the same way the daylight is. It cannot
// be told where an item lies, so tall grass hides it; here every clump reads the clearance field
// (`GrassClearance`) at its root and shrinks away. And its materials ship with instancing off.
// Nothing of the pack's shader is used or copied: this reads its meshes' vertex colours as
// authored and its textures through material slots the cache fills at runtime.
//
// **What the vertex colour carries** (research e-09 §3, measured across every Meadow mesh):
// red is a height gradient, 0 at the root and about 0.5 at a crown or a metre-tall blade — the
// bend weight, doubled; green is a leaf-tip gradient — where a leaf or blade is free to flutter;
// blue is the leaf mask, 1 on leaves and blades and 0 on bark, which also picks the texture. A
// mesh with no vertex colour reads white, which would be all leaf and all tip, so such meshes are
// given `_WindResponse` 0 by the cache rather than being trusted to be still.
//
// **Every pass moves a vertex through `FoliageDisplace` and nothing else.** A depth or shadow pass
// that bent a clump differently from the colour pass would put the outline, the ambient occlusion
// or the shadow somewhere the picture does not show the grass.
Shader "Odyssey/Foliage"
{
    Properties
    {
        // The tint the material cache writes: the foliage palette entry times the slice's depth
        // shade, exactly as every other bucket in the world receives it.
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)

        // The leaf and trunk albedo. The leaf carries the cut-out in alpha.
        _LeafMap("Leaf", 2D) = "white" {}
        _TrunkMap("Trunk", 2D) = "white" {}

        // A linear multiplier on the leaf colour and the trunk colour. FoliageLook writes it; the
        // grass interview (2026-09-22) asked for a lighter, yellower spring green than the art.
        _LeafGrade("Leaf grade (linear)", Vector) = (1, 1, 1, 1)
        _TrunkGrade("Trunk grade (linear)", Vector) = (1, 1, 1, 1)

        _Cutoff("Alpha cutoff", Range(0, 1)) = 0.25

        // The art's own leaf colouring, read from its material at runtime by FoliageLook (never
        // copied into this repository): with _LeafFlat on, a leaf takes a flat colour — a base,
        // pushed towards a small-scale and a large-scale colour by world-position noise, so that
        // neighbouring plants differ — and keeps only its cut-out and a little shading from the
        // texture. That, not the texture, is where the Meadow screenshots' colour comes from.
        _LeafFlat("Leaf colour is flat", Float) = 0
        _LeafBase("Leaf base colour", Color) = (1, 1, 1, 1)
        _LeafNoise("Leaf noise colour", Color) = (1, 1, 1, 1)
        _LeafNoiseLarge("Leaf large noise colour", Color) = (1, 1, 1, 1)
        _LeafNoiseAmount("Leaf noise amount", Range(0, 1)) = 0
        _LeafNoiseScale("Leaf noise scale", Float) = 1
        _LeafBigNoiseAmount("Leaf large noise amount", Range(0, 1)) = 0
        _LeafBigNoiseScale("Leaf large noise scale", Float) = 1
        // A colour laid over the upward-facing leaves: the sunlit tops of a canopy.
        _Frost("Frosting", Float) = 0
        _FrostColour("Frosting colour", Color) = (1, 1, 1, 1)
        _TrunkBase("Trunk colour", Color) = (1, 1, 1, 1)

        // How far the normal is pulled towards straight up. A clump of cards lit by their own
        // normals shades as a dozen facets; pulled up it lights as one mass, which is how grass
        // reads from 60 m.
        _NormalUp("Normal towards up", Range(0, 1)) = 0.6

        // How much of the global wind this material takes, and how far a leaf tip flutters, in
        // metres. Zero on both is a plant that does not move.
        _WindResponse("Wind response", Range(0, 2)) = 1
        _Flutter("Flutter, metres", Range(0, 0.2)) = 0.03

        // Whether the clearance field applies. Grass yes; a tree's crown is not in the way of an
        // item lying under it.
        _Clearable("Clears round items", Float) = 1

        // Patch-to-patch colour drift from world position: two rotated sines, no texture fetch.
        _PatchVariation("Patch variation", Range(0, 0.5)) = 0.1

        // A dithered fade, 1 solid and 0 gone: the see-through a canopy needs over a colonist.
        // Screen-space ordered dither, so it stays opaque and sorted; no transparency queue.
        _Fade("Fade", Range(0, 1)) = 1

        // Shrink with distance towards the root, for the density falloff in M4. Both zero is off,
        // and that is how it ships in M3.
        _ShrinkStart("Shrink starts, metres", Float) = 0
        _ShrinkEnd("Shrink complete by, metres", Float) = 0

        _Smoothness("Smoothness", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_LeafMap);  SAMPLER(sampler_LeafMap);
        TEXTURE2D(_TrunkMap); SAMPLER(sampler_TrunkMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _LeafMap_ST;
            float4 _TrunkMap_ST;
            float4 _LeafGrade;
            float4 _TrunkGrade;
            float _Cutoff;
            float _LeafFlat;
            float4 _LeafBase;
            float4 _LeafNoise;
            float4 _LeafNoiseLarge;
            float _LeafNoiseAmount;
            float _LeafNoiseScale;
            float _LeafBigNoiseAmount;
            float _LeafBigNoiseScale;
            float _Frost;
            float4 _FrostColour;
            float4 _TrunkBase;
            float _NormalUp;
            float _WindResponse;
            float _Flutter;
            float _Clearable;
            float _PatchVariation;
            float _Fade;
            float _ShrinkStart;
            float _ShrinkEnd;
            float _Smoothness;
        CBUFFER_END

        // Globals from WindDirector. All-zero is still air, so foliage drawn before anything sets
        // them — a contact sheet, a test, the first frame — stands up straight. xyz is the wind's
        // horizontal direction times its strength, a bow angle in radians; w is the phase from the
        // game clock, not from wall time.
        float4 _OdysseyWind;
        float _OdysseyWindWavelength;

        // The clearance field from GrassClearance: xy the window's world corner, z one over its
        // width in metres, w whether there is a field at all.
        TEXTURE2D(_OdysseyClearTex);
        SAMPLER(sampler_OdysseyClearTex);
        float4 _OdysseyClear;

        #define ODYSSEY_FOLIAGE_MAX_BOW 0.6

        // Sampled at the clump's root, so a clump clears or stands as one thing.
        float FoliageClearanceAt(float3 rootWS)
        {
            if (_OdysseyClear.w < 0.5 || _Clearable < 0.5) return 0;
            float2 uv = (rootWS.xz - _OdysseyClear.xy) * _OdysseyClear.z;
            // Outside the window is uncleared, never clamped: a clamp would smear the edge texels
            // across the rest of the board.
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0;
            return SAMPLE_TEXTURE2D_LOD(_OdysseyClearTex, sampler_OdysseyClearTex, uv, 0).r;
        }

        // One while the distance shrink is off; otherwise one near the camera falling to zero.
        float FoliageDistanceScale(float3 rootWS)
        {
            if (_ShrinkEnd <= _ShrinkStart) return 1;
            float span = distance(_WorldSpaceCameraPos.xyz, rootWS);
            return 1 - saturate((span - _ShrinkStart) / (_ShrinkEnd - _ShrinkStart));
        }

        float3 RotateAbout(float3 v, float3 axis, float angle)
        {
            float s, c;
            sincos(angle, s, c);
            return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
        }

        // The whole of where a vertex goes, for every pass. Scaled towards the root first (the
        // clearance and the distance shrink — a clump at zero scale is a degenerate triangle the
        // rasteriser drops), then bowed about the root by the wind, then a leaf tip flutters
        // along its normal.
        float3 FoliageDisplace(float3 positionOS, float3 normalOS, float4 colour)
        {
            float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
            float scale = FoliageDistanceScale(rootWS);
            float3 positionWS = TransformObjectToWorld(positionOS * scale);

            // The clearing, asked where the blade is rather than where its clump is rooted: a
            // Meadow tall-grass mat is six metres across, and a log two metres off its root was
            // standing in grass the root-sampled clearing never touched (the look pass, design 38
            // §17). Blades near an item or a mark lie flat towards the ground, so the grass parts
            // round the thing instead of the whole clump vanishing.
            float clear = FoliageClearanceAt(positionWS);
            positionWS.y = lerp(positionWS.y, rootWS.y + 0.02, clear);

            float strength = length(_OdysseyWind.xyz) * _WindResponse;
            if (strength < 1e-5) return positionWS;

            // Two waves at unrelated rates and a slow swell, travelling across the ground from the
            // clump's own position, so the field moves as weather rather than in step.
            float travel = (rootWS.x + rootWS.z) * _OdysseyWindWavelength;
            float fast = sin(_OdysseyWind.w + travel);
            float slow = sin(_OdysseyWind.w * 0.43 + travel * 0.31 + 1.7);
            float gust = fast * 0.62 + slow * 0.38;
            float swell = 0.62 + 0.38 * sin(_OdysseyWind.w * 0.17 + travel * 0.11);

            float3 direction = _OdysseyWind.xyz / max(length(_OdysseyWind.xyz), 1e-5);
            float bend = saturate(2.0 * colour.r);
            // Squared, so the base stays planted and the bow happens up the stem.
            float angle = min(strength * swell * (0.66 + 0.34 * gust), ODYSSEY_FOLIAGE_MAX_BOW) * bend * bend;
            float3 axis = normalize(cross(float3(0, 1, 0), direction));
            positionWS = rootWS + RotateAbout(positionWS - rootWS, axis, angle);

            // Flutter: leaves only (blue), strongest at the free tip (green).
            float3 normalWS = TransformObjectToWorldNormal(normalOS);
            float shiver = sin(_OdysseyWind.w * 2.3 + travel * 3.1 + dot(positionWS.xz, float2(1.7, 2.3)));
            positionWS += normalWS * (shiver * _Flutter * colour.b * colour.g * saturate(strength / 0.45));
            return positionWS;
        }

        // Smooth value noise over world position, [0, 1]. Ours: a hash lattice and a smoothstep.
        float FoliageHash(float2 p)
        {
            p = frac(p * float2(0.1031, 0.1030));
            p += dot(p, p.yx + 33.33);
            return frac((p.x + p.y) * p.x);
        }

        float FoliageNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = FoliageHash(i);
            float b = FoliageHash(i + float2(1, 0));
            float c = FoliageHash(i + float2(0, 1));
            float d = FoliageHash(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // The flat leaf colour at a world position and a height up the plant: the base colour at
        // the root, rising to the noise colours at the tip (vertex red is the height gradient, 0 at
        // the root and about 0.5 at a crown), which from above is what shows — so a clump seen
        // from the play camera is its bright tips over a dark heart, as the reference is. The noise
        // mixes towards the small noise colour, then towards the large one. The scales are the art's own noise frequencies; the metres they
        // map to are ours (a grass frequency of 12 varies over about a metre and a half, a tree's
        // 4 over a crown; a large frequency of 0.5 over a hundred metres of meadow).
        half3 FoliageLeafColour(float3 positionWS, float height)
        {
            float small = FoliageNoise(positionWS.xz * (_LeafNoiseScale * 0.05));
            float large = FoliageNoise(positionWS.xz * (_LeafBigNoiseScale * 0.02) + 17.0);
            half3 tip = lerp(_LeafNoise.rgb, _LeafNoiseLarge.rgb, saturate(large * _LeafBigNoiseAmount * 1.5));
            tip = lerp(tip, _LeafNoise.rgb, saturate(small * _LeafNoiseAmount) * 0.5);
            return lerp(_LeafBase.rgb, tip, saturate(height * 4.0));
        }

        // Leaf or trunk by the leaf mask; alpha is the cut-out.
        half4 FoliageSample(float2 uv, float leafMask, float3 positionWS, float height)
        {
            if (leafMask > 0.5)
            {
                half4 leaf = SAMPLE_TEXTURE2D(_LeafMap, sampler_LeafMap, TRANSFORM_TEX(uv, _LeafMap));
                if (_LeafFlat > 0.5)
                {
                    // Flat: the art's colour scheme, with a little of the texture's own light and
                    // dark kept so a clump is not a silhouette.
                    half luma = dot(leaf.rgb, half3(0.299, 0.587, 0.114));
                    half3 flat = FoliageLeafColour(positionWS, height) * lerp(1.0, saturate(luma * 3.0), 0.35);
                    return half4(flat * _LeafGrade.rgb, leaf.a);
                }
                return half4(leaf.rgb * _LeafGrade.rgb, leaf.a);
            }
            half4 trunk = SAMPLE_TEXTURE2D(_TrunkMap, sampler_TrunkMap, TRANSFORM_TEX(uv, _TrunkMap));
            return half4(trunk.rgb * _TrunkBase.rgb * _TrunkGrade.rgb, trunk.a);
        }

        // A 4x4 ordered dither, for the fade. Opaque and depth-writing throughout, so a faded
        // canopy still sorts and still occludes where it is drawn.
        float FoliageDither(float4 positionCS)
        {
            const float4x4 bayer = float4x4(
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5);
            uint2 p = uint2(positionCS.xy) % 4;
            return (bayer[p.y][p.x] + 0.5) / 16.0;
        }

        void FoliageClip(half alpha, float4 positionCS)
        {
            clip(alpha - _Cutoff);
            if (_Fade < 0.999) clip(_Fade - FoliageDither(positionCS));
        }

        // Patch drift from the root, so a whole clump moves together.
        float FoliagePatch(float3 rootWS)
        {
            float a = sin(dot(rootWS.xz, float2(0.0731, 0.0517)) + 1.3);
            float b = sin(dot(rootWS.xz, float2(-0.0413, 0.0629)) + 4.1);
            return saturate((a * 0.5 + b * 0.5) * 0.5 + 0.5);
        }
        ENDHLSL

        Pass
        {
            Name "FoliageForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            // Both sides: a card seen from behind is still grass.
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            // Without this the instanced path draws every clump at the origin, silently.
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
                float4 colour     : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                // x the leaf mask, y the clump's patch drift, z the height gradient.
                half3  leaf       : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                half3  vertexSH   : TEXCOORD5;
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
                float3 positionWS = FoliageDisplace(input.positionOS.xyz, input.normalOS, input.colour);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv = input.uv;
                output.leaf = half3(input.colour.b, FoliagePatch(rootWS), input.colour.r);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.vertexSH = SampleSH(normalize(lerp(normalWS, float3(0, 1, 0), _NormalUp)));
                return output;
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 art = FoliageSample(input.uv, input.leaf.x, input.positionWS, input.leaf.z);
                FoliageClip(art.a, input.positionCS);

                // A card lit from behind would go black; its back face takes the flipped normal,
                // then both are pulled towards up so the clump lights as one mass.
                float3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                normalWS = normalize(lerp(normalWS, float3(0, 1, 0), _NormalUp));

                float patch = input.leaf.y;
                float drift = lerp(1.0 - _PatchVariation, 1.0 + _PatchVariation, patch);
                float3 hue = float3(1.0 + (patch - 0.5) * _PatchVariation, 1.0,
                                    1.0 - (patch - 0.5) * _PatchVariation);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = art.rgb * _BaseColor.rgb * drift * hue;
                // The sunlit tops of a canopy: leaves facing up take the art's frosting colour.
                if (_Frost > 0.5 && input.leaf.x > 0.5)
                    surface.albedo = lerp(surface.albedo, _FrostColour.rgb * _BaseColor.rgb,
                                          saturate(normalWS.y * 1.6 - 0.6) * 0.7);
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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   leafMask   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = FoliageDisplace(input.positionOS.xyz, input.normalOS, input.colour);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = input.uv;
                output.leafMask = input.colour.b;
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageClip(FoliageSample(input.uv, input.leafMask, float3(0, 0, 0), 1.0).a, input.positionCS);
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
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 colour     : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   leafMask   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformWorldToHClip(
                    FoliageDisplace(input.positionOS.xyz, input.normalOS, input.colour));
                output.uv = input.uv;
                output.leafMask = input.colour.b;
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageClip(FoliageSample(input.uv, input.leafMask, float3(0, 0, 0), 1.0).a, input.positionCS);
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals is not optional: the renderer runs the prepass every frame for SSAO, and a
        // tree missing from it is a hole in the occlusion. Grass is drawn in queue 2501, outside
        // the opaque prepass, so it never reaches this pass (design 38 §13) — trees will (M5).
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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                half   leafMask   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            NormalsVaryings DepthNormalsVertex(NormalsAttributes input)
            {
                NormalsVaryings output = (NormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformWorldToHClip(
                    FoliageDisplace(input.positionOS.xyz, input.normalOS, input.colour));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.leafMask = input.colour.b;
                return output;
            }

            half4 DepthNormalsFragment(NormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageClip(FoliageSample(input.uv, input.leafMask, float3(0, 0, 0), 1.0).a, input.positionCS);
                float3 normalWS = normalize(lerp(normalize(input.normalWS), float3(0, 1, 0), _NormalUp));
                return half4(normalWS, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
