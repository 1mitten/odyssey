#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The edge of the colony's home, drawn on the board while the Home view is on (design 43 §5b,
    /// Claude Design's specification of 2026-09-25): a flat strip 0.25 m wide along every side of
    /// a home cell that faces a cell which is not home, in the accent at 70% on the active layer and
    /// 30% on the drawn layers below it, and nothing on the layers above. <b>An edge and no wash</b>:
    /// the inside of home is the board as it always looks.
    ///
    /// <para><b>Hidden is not submitted, and a still frame builds nothing.</b> The border comes from
    /// the simulation (<see cref="WorldSnapshot.HomeCells"/>), which works out the home and which
    /// of its cells a colonist can stand in; this pass only turns rows into quads. The two meshes are
    /// rebuilt when <see cref="WorldSnapshot.HomeVersion"/>, the active layer or the lowest drawn
    /// layer changes, or after the view was hidden — a colony at rest with the view open rebuilds
    /// nothing.</para>
    ///
    /// <para><b>Two draw calls whatever the size of home</b>: every strip on a tier is in one mesh,
    /// so a home of forty border cells and one of four thousand cost the same submission (P10).
    /// Ten thousand strips is 40,000 vertices, inside a 16-bit index until 65,000 and a 32-bit one
    /// past it.</para>
    ///
    /// <para><b>Inset, so corners never double.</b> A strip lies inside its own cell against the
    /// side it marks, as a stockpile's edge does (<see cref="CellMetrics.StoreEdge"/>). Where a cell
    /// has an east or west strip, its north and south strips stop short of it, so the corner square
    /// is covered once — at 70% a doubled corner is a visibly darker dot at every turn of the
    /// line.</para>
    ///
    /// <para><b>Draped as the ground is</b>, by <see cref="GroundRelief.Drape"/> about the cell's
    /// floor centre, then lifted <see cref="Lift"/>: over a built floor's walking surface
    /// (<see cref="CellMetrics.SlabLift"/>) and the ground alike, so the line ties with neither.
    /// The ramp at the foot of a terrace step is drawn by the ground skin above that height, and a
    /// strip there is hidden under the ramp rather than laid on it; recorded in design 43 §5b.</para>
    /// </summary>
    public sealed class HomeEdgePass : IDisposable
    {
        /// <summary>How wide the line is, metres (design 43 §5b).</summary>
        public const float Width = 0.25f;

        /// <summary>How far above the draped floor plane the line sits, metres.</summary>
        public const float Lift = 0.02f;

        /// <summary>The line's strength on the active layer.</summary>
        public const float ActiveAlpha = 0.70f;

        /// <summary>The line's strength on the drawn layers below the active one.</summary>
        public const float LowerAlpha = 0.30f;

        /// <summary>
        /// How far the grass is pushed back round the line on the active layer, metres. The meadow's
        /// grass stands 1.1 m, and a line 0.02 m off the ground inside it is not seen at all
        /// (owner, 2026-09-25: <i>part the grass along the edge</i>).
        /// </summary>
        public const float GrassRadius = 0.6f;

        /// <summary>Stamps along one side of a cell: at a quarter, a half and three quarters of it.</summary>
        public const int GrassStampsPerSide = 3;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        const int Tiers = 2; // 0 the active layer, 1 the drawn layers below it

        readonly List<Vector3>[] _vertices = { new List<Vector3>(), new List<Vector3>() };
        readonly List<int>[] _indices = { new List<int>(), new List<int>() };
        readonly Mesh?[] _meshes = new Mesh?[Tiers];
        readonly Material?[] _materials = new Material?[Tiers];
        readonly List<Vector3> _grass = new List<Vector3>();

        int _builtVersion = int.MinValue;
        int _builtActive = int.MinValue;
        int _builtLowest = int.MinValue;
        bool _built;

        /// <summary>The Unity layer the line is drawn on — the world's, so the world camera sees it.</summary>
        public int GameObjectLayer { get; set; }

        /// <summary>Calls submitted last frame. At most two, which is the claim this pass exists to keep.</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>How many times the meshes were rebuilt, for the test that says a still frame rebuilds nothing.</summary>
        public int Rebuilds { get; private set; }

        /// <summary>Strips in the last build, on each tier: 0 the active layer, 1 below it.</summary>
        public int StripsOn(int tier) => _vertices[tier].Count / 4;

        /// <summary>Where the grass is pushed back along the active layer's line, rebuilt with the meshes.</summary>
        public IReadOnlyList<Vector3> GrassPoints => _grass;

        /// <summary>
        /// Forget what was built, so the next <see cref="Draw"/> rebuilds. Called while the view is
        /// off: the rows are not published then, and the version need not move before it is on again.
        /// </summary>
        public void Hide()
        {
            LastDrawCalls = 0;
            _built = false;
        }

        /// <summary>
        /// Draw the frame's home edge on layers <paramref name="lowestLayer"/> to
        /// <paramref name="activeLayer"/>, rebuilding first if anything it was built from moved.
        /// </summary>
        public void Draw(WorldSnapshot snapshot, int activeLayer, int lowestLayer)
        {
            LastDrawCalls = 0;
            if (!_built || snapshot.HomeVersion != _builtVersion
                || activeLayer != _builtActive || lowestLayer != _builtLowest)
            {
                Rebuild(snapshot.HomeCells, snapshot.Size, activeLayer, lowestLayer);
                _builtVersion = snapshot.HomeVersion;
                _builtActive = activeLayer;
                _builtLowest = lowestLayer;
                _built = true;
            }

            for (int tier = 0; tier < Tiers; tier++)
            {
                Mesh? mesh = _meshes[tier];
                if (mesh == null || _vertices[tier].Count == 0) continue;
                var rp = new RenderParams(MaterialFor(tier))
                {
                    layer = GameObjectLayer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.identity);
                LastDrawCalls++;
            }
        }

        void Rebuild(ReadOnlySpan<HomeCellView> rows, GridSize size, int activeLayer, int lowestLayer)
        {
            Rebuilds++;
            for (int tier = 0; tier < Tiers; tier++)
            {
                _vertices[tier].Clear();
                _indices[tier].Clear();
            }
            _grass.Clear();

            Vector3 low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 high = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Span<Rect> strips = stackalloc Rect[4];

            for (int i = 0; i < rows.Length; i++)
            {
                HomeCellView row = rows[i];
                CellRef c = size.FromIndex(row.CellIndex);
                if (c.Y > activeLayer || c.Y < lowestLayer) continue;
                int tier = c.Y == activeLayer ? 0 : 1;

                Matrix4x4 at = GroundRelief.Drape(CellMetrics.FloorCentre(c));
                int n = StripsOf(row.Edges, strips);
                for (int s = 0; s < n; s++)
                {
                    Rect r = strips[s];
                    Quad(tier, at, r, ref low, ref high);
                    if (tier == 0) GrassAlong(at, r);
                }
            }

            var bounds = new Bounds();
            if (low.x <= high.x) bounds.SetMinMax(low - Vector3.one * 0.1f, high + Vector3.one * 0.1f);
            for (int tier = 0; tier < Tiers; tier++) Bake(tier, bounds);
        }

        /// <summary>
        /// The strips one border cell draws, in the cell's own coordinates about its floor centre:
        /// x and z, width and depth. West and east run the cell's full depth; south and north stop
        /// short of a west or east strip on the same cell, so no corner is covered twice. The one
        /// rule of the line's shape, as a pure function a test can state.
        /// </summary>
        public static int StripsOf(byte edges, Span<Rect> into)
        {
            const float h = CellMetrics.HalfXZ;
            bool west = (edges & 1) != 0, east = (edges & 2) != 0;
            bool south = (edges & 4) != 0, north = (edges & 8) != 0;
            int n = 0;
            if (west) into[n++] = new Rect(-h, -h, Width, CellMetrics.SizeXZ);
            if (east) into[n++] = new Rect(h - Width, -h, Width, CellMetrics.SizeXZ);
            float from = west ? -h + Width : -h;
            float to = east ? h - Width : h;
            if (south) into[n++] = new Rect(from, -h, to - from, Width);
            if (north) into[n++] = new Rect(from, h - Width, to - from, Width);
            return n;
        }

        void Quad(int tier, Matrix4x4 at, Rect r, ref Vector3 low, ref Vector3 high)
        {
            List<Vector3> v = _vertices[tier];
            List<int> t = _indices[tier];
            int start = v.Count;
            Corner(v, at, r.xMin, r.yMin, ref low, ref high);
            Corner(v, at, r.xMin, r.yMax, ref low, ref high);
            Corner(v, at, r.xMax, r.yMax, ref low, ref high);
            Corner(v, at, r.xMax, r.yMin, ref low, ref high);
            // Clockwise seen from above, which is Unity's front face; the material culls nothing
            // anyway, so a camera below the line still draws it.
            t.Add(start);
            t.Add(start + 1);
            t.Add(start + 2);
            t.Add(start);
            t.Add(start + 2);
            t.Add(start + 3);
        }

        static void Corner(List<Vector3> v, Matrix4x4 at, float x, float z, ref Vector3 low, ref Vector3 high)
        {
            Vector3 p = at.MultiplyPoint3x4(new Vector3(x, Lift, z));
            v.Add(p);
            low = Vector3.Min(low, p);
            high = Vector3.Max(high, p);
        }

        void GrassAlong(Matrix4x4 at, Rect r)
        {
            bool alongX = r.width > r.height;
            for (int k = 1; k <= GrassStampsPerSide; k++)
            {
                float f = k / (float)(GrassStampsPerSide + 1);
                float x = alongX ? Mathf.Lerp(r.xMin, r.xMax, f) : r.center.x;
                float z = alongX ? r.center.y : Mathf.Lerp(r.yMin, r.yMax, f);
                _grass.Add(at.MultiplyPoint3x4(new Vector3(x, 0f, z)));
            }
        }

        void Bake(int tier, Bounds bounds)
        {
            List<Vector3> v = _vertices[tier];
            Mesh? mesh = _meshes[tier];
            if (v.Count == 0)
            {
                if (mesh != null) mesh.Clear();
                return;
            }
            if (mesh == null)
            {
                mesh = new Mesh { name = tier == 0 ? "HomeEdge.Active" : "HomeEdge.Lower" };
                mesh.MarkDynamic();
                _meshes[tier] = mesh;
            }
            mesh.Clear();
            mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetTriangles(_indices[tier], 0, calculateBounds: false);
            mesh.bounds = bounds;
        }

        /// <summary>The line's colour on a tier: the accent, at 70% on the active layer and 30% below it.</summary>
        public static Color ColourOf(int tier)
        {
            Color colour = HudTokens.Convert(HudTheme.Accent);
            colour.a = tier == 0 ? ActiveAlpha : LowerAlpha;
            return colour;
        }

        Material MaterialFor(int tier)
        {
            Material? material = _materials[tier];
            if (material != null) return material;

            // URP's unlit, transparent, depth-tested and writing no depth: the line is on the
            // ground, so what stands in front of it hides it, and it hides nothing itself.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "Odyssey/HomeEdge" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor(BaseColorId, ColourOf(tier));
            _materials[tier] = material;
            return material;
        }

        public void Dispose()
        {
            for (int i = 0; i < Tiers; i++)
            {
                Destroy(_materials[i]);
                _materials[i] = null;
                Destroy(_meshes[i]);
                _meshes[i] = null;
            }
        }

        static void Destroy(UnityEngine.Object? thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(thing);
            else UnityEngine.Object.DestroyImmediate(thing);
        }
    }
}
