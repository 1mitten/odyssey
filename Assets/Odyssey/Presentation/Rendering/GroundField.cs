#nullable enable

using System;
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// What the ground is at each column of the board, as a small texture the shaders read in
    /// world space (design 38 §24): one texel per column, so a shader sampling it with a bilinear
    /// filter and a little noise warp gets soft, uneven boundaries metres wide instead of a cell's
    /// square edge.
    ///
    /// <para><b>Why it exists.</b> The marsh round every stream and pond was drawn per cell with its
    /// own tiled texture, so it came out as a ring of pale square tiles; and a pond's deep middle
    /// was a cross of darker water squares inside the shallow. Both are the grid showing through.
    /// A per-cell material can only change at a cell's edge; a field sampled per pixel can change
    /// anywhere, so the marsh fades into the meadow and the deep water into the shallow.</para>
    ///
    /// <para><b>Channels.</b> R: the column's surface is marsh. G: its surface is sand or gravel.
    /// B: its surface is water. A: that water is deep. Each is 0 or 1 here; the softness is the
    /// sampler's and the shader's.</para>
    ///
    /// <para><b>Drawing only.</b> Built from the render mirror, recomputable at any time, never in a
    /// cell, a save or the hash. Rebuilt a chunk's columns at a time when that chunk re-meshes —
    /// which is when anything in it changed — and uploaded once a frame at most.</para>
    /// </summary>
    public sealed class GroundField : IDisposable
    {
        public static readonly int FieldId = Shader.PropertyToID("_OdysseyGroundField");
        public static readonly int FieldStId = Shader.PropertyToID("_OdysseyGroundFieldST");
        public static readonly int FieldOnId = Shader.PropertyToID("_OdysseyGroundFieldOn");
        public static readonly int FlowId = Shader.PropertyToID("_OdysseyWaterFlow");

        readonly WorldRenderModel _model;
        readonly Texture2D _texture;
        readonly Color32[] _texels;
        bool _dirty;

        // The water's flow (§24f): a second texture of the same size, RG the direction the water
        // runs (0.5 is none), B how fast. Rebuilt whole, and only when a column's water changed —
        // it is a walk over the water network, and water is not dug every frame.
        readonly Texture2D _flow;
        readonly Color32[] _flowTexels;
        readonly short[] _waterLayer;
        readonly int[] _distance;
        readonly int[] _queue;
        bool _flowDirty;

        public GroundField(WorldRenderModel model)
        {
            _model = model;
            var size = model.Size;
            _texels = new Color32[size.SizeX * size.SizeZ];
            _texture = new Texture2D(size.SizeX, size.SizeZ, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "Odyssey/GroundField",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _flowTexels = new Color32[size.SizeX * size.SizeZ];
            _waterLayer = new short[size.SizeX * size.SizeZ];
            _distance = new int[size.SizeX * size.SizeZ];
            _queue = new int[size.SizeX * size.SizeZ];
            _flow = new Texture2D(size.SizeX, size.SizeZ, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "Odyssey/WaterFlow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
                _texels[z * size.SizeX + x] = Column(x, z);
            _dirty = true;
            _flowDirty = true;
        }

        public Texture2D Flow => _flow;

        /// <summary>The flow texel for one column, for tests. Rebuilds the flow if it is stale.</summary>
        public Color32 FlowAt(int x, int z)
        {
            if (_flowDirty) RebuildFlow();
            return _flowTexels[z * _model.Size.SizeX + x];
        }

        public Texture2D Texture => _texture;

        /// <summary>The texel for one column, for tests.</summary>
        public Color32 At(int x, int z) => _texels[z * _model.Size.SizeX + x];

        /// <summary>Recompute the columns a chunk covers. Called when that chunk re-meshes.</summary>
        public void RefreshChunk(int chunkIndex)
        {
            _model.ChunkBounds(chunkIndex, out int x0, out int z0, out _, out int x1, out int z1);
            int width = _model.Size.SizeX;
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                short layerWas = _waterLayer[z * width + x];
                Color32 next = Column(x, z);
                if (_waterLayer[z * width + x] != layerWas) _flowDirty = true;
                ref Color32 held = ref _texels[z * width + x];
                if (held.r == next.r && held.g == next.g && held.b == next.b && held.a == next.a) continue;
                held = next;
                _dirty = true;
            }
        }

        /// <summary>Upload if anything changed, and publish the field as a global. Once a frame, before anything draws.</summary>
        public void Publish()
        {
            if (_dirty)
            {
                _texture.SetPixels32(_texels);
                _texture.Apply(updateMipmaps: false);
                _dirty = false;
            }
            if (_flowDirty)
            {
                RebuildFlow();
                _flow.SetPixels32(_flowTexels);
                _flow.Apply(updateMipmaps: false);
            }

            var size = _model.Size;
            Shader.SetGlobalTexture(FieldId, _texture);
            Shader.SetGlobalTexture(FlowId, _flow);
            Shader.SetGlobalVector(FieldStId, new Vector4(
                1f / (size.SizeX * CellMetrics.SizeXZ), 1f / (size.SizeZ * CellMetrics.SizeXZ), 0f, 0f));
            Shader.SetGlobalFloat(FieldOnId, WaterShore.Enabled ? 1f : 0f);
        }

        /// <summary>Turn the field off for every shader: the square look, exactly as before.</summary>
        public static void Withdraw() => Shader.SetGlobalFloat(FieldOnId, 0f);

        Color32 Column(int x, int z)
        {
            var size = _model.Size;
            _waterLayer[z * size.SizeX + x] = -1;
            for (int y = size.SizeY - 1; y >= 0; y--)
            {
                int index = size.Index(x, z, y);
                ushort terrain = _model.Terrain(index);
                if (NaturalContent.IsWater(terrain))
                {
                    _waterLayer[z * size.SizeX + x] = (short)y;
                    return new Color32(0, 0, 255, terrain == NaturalContent.TerrainDeepWater ? (byte)255 : (byte)0);
                }
                if (!_model.IsSolid(index)) continue;
                byte marsh = terrain == NaturalContent.TerrainMarsh ? (byte)255 : (byte)0;
                byte sand = terrain == NaturalContent.TerrainSand || terrain == NaturalContent.TerrainPackedGravel
                    ? (byte)255 : (byte)0;
                return new Color32(marsh, sand, 0, 0);
            }
            return new Color32(0, 0, 0, 0);
        }

        /// <summary>
        /// **Which way the water runs** (design 38 §24f). Water leaves a stretch where it can fall:
        /// beside water a layer or more lower (a cascade), beside open air at its own layer (a
        /// fall), or off the board's edge. Those columns are the outlets; a walk out from them
        /// along water at the same layer gives every column its distance to one, and the water
        /// runs down that distance — at an outlet, straight over the edge. A stretch no walk
        /// reaches has no outlet and is still: a pond. Speed falls with how open the water is, so
        /// a channel runs and a wide pool barely drifts.
        /// </summary>
        void RebuildFlow()
        {
            _flowDirty = false;
            var size = _model.Size;
            int w = size.SizeX, d = size.SizeZ;
            int head = 0, tail = 0;
            for (int i = 0; i < w * d; i++)
            {
                _distance[i] = int.MaxValue;
                if (_waterLayer[i] >= 0 && Spill(i % w, i / w, out _, out _))
                {
                    _distance[i] = 0;
                    _queue[tail++] = i;
                }
            }
            while (head < tail)
            {
                int c = _queue[head++];
                int cx = c % w, cz = c / w;
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = cx + DX[dir], nz = cz + DZ[dir];
                    if (nx < 0 || nz < 0 || nx >= w || nz >= d) continue;
                    int n = nz * w + nx;
                    if (_waterLayer[n] != _waterLayer[c] || _distance[n] != int.MaxValue) continue;
                    _distance[n] = _distance[c] + 1;
                    _queue[tail++] = n;
                }
            }

            for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
            {
                int c = z * w + x;
                float fx = 0f, fz = 0f;
                if (_waterLayer[c] >= 0 && _distance[c] != int.MaxValue)
                {
                    if (_distance[c] == 0) Spill(x, z, out fx, out fz);
                    else
                        for (int dir = 0; dir < 4; dir++)
                        {
                            int nx = x + DX[dir], nz = z + DZ[dir];
                            if (nx < 0 || nz < 0 || nx >= w || nz >= d) continue;
                            int n = nz * w + nx;
                            if (_waterLayer[n] == _waterLayer[c] && _distance[n] < _distance[c]) { fx += DX[dir]; fz += DZ[dir]; }
                        }
                }
                float length = Mathf.Sqrt(fx * fx + fz * fz);
                if (length < 1e-4f) { _flowTexels[c] = new Color32(128, 128, 0, 0); continue; }
                fx /= length;
                fz /= length;

                // Open water drifts, a channel runs: the share of the 3 x 3 round it that is water.
                int wet = 0;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= d) continue;
                    if (_waterLayer[nz * w + nx] == _waterLayer[c]) wet++;
                }
                float speed = Mathf.Clamp(1.25f - (wet - 3) * 0.13f, 0.3f, 1f);
                _flowTexels[c] = new Color32(
                    (byte)Mathf.RoundToInt((fx * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((fz * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt(speed * 255f), 0);
            }
        }

        static readonly int[] DX = { 1, -1, 0, 0 };
        static readonly int[] DZ = { 0, 0, 1, -1 };

        /// <summary>Whether this water column is an outlet, and the sum of the ways it spills (two
        /// outlets on opposite sides cancel to none, and it is still an outlet).</summary>
        bool Spill(int x, int z, out float fx, out float fz)
        {
            fx = fz = 0f;
            var size = _model.Size;
            int layer = _waterLayer[z * size.SizeX + x];
            bool any = false;
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = x + DX[dir], nz = z + DZ[dir];
                bool spills;
                if (nx < 0 || nz < 0 || nx >= size.SizeX || nz >= size.SizeZ) spills = true;
                else
                {
                    int neighbour = _waterLayer[nz * size.SizeX + nx];
                    spills = (neighbour >= 0 && neighbour < layer)
                             || (neighbour < 0 && !_model.IsSolid(size.Index(nx, nz, layer)));
                }
                if (!spills) continue;
                any = true;
                fx += DX[dir];
                fz += DZ[dir];
            }
            return any;
        }

        public void Dispose()
        {
            if (_flow != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_flow);
                else UnityEngine.Object.DestroyImmediate(_flow);
            }
            if (_texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_texture);
                else UnityEngine.Object.DestroyImmediate(_texture);
            }
            Withdraw();
        }
    }
}
