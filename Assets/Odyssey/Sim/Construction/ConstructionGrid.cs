#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Construction
{
    /// <summary>
    /// Everything the colony has asked for that is not there yet.
    ///
    /// <para><b>Shaped after <c>DesignationGrid</c> on purpose.</b> Per cell, sparse, hashed, saved
    /// and published; validation here and nowhere else, so a site that exists is one that made
    /// sense when it was placed, and whether it still makes sense is the job's question. That file
    /// is the one to read beside this one, and the two deliberately answer the same questions the
    /// same way.</para>
    ///
    /// <para><b>Blueprint and frame are not two records.</b> The reference keeps them as separate
    /// entities and it has to, because its blueprint is a thing in the world with its own def. Ours
    /// is a cell with four numbers on it, so the distinction is arithmetic: a site whose material
    /// has not all arrived is a blueprint, and one whose has is a frame. Nothing has to be created,
    /// destroyed or swapped at the moment the last plank lands, and there is no state machine to
    /// get wrong — the two work givers simply ask different questions of the same row.</para>
    /// </summary>
    public sealed class ConstructionGrid : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly EdificeSaveSection _edificeSave;
        readonly List<PlacedEdifice> _edifices;
        readonly ColonyItems _items;
        readonly byte[] _building;
        readonly byte[] _stuff;
        readonly int[] _delivered;
        readonly int[] _work;
        readonly List<int> _sites = new List<int>();

        /// <summary>
        /// Takes the edifice list as its <see cref="EdificeSaveSection"/> rather than raw, because
        /// this is the one class in the game that <b>appends</b> to that list at run time — and
        /// until 2026-09-17 nothing saved or hashed what it appended. Threading the section through
        /// here means the thing that raises a wall and the thing that writes it down cannot be
        /// wired up separately: <see cref="Edifices"/> hands it on to whoever assembles the save.
        /// </summary>
        public ConstructionGrid(CellGrid grid, EdificeSaveSection edifices, ColonyItems items)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edificeSave = edifices ?? throw new ArgumentNullException(nameof(edifices));
            _edifices = edifices.Records;
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _building = new byte[grid.Size.CellCount];
            _stuff = new byte[grid.Size.CellCount];
            _delivered = new int[grid.Size.CellCount];
            _work = new int[grid.Size.CellCount];
        }

        public GridSize Size => _grid.Size;

        /// <summary>
        /// The standing buildings, as the channel that saves and hashes them. Whoever assembles the
        /// world's save components takes it from here, so a colony that can raise a wall is by
        /// construction a colony that writes that wall down.
        /// </summary>
        public EdificeSaveSection Edifices => _edificeSave;

        /// <summary>Every cell with a site on it, ascending. A stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Sites => _sites;

        public int Count => _sites.Count;

        /// <summary>What is being built here, as a <see cref="BuildingHandle"/> value, or 0.</summary>
        public int At(int index) => _building[index];

        /// <summary>What of, as a <see cref="StuffHandle"/> value.</summary>
        public int StuffAt(int index) => _stuff[index];

        /// <summary>Units of material that have arrived.</summary>
        public int Delivered(int index) => _delivered[index];

        /// <summary>Ticks of work applied. Banked on the cell, for the reason mining banks its own.</summary>
        public int WorkDone(int index) => _work[index];

        /// <summary>Units of material this site is still waiting for. 0 once it is a frame.</summary>
        public int Outstanding(int index)
        {
            if (_building[index] == 0) return 0;
            int wanted = ConstructionContent.BuildingAt(_building[index]).costCount - _delivered[index];
            return wanted < 0 ? 0 : wanted;
        }

        /// <summary>Has every unit of material arrived, so that the thing can be worked on?</summary>
        public bool IsFrame(int index) => _building[index] != 0 && Outstanding(index) == 0;

        /// <summary>What this site costs in ticks of work, or 0 where there is no site.</summary>
        public int WorkFor(int index) =>
            _building[index] == 0 ? 0 : ConstructionContent.WorkFor(_building[index], _stuff[index]);

        /// <summary>How far through its work a site is, 0 to 1. See <c>DesignationGrid.Fraction</c>.</summary>
        public float Fraction(int index)
        {
            int total = WorkFor(index);
            if (total <= 0) return 0f;
            float done = (float)_work[index] / total;
            return done < 0f ? 0f : done > 1f ? 1f : done;
        }

        // ---- placing and cancelling --------------------------------------------------------

        /// <summary>
        /// Put a site on a cell, or say why not. Out of the map is
        /// <see cref="IntentRejection.OutOfBounds"/>; a cell that cannot take it, or a thing or
        /// material that does not exist, is <see cref="IntentRejection.NotPermitted"/>; the same
        /// site again is <see cref="IntentRejection.AlreadyInThatState"/>.
        /// </summary>
        public IntentRejection Place(CellRef cell, int building, int stuff)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            if (!ConstructionContent.IsBuilding(building)) return IntentRejection.NotPermitted;
            if (!ConstructionContent.IsBuildable(stuff)) return IntentRejection.NotPermitted;

            int index = StandingOn(_grid.Index(cell));
            if (_building[index] == building && _stuff[index] == stuff)
                return IntentRejection.AlreadyInThatState;
            if (!Allows(index)) return IntentRejection.NotPermitted;

            // A site that is replaced gives back whatever had been carried to it, exactly as a
            // cancelled one does: the player changing their mind about the material is not a
            // reason to lose five wood, and the alternative is a silent tax on experimenting.
            Refund(index);
            Set(index, building, stuff);
            return IntentRejection.None;
        }

        /// <summary>
        /// The site the player means by naming this cell, or -1 where there is none.
        ///
        /// <para>The mirror of <see cref="StandingOn"/>, and the one answer to "which site is this
        /// click about": the site the player can see over a patch of ground is the one standing on
        /// it, so naming the ground names the site above. Cancelling asked it first and a forced
        /// order asks it now; both go through here rather than each carrying a copy of the rule,
        /// because two copies of a lift are how one of them gets fixed on its own.</para>
        /// </summary>
        public int SiteAt(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return -1;

            int index = _grid.Index(cell);
            if (_building[index] == 0) index = StandingOn(index);
            return _building[index] == 0 ? -1 : index;
        }

        public IntentRejection Cancel(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            int index = SiteAt(cell);
            if (index < 0) return IntentRejection.AlreadyInThatState;

            Refund(index);
            Set(index, BuildingHandle.None, StuffHandle.None);
            return IntentRejection.None;
        }

        /// <summary>
        /// A build order named at solid ground means the cell standing on it.
        ///
        /// <para><b>A click names a surface; an order names a cell.</b> Since 2026-09-16 the picker
        /// answers a click on bare ground with the ground <i>block</i> rather than the air above it
        /// (owner: "I still wanted to select the tile below it or not at all"), and a wall goes in
        /// the air. Without this every cell of a wall dragged across the meadow would be refused in
        /// silence — the player sweeping a tool over open grass and watching nothing happen, which
        /// is the exact fault the fell tool hit and <c>DesignationGrid.TreeAbove</c> fixed on the
        /// other side of the same relation.</para>
        ///
        /// <para>Only where the cell above can actually take a site, so a click on rock with more
        /// rock above it is still a refusal rather than an order placed a layer away from where it
        /// was asked for.</para>
        /// </summary>
        int StandingOn(int index)
        {
            if (!_grid.IsSolidTerrain(index)) return index;
            int above = index + _grid.Size.LayerStride;
            return above < _grid.Size.CellCount && Allows(above) ? above : index;
        }

        /// <summary>
        /// Whether a site may stand in this cell.
        ///
        /// <para>Somewhere to put it: inside the map, in open air, with a floor under it, nothing
        /// already standing there and no water. <b>A tree counts as something standing there</b>,
        /// which is the answer to "can I build through a wood" — fell it first, exactly as the
        /// ground under a tree cannot be mined until the tree is down. The rule about water is the
        /// one <c>DesignationGrid.Allows</c> wrote down in advance for this: <i>a cell you can wade
        /// through is still not one you can put a wall in</i>.</para>
        /// </summary>
        public bool Allows(int index)
        {
            if ((uint)index >= (uint)_grid.Size.CellCount) return false;
            if (_grid.IsSolidTerrain(index)) return false;
            if (NaturalContent.IsWater(_grid.Terrain[index])) return false;
            if (_grid.Edifice[index] >= 0) return false;

            // Something underfoot. A wall hanging in the air is the fault the whole support model
            // exists to prevent, and refusing it at the order is far better than collapsing it
            // afterwards: the player never gave an order that could not be carried out.
            return _grid.HasFloor(index);
        }

        /// <summary>Material has arrived. Returns what the site now holds.</summary>
        public int Deliver(int index, int count)
        {
            _delivered[index] += count;
            return _delivered[index];
        }

        /// <summary>Add ticks of work and return the new total. Banked on the cell, never on the job.</summary>
        public int AddWork(int index, int ticks)
        {
            _work[index] += ticks;
            return _work[index];
        }

        /// <summary>Take the site off without refunding: the thing has been built.</summary>
        public void Clear(int index)
        {
            if (_building[index] != 0) Set(index, BuildingHandle.None, StuffHandle.None);
        }

        /// <summary>
        /// Give back what was carried to a site that is being cancelled or re-materialled.
        ///
        /// <para><b>Not on the site's own cell, which is the obvious answer and wrong.</b> A cell
        /// with a site on it may perfectly well have a stack of something else lying in it — the
        /// site is an order about the cell, not an occupant of it — and a cell holds exactly one
        /// item record, so spawning there throws. It did, on the first run of
        /// <c>CancellingASiteGivesBackWhatWasCarriedToIt</c>, over a piece of salvage the scenario
        /// had put down. The nearest cell with room is the same answer
        /// <see cref="JobDriver.DropCarried"/> gives to the same question.</para>
        /// </summary>
        void Refund(int index)
        {
            int delivered = _delivered[index];
            if (delivered <= 0) return;

            int item = ConstructionContent.StuffAt(_stuff[index]).item;
            if (item < 0) return;

            int at = _items.NearestCellWithSpace(
                _grid, index, item, delivered, JobDriver.DropSearchRadius);

            // A board with no room within that radius is packed solid with things, which nothing in
            // the game can produce. Losing the load is the least bad answer; the alternative is
            // refusing to let the player cancel an order, which is worse.
            if (at >= 0) _items.Spawn(item, at, delivered);
        }

        void Set(int index, int building, int stuff)
        {
            bool was = _building[index] != 0;
            _building[index] = (byte)building;
            _stuff[index] = (byte)stuff;
            // A new site, a cancelled one and a finished one all start the next from nothing.
            _delivered[index] = 0;
            _work[index] = 0;
            bool now = building != BuildingHandle.None;
            if (was == now) return;

            int at = _sites.BinarySearch(index);
            if (now) _sites.Insert(~at, index);
            else _sites.RemoveAt(at);
        }

        // ---- the world edit ------------------------------------------------------------------

        /// <summary>
        /// Make the thing stand. Called from the deferred structural phase, never inline, for the
        /// reason <c>MineJobDriver.MineCell</c> is: a scan walking the world must not see it move.
        ///
        /// <para>Everything that changes is listed here in one place rather than discovered one bug
        /// at a time, which is the shape <c>MineCell</c> settled on.</para>
        ///
        /// <para><b>Support is deliberately not marked dirty.</b> Nothing collapses yet — mining a
        /// load-bearing wall out of a stamped shell does not bring it down either — so marking it
        /// would start the solver running over edits whose consequences U29 has not written. The
        /// omission is the same one mining makes, and the two should be wired together when U29
        /// lands rather than one of them quietly acquiring behaviour the other lacks.</para>
        /// </summary>
        public void Raise(PawnContext ctx, int cell)
        {
            int building = _building[cell];
            if (building == BuildingHandle.None) return;

            BuildingDef def = ConstructionContent.BuildingAt(building);
            ushort stuff = ConstructionContent.StuffAt(_stuff[cell]).stuff;

            Clear(cell);

            // 1. The thing itself, as the record the ruined city's own walls are kept in, so that a
            //    wall a colonist built and a wall the generator stamped are indistinguishable to
            //    everything downstream — the mesher, the picker, deconstruction and the solver.
            // Built = true: ours, and the only place in the game that says so. Everything the
            // generator stamps leaves it false, which is what makes "deconstruct our own buildings
            // and not the ruined city's" a rule that can be asked rather than guessed at.
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = cell, Def = def.edifice, Stuff = stuff, Built = true,
            });
            _grid.Edifice[cell] = _edifices.Count - 1;
            if (def.blocking) _grid.Flags[cell] |= CellFlags.BlockingEdifice;

            // 2. The cell and everything touching it must be re-meshed: a wall changes how its
            //    neighbours draw their own faces, and the vertical neighbours are in other chunks.
            MarkChunksAround(ctx, cell);

            // 3. What is walkable changed here, and in the cell above through the floor rule.
            ctx.Nav.MarkDirty(cell);
            int above = cell + _grid.Size.LayerStride;
            if (above < _grid.Size.CellCount) ctx.Nav.MarkDirty(above);
        }

        /// <summary>
        /// Take a standing building out of the world: <see cref="Raise"/>'s list, inverted, and
        /// beside it on purpose so the two cannot drift.
        ///
        /// <para><b>The record keeps its slot and is marked removed</b> rather than being dropped.
        /// Handles are positions in this list and are part of the determinism contract — every cell
        /// that points at a later entry would otherwise be pointing at the wrong building, which is
        /// the kind of corruption that shows up three saves later as a wall made of the wrong
        /// thing.</para>
        ///
        /// <para><b>Support is deliberately not marked dirty</b>, the same omission <see cref="Raise"/>
        /// and <c>MineCell</c> both make. Nothing collapses yet; U29 wires all three together, and
        /// none of the three should quietly acquire behaviour the others lack.</para>
        ///
        /// <para>What it was is returned, so the caller can pay the refund without asking the world
        /// a question whose answer it has just destroyed.</para>
        /// </summary>
        public bool Demolish(PawnContext ctx, int cell, out PlacedEdifice was)
        {
            int handle = _grid.Edifice[cell];
            if (handle < 0 || handle >= _edifices.Count)
            {
                was = default;
                return false;
            }

            was = _edifices[handle];
            if (was.Removed) return false;

            // 1. The thing itself.
            _grid.RemoveEdifice(cell);
            PlacedEdifice gone = was;
            gone.Removed = true;
            _edifices[handle] = gone;

            // 2. The cell and everything touching it must be re-meshed: a wall coming down changes
            //    how its neighbours draw their own faces, and the vertical neighbours are in other
            //    chunks.
            MarkChunksAround(ctx, cell);

            // 3. What is walkable changed here, and in the cell above through the floor rule.
            ctx.Nav.MarkDirty(cell);
            int above = cell + _grid.Size.LayerStride;
            if (above < _grid.Size.CellCount) ctx.Nav.MarkDirty(above);
            return true;
        }

        static void MarkChunksAround(PawnContext ctx, int cell)
        {
            if (ctx.Chunks == null) return;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);

            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = at.X + dx, z = at.Z + dz, y = at.Y + dy;
                if (size.Contains(x, z, y)) ctx.Chunks.MarkDirty(x, z, y);
            }
        }

        // ---- the intent seam -------------------------------------------------------------------

        /// <summary><c>PlaceBuilding(cell, A = building, B = stuff)</c>.</summary>
        public IntentRejection HandlePlace(Intent intent) => Place(intent.Cell, intent.A, intent.B);

        /// <summary><c>CancelBuilding(cell)</c>.</summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell);

        /// <summary>Register everything this grid is: hashed state, saved state, a channel, two intents.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.PlaceBuilding, HandlePlace)
                .AddIntentHandler(IntentKind.CancelBuilding, HandleCancel);
        }

        // ---- ITickable: registration only, so the hash and the save see the sites -------------

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_sites.Count);
            for (int i = 0; i < _sites.Count; i++)
            {
                int index = _sites[i];
                hash.Add(index);
                hash.Add(_building[index]);
                hash.Add(_stuff[index]);
                hash.Add(_delivered[index]);
                hash.Add(_work[index]);
            }
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "odyssey.construction";

        public void Save(SaveWriter writer)
        {
            writer.Write(_sites.Count);
            for (int i = 0; i < _sites.Count; i++)
            {
                int index = _sites[i];
                writer.Write(index);
                writer.Write(_building[index]);
                writer.Write(_stuff[index]);
                writer.Write(_delivered[index]);
                writer.Write(_work[index]);
            }
        }

        public void Load(SaveReader reader)
        {
            Array.Clear(_building, 0, _building.Length);
            Array.Clear(_stuff, 0, _stuff.Length);
            _sites.Clear();

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int index = reader.ReadInt();
                byte building = reader.ReadByte();
                byte stuff = reader.ReadByte();
                int delivered = reader.ReadInt();
                int work = reader.ReadInt();
                if (index < 0 || index >= _building.Length || building == 0) continue;

                Set(index, building, stuff);
                // After Set, which zeroes both: a half-built wall survives a save, and so does the
                // wood already carried to it.
                _delivered[index] = delivered;
                _work[index] = work;
            }
        }

        // ---- ISnapshotContributor: one entry per site, anywhere in the world -------------------

        /// <summary>
        /// Publish every site the colony has, with both of its measures of progress.
        ///
        /// <para>Sparse and whole-world, for the two reasons <c>DesignationGrid.Contribute</c> gives
        /// at length: a layer is fourteen thousand cells and a colony has tens of sites, and an
        /// order given on a layer that is not the active one must still be drawn.</para>
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                int index = _sites[i];
                byte building = _building[index];
                if (building == 0) continue;

                writer.AddSite(new SiteView(
                    index, building, _stuff[index],
                    (ushort)_delivered[index],
                    (ushort)ConstructionContent.BuildingAt(building).costCount,
                    _work[index], WorkFor(index)));
            }
        }
    }
}
