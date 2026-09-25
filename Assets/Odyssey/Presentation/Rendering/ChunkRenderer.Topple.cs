#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The topple (design 45 §5): a felled tree goes over where it stood, then sinks away. Drawn
    /// only — nothing of it is in a cell, a save or the hash, and the simulation removed the tree
    /// the instant the last swing landed.
    ///
    /// <para><b>It is the tree that was standing.</b> The same row of the same family, turned and
    /// sized by the same hash (<see cref="TreeArt"/>), drawn with the material the chunk drew it
    /// with, so the first frame of the fall is the last frame of the standing tree. It falls away
    /// from the nearest colonist — the one who felled it — or, with nobody near, along a bearing
    /// of its own cell's hash.</para>
    ///
    /// <para>It is a handful of meshes for a second or two, submitted once each, which is why it
    /// is plain <c>RenderMesh</c> rather than a bucket: at most <see cref="MaxTopples"/> trees are
    /// falling at once and nearly always none.</para>
    /// </summary>
    public sealed partial class ChunkRenderer
    {
        /// <summary>Seconds a tree takes to go from upright to the ground.</summary>
        public const float ToppleSeconds = 1.5f;

        /// <summary>Seconds it then lies before it has sunk out of sight.</summary>
        public const float SinkSeconds = 1.2f;

        /// <summary>How many trees may be falling at once; one more replaces the oldest.</summary>
        public const int MaxTopples = 16;

        /// <summary>Whether felled trees are drawn falling. A bench turns it off to time without them.</summary>
        public bool Topples { get; set; } = true;

        /// <summary>Trees falling now. Exposed for tests.</summary>
        public int TopplesInFlight => _topples.Count;

        struct Topple
        {
            public int Module;
            public ushort Def;
            public Matrix4x4 Base;       // where the trunk meets the ground
            public Quaternion Stance;    // its own yaw
            public float Size;
            public Vector3 Axis;         // what it pivots about, horizontal
            public float Started;
            public float Height;         // how far it sinks, the art's own height
        }

        readonly List<Topple> _topples = new List<Topple>();
        readonly List<WorldRenderModel.FelledTree> _felledScratch = new List<WorldRenderModel.FelledTree>();

        void DrawTopples(WorldSnapshot snapshot)
        {
            _felledScratch.Clear();
            _model.DrainFelled(_felledScratch);
            float now = Time.time;
            for (int i = 0; i < _felledScratch.Count; i++)
                if (Topples) Begin(_felledScratch[i], snapshot, now);

            for (int i = _topples.Count - 1; i >= 0; i--)
            {
                Topple t = _topples[i];
                float age = now - t.Started;
                if (age >= ToppleSeconds + SinkSeconds || age < 0f)
                {
                    _topples.RemoveAt(i);
                    continue;
                }
                Draw(t, age);
            }
        }

        void Begin(WorldRenderModel.FelledTree felled, WorldSnapshot snapshot, float now)
        {
            int family = _model.TreeModule(felled.Def);
            if (family == 0) return;
            CellRef at = _model.Size.FromIndex(felled.Cell);
            int[] rows = _mesher.TreeVariants(family);
            int module = rows[TreeArt.VariantFor(felled.Def, at.X, at.Z, rows.Length)];
            TreeArt.Stance(at.X, at.Z, out float yaw, out float size);
            if (rows.Length <= 1) { yaw = 0f; size = 1f; }

            Vector3 foot = CellMetrics.FloorCentre(at.X, at.Z, at.Y);
            Vector3 away = AwayFromFeller(snapshot, at);
            var topple = new Topple
            {
                Module = module,
                Def = felled.Def,
                Base = GroundRelief.Drape(foot),
                Stance = Quaternion.Euler(0f, yaw, 0f),
                Size = size,
                // The pivot is horizontal and square to the direction of the fall.
                Axis = Vector3.Cross(Vector3.up, away).normalized,
                Started = now,
                Height = Mathf.Max(1f, _model.Library[module].Bounds.max.y * size),
            };

            if (_topples.Count >= MaxTopples) _topples.RemoveAt(0);
            _topples.Add(topple);
        }

        /// <summary>
        /// Which way the tree goes over: away from the nearest colonist within three cells on its
        /// layer, or along its cell's own bearing when nobody is close. Horizontal and unit length.
        /// </summary>
        static Vector3 AwayFromFeller(WorldSnapshot snapshot, CellRef tree)
        {
            var pawns = snapshot.Pawns;
            int best = int.MaxValue;
            Vector3 away = Vector3.zero;
            for (int i = 0; i < pawns.Length; i++)
            {
                CellRef c = pawns[i].Cell;
                if (c.Y != tree.Y) continue;
                int dx = tree.X - c.X, dz = tree.Z - c.Z;
                int d = dx * dx + dz * dz;
                if (d == 0 || d > 9 || d >= best) continue;
                best = d;
                away = new Vector3(dx, 0f, dz);
            }
            if (away.sqrMagnitude > 0f) return away.normalized;

            float bearing = GroundScatter.Unit(tree.X, tree.Z, 0x7F11u) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing));
        }

        void Draw(in Topple t, float age)
        {
            // Over it goes like a weight: slow off the stump and fastest as it lands, the square of
            // the time, which is what a body tipping under gravity roughly does.
            float fall = Mathf.Clamp01(age / ToppleSeconds);
            float angle = 88f * fall * fall;
            float sink = age <= ToppleSeconds ? 0f : (age - ToppleSeconds) / SinkSeconds;
            Vector3 down = Vector3.down * (sink * sink * t.Height * 0.35f);

            Matrix4x4 placement = Matrix4x4.Translate(down) * t.Base
                * Matrix4x4.Rotate(Quaternion.AngleAxis(angle, t.Axis))
                * Matrix4x4.TRS(Vector3.zero, t.Stance, Vector3.one * t.Size);

            ResolvedModule resolved = _model.Library[t.Module];
            int tint = TintCode.Tree(TreeLook.SpeciesOf(t.Def));
            for (int p = 0; p < resolved.Parts.Length; p++)
            {
                ModulePart part = resolved.Parts[p];
                Material? material = IndirectMaterialFor(tint, part, 1f);
                if (material == null) continue;
                var rp = new RenderParams(material)
                {
                    layer = GameObjectLayer,
                    receiveShadows = true,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                };
                if (SubmitToGpu) Graphics.RenderMesh(rp, part.Mesh, part.Submesh, placement * part.Local);
                DrawCalls++;
            }
        }
    }
}
