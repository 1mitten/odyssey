#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Jagged stone blocks, built in code, that still fill a grid solidly.
    ///
    /// <para><b>The problem.</b> A rock cell was a cube, so an outcrop read as a stack of
    /// mud-bricks. What it wants is a chipped, faceted lump — but a lump carved freely out of a
    /// cell opens gaps against its neighbours, and a gap in a cliff face lets daylight through the
    /// world, which is far worse than a flat face.</para>
    ///
    /// <para><b>The rule: a lump must cover its whole cell box.</b> It may bulge outward past the
    /// box as much as it likes — neighbouring rock overlaps, which is invisible — but it may never
    /// fail to reach any part of it. Everything below follows from that one sentence:</para>
    /// <list type="bullet">
    /// <item><b>No vertex moves inward</b>, so the four sides always reach the seam planes.</item>
    /// <item><b>The lump hangs below its cell</b> by <see cref="Skirt"/>, which is deeper than any
    /// top can drop. This is the part the first two attempts got wrong. A dished top means a cell
    /// does <em>not</em> fill its own box, so a column of rock had a horizontal void at every layer
    /// boundary — and wherever the neighbours' rims had dropped too, that void was open sideways
    /// and you could see straight through the stack. The skirt of the cell above fills the dish of
    /// the cell below, so the column is solid again and the skyline stays broken.</item>
    /// <item><b>The skirt itself never bulges.</b> It sits at the exact footprint, because it hangs
    /// into the cell below, and a lip poking sideways out of <em>that</em> cell would be visible
    /// wherever the cell beside it happens to be open.</item>
    /// </list>
    ///
    /// <para>Three things stop it reading as masonry: the rim drops at each of its eight points, so
    /// a run of rock has a broken skyline; each variant carries a <see cref="MaxTilt"/> across its
    /// whole face, so one lump is a wedge and the next leans the other way; and each wall is built
    /// in courses whose corners step in and out, so a vertical corner is a broken line rather than
    /// the ruled edge of a cut block.</para>
    ///
    /// <para>Nothing is ever raised above the cell top, because the cell above a rock is where a
    /// colonist stands and rock growing through somebody's feet is a worse fault than a straight
    /// edge.</para>
    /// </summary>
    public static class RockMesh
    {
        /// <summary>How many distinct lumps are built. Each is one more instancing bucket.</summary>
        public const int Variants = 6;

        /// <summary>The most a rim vertex drops, as a fraction of the cell's height (3 m): 60 cm.</summary>
        public const float MaxDrop = 0.20f;

        /// <summary>
        /// How far a lump may tilt across its own face, as a fraction of the cell's height.
        ///
        /// Per-vertex noise alone chips a block evenly all round, which averages out into
        /// something that still reads as a block. A tilt is a single decision applied across the
        /// whole face, so one variant comes out as a wedge and another leans the other way.
        /// </summary>
        public const float MaxTilt = 0.14f;

        /// <summary>
        /// How far the lump hangs below its own cell, as a fraction of the cell's height.
        ///
        /// <b>Must exceed <see cref="MaxDrop"/> + <see cref="MaxTilt"/>,</b> which is the deepest a
        /// top can ever be chipped. That inequality is the whole guarantee that a stack of rock is
        /// solid, and <c>RockMeshTests</c> asserts it rather than leaving it to whoever next tunes
        /// one of these numbers.
        /// </summary>
        public const float Skirt = 0.42f;

        /// <summary>The most a vertex leans out, as a fraction of the cell's width (2.5 m): 22 cm.</summary>
        public const float MaxBulge = 0.09f;

        /// <summary>How far the one interior top vertex may wander. It is on no seam, so it is free.</summary>
        public const float MaxDrift = 0.06f;

        /// <summary>Heights of the two intermediate courses, as a fraction of base to rim.</summary>
        static readonly float[] CourseHeights = { 0.34f, 0.68f };

        /// <summary>How many courses of wall a lump is built in, counting the skirt.</summary>
        public static int CourseCount => CourseHeights.Length;

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
        /// </summary>
        public static Vector3[] Top(int variant)
        {
            var points = new Vector3[9];

            // One tilt per variant, decided once and applied across the whole face.
            float tiltX = (Unit((uint)variant + 1u, 0x6666u) - 0.5f) * 2f * MaxTilt;
            float tiltZ = (Unit((uint)variant + 1u, 0x7777u) - 0.5f) * 2f * MaxTilt;

            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                float x = -0.5f + 0.5f * i;
                float z = -0.5f + 0.5f * j;

                uint salt = (uint)(variant * 977 + j * 31 + i) + 1u;
                float tilt = Mathf.Max(0f, tiltX * (i - 1) + tiltZ * (j - 1));
                float drop = (Unit(salt, 0x1111u) * MaxDrop + tilt) * DropWeight(i, j);

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
        /// One intermediate course of wall vertices, at <paramref name="course"/> 0 or 1, in the
        /// same nine-position layout as <see cref="Top"/>. Only the eight rim entries are used.
        ///
        /// <para><b>One vertex per position, shared by both walls that meet at a corner.</b> This
        /// is the whole reason courses are computed here rather than inside each wall. The first
        /// version let each wall bulge its own ends, so at a corner the -z wall pushed its vertex
        /// backward while the +x wall pushed its one sideways — both moved strictly outward, as
        /// the rule demands, and the two walls still stopped meeting, opening a triangular sliver
        /// of daylight up the corner. Moving outward is not enough; they have to move outward
        /// <em>together</em>. So the bulge belongs to the position: one direction (the diagonal at
        /// a corner, the face normal along an edge), one amount, one point.</para>
        ///
        /// <para>Two courses rather than one, because a corner has to be a broken line. A wall of
        /// one flat course meets its neighbour in a ruled vertical edge, which is the silhouette
        /// of a cut block and never of a rock.</para>
        /// </summary>
        public static Vector3[] Course(int variant, int course)
        {
            Vector3[] top = Top(variant);
            var points = new Vector3[9];
            float height = CourseHeights[course];

            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                var atBase = new Vector3(-0.5f + 0.5f * i, -0.5f, -0.5f + 0.5f * j);
                Vector3 point = Vector3.Lerp(atBase, top[j * 3 + i], height);

                var outward = new Vector3(i == 0 ? -1f : i == 2 ? 1f : 0f, 0f, j == 0 ? -1f : j == 2 ? 1f : 0f);
                if (outward.sqrMagnitude > 0f)
                {
                    uint salt = (uint)(variant * 613 + course * 271 + j * 29 + i) + 1u;
                    point += outward.normalized * (Unit(salt, 0x8888u) * MaxBulge);
                }

                points[j * 3 + i] = point;
            }

            return points;
        }

        /// <summary>
        /// 1 for the vertex in the middle of the face, 0 for one in the middle of a rim edge.
        ///
        /// A mid-rim vertex must not drift along its own edge either. It looks harmless — the
        /// vertex stays on the seam plane — but sliding it sideways drags the facets that share it
        /// off the corner they are supposed to meet at, which pinches daylight into the joint.
        /// </summary>
        static float Interior(int other) => other == 1 ? 1f : 0f;

        /// <summary>
        /// How much of the drop a top vertex takes. The rim takes all of it; the one vertex in the
        /// middle of the face takes almost none.
        ///
        /// <para><b>Because the middle of the cell is where things stand.</b> A colonist, an item
        /// and a stack of stone are all drawn at the cell centre, and the floor of a mined tunnel
        /// is the top of the rock below it. Chipping that centre down by the full drop would leave
        /// a colonist hovering over the floor of its own mine — the same fault as rock growing
        /// through somebody's feet, just the other way up.</para>
        /// </summary>
        static float DropWeight(int i, int j) => i == 1 && j == 1 ? 0.1f : 1f;

        /// <summary>The nine plan positions at one height, at the exact cell footprint.</summary>
        static Vector3[] Ring(float y)
        {
            var points = new Vector3[9];
            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                points[j * 3 + i] = new Vector3(-0.5f + 0.5f * i, y, -0.5f + 0.5f * j);
            return points;
        }

        /// <summary>Every ring of the lump, bottom to top. A wall is one quad stitched between two.</summary>
        public static Vector3[][] Rings(int variant) => new[]
        {
            Ring(-0.5f - Skirt),
            Ring(-0.5f),
            Course(variant, 0),
            Course(variant, 1),
            Top(variant),
        };

        static Mesh Build(int variant)
        {
            Vector3[][] rings = Rings(variant);

            var vertices = new List<Vector3>(256);
            var normals = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var triangles = new List<int>(384);

            // ---- the top, as four facets ----------------------------------------------------
            Vector3[] top = rings[rings.Length - 1];
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                // Anticlockwise about +y. Taking them the other way points the facet at the ground.
                AddQuad(vertices, normals, uvs, triangles,
                    top[j * 3 + i], top[j * 3 + i + 1], top[(j + 1) * 3 + i + 1], top[(j + 1) * 3 + i],
                    PlanarTop);
            }

            // ---- the four walls, one course at a time ---------------------------------------
            for (int r = 0; r + 1 < rings.Length; r++)
            {
                Vector3[] lower = rings[r];
                Vector3[] upper = rings[r + 1];

                for (int i = 0; i < 2; i++)
                {
                    // -z wall, outward normal towards -z
                    AddQuad(vertices, normals, uvs, triangles,
                        lower[i], lower[i + 1], upper[i + 1], upper[i], PlanarSide);

                    // +z wall
                    AddQuad(vertices, normals, uvs, triangles,
                        lower[6 + i + 1], lower[6 + i], upper[6 + i], upper[6 + i + 1], PlanarSide);

                    // -x wall
                    AddQuad(vertices, normals, uvs, triangles,
                        lower[(i + 1) * 3], lower[i * 3], upper[i * 3], upper[(i + 1) * 3], PlanarSide);

                    // +x wall
                    AddQuad(vertices, normals, uvs, triangles,
                        lower[i * 3 + 2], lower[(i + 1) * 3 + 2], upper[(i + 1) * 3 + 2], upper[i * 3 + 2],
                        PlanarSide);
                }
            }

            // ---- the foot of the skirt ------------------------------------------------------
            //
            // In four facets, not one, and the reason is a T-junction rather than looks. The walls
            // stand on the mid-edge points, so a single quad spanning the whole edge matches
            // neither half of the wall above it: the edge belongs to one facet where it should
            // belong to two, and the lump is not a closed solid. It would never have shown as
            // daylight — the foot is buried in the cell below — which is why it wanted a test
            // rather than an eye.
            float footY = -0.5f - Skirt;
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                float x0 = -0.5f + 0.5f * i, x1 = -0.5f + 0.5f * (i + 1);
                float z0 = -0.5f + 0.5f * j, z1 = -0.5f + 0.5f * (j + 1);
                AddQuad(vertices, normals, uvs, triangles,
                    new Vector3(x0, footY, z0), new Vector3(x0, footY, z1),
                    new Vector3(x1, footY, z1), new Vector3(x1, footY, z0),
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
            Vector3 normal = Vector3.Cross(c - a, b - a);
            normal = normal.sqrMagnitude < 1e-12f ? Vector3.up : normal.normalized;
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
            float across = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? point.z : point.x;
            return new Vector2(across + 0.5f, point.y + 0.5f);
        }

        /// <summary>A stable 0..1 from a salt pair. The same avalanche <see cref="GroundScatter"/> uses.</summary>
        static float Unit(uint a, uint b) => (GroundScatter.Hash((int)a, (int)b, 0x9E3779B9u) & 0xFFFFFFu) * (1f / 0x1000000);
    }
}
