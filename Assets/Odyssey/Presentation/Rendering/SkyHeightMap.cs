#nullable enable
using System;
using Odyssey.Presentation.World;
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
    /// <para><b>Prototype (claude/rain-look).</b> Built from the render mirror, which is what the
    /// picture has to agree with. In the weather design the same column rule is owned once, in
    /// the simulation, and this becomes its reader — with a test that holds the two to the same
    /// answer cell by cell, the <c>TerraceFoot</c>/<c>BankLayout</c> pattern.</para>
    ///
    /// <para>Cost: <see cref="Rebuild"/> walks every column from the top down until it meets
    /// something — at most the board's layers per column, usually two or three. A game would call
    /// <see cref="RebuildChunkColumns"/> for the chunks <c>RefreshDirty</c> touched instead.</para>
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

        /// <summary>How many columns either side of a trunk its canopy covers. 1 is a 3 × 3.</summary>
        public static int CanopyReach { get; set; } = 1;

        static readonly int SkyTexId = Shader.PropertyToID("_OdysseySkyTex");
        static readonly int SkyParamsId = Shader.PropertyToID("_OdysseySkyParams");

        readonly WorldRenderModel _model;
        readonly Texture2D _texture;
        readonly Color[] _pixels;
        readonly float[] _stop;
        readonly byte[] _kind;

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
        }

        public Texture2D Texture => _texture;

        public int Width => _model.Size.SizeX;
        public int Depth => _model.Size.SizeZ;

        /// <summary>The rain-stop height over a column, in metres. For tests and the particle arm.</summary>
        public float StopAt(int x, int z) => _stop[z * _model.Size.SizeX + x];

        /// <summary>What the rain lands on over a column (see the Kind constants).</summary>
        public float KindAt(int x, int z) => _kind[z * _model.Size.SizeX + x];

        /// <summary>Every column, then the canopies, then upload and publish.</summary>
        public void Rebuild()
        {
            int w = _model.Size.SizeX, d = _model.Size.SizeZ;
            for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
                Column(x, z);
            Canopies(0, 0, w - 1, d - 1);
            Upload();
        }

        /// <summary>The columns a set of chunks covers, and the canopy reach around them.</summary>
        public void RebuildChunkColumns(int x0, int z0, int x1, int z1)
        {
            int r = CanopyReach;
            int w = _model.Size.SizeX, d = _model.Size.SizeZ;
            int ax = Math.Max(0, x0 - r), az = Math.Max(0, z0 - r);
            int bx = Math.Min(w - 1, x1 + r), bz = Math.Min(d - 1, z1 + r);
            for (int z = az; z <= bz; z++)
            for (int x = ax; x <= bx; x++)
                Column(x, z);
            Canopies(Math.Max(0, ax - r), Math.Max(0, az - r), Math.Min(w - 1, bx + r), Math.Min(d - 1, bz + r));
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
            float stop = 0f;
            byte kind = (byte)KindGround;

            for (int y = _model.Size.SizeY - 1; y >= 0; y--)
            {
                int index = _model.Index(x, z, y);
                if (_model.IsSolid(index))
                {
                    stop = (y + 1) * CellMetrics.SizeY;
                    kind = (byte)KindGround;
                    break;
                }
                if (NaturalContent.IsWater(_model.Terrain(index)))
                {
                    stop = y * CellMetrics.SizeY + CellMetrics.SizeY * ChunkMesher.WaterSurface;
                    kind = (byte)KindWater;
                    break;
                }
                if (_model.Floor(index) != 0)
                {
                    stop = y * CellMetrics.SizeY + CellMetrics.SlabLift;
                    kind = (byte)KindBuilt;
                    break;
                }
            }

            _stop[columnIndex] = stop;
            _kind[columnIndex] = kind;
        }

        /// <summary>
        /// Every tree in the rectangle lifts the columns around its trunk to its canopy, where the
        /// canopy is higher than what is already there. A tree stands in the air cell above its
        /// ground, so the canopy is measured from that cell's floor.
        /// </summary>
        void Canopies(int x0, int z0, int x1, int z1)
        {
            int w = _model.Size.SizeX, d = _model.Size.SizeZ, r = CanopyReach;
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            for (int y = _model.Size.SizeY - 1; y >= 0; y--)
            {
                int index = _model.Index(x, z, y);
                if (!NaturalContent.IsTree(_model.EdificeDef(index))) continue;

                float crown = y * CellMetrics.SizeY + CanopyHeight;
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int cx = x + dx, cz = z + dz;
                    if (cx < 0 || cz < 0 || cx >= w || cz >= d) continue;
                    int c = cz * w + cx;
                    if (_stop[c] >= crown) continue;
                    _stop[c] = crown;
                    _kind[c] = (byte)KindCanopy;
                }
                break;
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
}
