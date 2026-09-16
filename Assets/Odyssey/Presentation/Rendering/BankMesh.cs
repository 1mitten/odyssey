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
        public static float Sink => GroundMesh.MaxRipple + 0.02f;

        /// <summary>
        /// How far the open end of a bank is drawn in, as a fraction of the cell, by the time it
        /// reaches the top.
        ///
        /// <para><b>This is a bug fix, and the bug was dark green patches beside the steps.</b> A
        /// bank fills its cell in plan, so along a run each one's side wall is buried in the next
        /// and nothing shows. At the end of a run there is no next one, and the side wall — up to
        /// three metres of it — stands in open air below the terrace top. The owner saw them before
        /// any test did, and no test would have: every bank was a correct closed mesh in the right
        /// place, and the fault was that nothing stood beside it.</para>
        ///
        /// <para>Tapering rather than hiding the wall, because an earth bank does fade out at its
        /// end; it does not stop dead. Each step is drawn in on the open side as it rises, so the
        /// stack narrows away and the wall becomes a slope. The insets accumulate — a step starts
        /// where the step below finished — or every tread would overhang the one under it and the
        /// taper would come out as a flight of ledges.</para>
        /// </summary>
        public const float EndTaper = 0.40f;

        /// <summary>How many ways a bank's two ends can be open: neither, left, right, both.</summary>
        public const int EndCases = 4;

        /// <summary>One mesh per variant per pattern of open ends.</summary>
        public static int Slots => Variants * EndCases;

        static readonly Mesh?[] Cache = new Mesh?[Variants * EndCases];

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
        /// One of the banks, built on first use and kept.
        ///
        /// <para>In the unit box, like every other module the renderer owns: <c>y = -0.5</c> is the
        /// floor of the cell the bank stands in, which is the top of the lower terrace, and
        /// <c>y = +0.5</c> is the top of the cell, which is the top of the riser beside it. Local
        /// <c>+z</c> points at the riser, because that is where
        /// <see cref="Directions.Yaw"/> turns a module's local <c>+z</c> to face.</para>
        /// </summary>
        public static Mesh For(int variant) => For(variant, 0);

        /// <summary>
        /// A bank, built for one pattern of open ends.
        ///
        /// <paramref name="ends"/> is two bits in the bank's own frame: bit 0 is the local -x side
        /// and bit 1 the local +x side. An open side is one with no bank beside it, whose wall
        /// would otherwise stand in the air.
        /// </summary>
        public static Mesh For(int variant, int ends)
        {
            int slot = Slot(variant, ends);
            Mesh? mesh = Cache[slot];
            if (mesh != null) return mesh;
            return Cache[slot] = Build(slot % Variants, slot / Variants);
        }

        /// <summary>Where a (variant, ends) pair sits in the family. Variant-minor, like the faces.</summary>
        public static int Slot(int variant, int ends)
        {
            int v = ((variant % Variants) + Variants) % Variants;
            int e = ((ends % EndCases) + EndCases) % EndCases;
            return e * Variants + v;
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

        static Mesh Build(int variant, int ends)
        {
            float[] edges = Edges(variant);
            float perStep = EndTaper / Steps;
            bool openMinX = (ends & 1) != 0;
            bool openMaxX = (ends & 2) != 0;

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

                // The taper accumulates, so this step starts exactly where the step below finished.
                // Anything else leaves a ledge at every tread.
                AddTaperedBox(vertices, normals, uvs, triangles,
                    new Vector3(-0.5f, bottom, front),
                    new Vector3(0.5f, top, 0.5f),
                    openMinX ? k * perStep : 0f, openMinX ? (k + 1) * perStep : 0f,
                    openMaxX ? k * perStep : 0f, openMaxX ? (k + 1) * perStep : 0f);
            }

            var mesh = new Mesh { name = "Odyssey/Bank" + variant + "-" + ends };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// One box whose top face may be drawn in along x, so a side becomes a slope rather than a
        /// wall. Six flat-shaded quads, every face pointing outward, exactly as a plain box.
        ///
        /// <para>The insets are given for the bottom and the top of each x side separately, so a
        /// stack of these forms one continuous slope instead of a flight of ledges: a step takes as
        /// its bottom inset whatever the step below used at its top. All four zero is an ordinary
        /// box, which is what a bank with no open end gets.</para>
        /// </summary>
        static void AddTaperedBox(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 min, Vector3 max,
            float bottomMinX, float topMinX, float bottomMaxX, float topMaxX)
        {
            float bx0 = min.x + bottomMinX, bx1 = max.x - bottomMaxX;
            float tx0 = min.x + topMinX, tx1 = max.x - topMaxX;

            // A taper wide enough to cross itself would turn the box inside out. Clamped rather
            // than asserted, because the caller is arithmetic and not a person, and the sensible
            // answer to "narrower than nothing" is a ridge.
            if (tx0 > tx1) { float mid = 0.5f * (tx0 + tx1); tx0 = mid; tx1 = mid; }
            if (bx0 > bx1) { float mid = 0.5f * (bx0 + bx1); bx0 = mid; bx1 = mid; }

            // -z, +z, -x, +x, -y, +y. Corners of each are given anticlockwise about the outward
            // normal, which AddQuad's winding then turns into triangles facing that way.
            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(bx0, min.y, min.z), new Vector3(bx1, min.y, min.z),
                new Vector3(tx1, max.y, min.z), new Vector3(tx0, max.y, min.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(bx1, min.y, max.z), new Vector3(bx0, min.y, max.z),
                new Vector3(tx0, max.y, max.z), new Vector3(tx1, max.y, max.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(bx0, min.y, max.z), new Vector3(bx0, min.y, min.z),
                new Vector3(tx0, max.y, min.z), new Vector3(tx0, max.y, max.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(bx1, min.y, min.z), new Vector3(bx1, min.y, max.z),
                new Vector3(tx1, max.y, max.z), new Vector3(tx1, max.y, min.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(bx0, min.y, max.z), new Vector3(bx1, min.y, max.z),
                new Vector3(bx1, min.y, min.z), new Vector3(bx0, min.y, min.z));

            AddQuad(vertices, normals, uvs, triangles,
                new Vector3(tx0, max.y, min.z), new Vector3(tx1, max.y, min.z),
                new Vector3(tx1, max.y, max.z), new Vector3(tx0, max.y, max.z));
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
            // Fold away corners that have collapsed onto one another before anything else looks at
            // them. A fully tapered step brings the two ends of a side together, and a quad with
            // coincident corners carries a zero-length edge and counts a real edge three times, so
            // the block stops being closed. Same reasoning, and the same fix, as GroundMesh.
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

            a = corners[0];
            b = corners[1];
            c = corners[2];
            d = count > 3 ? corners[3] : corners[2];

            Vector3 normal = Vector3.Cross(c - a, b - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal = normal.normalized;
            int start = vertices.Count;

            // Shaded like the ground it grows out of, through GroundMesh.SideNormalTiltDegrees.
            // A bank is a metre-high riser every 83 cm, so if its risers kept true vertical normals
            // while the terrace either side of it did not, the bank would come out as a ladder of
            // black bars laid across the one place the eye is being drawn to.
            Vector3 shaded = GroundMesh.ShadingNormal(normal);

            for (int i = 0; i < count; i++)
            {
                vertices.Add(corners[i]);
                normals.Add(shaded);
                // Planar UVs, one repeat per cell, so the bank wears the same tiling terrain
                // texture at the same scale as the ground it grows out of.
                uvs.Add(Uv(corners[i], normal));
            }

            // A fan from the first corner, wound 0-2-1 for the reason PrimitiveMeshes records: the
            // obvious order builds triangles facing inward, which renders as a hole.
            for (int i = 2; i < count; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i);
                triangles.Add(start + i - 1);
            }
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
