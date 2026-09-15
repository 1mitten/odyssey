#nullable enable
using System.Collections.Generic;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The geometry half of the publish seam: copies the cell grid into
    /// <see cref="WorldRenderModel"/> at tick end, and fills the snapshot's per-cell slice channel.
    ///
    /// It runs as a snapshot contributor, which is the point. The copy happens in phase 6, after
    /// every system has finished mutating the world and before anything can observe a half-written
    /// frame — so the renderer, which reads the mirror between ticks, is reading a settled world
    /// by construction rather than by luck. This is the only class in the presentation assembly
    /// that holds a <see cref="CellGrid"/>.
    /// </summary>
    public sealed class GridMirrorContributor : ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly WorldRenderModel _model;
        bool _primed;

        public GridMirrorContributor(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices, WorldRenderModel model)
        {
            _grid = grid;
            _edifices = edifices;
            _model = model;
        }

        public WorldRenderModel Model => _model;

        /// <summary>Cells the renderer draws as walkable, blocked or solid. One byte each.</summary>
        public const byte SliceEmpty = 0;
        public const byte SliceWalkable = 1;
        public const byte SliceBlocked = 2;
        public const byte SliceSolid = 3;

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            if (!_primed)
            {
                _model.RefreshAll(_grid, _edifices);
                _primed = true;
            }
            else
            {
                _model.RefreshDirty(_grid, _edifices);
            }

            WriteSliceChannel(world, writer);
        }

        /// <summary>
        /// One byte per cell of the active layer, which is what overlays and the minimap mesh
        /// from later milestones read. The renderer does not use it — geometry comes from the
        /// mirror — but the snapshot is a contract and publishing a complete one now means the UI
        /// line is not blocked on this file.
        /// </summary>
        void WriteSliceChannel(SimWorld world, SnapshotWriter writer)
        {
            var size = world.Size;
            int layer = world.Views.SliceLayer;
            if (layer < 0) layer = 0;
            if (layer >= size.SizeY) layer = size.SizeY - 1;

            var slice = writer.BeginSlice(size.LayerStride);
            int baseIndex = layer * size.LayerStride;
            for (int i = 0; i < slice.Length; i++)
            {
                int index = baseIndex + i;
                if (_grid.IsSolidTerrain(index)) slice[i] = SliceSolid;
                else if (_grid.IsBlockedByEdifice(index)) slice[i] = SliceBlocked;
                else if (_grid.HasFloor(index)) slice[i] = SliceWalkable;
                else slice[i] = SliceEmpty;
            }
        }
    }
}
