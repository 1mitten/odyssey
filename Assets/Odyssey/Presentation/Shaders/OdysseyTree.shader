// A tree, with its bark and its canopy repainted separately from the pack atlas.
//
// **Why a shader and not a tint.** Every other module in the world is coloured by multiplying
// `_BaseColor` over whatever the art already says, and that is the committed strategy
// (`e-04-tint-strategy.md`). It cannot express a tree. A Synty tree is one mesh with one material
// and one 4096 x 4096 atlas, and the trunk and the leaves are two different flat cells of it, so a
// single multiply moves both: brown the bark and the leaves go brown with it. That is exactly the
// conflation `ChunkMesher.EmitEdifice` already refuses to make with the wood stuff tint.
//
// **Why repainting a cell is legitimate here.** The same claim `SwatchProbe` established for
// character bodies — that a Synty region is UV-mapped onto a *flat* cell, so replacing the colour
// inside a rectangle throws no art away — has to hold for trees too, and it is measured rather
// than assumed: `TreeSwatchProbe` reports the deviation of every texel inside every cluster on
// both tree meshes. If a canopy had come back carrying a gradient or a leaf pattern this would be
// the wrong mechanism and the answer would have to be a multiply after all.
//
// **Why not recolour the atlas instead.** Because it is 4096 x 4096: one repainted copy is 64 MB
// uncompressed, and this feature wants about a dozen. Measured, not estimated — the file is
// `PolygonGeneric/Textures/Alts/Generic_01_A.png`. Repainting in the fragment shader costs four
// rectangle tests and no memory at all.
//
// **Its relationship to `Odyssey/Character`.** This is that shader with the slots renamed and the
// ink hull removed, and the two are deliberately *not* merged. The hull exists because skinned
// meshes are missing from the depth texture `OdysseyOutline` reads; a tree is ordinary instanced
// geometry that the screen-space pass inks perfectly well, and giving it a second, closer line of
// its own would ink every tree twice. The shared half is thirty lines of rectangle arithmetic, and
// an include file holding it would have to fix one set of property names for both — which would
// mean renaming the character's, in a feature the owner has already judged.
//
// A material with no rects set draws what the stock shader draws: every rect defaults to
// (1,1,0,0), which no UV can be inside. "Off" is a value, not a branch.
Shader "Odyssey/Tree"
{
    Properties
    {
        [MainTexture] _Albedo_Map("Albedo atlas", 2D) = "white" {}

        // The depth shade rides here, exactly as it does for every other bucket in the world:
        // ChunkRenderer resolves a shade per layer and it multiplies the finished albedo. It is
        // applied *after* the repaint, unlike the character shader, because a shaded tree must be
        // a darker tree and not a tree whose repainted parts ignored the slice.
        [MainColor] _BaseColor("Base colour", Color) = (1, 1, 1, 1)

        [Normal] _Normal_Map("Normal map", 2D) = "bump" {}
        _Normal_Amount("Normal amount", Range(0, 2)) = 1

        // Carried from the pack material rather than dropped, and it is not decoration: a tree
        // drawn without it came out a uniform 13% darker than the same tree drawn by
        // Synty/Generic_Basic, measured off TreeCheck's fidelity column. A shader that is "the
        // same but a bit darker" is the kind of difference that gets blamed on the palette.
        _Emission_Map("Emission map", 2D) = "black" {}
        [HDR] _Emission_Color("Emission colour", Color) = (0, 0, 0, 0)
        _Enable_Emission("Enable emission", Float) = 0

        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.2
        _Alpha_Clip_Threshold("Alpha clip threshold", Range(0, 1)) = 0.5

        // Two rectangles per slot, because a mesh sometimes paints one region from two
        // neighbouring cells. Written as (minU, minV, maxU, maxV).
        _BarkDeepRect0("Bark deep rect 0", Vector) = (1, 1, 0, 0)
        _BarkDeepRect1("Bark deep rect 1", Vector) = (1, 1, 0, 0)
        _BarkWarmRect0("Bark warm rect 0", Vector) = (1, 1, 0, 0)
        _BarkWarmRect1("Bark warm rect 1", Vector) = (1, 1, 0, 0)
        _LeafDeepRect0("Leaf deep rect 0", Vector) = (1, 1, 0, 0)
        _LeafDeepRect1("Leaf deep rect 1", Vector) = (1, 1, 0, 0)
        _LeafFreshRect0("Leaf fresh rect 0", Vector) = (1, 1, 0, 0)
        _LeafFreshRect1("Leaf fresh rect 1", Vector) = (1, 1, 0, 0)

        // Per *instance*, not per material — see the instancing buffer below. The values here are
        // the fallback a non-instanced draw takes, and a material carries its species' first theme
        // in them so that such a draw still looks like a tree.
        _BarkDeepColour("Bark deep colour", Color) = (1, 1, 1, 1)
        _BarkWarmColour("Bark warm colour", Color) = (1, 1, 1, 1)
        _LeafDeepColour("Leaf deep colour", Color) = (1, 1, 1, 1)
        _LeafFreshColour("Leaf fresh colour", Color) = (1, 1, 1, 1)

        // 0 draws the art untouched, which is what a fidelity contact sheet compares against.
        _RemapStrength("Remap strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        // Declared once so the forward, shadow and depth passes cannot drift apart on the clip
        // threshold, which is the classic way leaf cards end up casting solid quad shadows.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Albedo_Map_ST;
            float4 _BaseColor;
            float _Normal_Amount;
            float4 _Emission_Color;
            float _Enable_Emission;
            float _Metallic;
            float _Smoothness;
            float _Alpha_Clip_Threshold;
            float4 _BarkDeepRect0;
            float4 _BarkDeepRect1;
            float4 _BarkWarmRect0;
            float4 _BarkWarmRect1;
            float4 _LeafDeepRect0;
            float4 _LeafDeepRect1;
            float4 _LeafFreshRect0;
            float4 _LeafFreshRect1;
            float _RemapStrength;
        CBUFFER_END

        // **The four colours are per instance, and that is the whole performance story.**
        //
        // A bucket is one instanced draw of one (module, part, tint), so while the colour lived on
        // the material it *was* a bucket key: every extra colour standing in a chunk cost a draw
        // call, and a mixed wood cost 384 of them on the played board. Moving the colours here
        // makes every tree in a chunk one draw again — the tint code carries only which species it
        // is, the colours ride beside the matrices, and the palette can be any size at all for
        // nothing.
        //
        // Written and read through UNITY_ACCESS_INSTANCED_PROP so that a draw *without* instancing
        // still works: outside an instanced draw these resolve to the plain uniforms declared by
        // the Properties block above, which a material fills with its species' first theme.
        UNITY_INSTANCING_BUFFER_START(TreeProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BarkDeepColour)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BarkWarmColour)
            UNITY_DEFINE_INSTANCED_PROP(float4, _LeafDeepColour)
            UNITY_DEFINE_INSTANCED_PROP(float4, _LeafFreshColour)
        UNITY_INSTANCING_BUFFER_END(TreeProps)

        TEXTURE2D(_Albedo_Map);     SAMPLER(sampler_Albedo_Map);
        TEXTURE2D(_Normal_Map);     SAMPLER(sampler_Normal_Map);
        TEXTURE2D(_Emission_Map);   SAMPLER(sampler_Emission_Map);

        float InsideRect(float2 uv, float4 r)
        {
            float2 inside = step(r.xy, uv) * step(uv, r.zw);
            return inside.x * inside.y;
        }

        float InsidePair(float2 uv, float4 a, float4 b)
        {
            return saturate(InsideRect(uv, a) + InsideRect(uv, b));
        }

        // The order of the four is irrelevant: the probe guarantees the slots are disjoint, and
        // TreeSwatches asserts it, so no fragment is ever inside two of them.
        // Reads the instanced colours, so every caller must have run UNITY_SETUP_INSTANCE_ID first.
        // Only the forward pass repaints; the shadow and depth passes sample alpha alone.
        float3 Repaint(float3 albedo, float2 uv)
        {
            float4 barkDeep  = UNITY_ACCESS_INSTANCED_PROP(TreeProps, _BarkDeepColour);
            float4 barkWarm  = UNITY_ACCESS_INSTANCED_PROP(TreeProps, _BarkWarmColour);
            float4 leafDeep  = UNITY_ACCESS_INSTANCED_PROP(TreeProps, _LeafDeepColour);
            float4 leafFresh = UNITY_ACCESS_INSTANCED_PROP(TreeProps, _LeafFreshColour);

            albedo = lerp(albedo, barkDeep.rgb,  _RemapStrength * InsidePair(uv, _BarkDeepRect0, _BarkDeepRect1));
            albedo = lerp(albedo, barkWarm.rgb,  _RemapStrength * InsidePair(uv, _BarkWarmRect0, _BarkWarmRect1));
            albedo = lerp(albedo, leafDeep.rgb,  _RemapStrength * InsidePair(uv, _LeafDeepRect0, _LeafDeepRect1));
            albedo = lerp(albedo, leafFresh.rgb, _RemapStrength * InsidePair(uv, _LeafFreshRect0, _LeafFreshRect1));
            return albedo;
        }

        float4 SampleAlbedo(float2 uv)
        {
            float4 c = SAMPLE_TEXTURE2D(_Albedo_Map, sampler_Albedo_Map, uv);
            c.rgb = Repaint(c.rgb, uv) * _BaseColor.rgb;
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "TreeForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            // Without this the instanced path draws every tree at the origin, and it fails
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
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
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
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _Albedo_Map);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 albedo = SampleAlbedo(input.uv);
                clip(albedo.a - _Alpha_Clip_Threshold);

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_Normal_Map, sampler_Normal_Map, input.uv), _Normal_Amount);

                float sign = input.tangentWS.w;
                float3 bitangent = sign * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = 1;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = normalTS;
                surface.occlusion = 1;
                surface.emission = _Enable_Emission > 0.5
                    ? SAMPLE_TEXTURE2D(_Emission_Map, sampler_Emission_Map, input.uv).rgb * _Emission_Color.rgb
                    : half3(0, 0, 0);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
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
            Cull Back

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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
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
                output.uv = TRANSFORM_TEX(input.uv, _Albedo_Map);
                return output;
            }

            // The same clip the forward pass uses, and that is the point of it being here: a leaf
            // card whose shadow pass skipped the cutout casts the shadow of a solid rectangle.
            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float alpha = SAMPLE_TEXTURE2D(_Albedo_Map, sampler_Albedo_Map, input.uv).a;
                clip(alpha - _Alpha_Clip_Threshold);
                return 0;
            }
            ENDHLSL
        }

        // Trees must stay in the depth texture: it is what the screen-space outline pass inks
        // from, and a tree that dropped out of it would lose the black line every other solid
        // thing in the world has.
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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _Albedo_Map);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float alpha = SAMPLE_TEXTURE2D(_Albedo_Map, sampler_Albedo_Map, input.uv).a;
                clip(alpha - _Alpha_Clip_Threshold);
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals as well, because the outline feature reads normals where it has them and a
        // tree missing from that buffer inks differently from the wall beside it.
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct NormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            NormalsVaryings DepthNormalsVertex(NormalsAttributes input)
            {
                NormalsVaryings output = (NormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _Albedo_Map);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(NormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float alpha = SAMPLE_TEXTURE2D(_Albedo_Map, sampler_Albedo_Map, input.uv).a;
                clip(alpha - _Alpha_Clip_Threshold);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
