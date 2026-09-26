// A piece of a broken wall or rock face (design 58 §7): the cell's own meshes, clipped to one
// quarter of it and moved by a matrix of the renderer's, for the second and a half it takes to
// split and fall.
//
// **Clipped, not cut.** Nothing slices a mesh on the CPU: the pack's meshes are not all readable
// in a player, and a cut would have to be made again every time the art changed. The vertex stage
// keeps where each vertex was *before* the break, and the fragment stage throws away everything on
// the far side of the piece's two planes — the vertical plane that halves the cell and the
// horizontal one that quarters it — measured in that unmoved position, so a piece keeps exactly
// its share of the surface wherever it falls. `_ClipSides` 0 on both axes clips nothing, which is
// how the solid inner block of each piece is drawn.
//
// **Lit here, not by the art's own shader**, which cannot be asked to clip: the art's texture and
// colour are copied on to this material, and it is lit by the main light and the ambient probe the
// way a matte surface is. The inside of a piece — any back face, now that culling is off — is the
// broken interior, `_Interior`, which is what makes a hollow shell of panels read as a solid lump.
//
// It carries the cracks the wall had (OdysseyCrack.hlsl, measured in the unmoved position so the
// pattern rides with the piece) at `_Severity`, the level the cell had reached.
Shader "Odyssey/Shard"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Interior ("Broken interior", Color) = (0.3, 0.28, 0.26, 1)
        _Severity ("How cracked, 0 to 1", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Shard"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OdysseyCrack.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Interior;
                float _Severity;
            CBUFFER_END

            // Per piece, set by the renderer for each draw: where the piece has got to, and which
            // quarter of the cell it is.
            float4x4 _Motion;       // the unmoved world position to where it is now
            float4 _ClipCentre;     // the middle of the cell's drawn bounds, world space
            float4 _ClipAxis;       // xyz the horizontal axis the cell was halved along
            float4 _ClipSides;      // x which side of the halving plane, y above (+1) or below (-1); 0 keeps all

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 unmovedWS : TEXCOORD1;
                float3 unmovedNormalWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float2 sides : TEXCOORD4;
                float fog : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 unmoved = TransformObjectToWorld(input.positionOS.xyz);
                float3 unmovedNormal = TransformObjectToWorldNormal(input.normalOS);
                float3 moved = mul(_Motion, float4(unmoved, 1.0)).xyz;

                output.positionCS = TransformWorldToHClip(moved);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.unmovedWS = unmoved;
                output.unmovedNormalWS = unmovedNormal;
                output.normalWS = normalize(mul((float3x3)_Motion, unmovedNormal));
                float3 offset = unmoved - _ClipCentre.xyz;
                output.sides = float2(dot(offset, _ClipAxis.xyz) * _ClipSides.x, offset.y * _ClipSides.y);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Taken before the clip and any branch: the crack's antialiasing and the texture's
                // mip both need screen derivatives, which a discarded neighbour would spoil.
                float crack = OdysseyCrackMultiply(input.unmovedWS, normalize(input.unmovedNormalWS), _Severity);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(min(input.sides.x, input.sides.y));

                bool front = IS_FRONT_VFACE(face, true, false);
                float3 normal = normalize(input.normalWS) * (front ? 1.0 : -1.0);
                half3 surface = front ? albedo.rgb * crack : _Interior.rgb;

                Light sun = GetMainLight();
                half3 light = sun.color * saturate(dot(normal, sun.direction)) + SampleSH(normal);
                half3 colour = surface * light;
                colour = MixFog(colour, input.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
