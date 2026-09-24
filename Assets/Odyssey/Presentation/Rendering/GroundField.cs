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

        readonly WorldRenderModel _model;
        readonly Texture2D _texture;
        readonly Color32[] _texels;
        bool _dirty;

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
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
                _texels[z * size.SizeX + x] = Column(x, z);
            _dirty = true;
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
                Color32 next = Column(x, z);
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

            var size = _model.Size;
            Shader.SetGlobalTexture(FieldId, _texture);
            Shader.SetGlobalVector(FieldStId, new Vector4(
                1f / (size.SizeX * CellMetrics.SizeXZ), 1f / (size.SizeZ * CellMetrics.SizeXZ), 0f, 0f));
            Shader.SetGlobalFloat(FieldOnId, WaterShore.Enabled ? 1f : 0f);
        }

        /// <summary>Turn the field off for every shader: the square look, exactly as before.</summary>
        public static void Withdraw() => Shader.SetGlobalFloat(FieldOnId, 0f);

        Color32 Column(int x, int z)
        {
            var size = _model.Size;
            for (int y = size.SizeY - 1; y >= 0; y--)
            {
                int index = size.Index(x, z, y);
                ushort terrain = _model.Terrain(index);
                if (NaturalContent.IsWater(terrain))
                    return new Color32(0, 0, 255, terrain == NaturalContent.TerrainDeepWater ? (byte)255 : (byte)0);
                if (!_model.IsSolid(index)) continue;
                byte marsh = terrain == NaturalContent.TerrainMarsh ? (byte)255 : (byte)0;
                byte sand = terrain == NaturalContent.TerrainSand || terrain == NaturalContent.TerrainPackedGravel
                    ? (byte)255 : (byte)0;
                return new Color32(marsh, sand, 0, 0);
            }
            return new Color32(0, 0, 0, 0);
        }

        public void Dispose()
        {
            if (_texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_texture);
                else UnityEngine.Object.DestroyImmediate(_texture);
            }
            Withdraw();
        }
    }
}
