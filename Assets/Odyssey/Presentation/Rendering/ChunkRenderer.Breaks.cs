#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The break (design 57 §7): a cracked wall that comes down, or a face mined out, does not
    /// vanish between one frame and the next. It shudders, splits in half, each half splits again,
    /// and the four pieces fall apart and sink away (owner, 2026-09-26: <i>"it breaks in half into
    /// quarters … some kind of motion to indicate the breaking of the piece and also would be
    /// really satisfying for mining"</i>). Drawn only — the simulation removed the thing the
    /// instant it went, exactly as with a felled tree's topple.
    ///
    /// <para><b>It is the thing that was standing.</b> The crack pass already holds every cracked
    /// cell's own meshes (<see cref="ChunkMesher.MeshCell"/>); when a cell stops being listed its
    /// batch is not thrown away but <em>watched</em>, and if the mirror then says the rock in it is
    /// no longer solid, or the building in it is gone, it came down and the batch is what breaks.
    /// Repaired, cancelled or kept (a part-mined face) and the cell never loses it, so the watch
    /// times out and gives the batch back. A thing destroyed from whole in one blow was never cracked and
    /// simply goes, as it did before.</para>
    ///
    /// <para><b>Four pieces, clipped rather than cut.</b> Each piece is every mesh of the cell drawn
    /// in <c>Odyssey/Shard</c>, clipped to its quarter and moved by one matrix — a vertical plane
    /// halving the cell across the view, a horizontal one halving it again — with a solid block of
    /// the broken interior inside it, so a shell of panels reads as a lump.</para>
    ///
    /// <para><b>What it costs:</b> for <see cref="BreakSeconds"/>, the cell's parts times four plus
    /// four blocks, in plain <c>RenderMesh</c> calls, as the topple is: a colony breaks a few cells
    /// at a time and nearly always none, and at most <see cref="MaxBreaks"/> are in the air.</para>
    /// </summary>
    public sealed partial class ChunkRenderer
    {
        /// <summary>Seconds from the first shudder to the last piece gone into the ground.</summary>
        public const float BreakSeconds = 1.6f;

        /// <summary>Seconds it shudders, whole, before it splits.</summary>
        public const float ShudderSeconds = 0.14f;

        /// <summary>Seconds the two halves take to lean apart before the quarters separate.</summary>
        public const float HalvesSeconds = 0.26f;

        /// <summary>When, from the start, the pieces begin to sink out of sight.</summary>
        public const float SinkFrom = 0.95f;

        /// <summary>How many cells may be breaking at once; one more replaces the oldest.</summary>
        public const int MaxBreaks = 12;

        /// <summary>
        /// How many frames a cell that stopped cracking is watched for coming down. The order can
        /// leave the snapshot a publish before the cell's new geometry reaches the chunk, and the
        /// chunk can wait on the meshing budget; well under a second at any playable frame rate.
        /// </summary>
        public const int WatchFrames = 45;

        /// <summary>Whether things break apart. A bench turns it off to time the frame without it.</summary>
        public bool Breaks { get; set; } = true;

        /// <summary>Cells breaking now. Exposed for tests.</summary>
        public int BreaksInFlight => _breaks.Count;

        /// <summary>Cells that stopped cracking and are being watched for coming down. Exposed for tests.</summary>
        public int BreakCellsWatched => _watched.Count;

        /// <summary>Calls the breaks took last frame. A measurement; also counted in <see cref="DrawCalls"/>.</summary>
        public int BreakDrawCalls { get; private set; }

        sealed class Watched
        {
            public int Cell;
            public CrackEntry Entry = null!;
            public int FramesLeft;
        }

        struct BreakPart
        {
            public Mesh Mesh;
            public int Submesh;
            public Material Material;   // the shard material made from what the chunk drew it with
            public Matrix4x4 Matrix;
        }

        sealed class Break
        {
            public CrackEntry Entry = null!;
            public readonly List<BreakPart> Parts = new List<BreakPart>();
            public float Started;
            public float Severity;
            public bool Ground;
            public Vector3 Centre;      // the middle of what was drawn in the cell
            public Vector3 Extents;     // its half sizes
            public Vector3 Axis;        // the horizontal axis it halves along, X or Z
            public float Seed;          // 0..1, the cell's own, for the jitter and the spin
            public Bounds Reach;        // everything the pieces can reach, for the camera's cull
        }

        readonly List<Watched> _watched = new List<Watched>();
        readonly List<Break> _breaks = new List<Break>();
        readonly List<MaterialPropertyBlock> _breakProps = new List<MaterialPropertyBlock>();
        readonly Dictionary<Material, Material> _shardMaterials = new Dictionary<Material, Material>();
        readonly Material?[] _blockMaterials = new Material?[2];
        Shader? _shardShader;
        bool _shardLooked;

        static readonly int MotionId = Shader.PropertyToID("_Motion");
        static readonly int ClipCentreId = Shader.PropertyToID("_ClipCentre");
        static readonly int ClipAxisId = Shader.PropertyToID("_ClipAxis");
        static readonly int ClipSidesId = Shader.PropertyToID("_ClipSides");
        static readonly int ShardBaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int ShardBaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ShardColorId = Shader.PropertyToID("_Color");
        static readonly int InteriorId = Shader.PropertyToID("_Interior");

        /// <summary>Where the packs and URP keep a material's albedo, in the order asked.</summary>
        static readonly string[] ShardAlbedoNames = { "_BaseMap", "_MainTex", "_Albedo_Map", "_AlbedoMap", "_Albedo" };

        /// <summary>The inside of broken rock, and of a broken wall: lit by the shader like any surface.</summary>
        static readonly Color RockInterior = new Color(0.34f, 0.31f, 0.28f, 1f);
        static readonly Color WallInterior = new Color(0.52f, 0.5f, 0.47f, 1f);

        void Watch(int cell, CrackEntry entry)
        {
            if (!Breaks)
            {
                entry.Batch.Dispose();
                return;
            }
            _watched.Add(new Watched { Cell = cell, Entry = entry, FramesLeft = WatchFrames });
        }

        CrackEntry? ReclaimWatched(int cell)
        {
            for (int i = 0; i < _watched.Count; i++)
            {
                if (_watched[i].Cell != cell) continue;
                CrackEntry entry = _watched[i].Entry;
                _watched.RemoveAt(i);
                return entry;
            }
            return null;
        }

        /// <summary>Settle the watch, then draw every break in the air. Called by <see cref="DrawCracks"/>.</summary>
        void DrawBreaks()
        {
            BreakDrawCalls = 0;
            float now = Time.time;

            for (int i = _watched.Count - 1; i >= 0; i--)
            {
                Watched w = _watched[i];
                w.FramesLeft--;

                if (!CameDown(w))
                {
                    // Still standing: repaired, cancelled or kept, so far.
                    if (w.FramesLeft > 0) continue;
                    w.Entry.Batch.Dispose();
                    _watched.RemoveAt(i);
                    continue;
                }

                // Gone from the mirror. Wait, within the watch, until its chunk is drawn without
                // it too, or the old wall and its pieces would stand side by side for a frame.
                int chunk = _model.Chunks.ChunkIndexOfCell(_model.Size.FromIndex(w.Cell));
                ChunkBatch? drawn = _batches[chunk];
                if (drawn != null && drawn.Version != _model.ChunkVersion(chunk) && w.FramesLeft > 0) continue;

                _watched.RemoveAt(i);
                Begin(w, now);
            }

            for (int i = _breaks.Count - 1; i >= 0; i--)
            {
                Break b = _breaks[i];
                float age = now - b.Started;
                if (age >= BreakSeconds || age < 0f)
                {
                    b.Entry.Batch.Dispose();
                    _breaks.RemoveAt(i);
                }
            }
            for (int i = 0; i < _breaks.Count; i++) DrawBreak(_breaks[i], now - _breaks[i].Started, i);
        }

        /// <summary>
        /// Whether the cell lost what was cracked in it, asked of the mirror rather than of what
        /// is drawn: rock that was solid when it was meshed and is not now, a building that stood
        /// there and does not. Counting drawn parts was the first idea and is wrong — a wall built
        /// beside a cracked one hides a panel, so the cracked wall draws less and would have
        /// fallen apart for being given a neighbour.
        /// </summary>
        bool CameDown(Watched w) =>
            w.Entry.Ground
                ? w.Entry.Solid && !_model.IsSolid(w.Cell)
                : w.Entry.Def != 0 && _model.EdificeDef(w.Cell) != w.Entry.Def;

        void Begin(Watched w, float now)
        {
            if (ShardMaterialFor(null) == null)
            {
                w.Entry.Batch.Dispose();
                return;
            }

            CellRef cell = _model.Size.FromIndex(w.Cell);
            bool lowered = _drawnSlice != null && _drawnSlice.LowersWallsOn(_drawnLayer, cell.Y);
            var b = new Break
            {
                Entry = w.Entry,
                Started = now,
                Severity = SeverityOf(w.Entry.Level),
                Ground = w.Entry.Ground,
                Seed = GroundScatter.Unit(cell.X, cell.Z + cell.Y * 977, 0xB4EAu),
            };

            ChunkBatch batch = w.Entry.Batch;
            CollectBreakParts(b, batch.Body);
            CollectBreakParts(b, batch.Roof);
            CollectBreakParts(b, lowered ? batch.Stumps : batch.Walls);
            GroundSkinMesh skin = batch.Skin;
            if (w.Entry.Ground && skin.Mesh != null)
            {
                for (int g = 0; g < skin.GroupCount; g++)
                {
                    ResolveColour(skin.GroupTint(g), skin.GroupFallback(g), 1f, out Color tint, out Color emission);
                    Material? shard = ShardMaterialFor(_materials.Get(skin.GroupMaterial(g), tint, emission, false, 1f));
                    if (shard == null) continue;
                    b.Parts.Add(new BreakPart { Mesh = skin.Mesh, Submesh = g, Material = shard, Matrix = Matrix4x4.identity });
                }
            }
            if (b.Parts.Count == 0)
            {
                batch.Dispose();
                return;
            }

            // What was drawn, held to the cell so a panel's overhang cannot make a piece of the
            // neighbour's width.
            Vector3 corner = CellMetrics.Corner(cell.X, cell.Z, cell.Y);
            var box = new Bounds();
            bool any = false;
            foreach (BreakPart part in b.Parts)
            {
                Bounds drawn = TransformBounds(part.Matrix, part.Mesh.bounds);
                if (!any) { box = drawn; any = true; }
                else box.Encapsulate(drawn);
            }
            Vector3 min = Vector3.Max(box.min, corner - Vector3.one * 0.05f);
            Vector3 max = Vector3.Min(box.max, corner + new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ) + Vector3.one * 0.05f);
            if (max.x <= min.x || max.y <= min.y || max.z <= min.z)
            {
                min = corner;
                max = corner + new Vector3(CellMetrics.SizeXZ, CellMetrics.SizeY, CellMetrics.SizeXZ);
            }
            b.Centre = (min + max) * 0.5f;
            b.Extents = (max - min) * 0.5f;
            b.Axis = SplitAxis(b.Centre, b.Extents);
            b.Reach = new Bounds(b.Centre, b.Extents * 2f + new Vector3(6f, 6f, 6f));

            if (_breaks.Count >= MaxBreaks)
            {
                _breaks[0].Entry.Batch.Dispose();
                _breaks.RemoveAt(0);
            }
            _breaks.Add(b);
        }

        /// <summary>
        /// The axis the cell is halved along. A thing plainly longer one way halves across its
        /// length; otherwise across the view, so the two halves part side by side on screen rather
        /// than one hiding behind the other.
        /// </summary>
        Vector3 SplitAxis(Vector3 centre, Vector3 extents)
        {
            if (extents.x > extents.z * 1.6f) return Vector3.right;
            if (extents.z > extents.x * 1.6f) return Vector3.forward;
            if (ViewerPosition.HasValue)
            {
                Vector3 look = centre - ViewerPosition.Value;
                return Mathf.Abs(look.z) >= Mathf.Abs(look.x) ? Vector3.right : Vector3.forward;
            }
            return Vector3.right;
        }

        void CollectBreakParts(Break b, List<InstanceBucket> buckets)
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                InstanceBucket bucket = buckets[i];
                if (bucket.Count == 0) continue;
                ResolvedModule resolved = _model.Library[bucket.Module];
                ModulePart[] parts = resolved.DrawsByLevel ? resolved.Lods[0].Parts : resolved.Parts;
                int first = resolved.DrawsByLevel ? 0 : bucket.Part;
                int last = resolved.DrawsByLevel ? parts.Length : bucket.Part + 1;
                for (int p = first; p < last && p < parts.Length; p++)
                {
                    ModulePart part = parts[p];
                    if (part.Mesh == null) continue;
                    ResolveColour(bucket.Tint, part.IsFallback, 1f, out Color tint, out Color emission);
                    Material? shard = ShardMaterialFor(_materials.Get(part.Material, tint, emission, false, 1f));
                    if (shard == null) continue;
                    for (int k = 0; k < bucket.Count; k++)
                        b.Parts.Add(new BreakPart
                        {
                            Mesh = part.Mesh, Submesh = part.Submesh, Material = shard, Matrix = bucket.Matrices[k],
                        });
                }
            }
        }

        /// <summary>
        /// The shard material for what the chunk drew a part with — its texture and colour copied
        /// on to <c>Odyssey/Shard</c> — made once per material. Null when the shader is missing,
        /// and a null <paramref name="drawnWith"/> only asks whether it is there.
        /// </summary>
        Material? ShardMaterialFor(Material? drawnWith)
        {
            if (!_shardLooked)
            {
                _shardLooked = true;
                _shardShader = Shader.Find("Odyssey/Shard");
                if (_shardShader == null)
                    Debug.LogWarning("Odyssey/Shard shader not found; broken walls and mined rock vanish without breaking.");
            }
            if (_shardShader == null) return null;
            if (drawnWith == null) return BlockMaterial(false);
            if (_shardMaterials.TryGetValue(drawnWith, out Material cached)) return cached;

            var material = new Material(_shardShader) { name = "Odyssey/Shard#" + drawnWith.name };
            foreach (string name in ShardAlbedoNames)
            {
                if (!drawnWith.HasProperty(name)) continue;
                Texture texture = drawnWith.GetTexture(name);
                if (texture == null) continue;
                material.SetTexture(ShardBaseMapId, texture);
                material.SetTextureScale(ShardBaseMapId, drawnWith.GetTextureScale(name));
                material.SetTextureOffset(ShardBaseMapId, drawnWith.GetTextureOffset(name));
                break;
            }
            Color colour = drawnWith.HasProperty(ShardBaseColorId) ? drawnWith.GetColor(ShardBaseColorId)
                : drawnWith.HasProperty(ShardColorId) ? drawnWith.GetColor(ShardColorId)
                : Color.white;
            colour.a = 1f;
            material.SetColor(ShardBaseColorId, colour);
            _shardMaterials.Add(drawnWith, material);
            return material;
        }

        /// <summary>The solid block inside each piece: the broken interior, rock's or a wall's.</summary>
        Material? BlockMaterial(bool ground)
        {
            if (_shardShader == null) return null;
            int slot = ground ? 1 : 0;
            Material? material = _blockMaterials[slot];
            if (material != null) return material;
            Color inside = ground ? RockInterior : WallInterior;
            material = new Material(_shardShader) { name = ground ? "Odyssey/Shard#rock" : "Odyssey/Shard#wall" };
            material.SetColor(ShardBaseColorId, inside);
            material.SetColor(InteriorId, inside);
            _blockMaterials[slot] = material;
            return material;
        }

        void DrawBreak(Break b, float age, int slot)
        {
            Material? block = BlockMaterial(b.Ground);
            Color inside = b.Ground ? RockInterior : WallInterior;
            for (int q = 0; q < 4; q++)
            {
                int side = (q & 1) == 0 ? -1 : 1;
                bool upper = q >= 2;
                Matrix4x4 motion = PieceMotion(b, side, upper, age);

                MaterialPropertyBlock props = BreakProps(slot * 8 + q * 2);
                props.SetMatrix(MotionId, motion);
                props.SetVector(ClipCentreId, b.Centre);
                props.SetVector(ClipAxisId, b.Axis);
                props.SetVector(ClipSidesId, new Vector4(side, upper ? 1f : -1f, 0f, 0f));
                props.SetColor(InteriorId, inside);
                props.SetFloat(SeverityId, b.Severity);

                for (int p = 0; p < b.Parts.Count; p++)
                {
                    BreakPart part = b.Parts[p];
                    var rp = new RenderParams(part.Material)
                    {
                        layer = GameObjectLayer,
                        worldBounds = b.Reach,
                        matProps = props,
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = false,
                    };
                    if (SubmitToGpu) Graphics.RenderMesh(rp, part.Mesh, part.Submesh, part.Matrix);
                    DrawCalls++;
                    BreakDrawCalls++;
                }

                if (block == null) continue;
                // The block fills the piece's quarter, a little inside the surface so the art
                // wins where both are, and clips nothing.
                MaterialPropertyBlock blockProps = BreakProps(slot * 8 + q * 2 + 1);
                blockProps.SetMatrix(MotionId, motion);
                blockProps.SetVector(ClipCentreId, b.Centre);
                blockProps.SetVector(ClipAxisId, b.Axis);
                blockProps.SetVector(ClipSidesId, Vector4.zero);
                blockProps.SetColor(InteriorId, inside);
                blockProps.SetFloat(SeverityId, b.Severity);

                Vector3 across = Vector3.Cross(Vector3.up, b.Axis);
                float along = Vector3.Dot(b.Extents, Abs(b.Axis));
                float wide = Vector3.Dot(b.Extents, Abs(across));
                Vector3 centre = b.Centre + b.Axis * (side * along * 0.5f)
                                 + Vector3.up * ((upper ? 1f : -1f) * b.Extents.y * 0.5f);
                const float inset = 0.04f;
                Vector3 size = b.Axis * Mathf.Max(0.01f, along - inset)
                               + Vector3.up * Mathf.Max(0.01f, b.Extents.y - inset)
                               + across * Mathf.Max(0.01f, wide * 2f - inset * 2f);
                var blockRp = new RenderParams(block)
                {
                    layer = GameObjectLayer,
                    worldBounds = b.Reach,
                    matProps = blockProps,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                if (SubmitToGpu)
                    Graphics.RenderMesh(blockRp, PrimitiveMeshes.UnitCube, 0,
                        Matrix4x4.TRS(centre, Quaternion.identity, Abs(size)));
                DrawCalls++;
                BreakDrawCalls++;
            }
        }

        /// <summary>
        /// Where one piece has got to, as a matrix from where it stood. <paramref name="side"/> is
        /// which half, along <see cref="Break.Axis"/>; <paramref name="upper"/> which quarter of it.
        ///
        /// <para>The script, in seconds from the break: a shudder, whole, to
        /// <see cref="ShudderSeconds"/>; the two halves leaning apart on their outer foot edges over
        /// <see cref="HalvesSeconds"/>; then each upper quarter toppling outward off its lower one
        /// and falling to the ground under gravity while the lower quarter slumps; and from
        /// <see cref="SinkFrom"/> everything sinks out of sight. Rock flies less far than a wall:
        /// the faces round it are usually rock too.</para>
        /// </summary>
        public static Matrix4x4 PieceMotion(Vector3 centre, Vector3 extents, Vector3 axis, float seed,
            bool ground, int side, bool upper, float age)
        {
            Vector3 outward = axis * side;
            Vector3 tip = Vector3.Cross(Vector3.up, outward);     // turning about this tips the top outward
            float along = Vector3.Dot(extents, Abs(axis));
            float half = extents.y;
            float floor = centre.y - half;
            float reach = ground ? 0.45f : 1f;

            // Its own small differences, so four pieces never move as one.
            float own = Fract(seed * 7.13f + side * 0.37f + (upper ? 0.61f : 0.19f));

            // The shudder: whole, a few centimetres, quickening.
            Matrix4x4 shake = Matrix4x4.identity;
            if (age < ShudderSeconds)
            {
                float amount = 0.03f * (0.4f + 0.6f * age / ShudderSeconds);
                shake = Matrix4x4.Translate(new Vector3(
                    Mathf.Sin(age * 97f + seed * 40f), 0f, Mathf.Sin(age * 83f + seed * 17f)) * amount);
            }

            // The halves lean apart, each on the outer edge of its foot.
            float opened = Ease(Mathf.Clamp01((age - ShudderSeconds) / HalvesSeconds));
            Vector3 foot = new Vector3(centre.x, floor, centre.z) + outward * along;
            Matrix4x4 halves = Matrix4x4.Translate(outward * (0.06f * opened))
                               * About(foot, (6f + 3f * own) * opened, tip);

            Matrix4x4 piece;
            float falling = age - ShudderSeconds - HalvesSeconds;
            if (falling <= 0f)
            {
                piece = halves;
            }
            else if (upper)
            {
                // Off the top of its lower quarter and down to the ground, turning over as it goes.
                const float gravity = 14f;
                float drop = Mathf.Min(0.5f * gravity * falling * falling, half);
                float flung = reach * (1f - Mathf.Exp(-falling * 2.4f)) * (0.8f + 0.5f * own);
                Vector3 middle = halves.MultiplyPoint3x4(centre + outward * (along * 0.5f) + Vector3.up * (half * 0.5f));
                float turn = Mathf.Min(falling * (200f + 120f * own), 70f + 40f * own);
                piece = Matrix4x4.Translate(outward * flung + Vector3.down * drop)
                        * About(middle, turn, tip)
                        * halves;
            }
            else
            {
                // The lower quarter slumps outward on its foot and slides a little.
                float slump = Mathf.Min(falling * 55f, 12f + 8f * own);
                float slid = reach * 0.18f * (1f - Mathf.Exp(-falling * 3f));
                piece = Matrix4x4.Translate(outward * slid)
                        * About(halves.MultiplyPoint3x4(foot), slump, tip)
                        * halves;
            }

            // And everything into the ground.
            float sink = Mathf.Clamp01((age - SinkFrom) / (BreakSeconds - SinkFrom));
            Vector3 down = Vector3.down * (sink * sink * (half * 2f + 0.3f));
            return Matrix4x4.Translate(down) * piece * shake;
        }

        Matrix4x4 PieceMotion(Break b, int side, bool upper, float age) =>
            PieceMotion(b.Centre, b.Extents, b.Axis, b.Seed, b.Ground, side, upper, age);

        static Matrix4x4 About(Vector3 pivot, float degrees, Vector3 axis) =>
            Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.AngleAxis(degrees, axis)) * Matrix4x4.Translate(-pivot);

        static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        static float Fract(float v) => v - Mathf.Floor(v);

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static Bounds TransformBounds(in Matrix4x4 matrix, Bounds local)
        {
            Vector3 centre = matrix.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            Vector3 extents = new Vector3(
                Mathf.Abs(matrix.m00) * e.x + Mathf.Abs(matrix.m01) * e.y + Mathf.Abs(matrix.m02) * e.z,
                Mathf.Abs(matrix.m10) * e.x + Mathf.Abs(matrix.m11) * e.y + Mathf.Abs(matrix.m12) * e.z,
                Mathf.Abs(matrix.m20) * e.x + Mathf.Abs(matrix.m21) * e.y + Mathf.Abs(matrix.m22) * e.z);
            return new Bounds(centre, extents * 2f);
        }

        MaterialPropertyBlock BreakProps(int index)
        {
            while (_breakProps.Count <= index) _breakProps.Add(new MaterialPropertyBlock());
            return _breakProps[index];
        }

        void DisposeBreaks()
        {
            foreach (Watched w in _watched) w.Entry.Batch.Dispose();
            _watched.Clear();
            foreach (Break b in _breaks) b.Entry.Batch.Dispose();
            _breaks.Clear();
            foreach (Material material in _shardMaterials.Values) DestroyBreakMaterial(material);
            _shardMaterials.Clear();
            for (int i = 0; i < _blockMaterials.Length; i++)
            {
                if (_blockMaterials[i] != null) DestroyBreakMaterial(_blockMaterials[i]!);
                _blockMaterials[i] = null;
            }
        }

        static void DestroyBreakMaterial(Material material)
        {
            if (Application.isPlaying) Object.Destroy(material);
            else Object.DestroyImmediate(material);
        }
    }
}
