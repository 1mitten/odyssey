// The ambient birds (design 50 §3): a flat-coloured bird built in code, flapped here.
//
// Why the flap is the vertex shader's. Each vertex carries a wing weight in uv.x — 0 at the
// shoulder, 0.45 at the elbow, 1 at the tip (Odyssey.Hud.BirdShape) — and is lifted by
// sin(clock × 2π × beats + phase) × amplitude × weight^1.4 × flap, so the outer panel travels
// further than the inner one and the wing bends at the elbow. A perched bird's fold pulls its
// wings in along the body. Nothing is skinned and nothing is animated on the CPU, so a species is
// one instanced call whatever it is doing.
//
// Why every pass calls BirdPose. The shadow, the depth texture (which the outline inks from) and
// the colour must agree on where the wing is, or a bird casts the shadow of a wing held somewhere
// else and the outline draws round it.
//
// Why flat shading from derivatives. The mesh's normals are the resting bird's; a flapped wing is
// bent, and a face lit by its resting normal would not darken as it tilts away from the sun. The
// screen-space derivative of the world position is the face as drawn.
//
// The clock is game time, wrapped every hour (3,600 s is a whole number of beats for both species,
// so the wrap is seamless): a paused game holds every wing where it is.
Shader "Odyssey/Bird"
{
    Properties
    {
        _BirdBeats ("Wingbeats a second", Float) = 3
        _BirdAmplitude ("Wingtip travel, share of span", Float) = 0.28
        _BirdClock ("Clock, game seconds", Float) = 0
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
            float _BirdBeats;
            float _BirdAmplitude;
            float _BirdClock;
        CBUFFER_END

        // Per bird: x phase, y flap (0 gliding to 1 beating), z fold (0 flying to 1 perched).
        UNITY_INSTANCING_BUFFER_START(BirdProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BirdState)
        UNITY_INSTANCING_BUFFER_END(BirdProps)

        // The bird as it is this instant, in object space. Needs UNITY_SETUP_INSTANCE_ID first.
        float3 BirdPose(float3 p, float weight)
        {
            float4 state = UNITY_ACCESS_INSTANCED_PROP(BirdProps, _BirdState);
            float w = pow(max(weight, 0.0), 1.4);
            float beat = sin(_BirdClock * 6.2831853 * _BirdBeats + state.x);
            float open = 1.0 - saturate(state.z);
            p.y += beat * _BirdAmplitude * w * state.y * open;
            p.z -= abs(beat) * 0.06 * w * state.y * open;

            // Folded: the wing drawn in along the body, swept back and a touch raised.
            float fold = saturate(state.z);
            float wing = step(0.001, weight);
            p.x = lerp(p.x, p.x * 0.28, fold * wing);
            p.z = lerp(p.z, p.z - 0.14 * weight, fold);
            p.y = lerp(p.y, p.y + 0.02 * weight, fold);
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "BirdForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            // Without this the instanced path draws every bird at the origin, silently.
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  color      : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(BirdPose(input.positionOS.xyz, input.uv.x));
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.color = input.color.rgb;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // The face as drawn, turned towards the eye: a bird is two-sided and the sky's
                // ambient must come from above a wing whichever side of it is showing.
                float3 normal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                if (dot(normal, view) < 0) normal = -normal;

                Light sun = GetMainLight();
                // A little wrap, so a wing edge-on to the sun is not black against the sky.
                half direct = saturate(dot(normal, sun.direction) * 0.8 + 0.2);
                half3 light = SampleSH(normal) + sun.color * direct;
                half3 colour = input.color * light;
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
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
                float2 uv         : TEXCOORD0;
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

                float3 positionWS = TransformObjectToWorld(BirdPose(input.positionOS.xyz, input.uv.x));
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
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // In the depth texture, which is what the outline inks from: a bird out of it would be the
        // one solid thing in the world without the line.
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
                float2 uv         : TEXCOORD0;
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
                output.positionCS = TransformObjectToHClip(BirdPose(input.positionOS.xyz, input.uv.x));
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
                float2 uv         : TEXCOORD0;
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
                output.positionCS = TransformObjectToHClip(BirdPose(input.positionOS.xyz, input.uv.x));
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
}
