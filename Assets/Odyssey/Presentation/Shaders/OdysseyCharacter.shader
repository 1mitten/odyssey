// A colonist, with up to four regions of the pack atlas repainted per material.
//
// **Why a shader at all, when the packs already ship recoloured atlases.** Synty's own recolour
// mechanism is to swap the whole atlas for one of the twelve to twenty-four repainted alternates.
// That recolours *everything* on the character at once — skin with the shirt — and there are only
// ever as many looks as there are alternates. What was wanted is one colonist in a green coat
// standing beside another in a blue one, with different hair and different skin, and no atlas swap
// can express that.
//
// **Why replacing a colour rather than moving a UV.** Every garment, hair patch and skin region on
// a Synty body is UV-mapped onto a small *flat* cell of the atlas's "Character Colours" block, so
// the obvious trick is to shift the UV onto a neighbouring cell. Measuring first turned out to be
// worth it: `SwatchProbe` reports the deviation of every texel inside every cluster's rectangle,
// across eight bodies from all four packs, and it is **zero everywhere**. The cells really are
// flat. So sampling a different cell and outputting a different colour are the same picture, and
// outputting the colour is strictly better — it needs no second texture fetch, it cannot disturb
// mip selection or derivatives, and the palette stops being limited to the colours Synty happened
// to paint. If a future pack ships a *patterned* garment cell, that is the observation that would
// send this back to moving UVs; the probe is how to check.
//
// **Why hand-written HLSL.** It is the house idiom (`OdysseyWater`, `OdysseyOutline`,
// `OdysseyGradientSky`) and it keeps every property inside `UnityPerMaterial`, so the SRP Batcher
// still batches a colony of fifty materials as one shader variant. Nothing here is forked from
// `Synty/Generic_Basic`: the property *names* match so that a material cloned from a pack material
// keeps its maps, but the code is written from scratch, which is the rule the licence and
// `e-04-tint-strategy.md` both set.
//
// A material with no rects set draws exactly what the stock shader draws — every rect defaults to
// (1,1,0,0), which no UV can be inside. "Off" is a value, not a branch, so a body whose regions
// were never classified takes the same path through the shader as one that was.
Shader "Odyssey/Character"
{
    Properties
    {
        [MainTexture] _Albedo_Map("Albedo atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Base colour", Color) = (1, 1, 1, 1)

        [Normal] _Normal_Map("Normal map", 2D) = "bump" {}
        _Normal_Amount("Normal amount", Range(0, 2)) = 1

        _Emission_Map("Emission map", 2D) = "black" {}
        [HDR] _Emission_Color("Emission colour", Color) = (0, 0, 0, 0)
        _Enable_Emission("Enable emission", Float) = 0

        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.2
        _Alpha_Clip_Threshold("Alpha clip threshold", Range(0, 1)) = 0.5

        // Each slot is two rectangles, because a body's skin or clothing sometimes lands on two
        // cells rather than one (measured: the farmer's skin is two clusters, the bandit's hair is
        // two). Written as (minU, minV, maxU, maxV).
        _SkinRect0("Skin rect 0", Vector) = (1, 1, 0, 0)
        _SkinRect1("Skin rect 1", Vector) = (1, 1, 0, 0)
        _HairRect0("Hair rect 0", Vector) = (1, 1, 0, 0)
        _HairRect1("Hair rect 1", Vector) = (1, 1, 0, 0)
        _ClothRect0("Cloth rect 0", Vector) = (1, 1, 0, 0)
        _ClothRect1("Cloth rect 1", Vector) = (1, 1, 0, 0)
        _Cloth2Rect0("Cloth 2 rect 0", Vector) = (1, 1, 0, 0)
        _Cloth2Rect1("Cloth 2 rect 1", Vector) = (1, 1, 0, 0)

        _SkinColour("Skin colour", Color) = (1, 1, 1, 1)
        _HairColour("Hair colour", Color) = (1, 1, 1, 1)
        _ClothColour("Cloth colour", Color) = (1, 1, 1, 1)
        _Cloth2Colour("Cloth 2 colour", Color) = (1, 1, 1, 1)

        // 0 draws the art untouched, which is what the fidelity contact sheet compares against.
        _RemapStrength("Remap strength", Range(0, 1)) = 1

        [Header(Ink)]
        _InkColour("Ink colour", Color) = (0.06, 0.09, 0.08, 1)
        _InkWidth("Ink width in pixels", Float) = 2.2
        _InkOn("Draw the ink hull", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        // Everything the three passes share. Declared once so the forward pass and the shadow
        // pass cannot drift apart on the clip threshold, which is the classic way hair cards end
        // up casting solid quad shadows.
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
            float4 _SkinRect0;
            float4 _SkinRect1;
            float4 _HairRect0;
            float4 _HairRect1;
            float4 _ClothRect0;
            float4 _ClothRect1;
            float4 _Cloth2Rect0;
            float4 _Cloth2Rect1;
            float4 _SkinColour;
            float4 _HairColour;
            float4 _ClothColour;
            float4 _Cloth2Colour;
            float _RemapStrength;
            float4 _InkColour;
            float _InkWidth;
            float _InkOn;
        CBUFFER_END

        TEXTURE2D(_Albedo_Map);     SAMPLER(sampler_Albedo_Map);
        TEXTURE2D(_Normal_Map);     SAMPLER(sampler_Normal_Map);
        TEXTURE2D(_Emission_Map);   SAMPLER(sampler_Emission_Map);

        // A rectangle written (1,1,0,0) is empty and no UV is inside it, which is how a slot is
        // switched off without a branch or a count.
        float InsideRect(float2 uv, float4 r)
        {
            float2 inside = step(r.xy, uv) * step(uv, r.zw);
            return inside.x * inside.y;
        }

        float InsidePair(float2 uv, float4 a, float4 b)
        {
            return saturate(InsideRect(uv, a) + InsideRect(uv, b));
        }

        // The first slot a fragment is inside wins, in the order skin, hair, cloth, cloth2. For a
        // colonist that is no rule at all: the classifier guarantees the slots are disjoint, and
        // asserts it, so no fragment is ever inside two of them and the order cannot show. It is
        // for the bandit (docs/design/42-bandits.md §5), whose cloth2 is the whole atlas — "every
        // part of this body that is not skin, black" — lying behind the skin and the vest's red.
        float3 Repaint(float3 albedo, float2 uv)
        {
            float taken = 0;
            float w;
            w = InsidePair(uv, _SkinRect0, _SkinRect1);
            albedo = lerp(albedo, _SkinColour.rgb, _RemapStrength * w);
            taken = w;
            w = InsidePair(uv, _HairRect0, _HairRect1) * (1 - taken);
            albedo = lerp(albedo, _HairColour.rgb, _RemapStrength * w);
            taken = saturate(taken + w);
            w = InsidePair(uv, _ClothRect0, _ClothRect1) * (1 - taken);
            albedo = lerp(albedo, _ClothColour.rgb, _RemapStrength * w);
            taken = saturate(taken + w);
            w = InsidePair(uv, _Cloth2Rect0, _Cloth2Rect1) * (1 - taken);
            albedo = lerp(albedo, _Cloth2Colour.rgb, _RemapStrength * w);
            return albedo;
        }

        float4 SampleAlbedo(float2 uv)
        {
            float4 c = SAMPLE_TEXTURE2D(_Albedo_Map, sampler_Albedo_Map, uv) * _BaseColor;
            c.rgb = Repaint(c.rgb, uv);
            return c;
        }
        ENDHLSL

        // The ink line, drawn as an expanded back-face hull rather than read from the depth
        // texture like everything else in the world.
        //
        // **Why characters cannot use the screen-space pass.** OdysseyOutline inks depth
        // discontinuities in _CameraDepthTexture, and skinned meshes are not in it. That was
        // measured rather than guessed: a plain cube stood behind a colonist keeps its outline
        // running straight across the colonist, so the colonist never occludes the ink, while
        // occluding the cube perfectly well in colour. The renderer is Forward+, which builds its
        // depth from a prepass, and the figures do not reach it. Until that is fixed the world is
        // inked from depth and characters are inked here, and the two agree by their numbers --
        // _InkColour and _InkWidth are set from the feature, so there is one colour and one
        // thickness in the game rather than two.
        //
        // **Why a hull is affordable here and nowhere else.** OdysseyOutline rejects hull outlines
        // for world geometry, and rightly: a second draw of every object is the one thing a
        // renderer submitting tens of thousands of instanced modules must not do. A colony is
        // capped at 64 live figures, so this is at most 64 extra draws of a small mesh. The
        // argument that kills it for walls does not reach characters.
        Pass
        {
            Name "CharacterInk"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex InkVertex
            #pragma fragment InkFragment
            #pragma multi_compile_instancing
            #pragma target 3.5

            struct InkAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct InkVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            InkVaryings InkVertex(InkAttributes input)
            {
                InkVaryings output = (InkVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Widened in clip space, so the line is a constant number of *pixels* however far
                // away the colonist is, which is what the screen-space pass gives the rest of the
                // world. Expanding by a fixed number of metres instead would make a distant
                // colonist a black dot and a near one a hairline.
                float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
                float2 offset = normalize(normalVS.xy + 1e-6) * (_InkWidth * 2.0 / _ScreenParams.y);
                positionCS.xy += offset * positionCS.w * _InkOn;

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _Albedo_Map);
                return output;
            }

            half4 InkFragment(InkVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // The same cutout the lit pass uses, or the hull of a hair card is a solid slab.
                float alpha = SAMPLE_TEXTURE2D(_Albedo_Map, sampler_Albedo_Map, input.uv).a;
                clip(alpha - _Alpha_Clip_Threshold);
                clip(_InkOn - 0.5);
                return half4(_InkColour.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CharacterForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            // Without this the baked instanced path draws every colonist at the origin, and it
            // fails silently rather than magenta.
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

            // The same clip as the forward pass, and that is the point of it being here: a hair
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

        // In the normals prepass, which a colonist was never in (d-18 noted it). It matters now for
        // one reason: the ink line leaves a pixel alone when the normals texture says its visible
        // surface is terrain (design 38 §17c), and a colonist missing from the prepass left the
        // ground's mark under it — so the colonist would have lost its own outline. 0 in the spare
        // channel: a colonist is not terrain.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma target 3.5

            struct NormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
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

            NormalsVaryings NormalsVertex(NormalsAttributes input)
            {
                NormalsVaryings output = (NormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _Albedo_Map);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFragment(NormalsVaryings input) : SV_Target
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
