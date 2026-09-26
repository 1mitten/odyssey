// An ink line around everything the camera can see, drawn from the depth buffer.
//
// Why depth rather than geometry. Outlining by rendering back faces expanded along their normals
// costs a second draw of every object in the world, which in a renderer that submits tens of
// thousands of instanced modules a frame is the one thing it is built not to do. A screen-space
// pass costs one fullscreen blit whatever the scene contains, and the scene here contains a lot.
//
// Why depth rather than depth and normals. A normals texture comes from a prepass that re-renders
// the world through each shader's DepthNormals pass, and this game's geometry is submitted with
// Graphics.RenderMeshInstanced from a camera callback — whether that reaches the prepass depends
// on the pack shaders having the pass at all. Depth is copied from the real depth buffer after
// opaques, so it is guaranteed to contain exactly what was drawn. The cost is that two flat
// surfaces meeting at the same depth get no crease line; silhouettes, which are most of the look,
// all come through.
Shader "Odyssey/Outline"
{
    Properties
    {
        _OutlineColour ("Outline colour", Color) = (0.06, 0.09, 0.08, 0.85)
        _Thickness ("Thickness in pixels", Float) = 1.2
        _DepthThreshold ("Depth threshold", Float) = 0.012
        _FadeStart ("Fade start in metres", Float) = 40
        _FadeEnd ("Fade end in metres", Float) = 90
        _SliverRadius ("Sliver test radius in pixels", Float) = 3
        _SliverTolerance ("Sliver test tolerance", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OdysseyOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineColour;
            float _Thickness;
            float _DepthThreshold;
            float _FadeStart;
            float _FadeEnd;
            float _SliverRadius;
            float _SliverTolerance;

            // Metres from the camera, so the comparison below is in world units rather than in the
            // depth buffer's own wildly non-linear ones.
            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // No ink on terrain (owner, 2026-09-24; design 38 §17c). The ground shader writes a
                // mark into the normals texture's spare channel from its DepthNormals pass, and the
                // prepass keeps only the surface that is finally visible, so a colonist standing on
                // the ground overwrites the mark with its own 0 and keeps its line. The ink lands on
                // the near side of a step (below), and the near side of a terrace riser, a bank or a
                // stream's edge is the ground itself — so asking the centre pixel is the whole test.
                // It also skips the rest of the work on most of the screen.
                half terrain = SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, sampler_PointClamp,
                    UnityStereoTransformScreenSpaceTex(uv)).a;
                if (terrain > 0.5) return scene;

                float2 texel = _BlitTexture_TexelSize.xy * _Thickness;

                // Roberts cross: two diagonal differences. Cheaper than a Sobel, and on a depth
                // buffer it gives a thinner, more even line, which is what an ink edge wants.
                float d0 = EyeDepth(uv + float2(-texel.x, -texel.y));
                float d1 = EyeDepth(uv + float2( texel.x,  texel.y));
                float d2 = EyeDepth(uv + float2(-texel.x,  texel.y));
                float d3 = EyeDepth(uv + float2( texel.x, -texel.y));

                float centre = EyeDepth(uv);

                // The edge measure is the *second* difference of inverse depth, not the first
                // difference of depth. On any flat surface 1/z is affine across the screen — the
                // same fact that makes perspective-correct interpolation work — so a plane scores
                // exactly zero however steeply it recedes, and only a real step in depth scores.
                //
                // The first difference could not do this. A flat field seen low across the board
                // has its depth change per pixel grow with the square of the distance, while the
                // threshold below grew only linearly with it, so from about eighty metres out the
                // ground itself tripped the detector and inked over: the meadow went dark towards
                // the horizon, with the tufts bright because their own depth broke the gradient.
                // Photographed under MeadowCheck, 2026-09-16, after the ambient occlusion, the
                // grass cutoff and the grass texture import had each been ruled out.
                //
                // Multiplying by the centre depth turns the score into a relative step: a
                // neighbour at n in front of a centre at c gives (c - n) / n on that diagonal, so
                // the threshold keeps its meaning as a fraction of distance.
                float invCentre = 1.0 / centre;
                float curvature = abs(1.0 / d0 + 1.0 / d1 - 2.0 * invCentre)
                                + abs(1.0 / d2 + 1.0 / d3 - 2.0 * invCentre);
                float step = curvature * centre;
                float threshold = max(_DepthThreshold * centre, 1e-4);

                float edge = saturate((step - _DepthThreshold) / _DepthThreshold);

                // One-sided: keep the ink on the near side of the step, which is the object.
                //
                // A Roberts cross fires on both sides of a depth discontinuity, so half the line
                // around anything lands on the *background* behind it. That is why the line looked
                // soft, and it is also what makes the width test below impossible — a test that
                // asks "is the thing I am standing on narrow?" cannot answer for a pixel standing
                // on the sky. If the centre is background, its furthest neighbour is no further
                // than it is and this term is zero.
                float furthest = max(max(d0, d1), max(d2, d3));
                edge *= saturate((furthest - centre) / threshold);

                // The sliver test: no ink on anything narrower than the line itself.
                //
                // This replaces guessing with the right question. A clump of grass is a few pixels
                // wide at board distance and every one of them sits on a discontinuity, so the
                // whole clump inks over and the meadow reads as dark smudges. Distance was the
                // wrong thing to measure — a far-off building should keep its outline and a
                // close-up railing should not. Width is the thing that decides whether a line is
                // legible at all.
                //
                // Sample wider than the detector. If both opposite neighbours on an axis are
                // clearly behind the centre, the centre belongs to something thinner than the
                // sample diameter, and no readable line can be drawn on it. min() is "this axis is
                // a sliver", max() is "either axis is". Because the ramp is signed, a neighbour
                // *nearer* than the centre clamps to zero, so nothing is ever called a sliver
                // merely because something stands in front of it.
                //
                // It fixes itself as the camera comes in: once a tuft is wider than the sample
                // diameter its interior pixels stop having background on both sides and the line
                // comes back. That is exactly what a distance fade could never do.
                float2 radius = _BlitTexture_TexelSize.xy * _SliverRadius;
                float tolerance = max(_SliverTolerance * centre, 1e-4);

                float behindL = saturate((EyeDepth(uv - float2(radius.x, 0.0)) - centre) / tolerance - 1.0);
                float behindR = saturate((EyeDepth(uv + float2(radius.x, 0.0)) - centre) / tolerance - 1.0);
                float behindD = saturate((EyeDepth(uv - float2(0.0, radius.y)) - centre) / tolerance - 1.0);
                float behindU = saturate((EyeDepth(uv + float2(0.0, radius.y)) - centre) / tolerance - 1.0);

                float sliver = max(min(behindL, behindR), min(behindD, behindU));
                edge *= 1.0 - sliver;

                // The distance fade stays, but only as a long-range backstop now that width is
                // being measured properly. Aerial perspective does the same thing in the reference.
                edge *= 1.0 - smoothstep(_FadeStart, _FadeEnd, centre);

                scene.rgb = lerp(scene.rgb, _OutlineColour.rgb, edge * _OutlineColour.a);
                return scene;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
