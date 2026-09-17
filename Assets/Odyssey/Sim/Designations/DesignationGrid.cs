#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Designations
{
    /// <summary>What the player has asked to happen to a cell. One per cell; a later order replaces an earlier one.</summary>
    public enum DesignationKind : byte
    {
        None = 0,

        /// <summary>Dig the cell out. Only on solid terrain.</summary>
        Mine = 1,

        /// <summary>Take a built thing apart. Only on a construction, never on a tree.</summary>
        Deconstruct = 2,

        /// <summary>Cut a tree down for wood. Only on a tree.</summary>
        Fell = 3,
    }

    /// <summary>
    /// The player's standing orders, one byte per cell.
    ///
    /// A designation is authored state: it is hashed, saved and published, and the simulation
    /// only ever reads it through the intents that create and cancel it. Work givers scan the
    /// designated cells rather than the whole grid, which is why the grid keeps a sorted list of
    /// them beside the byte array — a think scan over 230,000 cells per idle pawn would be the
    /// wrong price for finding three trees.
    ///
    /// Validation lives here and nowhere else, so a designation that exists is one that made
    /// sense when it was placed. Whether it still makes sense is the job's question: the tree
    /// may have been felled by somebody else, and the driver checks before it swings.
    /// </summary>
    public sealed class DesignationGrid : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly byte[] _kinds;
        readonly int[] _work;
        readonly List<int> _cells = new List<int>();

        public DesignationGrid(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
            _kinds = new byte[grid.Size.CellCount];
            _work = new int[grid.Size.CellCount];
        }

        public GridSize Size => _grid.Size;

        public DesignationKind At(int index) => (DesignationKind)_kinds[index];

        /// <summary>
        /// Ticks of work already done on a cell's order.
        ///
        /// <para><b>On the cell, not on the job, and that is the point.</b> It used to live on the
        /// driver as a toil counter, so a miner who stopped for a meal, a sleep or a mental break
        /// took the whole morning's work with it and the next colonist started the cell from
        /// nothing. Nobody would ever have reported that as a bug — the rock does come down
        /// eventually — but it is hours of work quietly thrown away, and it is why a half-cut face
        /// could never be drawn as half cut.</para>
        ///
        /// <para>Authored state: hashed, saved, and cleared when the order is placed, cancelled or
        /// carried out.</para>
        /// </summary>
        public int WorkDone(int index) => _work[index];

        /// <summary>Add a tick of work and return the new total.</summary>
        public int AddWork(int index, int ticks)
        {
            _work[index] += ticks;
            return _work[index];
        }

        /// <summary>
        /// How far through its order a cell is, 0 to 1, or 0 where nothing is ordered.
        ///
        /// Presentation reads this through the snapshot rather than calling it, but it lives here
        /// because the denominator is content — what the terrain costs to clear — and content is
        /// the simulation's business.
        /// </summary>
        public float Fraction(int index)
        {
            if (_kinds[index] == 0) return 0f;
            int total = WorkFor(index);
            if (total <= 0) return 0f;
            float done = (float)_work[index] / total;
            return done < 0f ? 0f : done > 1f ? 1f : done;
        }

        /// <summary>What the order on this cell costs in ticks, or 0 if it has none.</summary>
        public int WorkFor(int index)
        {
            switch ((DesignationKind)_kinds[index])
            {
                case DesignationKind.Mine:
                    return NaturalContent.TerrainAt(_grid.Terrain[index]).workToClear;
                default:
                    return 0;
            }
        }

        /// <summary>Every designated cell index, ascending. Stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Cells => _cells;

        public int Count => _cells.Count;

        // ---- placing and cancelling --------------------------------------------------------

        /// <summary>
        /// Place an order, or say why not. Out of the map is <see cref="IntentRejection.OutOfBounds"/>;
        /// a kind the cell cannot take is <see cref="IntentRejection.NotPermitted"/>; the same order
        /// twice is <see cref="IntentRejection.AlreadyInThatState"/>.
        /// </summary>
        public IntentRejection Designate(CellRef cell, DesignationKind kind)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            if (kind == DesignationKind.None) return IntentRejection.NotPermitted;

            int index = TreeAbove(_grid.Index(cell), kind);
            if (!Allows(index, kind)) return IntentRejection.NotPermitted;
            if (_kinds[index] == (byte)kind) return IntentRejection.AlreadyInThatState;

            Set(index, kind);
            return IntentRejection.None;
        }

        public IntentRejection Cancel(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            int index = _grid.Index(cell);

            // The mirror of TreeAbove: the order the player can see over this patch of ground is
            // the one standing on it, so naming the ground takes it off.
            if (_kinds[index] == 0)
            {
                int above = index + _grid.Size.LayerStride;
                if (_grid.IsSolidTerrain(index) && above < _grid.Size.CellCount && _kinds[above] != 0)
                    index = above;
            }

            if (_kinds[index] == 0) return IntentRejection.AlreadyInThatState;
            Set(index, DesignationKind.None);
            return IntentRejection.None;
        }

        /// <summary>
        /// A fell order named at solid ground means the tree standing on it.
        ///
        /// <para><b>A click names a surface; an order names a cell, and the two stopped being the
        /// same thing on 2026-09-16.</b> The picker now answers a click on bare ground with the
        /// ground <i>block</i> rather than the air above it (owner: "I still wanted to select the
        /// tile below it or not at all") — and a tree is an edifice standing in that air cell. So
        /// a click straight on a tree still names the tree, but a drag box begun on open grass
        /// comes through a layer too low, and every cell of it would be refused in silence. The
        /// player would have swept the tool across a wood and watched nothing happen.</para>
        ///
        /// <para>It is the same relation <see cref="CanMine"/> already knows about from the other
        /// side — that the ground under a standing tree is not diggable while the tree is up — so
        /// the ground and the tree on it were already two views of one thing here.</para>
        ///
        /// <para>Only felling. Mining means the block itself, which is exactly what the click now
        /// gives, and no other kind is about something standing on the ground.</para>
        /// </summary>
        int TreeAbove(int index, DesignationKind kind)
        {
            if (!_grid.IsSolidTerrain(index)) return index;
            int above = index + _grid.Size.LayerStride;
            if (above >= _grid.Size.CellCount) return index;

            // Both kinds that name a thing standing on the ground need this lift, and for one
            // reason: a wall is raised into the air cell above the ground exactly as a tree grows
            // there (`ConstructionGrid.StandingOn` is the same relation from the other side). A
            // drag over open ground comes through a layer too low, and without the lift every
            // cell of it is refused in silence — the player sweeping a tool over their own colony
            // and watching nothing happen.
            return kind switch
            {
                DesignationKind.Fell when IsTree(above) => above,
                DesignationKind.Deconstruct when CanDeconstruct(above) => above,
                _ => index,
            };
        }

        /// <summary>Clear a cell without ceremony: the order was carried out.</summary>
        public void Clear(int index)
        {
            if (_kinds[index] != 0) Set(index, DesignationKind.None);
        }

        /// <summary>Whether the cell can take this kind of order right now.</summary>
        public bool Allows(int index, DesignationKind kind)
        {
            // Nothing is ordered in water. Every kind below would refuse it anyway today — mining
            // wants solid ground, felling wants a tree, deconstructing wants something built —
            // so this line changes no behaviour and is here for the kind that comes next, which
            // would otherwise have to rediscover that a river is not a building site. When the
            // build pipeline lands, the rule it needs is already stated, in the one place this
            // class's own doc says validation lives.
            if (IsWater(index)) return false;

            switch (kind)
            {
                case DesignationKind.Mine:
                    return CanMine(index);
                case DesignationKind.Deconstruct:
                    return CanDeconstruct(index);
                case DesignationKind.Fell:
                    return IsTree(index);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Can this cell be dug out at all?
        ///
        /// <para>Solid terrain, and not bedrock. **Bedrock is the floor of the world**, not merely
        /// an expensive rock: it refuses the order outright rather than quoting the 2,400 ticks it
        /// would otherwise take — roughly forty trees for one cell of nothing. An order a colonist
        /// would spend a day on and get no material from is worse than no order, because the
        /// colonist takes the day.</para>
        ///
        /// <para>And not the ground under a standing tree. Digging it away would leave the tree
        /// rooted in mid-air, and the right answer is to fell it first rather than to invent a
        /// falling rule here. The order becomes available the moment the tree is gone.</para>
        /// </summary>
        public bool CanMine(int index)
        {
            // Solid rock to cut, or a heap to clear. `clearable` is set by exactly one terrain —
            // rubble — and it is a flag rather than a rule because "any non-solid terrain with work
            // to clear" would have offered a Mine order on open water, marsh and the soil band
            // under the city, all of which are non-solid and all of which carry the default
            // workToClear. This is `vertical-slice.md`'s U28 line "breach a slab, clear rubble,
            // mine rock", whose middle speed had nothing to clear until a collapse made some.
            if (!_grid.IsSolidTerrain(index) && !NaturalContent.TerrainAt(_grid.Terrain[index]).clearable)
                return false;
            if (_grid.Terrain[index] == NaturalContent.TerrainBedrock) return false;

            int above = index + _grid.Size.LayerStride;
            return above >= _grid.Size.CellCount || !IsTree(above);
        }

        /// <summary>
        /// Could a colonist get out of this cell once it has been dug out?
        ///
        /// <para><b>The rule that replaced climbing</b> (owner, 2026-09-16). Unaided, a colonist
        /// gets up one block by jumping and no more; deeper than that wants a ladder, which is a
        /// built thing and nothing builds one yet. So the work giver will not hand out a cut that
        /// would strand the miner, and a quarry comes out as a flight of benches rather than as a
        /// shaft with somebody at the bottom of it.</para>
        ///
        /// <para>The test is on the four orthogonal columns, at this layer and the one above it:
        /// somewhere to step out onto, or somewhere to jump up onto. Not the layer below — dropping
        /// deeper is a way further in, not a way out.</para>
        ///
        /// <para><b>Asked when the job is given, not when the order is placed.</b> Marking is the
        /// player saying what they want; whether it can be done safely depends on the world at the
        /// moment somebody goes to do it. A cell buried in the middle of a mass of rock fails this
        /// test today and passes it the moment a tunnel reaches it, and an order that could not be
        /// marked until then would be an order the player could never give in advance. Putting it
        /// in <c>CanMine</c> was tried and it made every buried cell unmarkable, which made the
        /// first cut of a tunnel impossible.</para>
        /// </summary>
        public static bool CanBeLeftAfterCutting(CellGrid grid, int index)
        {
            GridSize size = grid.Size;
            CellRef at = size.FromIndex(index);

            for (int i = 0; i < 4; i++)
            {
                int x = at.X + (i == 0 ? 1 : i == 1 ? -1 : 0);
                int z = at.Z + (i == 2 ? 1 : i == 3 ? -1 : 0);

                if (Standable(x, z, at.Y)) return true;
                if (Standable(x, z, at.Y + 1)) return true;
            }

            return false;

            bool Standable(int x, int z, int y)
            {
                if (!size.Contains(x, z, y)) return false;
                int n = size.Index(x, z, y);
                if (grid.IsSolidTerrain(n) || grid.IsBlockedByEdifice(n)) return false;
                if (NaturalContent.IsWater(grid.Terrain[n])) return false;
                return grid.HasFloor(n);
            }
        }

        /// <summary>Rock or ore that can be dug out — what a scenario means by "an outcrop".</summary>
        public bool IsMinableStone(int index)
        {
            ushort terrain = _grid.Terrain[index];
            bool stone = terrain == NaturalContent.TerrainRock || NaturalContent.IsOre(terrain);
            return stone && CanMine(index);
        }

        /// <summary>
        /// Water of either depth. Shallow water is walkable, so a colonist may stand in it, and
        /// that is exactly why the question has to be asked separately from walkability: a cell
        /// you can wade through is still not one you can put a wall in.
        /// </summary>
        public bool IsWater(int index) => NaturalContent.IsWater(_grid.Terrain[index]);

        /// <summary>Whether a tree stands in the cell right now.</summary>
        public bool IsTree(int index) => TryEdificeDef(index, out ushort def) && NaturalContent.IsTree(def);

        /// <summary>
        /// Can this be taken apart? Only what the colony built itself.
        ///
        /// <para><b>Ownership, not shape.</b> The test used to be <c>def &lt; FirstEdifice</c> — a
        /// core wall rather than a natural one — which excludes trees and includes every wall the
        /// generator stamped into the ruined city. Those are Reclaim's and Salvage's, with their
        /// own yields and their own "claim it first" step (<c>a-04-building-and-materials.md</c>),
        /// and offering a colonist a free demolition of the city would quietly pre-empt that whole
        /// line. <see cref="PlacedEdifice.Built"/> is set by <c>ConstructionGrid.Raise</c> and
        /// nowhere else, so this asks the only question that actually matters: did we put it
        /// there?</para>
        ///
        /// <para>Trees need no separate exclusion — a tree is the generator's, so it is never
        /// <c>Built</c>, and felling remains the way one comes down.</para>
        /// </summary>
        public bool CanDeconstruct(int index) => TryTakeApart(index, out _, out _);

        /// <summary>
        /// What this cell holds that is ours to take apart, as the building and material handles
        /// that price the work and the refund, or false.
        ///
        /// <para><b>One resolver, two kinds of thing.</b> A wall is an edifice standing in the cell
        /// and a floor is a slab at its lower boundary, and the deconstruct line wants exactly the
        /// same two numbers from either — so the giver, the driver and the rule all ask here rather
        /// than each learning the difference.</para>
        ///
        /// <para><b>Only what we built</b>, in both cases and by the same argument.
        /// <see cref="PlacedEdifice.Built"/> says so for a wall; <c>CoreContent.SlabBuilt</c> says
        /// so for a floor, because the generator's three slab kinds are stamped and this fourth one
        /// is only ever written by <c>ConstructionGrid.Raise</c>. The ruined city stays Reclaim's
        /// and Salvage's, decks included.</para>
        /// </summary>
        public bool TryTakeApart(int index, out int building, out int stuff)
        {
            if (TryEdifice(index, out PlacedEdifice placed) && placed.Built)
            {
                building = ConstructionContent.BuildingForEdifice(placed.Def);
                stuff = ConstructionContent.StuffForValue(placed.Stuff);
                return building != BuildingHandle.None;
            }

            if ((uint)index < (uint)_grid.Size.CellCount && _grid.Floor[index] == CoreContent.SlabBuilt)
            {
                building = BuildingHandle.Floor;
                stuff = ConstructionContent.StuffForValue(_grid.FloorStuff[index]);
                return true;
            }

            building = BuildingHandle.None;
            stuff = StuffHandle.None;
            return false;
        }

        /// <summary>
        /// The building standing in this cell, or false. Public because deconstruct needs more than
        /// the def: it prices the work from what the thing is and the refund from what it is made
        /// of, and both live on the record rather than in the cell.
        /// </summary>
        public bool TryEdifice(int index, out PlacedEdifice placed)
        {
            int handle = _grid.Edifice[index];
            if (handle < 0 || handle >= _edifices.Count)
            {
                placed = default;
                return false;
            }

            placed = _edifices[handle];
            return !placed.Removed && placed.Def != CoreContent.EdificeNone;
        }

        bool TryEdificeDef(int index, out ushort def)
        {
            int handle = _grid.Edifice[index];
            if (handle < 0 || handle >= _edifices.Count)
            {
                def = 0;
                return false;
            }
            var placed = _edifices[handle];
            def = placed.Def;
            return !placed.Removed && def != CoreContent.EdificeNone;
        }

        void Set(int index, DesignationKind kind)
        {
            bool was = _kinds[index] != 0;
            _kinds[index] = (byte)kind;
            // A new order, a cancelled one and a carried-out one all start the next from nothing.
            _work[index] = 0;
            bool now = kind != DesignationKind.None;
            if (was == now) return;

            int at = _cells.BinarySearch(index);
            if (now) _cells.Insert(~at, index);
            else _cells.RemoveAt(at);
        }

        // ---- the intent seam -------------------------------------------------------------------

        /// <summary><c>Designate(cell, A = kind)</c>.</summary>
        public IntentRejection HandleDesignate(Intent intent)
        {
            if (intent.A <= 0 || intent.A > (int)DesignationKind.Fell) return IntentRejection.NotPermitted;
            return Designate(intent.Cell, (DesignationKind)intent.A);
        }

        /// <summary><c>CancelDesignation(cell)</c>.</summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell);

        /// <summary>Register everything this grid is: hashed state, saved state, a snapshot channel, two intents.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.Designate, HandleDesignate)
                .AddIntentHandler(IntentKind.CancelDesignation, HandleCancel);
        }

        // ---- ITickable: registration only, so the hash and the save see the orders -----------

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                hash.Add(_cells[i]);
                hash.Add(_kinds[_cells[i]]);
                hash.Add(_work[_cells[i]]);
            }
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "odyssey.designations";

        public void Save(SaveWriter writer)
        {
            writer.Write(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                writer.Write(_cells[i]);
                writer.Write(_kinds[_cells[i]]);
                writer.Write(_work[_cells[i]]);
            }
        }

        public void Load(SaveReader reader)
        {
            Array.Clear(_kinds, 0, _kinds.Length);
            _cells.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int index = reader.ReadInt();
                byte kind = reader.ReadByte();
                int work = reader.ReadInt();
                if (index < 0 || index >= _kinds.Length || kind == 0) continue;
                Set(index, (DesignationKind)kind);
                // After Set, which zeroes it: a half-cut face survives a save.
                _work[index] = work;
            }
        }

        // ---- ISnapshotContributor: one entry per standing order, anywhere in the world -------

        /// <summary>
        /// Publish every standing order the colony has, with how far through it is.
        ///
        /// <para><b>Walked over the orders, not over the board.</b> The first version asked
        /// <c>Fraction()</c> for all 14,400 cells of the active layer every tick, which cost
        /// 0.055 ms on a board with no orders on it at all — twenty-eight times the entire rest of
        /// the simulation, and it took the ten-day soak from 1 second a seed to 39. A layer has
        /// fourteen thousand cells and a colony has tens of orders, so sparse is the right shape by
        /// three orders of magnitude.</para>
        ///
        /// <para><b>And every layer, not the active one.</b> It used to copy the active layer's
        /// slice of <c>_kinds</c>, which was right while a click could not reach another layer.
        /// Since 2026-09-16 it can, so an order given on an outcrop standing over the meadow was
        /// accepted, worked and never drawn — the player's reading being that nothing happened.
        /// Publishing every order costs less than publishing one layer of mostly nothing did, and
        /// presentation filters to the layers it is drawing.</para>
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            // _cells is sorted, so orders arrive in cell-index order and presentation gets a
            // stable sequence rather than one that reshuffles as orders are given and carried out.
            for (int i = 0; i < _cells.Count; i++)
            {
                int index = _cells[i];
                byte kind = _kinds[index];
                if (kind == 0) continue;
                writer.AddOrder(new OrderView(index, kind, (byte)(Fraction(index) * 255f)));
            }
        }
    }
}
