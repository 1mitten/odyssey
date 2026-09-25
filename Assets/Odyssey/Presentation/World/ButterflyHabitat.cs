#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Where a butterfly may live, answered from the render mirror (design 52 §4) — the Unity half of
    /// <see cref="IButterflyHabitat"/>.
    ///
    /// <para><b>Open grass, and nothing else</b> (owner, 2026-09-25): a column is habitat when the
    /// first thing met walking down from the sky is a solid cell whose drawn terrain is grass, with
    /// nothing the colony built standing on it. The drawn terrain rather than the grid's, so a growing
    /// zone — drawn as tilled earth — is not habitat; and "built" rather than "any edifice", so a
    /// tree or a bush is meadow, as it is for the grass tufts (<c>ChunkMesher.EmitScatter</c>, whose
    /// rule this is, asked of the top of a column rather than of one cell). A floor, a roof, water or
    /// rock met first is not habitat, so there are none indoors, over a pond or down a mine.</para>
    ///
    /// <para><b>A column is walked once and remembered</b> until the chunk it runs through changes.
    /// <see cref="SyncDirty"/> is the rain's cover-map trick (<see cref="SkyHeightMap.SyncDirty"/>):
    /// one integer comparison per chunk a frame, and a changed chunk forgets its columns, which are
    /// walked again only when a butterfly asks. Nothing is walked for a column nobody flies over, so
    /// a Huge board costs what the window round the camera costs.</para>
    /// </summary>
    public sealed class ButterflyHabitat : IButterflyHabitat
    {
        const byte Unknown = 0, Meadow = 1, Other = 2;

        readonly WorldRenderModel _model;
        readonly byte[] _kind;
        readonly float[] _top;
        int[]? _seen;

        public ButterflyHabitat(WorldRenderModel model)
        {
            _model = model;
            int columns = model.Size.SizeX * model.Size.SizeZ;
            _kind = new byte[columns];
            _top = new float[columns];
        }

        /// <summary>How many columns have been walked since the session began. For a test that says
        /// a still frame walks nothing.</summary>
        public int Walks { get; private set; }

        /// <summary>
        /// Forget the columns of every chunk that has changed since the last call. The first call
        /// only learns the versions: nothing has been walked yet, so there is nothing to forget.
        /// </summary>
        public int SyncDirty()
        {
            int count = _model.Chunks.Count;
            if (_seen == null || _seen.Length != count)
            {
                _seen = new int[count];
                for (int i = 0; i < count; i++) _seen[i] = _model.ChunkVersion(i);
                return 0;
            }

            int changed = 0;
            int w = _model.Size.SizeX;
            for (int i = 0; i < count; i++)
            {
                int version = _model.ChunkVersion(i);
                if (version == _seen[i]) continue;
                _seen[i] = version;
                changed++;
                _model.ChunkBounds(i, out int x0, out int z0, out _, out int x1, out int z1);
                for (int z = z0; z < z1; z++)
                    Array.Clear(_kind, z * w + x0, x1 - x0);
            }
            return changed;
        }

        public bool Habitat(float x, float z, out float ground)
        {
            int column = Column(x, z);
            if (column < 0)
            {
                ground = 0f;
                return false;
            }
            if (_kind[column] == Unknown) Walk(column);
            ground = _top[column] + GroundRelief.HeightAt(x, z);
            return _kind[column] == Meadow;
        }

        public float Surface(float x, float z)
        {
            int column = Column(x, z);
            if (column < 0) return 0f;
            if (_kind[column] == Unknown) Walk(column);
            return _top[column] + GroundRelief.HeightAt(x, z);
        }

        /// <summary>
        /// Whether the Meadow dressing strews a flower on this cell — its own answer, asked at the
        /// shipped density, so a butterfly lands where a flower is drawn (design 38 §17).
        /// </summary>
        public bool Flowers(float x, float z)
        {
            int cx = Mathf.FloorToInt(x / CellMetrics.SizeXZ), cz = Mathf.FloorToInt(z / CellMetrics.SizeXZ);
            return MeadowDressing.SmallPiece(cx, cz, 0, 1f, false) == MeadowDressing.Kind.Flower
                   || MeadowDressing.SmallPiece(cx, cz, 3, 1f, false) == MeadowDressing.Kind.Sunflower;
        }

        int Column(float x, float z)
        {
            int cx = Mathf.FloorToInt(x / CellMetrics.SizeXZ), cz = Mathf.FloorToInt(z / CellMetrics.SizeXZ);
            GridSize size = _model.Size;
            if (cx < 0 || cz < 0 || cx >= size.SizeX || cz >= size.SizeZ) return -1;
            return cz * size.SizeX + cx;
        }

        /// <summary>Down from the top of the drawn world to the first thing that stops the sky.</summary>
        void Walk(int column)
        {
            Walks++;
            GridSize size = _model.Size;
            int x = column % size.SizeX, z = column / size.SizeX;
            int top = Math.Min(size.SizeY - 1, _model.HighestOccupiedLayer);

            byte kind = Other;
            float height = 0f;
            for (int y = top; y >= 0; y--)
            {
                int index = _model.Index(x, z, y);
                if (_model.Floor(index) != CoreContent.SlabNone)
                {
                    height = y * CellMetrics.SizeY + CellMetrics.SlabLift;
                    break;
                }

                ushort terrain = _model.DrawnTerrain(index);
                if (NaturalContent.IsWater(terrain))
                {
                    height = y * CellMetrics.SizeY + CellMetrics.SizeY * ChunkMesher.WaterSurface;
                    break;
                }

                if (!_model.IsSolid(index)) continue;
                height = (y + 1) * CellMetrics.SizeY;
                if (terrain == NaturalContent.TerrainGrass && !BuiltOn(index, y)) kind = Meadow;
                break;
            }

            _kind[column] = kind;
            _top[column] = height;
        }

        bool BuiltOn(int index, int y)
        {
            if (y + 1 >= _model.Size.SizeY) return false;
            ushort above = _model.EdificeDef(index + _model.Size.LayerStride);
            return above != CoreContent.EdificeNone && !NaturalContent.IsNatural(above);
        }
    }
}
