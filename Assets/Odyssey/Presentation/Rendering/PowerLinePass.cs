#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The power lines, drawn (design 32 §9): a small box at every line cell and a rod along every
    /// link to the line beside, above or below it, in the colour of the line's state, over
    /// everything.
    ///
    /// <para><b>Hidden is not submitted.</b> Whether the built lines show is
    /// <see cref="PowerLinesVisibility"/>'s answer, handed in; when it is no, nothing is drawn and
    /// nothing is rebuilt — no chunk is re-meshed and <c>WorldRenderModel.Version</c> is never
    /// touched, because the lines are not in the chunk meshes at all. An <b>order</b> is drawn
    /// either way, like every other standing order.</para>
    ///
    /// <para><b>Draws in colours, never in cells</b> (<c>docs/bug-patterns.md</c> P10). Every
    /// instance of one colour on one tier goes out as one instanced call per
    /// <see cref="ChunkRenderer.MaxInstancesPerCall"/>, so two thousand lines are a handful of
    /// calls. The matrices are rebuilt only when the frame's <see cref="WorldSnapshot.PowerVersion"/>,
    /// its line count, the active layer or the visibility changes — a colony at rest with the
    /// overlay open rebuilds nothing.</para>
    ///
    /// <para><b>Two tiers</b>: lines on the active layer at full strength, lines on every other
    /// layer faint. Drawn through everything is what decision 6 asked for; drawn through
    /// everything at one strength is a tangle nobody can read the depth of.</para>
    /// </summary>
    public sealed class PowerLinePass : IDisposable
    {
        /// <summary>How far above its cell's floor a line runs: under a colonist's knee, over the floor.</summary>
        public const float Lift = 0.30f;

        /// <summary>The box at a line cell, metres a side.</summary>
        public const float Node = 0.34f;

        /// <summary>A link rod's thickness, metres.</summary>
        public const float Rod = 0.13f;

        /// <summary>How strong a line off the active layer is drawn, against one on it.</summary>
        public const float OffLayerAlpha = 0.38f;

        const int Kinds = 5;   // live, dark, idle, ordered, marked
        const int Tiers = 2;   // on the active layer, off it

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly Material?[] _materials = new Material?[Kinds * Tiers];
        readonly Matrix4x4[][] _buckets = new Matrix4x4[Kinds * Tiers][];
        readonly int[] _counts = new int[Kinds * Tiers];

        int _builtVersion = int.MinValue;
        int _builtCount = -1;
        int _builtLayer = int.MinValue;
        bool _builtVisible;

        /// <summary>The Unity layer the lines are drawn on — the world's, so the world camera sees them.</summary>
        public int GameObjectLayer { get; set; }

        /// <summary>Instanced calls submitted last frame. The frame budget's to read (design 32 §11).</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>How many times the matrices have been rebuilt, for the test that says a still frame rebuilds nothing.</summary>
        public int Rebuilds { get; private set; }

        public PowerLinePass()
        {
            for (int i = 0; i < _buckets.Length; i++) _buckets[i] = new Matrix4x4[64];
        }

        /// <summary>The colour of a kind of line, full strength. The pane's colours for the same states (<see cref="PowerLabels.Colour"/>).</summary>
        public static Color ColourOf(int kind) => kind switch
        {
            0 => HudTokens.Convert(PowerLabels.Colour(PowerNetState.Live)),
            1 => HudTokens.Convert(PowerLabels.Colour(PowerNetState.Dark)),
            2 => HudTokens.Convert(PowerLabels.Colour(PowerNetState.Idle)),
            3 => HudTokens.Convert(OrderColours.Hue(DesignateTool.Build)),
            _ => HudTokens.Convert(OrderColours.Hue(DesignateTool.RemoveConduit)),
        };

        static int KindOf(in ConduitView line)
        {
            if (line.Kind == ConduitKind.Ordered) return 3;
            if (line.Kind == ConduitKind.Marked) return 4;
            return line.State switch
            {
                PowerNetState.Live => 0,
                PowerNetState.Dark => 1,
                _ => 2,
            };
        }

        /// <summary>
        /// Draw the frame's lines. <paramref name="visible"/> is whether the built lines are shown;
        /// orders and marks are drawn regardless.
        /// </summary>
        public void Draw(WorldSnapshot snapshot, bool visible, int activeLayer)
        {
            LastDrawCalls = 0;
            ReadOnlySpan<ConduitView> lines = snapshot.Conduits;
            if (lines.Length == 0) return;

            if (snapshot.PowerVersion != _builtVersion || lines.Length != _builtCount
                || activeLayer != _builtLayer || visible != _builtVisible)
            {
                Rebuild(lines, snapshot.Size, visible, activeLayer);
                _builtVersion = snapshot.PowerVersion;
                _builtCount = lines.Length;
                _builtLayer = activeLayer;
                _builtVisible = visible;
            }

            Mesh mesh = PrimitiveMeshes.UnitCube;
            for (int b = 0; b < _buckets.Length; b++)
            {
                int count = _counts[b];
                if (count == 0) continue;
                Material material = MaterialFor(b);
                var rp = new RenderParams(material)
                {
                    layer = GameObjectLayer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                for (int sent = 0; sent < count; sent += ChunkRenderer.MaxInstancesPerCall)
                {
                    int n = Mathf.Min(ChunkRenderer.MaxInstancesPerCall, count - sent);
                    Graphics.RenderMeshInstanced(rp, mesh, 0, _buckets[b], n, sent);
                    LastDrawCalls++;
                }
            }
        }

        void Rebuild(ReadOnlySpan<ConduitView> lines, GridSize size, bool visible, int activeLayer)
        {
            Rebuilds++;
            Array.Clear(_counts, 0, _counts.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                ConduitView line = lines[i];
                if (line.Kind == ConduitKind.Built && !visible) continue;

                CellRef c = size.FromIndex(line.CellIndex);
                int bucket = KindOf(line) * Tiers + (c.Y == activeLayer ? 0 : 1);
                Vector3 at = CellMetrics.FloorCentre(c) + Vector3.up * Lift;

                Add(bucket, Matrix4x4.TRS(at, Quaternion.identity, new Vector3(Node, Node, Node)));

                // Each link drawn once, from its lower side: north, east and up. South, west and
                // down are the same rods seen from the other end.
                if ((line.Links & (1 << 0)) != 0)
                    Add(bucket, Matrix4x4.TRS(at + new Vector3(0f, 0f, CellMetrics.HalfXZ), Quaternion.identity,
                        new Vector3(Rod, Rod, CellMetrics.SizeXZ)));
                if ((line.Links & (1 << 1)) != 0)
                    Add(bucket, Matrix4x4.TRS(at + new Vector3(CellMetrics.HalfXZ, 0f, 0f), Quaternion.identity,
                        new Vector3(CellMetrics.SizeXZ, Rod, Rod)));
                if ((line.Links & (1 << 4)) != 0)
                    Add(bucket, Matrix4x4.TRS(at + new Vector3(0f, CellMetrics.SizeY * 0.5f, 0f), Quaternion.identity,
                        new Vector3(Rod, CellMetrics.SizeY, Rod)));
            }
        }

        void Add(int bucket, Matrix4x4 matrix)
        {
            ref Matrix4x4[] list = ref _buckets[bucket];
            if (_counts[bucket] == list.Length) Array.Resize(ref list, list.Length * 2);
            list[_counts[bucket]++] = matrix;
        }

        Material MaterialFor(int bucket)
        {
            Material? material = _materials[bucket];
            if (material != null) return material;

            Shader? shader = Shader.Find("Odyssey/PowerLine");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "Odyssey/PowerLine", enableInstancing = true };

            Color colour = ColourOf(bucket / Tiers);
            if (bucket % Tiers == 1) colour.a *= OffLayerAlpha;
            material.SetColor(BaseColorId, colour);
            _materials[bucket] = material;
            return material;
        }

        public void Dispose()
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(_materials[i]);
                else UnityEngine.Object.DestroyImmediate(_materials[i]);
                _materials[i] = null;
            }
        }
    }
}
