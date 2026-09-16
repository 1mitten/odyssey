#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The earth bank that makes a terrace step somewhere you can walk up.
    ///
    /// <para><b>The problem, and why it is not solvable in the cell model.</b> A terrace riser is a
    /// whole cell: 3.0 m of sheer face, drawn as one flat rectangle. ADR 0002 fixes the cell at
    /// 2.5 x 2.5 x 3.0 m and calls it effectively irreversible, so there are no half-heights to
    /// spread the drop over. Meanwhile the simulation says a colonist walks up that face —
    /// <c>MoveCost.JumpUp</c>, a hop into the column next door — so the board shows a wall where
    /// the game has a path.</para>
    ///
    /// <para><b>Where the half and quarter steps actually go: in the mesh.</b> A mesh variant
    /// costs one instancing bucket, not one instance per cell, so geometry is the cheap place to
    /// put detail the cell model cannot carry. This is a stepped bank, three treads and three
    /// risers, drawn in the empty cell beside the step. It is a facade in exactly the sense
    /// <see cref="GroundRelief"/> and <see cref="GroundScatter"/> are: no cell knows about it, it
    /// is not pathable, not selectable, not in the save and not in the state hash.</para>
    ///
    /// <para><b>Stepped rather than smooth, on purpose.</b> Three metres over one cell is a
    /// fifty-degree ramp however it is drawn, and a smooth fifty-degree ramp reads as a road
    /// somebody built. Cut into treads it reads as a bank that has weathered into ledges, which is
    /// what a hillside does and what the eye accepts a person climbing.</para>
    ///
    /// <para><b>Three stacked boxes, not one carved solid.</b> Each step is its own closed box,
    /// stacked on the one below rather than nested inside it. Carving a single staircase solid
    /// means triangulating two staircase-shaped side polygons and getting the shared vertical
    /// edges right, and every one of those edges is a chance at a T-junction and a crack. Stacking
    /// costs a few triangles nobody can see — the faces where two boxes meet are back to back, so
    /// at most one is ever front-facing and there is nothing to z-fight — and it cannot be got
    /// wrong.</para>
    /// </summary>
    public static class BankMesh
    {
        /// <summary>
        /// How many treads a bank is cut into.
        ///
        /// <para>Three, which over a 3 m rise is a one-metre riser and an 83 cm tread. Two reads as
        /// a single awkward half-step; four makes each tread narrower than a boot and the whole
        /// thing turns back into a ramp with texture on it. Three is also what a hillside path
        /// tends to weather into, which is the look being borrowed.</para>
        /// </summary>
        public const int Steps = 3;

        /// <summary>
        /// How many distinct banks are built. Each is one more instancing bucket.
        ///
        /// <para>Unlike <see cref="GroundMesh"/>, a bank cannot take its variety from the four
        /// right-angle bearings: the bearing is already spoken for, because it is what points the
        /// bank at the step it climbs. So variety has to come from meshes, and two is enough that
        /// a long riser does not read as a repeated stamp.</para>
        /// </summary>
        public const int Variants = 2;

        /// <summary>
        /// How far a tread's edge wanders from the even division, as a fraction of the cell.
        ///
        /// Small: enough that two banks side by side do not line their steps up into a continuous
        /// ledge running along the whole terrace, which is the thing that would read as masonry.
        /// </summary>
        public const float MaxJitter = 0.06f;

        /// <summary>
        /// How far the bank is sunk into the ground it stands on, as a fraction of cell height.
        ///
        /// <para>The cell below the bank wears a <see cref="GroundMesh"/> top whose rim may dip by
        /// <see cref="GroundMesh.MaxRipple"/>, so a bank sitting exactly on the nominal floor would
        /// hang over that dip by a visible centimetre or two along its whole foot. Sinking it past
        /// the deepest possible dip costs nothing — what is buried is buried — and removes the
        /// question.</para>
        /// </summary>
        public const float Sink = GroundMesh.MaxRipple + 0.02f;

        static readonly Mesh?[] Cache = new Mesh?[Variants];

        /// <summary>
        /// One of the banks, built on first use and kept.
        ///
        /// <para>In the unit box, like every other module the renderer owns: <c>y = -0.5</c> is the
        /// floor of the cell the bank stands in, which is the top of the lower terrace, and
        /// <c>y = +0.5</c> is the top of the cell, which is the top of the riser beside it. Local
        /// <c>+z</c> points at the riser, because that is where
        /// <see cref="Directions.Yaw"/> turns a module's local <c>+z</c> to face.</para>
        /// </summary>
        public static Mesh For(int variant)
        {
            int index = ((variant % Variants) + Variants) % Variants;
            Mesh? mesh = Cache[index];
            if (mesh != null) return mesh;
            return Cache[index] = Build(index);
        }

        /// <summary>
        /// Where the treads are, front to back: <c>Edges(v)[k]</c> is the z at which tread
        /// <c>k</c> begins, and <c>Edges(v)[Steps]</c> is the back of the cell.
        ///
        /// <para>Monotonic by construction rather than by luck — each edge is the even division
        /// plus a bounded wobble, and the wobble is less than half the spacing, so no amount of
        /// jitter can put two edges out of order and turn a tread inside out.</para>
        /// </summary>
        public static float[] Edges(int variant)
        {
            var edges = new float[Steps + 1];
            float span = 1f / Steps;

            for (int k = 0; k <= Steps; k++)
            {
                float even = -0.5f + span * k;
                if (k == 0 || k == Steps) { edges[k] = even; continue; }

                uint salt = (uint)(variant * 397 + k) + 1u;
                edges[k] = even + (Unit(salt, 0x5B5Bu) - 0.5f) * 2f * Mathf.Min(MaxJitter, span * 0.4f);
            }

            return edges;
        }

        /// <summary>The height of tread <c>k</c>, in the unit box. The last one is the cell top.</summary>
        public static float TreadHeight(int variant, int step)
        {
            // Evenly spaced and not jittered. The top tread has to meet the terrace it climbs to
            // exactly, and the foot has to meet the ground it stands on; wandering the heights in
            // between would buy very little and risks a step taller than the one above it.
            return -0.5f + (step + 1) * (1f / Steps);
        }

        static Mesh Build(int variant)
        {
            float[] edges = Edges(variant);

            var vertices = new List<Vector3>(128);
            var normals = new List<Vector3>(128);
            var uvs = new List<Vector2>(128);
            var triangles = new List<int>(192);

            float below = -0.5f - Sink;

            for (int k = 0; k < Steps; k++)
            {
                // Step k spans from its own front edge to the back of the cell, and stands on the
                // step in front of it rather than on the floor — stacked, not nested. Its top face
                // is the tread; the part of it the next step stands on is buried, which is exactly
                // what makes the visible tread the right width without anyone computing one.
                float front = edges[k];
                float top = TreadHeight(variant, k);
                float bottom = k == 0 ? below : TreadHeight(variant, k - 1);

                AddBox(vertices, normals, uvs, triangles,
                    new Vector3(-0.5f, bottom, front),
                    new Vector3(0.5f, top, 0.5f));
            }

            var mesh = new Mesh { name = "Odyssey/Bank" + variant };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>One axis-aligned box, six flat-shaded quads, every face pointing outward.</summary>
        static void AddBox(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 min, Vector3 max)
        {
            // -z, +z, -x, +x, -y, +y. Corners of each are given anticlockwise about the outward
            // normal, which AddQuad's winding then turns into triangles facing that way.
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z));
        }

        /// <summary>
        /// One flat facet. The winding is 0-2-1 and 0-3-2, the same convention
        /// <see cref="PrimitiveMeshes.UnitCube"/> and <see cref="RockMesh"/> use — the opposite of
        /// the obvious order, because taking the corners as given builds triangles facing inward,
        /// which is valid geometry that renders as a hole.
        /// </summary>
        static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 normal = Vector3.Cross(c - a, b - a);
            normal = normal.sqrMagnitude < 1e-12f ? Vector3.up : normal.normalized;
            int start = vertices.Count;

            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);

            // Planar UVs, one repeat per cell, so the bank wears the same tiling terrain texture
            // at the same scale as the ground it grows out of.
            uvs.Add(Uv(a, normal)); uvs.Add(Uv(b, normal));
            uvs.Add(Uv(c, normal)); uvs.Add(Uv(d, normal));

            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        static Vector2 Uv(Vector3 point, Vector3 normal)
        {
            if (Mathf.Abs(normal.y) > 0.5f) return new Vector2(point.x + 0.5f, point.z + 0.5f);
            float across = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? point.z : point.x;
            return new Vector2(across + 0.5f, point.y + 0.5f);
        }

        /// <summary>A stable 0..1 from a salt pair. The same avalanche <see cref="GroundScatter"/> uses.</summary>
        static float Unit(uint a, uint b) =>
            (GroundScatter.Hash((int)a, (int)b, 0x9E3779B9u) & 0xFFFFFFu) * (1f / 0x1000000);
    }
}
