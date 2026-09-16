#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What stands between the camera and the people the player has selected.
    ///
    /// <para><b>Why this exists.</b> The board is a wood. A colonist selected and then walked
    /// under a canopy disappears behind it, and because the camera orbits rather than cuts, the
    /// only way to see them again was to spin the camera until a gap opened — which loses the
    /// bearing the player had, and has to be done again the moment the colonist moves. The owner
    /// reported it as trees getting in the way (2026-09-16). So anything standing on the line
    /// between the eye and a selected figure is drawn ghosted for as long as it stands there.</para>
    ///
    /// <para><b>It is a segment test against real geometry, not a cell march.</b> A tree's cell is
    /// the one its trunk stands in, while what actually hides a colonist is its crown, six metres
    /// up and one cell nearer the camera. Asking which cells the line passes through would fade
    /// the wrong ones. Asking whether the line passes through an instance's own world bounds gets
    /// a tall thing right for the same reason it gets a wall right, and needs nothing from the
    /// mesher: the bounds come from the module and the placement from the matrix that was going
    /// to be drawn anyway.</para>
    ///
    /// <para><b>Two grains, for cost.</b> <see cref="Touches"/> is asked of a whole chunk once,
    /// and answers no for all but a handful of them; only inside those does
    /// <see cref="Blocks"/> run per instance. A meadow is thirty thousand instances a frame and
    /// testing every one of them would cost more than the thing is worth.</para>
    ///
    /// <para>Unity-free in everything but its vector types, and deliberately so: the whole of the
    /// decision is testable in EditMode with no scene, no camera and no art.</para>
    /// </summary>
    public sealed class SightLines
    {
        readonly struct Line
        {
            public Line(Vector3 eye, Vector3 target)
            {
                Eye = eye;
                Vector3 span = target - eye;
                Length = span.magnitude;
                Direction = Length > 1e-4f ? span / Length : Vector3.forward;
            }

            public readonly Vector3 Eye;
            public readonly Vector3 Direction;
            public readonly float Length;
        }

        readonly List<Line> _lines = new List<Line>();

        /// <summary>
        /// How wide the beam is, in metres.
        ///
        /// It stands for the width of the person being looked at rather than for the thickness of
        /// the line: a trunk half a metre to the side of the exact centre line still covers a
        /// colonist who is 1.15 m across. The occluder's own size is already accounted for, since
        /// what is tested is its bounds and not its origin.
        /// </summary>
        public float Radius { get; set; } = DefaultRadius;

        public const float DefaultRadius = 0.9f;

        /// <summary>Is anything selected? False is the common case and costs nothing.</summary>
        public bool Any => _lines.Count > 0;

        public int Count => _lines.Count;

        public void Clear() => _lines.Clear();

        /// <summary>
        /// A line of sight from the eye to a point on a figure.
        ///
        /// <para>The target is the chest rather than the feet, the same point the selection
        /// bracket and the hit-test use, because the segment stops there: geometry beyond the
        /// target is not in the way of anything. Ending at the chest is also what keeps the floor
        /// a colonist is standing on out of the beam — it lies more than <see cref="Radius"/>
        /// below the end of the line.</para>
        /// </summary>
        public void Add(Vector3 eye, Vector3 target)
        {
            var line = new Line(eye, target);
            if (line.Length <= 1e-4f) return;
            _lines.Add(line);
        }

        /// <summary>
        /// Could anything inside these bounds be in the way? The coarse test, asked of a chunk.
        ///
        /// <para><paramref name="upwards"/> is the allowance for geometry that is taller than the
        /// box it belongs to. A chunk's bounds are one layer high; a tree rooted in that layer is
        /// three layers tall and is exactly the thing this whole class is about, so a chunk has to
        /// answer for the air above it as well as for itself. Getting this wrong fails silently
        /// and in the most misleading way possible: the wood keeps hiding the colonist while every
        /// per-instance test passes, because no instance is ever reached.</para>
        /// </summary>
        public bool Touches(in Bounds bounds, float upwards = 0f)
        {
            if (_lines.Count == 0) return false;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            max.y += upwards;
            return HitsAny(min, max);
        }

        /// <summary>Is this one drawn instance in the way? The fine test, asked per matrix.</summary>
        public bool Blocks(in Bounds worldBounds) => HitsAny(worldBounds.min, worldBounds.max);

        bool HitsAny(Vector3 min, Vector3 max)
        {
            float r = Radius;
            min -= new Vector3(r, r, r);
            max += new Vector3(r, r, r);
            for (int i = 0; i < _lines.Count; i++)
                if (Hits(_lines[i], min, max)) return true;
            return false;
        }

        /// <summary>
        /// The slab test, clipped to the segment rather than run to infinity.
        ///
        /// <para>The clip is the whole point twice over: past the far end sits everything behind
        /// the colonist, which hides nothing, and before the near end sits everything behind the
        /// camera, which is not drawn. An unclipped ray test would ghost half the board.</para>
        /// </summary>
        static bool Hits(in Line line, Vector3 min, Vector3 max)
        {
            float near = 0f;
            float far = line.Length;

            for (int axis = 0; axis < 3; axis++)
            {
                float origin = line.Eye[axis];
                float direction = line.Direction[axis];
                if (Mathf.Abs(direction) < 1e-6f)
                {
                    // Parallel to this pair of planes: either inside them for the whole segment or
                    // outside them for the whole segment.
                    if (origin < min[axis] || origin > max[axis]) return false;
                    continue;
                }

                float inverse = 1f / direction;
                float t0 = (min[axis] - origin) * inverse;
                float t1 = (max[axis] - origin) * inverse;
                if (t0 > t1) (t0, t1) = (t1, t0);

                if (t0 > near) near = t0;
                if (t1 < far) far = t1;
                if (near > far) return false;
            }

            return true;
        }

        /// <summary>
        /// A module's own bounds, placed where the instance is going to be drawn.
        ///
        /// <para>The transform is the standard one: the centre goes through the matrix, and the
        /// extents through the matrix with every term made positive, which is what gives an
        /// axis-aligned box that still contains the rotated one. A tree turned to face a different
        /// way therefore keeps a box that contains it rather than one that has rotated out from
        /// under it.</para>
        /// </summary>
        public static Bounds Place(in Bounds local, in Matrix4x4 matrix)
        {
            Vector3 centre = matrix.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            var extents = new Vector3(
                Mathf.Abs(matrix.m00) * e.x + Mathf.Abs(matrix.m01) * e.y + Mathf.Abs(matrix.m02) * e.z,
                Mathf.Abs(matrix.m10) * e.x + Mathf.Abs(matrix.m11) * e.y + Mathf.Abs(matrix.m12) * e.z,
                Mathf.Abs(matrix.m20) * e.x + Mathf.Abs(matrix.m21) * e.y + Mathf.Abs(matrix.m22) * e.z);
            return new Bounds(centre, extents * 2f);
        }
    }
}
