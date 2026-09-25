#nullable enable
using System;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Where the rain stops, column by column: the one texture every rain shader asks
    /// "is this under cover?" (<c>OdysseyWeather.hlsl</c>).
    ///
    /// <para>R is the height in metres at which rain stops over the column — the top of the highest
    /// roof slab, solid cell or water surface, or a tree's canopy if that is higher. G is what it
    /// lands on (ground, water, canopy, something built), stored as kind / 4. One texel per
    /// column: 115 KB on the Huge board as half floats.</para>
    ///
    /// <para><b>The rule is the simulation's</b> (design 43 §6, P1). Which layer the rain stops at
    /// is <see cref="SkyColumnRule.Compute{T}"/>, the same function the simulation's
    /// <see cref="SkyColumns"/> calls, asked here of the render mirror
    /// (<see cref="MirrorSkySource"/>) because the mirror is what the picture has to agree with.
    /// This class owns only the metres — where in that layer a slab, a pond's surface or a
    /// crown sits — and <c>SkyAgreementTests</c> holds the two readers to the same answer cell
    /// by cell on the played board, the <c>TerraceFoot</c>/<c>BankLayout</c> pattern. So the
    /// pace penalty and the drawn rain cannot disagree about where a roof is.</para>
    ///
    /// <para>Cost: <see cref="Rebuild"/> is the rule's board-wide form
    /// (<see cref="SkyColumnRule.ComputeBoard{T}"/>), one walk per column from the top down until
    /// it meets something — usually a few cells. <see cref="SyncDirty"/> asks the rule only for the
    /// columns of the chunks that changed and the canopy's reach around them.</para>
    ///
    /// <para><b>One difference in the metres, and it is deliberate.</b> The rule compares a canopy
    /// with a column's own landing in layers, so a pond one layer above a trunk and within its
    /// reach counts as under the crown, where comparing metres would have put the pond's surface
    /// (0.72 of a layer) above a crown 4.5 m up. The simulation must answer in layers, and the
    /// picture follows it rather than the other way round; the case needs a pond on the terrace
    /// above a tree, within a column of it.</para>
    /// </summary>
    public sealed class SkyHeightMap : IDisposable
    {
        public const float KindGround = 0f, KindWater = 1f, KindCanopy = 2f, KindBuilt = 3f;

        /// <summary>
        /// How far above the ground a tree's canopy keeps the rain off, in metres. A crown's
        /// underside on the Meadow trees sits roughly here; streaks stopping at it vanish inside
        /// the leaves rather than above them.
        /// </summary>
        public static float CanopyHeight { get; set; } = 4.5f;

        /// <summary>How many columns either side of a trunk its canopy covers: the simulation's, never a second number.</summary>
        public static int CanopyReach => SkyColumnRule.CanopyReach;

        static readonly int SkyTexId = Shader.PropertyToID("_OdysseySkyTex");
        static readonly int SkyParamsId = Shader.PropertyToID("_OdysseySkyParams");

        readonly WorldRenderModel _model;
        readonly Texture2D _texture;
        readonly Color[] _pixels;
        readonly float[] _stop;
        readonly byte[] _kind;
        readonly SkyColumn[] _columns;
        readonly int[] _trunks;

        public SkyHeightMap(WorldRenderModel model)
        {
            _model = model;
            int w = model.Size.SizeX, d = model.Size.SizeZ;
            _texture = new Texture2D(w, d, TextureFormat.RGBAHalf, mipChain: false, linear: true)
            {
                name = "Odyssey/SkyHeight",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            _pixels = new Color[w * d];
            _stop = new float[w * d];
            _kind = new byte[w * d];
            _columns = new SkyColumn[w * d];
            _trunks = new int[w * d];
        }

        public Texture2D Texture => _texture;

        public int Width => _model.Size.SizeX;
        public int Depth => _model.Size.SizeZ;

        /// <summary>The rain-stop height over a column, in metres. For tests and the particle arm.</summary>
        public float StopAt(int x, int z) => _stop[z * _model.Size.SizeX + x];

        /// <summary>What the rain lands on over a column (see the Kind constants).</summary>
        public float KindAt(int x, int z) => _kind[z * _model.Size.SizeX + x];

        /// <summary>Every column, by the rule's board-wide form, then upload and publish.</summary>
        public void Rebuild()
        {
            SkyColumnRule.ComputeBoard(new MirrorSkySource(_model), _columns, _trunks);
            for (int c = 0; c < _columns.Length; c++)
            {
                _stop[c] = Metres(_columns[c], out byte kind);
                _kind[c] = kind;
            }
            Upload();
        }

        /// <summary>
        /// The columns a set of chunks covers, and the canopy reach around them: a trunk that went
        /// in the rectangle changes the columns within reach of it, and nothing further.
        /// </summary>
        public void RebuildChunkColumns(int x0, int z0, int x1, int z1)
        {
            int r = CanopyReach;
            int w = _model.Size.SizeX, d = _model.Size.SizeZ;
            int ax = Math.Max(0, x0 - r), az = Math.Max(0, z0 - r);
            int bx = Math.Min(w - 1, x1 + r), bz = Math.Min(d - 1, z1 + r);
            for (int z = az; z <= bz; z++)
            for (int x = ax; x <= bx; x++)
                Column(x, z);
            Upload();
        }

        int[]? _seen;

        /// <summary>
        /// Rebuild the columns of every chunk that has changed since the last call, on any layer —
        /// a roof built, a tree felled, a floor fallen in. The first call rebuilds the whole board.
        /// Cost: one integer comparison per chunk (a few hundred), and a column walk only for what
        /// moved. Returns how many chunks were found changed.
        /// </summary>
        public int SyncDirty()
        {
            int count = _model.Chunks.Count;
            if (_seen == null || _seen.Length != count)
            {
                _seen = new int[count];
                for (int i = 0; i < count; i++) _seen[i] = _model.ChunkVersion(i);
                Rebuild();
                return count;
            }

            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = -1, z1 = -1, changed = 0;
            for (int i = 0; i < count; i++)
            {
                int version = _model.ChunkVersion(i);
                if (version == _seen[i]) continue;
                _seen[i] = version;
                changed++;
                _model.ChunkBounds(i, out int cx0, out int cz0, out _, out int cx1, out int cz1);
                x0 = Math.Min(x0, cx0);
                z0 = Math.Min(z0, cz0);
                x1 = Math.Max(x1, cx1 - 1);
                z1 = Math.Max(z1, cz1 - 1);
            }
            if (changed > 0) RebuildChunkColumns(x0, z0, x1, z1);
            return changed;
        }

        /// <summary>Forget what has been seen, so the next <see cref="SyncDirty"/> rebuilds everything.</summary>
        public void Invalidate() => _seen = null;

        void Column(int x, int z)
        {
            int columnIndex = z * _model.Size.SizeX + x;
            SkyColumn answer = SkyColumnRule.Compute(new MirrorSkySource(_model), x, z);
            _stop[columnIndex] = Metres(answer, out byte kind);
            _kind[columnIndex] = kind;
        }

        /// <summary>
        /// Where in its layer the rain lands, in metres, and on what. The layer is the rule's; the
        /// height within it is the drawing's own: a solid cell's top, a slab's lift, a pond's
        /// surface, and a crown <see cref="CanopyHeight"/> above the floor its trunk stands on —
        /// inside the second of the <see cref="SkyColumnRule.CanopyLayers"/> it covers.
        /// </summary>
        public static float Metres(SkyColumn answer, out byte kind)
        {
            switch (answer.Kind)
            {
                case SkyStop.Water:
                    kind = (byte)KindWater;
                    return answer.StopLayer * CellMetrics.SizeY + CellMetrics.SizeY * ChunkMesher.WaterSurface;
                case SkyStop.Built:
                    kind = (byte)KindBuilt;
                    return answer.StopLayer * CellMetrics.SizeY + CellMetrics.SlabLift;
                case SkyStop.Canopy:
                    kind = (byte)KindCanopy;
                    return answer.TrunkLayer * CellMetrics.SizeY + CanopyHeight;
                case SkyStop.Ground:
                    kind = (byte)KindGround;
                    return answer.StopLayer * CellMetrics.SizeY;
                default:
                    kind = (byte)KindGround;
                    return 0f;
            }
        }

        void Upload()
        {
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = new Color(_stop[i], _kind[i] / 4f, 0f, 1f);
            _texture.SetPixels(_pixels);
            _texture.Apply(updateMipmaps: false);

            Shader.SetGlobalTexture(SkyTexId, _texture);
            Shader.SetGlobalVector(SkyParamsId, new Vector4(
                1f / (_model.Size.SizeX * CellMetrics.SizeXZ),
                1f / (_model.Size.SizeZ * CellMetrics.SizeXZ),
                1f, 0f));
        }

        /// <summary>Unpublish, so nothing drawn afterwards reads a map of a world that is gone.</summary>
        public void Dispose()
        {
            Shader.SetGlobalVector(SkyParamsId, Vector4.zero);
            if (Application.isPlaying) UnityEngine.Object.Destroy(_texture);
            else UnityEngine.Object.DestroyImmediate(_texture);
        }
    }

    /// <summary>
    /// The render mirror's answers to the column rule's four questions (design 43 §6). Read off
    /// the same arrays the chunk mesher draws from, so what the rule is told is what is drawn.
    /// </summary>
    public readonly struct MirrorSkySource : ISkyColumnSource
    {
        readonly WorldRenderModel _model;

        public MirrorSkySource(WorldRenderModel model) => _model = model;

        public GridSize Size => _model.Size;
        public bool IsSolid(int index) => _model.IsSolid(index);
        public bool IsWater(int index) => NaturalContent.IsWater(_model.Terrain(index));
        public bool HasSlab(int index) => _model.Floor(index) != 0;
        public bool IsTree(int index) => NaturalContent.IsTree(_model.EdificeDef(index));
    }
}
