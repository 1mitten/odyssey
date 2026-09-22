#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// How big a drawn colonist actually is — measured off the posed mesh, once, and in one place.
    ///
    /// <para><b>Written because the director was measuring the floor.</b> Every length in
    /// <c>PawnFigureDirector</c> is deliberately a fraction of the figure's own build rather than a
    /// number of metres: sixty-one characters have sixty-one sets of proportions and the director
    /// scales them besides, so a crouch or a body length written in metres is right on one of them
    /// and wrong on the other sixty. The quantity that fraction was taken of was
    /// <c>StandingHipHeight</c>, <c>hips.position.y - transform.position.y</c> — and on the Synty
    /// humanoid avatar <c>HumanBodyBones.Hips</c> maps to a bone literally named <c>Root</c> that
    /// sits at the model origin, with the real pelvis as its child. The difference is nought, on
    /// every character, so the measurement came back as its own 0.2 m floor and
    /// <c>SleepPose.BodyLength</c> laid a 2.49 m colonist down as though she were 0.38 m long
    /// (<c>docs/design/20-beds.md</c> §7b).</para>
    ///
    /// <para><b>So this asks the mesh, not the skeleton.</b> A bone's meaning is a decision made by
    /// whoever rigged the character and cannot be assumed — the same trap <c>GripTool</c> avoids by
    /// finding a haft from mesh bounds rather than trusting a prefab's orientation, and the same
    /// one <c>MeasureSole</c> avoids by baking the pose rather than believing the root is the sole.
    /// Where the drawn vertices are is not a decision anybody made; it is what the player sees.</para>
    ///
    /// <para><b>Baked, not bounded.</b> A <see cref="SkinnedMeshRenderer"/>'s <c>bounds</c> are the
    /// loose precomputed volume, not the posed mesh: on the cast measured here they run from
    /// −0.296 m to 2.618 m on a figure whose drawn body is 0.000 m to 2.488 m, which is a third of
    /// a metre of slack at each end. That is the error that put <c>MeasureSole</c> at 0.394 m once,
    /// and it is far too much for a body that has to fit a bed.</para>
    /// </summary>
    public static class FigureBuild
    {
        /// <summary>
        /// The lowest and highest drawn point of a posed figure, in world metres.
        ///
        /// <para><paramref name="lowest"/> comes back as <see cref="float.MaxValue"/> and
        /// <paramref name="highest"/> as <see cref="float.MinValue"/> when there is nothing
        /// bakeable — a prefab with no skin, or a clone with no packs — which callers test for
        /// rather than being handed a plausible-looking nought.</para>
        ///
        /// <para><b>The two transform steps are not interchangeable and neither announces itself.</b>
        /// <c>BakeMesh(mesh, useScale: true)</c> applies the renderer transform's own
        /// <i>local</i> scale, which on these prefabs is one — the figure's 1.4 lives on the root
        /// above it — so the full local-to-world is still needed afterwards and
        /// <c>TransformPoint</c> is right. Measured: this pair puts the body skin at
        /// 0.000..2.488 m, which agrees with the head bone at 2.181 m; baking without the scale
        /// and transforming gives 0.000..3.483 m, and baking with it and only turning gives
        /// 0.000..1.777 m. Two of the three are wrong by 40% and all three look like lengths.</para>
        /// </summary>
        public static void DrawnExtent(SkinnedMeshRenderer?[]? skins, out float lowest, out float highest)
        {
            lowest = float.MaxValue;
            highest = float.MinValue;
            if (skins == null) return;

            Mesh? baked = null;
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer? skin = skins[i];
                if (skin == null || !skin.enabled || skin.sharedMesh == null) continue;

                baked ??= new Mesh { name = "Odyssey/BuildProbe" };
                skin.BakeMesh(baked, useScale: true);

                Vector3[] vertices = baked.vertices;
                Transform at = skin.transform;
                for (int v = 0; v < vertices.Length; v++)
                {
                    float y = at.TransformPoint(vertices[v]).y;
                    if (y < lowest) lowest = y;
                    if (y > highest) highest = y;
                }
            }

            if (baked != null) Object.DestroyImmediate(baked);
        }

        /// <summary>
        /// A colonist's drawn height, sole to crown, which is also the length of body there is to
        /// lay down when it sleeps.
        ///
        /// <para><b>No ratio to a bone, because every ratio here has been wrong.</b> A standing
        /// figure's height and a lying figure's length are the same quantity; deriving one from a
        /// hip, a head or a leg puts a factor between the measurement and the thing measured, and
        /// that factor is a per-rig assumption dressed up as arithmetic. The drawn extent has
        /// none.</para>
        ///
        /// <para><paramref name="fallback"/> is what a figure with nothing bakeable gets — a clone
        /// with no packs, where the colonist is drawn from the baked instanced path anyway.</para>
        ///
        /// <para><b>Guarded at both ends, for the reason <see cref="Odyssey.Presentation.Rendering.Footing"/>'s
        /// sole is:</b> an absurd answer means the rig is not built the way this assumes, and
        /// better the fallback than a colonist laid out along three cells of bed. The window is
        /// wide — a metre to five — because it is there to catch a measurement that has collapsed
        /// or run away, not to second-guess a pack whose people are unusually tall.</para>
        /// </summary>
        public static float Height(SkinnedMeshRenderer?[]? skins, float rootY, float fallback,
            float minimum = 1f)
        {
            DrawnExtent(skins, out float lowest, out float highest);
            if (lowest >= float.MaxValue || highest <= float.MinValue) return fallback;

            // From the sole rather than from the root: the root sits where a boot would be, and a
            // barefoot character's heel is higher — the same difference MeasureSole exists for.
            // The floor of the window is the caller's: a metre for a person, and a few
            // centimetres for an animal (design 29), which is a quarter of a metre and not a
            // failed bake.
            float height = highest - Mathf.Min(lowest, rootY);
            return height >= minimum && height <= 5f ? height : fallback;
        }

        /// <summary>
        /// What a figure is laid down as when nothing can be measured: a colonist at the drawn
        /// scale, which is half again life size (<c>PawnFigureDirector.GroundSpeeds</c> says why).
        /// </summary>
        public const float FallbackHeight = 2.5f;
    }
}
