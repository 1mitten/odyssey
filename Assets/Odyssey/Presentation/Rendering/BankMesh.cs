#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The earth bank that makes a terrace step somewhere you can walk up: one smooth slope, the
    /// full width of its cell, with corners that meet.
    ///
    /// <para><b>The problem, and why it is not solvable in the cell model.</b> A terrace riser is a
    /// whole cell: 3.0 m of sheer face. ADR 0002 fixes the cell at 2.5 x 2.5 x 3.0 m and calls it
    /// effectively irreversible, so there are no half-heights to spread the drop over. Meanwhile the
    /// simulation says a colonist walks up that face — <c>MoveCost.JumpUp</c>, a hop into the column
    /// next door — so the board shows a wall where the game has a path. This is the picture of that
    /// path, and it is a facade in the strict sense: no cell knows about it, it is not pathable, not
    /// selectable, not in the save and not in the state hash.</para>
    ///
    /// <para><b>It was a staircase first, and that was wrong.</b> The first version cut the slope
    /// into three treads with the tread positions jittered per cell so a long run would not repeat.
    /// Every part of that was a mistake and the owner named it exactly: the jitter meant
    /// neighbouring cells put their treads in different places, so a run came out as a ridge of
    /// misaligned bars rather than as one flight; the taper that stopped the ends being walls turned
    /// them into wedges; and it read as built furniture dropped onto the ground rather than as
    /// ground. <b>Simple and continuous beats varied and broken</b>, which is the same lesson the
    /// rim ripple taught one file over.</para>
    ///
    /// <para><b>Three shapes, and they tile.</b> The top surface of a bank is a height field over
    /// its own cell, and the three cases are the three simplest functions there are — where local
    /// <c>+z</c> and <c>+x</c> point at the steps a cell is climbing:</para>
    ///
    /// <list type="bullet">
    /// <item><see cref="Kind.Straight"/>, against one step: <c>y = z</c>.</item>
    /// <item><see cref="Kind.Inner"/>, tucked into a corner with steps on two adjacent sides:
    /// <c>y = max(x, z)</c>, which is high along both and dips to the open corner.</item>
    /// <item><see cref="Kind.Outer"/>, wrapping the outside of a corner with a step only on the
    /// diagonal: <c>y = min(x, z)</c>, a hip that rises to that one corner.</item>
    /// </list>
    ///
    /// <para><b>They agree exactly where they meet</b>, which is the whole reason for choosing these
    /// three. Along the edge it shares with a straight neighbour, <c>max(x, z)</c> is <c>z</c> and so
    /// is <c>min(x, z)</c> — the same value the straight piece has there. So a run of banks around a
    /// terrace, corners and all, is one continuous surface with no seam to find and no width to
    /// match by hand. It is matched by construction.</para>
    ///
    /// <para>The <see cref="Outer"/> piece is what fixes corners rather than merely surviving them.
    /// A cell diagonally outside a convex corner touches no step orthogonally, so it used to get
    /// nothing and the run had a square bite out of it. It is exactly the cell that should carry the
    /// hip.</para>
    ///
    /// <para><b>Nothing is varied and nothing is jittered.</b> One mesh per shape, four bearings
    /// from the instance matrix, and no per-cell choice at all — so three modules cover every bank
    /// on the board, which is fewer than the stepped version needed for one.</para>
    /// </summary>
    public static class BankMesh
    {
        /// <summary>The three ways a cell can meet the steps around it.</summary>
        public enum Kind
        {
            /// <summary>One step, against local +z. The surface is <c>y = z</c>.</summary>
            Straight = 0,

            /// <summary>Steps on local +z and +x. The surface is <c>y = max(x, z)</c>.</summary>
            Inner = 1,

            /// <summary>A step only on the local +z/+x diagonal. The surface is <c>y = min(x, z)</c>.</summary>
            Outer = 2,
        }

        /// <summary>How many shapes there are. One module each, per earth terrain.</summary>
        public const int Kinds = 3;

        /// <summary>
        /// How far the slope is sunk into the ground it stands on, as a fraction of cell height.
        ///
        /// <para>The cell below wears a <see cref="GroundMesh"/> top whose rim may move by
        /// <see cref="GroundMesh.MaxRipple"/>, so a slope sitting exactly on the nominal floor could
        /// hang over that by a centimetre along its foot. Sinking it past the deepest the rim can go
        /// costs nothing — what is buried is buried — and removes the question.</para>
        /// </summary>
        public static float Sink => GroundMesh.MaxRipple + 0.02f;

        static readonly Mesh?[] Cache = new Mesh?[Kinds];

        /// <summary>
        /// Throw away the built banks so the next request rebuilds them, for a harness sweeping
        /// <see cref="GroundMesh.SideNormalTiltDegrees"/>. Destroyed rather than dropped, because a
        /// mesh made in code is a GPU allocation Unity never collects.
        /// </summary>
        public static void Invalidate()
        {
            for (int i = 0; i < Cache.Length; i++)
            {
                Mesh? mesh = Cache[i];
                if (mesh == null) continue;
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
                Cache[i] = null;
            }
        }

        /// <summary>
        /// One of the three slopes, built on first use and kept.
        ///
        /// <para>In the unit box, like every other module the renderer owns: <c>y = -0.5</c> is the
        /// floor of the cell the bank stands in, which is the top of the lower terrace, and
        /// <c>y = +0.5</c> is the top of the cell, which is the top of the step beside it. Local
        /// <c>+z</c> points at the step, because that is where <see cref="Directions.Yaw"/> turns a
        /// module's local <c>+z</c> to face.</para>
        /// </summary>
        public static Mesh For(Kind kind)
        {
            int index = ((int)kind % Kinds + Kinds) % Kinds;
            Mesh? mesh = Cache[index];
            if (mesh != null) return mesh;
            return Cache[index] = Build((Kind)index);
        }

        /// <summary>The same, by index, for a caller that resolves modules through one integer.</summary>
        public static Mesh For(int kind) => For((Kind)(((kind % Kinds) + Kinds) % Kinds));

        /// <summary>
        /// The height of a bank's surface at a point in its own cell, where <c>x</c> and <c>z</c>
        /// run -0.5 to 0.5.
        ///
        /// <para>Public because it is the whole definition of the shape, and because the tests that
        /// matter are about this function rather than about the triangles built from it: that the
        /// slope reaches the step, that it meets the floor at the open side, and that neighbouring
        /// pieces agree along the edge they share.</para>
        /// </summary>
        public static float HeightAt(Kind kind, float x, float z)
        {
            switch (kind)
            {
                case Kind.Inner: return Mathf.Max(x, z);
                case Kind.Outer: return Mathf.Min(x, z);
                default: return z;
            }
        }

        static Mesh Build(Kind kind)
        {
            var vertices = new List<Vector3>(64);
            var normals = new List<Vector3>(64);
            var uvs = new List<Vector2>(64);
            var triangles = new List<int>(96);

            const float lo = -0.5f, hi = 0.5f;
            float foot = lo - Sink;

            float hMinMin = HeightAt(kind, lo, lo);
            float hMaxMin = HeightAt(kind, hi, lo);
            float hMaxMax = HeightAt(kind, hi, hi);
            float hMinMax = HeightAt(kind, lo, hi);

            var cMinMin = new Vector3(lo, hMinMin, lo);
            var cMaxMin = new Vector3(hi, hMaxMin, lo);
            var cMaxMax = new Vector3(hi, hMaxMax, hi);
            var cMinMax = new Vector3(lo, hMinMax, hi);

            // ---- the top, as two triangles split along the main diagonal -----------------------
            //
            // Both max and min bend along x = z and are planar either side of it, so that diagonal
            // is where the surface really creases. A straight slope is planar and does not care
            // which way it is cut, so one split serves all three and there is no special case.
            AddQuad(vertices, normals, uvs, triangles, cMinMin, cMaxMin, cMaxMax, cMaxMax);
            AddQuad(vertices, normals, uvs, triangles, cMinMin, cMaxMax, cMinMax, cMinMax);

            // ---- the four sides ---------------------------------------------------------------
            //
            // Every side is a straight line in all three shapes — along an edge, max and min each
            // collapse to one of their arguments or to a constant — so a quad from the foot up to
            // the two corner heights is exact rather than an approximation of a curve.
            AddQuad(vertices, normals, uvs, triangles,          // -z
                new Vector3(lo, foot, lo), new Vector3(hi, foot, lo), cMaxMin, cMinMin);

            AddQuad(vertices, normals, uvs, triangles,          // +z
                new Vector3(hi, foot, hi), new Vector3(lo, foot, hi), cMinMax, cMaxMax);

            AddQuad(vertices, normals, uvs, triangles,          // -x
                new Vector3(lo, foot, hi), new Vector3(lo, foot, lo), cMinMin, cMinMax);

            AddQuad(vertices, normals, uvs, triangles,          // +x
                new Vector3(hi, foot, lo), new Vector3(hi, foot, hi), cMaxMax, cMaxMin);

            // ---- the foot ---------------------------------------------------------------------
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(lo, foot, hi), new Vector3(hi, foot, hi),
                new Vector3(hi, foot, lo), new Vector3(lo, foot, lo));

            var mesh = new Mesh { name = "Odyssey/Bank" + kind };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// One flat facet, given clockwise as seen from outside. Repeating the last corner makes it
        /// a triangle, which is how the two halves of the top are emitted.
        ///
        /// <para>The winding is 0-2-1 and 0-3-2, the same convention
        /// <see cref="PrimitiveMeshes.UnitCube"/> and <see cref="RockMesh"/> use — the opposite of
        /// the obvious order, because taking the corners as given builds triangles facing inward,
        /// which is valid geometry that renders as a hole.</para>
        /// </summary>
        static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Fold away corners that have collapsed onto one another. A slope that reaches the
            // floor at an edge leaves that side with no height at all, and a quad with coincident
            // corners carries a zero-length edge and a triangle with no area, which shades from a
            // normal that cannot be computed. Same guard, same reason, as GroundMesh.
            var given = new[] { a, b, c, d };
            var corners = new Vector3[4];
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (count > 0 && (given[i] - corners[count - 1]).sqrMagnitude < 1e-10f) continue;
                corners[count++] = given[i];
            }
            if (count > 1 && (corners[0] - corners[count - 1]).sqrMagnitude < 1e-10f) count--;
            if (count < 3) return;

            Vector3 normal = Vector3.Cross(corners[2] - corners[0], corners[1] - corners[0]);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal = normal.normalized;

            // Shaded like the ground it grows out of, through GroundMesh.SideNormalTiltDegrees, so
            // a near-vertical side of a slope is lit as earth rather than as the side of a building.
            Vector3 shaded = GroundMesh.ShadingNormal(normal);
            int start = vertices.Count;

            for (int i = 0; i < count; i++)
            {
                vertices.Add(corners[i]);
                normals.Add(shaded);
                uvs.Add(Uv(corners[i], normal));
            }

            for (int i = 2; i < count; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i);
                triangles.Add(start + i - 1);
            }
        }

        /// <summary>
        /// Planar UVs, one repeat per cell, so a bank wears the same tiling terrain texture at the
        /// same scale as the ground either side of it. Taken from the plan for anything facing
        /// mostly upward, which is what keeps a slope continuous with the flat ground it joins.
        /// </summary>
        static Vector2 Uv(Vector3 point, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f) return new Vector2(point.x + 0.5f, point.z + 0.5f);
            float across = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? point.z : point.x;
            return new Vector2(across + 0.5f, point.y + 0.5f);
        }
    }
}
