#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Jagged stone blocks, built in code, that still tile a grid exactly.
    ///
    /// <para><b>The problem.</b> A rock cell was a cube, so an outcrop read as a stack of brown
    /// boxes. What it wants is a chipped, faceted lump — but a lump carved freely out of a cell
    /// opens cracks along the seams where it meets the next cell, and a crack in a cliff face is
    /// far worse than a flat one.</para>
    ///
    /// <para><b>The rule that makes it safe, and it is the only rule that matters: no vertex ever
    /// moves inward.</b> Rim vertices may push *outward* in x and z, and top vertices may drop, and
    /// nothing else moves at all. The base stays the exact cell footprint. Two neighbouring blocks
    /// therefore always meet at their bases and overlap above them — opaque rock inside opaque
    /// rock, which is invisible — so there is no arrangement of variants, rotations or heights
    /// that can leave a gap between two solid cells. <see cref="Variants"/> different lumps and
    /// four yaws give twenty-four looks, and every one of them fits every other.</para>
    ///
    /// <para>Three things together stop it reading as masonry, and the first attempt had only the
    /// first of them: the rim drops by a different amount at each of its eight points, so a run of
    /// rock has a broken skyline; each variant carries a <see cref="MaxTilt"/> across its whole
    /// face, so one lump is a wedge and the next leans the other way; and every wall has a vertex
    /// half way up pushed outward, so it swells and undercuts instead of being a vertical plane.
    /// A face made of vertical planes reads as brickwork however much its top edge wanders.</para>
    ///
    /// <para>Nothing is ever raised above the cell top, because the cell above a rock is where a
    /// colonist stands and rock growing through somebody's feet is a worse fault than a straight
    /// edge.</para>
    /// </summary>
    public static class RockMesh
    {
        /// <summary>How many distinct lumps are built. Each is one more instancing bucket.</summary>
        public const int Variants = 6;

        /// <summary>The most a rim vertex drops, as a fraction of the cell's height (3 m): 66 cm.</summary>
        public const float MaxDrop = 0.22f;

        /// <summary>The most a vertex leans out, as a fraction of the cell's width (2.5 m): 32 cm.</summary>
        public const float MaxBulge = 0.13f;

        /// <summary>
        /// How far a lump may tilt across its own face, as a fraction of the cell's height: 45 cm
        /// corner to corner.
        ///
        /// Per-vertex noise alone chips a block evenly all round, which averages out into
        /// something that still reads as a block. A tilt is a single decision applied across the
        /// whole face, so one variant comes out as a wedge and another leans the other way, and
        /// *that* is what stops a cliff looking like masonry.
        /// </summary>
        public const float MaxTilt = 0.15f;

        /// <summary>How far the one interior top vertex may wander. It is on no seam, so it is free.</summary>
        public const float MaxDrift = 0.06f;

        static readonly Mesh?[] Cache = new Mesh?[Variants];

        /// <summary>One of the lumps, built on first use and kept.</summary>
        public static Mesh For(int variant)
        {
            int index = ((variant % Variants) + Variants) % Variants;
            Mesh? mesh = Cache[index];
            if (mesh != null) return mesh;
            return Cache[index] = Build(index);
        }

        /// <summary>
        /// The nine top-surface vertices of a variant, in the unit cube's own space, laid out
        /// row by row: <c>Top(variant)[j * 3 + i]</c> for <c>i</c> along x and <c>j</c> along z.
        ///
        /// Exposed because it is what the tiling rule is asserted against: a test can check every
        /// variant's rim without having to unpick a triangle list to do it.
        /// </summary>
        public static Vector3[] Top(int variant)
        {
            var points = new Vector3[9];
            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                float x = -0.5f + 0.5f * i;
                float z = -0.5f + 0.5f * j;

                // A salt per axis per corner, so the drop, the lean and the drift of one vertex
                // are independent of each other and of its neighbours'.
                uint salt = (uint)(variant * 977 + j * 31 + i) + 1u;

                // One tilt decided per variant and applied across the whole face, plus per-vertex
                // noise on top of it. The tilt is what makes a lump a wedge rather than a block
                // with nibbled corners.
                float tiltX = (Unit((uint)variant + 1u, 0x6666u) - 0.5f) * 2f * MaxTilt;
                float tiltZ = (Unit((uint)variant + 1u, 0x7777u) - 0.5f) * 2f * MaxTilt;
                float tilt = tiltX * (i - 1) + tiltZ * (j - 1);

                float drop = (Unit(salt, 0x1111u) * MaxDrop + Mathf.Max(0f, tilt)) * DropWeight(i, j);

                if (i == 0) x -= Unit(salt, 0x2222u) * MaxBulge;
                else if (i == 2) x += Unit(salt, 0x2222u) * MaxBulge;
                else x += (Unit(salt, 0x4444u) - 0.5f) * 2f * MaxDrift * Interior(j);

                if (j == 0) z -= Unit(salt, 0x3333u) * MaxBulge;
                else if (j == 2) z += Unit(salt, 0x3333u) * MaxBulge;
                else z += (Unit(salt, 0x5555u) - 0.5f) * 2f * MaxDrift * Interior(i);

                points[j * 3 + i] = new Vector3(x, 0.5f - drop, z);
            }

            return points;
        }

        /// <summary>
        /// The half-height vertices, one per plan position, in the same layout as
        /// <see cref="Top"/>. Only the eight rim entries are used; the middle is filler.
        ///
        /// <para><b>One vertex per corner, shared by both walls that meet there.</b> This is the
        /// whole reason the method exists. The first version let each wall bulge its own ends, so
        /// at a corner the -z wall pushed its half-height vertex backward while the +x wall pushed
        /// its one sideways — the two walls stopped meeting and a triangular sliver of daylight
        /// opened up the corner, tapering to nothing at the base and the rim. It is the exact
        /// crack the no-inward rule is supposed to make impossible, and the rule did not prevent
        /// it because both vertices had moved strictly outward; they had simply moved outward in
        /// different directions.</para>
        ///
        /// <para>So the bulge is a property of the <em>position</em>, not of the wall asking about
        /// it: one direction (the diagonal at a corner, the face normal along an edge), one
        /// amount, one point. Both walls then read the same vertex and share the whole edge.</para>
        /// </summary>
        public static Vector3[] Mid(int variant)
        {
            Vector3[] top = Top(variant);
            var points = new Vector3[9];

            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                var atBase = new Vector3(-0.5f + 0.5f * i, -0.5f, -0.5f + 0.5f * j);
                Vector3 half = Vector3.Lerp(atBase, top[j * 3 + i], 0.55f);

                var outward = new Vector3(i == 0 ? -1f : i == 2 ? 1f : 0f, 0f, j == 0 ? -1f : j == 2 ? 1f : 0f);
                if (outward.sqrMagnitude > 0f)
                {
                    uint salt = (uint)(variant * 613 + j * 29 + i) + 1u;
                    half += outward.normalized * (Unit(salt, 0x8888u) * MaxBulge);
                }

                points[j * 3 + i] = half;
            }

            return points;
        }

        /// <summary>
        /// 1 for the vertex in the middle of the face, 0 for one in the middle of a rim edge.
        ///
        /// A mid-rim vertex must not drift along its own edge either. It looks harmless — the
        /// vertex stays on the seam plane — but sliding it sideways drags the two triangles that
        /// share it, and the wall below it, off the corner they are supposed to meet at, which
        /// pinches a sliver of daylight into the joint. Only the one true interior vertex is free.
        /// </summary>
        static float Interior(int other) => other == 1 ? 1f : 0f;

        /// <summary>
        /// How much of the drop a top vertex takes. The rim takes all of it; the one vertex in the
        /// middle of the face takes almost none.
        ///
        /// <para><b>Because the middle of the cell is where things stand.</b> A colonist, an item
        /// and a stack of stone are all drawn at the cell centre, and the floor of a mined tunnel
        /// is the top of the rock below it. Chipping that centre down by the full 42 cm would
        /// leave a colonist hovering over the floor of its own mine — the same fault as rock
        /// growing through somebody's feet, just the other way up. Dropping the rim and leaving
        /// the middle alone gives the silhouette its broken skyline, which is what the eye reads,
        /// while the surface anything stands on stays where the simulation says it is.</para>
        /// </summary>
        static float DropWeight(int i, int j) => i == 1 && j == 1 ? 0.1f : 1f;

        static Mesh Build(int variant)
        {
            Vector3[] top = Top(variant);
            Vector3[] mid = Mid(variant);

            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uvs = new List<Vector2>(96);
            var triangles = new List<int>(144);

            // ---- the top, as four facets ----------------------------------------------------
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                // Anticlockwise about +y: (i,j), (i+1,j), (i+1,j+1), (i,j+1). Taking them the
                // other way round points the facet at the ground.
                Vector3 a = top[j * 3 + i];
                Vector3 b = top[j * 3 + i + 1];
                Vector3 c = top[(j + 1) * 3 + i + 1];
                Vector3 d = top[(j + 1) * 3 + i];
                AddQuad(vertices, normals, uvs, triangles, a, b, c, d, PlanarTop);
            }

            // ---- the four walls, two facets each, leaning out to meet the rim ---------------
            //
            // Each wall is driven by the three top vertices along its own edge, so the top of the
            // wall is exactly the jagged line the top surface ends at and the two never part.
            const float baseY = -0.5f;
            for (int i = 0; i < 2; i++)
            {
                // -z wall, outward normal towards -z
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(-0.5f + 0.5f * i, baseY, -0.5f),
                    new Vector3(-0.5f + 0.5f * (i + 1), baseY, -0.5f),
                    top[i], top[i + 1], mid[i], mid[i + 1]);

                // +z wall
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(-0.5f + 0.5f * (i + 1), baseY, 0.5f),
                    new Vector3(-0.5f + 0.5f * i, baseY, 0.5f),
                    top[6 + i + 1], top[6 + i], mid[6 + i + 1], mid[6 + i]);

                // -x wall
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(-0.5f, baseY, -0.5f + 0.5f * (i + 1)),
                    new Vector3(-0.5f, baseY, -0.5f + 0.5f * i),
                    top[(i + 1) * 3], top[i * 3], mid[(i + 1) * 3], mid[i * 3]);

                // +x wall
                AddWall(vertices, normals, uvs, triangles,
                    new Vector3(0.5f, baseY, -0.5f + 0.5f * i),
                    new Vector3(0.5f, baseY, -0.5f + 0.5f * (i + 1)),
                    top[i * 3 + 2], top[(i + 1) * 3 + 2], mid[i * 3 + 2], mid[(i + 1) * 3 + 2]);
            }

            // ---- the base, flat and the exact cell footprint ---------------------------------
            //
            // In four facets, not one, and the reason is a T-junction rather than looks. The walls
            // stand their feet on the mid-edge points, so a single base quad spanning the whole
            // edge matches neither half of the wall above it: the edge belongs to one facet where
            // it should belong to two, and the lump is not a closed solid. It would not have shown
            // as daylight — the base is buried against the cell below — which is exactly why it
            // wanted a test rather than an eye.
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                float x0 = -0.5f + 0.5f * i, x1 = -0.5f + 0.5f * (i + 1);
                float z0 = -0.5f + 0.5f * j, z1 = -0.5f + 0.5f * (j + 1);
                AddQuad(vertices, normals, uvs, triangles,
                    new Vector3(x0, baseY, z0),
                    new Vector3(x0, baseY, z1),
                    new Vector3(x1, baseY, z1),
                    new Vector3(x1, baseY, z0),
                    PlanarTop);
            }

            var mesh = new Mesh { name = "Odyssey/RockBlock" + variant };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A wall from a base edge up to two top vertices, in two courses.
        ///
        /// <para>The middle course is the point. A wall built as one flat quad from base to rim is
        /// a vertical plane however much its top edge wanders, and a face made of vertical planes
        /// reads as masonry no matter what the skyline does — which is exactly what the first
        /// attempt at this looked like. A vertex half way up, pushed out, gives the wall a
        /// profile: it swells and undercuts like weathered stone.</para>
        ///
        /// <para>The two half-height vertices are handed in rather than worked out here, because
        /// they belong to the plan position and are shared with the wall around the corner. See
        /// <see cref="Mid"/> for the crack that taught us so.</para>
        /// </summary>
        static void AddWall(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 baseLeft, Vector3 baseRight, Vector3 topLeft, Vector3 topRight,
            Vector3 midLeft, Vector3 midRight)
        {
            AddQuad(vertices, normals, uvs, triangles, baseLeft, baseRight, midRight, midLeft, PlanarSide);
            AddQuad(vertices, normals, uvs, triangles, midLeft, midRight, topRight, topLeft, PlanarSide);
        }

        /// <summary>
        /// One flat facet. The four corners are given anticlockwise about the outward normal, and
        /// the winding below turns them into two triangles that face that way.
        ///
        /// <para>The winding is 0-2-1 and 0-3-2, which is the opposite of the obvious order and is
        /// the same convention <see cref="PrimitiveMeshes.UnitCube"/> uses. Taking the corners in
        /// their given order instead builds triangles facing *inward*, which is perfectly valid
        /// geometry that renders as a hole — see the note in that file, where it cost a while.</para>
        /// </summary>
        static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            System.Func<Vector3, Vector3, Vector2> uv)
        {
            Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
            int start = vertices.Count;

            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            uvs.Add(uv(a, normal)); uvs.Add(uv(b, normal)); uvs.Add(uv(c, normal)); uvs.Add(uv(d, normal));

            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        /// <summary>
        /// Planar UVs, one repeat per cell, because the catalogue rows ask for one tile per cell
        /// and a stretched tile on a leaning wall reads as a smear rather than as stone.
        /// </summary>
        static Vector2 PlanarTop(Vector3 point, Vector3 normal) =>
            new Vector2(point.x + 0.5f, point.z + 0.5f);

        static Vector2 PlanarSide(Vector3 point, Vector3 normal)
        {
            // Project onto whichever horizontal axis the wall runs along, so the texture runs
            // across the face rather than being squashed towards its ends.
            float across = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? point.z : point.x;
            return new Vector2(across + 0.5f, point.y + 0.5f);
        }

        /// <summary>A stable 0..1 from a salt pair. The same avalanche <see cref="GroundScatter"/> uses.</summary>
        static float Unit(uint a, uint b) => (GroundScatter.Hash((int)a, (int)b, 0x9E3779B9u) & 0xFFFFFFu) * (1f / 0x1000000);
    }
}
