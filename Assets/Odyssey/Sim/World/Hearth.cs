#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// The colony's hearth (design 43 §3f): the one campfire home is centred on. Home is the piece
    /// of the colony's footprint joined to it, so a colony with no hearth has no home and restricts
    /// nobody.
    ///
    /// <para><b>Any campfire may be the hearth, and there is one at most.</b> Campfires stay
    /// unlimited because they heat rooms. The first campfire raised while there is no hearth takes
    /// the title (<see cref="OfferRaised"/>); the player moves it with <c>SetHearth</c> on another
    /// campfire's pane. Taking the hearth down, by deconstruction or by a bandit's hand, leaves no
    /// hearth, and nothing is promoted in its place: the next campfire raised becomes it, or the
    /// player marks one (owner, 2026-09-25).</para>
    ///
    /// <para><b>Saved and hashed only while set.</b> It is the player's choice, and it decides what
    /// a colonist kept home may do, so it is state. Unset it writes a cell of -1 and hashes nothing,
    /// so a colony that never built a campfire hashes as it did before, and no golden moves: none of
    /// them builds one.</para>
    /// </summary>
    public sealed class Hearth : ISaveable, IStateHashable, ISnapshotContributor
    {
        /// <summary>The record layout this build writes. 1: the cell.</summary>
        public const int Layout = 1;

        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;

        public Hearth(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
        }

        /// <summary>The hearth's cell, or -1 when the colony has none.</summary>
        public int Cell { get; private set; } = -1;

        public bool Exists => Cell >= 0;

        /// <summary>Does a campfire of ours stand in this cell — one the colony built and still has?</summary>
        public bool IsOurCampfire(int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return false;
            int handle = _grid.Edifice[cell];
            if (handle < 0 || handle >= _edifices.Count) return false;
            PlacedEdifice record = _edifices[handle];
            return record.Built && !record.Removed && record.Def == CoreContent.EdificeCampfire
                   && record.CellIndex == cell;
        }

        /// <summary>A campfire was just raised here: it becomes the hearth if there is none.</summary>
        public void OfferRaised(int cell)
        {
            if (Cell < 0 && IsOurCampfire(cell)) Set(cell);
        }

        /// <summary>Whatever stood in this cell is gone: if it was the hearth, there is none now.</summary>
        public void Lost(int cell)
        {
            if (cell == Cell) Set(-1);
        }

        /// <summary>
        /// <c>SetHearth(cell)</c>: make the campfire in this cell the hearth. Refused unless a
        /// campfire of ours stands there; <c>AlreadyInThatState</c> for the hearth itself.
        /// </summary>
        public IntentRejection HandleSetHearth(Intent intent)
        {
            CellRef at = intent.Cell;
            if (!_grid.Size.Contains(at)) return IntentRejection.OutOfBounds;
            int cell = _grid.Size.Index(at);
            if (cell == Cell) return IntentRejection.AlreadyInThatState;
            if (!IsOurCampfire(cell)) return IntentRejection.NotPermitted;
            Set(cell);
            return IntentRejection.None;
        }

        void Set(int cell)
        {
            Cell = cell;
            // Which piece of the footprint is home depends on where the hearth is, everywhere.
            _grid.Footprint.TouchAll();
        }

        public string SaveKey => "odyssey.hearth";

        public void Save(SaveWriter writer)
        {
            writer.Write(Layout);
            writer.Write(Cell);
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException($"The hearth section has layout {layout} and this build reads up to {Layout}.");
            int cell = reader.ReadInt();
            Set((uint)cell < (uint)_grid.Size.CellCount ? cell : -1);
        }

        public void ContributeTo(ref StateHash hash)
        {
            if (Cell < 0) return;
            hash.Add(0x48454152); // "HEAR": a marker, so the cell is not mistaken for another count
            hash.Add(Cell);
        }

        public void Contribute(SimWorld world, SnapshotWriter writer) => writer.SetHearthCell(Cell);
    }
}
