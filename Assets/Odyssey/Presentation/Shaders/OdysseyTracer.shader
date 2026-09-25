// A bullet's streak and a muzzle's flash (design 47 §4c): emissive, additive, one instanced call
// per material, drawn by ProjectileDirector.
//
// **The matrix is not a transform.** Each instance carries its whole shape in its four columns,
// so no per-instance property has to be kept alive in a player build and a bucket of streaks
// is one call:
//
//   streak (_Billboard 0): translation = the tail; the z column = head minus tail; the length
//     of the x column = the width in metres; the length of the y column = the fade, 0..1.
//   flash  (_Billboard 1): translation = the centre; the length of the x column = the size in
//     metres; the length of the y column = the fade, 0..1.
//
// The x and y columns are built orthogonal to the z column, so every matrix stays invertible
// and nothing Unity derives from it (the inverse, the determinant's sign) is ever degenerate.
//
// **Two pixels at least.** An additive line thinner than a pixel vanishes under anti-aliasing at
// the far zoom, so the width is raised to _MinPixels screen pixels at the vertex's own depth.
// That is why the quad is widened here, facing the camera, rather than in the matrix.
//
// **Explicit queue, Transparent+60** (P17): above the rain's Transparent+50, so a tracer through
// rain is never hidden by the rain batch's single sort distance. Depth-tested, never written.
Shader "Odyssey/Tracer"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.8, 0.45, 1)
        _Intensity ("Intensity", Float) = 2.5
        _MinPixels ("Least width, in screen pixels", Float) = 2
        _Billboard ("Mode (0 streak, 1 flash)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TracerForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Intensity;
                float _MinPixels;
                float _Billboard;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;      // x across (-1..1), y along (0 tail .. 1 head)
                float fade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // How wide one screen pixel is at this view depth, in metres. The absolute value
            // because URP flips the projection when it draws into a texture (the rain's lesson).
            float PixelMetres(float depth)
            {
                float perspective = unity_OrthoParams.w > 0.5 ? 1.0 : depth;
                return perspective * 2.0 / (_ScreenParams.y * abs(UNITY_MATRIX_P._m11));
            }

            float ViewDepth(float3 positionWS)
            {
                return max(1e-3, -TransformWorldToView(positionWS).z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4x4 m = UNITY_MATRIX_M;
                float3 origin = float3(m._m03, m._m13, m._m23);
                float3 xColumn = float3(m._m00, m._m10, m._m20);
                float3 yColumn = float3(m._m01, m._m11, m._m21);
                float3 zColumn = float3(m._m02, m._m12, m._m22);
                float width = length(xColumn);
                float fade = length(yColumn);

                // The quad: x in -0.5..0.5 across, z in 0..1 along.
                float side = input.positionOS.x * 2.0;
                float along = input.positionOS.z;

                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 positionWS;

                if (_Billboard > 0.5)
                {
                    float size = max(width, _MinPixels * PixelMetres(ViewDepth(origin)));
                    positionWS = origin + cameraRight * (side * 0.5 * size) + cameraUp * ((along - 0.5) * size);
                }
                else
                {
                    float3 onLine = origin + zColumn * along;
                    // The view matrix's third row is the camera's backward axis in the world.
                    float3 toCamera = unity_OrthoParams.w > 0.5
                        ? UNITY_MATRIX_V[2].xyz
                        : _WorldSpaceCameraPos - onLine;
                    float3 direction = normalize(zColumn + 1e-6);
                    float3 across = cross(direction, toCamera);
                    // Looking straight down the streak there is no across: fall back to the
                    // camera's own right, which is a dot rather than nothing.
                    across = dot(across, across) > 1e-8 ? normalize(across) : cameraRight;
                    float w = max(width, _MinPixels * PixelMetres(ViewDepth(onLine)));
                    positionWS = onLine + across * (side * 0.5 * w);
                }

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = float2(side, along);
                output.fade = fade;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float strength;
                if (_Billboard > 0.5)
                {
                    // A soft round flare, hottest in the middle.
                    float2 p = float2(input.uv.x, input.uv.y * 2.0 - 1.0);
                    float r = saturate(length(p));
                    strength = pow(1.0 - r, 2.0);
                }
                else
                {
                    // A line brightest down its middle and at its head, fading to the tail.
                    float across = 1.0 - abs(input.uv.x);
                    strength = across * across * lerp(0.12, 1.0, input.uv.y * input.uv.y);
                }
                float3 colour = _BaseColor.rgb * (_Intensity * strength * saturate(input.fade));
                return half4(colour, 0.0);
            }
            ENDHLSL
        }
    }
}
