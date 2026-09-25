#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
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

        /// <summary>
        /// The structural solver, when the world has one, asked one question only: what support a
        /// slab would have if one were built here. Null in a fixture with no structure, in which
        /// case a slab order is judged on everything except its support — which is the right answer
        /// for a test that never built a solver, and would be the wrong one in a game.
        /// </summary>
        readonly SupportSolver? _support;

        readonly EdificeSaveSection _edificeSave;
        readonly List<PlacedEdifice> _edifices;
        readonly ColonyItems _items;
        readonly PawnRegistry _pawns;
        readonly byte[] _building;
        readonly byte[] _stuff;
        readonly byte[] _facing;
        readonly int[] _delivered;

        /// <summary>
        /// Units of the building's <c>partItem</c> that have arrived — the second payment, banked
        /// apart from the material (design 32 §14). Zero for everything with no parts.
        /// </summary>
        readonly int[] _parts;
        readonly PartsSection _partsSection;
        readonly int[] _work;
        readonly List<int> _sites = new List<int>();

        /// <summary>
        /// Takes the edifice list as its <see cref="EdificeSaveSection"/> rather than raw, because
        /// this is the one class in the game that <b>appends</b> to that list at run time — and
        /// until 2026-09-17 nothing saved or hashed what it appended. Threading the section through
        /// here means the thing that raises a wall and the thing that writes it down cannot be
        /// wired up separately: <see cref="Edifices"/> hands it on to whoever assembles the save.
        /// </summary>
        public ConstructionGrid(CellGrid grid, EdificeSaveSection edifices, ColonyItems items,
            PawnRegistry pawns, SupportSolver? support = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edificeSave = edifices ?? throw new ArgumentNullException(nameof(edifices));
            _edifices = edifices.Records;
            _items = items ?? throw new ArgumentNullException(nameof(items));
            // For validating that a bed's owner names a colonist who exists. Required rather than
            // optional for the same reason this constructor's own remarks give about the grid
            // being built in AddColony: an optional pawn list is a build that silently accepts
            // any id whatever, which is a corrupt record waiting for a save.
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
            _support = support;
            _building = new byte[grid.Size.CellCount];
            _stuff = new byte[grid.Size.CellCount];
            _facing = new byte[grid.Size.CellCount];
            _delivered = new int[grid.Size.CellCount];
            _parts = new int[grid.Size.CellCount];
            _partsSection = new PartsSection(this);
            _work = new int[grid.Size.CellCount];
        }

        /// <summary>
        /// The navigation graph, when the colony has one, told two things and asked nothing: that
        /// a cell now holds an ordered building (<c>NavFlags.BuildSite</c>, which makes a colonist
        /// with any other route take it), and that the cell changed when the thing goes up.
        ///
        /// <para>Set by <c>ColonyComposition.AddColony</c> and null in a fixture with no
        /// navigation, in which case sites simply carry no detour — which is the right answer for
        /// a test that never built a graph. It is a property rather than a constructor argument
        /// because the graph is built before the grid and the grid is built inside the
        /// composition; a fourth constructor argument would be a fourth thing to forget.</para>
        /// </summary>
        public Pathing.NavGraph? Nav { get; set; }

        /// <summary>
        /// The power grid (design 32), which owns lines. A line is ordered with the same intent,
        /// cursor and palette row as a wall, so this grid is where the order arrives — and hands it
        /// on, because a line is not a site of this grid and not an edifice. Asked too for where a
        /// line may go, so <see cref="Allows(int, int)"/> and <see cref="WhereItWouldLand"/> give
        /// the cursor the power grid's own answer. Set by the composition like <see cref="Nav"/>;
        /// null in a fixture with no power, where a line order is refused.
        /// </summary>
        public Power.PowerGrid? Power { get; set; }

        /// <summary>
        /// The hearth (design 43 §3f), told when a campfire is raised or anything is demolished.
        /// Null in a bare fixture with no colony.
        /// </summary>
        public World.Hearth? Hearth { get; set; }

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

        /// <summary>Milliwork applied (thousandths of a tick; see <see cref="Rates"/>). Banked on
        /// the cell, for the reason mining banks its own.</summary>
        public int WorkDone(int index) => _work[index];

        /// <summary>Units of material this site is still waiting for. 0 once it is a frame.</summary>
        public int Outstanding(int index)
        {
            if (_building[index] == 0) return 0;
            int wanted = ConstructionContent.BuildingAt(_building[index]).costCount - _delivered[index];
            return wanted < 0 ? 0 : wanted;
        }

        /// <summary>Units of the site's part item that have arrived (design 32 §14).</summary>
        public int PartsDelivered(int index) => _parts[index];

        /// <summary>Units of the site's part item still wanted; 0 for a site with no parts, or once they are in.</summary>
        public int OutstandingParts(int index)
        {
            if (_building[index] == 0) return 0;
            BuildingDef def = ConstructionContent.BuildingAt(_building[index]);
            if (!def.HasParts) return 0;
            int wanted = def.partCount - _parts[index];
            return wanted < 0 ? 0 : wanted;
        }

        /// <summary>
        /// Has everything arrived — the material <b>and</b> the parts — so the thing can be worked
        /// on? A generator with its thirty wood in and its scrap metal still out is a blueprint.
        /// </summary>
        public bool IsFrame(int index) =>
            _building[index] != 0 && Outstanding(index) == 0 && OutstandingParts(index) == 0;

        /// <summary>The site's second payment arrived. Returns what the site now holds of it.</summary>
        public int DeliverParts(int index, int count)
        {
            _parts[index] += count;
            return _parts[index];
        }

        /// <summary>
        /// The save section that carries the parts delivered to sites (design 32 §14). A section of
        /// its own rather than a field in the site record, so the site layout — and the save format
        /// — is untouched; whoever assembles the save takes it from here, as it does the edifices.
        /// </summary>
        public ISaveable Parts => _partsSection;

        /// <summary>What this site costs in ticks of work, or 0 where there is no site.</summary>
        public int WorkFor(int index) =>
            _building[index] == 0 ? 0 : ConstructionContent.WorkFor(_building[index], _stuff[index]);

        /// <summary>How far through its work a site is, 0 to 1. See <c>DesignationGrid.Fraction</c>.</summary>
        public float Fraction(int index)
        {
            int total = WorkFor(index);
            if (total <= 0) return 0f;
            // Milliwork ledger against a tick price; the scale stops at this contract.
            float done = (float)_work[index] / (total * Rates.Scale);
            return done < 0f ? 0f : done > 1f ? 1f : done;
        }

        // ---- placing and cancelling --------------------------------------------------------

        /// <summary>
        /// Put a site on a cell, or say why not. Out of the map is
        /// <see cref="IntentRejection.OutOfBounds"/>; a cell that cannot take it, or a thing or
        /// material that does not exist, is <see cref="IntentRejection.NotPermitted"/>; the same
        /// site again is <see cref="IntentRejection.AlreadyInThatState"/>.
        ///
        /// <para><c>facing</c> is the rotation a rotatable thing was given at the ghost; ignored,
        /// and expected zero, for everything that does not rotate. A multi-cell thing must have
        /// <b>all</b> of its cells and both are validated here — but it is lifted exactly as a
        /// wall is, and the far cell is derived after the lift, so a bed can never straddle two
        /// layers. <see cref="StandingOn"/> carries why the first cut's refusal was wrong.</para>
        /// </summary>
        public IntentRejection Place(CellRef cell, int building, int stuff, int facing = 0)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            if (!ConstructionContent.IsBuilding(building)) return IntentRejection.NotPermitted;
            BuildingDef def = ConstructionContent.BuildingAt(building);
            // A thing always made of one material is made of it whatever the order named (design
            // 50 §4): the sandbags' five stone. Before the buildable test, so an order that names
            // no material at all is still good for them.
            if (def.fixedStuff != StuffHandle.None) stuff = def.fixedStuff;
            if (!ConstructionContent.IsBuildable(stuff)) return IntentRejection.NotPermitted;

            // A line is not a site of this grid: it lives in the power grid's own layer, where a
            // cell can hold a line and a wall at once (design 32 §3). Handed on whole — the lift,
            // the rule and the refusal are the power grid's.
            if (def.conduit)
                return Power != null ? Power.PlaceLine(_grid.Index(cell)) : IntentRejection.NotPermitted;

            // A thing that does not rotate never carries one, even if the interface sent a stale
            // number: a site's facing is hashed, and two identical wall orders that arrived with
            // different leftovers would have to hash apart for no reason a player can see.
            if (!def.rotates) facing = 0;

            // A click names a surface and an order names a cell, and which surface depends on what
            // is armed. WhereItWouldLand is that one answer, public so the build cursor asks it
            // rather than working it out again.
            int index = WhereItWouldLand(_grid.Index(cell), building);

            if (def.footprint > 1)
            {
                int second = EdificeFootprint.SecondCell(index, def.edifice, facing, _grid.Size);
                if (second < 0 || !Allows(second)) return IntentRejection.NotPermitted;

                // A site standing on the far cell is an order the bed would eat: refuse rather
                // than refund somebody's half-delivered wall out from under them.
                if (_building[second] != 0) return IntentRejection.NotPermitted;
            }

            if (_building[index] == building && _stuff[index] == stuff)
                return IntentRejection.AlreadyInThatState;
            if (!Allows(index, building)) return IntentRejection.NotPermitted;

            // The mirror of the check above: a one-cell thing may not be ordered into the far
            // cell of a bed that is already asked for, or the two sites would overlap the moment
            // both finished.
            if (HeadClaimingSite(index) >= 0) return IntentRejection.NotPermitted;

            // A site that is replaced gives back whatever had been carried to it, exactly as a
            // cancelled one does: the player changing their mind about the material is not a
            // reason to lose five wood, and the alternative is a silent tax on experimenting.
            Refund(index);
            Set(index, building, stuff, facing);
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
        ///
        /// <para><b>Either half of a two-cell site names the site.</b> A bed is one order, not
        /// two, so clicking the far cell finds the head's site — the same one-record rule the
        /// built thing will carry, answered while it is still an order.</para>
        /// </summary>
        public int SiteAt(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return -1;

            int index = _grid.Index(cell);
            if (_building[index] == 0) index = StandingOn(index);
            if (_building[index] != 0) return index;

            return HeadClaimingSite(_grid.Index(cell));
        }

        /// <summary>
        /// The head of a multi-cell site whose far cell is this one, or -1. A four-neighbour look:
        /// only a cell directly beside this one can be the head of a two-cell thing that claims it.
        /// </summary>
        int HeadClaimingSite(int index)
        {
            GridSize size = _grid.Size;
            CellRef at = size.FromIndex(index);
            for (int f = 0; f < 4; f++)
            {
                int x = at.X + (f == 1 ? 1 : f == 3 ? -1 : 0);
                int z = at.Z + (f == 0 ? 1 : f == 2 ? -1 : 0);
                if (!size.Contains(x, z, at.Y)) continue;

                int head = size.Index(x, z, at.Y);
                if (_building[head] == 0) continue;
                ushort edifice = ConstructionContent.BuildingAt(_building[head]).edifice;
                if (EdificeFootprint.Claims(head, edifice, _facing[head], index, size)) return head;
            }
            return -1;
        }

        public IntentRejection Cancel(CellRef cell, bool lines = true)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            // Every order in the cell, the line orders included: a cancel drag is "undo what I
            // asked for here", and a cell can hold a wall order and a line order at once (design
            // 32 §3). Asked first and separately, so a cell with only a line order in it is a
            // cancel that did something rather than AlreadyInThatState. A building's own pane
            // asks for the building alone (`lines: false`), because its Cancel names one thing.
            bool line = lines && Power != null && Power.CancelAt(_grid.Index(cell));

            int index = SiteAt(cell);
            if (index < 0) return line ? IntentRejection.None : IntentRejection.AlreadyInThatState;

            Refund(index);
            Set(index, BuildingHandle.None, StuffHandle.None);
            return IntentRejection.None;
        }

        /// <summary>
        /// Which cell an order named here would actually land in, without placing anything.
        ///
        /// <para><b>Public so the cursor can ask.</b> A build ghost has to be drawn where the thing
        /// will end up, not where the pointer is, and there are three different lifts depending on
        /// what is armed — a wall onto the ground it was clicked on, a slab onto whatever fills the
        /// cell, paving into the air over the block. Working that out a second time in the renderer
        /// is precisely how the cursor and the order came to disagree twice already, so they ask the
        /// same method (`19-build-cursor.md` §6).</para>
        ///
        /// <para>Changes nothing and reserves nothing: it is the arithmetic <see cref="Place"/> does
        /// on its first line, lifted out so that two callers cannot drift.</para>
        /// </summary>
        /// <summary>
        /// <b>The one layer a whole dragged run lands on.</b>
        ///
        /// <para><see cref="WhereItWouldLand"/> answers for one cell, and that is right for one
        /// click and wrong for a drag: the lift is <i>conditional</i> — a slab is lifted over
        /// whatever fills its cell and left where it was over open air — so a single box can resolve
        /// to two different layers, cell by cell, with nothing in the preview to say so.</para>
        ///
        /// <para><b>The owner met it as a hole in a floor</b> (2026-09-18, with a screenshot): a
        /// floor dragged over a walled room built the <i>ring</i> on the storey above, because those
        /// cells sit over walls and lifted, and refused the <i>middle</i>, because those cells sit
        /// over open air and did not. What is left is a deck with a hole in it and the storey below
        /// showing through — read, reasonably, as "it built the floor on the layer below". Measured
        /// by probe: twelve cells <c>None</c> at L12 and four <c>NotPermitted</c> at L11, from one
        /// drag.</para>
        ///
        /// <para><b>The highest any cell reaches wins</b>, and the alternative was worse. Taking the
        /// <i>anchor</i>'s lift is more predictable in principle, but a player who starts the drag
        /// in the middle of the room anchors on open air, which lifts nowhere and would refuse the
        /// whole run rather than a quarter of it. The highest cell is the storey the player is
        /// plainly pointing at, and over flat ground every cell agrees anyway.</para>
        ///
        /// <para>The lift is idempotent, which is what makes this safe: the cells handed back are
        /// asked again by <see cref="Place"/>, and a cell already on the open layer holds neither
        /// terrain nor an edifice, so it lifts no further.</para>
        /// </summary>
        public int RunLayerFor(IReadOnlyList<CellRef> cells, int building)
        {
            int best = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!_grid.Contains(cells[i].X, cells[i].Z, cells[i].Y)) continue;
                int landed = WhereItWouldLand(_grid.Index(cells[i]), building);
                int y = _grid.Size.FromIndex(landed).Y;
                if (y > best) best = y;
            }

            return best == int.MinValue ? 0 : best;
        }

        public int WhereItWouldLand(int index, int building)
        {
            if ((uint)index >= (uint)_grid.Size.CellCount) return index;

            BuildingDef what = ConstructionContent.BuildingAt(building);

            // A line takes its own lift, asked with its own rule: a click on the ground under a
            // wall names the wall's cell for a line, where the wall's rule would refuse it.
            if (what.conduit) return Power != null ? Power.WhereItWouldLand(index) : index;

            // A covering takes the WALL's lift, not the slab's: a click on grass names the ground
            // block and paving goes in the air cell above it, which is exactly what StandingOn
            // already does. Only structure is lifted over things that fill a cell (U42).
            return what.slab && !what.covering
                ? StandingOver(index, building)
                : StandingOn(index);
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
        ///
        /// <para><b>A two-cell thing is lifted too, and the argument against it was wrong.</b> The
        /// bed was written to refuse the lift, on the reasoning that raising one end of a bed into
        /// the air while the other stayed put is an order whose shape the player cannot see. That
        /// case cannot arise: the far cell is derived from the head <i>after</i> the lift, so both
        /// ends are always on one layer, and a far cell with nothing under it fails
        /// <see cref="Allows"/> and refuses the whole order. What the rule did instead was make the
        /// bed unorderable — the picker answers a click on grass with the ground block, so every
        /// bed a player could point at was <c>NotPermitted</c> and the tool armed, dragged,
        /// previewed and did nothing. Measured at the picker-to-order seam
        /// (<c>FloorToolReachTests</c>), which is the only place the two halves meet.</para>
        /// </summary>
        int StandingOn(int index)
        {
            if (!_grid.IsSolidTerrain(index)) return index;
            int above = index + _grid.Size.LayerStride;
            return above < _grid.Size.CellCount && Allows(above) ? above : index;
        }

        /// <summary>
        /// A slab order named at something that fills a cell means the boundary on top of it.
        ///
        /// <para><see cref="StandingOn"/>'s twin, and it exists for the same reason: a click names
        /// a surface. The difference is <b>which</b> surfaces, and it is the whole of whether the
        /// floor tool works at all. A wall is put in the air over the ground <i>block</i>, so that
        /// lift asks about solid terrain and nothing else. A floor's surface is just as often a
        /// <b>wall</b> — the first slab of any storey rests on the walls of the one below — and the
        /// picker answers a click on a wall with the wall's own cell, because that is the cell
        /// whose face occludes the ray.</para>
        ///
        /// <para><b>Without this the tool was armable, draggable and inert</b> (measured
        /// 2026-09-17): ordering a floor at the cell a click actually produces was
        /// <c>NotPermitted</c> on a wall, on bare ground and on the air above bare ground, and the
        /// one cell that answered <c>None</c> — the cell above a wall — was reachable only by
        /// naming it in a test. That is the silent refusal `15-building.md` §6 was written about,
        /// and the unit's own tests could not see it because they name the site by hand.</para>
        ///
        /// <para>Ordinary ground is untouched by it. A click on grass lifts to the air above,
        /// <see cref="AllowsSlab"/> refuses that for having a floor already, and the refusal is
        /// reported at the cell the player clicked — exactly as it was before.</para>
        /// </summary>
        int StandingOver(int index, int building)
        {
            // Anything that fills the cell, which is the same set the solver calls grounding: a
            // slab laid over it has something underneath to rest on.
            if (!_grid.IsSolidTerrain(index) && _grid.Edifice[index] < 0) return index;

            int above = index + _grid.Size.LayerStride;
            return above < _grid.Size.CellCount && Allows(above, building) ? above : index;
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
        public bool Allows(int index) => Allows(index, BuildingHandle.Wall);

        /// <summary>
        /// Whether <em>this</em> thing may be built in this cell. An edifice and a slab want
        /// opposite answers to the same question about the floor, so the building has to be named.
        /// </summary>
        public bool Allows(int index, int building)
        {
            if ((uint)index >= (uint)_grid.Size.CellCount) return false;

            // A line answers to the power grid's rule alone — it may go where a wall stands, which
            // every line below would refuse (design 32 §3).
            if (ConstructionContent.BuildingAt(building).conduit)
                return Power != null && Power.AllowsLine(index);

            if (_grid.IsSolidTerrain(index)) return false;
            if (NaturalContent.IsWater(_grid.Terrain[index])) return false;
            if (_grid.Edifice[index] >= 0) return false;

            // **Furniture needs a clear cell** (owner, 2026-09-18: "you shouldn't be able to put a
            // bed where there is an object/item structure in the way … you can see some meals
            // poking through the bed, this is invalid").
            //
            // A property of the thing, not a rule about building, because the two really do
            // differ. A bed is broad and low and open, so a stack of meals stands up through the
            // mattress and is plainly wrong. A wall fills its cell and a slab is laid at the
            // boundary underneath it; neither shows what is in the cell, and refusing those would
            // stop a colonist walling a corner because somebody dropped a log there — which was
            // measured, not supposed: the blanket rule failed twelve tests that build perfectly
            // ordinary walls near a start the scenario strews with wood.
            if (ConstructionContent.BuildingAt(building).needsClearCell && _items.ItemAt(index) != null)
                return false;

            // Rubble, and nothing else today. The flag has existed on TerrainDef since the tables
            // were written and had no reader until a collapse started leaving a mess (U29): a heap
            // of debris is cleared before anything is built on it.
            if (!NaturalContent.TerrainAt(_grid.Terrain[index]).buildable) return false;

            BuildingDef def = ConstructionContent.BuildingAt(building);

            // The shaft rule, stated once, for both sides of it and for both orders it can be given
            // in. See ShaftRulePermits.
            if (!ShaftRulePermits(index, def, building)) return false;

            return def.covering ? AllowsCovering(index)
                : def.slab
                ? AllowsSlab(index)
                : building == BuildingHandle.Ladder
                ? StandsOnSomething(index) && AllowsLadder(index)
                // Something underfoot. A wall hanging in the air is the fault the whole support
                // model exists to prevent, and refusing it at the order is far better than
                // collapsing it afterwards: the player never gave an order that could not be
                // carried out.
                : SomethingUnderfoot(index);
        }

        /// <summary>
        /// Is there something for an edifice to stand on at the bottom of this cell?
        ///
        /// <para><b>A wall may stand on a wall</b>, and until 2026-09-17 it could not. This asked
        /// <c>HasFloor</c>, which counts a slab at the boundary or solid terrain below and knows
        /// nothing about what anyone has built — so a second storey went up over the room and
        /// refused over its own walls. The owner hit exactly that: *"on the next floor - I couldn't
        /// build a wall on top of the wall below, but could on other tiles."* The other tiles had
        /// the new slab under them.</para>
        ///
        /// <para><b>The support model always agreed with the player, not with the rule.</b>
        /// <c>SupportSolver.IsGrounded</c> has counted the cell above a blocking edifice as fully
        /// grounded since M1, so the wall would have stood; the order was refused for a reason the
        /// physics did not share. This is the permission catching up, not a new allowance.</para>
        ///
        /// <para><b>Blocking, rather than any edifice.</b> The solver's own test is wider — it
        /// takes any edifice at all, which is how a tree came to hold up a roof — and a tree is
        /// something you fell before you build, exactly as <see cref="Allows(int, int)"/> already
        /// says about building through a wood. A ladder is excluded by the same word and that is
        /// wanted too: capping a ladder with a wall is not a thing to permit by accident.</para>
        /// </summary>
        /// <summary>
        /// <b>A ladder climbs an open shaft, and this is the whole rule that keeps it open</b> —
        /// stated once, for both sides of it, for both orders it can be given in, and asked again
        /// at the moment the thing is actually built.
        ///
        /// <para>A ladder under a slab is a ladder a colonist climbs <i>through the floor</i>
        /// (owner, 2026-09-18). So a ladder is refused under a floor, and a floor is refused over a
        /// ladder — two sides of one rule, because a rule that can be walked around in two moves is
        /// not a rule: refusing the ladder alone would leave a player free to build the ladder
        /// first and pour the floor over it afterwards.</para>
        ///
        /// <para><b>And a blueprint counts, which is what the first cut got wrong.</b> The rule
        /// asked <c>_grid.Edifice</c> and <c>_grid.Floor</c> — the <i>built</i> world — so an order
        /// that was still a site was invisible to it. Two individually legal orders therefore
        /// combined into the arrangement the rule exists to forbid: order the ladder, order the
        /// floor above it, and both are permitted because neither exists yet. The owner reported it
        /// as *"sometimes the colonists climb up the ladder where there is wall or slab directly
        /// above"*, and "sometimes" is what a race between two blueprints sounds like — it depended
        /// on which job a colonist happened to pick up. Reproduced by probe, 2026-09-18, on the
        /// played meadow, in both orders.</para>
        ///
        /// <para><b>The top of the shaft is protected too.</b> A floor laid over the open cell a
        /// ladder arrives in is not capping the ladder, but it caps the shaft — and it used to be
        /// permitted, silently closing a working way up with nothing said (owner's answer: refuse
        /// it, like the slab rule). That is the <c>BelowOf(BelowOf(...))</c> reach: a slab is
        /// refused with a ladder one cell under it or two.</para>
        ///
        /// <para><b>A landing is deliberately not asked for here</b>, although it is what makes the
        /// ladder <i>work</i>. The top of a chain of ladders is the only one that needs one, and a
        /// player builds a chain from the bottom: demanding a landing at the order would refuse
        /// every ladder in a shaft except the last, in the only order they can be built in. It is
        /// asked at the connector instead, where it can be answered again each time either end
        /// changes — which <see cref="RefreshLadder"/> already does in both directions.</para>
        /// </summary>
        bool ShaftRulePermits(int index, BuildingDef def, int building)
        {
            if (def.slab || def.covering)
                return !LadderHereOrOrdered(BelowOf(index))
                    && !LadderHereOrOrdered(BelowOf(BelowOf(index)));

            return building != BuildingHandle.Ladder || AllowsLadder(index);
        }

        /// <summary>Whether a ladder may go up from this cell: the cell above has to be open.</summary>
        bool AllowsLadder(int index)
        {
            int above = index + _grid.Size.LayerStride;
            if (above >= _grid.Size.CellCount) return false;

            return !FloorHereOrOrdered(above);
        }

        /// <summary>
        /// A ladder standing in this cell, <b>or ordered into it</b>. The built half is
        /// <see cref="IsLadder"/>; the ordered half is why the shaft rule survives a player who
        /// gives two orders before either is carried out.
        /// </summary>
        bool LadderHereOrOrdered(int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return false;
            return IsLadder(cell) || _building[cell] == BuildingHandle.Ladder;
        }

        /// <summary>
        /// Something a colonist would walk out on to at this cell's lower boundary — solid ground,
        /// a laid slab, or a slab somebody has ordered there and not yet built.
        /// </summary>
        bool FloorHereOrOrdered(int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return false;
            if (_grid.IsSolidTerrain(cell)) return true;
            if (_grid.Floor[cell] != CoreContent.SlabNone) return true;

            int ordered = _building[cell];
            if (ordered == BuildingHandle.None) return false;

            BuildingDef def = ConstructionContent.BuildingAt(ordered);
            return def.slab || def.covering;
        }

        /// <summary>The cell one layer down, or -1 at the bottom of the world.</summary>
        int BelowOf(int index)
        {
            int below = index - _grid.Size.LayerStride;
            return below < 0 ? -1 : below;
        }

        bool SomethingUnderfoot(int index)
        {
            if (_grid.HasFloor(index)) return true;
            int below = index - _grid.Size.LayerStride;
            return below >= 0 && (_grid.Flags[below] & CellFlags.BlockingEdifice) != 0;
        }

        /// <summary>
        /// What a <b>ladder</b> may stand on: anything an edifice may stand on, and also the top of
        /// another ladder.
        ///
        /// <para><b>Without the second half a shaft can only ever be one storey.</b> A ladder is
        /// <c>blocking false</c> on purpose — a ladder you cannot stand in is a decoration — and
        /// <see cref="SomethingUnderfoot"/> wants a floor or a <i>blocking</i> edifice, so the
        /// second ladder of a chain was refused and <see cref="LadderArrivesAt"/>'s "another ladder
        /// in it" clause was unreachable for anything a player built. Measured by probe, not read.
        /// </para>
        ///
        /// <para>It is deliberately not folded into <see cref="SomethingUnderfoot"/>, which every
        /// other edifice asks: standing a <i>wall</i> on a ladder would cap the shaft with something
        /// the rule above cannot see, and a bed on a ladder is not a thing to permit by accident.
        /// </para>
        /// </summary>
        bool StandsOnSomething(int index) =>
            SomethingUnderfoot(index) || LadderHereOrOrdered(BelowOf(index));

        /// <summary>
        /// Whether a ladder standing here has anything to start from — the <b>connector's</b> half
        /// of <see cref="StandsOnSomething"/>, and deliberately a different question.
        ///
        /// <para>Two differences, both deliberate. It asks <c>CellGrid.IsWalkable</c> rather than
        /// <c>SomethingUnderfoot</c>, because a connector wants a cell a colonist can actually stand
        /// in and not merely a surface an edifice could rest on. And it counts only a <b>built</b>
        /// ladder below, never an ordered one: a blueprint may let you place the next ladder of a
        /// chain, but it must never open a way up that nobody has built yet.</para>
        ///
        /// <para>The <c>IsWalkable</c> half alone is what kept a chain from ever working even after
        /// the placement rule allowed one: the foot of an upper ladder is the open shaft cell below
        /// it, which is standable only through that lower ladder's own connector — and
        /// <c>CellGrid</c> cannot see connectors, that being <c>NavGrid.RefreshFrom</c>'s rule that
        /// a connector is its own floor. Measured, not read: the chain built, and the upper ladder
        /// silently had no connector.</para>
        /// </summary>
        bool StandsOnAFooting(int cell) => _grid.IsWalkable(cell) || IsLadder(BelowOf(cell));

        /// <summary>
        /// Whether a slab may be built at this cell's lower boundary.
        ///
        /// <para>The mirror of the edifice rule: a wall wants something under it and a floor is the
        /// something. So the cell must have <b>no floor already</b> — neither a slab nor solid
        /// ground beneath it — because a floor over a floor is an order with nothing to do.</para>
        ///
        /// <para><b>And the support rule must permit it</b>, which is the line that makes the whole
        /// mechanic playable rather than punitive. Without it a colony can order a slab anywhere,
        /// carry four wood across the map, build it, and watch it fall on the tick it is finished.
        /// With it a colonist bridges out from a wall as far as support reaches and the next order
        /// is refused with a reason — the overhang is not a number anybody tuned, it is the support
        /// rule seen from the side.</para>
        /// </summary>
        bool AllowsSlab(int index)
        {
            if (_grid.Floor[index] != CoreContent.SlabNone) return false;
            if (_grid.HasFloor(index)) return false;
            if (_support == null) return true;
            return _support.SupportIfSlabAt(index) > 0 || SupportedByWhatIsPlanned(index);
        }

        /// <summary>
        /// <b>Would the slab ordered here stand up if it were finished now?</b> Asked of the
        /// <em>built</em> world only — no plans, no blueprints.
        ///
        /// <para><see cref="AllowsSlab"/>'s deliberate other half. That one accepts a cell held up
        /// by slabs merely <em>ordered</em> around it, which is the whole of why a roof can be
        /// dragged in one gesture. It makes a promise about the finished roof and says nothing
        /// about the order the cells go up in — and nothing used to enforce that order, so a
        /// colonist taking the nearest site could raise the far end of a bridge while the cells
        /// meant to hold it up were still blueprints. It stood on nothing and fell on the tick it
        /// was finished.</para>
        ///
        /// <para><b>The owner reported precisely this first</b> (2026-09-18): <i>"the colonists
        /// tried to build the most outer slabs first which then landed a stone/steel looking tile 1
        /// height below instead of where it was"</i>. The tile below was the rubble the collapse
        /// left, and three sessions went on the rubble's drawing before the collapse was measured.
        /// The owner's rule: <b>a slab that cannot stand is not built yet, and it never leaves
        /// rubble.</b> Rubble is for construction that was destroyed, not for construction that
        /// never happened.</para>
        ///
        /// <para>True for anything that is not a slab, so a caller can ask it of any site without
        /// first working out what kind it is. <see cref="BuildWorkGiver.CanBuild"/> asks it before
        /// offering a job, which defers the cell rather than refusing it; <see cref="Raise"/> asks
        /// it again at the moment of truth, for the same reason the shaft rule is asked twice.</para>
        /// </summary>
        public bool SlabWouldStand(int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return false;

            int building = _building[cell];
            if (building == BuildingHandle.None) return true;
            if (!ConstructionContent.BuildingAt(building).slab) return true;

            // A covering is laid on ground that is already there and is held up by whatever holds
            // that ground up, so it can never be the unsupported case (18-paving.md).
            if (ConstructionContent.BuildingAt(building).covering) return true;

            return _support == null || _support.SupportIfSlabAt(cell) > 0;
        }

        /// <summary>
        /// Would this slab stand once the slabs already <b>ordered</b> around it are built?
        ///
        /// <para><b>Without this you cannot roof a room in one gesture, and that is what the owner
        /// hit</b> (2026-09-17): *"I wasn't able to create a slab across a house — it requires
        /// blocks underneath."* Support crosses a slab and does not cross a hole, so before any of
        /// a roof exists the only cells that answer yes are the ones touching a wall. Measured on a
        /// 6 × 5 house: 28 of 30 cells took the order and the two in the middle refused, and on a
        /// larger house the refusing middle is most of the roof. The player had to order a ring,
        /// wait for colonists to build it, order the next ring, and wait again.</para>
        ///
        /// <para><b>The guarantee is kept rather than traded away.</b> The point of checking support
        /// at order time is that a colony never carries material across the map to build something
        /// that falls on the tick it is finished, and a bare "is a neighbour planned?" would throw
        /// that away — twenty cells dragged off a wall would all be accepted and sixteen of them
        /// would collapse. So this walks outward over slabs that exist <em>or are ordered</em>,
        /// spending one point of support per step exactly as the solver does, and answers yes only
        /// if a real source is reachable within budget. A bridge still stops at
        /// <c>S_max</c>; a roof orders in one drag.</para>
        ///
        /// <para>Bounded by <c>MaxSupport</c> steps on one layer, so it visits a few dozen cells at
        /// worst and cannot walk the board.</para>
        /// </summary>
        bool SupportedByWhatIsPlanned(int index)
        {
            int budget = _support!.MaxSupport;
            if (budget <= 1) return false;

            GridSize size = _grid.Size;
            int layerBase = index / size.LayerStride * size.LayerStride;

            _plannedFrontier.Clear();
            _plannedSeen.Clear();
            _plannedFrontier.Add(index);
            _plannedSeen.Add(index);

            // One ring at a time, so the first source found is the nearest and the budget spent is
            // the distance travelled.
            for (int step = 1; step < budget; step++)
            {
                int count = _plannedFrontier.Count;
                for (int f = 0; f < count; f++)
                {
                    CellRef at = size.FromIndex(_plannedFrontier[f]);
                    for (int d = 0; d < 4; d++)
                    {
                        int x = at.X + (d == 0 ? -1 : d == 1 ? 1 : 0);
                        int z = at.Z + (d == 2 ? -1 : d == 3 ? 1 : 0);
                        if (!size.Contains(x, z, at.Y)) continue;

                        int next = size.Index(x, z, at.Y);
                        if (next < layerBase || next >= layerBase + size.LayerStride) continue;
                        if (!_plannedSeen.Add(next)) continue;

                        // A real source: something already holding this boundary up. Reached in
                        // `step` moves, so it can spare `support - step` points.
                        if (_support.SupportIfSlabAt(next) - step > 0) return true;

                        // Otherwise it is only worth walking through if a slab will be there.
                        if (_building[next] != BuildingHandle.None
                            && ConstructionContent.BuildingAt(_building[next]).slab)
                            _plannedFrontier.Add(next);
                    }
                }

                _plannedFrontier.RemoveRange(0, count);
                if (_plannedFrontier.Count == 0) return false;
            }

            return false;
        }

        readonly List<int> _plannedFrontier = new List<int>();
        readonly HashSet<int> _plannedSeen = new HashSet<int>();

        /// <summary>
        /// Whether a <b>covering</b> may be laid here: paving, on ground that is already there.
        ///
        /// <para><b><see cref="AllowsSlab"/> turned inside out, and it is two lines because that is
        /// genuinely the whole difference.</b> A structural slab wants a cell with nothing beneath
        /// it and has to satisfy the support rule; a covering wants a cell that is <em>already</em>
        /// floored — that floor is what it is laid on — and asks the support rule nothing at all,
        /// because whatever holds the ground up is holding the covering up too. It can never
        /// collapse, so there is no rule here saying it cannot.</para>
        ///
        /// <para><b>Nothing laid yet</b>, which is the one question the two share: a covering over a
        /// covering, or over a floor we built, is an order with nothing to do. The kind is not
        /// examined — a slab is a slab for this purpose — so paving a built floor is refused for
        /// the same reason paving paving is.</para>
        ///
        /// <para>This is what the owner was reaching for when they reported that "nothing happens"
        /// (U42, 2026-09-17). Ordinary ground answers yes here and no to
        /// <see cref="AllowsSlab"/>, which is the whole of why the floor tool looked broken.</para>
        /// </summary>
        bool AllowsCovering(int index)
        {
            if (_grid.Floor[index] != CoreContent.SlabNone) return false;
            return _grid.HasFloor(index);
        }

        /// <summary>Material has arrived. Returns what the site now holds.</summary>
        public int Deliver(int index, int count)
        {
            _delivered[index] += count;
            return _delivered[index];
        }

        /// <summary>Add a payment of milliwork and return the new total. Banked on the cell, never on the job.</summary>
        public int AddWork(int index, int milliwork)
        {
            _work[index] += milliwork;
            return _work[index];
        }

        /// <summary>Take the site off without refunding: the thing has been built.</summary>
        public void Clear(int index)
        {
            if (_building[index] != 0) Set(index, BuildingHandle.None, StuffHandle.None);
        }

        /// <summary>
        /// A completed build that rolled and failed. The banked work is thrown away and the
        /// delivery is cut to what the caller says it keeps — half, in practice, the odd unit by a
        /// seeded flip — and <b>the site stands</b>, a blueprint again.
        ///
        /// <para><b>Nothing in the world changed, and that is why nothing here marks anything
        /// dirty.</b> No wall appeared, so no chunk, no walkability and no support moved; the only
        /// state that changed is the two numbers on the site, which the hash and the save already
        /// carry. The two givers re-ask their questions of the row on the next scan — the
        /// deliverer, because material is outstanding again, and then the builder — so a botch
        /// costs the colony the work and the material, and never the order.</para>
        /// </summary>
        public void Botch(int index, int keepDelivered)
        {
            if (_building[index] == 0) return;
            _work[index] = 0;
            _delivered[index] = keepDelivered;
            // The parts go the same way, half kept, rounded up: the part is a handful of pieces
            // and a botch that took the last one of an odd count would be a harsher rule than the
            // material's own coin flip for no reason a player could see (design 32 §14).
            _parts[index] = (_parts[index] + 1) / 2;
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
            // The parts first, and whatever was delivered of them, in full: a cancelled order gives
            // back everything carried to it, material and parts alike (design 32 §14).
            if (_parts[index] > 0 && _building[index] != 0)
                GiveBack(index, ConstructionContent.BuildingAt(_building[index]).partItem, _parts[index]);

            int delivered = _delivered[index];
            if (delivered <= 0) return;
            GiveBack(index, ConstructionContent.StuffAt(_stuff[index]).item, delivered);
        }

        void GiveBack(int index, int item, int count)
        {
            if (item < 0 || count <= 0) return;

            int at = _items.NearestCellWithSpace(
                _grid, index, item, count, JobDriver.DropSearchRadius);

            // A board with no room within that radius is packed solid with things, which nothing in
            // the game can produce. Losing the load is the least bad answer; the alternative is
            // refusing to let the player cancel an order, which is worse.
            if (at >= 0) _items.Spawn(item, at, count);
        }

        void Set(int index, int building, int stuff, int facing = 0)
        {
            // **A site holds its cells against items from the moment it is ordered**, not from the
            // moment it stands. Ordering already refuses a cell that holds something (Allows), but
            // a bed takes a colonist a while to build, and in between a hauler was free to put a
            // log down on the cell and the bed was raised straight over it — which is exactly what
            // the owner photographed after the first fix (2026-09-18: "the bed was built and there
            // was a log going through it"). Refusing the order and blocking the built thing left
            // the whole of the build in between.
            //
            // Released first, from what is there now, because a cancelled or replaced site has to
            // give its cells back — and the facing that says which second cell to give back is
            // about to be overwritten.
            ReleaseItemHold(index);

            bool was = _building[index] != 0;
            _building[index] = (byte)building;
            _stuff[index] = (byte)stuff;
            // The facing a rotatable site was placed at, kept until Raise copies it onto the
            // record. Zeroed with the rest when the site goes, so a stale facing can never
            // outlive its site and claim a neighbour for nothing.
            _facing[index] = building != BuildingHandle.None ? (byte)(facing & 3) : (byte)0;
            // A new site, a cancelled one and a finished one all start the next from nothing.
            _delivered[index] = 0;
            _parts[index] = 0;
            _work[index] = 0;
            if (building != BuildingHandle.None)
            {
                BuildingDef def = ConstructionContent.BuildingAt(building);
                if (def.needsClearCell)
                {
                    _items.BlockItemsAt(index);
                    int second = EdificeFootprint.SecondCell(
                        index, def.edifice, _facing[index], _grid.Size);
                    if (second >= 0) _items.BlockItemsAt(second);
                }
            }

            bool now = building != BuildingHandle.None;

            // The detour. A site that will block the cell is dearer to walk into than the ground
            // beside it, so a colonist crossing the room goes round the wall somebody is putting
            // up rather than through it — which is what makes the eviction in `Raise` a rarity
            // rather than a thing the player watches. Asked of every write, including a site
            // replaced by one of a different kind, and cleared when the site goes for any reason:
            // cancelled, refunded, or raised into a real wall that carries its own flags.
            Nav?.SetBuildSite(index,
                now && ConstructionContent.BuildingAt(building).blocking);

            if (was == now) return;

            int at = _sites.BinarySearch(index);
            if (now) _sites.Insert(~at, index);
            else _sites.RemoveAt(at);
            // A site is part of home (design 43 §3a), and so is whatever it is raised into.
            _grid.Footprint.Touch(index);
        }

        /// <summary>
        /// Give back the cells a site standing here was holding against items, if it was holding
        /// any. Read from the site as it is now, so it must run before the site is overwritten.
        /// </summary>
        void ReleaseItemHold(int index)
        {
            int standing = _building[index];
            if (standing == BuildingHandle.None) return;

            BuildingDef def = ConstructionContent.BuildingAt(standing);
            if (!def.needsClearCell) return;

            _items.AllowItemsAt(index);
            int second = EdificeFootprint.SecondCell(index, def.edifice, _facing[index], _grid.Size);
            if (second >= 0) _items.AllowItemsAt(second);
        }

        // ---- the world edit ------------------------------------------------------------------

        /// <summary>
        /// Make the thing stand. Called from the deferred structural phase, never inline, for the
        /// reason <c>MineJobDriver.MineCell</c> is: a scan walking the world must not see it move.
        ///
        /// <para>Everything that changes is listed here in one place rather than discovered one bug
        /// at a time, which is the shape <c>MineCell</c> settled on.</para>
        ///
        /// <para><b>Support is marked dirty since U29.</b> It deliberately was not, while nothing
        /// collapsed: marking it would have started the solver running over edits whose
        /// consequences were not written. Mining and demolition made the same omission and carried
        /// the same warning — that the three should be wired together rather than one of them
        /// quietly acquiring behaviour the others lack — and all three are wired here.</para>
        ///
        /// <para><c>quality</c> is the tier the finishing colonist rolled, for the one def that
        /// takes one; zero, and written as zero, for everything else.</para>
        /// </summary>
        /// <summary>
        /// Returns false only when the raise was **refused because somebody is in the way** and is
        /// worth trying again — see <see cref="RaiseWhenClear"/>, which is what the build driver
        /// actually calls. Every other outcome, including a site that refunded itself and died, is
        /// true: the question this answers is "should anybody ask again", not "did a wall appear".
        /// </summary>
        public bool Raise(PawnContext ctx, int cell, byte quality = 0)
        {
            int building = _building[cell];
            if (building == BuildingHandle.None) return true;

            BuildingDef def = ConstructionContent.BuildingAt(building);
            ushort stuff = ConstructionContent.StuffAt(_stuff[cell]).stuff;

            // **Read before the site is cleared, because clearing it takes the facing with it.**
            //
            // This line used to be two: the far cell was derived here and the facing was read again
            // *after* `Clear(cell)` on its way to the record — out of a slot that had just been
            // zeroed. So every rotatable thing was built facing north whatever the player chose,
            // and the fault hid perfectly. The cells were right, because they were derived up here
            // from the real facing; only the drawn thing was wrong, so no simulation test could see
            // it, the footprint guard still refused a bed whose far half was in a wall, and
            // `ABedsFacingIsInTheStateHash` passed on the difference between the two beds' *cells*
            // rather than on the facings it was written to pin.
            //
            // What the owner saw: a bed ghost turned the way they wanted, and a built bed at a
            // quarter turn to it, sometimes lying through a wall it was never allowed to occupy
            // (2026-09-18, with a screenshot of each). And with it the sleeper, who is laid out
            // from the bed's facing and was lying across a bed that had been placed along.
            byte facing = _facing[cell];
            int second = EdificeFootprint.SecondCell(cell, def.edifice, facing, _grid.Size);

            // A two-cell thing's cells were both validated at the order; the world can still have
            // moved under the far one while the wood was being fetched — the ground below it can
            // have been mined out. The order dies and the material goes back rather than half a
            // bed standing on air, which is the same answer a collapse will give when it exists.
            if (second >= 0 && !Allows(second))
            {
                Refund(cell);
                Clear(cell);
                return true;
            }

            // **The shaft rule, asked again at the moment of truth**, and it is the only rule that
            // is. Everything else <see cref="Place"/> checks is checked once because a site holds
            // its own cell against anything else being ordered there — but the shaft rule spans two
            // cells that are ordered separately, so the *other* order can arrive after this one and
            // be perfectly legal when it does. Asking once left a ladder and the floor above it both
            // permitted, both built, and a colonist climbing through the deck: the owner's third
            // report, reproduced by probe in both orders.
            //
            // Deliberately this rule alone rather than the whole of `Allows`: a site with material
            // hauled to it fails `needsClearCell`, so re-asking everything would refuse every bed
            // whose own wood had been delivered.
            if (!ShaftRulePermits(cell, def, building))
            {
                Refund(cell);
                Clear(cell);
                return true;
            }

            // **And the support rule, for the same reason and with the opposite answer.**
            //
            // A slab accepted on the strength of its planned neighbours must not go up before they
            // do. `BuildWorkGiver.CanBuild` already declines to offer such a site, so reaching here
            // means support was lost between the job starting and the last blow landing — a
            // neighbour cancelled, or mined out from under it.
            //
            // **The site is kept, not cancelled**, which is the one place this differs from the
            // shaft rule above. A shaft violation is permanent: the floor above it exists and the
            // ladder can never be legal there. This is a race, and the cell is very likely to
            // become legal again the moment somebody builds the neighbour that was always planned.
            // Cancelling would take the far half of a dragged roof away from the player, which is
            // exactly what `SupportedByWhatIsPlanned` exists to prevent.
            //
            // The work already done stays done, so the retry costs nothing, and nothing falls —
            // which is the owner's rule (2026-09-18): a slab that cannot stand is simply not built
            // yet, and it never leaves rubble.
            if (!SlabWouldStand(cell)) return true;

            // **And nobody is built into it.** A site is walkable up to this instant, so a
            // colonist can perfectly well be standing where the wall is about to be, and until
            // 2026-09-21 the wall simply went up around them: the cell stopped being walkable,
            // no path could start in it or end in it, and the colonist was sealed in for the life
            // of the building (owner's report, and `EntombmentTests` is its reproduction).
            //
            // Two answers, because the two cases are different. Somebody **walking through** is
            // gone in a second, so the raise is refused and the last blow lands again later —
            // the work stays banked and the material is untouched, so waiting costs nothing.
            // Somebody **standing** there will still be there in an hour, so they are moved
            // aside; that is the deadlock this must not have, and one cell of shove is cheaper
            // than an order the colony can never finish. `CanRaiseNow` is the same question
            // asked without the shove, which is what lets the builder hold the last blow rather
            // than roll a botch for a wall it is going to build anyway.
            if (!MakeRoom(ctx, cell, second)) return false;

            Clear(cell);

            // 1. The thing itself — a slab at the cell's lower boundary, or an edifice standing in
            //    the cell. One `if`, because everything else about the two is identical.
            if (def.slab) RaiseSlab(cell, stuff, def.covering);
            else RaiseEdifice(cell, def, stuff, second, facing, quality);

            if (def.edifice == CoreContent.EdificeDoor) ctx.Nav.SetDoor(cell, isDoor: true, open: false);
            if (def.edifice == CoreContent.EdificeBed) _items.AddBed(cell);
            // Cover that is crossed but never stood on (design 50 §5): the crossing's price.
            if (def.passThrough) ctx.Nav.SetPassThrough(cell, def.crossCost);

            // 1b. A thing that makes or spends power has a switch and a hopper the world does not,
            //     and joins whatever net a line beside it is on (design 32 §5).
            if (def.IsPowered) ctx.Power?.AddDevice(_grid.Edifice[cell]);

            // 1a. A store that was built rather than painted. The cell leaves whatever zone held
            //     it *first*, so nothing can ever observe a cell that is in two stores at once: a
            //     shelf carries its own filter and its own rung, and a cell with two answers to
            //     "what goes here" is the fault the zones' own anchor rule exists to prevent. The
            //     other direction is already closed — StorageZones.SiteAllows refuses to paint over
            //     an edifice — so this is the half that was missing.
            if (def.storageSlots > 0)
            {
                ctx.Storage?.LeaveCell(cell);
                ctx.StorageUnits?.Raise(_grid.Edifice[cell], def.storageSlots);
            }

            // 2. The cells and everything touching them must be re-meshed: a thing changes how its
            // neighbours draw their own faces, and the vertical neighbours are in other chunks.
            MarkChunksAround(ctx, cell);
            if (second >= 0) MarkChunksAround(ctx, second);

            // 3. What is walkable changed here, and in the cell above through the floor rule.
            MarkNavAround(ctx, cell);
            if (second >= 0) MarkNavAround(ctx, second);
            return true;
        }

        /// <summary>
        /// Raise the thing, and keep asking on later ticks while somebody is walking through the
        /// cell it will fill.
        ///
        /// <para><b>Because the driver's own check cannot close the window.</b>
        /// <see cref="CanRaiseNow"/> is asked in the pawn phase and the raise happens in the
        /// deferred phase at the end of the same tick, so a colonist can step into the cell in
        /// between — movement runs in that same phase, and pawns after this one in the order have
        /// not moved yet when the check is made. Without a retry the site would sit finished and
        /// unraised until a work giver offered it again, and the next builder's first stroke would
        /// roll for success a **second** time on work that was already done. The roll happens
        /// once; this is what gets the result of it into the world.</para>
        ///
        /// <para>The order it was rolled for is named, so a player who cancels the site or orders
        /// something else there in the meantime does not get a bed built with a wall's dice.</para>
        /// </summary>
        public void RaiseWhenClear(PawnContext ctx, int cell, int building, byte quality = 0)
        {
            if (_building[cell] != building) return;
            if (Raise(ctx, cell, quality)) return;

            // Deferred from inside the deferred phase, which SimWorld.Tick puts on the next tick.
            ctx.Defer(_ => RaiseWhenClear(ctx, cell, building, quality));
        }

        /// <summary>The walkability half of a world edit, both ends of a two-cell thing's "above".</summary>
        void MarkNavAround(PawnContext ctx, int cell)
        {
            ctx.Nav.MarkDirty(cell);
            int above = cell + _grid.Size.LayerStride;
            if (above < _grid.Size.CellCount) ctx.Nav.MarkDirty(above);

            // 4. And what holds the boundary above this cell up. A wall raised gives the slab over
            //    it full support; a slab raised is itself a medium that carries load sideways to
            //    the slabs beside it.
            ctx.MarkStructureChanged(cell);
            ctx.Enclosure?.MarkDirty(cell);

            // 5. A ladder joins two layers, and a slab is what gives a ladder somewhere to arrive.
            //    Both are refreshed here because either can be the one that completes the pair.
            RefreshLaddersAround(ctx, cell);
        }

        /// <summary>
        /// Make the connector under this cell agree with what is actually standing there.
        ///
        /// <para><b>This is the line that makes an upper storey somewhere you can go.</b> Measured
        /// before it existed: every slab in the game came back <c>walkable = true, reachable =
        /// false</c> — a lone slab, a roof corner, a roof middle. Vertical movement goes through a
        /// <c>Pathing.Connector</c>, and connectors only ever came out of worldgen, so a colony
        /// could build a second storey and never stand on it (U43).</para>
        ///
        /// <para><b>Idempotent, and called from everywhere either end can change</b> — the ladder
        /// going up, the floor above it going in, and either coming out again. That is deliberate:
        /// a player may build the ladder first or the floor first, and a rule that only worked in
        /// one order would be a fault nobody could describe.</para>
        ///
        /// <para>A connector wants both ends walkable, which is the registrar's own rule. So a
        /// ladder with nothing above it registers nothing and is simply a thing on a wall until a
        /// floor arrives over it.</para>
        /// </summary>
        /// <summary>
        /// Every ladder whose connector this cell could have changed.
        ///
        /// <para>Three families, and the third is new with the landing rule. A ladder <b>in</b> this
        /// cell, and one <b>under</b> it, are the two ends of the pair the cell is part of. But a
        /// shaft arrives in an <i>open</i> cell now and steps sideways on to the floor beside it, so
        /// a slab laid here is also the landing for a ladder standing under each of this cell's four
        /// neighbours — and taking it away again strands that ladder. Missing those is the class of
        /// fault that leaves a colony with a ladder it can climb and a ladder it cannot, identical
        /// on the board and differing only in the order the two things were built.</para>
        /// </summary>
        void RefreshLaddersAround(PawnContext ctx, int cell)
        {
            GridSize size = _grid.Size;
            RefreshLadder(ctx, cell);
            RefreshLadder(ctx, cell - size.LayerStride);

            // And the ladder *above* this cell, since a chain was allowed (2026-09-18): a ladder
            // stands on the one below it, so pulling this one out has to take the connector of the
            // one above with it. Without this line the upper half of a shaft outlives its own
            // footing — a colonist climbing a ladder that starts in mid-air.
            RefreshLadder(ctx, cell + size.LayerStride);

            CellRef at = size.FromIndex(cell);
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = at.X + (dir == 1 ? 1 : dir == 3 ? -1 : 0);
                int nz = at.Z + (dir == 0 ? 1 : dir == 2 ? -1 : 0);
                if (!size.Contains(nx, nz, at.Y)) continue;
                RefreshLadder(ctx, size.Index(nx, nz, at.Y) - size.LayerStride);
            }
        }

        void RefreshLadder(PawnContext ctx, int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return;

            bool wanted = IsLadder(cell);
            int above = cell + _grid.Size.LayerStride;
            if (wanted)
                wanted = above < _grid.Size.CellCount
                    && StandsOnAFooting(cell) && LadderArrivesAt(above);

            int existing = ctx.Nav.OneCellConnectorAt(cell);
            if (wanted == (existing >= 0)) return;

            if (wanted) ctx.Nav.AddConnector(ConnectorKind.Ladder, new[] { cell }, new[] { above });
            else ctx.Nav.RemoveConnector(existing);
        }

        /// <summary>
        /// Re-derive every ladder's connector, for a colony that has just been loaded.
        ///
        /// <para>A built ladder is an edifice and edifices are saved; its connector is <b>not</b>
        /// saved, because it is derived — the same argument <c>ColonyWorld.RebuildDerived</c>
        /// already makes about structural support and the region graph, and the reason this unit
        /// needs no save-format change at all. Worldgen's own ladders come back when the seed is
        /// regenerated; these are the ones a colony added afterwards.</para>
        /// </summary>
        public void RebuildLadderConnectors(PawnContext ctx)
        {
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice placed = _edifices[i];
                if (placed.Removed || placed.Def != CoreContent.EdificeLadder) continue;
                RefreshLadder(ctx, placed.CellIndex);
            }
        }

        /// <summary>
        /// Is the top of a ladder somewhere a colonist can arrive?
        ///
        /// <para><b>The old test was <c>IsWalkable</c>, and it demanded the one arrangement the
        /// owner reported as a bug.</b> <c>CellGrid.IsWalkable</c> needs <c>HasFloor</c> — a slab in
        /// the cell, or solid ground under it — so the only ladder that ever registered a connector
        /// was a ladder with a slab <i>directly above it</i>, and a colonist climbing it went
        /// straight through the floor (owner, 2026-09-18). The configuration the report calls wrong
        /// and the configuration the rule demanded were the same one.</para>
        ///
        /// <para>So the shaft cell is <b>open</b> — that is what a ladder goes up — and what makes
        /// it somewhere to arrive is one of two things:</para>
        /// <list type="bullet">
        /// <item><b>a landing beside it:</b> an orthogonal neighbour with a real floor, which is the
        /// slab you step off on to. The owner's own answer to what a legal ladder needs at the
        /// top.</item>
        /// <item><b>another ladder in it:</b> the shaft continues, and it is the topmost ladder of
        /// the chain that has to find a landing. Without this clause a run of ladders up through a
        /// mined shaft could never be built from the bottom, which is the only order a player can
        /// build one in.</item>
        /// </list>
        ///
        /// <para><b>A real floor used to count too, and no longer does</b> (2026-09-18, owner:
        /// *"accept the save break"*). That clause was kept so the rule would be a strict superset
        /// of the old one — every ladder the ruined city stamped and every ladder in an older save
        /// went on working — and its price was exactly the thing the placement rule forbids: a
        /// ladder under a floor that still registers its connector, so a colonist still climbs
        /// through the deck. A ladder under a floor now opens nothing, which is the second line of
        /// defence behind <see cref="ShaftRulePermits"/> rather than the fix itself: the fix is that
        /// the arrangement can no longer be built.</para>
        ///
        /// <para>Standing in the shaft cell is granted by <c>NavGrid.RefreshFrom</c> — <i>"a
        /// connector is its own floor"</i> — so the cell becomes walkable the moment the connector
        /// registers, and the step sideways on to the landing is an ordinary walk.</para>
        /// </summary>
        bool LadderArrivesAt(int top)
        {
            if ((uint)top >= (uint)_grid.Size.CellCount) return false;
            if (_grid.IsSolidTerrain(top)) return false;
            if (_grid.IsBlockedByEdifice(top)) return false;
            if (NaturalContent.IsWater(_grid.Terrain[top])) return false;

            if (IsLadder(top)) return true;

            return HasLandingBeside(top);
        }

        /// <summary>
        /// Is there a floor to step off on to, orthogonally beside this cell?
        ///
        /// <para>Orthogonal and on the same layer: a landing is the slab next to the top of the
        /// shaft, not one a layer away and not a diagonal, because a diagonal is not a step the
        /// mover makes (the search is 4-connected — see <c>PathFinder</c>).</para>
        /// </summary>
        bool HasLandingBeside(int cell)
        {
            GridSize size = _grid.Size;
            CellRef at = size.FromIndex(cell);

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = at.X + (dir == 1 ? 1 : dir == 3 ? -1 : 0);
                int nz = at.Z + (dir == 0 ? 1 : dir == 2 ? -1 : 0);
                if (!size.Contains(nx, nz, at.Y)) continue;
                if (_grid.IsWalkable(size.Index(nx, nz, at.Y))) return true;
            }

            return false;
        }

        /// <summary>Is a ladder standing in this cell, whoever put it there?</summary>
        bool IsLadder(int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return false;
            int handle = _grid.Edifice[cell];
            if (handle < 0 || handle >= _edifices.Count) return false;
            PlacedEdifice placed = _edifices[handle];
            return !placed.Removed && placed.Def == CoreContent.EdificeLadder;
        }

        /// <summary>
        /// A floor: the slab kind and the material, written into the cell's lower boundary.
        ///
        /// <para><c>CoreContent.SlabBuilt</c> and never one of the generator's three kinds, because
        /// that is what makes "take our own floors apart and not the ruined city's" a question that
        /// can be asked. It is <see cref="PlacedEdifice.Built"/>'s argument one level down, and it
        /// costs no new state: <c>Floor[]</c> has always been saved and always been hashed.</para>
        /// </summary>
        void RaiseSlab(int cell, ushort stuff, bool covering)
        {
            _grid.Floor[cell] = covering ? CoreContent.SlabPaved : CoreContent.SlabBuilt;
            _grid.FloorStuff[cell] = stuff;
            _grid.Footprint.Touch(cell);
        }

        /// <summary>
        /// A wall, as the record the ruined city's own walls are kept in, so that a wall a colonist
        /// built and a wall the generator stamped are indistinguishable to everything downstream —
        /// the mesher, the picker, deconstruction and the solver.
        ///
        /// <para><c>Built = true</c>: ours, and the only place in the game that says so. Everything
        /// the generator stamps leaves it false.</para>
        /// </summary>
        /// <para><c>second</c> is the far cell of a two-cell thing, or -1. Both cells point at the
        /// <b>one</b> record — the invariant the whole bed design stands on
        /// (docs/design/20-beds.md §4) — and the facing it was placed at is what derives that cell
        /// again later.</para>
        ///
        /// <para><b>The facing is kept for anything that rotates, not only for what is two cells
        /// wide</b> (2026-09-18). It used to be stored only when there was a far cell to derive,
        /// on the reasoning that a one-cell thing has nothing to point at — which stopped being
        /// true the moment a ladder became rotatable, and would have thrown the player's rotation
        /// away silently between the order and the built thing. <c>Place</c> already zeroes the
        /// facing of anything that does not rotate, so this is the same rule read off the def
        /// rather than off the footprint.</para>
        void RaiseEdifice(int cell, BuildingDef def, ushort stuff, int second, byte facing, byte quality)
        {
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = cell, Def = def.edifice, Stuff = stuff, Built = true,
                Facing = def.rotates ? facing : (byte)0, Quality = quality,
            });
            _grid.Edifice[cell] = _edifices.Count - 1;
            if (second >= 0) _grid.Edifice[second] = _edifices.Count - 1;
            _grid.Footprint.Touch(cell);
            if (second >= 0) _grid.Footprint.Touch(second);
            // The first campfire of a colony with no hearth becomes it (design 43 §3f).
            if (def.edifice == CoreContent.EdificeCampfire) Hearth?.OfferRaised(cell);
            if (def.blocking)
            {
                _grid.Flags[cell] |= CellFlags.BlockingEdifice;
                if (second >= 0) _grid.Flags[second] |= CellFlags.BlockingEdifice;
            }

            // Furniture takes its cells out of circulation for items, both of them: a bed must not
            // be walled off from the order and then have a pile carried on to it afterwards.
            if (def.needsClearCell)
            {
                _items.BlockItemsAt(cell);
                if (second >= 0) _items.BlockItemsAt(second);
            }
        }

        /// <summary>
        /// Work out again which cells hold furniture nothing may be put down in, for a colony that
        /// has just been loaded.
        ///
        /// <para>Derived rather than saved, the same argument <see cref="RebuildLadderConnectors"/>
        /// makes one line along: the edifice is in the save and what it implies about the cells
        /// around it is worked out from that, so this costs no save format and no hash bit.</para>
        /// </summary>
        public void RebuildItemBlocks()
        {
            _items.ClearItemBlocks();

            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice placed = _edifices[i];
                if (placed.Removed) continue;
                if (!ConstructionContent.NeedsClearCell(placed.Def)) continue;

                _items.BlockItemsAt(placed.CellIndex);
                int second = EdificeFootprint.SecondCell(
                    placed.CellIndex, placed.Def, placed.Facing, _grid.Size);
                if (second >= 0) _items.BlockItemsAt(second);
            }
        }

        /// <summary>
        /// Re-register NavFlags.Door on the NavGraph for doors in a colony that has just been loaded.
        /// Derived from the edifice list, matching RebuildLadderConnectors and RebuildItemBlocks.
        /// </summary>
        public void RebuildDoors(PawnContext ctx)
        {
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice placed = _edifices[i];
                if (placed.Removed) continue;
                if (placed.Def == CoreContent.EdificeDoor)
                    ctx.Nav.SetDoor(placed.CellIndex, isDoor: true, open: false);
                // And the cover crossed but never stood on (design 50 §5), derived the same way.
                int building = ConstructionContent.BuildingForEdifice(placed.Def);
                if (building != BuildingHandle.None && ConstructionContent.BuildingAt(building).passThrough)
                    ctx.Nav.SetPassThrough(placed.CellIndex, ConstructionContent.BuildingAt(building).crossCost);
            }
        }

        /// <summary>Is this edifice something crossed but never stood on (design 50 §5)?</summary>
        public static bool IsPassThrough(ushort edifice)
        {
            int building = ConstructionContent.BuildingForEdifice(edifice);
            return building != BuildingHandle.None && ConstructionContent.BuildingAt(building).passThrough;
        }

        /// <summary>
        /// Take a floor of ours back out, and say whether there was one.
        ///
        /// <para><see cref="Demolish"/>'s twin, beside it for the same reason <see cref="Raise"/>
        /// keeps both halves together. Only <c>SlabBuilt</c>: a stamped deck belongs to the city
        /// and to whatever line of work claims ruins, and the check is here as well as in the
        /// designation rule because this is the method that does the damage.</para>
        /// </summary>
        public bool RemoveSlab(PawnContext ctx, int cell, out ushort stuff)
        {
            stuff = CoreContent.StuffNone;
            if ((uint)cell >= (uint)_grid.Size.CellCount) return false;
            if (!ConstructionContent.IsOurs(_grid.Floor[cell])) return false;

            stuff = _grid.FloorStuff[cell];
            _grid.Floor[cell] = CoreContent.SlabNone;
            _grid.FloorStuff[cell] = CoreContent.StuffNone;
            _grid.Footprint.Touch(cell);

            MarkChunksAround(ctx, cell);
            ctx.Nav.MarkDirty(cell);
            int above = cell + _grid.Size.LayerStride;
            if (above < _grid.Size.CellCount) ctx.Nav.MarkDirty(above);

            // The slab was the roof of whatever is under it, and a room with a hole in its roof
            // is not a room: the enclosure must hear about it exactly as Demolish tells it about
            // a wall. It did not until the 2026-09-21 review (design 28 §12, F3), and a roof taken
            // off stayed warm until an unrelated edit re-solved the layer.
            ctx.Enclosure?.MarkDirty(cell);

            // Pawns and loose items resting on the removed slab drop to the landing floor below.
            // A colonist tearing down the floor underfoot steps down without panic (NoThought).
            Falling.OutOf(ctx, cell, thought: Falling.NoThought, tick: 0);

            // The floor that has just gone was holding up whatever was beside it on this boundary.
            // This is the line that lets a player pull the last support out of a room and watch it
            // come down, which is what the unit is for.
            ctx.MarkStructureChanged(cell);

            // And a ladder below has just lost the landing it arrived at (U43).
            RefreshLaddersAround(ctx, cell);
            return true;
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
        /// <para><b>Support is marked dirty since U29</b>, as it is in <see cref="Raise"/> and in
        /// <c>MineCell</c>. All three deliberately did not while nothing collapsed, and all three
        /// carried the warning that they should be wired together rather than one of them quietly
        /// acquiring behaviour the others lack.</para>
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

            // Both cells go from the record, not from the click: a two-cell thing taken apart by
            // naming either half must leave neither half behind, and clearing "whichever cell was
            // clicked" is how the other one survives to point at a record that says it is gone.
            int second = EdificeFootprint.SecondCell(was.CellIndex, was.Def, was.Facing, _grid.Size);

            // 1. The thing itself.
            _grid.RemoveEdifice(was.CellIndex);
            if (second >= 0) _grid.RemoveEdifice(second);
            _grid.Footprint.Touch(was.CellIndex);
            if (second >= 0) _grid.Footprint.Touch(second);
            // The hearth coming down leaves the colony without one; nothing takes its place.
            Hearth?.Lost(was.CellIndex);
            PlacedEdifice gone = was;
            gone.Removed = true;
            _edifices[handle] = gone;

            if (was.Def == CoreContent.EdificeDoor) ctx.Nav.SetDoor(was.CellIndex, isDoor: false, open: false);
            if (IsPassThrough(was.Def)) ctx.Nav.SetPassThrough(was.CellIndex, 0);
            if (was.Def == CoreContent.EdificeBed)
            {
                _items.RemoveBed(was.CellIndex);
                ReleasePatientsBed(ctx, was.CellIndex);
            }

            // A store coming down spills what the board will take and loses the rest. That this
            // destroys is right here and refused one level up: the deconstruct job will not finish
            // a shelf whose contents have nowhere to go, so by the time this runs either the shelf
            // is empty or the building fell on it.
            if (ConstructionContent.SlotsOf(was.Def) > 0) ctx.StorageUnits?.Dissolve(ctx, handle);

            // Its switch and whatever wood was in its hopper go with it (design 32 §5).
            ctx.Power?.RemoveDevice(handle);

            // And a cooking station's bills and whatever was on the hob (design 48 §5).
            ctx.Kitchen?.Remove(handle);

            // Nothing may go on pointing at a building that has gone (design 33 §13h): what was
            // left of it after a fight, and an order to take it apart. Here, because this is the one
            // way an edifice leaves the world — taken apart, beaten down, or whatever calls it next.
            ctx.EdificeDamage.Clear(was.CellIndex);
            ClearDeconstructOrder(ctx, was.CellIndex);
            if (second >= 0) ClearDeconstructOrder(ctx, second);

            // 2. The cells and everything touching them must be re-meshed: a thing coming down
            // changes how its neighbours draw their own faces, and the vertical neighbours are in
            // other chunks.
            MarkChunksAround(ctx, was.CellIndex);
            if (second >= 0) MarkChunksAround(ctx, second);

            // 3. What is walkable changed here, and in the cell above through the floor rule.
            // The cells are ordinary ground again, so things may be put down in them.
            _items.AllowItemsAt(was.CellIndex);
            if (second >= 0) _items.AllowItemsAt(second);

            // 4 and 5 — what was holding the boundary above it up, and a ladder's connector at
            // either end — ride along inside MarkNavAround, which is why a two-cell thing gets it
            // for both of its cells rather than only for the one that was clicked.
            MarkNavAround(ctx, was.CellIndex);
            if (second >= 0) MarkNavAround(ctx, second);
            return true;
        }

        /// <summary>
        /// A bed coming down lets go of the patient lying in it (design 33 §11h). The bed's head-cell
        /// reservation passed to her on the lay and her <c>Job_Downed</c> holds it until she gets up,
        /// so without this she kept a claim on bare ground for days and a bed raised on that cell read
        /// as taken. Only a downed pawn: a sleeper's and a rescuer's jobs ask about their bed and let
        /// go themselves. Taken off her own list as well as the table, so the two keep agreeing and
        /// her job's end has nothing left to release. Scales with the pawns, once per bed demolished.
        /// </summary>
        static void ReleasePatientsBed(PawnContext ctx, int head)
        {
            long key = ReservationManager.Key(ReservationTargetKind.Cell, head);
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!pawn.Downed || !pawn.HeldReservations.Remove(key)) continue;
                ctx.Reservations.Release(pawn.Id, key);
            }
        }

        /// <summary>
        /// An order to take apart a building that is no longer there is an order on nothing. The
        /// deconstruct driver clears its own before the removal; this is for every other route.
        /// </summary>
        static void ClearDeconstructOrder(PawnContext ctx, int cell)
        {
            var designations = ctx.Designations;
            if (designations != null && designations.At(cell) == Designations.DesignationKind.Deconstruct)
                designations.Clear(cell);
        }

        /// <summary>
        /// Is there anybody in the way who will not move by themselves? False means the raise
        /// must wait: somebody is walking through the cell this building will fill.
        ///
        /// <para>Asked by the build driver before the last blow — see <c>BuildJobDriver</c> — so
        /// that waiting costs a held hammer rather than a fresh success roll on work that is
        /// already done.</para>
        /// </summary>
        public bool CanRaiseNow(PawnContext ctx, int cell)
        {
            if (!BlocksTheCell(cell)) return true;

            int second = SecondCellOf(cell);
            return !PassingThrough(ctx, cell) && (second < 0 || !PassingThrough(ctx, second));
        }

        /// <summary>
        /// Clear both of a building's cells of people, or say that it cannot be done yet.
        /// <see cref="PawnEviction"/> holds the rule about where a displaced colonist goes.
        /// </summary>
        bool MakeRoom(PawnContext ctx, int cell, int second)
        {
            if (!BlocksTheCell(cell)) return true;
            if (!MakeRoomIn(ctx, cell)) return false;
            return second < 0 || MakeRoomIn(ctx, second);
        }

        static bool MakeRoomIn(PawnContext ctx, int cell)
        {
            Pawn? occupant = PawnEviction.Occupant(ctx, cell);
            if (occupant == null) return true;
            // Walking through: wait rather than shove, because they will be gone of their own
            // accord and a shove the player can see is the cost being avoided here.
            if (occupant.HasPath) return false;
            // Standing there, and standing there is forever as far as a build order is concerned.
            // A colonist with nowhere at all to be put — enclosed in solid world — holds the
            // order up rather than being pushed into rock.
            return PawnEviction.Evict(ctx, occupant);
        }

        static bool PassingThrough(PawnContext ctx, int cell)
        {
            Pawn? occupant = PawnEviction.Occupant(ctx, cell);
            return occupant != null && occupant.HasPath;
        }

        /// <summary>Will the thing ordered here stand in the cell rather than under it?</summary>
        bool BlocksTheCell(int cell)
        {
            int building = _building[cell];
            return building != BuildingHandle.None
                   && ConstructionContent.BuildingAt(building).blocking;
        }

        /// <summary>The far cell of a two-cell site, or -1. Read from the site as it stands.</summary>
        int SecondCellOf(int cell)
        {
            int building = _building[cell];
            if (building == BuildingHandle.None) return -1;

            BuildingDef def = ConstructionContent.BuildingAt(building);
            if (def.footprint <= 1) return -1;

            return EdificeFootprint.SecondCell(cell, def.edifice, _facing[cell], _grid.Size);
        }

        static void MarkChunksAround(PawnContext ctx, int cell)
        {
            if (ctx.Chunks == null) return;

            // **The chunk grid's own bounds, not the cell grid's.** This loop decides which
            // chunks to dirty, so the grid it is about to write into is the one to ask. It used
            // to ask `ctx.Size`, which is right only while the two agree — and on 2026-09-20 they
            // did not: a new game on any board but the scene's default built its chunk grid from
            // the inspector's numbers and its world from the setup page's, and this wrote past
            // the end of the array with the check one line above passing. `OdysseyBootstrap`
            // decides the size once now, so they cannot disagree; asking the right grid is the
            // half of the fix that does not depend on remembering that.
            ChunkGrid chunks = ctx.Chunks;
            GridSize size = chunks.Size;
            CellRef at = ctx.Size.FromIndex(cell);

            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = at.X + dx, z = at.Z + dz, y = at.Y + dy;
                if (size.Contains(x, z, y)) chunks.MarkDirty(x, z, y);
            }
        }

        // ---- the intent seam -------------------------------------------------------------------

        /// <summary><c>PlaceBuilding(cell, A = building, B = stuff, C = facing)</c>.</summary>
        public IntentRejection HandlePlace(Intent intent) =>
            Place(intent.Cell, intent.A, intent.B, intent.C);

        /// <summary>
        /// <c>CancelBuilding(cell, A)</c>. <c>A</c> = 1 takes the building order only — the pane's
        /// Cancel, which names one thing; 0, every order in the cell, which is what a drag means.
        /// </summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell, lines: intent.A != 1);

        /// <summary>
        /// <c>AssignBedOwner(cell, A = pawn)</c>, A = -1 to leave the bed unowned. Either cell of
        /// the bed names it. See <see cref="IntentKind.AssignBedOwner"/> for the shape; the rules
        /// live here because this is the one owner of the edifice list.
        /// </summary>
        public IntentRejection HandleAssignOwner(Intent intent) => AssignOwner(intent.Cell, intent.A);

        /// <summary>
        /// The whole of bed ownership, as both the player's intent and the sleeper's own
        /// auto-claim reach it. <paramref name="pawnId"/> below zero releases the bed.
        /// </summary>
        public IntentRejection AssignOwner(CellRef cell, int pawn)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y))
                return IntentRejection.OutOfBounds;

            return AssignOwnerAt(_grid.Index(cell), pawn);
        }

        /// <summary>
        /// <see cref="AssignOwner"/> by whole-world cell index, which is the shape the sleep
        /// driver holds a bed in. The bounds check is the caller's; an index out of the edifice
        /// list is refused here as it is there.
        /// </summary>
        public IntentRejection AssignOwnerAt(int index, int pawn)
        {
            if (index < 0 || index >= _grid.Edifice.Length) return IntentRejection.OutOfBounds;
            int handle = _grid.Edifice[index];
            if (handle < 0 || handle >= _edifices.Count) return IntentRejection.NotPermitted;

            PlacedEdifice placed = _edifices[handle];
            if (placed.Def != CoreContent.EdificeBed || placed.Removed || !placed.Built)
                return IntentRejection.NotPermitted;

            // -1 is the interface's "release"; 0 is the record's "nobody" — the same answer.
            int pawnId = pawn < 0 ? 0 : pawn;
            if (pawnId != 0 && _pawns.Get(new PawnId(pawnId)) == null)
                return IntentRejection.NotPermitted;
            if (placed.Owner == pawnId) return IntentRejection.AlreadyInThatState;

            // One bed per colonist, kept by the handler rather than hoped for by the interface:
            // taking a new bed releases the old one in the same breath.
            if (pawnId != 0)
            {
                for (int i = 0; i < _edifices.Count; i++)
                {
                    PlacedEdifice other = _edifices[i];
                    if (other.Def != CoreContent.EdificeBed || other.Removed || other.Owner != pawnId)
                        continue;
                    other.Owner = 0;
                    _edifices[i] = other;
                }
            }

            // Somebody's night just changed. Which body it is, is not this class's question —
            // see `BedOwnershipChanged`.
            BedOwnershipChanged = true;

            placed.Owner = pawnId;
            _edifices[handle] = placed;
            return IntentRejection.None;
        }

        /// <summary>
        /// A bed changed hands this tick, so somebody may be asleep in the wrong one.
        /// <c>JobSystem.GetOutOfTheWrongBed</c> reads it, acts, and clears it.
        ///
        /// <para><b>A flag rather than a list of the colonists involved</b>, and the first draft
        /// was the list. Naming the old owner and the new one misses the
        /// commonest case there is: a colony short of beds keeps them unowned and shared
        /// (<see cref="TryClaimForSleeper"/>), so the colonist actually lying in a bed when the
        /// player gives it away is very often nobody's owner and appears in no such list. Who is
        /// affected is a question about where people are sleeping, and this class does not know
        /// that — the job system does.</para>
        ///
        /// <para><b>Not saved and not hashed, and it cannot be.</b> Intents drain at step 1 of
        /// <c>SimWorld.Tick</c> and the pawn phase runs at step 4 of the same tick, so the flag is
        /// raised and lowered inside one tick and never crosses a save boundary. That is what
        /// keeps it from being a second copy of world state.</para>
        /// </summary>
        public bool BedOwnershipChanged { get; private set; }

        /// <summary>Lower the flag, once it has been acted on.</summary>
        public void ClearBedOwnershipChanged() => BedOwnershipChanged = false;

        // ---- the bed, as the sleep chooser and the pane read it --------------------------------

        /// <summary>The tier the bed at this cell finished at, or 0 where no bed stands here.</summary>
        public byte BedQualityAt(int cell)
        {
            PlacedEdifice placed = BedAt(cell);
            return placed.Def == CoreContent.EdificeBed ? placed.Quality : (byte)0;
        }

        /// <summary>Who owns the bed at this cell, or 0 where no bed stands here or it is nobody's.</summary>
        public int BedOwnerAt(int cell) =>
            (uint)cell < (uint)_grid.Edifice.Length ? BedAt(cell).Owner : 0;

        /// <summary><see cref="BedOwnerAt(int)"/> by cell reference, which is how the pane holds one.</summary>
        public int BedOwnerAt(CellRef cell) =>
            _grid.Contains(cell.X, cell.Z, cell.Y) ? BedOwnerAt(_grid.Index(cell)) : 0;

        /// <summary>
        /// Every bed this pawn owns goes back to nobody: the pawn is leaving the board
        /// (<c>PawnRegistry.Despawn</c>, design 33 §5c). Returns how many were released.
        ///
        /// <para><b>Does not raise <see cref="BedOwnershipChanged"/>.</b> That flag asks the job
        /// system to move sleepers out of beds that are no longer theirs, and a bed going to nobody
        /// takes nobody out of it — so raising it would only be a flag that can outlive its tick.</para>
        /// </summary>
        public int ReleaseBedsOf(int pawnId)
        {
            if (pawnId <= 0) return 0;
            int released = 0;
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice bed = _edifices[i];
                if (bed.Def != CoreContent.EdificeBed || bed.Removed || bed.Owner != pawnId) continue;
                bed.Owner = 0;
                _edifices[i] = bed;
                released++;
            }
            return released;
        }

        /// <summary>Whether this colonist already has a bed of her own somewhere on the map.</summary>
        public bool PawnOwnsABed(int pawnId)
        {
            if (pawnId <= 0) return false;
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice bed = _edifices[i];
                if (bed.Def == CoreContent.EdificeBed && !bed.Removed && bed.Owner == pawnId)
                    return true;
            }
            return false;
        }

        /// <summary>Built beds nobody owns — the shared pool anyone may sleep in.</summary>
        public int UnownedBedCount()
        {
            int free = 0;
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice bed = _edifices[i];
                if (bed.Def == CoreContent.EdificeBed && !bed.Removed && bed.Built && bed.Owner == 0)
                    free++;
            }
            return free;
        }

        /// <summary>
        /// A colonist who has just reached a bed nobody owns takes it as her own — the "not
        /// claimed" half of the owner's rule of 2026-09-19, so that who sleeps where settles
        /// instead of being redecided every night by whoever is nearest.
        ///
        /// <para><b>But never at somebody else's expense.</b> An unowned bed is the shared pool:
        /// anyone may sleep in one, and claiming takes it out of that pool for good. So the claim
        /// happens only when the pool would still hold a bed for every colonist who has none —
        /// two beds between three colonists stay unowned and shared for ever, and the third does
        /// not end up on the floor because the first two got in early. With a bed each, all of
        /// them claim on their first night and nothing is lost.</para>
        ///
        /// <para>Returns whether the bed was claimed. It is a no-op on a bed that is already
        /// owned, including by this colonist.</para>
        /// </summary>
        public bool TryClaimForSleeper(int cell, PawnId pawn)
        {
            if (cell < 0 || cell >= _grid.Edifice.Length) return false;
            if (BedOwnerAt(cell) != 0) return false;
            if (PawnOwnsABed(pawn.Value)) return false;

            int bedlessOthers = 0;
            var all = _pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                Pawns.Pawn other = all[i];
                if (other.Id.Value == pawn.Value) continue;
                if (!PawnOwnsABed(other.Id.Value)) bedlessOthers++;
            }

            // One bed leaves the pool if this succeeds; what is left must still cover everyone
            // else who has none.
            if (UnownedBedCount() - 1 < bedlessOthers) return false;

            return AssignOwnerAt(cell, pawn.Value) == IntentRejection.None;
        }

        /// <summary>
        /// The bed record a cell points at, or a record whose <c>Def</c> is not a bed. Either cell
        /// of a two-cell bed answers, because both point at the one record.
        /// </summary>
        PlacedEdifice BedAt(int cell)
        {
            int handle = _grid.Edifice[cell];
            if (handle < 0 || handle >= _edifices.Count) return default;

            PlacedEdifice placed = _edifices[handle];
            return placed.Def == CoreContent.EdificeBed && !placed.Removed ? placed : default;
        }

        /// <summary>Register everything this grid is: hashed state, saved state, a channel, the intents.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.PlaceBuilding, HandlePlace)
                .AddIntentHandler(IntentKind.CancelBuilding, HandleCancel)
                .AddIntentHandler(IntentKind.AssignBedOwner, HandleAssignOwner);
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
                hash.Add(_facing[index]);
                hash.Add(_delivered[index]);
                hash.Add(_parts[index]);
                // Milliwork, whole, for the reason DesignationGrid's own ledger states: the hash
                // is kept at the resolution the ledger is.
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
                writer.Write(_facing[index]);
                writer.Write(_delivered[index]);
                writer.Write(_work[index]);
            }
        }

        public void Load(SaveReader reader)
        {
            Array.Clear(_building, 0, _building.Length);
            Array.Clear(_stuff, 0, _stuff.Length);
            Array.Clear(_facing, 0, _facing.Length);
            _sites.Clear();

            // Version 3 gave a site its facing; a version 2 site list never wrote one, and north
            // is the zero it falls back to.
            bool facings = reader.FormatVersion >= 4;
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int index = reader.ReadInt();
                byte building = reader.ReadByte();
                byte stuff = reader.ReadByte();
                byte facing = facings ? reader.ReadByte() : (byte)0;
                int delivered = reader.ReadInt();
                int work = reader.ReadInt();
                if (index < 0 || index >= _building.Length || building == 0) continue;

                Set(index, building, stuff, facing);
                // After Set, which zeroes both: a half-built wall survives a save, and so does the
                // wood already carried to it. Before format 5 the ledger counted ticks.
                _delivered[index] = delivered;
                _work[index] = Rates.FromSave(work, reader.FormatVersion);
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

                BuildingDef def = ConstructionContent.BuildingAt(building);
                writer.AddSite(new SiteView(
                    index, building, _stuff[index],
                    (ushort)_delivered[index],
                    (ushort)def.costCount,
                    // The contract is ticks: the ledger is divided back where it is published,
                    // and the price was never scaled (§2bb — the scale stops at the contract).
                    _work[index] / Rates.Scale, WorkFor(index),
                    _facing[index], (byte)def.footprint,
                    (ushort)_parts[index], (ushort)(def.HasParts ? def.partCount : 0),
                    (short)(def.HasParts ? def.partItem : -1)));
            }
        }

        /// <summary>
        /// The parts delivered to sites, saved apart from the sites themselves (design 32 §14):
        /// <c>(cell, parts)</c> for every site holding any. Appended after the construction section,
        /// so the sites it names already exist when it is read; one naming a cell with no site is a
        /// file that disagrees with itself and is skipped. Hashed by the grid, not here.
        /// </summary>
        sealed class PartsSection : ISaveable
        {
            readonly ConstructionGrid _grid;

            public PartsSection(ConstructionGrid grid) { _grid = grid; }

            public string SaveKey => "odyssey.construction.parts";

            public void Save(SaveWriter writer)
            {
                int count = 0;
                for (int i = 0; i < _grid._sites.Count; i++) if (_grid._parts[_grid._sites[i]] > 0) count++;
                writer.Write(count);
                for (int i = 0; i < _grid._sites.Count; i++)
                {
                    int index = _grid._sites[i];
                    if (_grid._parts[index] <= 0) continue;
                    writer.Write(index);
                    writer.Write(_grid._parts[index]);
                }
            }

            public void Load(SaveReader reader)
            {
                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    int index = reader.ReadInt();
                    int parts = reader.ReadInt();
                    if ((uint)index >= (uint)_grid._parts.Length || _grid._building[index] == 0) continue;
                    _grid._parts[index] = parts;
                }
            }
        }
    }
}
