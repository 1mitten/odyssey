// A painted gradient sky: three colours and two falloffs, and nothing else.
//
// Why not Unity's procedural skybox. That one models atmospheric scattering to look photographic,
// which is the opposite of what the reference art does — there the sky is a flat wash of colour
// that gets paler towards the horizon, with no sun disc and no haze band. A procedural sky also
// gives you a sun position you then have to keep in step with the directional light, which is a
// second thing to get wrong for a game that looks down at a board and never shows the horizon
// except at the very edge of the board.
//
// Why a sky at all, when the camera looks down. Two reasons, both about the edges: the board is
// finite, so past its rim the screen was a flat dark blue-grey that read as "nothing rendered
// here" rather than as distance; and fog now has something to fade into, which is what makes the
// far corner of a 300 m board recede instead of just going grey.
Shader "Odyssey/GradientSky"
{
    Properties
    {
        _SkyColour ("Overhead", Color) = (0.36, 0.60, 0.86, 1)
        _HorizonColour ("Horizon", Color) = (0.76, 0.86, 0.91, 1)
        _GroundColour ("Below horizon", Color) = (0.34, 0.37, 0.39, 1)
        _HorizonFalloff ("Horizon falloff", Range(0.2, 8)) = 2.2
        _GroundFalloff ("Ground falloff", Range(0.2, 8)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "OdysseyGradientSky"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SkyColour;
            float4 _HorizonColour;
            float4 _GroundColour;
            float _HorizonFalloff;
            float _GroundFalloff;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // The skybox mesh is drawn around the camera, so a vertex's object-space position
                // *is* the direction you are looking in. No view matrix arithmetic needed.
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = normalize(input.direction).y;

                // Falloff as an exponent rather than a straight lerp, so the pale band hugs the
                // horizon instead of washing halfway up the sky. Larger means a tighter band.
                float sky = pow(saturate(height), 1.0 / max(_HorizonFalloff, 0.01));
                float3 colour = lerp(_HorizonColour.rgb, _SkyColour.rgb, sky);

                float below = pow(saturate(-height), 1.0 / max(_GroundFalloff, 0.01));
                colour = lerp(colour, _GroundColour.rgb, below);

                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
