#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Earth with a surface: uneven ground that still tiles, and terrace faces that are not cast
    /// planes.
    ///
    /// <para><b>The problem.</b> A ground cell is one 2.5 x 3.0 x 2.5 box, so every top face is a
    /// flat quad at exactly the layer height and every terrace riser is a single unbroken
    /// rectangle 3 m tall. <see cref="GroundRelief"/> fixed the first of those at the scale of the
    /// whole board — it tilts each cell onto the tangent plane of a rolling field — but a tilted
    /// flat quad is still a flat quad, and the relief says nothing at all about the risers, which
    /// are the thing that reads as masonry.</para>
    ///
    /// <para><b>Stone already solved this</b> (<see cref="RockMesh"/>) and earth cannot simply
    /// borrow the answer, for one reason that decides most of what is below: <b>the middle of a
    /// ground cell is where everything stands</b>. A colonist, a stack of wood, a tree, a bed and
    /// the selection cursor are all drawn at <see cref="CellMetrics.FloorCentre"/>. Rock may chip
    /// its whole top face down because the thing standing on a rock stands on the cell
    /// <em>above</em> it; ground has no such freedom, and a dished meadow would hover every
    /// colonist on it. So the centre vertex is pinned, exactly, and only the rim moves.</para>
    ///
    /// <para><b>The rim may rise as well as fall</b>, which is the other departure. Rock only ever
    /// chips downward, because rock growing up through somebody's feet is worse than a straight
    /// edge. At the rim, 1.25 m from where anything stands, nothing is in the way — and the
    /// freedom matters: a rim that can only drop gives every cell the same shallow bowl, and a
    /// field of identical bowls is a waffle, which is a more obviously artificial pattern than the
    /// flat quads it replaced. Two neighbours disagreeing across their shared edge leave a few
    /// centimetres of one cell's side face showing, never a hole, because a cell carries all four
    /// walls whether or not anything can see them.</para>
    ///
    /// <para><b>Two meshes, and that is a performance decision.</b> On flat ground a surface
    /// cell's four same-layer neighbours are solid too, so only its top is ever seen; sides appear
    /// at terrace risers, at the rim of the board and at mined faces, which together are a small
    /// fraction of the cells drawn. <see cref="Turf"/> is the cheap common case — a rippled top on
    /// plain walls. <see cref="Face"/> pays for coursed walls, and only cells that show one get
    /// it. Both span the same unit box, so the placement arithmetic is identical for either and
    /// the mesher chooses between them with no other consequence.</para>
    ///
    /// <para><b>No vertex moves in plan.</b> Rock is free to bulge sideways because neighbouring
    /// rock overlaps invisibly; earth meets air at a riser, where a bulge would hang over the
    /// terrace below. The walls of <see cref="Turf"/> therefore stand exactly on the cell
    /// boundary, and only <see cref="Face"/>'s courses step out — by a few centimetres, into air
    /// that is always either the open side of a riser or another cell's solid interior.</para>
    /// </summary>
    public static class GroundMesh
    {
        /// <summary>
        /// How many distinct tops are built. Each is one more instancing bucket per terrain.
        ///
        /// <para>Deliberately small, because ground is the largest instance population in the
        /// world and a bucket is a draw call per chunk. Four right-angle bearings come free on top
        /// of this — a square footprint is unchanged by a quarter turn and the turn rides in the
        /// instance matrix — so two tops are eight looks for two buckets. See
        /// <see cref="GroundLook.Yaw"/>.</para>
        ///
        /// <para>One would cost nothing at all over today and still give four looks. Two is the
        /// starting point because it exercises the variant machinery and because the measurement
        /// that decides it has to be made against something; if the frame time objects, this is
        /// the number to turn down.</para>
        /// </summary>
        public const int Variants = 2;

        /// <summary>
        /// How many turf meshes are actually built, which is one when there is nothing to vary.
        ///
        /// <para>With <see cref="MaxRipple"/> at zero every turf variant is the same flat-topped
        /// block, and two identical meshes are two instancing buckets and two draw calls a chunk
        /// for no difference on screen. Ground is the largest instance population in the world, so
        /// that is not a rounding error. Faces are unaffected: their courses are hashed
        /// independently of the ripple, so they differ whatever it is set to.</para>
        /// </summary>
        public static int TurfVariants => MaxRipple > 0f ? Variants : 1;

        /// <summary>
        /// How far a rim vertex moves, up or down, as a fraction of the cell's height.
        /// <b>Zero, and it ships that way because it was measured and it lost.</b>
        ///
        /// <para><b>What it was for.</b> The owner asked for uneven grass, and moving each cell's
        /// rim is the only way a mesh reused by every cell can offer any. It was tried at 12 cm and
        /// then at 3.6 cm.</para>
        ///
        /// <para><b>Why it is off.</b> Cells pick their tops independently, so two neighbours can
        /// disagree across a shared edge by <em>twice</em> whatever this is. The step is filled by
        /// the taller cell's own side wall — nothing is missing — but that wall is vertical and
        /// catches almost no light, so every cell boundary on the board came out as a dark dash and
        /// the meadow read as crazy paving. <c>GroundSeamTests</c> weighed the two contributions:
        /// the relief field's own parting is <b>14.7 mm</b> at the shipped amplitude, and the
        /// ripple at 3.6 cm added <b>72 mm</b> on top of it — five times the artefact for a benefit
        /// no photograph could find. The owner reported not seeing it on `main`, which has no
        /// <see cref="GroundMesh"/> at all; that is the feature being absent rather than evidence
        /// about its cause, and the measurement is what settled it.</para>
        ///
        /// <para>It is also worth recording that the top surface was never the complaint.
        /// <see cref="GroundRelief"/> already rolls it at a scale where neighbours are tangent
        /// planes of one smooth field. The complaint was the riser, and adding per-cell noise to
        /// the part that was working traded a good surface for a bad one while every test passed.</para>
        ///
        /// <para><b>The ceiling on the approach, for whoever raises it next.</b> No amount of
        /// tuning makes a per-cell ripple continuous. Genuinely uneven ground at cell scale needs
        /// the rim height to be a function of the <em>shared corner's world position</em>, which a
        /// mesh reused by every cell cannot express and a vertex shader can. That is the piece of
        /// work to do, not a bigger number here.</para>
        ///
        /// <para>It survives as a lever rather than being deleted so the contact sheet can keep
        /// showing what it costs. Moving it needs an explicit <see cref="Invalidate"/> and a fresh
        /// library, like <see cref="SideNormalTiltDegrees"/>.</para>
        /// </summary>
        public static float MaxRipple { get; set; } = 0f;

        /// <summary>
        /// How far a course of a terrace face steps out, as a fraction of the cell's width: 12 cm.
        ///
        /// <para>It steps <em>out</em>, never in. An inward step recedes past whatever is behind
        /// it, and what is behind a riser may be a cell the mesher culled for being buried — which
        /// would be a hole through the world to the sky, the exact fault
        /// <see cref="RockMesh"/> forbids inward motion to prevent. Outward is always safe: it
        /// reaches either into the open air in front of the riser or into a neighbour's solid
        /// interior.</para>
        /// </summary>
        public const float MaxBulge = 0.05f;

        /// <summary>
        /// How far the block hangs below its own cell, as a fraction of the cell's height: 30 cm.
        ///
        /// <para><b>Must exceed <see cref="MaxRipple"/>,</b> and for the reason
        /// <see cref="RockMesh.Skirt"/> records at length. A rim that dips means the cell does not
        /// fill its own box, so a column of earth would carry a horizontal slot at every layer
        /// boundary; wherever the cell beside it is open — which at a terrace riser is exactly
        /// where anyone is looking — that slot is a dark line ruled along the face. The skirt of
        /// the cell above fills the dish of the cell below. <c>GroundMeshTests</c> asserts the
        /// inequality rather than leaving it to whoever next tunes one of these numbers.</para>
        /// </summary>
        public const float Skirt = 0.10f;

        /// <summary>
        /// How far a side face's shading normal is tilted up towards the sky, in degrees.
        ///
        /// <para><b>This is the fix for the black lines between tiles, and it is a lighting fix
        /// rather than a geometry one because the lines were never holes.</b> Every cell is drawn
        /// as its own box sheared onto the tangent plane of the relief field, so where two
        /// neighbouring planes disagree — a few centimetres, by <c>GroundSeamTests</c> — the taller
        /// cell's own side wall fills the step. Nothing is missing. But that wall is vertical, the
        /// sun sits at 72 degrees and terrain casts no shadows, so it receives almost nothing and
        /// reads as a black hairline drawn round every cell on the board.</para>
        ///
        /// <para>Tilting the shading normal up decouples how a face is lit from how it is placed.
        /// The sliver then shades like the ground it sits between instead of like the side of a
        /// building, and the seam stops being a line. It costs nothing at all: no extra vertex, no
        /// extra triangle, no extra draw call and no shader change — only different numbers in a
        /// buffer the mesh already had.</para>
        ///
        /// <para><b>It is a lever because it trades against readability.</b> The same tilt also
        /// lifts a 3 m terrace riser towards the colour of the ground above and below it, and past
        /// some angle you can no longer see at a glance where the ground changes level, which is
        /// information a player needs. Zero is exactly the old normals. The owner picks the number
        /// off a contact sheet; it is not a thing to reason out.</para>
        ///
        /// <para>The normal keeps its horizontal bearing, so a face still lights from the side it
        /// faces. Turning that as well would light a wall from behind, which is a worse fault than
        /// the one being fixed.</para>
        ///
        /// <para><b>Changing it does not rebuild anything by itself</b>, and that is deliberate
        /// rather than an omission. A <see cref="ModuleLibrary"/> holds the built meshes by
        /// reference, so destroying them under a live one leaves it pointing at nothing and the
        /// ground silently stops drawing. A caller that moves this calls <see cref="Invalidate"/>
        /// and then builds a fresh library, which is what the check harness already does once per
        /// condition for its own reasons.</para>
        /// </summary>
        public static float SideNormalTiltDegrees { get; set; } = 38f;

        static float Tilt => SideNormalTiltDegrees;

        /// <summary>
        /// Throw away the built meshes so the next request rebuilds them.
        ///
        /// <para>Only the tuning levers need this, and in practice only the check harness moves
        /// them. The meshes are destroyed rather than dropped: a <c>Mesh</c> made in code is a GPU
        /// allocation Unity never collects, and a harness that sweeps a dozen settings would
        /// otherwise leak one set per setting — the same fault that once took the graphics device
        /// down through <c>ModuleLibrary</c>.</para>
        /// </summary>
        public static void Invalidate()
        {
            Release(TurfCache);
            Release(FaceCache);
        }

        /// <summary>
        /// Put the tuning levers back to what the game ships with, and rebuild.
        ///
        /// For tests, which must not inherit each other's tuning — the same service
        /// <c>GroundRelief.Reset</c> and <c>Footing.Reset</c> provide, and needed for the same
        /// reason now that these are levers a contact sheet sweeps rather than constants.
        /// </summary>
        public static void ResetLevers()
        {
            MaxRipple = 0f;
            SideNormalTiltDegrees = 38f;
            Invalidate();
            BankMesh.Invalidate();
        }

        static void Release(Mesh?[] cache)
        {
            for (int i = 0; i < cache.Length; i++)
            {
                Mesh? mesh = cache[i];
                if (mesh == null) continue;
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
                cache[i] = null;
            }
        }

        /// <summary>
        /// The shading normal for a face, given the way the face actually points.
        ///
        /// Horizontal faces are tilted up by <see cref="SideNormalTiltDegrees"/>; anything already
        /// pointing up or down is left exactly alone, because tilting a top face would light the
        /// whole board as though the sun were somewhere it is not.
        /// </summary>
        public static Vector3 ShadingNormal(Vector3 geometric)
        {
            if (Mathf.Abs(geometric.y) > 0.1f) return geometric;

            float tilt = Mathf.Clamp(Tilt, 0f, 89f);
            if (tilt <= 0f) return geometric;

            float radians = tilt * Mathf.Deg2Rad;
            var flat = new Vector3(geometric.x, 0f, geometric.z);
            if (flat.sqrMagnitude < 1e-8f) return geometric;

            return (flat.normalized * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians)).normalized;
        }

        /// <summary>Heights of the two intermediate courses on a face, as a fraction of base to rim.</summary>
        static readonly float[] CourseHeights = { 0.38f, 0.72f };

        /// <summary>How many courses of wall a face is built in.</summary>
        public static int CourseCount => CourseHeights.Length;

        static readonly Mesh?[] TurfCache = new Mesh?[Variants];
        static readonly Mesh?[] FaceCache = new Mesh?[Variants];

        /// <summary>
        /// Ground whose sides are never seen: a rippled top on plain vertical walls.
        ///
        /// This is what nearly every drawn ground cell gets, so it is kept as close to the cube it
        /// replaces as the shape allows — the walls are single spans rather than coursed, and the
        /// base is one quad.
        /// </summary>
        public static Mesh Turf(int variant) => Cached(TurfCache, variant, coursed: false);

        /// <summary>
        /// Ground that shows a side: the same top, over walls built in courses that step out, so a
        /// 3 m riser is a broken face rather than one ruled rectangle.
        /// </summary>
        public static Mesh Face(int variant) => Cached(FaceCache, variant, coursed: true);

        static Mesh Cached(Mesh?[] cache, int variant, bool coursed)
        {
            int index = Wrap(variant);
            Mesh? mesh = cache[index];
            if (mesh != null) return mesh;
            return cache[index] = Build(index, coursed);
        }

        static int Wrap(int variant) => ((variant % Variants) + Variants) % Variants;

        /// <summary>
        /// The nine top-surface vertices of a variant, in the unit cube's own space, laid out row
        /// by row: <c>Top(variant)[j * 3 + i]</c> for <c>i</c> along x and <c>j</c> along z.
        ///
        /// <para>Only the height varies. Nothing moves in plan, so the footprint is exactly the
        /// cell and two neighbours cannot open a gap between them however differently they ripple.
        /// The centre, <c>[4]</c>, is pinned at the cell top: it is where everything stands.</para>
        /// </summary>
        public static Vector3[] Top(int variant)
        {
            int v = Wrap(variant);
            var points = new Vector3[9];

            // One tilt per variant, decided once and laid across the whole face, so one variant is
            // a shallow wedge and the next leans the other way. Per-vertex noise on its own chips
            // a cell evenly all round, and evenly all round averages back out into flat.
            float tiltX = (Unit((uint)v + 1u, 0x3131u) - 0.5f) * 2f * MaxRipple;
            float tiltZ = (Unit((uint)v + 1u, 0x4242u) - 0.5f) * 2f * MaxRipple;

            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                float x = -0.5f + 0.5f * i;
                float z = -0.5f + 0.5f * j;

                float rise;
                if (i == 1 && j == 1)
                {
                    // Pinned. Everything in the world is drawn standing on this point.
                    rise = 0f;
                }
                else
                {
                    uint salt = (uint)(v * 811 + j * 23 + i) + 1u;
                    float wander = (Unit(salt, 0x1717u) - 0.5f) * 2f * MaxRipple;
                    float tilt = tiltX * (i - 1) + tiltZ * (j - 1);
                    rise = Mathf.Clamp(wander + tilt, -MaxRipple, MaxRipple);
                }

                points[j * 3 + i] = new Vector3(x, 0.5f + rise, z);
            }

            return points;
        }

        /// <summary>
        /// One intermediate course of a face, at <paramref name="course"/> 0 or 1, in the same
        /// nine-position layout as <see cref="Top"/>. Only the eight rim entries are used.
        ///
        /// <para><b>One vertex per rim position, shared by both walls that meet at a corner.</b>
        /// This is the whole reason a course is computed here rather than inside each wall, and
        /// <see cref="RockMesh.Course"/> records what happens otherwise: two walls that each bulge
        /// their own end of a shared corner both move strictly outward, as required, and still
        /// stop meeting — because they move outward in different directions. The bulge belongs to
        /// the position, so it is one direction, one amount, one point.</para>
        /// </summary>
        public static Vector3[] Course(int variant, int course)
        {
            int v = Wrap(variant);
            Vector3[] top = Top(v);
            var points = new Vector3[9];
            float height = CourseHeights[course];

            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                var atBase = new Vector3(-0.5f + 0.5f * i, -0.5f, -0.5f + 0.5f * j);
                Vector3 point = Vector3.Lerp(atBase, top[j * 3 + i], height);

                var outward = new Vector3(
                    i == 0 ? -1f : i == 2 ? 1f : 0f, 0f,
                    j == 0 ? -1f : j == 2 ? 1f : 0f);

                if (outward.sqrMagnitude > 0f)
                {
                    uint salt = (uint)(v * 577 + course * 149 + j * 19 + i) + 1u;
                    point += outward.normalized * (Step(salt) * MaxBulge);
                }

                points[j * 3 + i] = point;
            }

            return points;
        }

        /// <summary>
        /// The height of a variant's top surface at a point inside the cell, in the mesh's own
        /// space, where <paramref name="u"/> and <paramref name="v"/> run -0.5 to 0.5.
        ///
        /// <para>What a tuft of grass asks, so that it neither floats over a dip nor is buried to
        /// its neck in a rise. Bilinear across the same four facets the mesh is built from, so the
        /// answer is exactly the surface that is drawn rather than an approximation of it.</para>
        /// </summary>
        public static float HeightAtLocal(int variant, float u, float v)
        {
            Vector3[] top = Top(variant);

            // Which of the four facets, and where inside it. Clamped rather than wrapped: a caller
            // asking outside the cell wants the edge, not the far side.
            float fx = Mathf.Clamp(u + 0.5f, 0f, 1f) * 2f;
            float fz = Mathf.Clamp(v + 0.5f, 0f, 1f) * 2f;
            int i = Mathf.Min(1, Mathf.FloorToInt(fx));
            int j = Mathf.Min(1, Mathf.FloorToInt(fz));
            float tx = fx - i;
            float tz = fz - j;

            float a = Mathf.Lerp(top[j * 3 + i].y, top[j * 3 + i + 1].y, tx);
            float b = Mathf.Lerp(top[(j + 1) * 3 + i].y, top[(j + 1) * 3 + i + 1].y, tx);
            return Mathf.Lerp(a, b, tz) - 0.5f;
        }

        /// <summary>Every ring of the block, bottom to top. A wall is stitched between two.</summary>
        public static Vector3[][] Rings(int variant, bool coursed)
        {
            Vector3[] foot = Ring(-0.5f - Skirt);
            Vector3[] top = Top(variant);

            if (!coursed) return new[] { foot, top };

            return new[] { foot, Ring(-0.5f), Course(variant, 0), Course(variant, 1), top };
        }

        /// <summary>The nine plan positions at one height, at the exact cell footprint.</summary>
        static Vector3[] Ring(float y)
        {
            var points = new Vector3[9];
            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                points[j * 3 + i] = new Vector3(-0.5f + 0.5f * i, y, -0.5f + 0.5f * j);
            return points;
        }

        static Mesh Build(int variant, bool coursed)
        {
            Vector3[][] rings = Rings(variant, coursed);

            var vertices = new List<Vector3>(256);
            var normals = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var triangles = new List<int>(384);

            // ---- the top, as four facets ----------------------------------------------------
            Vector3[] top = rings[rings.Length - 1];
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
                AddQuad(vertices, normals, uvs, triangles,
                    top[j * 3 + i], top[j * 3 + i + 1], top[(j + 1) * 3 + i + 1], top[(j + 1) * 3 + i],
                    PlanarTop);

            // ---- the four walls, one band at a time ------------------------------------------
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
            // In four facets, not one, and for a T-junction rather than for looks: the walls stand
            // on the mid-edge points, so a single quad spanning a whole edge belongs to one facet
            // where it should belong to two, and the block is not a closed solid. It is buried in
            // the cell below and would never have shown as daylight, which is why it wants a test
            // rather than an eye — the same reasoning, and the same fix, as RockMesh.
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

            var mesh = new Mesh { name = (coursed ? "Odyssey/GroundFace" : "Odyssey/GroundTurf") + variant };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// One flat facet, corners given anticlockwise about the outward normal.
        ///
        /// <para>The winding is 0-2-1 and 0-3-2, which is the opposite of the obvious order and is
        /// the convention <see cref="PrimitiveMeshes.UnitCube"/> and <see cref="RockMesh"/> both
        /// use. Taking the corners in their given order instead builds triangles facing inward,
        /// which is perfectly valid geometry that renders as a hole — see the note in
        /// <c>PrimitiveMeshes</c>, where it cost a while to find.</para>
        /// </summary>
        static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
            List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            System.Func<Vector3, Vector3, Vector2> uv)
        {
            Vector3 normal = Vector3.Cross(c - a, b - a);
            normal = normal.sqrMagnitude < 1e-12f ? Vector3.up : normal.normalized;
            int start = vertices.Count;

            // The UVs are laid out from the face's own geometry and the shading normal is written
            // to the buffer, which is the whole trick: how a face is lit stops being how it is
            // placed. See SideNormalTiltDegrees.
            Vector3 shaded = ShadingNormal(normal);

            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(shaded);
            uvs.Add(uv(a, normal)); uvs.Add(uv(b, normal)); uvs.Add(uv(c, normal)); uvs.Add(uv(d, normal));

            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        /// <summary>
        /// A step in one of three discrete sizes rather than anywhere in the range.
        ///
        /// A smooth draw is the wrong distribution: two consecutive courses drawn uniformly differ
        /// by about a third of the range on average, which on these numbers is under four
        /// centimetres, and the corner between them comes out very nearly a straight line — the
        /// silhouette of a cut block, which is the thing the courses exist to destroy.
        /// </summary>
        static float Step(uint salt)
        {
            float u = Unit(salt, 0x9898u);
            return u < 0.34f ? 0.15f : u < 0.67f ? 0.55f : 1f;
        }

        /// <summary>
        /// Planar UVs, one repeat per cell, because the catalogue rows ask for one tile per cell
        /// and a stretched tile on a leaning wall reads as a smear rather than as earth.
        /// </summary>
        static Vector2 PlanarTop(Vector3 point, Vector3 normal) =>
            new Vector2(point.x + 0.5f, point.z + 0.5f);

        static Vector2 PlanarSide(Vector3 point, Vector3 normal)
        {
            float across = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? point.z : point.x;
            return new Vector2(across + 0.5f, point.y + 0.5f);
        }

        /// <summary>A stable 0..1 from a salt pair. The same avalanche <see cref="GroundScatter"/> uses.</summary>
        static float Unit(uint a, uint b) =>
            (GroundScatter.Hash((int)a, (int)b, 0x9E3779B9u) & 0xFFFFFFu) * (1f / 0x1000000);
    }
}
