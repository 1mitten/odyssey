#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Answers "what is this cell": publishes one <see cref="CellDetail"/> row for the cell the
    /// interface has asked about, and nothing for any other cell.
    ///
    /// <para><b>The per-cell half of the read contract</b>, beside <c>PawnRegistry</c>'s per-pawn
    /// half. Until now the snapshot's answer to "what is on this layer" was one byte per cell
    /// with four meanings, which is what overlays need and no way to describe a tile to a player
    /// who has clicked it. A click asks about one cell on any layer, so the answer is one row,
    /// published while a question stands and costing nothing when none does.</para>
    ///
    /// <para><b>The crossing cost is the simulation's own number, read from the table worldgen
    /// fills.</b> The addends live in <see cref="NaturalContent.ApplyCostClasses"/> — the one
    /// place that owns "boggy is +40, wading is +200" — and are restated here in thousandths of a
    /// clear crossing because that is the ratio a player reads. The class asked for is the
    /// cell's own terrain rather than the navigation grid's entry class: a click on solid ground
    /// asks about the surface, and the surface's price is the terrain's, not the air above it.</para>
    /// </summary>
    public sealed class CellDetailContributor : ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly EnclosureGrid? _enclosure;
        readonly int[] _costByClass = new int[256];

        public CellDetailContributor(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices,
            EnclosureGrid? enclosure = null)
        {
            _grid = grid;
            _edifices = edifices;
            _enclosure = enclosure;
            NaturalContent.ApplyCostClasses(_costByClass);
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            int cell = world.Views.QueryCell;
            if (cell < 0 || cell >= _grid.Terrain.Length) return;

            ushort terrain = _grid.Terrain[cell];

            byte edifice = 0;
            byte quality = 0;
            int owner = 0;
            int handle = _grid.Edifice[cell];
            if (handle >= 0 && handle < _edifices.Count)
            {
                PlacedEdifice placed = _edifices[handle];
                if (!placed.Removed)
                {
                    edifice = (byte)placed.Def;

                    // A bed's own two answers, published beside what it is: the tier its finisher
                    // rolled and who it belongs to. Every other edifice carries zeros and the pane
                    // shows nothing, which is the same silence walls have always had (design 20
                    // §8).
                    if (placed.Def == CoreContent.EdificeBed)
                    {
                        quality = placed.Quality;
                        owner = placed.Owner;
                    }
                }
            }

            byte floorStuff = _grid.Floor[cell] != 0 ? (byte)_grid.FloorStuff[cell] : (byte)0;

            // Zero means "cannot be crossed": impassable water, or a cell with nothing to stand
            // on. Everything else is firm by default and slower only where worldgen says so.
            ushort cost = 0;
            if (!_grid.IsImpassableTerrain(cell) && (_grid.IsSolidTerrain(cell) || _grid.HasFloor(cell)))
                cost = (ushort)(1000 + _costByClass[NaturalContent.CostClassOf(terrain)] * 10);

            ushort workToClear = (ushort)WorldContent.Table[terrain].workToClear;

            bool isIndoors = _enclosure?.IsIndoors(cell) ?? false;
            writer.AddCellDetail(new CellDetail(
                cell, (byte)terrain, edifice, floorStuff, _grid.Support[cell], cost, workToClear,
                quality, owner, isIndoors));
        }
    }
}
