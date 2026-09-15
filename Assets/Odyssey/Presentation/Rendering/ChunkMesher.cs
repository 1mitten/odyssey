#nullable enable
using System.Collections.Generic;
using Odyssey.Presentation.World;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Turns one chunk of the cell mirror into instanced draw buckets.
    ///
    /// Everything interesting about how the world looks is decided here, and all of it is per
    /// cell arithmetic with no allocation in the steady state:
    ///
    /// - **Solid strata are face-culled.** A rock cell with six solid neighbours is not drawn.
    ///   Underground layers are mostly solid, so this is the difference between drawing a chunk
    ///   and drawing its surface.
    /// - **Walls are drawn on faces, not in cells.** A wall occupies a whole 2.5 m cell, so
    ///   drawing a wall-shaped box would give a 2.5 m thick slab. Instead each exposed vertical
    ///   face of a wall cell takes a panel, which is what makes a stamped shell read as a
    ///   building from outside and as a room from inside.
    /// - **Doors get one leaf, in the middle.** Two panels on opposite faces of the same cell
    ///   would read as an airlock.
    /// - **Stairs climb.** The lower half sits on the floor, the upper half 1.5 m up, both facing
    ///   the direction of travel — which is the kit's native geometry (<c>e-01</c>).
    /// </summary>
    public sealed class ChunkMesher
    {
        readonly Dictionary<long, int> _bodyIndex = new Dictionary<long, int>();
        readonly Dictionary<long, int> _roofIndex = new Dictionary<long, int>();
        readonly WorldRenderModel _model;

        public ChunkMesher(WorldRenderModel model) => _model = model;

        public void Mesh(ChunkBatch batch, int chunkIndex)
        {
            _model.ChunkBounds(chunkIndex, out int x0, out int z0, out int y, out int x1, out int z1);

            batch.ChunkIndex = chunkIndex;
            batch.Layer = y;
            batch.Bounds = ChunkWorldBounds(x0, z0, y, x1, z1);
            batch.Clear();
            _bodyIndex.Clear();
            _roofIndex.Clear();
            for (int i = 0; i < batch.Body.Count; i++)
                _bodyIndex[KeyOf(batch.Body[i])] = i;
            for (int i = 0; i < batch.Roof.Count; i++)
                _roofIndex[KeyOf(batch.Roof[i])] = i;

            var size = _model.Size;
            for (int z = z0; z < z1; z++)
            for (int x = x0; x < x1; x++)
            {
                int index = size.Index(x, z, y);
                EmitTerrain(batch, index, x, z, y);
                EmitFloor(batch, index, x, z, y);
                EmitEdifice(batch, index, x, z, y);
            }

            batch.Version = _model.Version;
        }

        /// <summary>
        /// The chunk's cell volume, padded.
        ///
        /// The padding is not cosmetic. <c>RenderParams.worldBounds</c> is what culling believes,
        /// and modules legitimately overhang their cells: a wall panel straddles the face it sits
        /// on, a pillar is 3.02 m in a 3.00 m layer, the upper half of a stair reaches 3.33 m. Too
        /// tight a box and whole chunks vanish at the screen edge, which is a horrible bug to
        /// track down because the geometry is provably correct.
        /// </summary>
        const float BoundsPadding = 2f;

        static Bounds ChunkWorldBounds(int x0, int z0, int y, int x1, int z1)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(
                    x0 * CellMetrics.SizeXZ - BoundsPadding,
                    y * CellMetrics.SizeY - BoundsPadding,
                    z0 * CellMetrics.SizeXZ - BoundsPadding),
                new Vector3(
                    x1 * CellMetrics.SizeXZ + BoundsPadding,
                    (y + 1) * CellMetrics.SizeY + BoundsPadding,
                    z1 * CellMetrics.SizeXZ + BoundsPadding));
            return bounds;
        }

        // --------------------------------------------------------------- cells

        void EmitTerrain(ChunkBatch batch, int index, int x, int z, int y)
        {
            ushort terrain = _model.Terrain(index);
            if (terrain == CoreContent.TerrainAir) return;

            int module = _model.TerrainModule(index);
            if (module == 0) return;

            int tint = TintCode.Terrain(terrain);
            Matrix4x4 at = Matrix4x4.Translate(CellMetrics.FloorCentre(x, z, y));

            if (!_model.IsSolid(index))
            {
                // A surface material: pavement, soil, rubble. One tile on the cell floor, and it
                // belongs to the roof list so that a storey above the slice can drop its ground.
                AddRoof(batch, module, tint, at);
                return;
            }

            if (!HasExposedFace(index, x, z, y)) return;
            AddBody(batch, module, tint, at);
        }

        /// <summary>A buried cell is invisible. At the map edge the strata are cut away on purpose.</summary>
        bool HasExposedFace(int index, int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) return true;
                if (!_model.IsSolid(size.Index(nx, nz, y))) return true;
            }
            if (y + 1 >= size.SizeY) return true;
            if (!_model.IsSolid(index + size.LayerStride)) return true;
            // Downwards only matters at the very bottom, which nothing can see.
            return y > 0 && !_model.IsSolid(index - size.LayerStride);
        }

        void EmitFloor(ChunkBatch batch, int index, int x, int z, int y)
        {
            if (_model.IsSolid(index)) return; // a slab inside rock is not visible
            int module = _model.FloorModule(index);
            if (module == 0) return;
            AddRoof(batch, module, TintCode.Stuff(_model.FloorStuff(index)),
                Matrix4x4.Translate(CellMetrics.FloorCentre(x, z, y)));
        }

        void EmitEdifice(ChunkBatch batch, int index, int x, int z, int y)
        {
            ushort def = _model.EdificeDef(index);
            if (def == CoreContent.EdificeNone) return;
            int module = _model.EdificeModule(index);
            if (module == 0) return;

            int tint = TintCode.Stuff(_model.EdificeStuff(index));
            var shape = _model.Library[module].Shape;

            switch (def)
            {
                case CoreContent.EdificeDoor:
                    EmitDoor(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificeStairLower:
                case CoreContent.EdificeStairUpper:
                    EmitStair(batch, module, tint, def, x, z, y);
                    return;
                case CoreContent.EdificeLadder:
                    EmitLadder(batch, module, tint, index, x, z, y);
                    return;
                case CoreContent.EdificePillar:
                case CoreContent.EdificeUtilityTap:
                    AddBody(batch, module, tint, Matrix4x4.Translate(CellMetrics.FloorCentre(x, z, y)));
                    return;
            }

            if (shape != ModuleShape.WallPanel)
            {
                AddBody(batch, module, tint, Matrix4x4.Translate(CellMetrics.FloorCentre(x, z, y)));
                return;
            }

            EmitFacePanels(batch, module, tint, index, x, z, y);
        }

        void EmitFacePanels(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.OccludesFace(size.Index(nx, nz, y))) continue;
                AddBody(batch, module, tint, Matrix4x4.TRS(
                    CellMetrics.FaceCentre(x, z, y, dir),
                    Quaternion.Euler(0f, Directions.Yaw[dir], 0f),
                    Vector3.one));
            }
        }

        void EmitDoor(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int dir = FirstOpenDirection(x, z, y);
            AddBody(batch, module, tint, Matrix4x4.TRS(
                CellMetrics.FloorCentre(x, z, y),
                Quaternion.Euler(0f, Directions.Yaw[dir], 0f),
                Vector3.one));
        }

        void EmitStair(ChunkBatch batch, int module, int tint, ushort def, int x, int z, int y)
        {
            ushort partner = def == CoreContent.EdificeStairLower
                ? CoreContent.EdificeStairUpper
                : CoreContent.EdificeStairLower;
            int dir = FindNeighbour(x, z, y, partner);
            if (dir < 0) dir = Directions.North;

            // The climb runs lower -> upper. Seen from the upper half, that is the way it came.
            int climb = def == CoreContent.EdificeStairLower ? dir : Directions.Opposite(dir);
            float rise = def == CoreContent.EdificeStairLower ? 0f : CellMetrics.SizeY * 0.5f;

            AddBody(batch, module, tint, Matrix4x4.TRS(
                CellMetrics.FloorCentre(x, z, y) + Vector3.up * rise,
                Quaternion.Euler(0f, Directions.Yaw[climb], 0f),
                Vector3.one));
        }

        void EmitLadder(ChunkBatch batch, int module, int tint, int index, int x, int z, int y)
        {
            int wall = FirstOccludingDirection(x, z, y);
            int facing = wall >= 0 ? Directions.Opposite(wall) : Directions.North;
            AddBody(batch, module, tint, Matrix4x4.TRS(
                CellMetrics.FloorCentre(x, z, y),
                Quaternion.Euler(0f, Directions.Yaw[facing], 0f),
                Vector3.one));
        }

        int FirstOpenDirection(int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) return dir;
                if (!_model.OccludesFace(size.Index(nx, nz, y))) return dir;
            }
            return Directions.North;
        }

        int FirstOccludingDirection(int x, int z, int y)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (size.Contains(nx, nz, y) && _model.OccludesFace(size.Index(nx, nz, y))) return dir;
            }
            return -1;
        }

        int FindNeighbour(int x, int z, int y, ushort edificeDef)
        {
            var size = _model.Size;
            for (int dir = 0; dir < Directions.Count; dir++)
            {
                int nx = x + Directions.DeltaX[dir], nz = z + Directions.DeltaZ[dir];
                if (!size.Contains(nx, nz, y)) continue;
                if (_model.EdificeDef(size.Index(nx, nz, y)) == edificeDef) return dir;
            }
            return -1;
        }

        // ------------------------------------------------------------- buckets

        void AddBody(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Body, _bodyIndex, batch, module, tint, placement);

        void AddRoof(ChunkBatch batch, int module, int tint, in Matrix4x4 placement) =>
            Add(batch.Roof, _roofIndex, batch, module, tint, placement);

        void Add(List<InstanceBucket> list, Dictionary<long, int> lookup, ChunkBatch batch,
            int module, int tint, in Matrix4x4 placement)
        {
            var parts = _model.Library[module].Parts;
            for (int p = 0; p < parts.Length; p++)
            {
                long key = Key(module, p, tint);
                if (!lookup.TryGetValue(key, out int slot))
                {
                    slot = list.Count;
                    list.Add(new InstanceBucket { Module = module, Part = p, Tint = tint });
                    lookup.Add(key, slot);
                }
                list[slot].Add(placement * parts[p].Local);
                batch.InstanceCount++;
            }
        }

        static long Key(int module, int part, int tint) =>
            ((long)module << 40) | ((long)part << 20) | (uint)tint;

        static long KeyOf(InstanceBucket bucket) => Key(bucket.Module, bucket.Part, bucket.Tint);
    }
}
