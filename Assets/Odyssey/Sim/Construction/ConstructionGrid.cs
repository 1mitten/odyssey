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
            if (!ConstructionContent.IsBuildable(stuff)) return IntentRejection.NotPermitted;

            BuildingDef def = ConstructionContent.BuildingAt(building);

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
        public int WhereItWouldLand(int index, int building)
        {
            if ((uint)index >= (uint)_grid.Size.CellCount) return index;

            BuildingDef what = ConstructionContent.BuildingAt(building);

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
            return def.covering ? AllowsCovering(index)
                : def.slab
                ? AllowsSlab(index)
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
        bool SomethingUnderfoot(int index)
        {
            if (_grid.HasFloor(index)) return true;
            int below = index - _grid.Size.LayerStride;
            return below >= 0 && (_grid.Flags[below] & CellFlags.BlockingEdifice) != 0;
        }

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
            if (was == now) return;

            int at = _sites.BinarySearch(index);
            if (now) _sites.Insert(~at, index);
            else _sites.RemoveAt(at);
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
        public void Raise(PawnContext ctx, int cell, byte quality = 0)
        {
            int building = _building[cell];
            if (building == BuildingHandle.None) return;

            BuildingDef def = ConstructionContent.BuildingAt(building);
            ushort stuff = ConstructionContent.StuffAt(_stuff[cell]).stuff;
            int second = EdificeFootprint.SecondCell(cell, def.edifice, _facing[cell], _grid.Size);

            // A two-cell thing's cells were both validated at the order; the world can still have
            // moved under the far one while the wood was being fetched — the ground below it can
            // have been mined out. The order dies and the material goes back rather than half a
            // bed standing on air, which is the same answer a collapse will give when it exists.
            if (second >= 0 && !Allows(second))
            {
                Refund(cell);
                Clear(cell);
                return;
            }

            Clear(cell);

            // 1. The thing itself — a slab at the cell's lower boundary, or an edifice standing in
            //    the cell. One `if`, because everything else about the two is identical.
            if (def.slab) RaiseSlab(cell, stuff, def.covering);
            else RaiseEdifice(cell, def, stuff, second, _facing[cell], quality);

            // A finished bed's head cell joins the list the sleep chooser already scans — the
            // scenario's own start-of-world cells are already in it, and the chooser does not
            // care which half of the game put a cell there.
            if (def.edifice == CoreContent.EdificeBed) _items.AddBed(cell);

            // 2. The cells and everything touching them must be re-meshed: a thing changes how its
            // neighbours draw their own faces, and the vertical neighbours are in other chunks.
            MarkChunksAround(ctx, cell);
            if (second >= 0) MarkChunksAround(ctx, second);

            // 3. What is walkable changed here, and in the cell above through the floor rule.
            MarkNavAround(ctx, cell);
            if (second >= 0) MarkNavAround(ctx, second);
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

            // 5. A ladder joins two layers, and a slab is what gives a ladder somewhere to arrive.
            //    Both are refreshed here because either can be the one that completes the pair.
            RefreshLadder(ctx, cell);
            RefreshLadder(ctx, cell - _grid.Size.LayerStride);
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
        void RefreshLadder(PawnContext ctx, int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return;

            bool wanted = IsLadder(cell);
            int above = cell + _grid.Size.LayerStride;
            if (wanted)
                wanted = above < _grid.Size.CellCount
                    && _grid.IsWalkable(cell) && _grid.IsWalkable(above);

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

        /// <summary>Is a ladder standing in this cell, whoever put it there?</summary>
        bool IsLadder(int cell)
        {
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
        /// again later, so it is stored only for things that have one.</para>
        void RaiseEdifice(int cell, BuildingDef def, ushort stuff, int second, byte facing, byte quality)
        {
            _edifices.Add(new PlacedEdifice
            {
                CellIndex = cell, Def = def.edifice, Stuff = stuff, Built = true,
                Facing = second >= 0 ? facing : (byte)0, Quality = quality,
            });
            _grid.Edifice[cell] = _edifices.Count - 1;
            if (second >= 0) _grid.Edifice[second] = _edifices.Count - 1;
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

            MarkChunksAround(ctx, cell);
            ctx.Nav.MarkDirty(cell);
            int above = cell + _grid.Size.LayerStride;
            if (above < _grid.Size.CellCount) ctx.Nav.MarkDirty(above);

            // The floor that has just gone was holding up whatever was beside it on this boundary.
            // This is the line that lets a player pull the last support out of a room and watch it
            // come down, which is what the unit is for.
            ctx.MarkStructureChanged(cell);

            // And a ladder below has just lost the landing it arrived at (U43).
            RefreshLadder(ctx, cell - _grid.Size.LayerStride);
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
            PlacedEdifice gone = was;
            gone.Removed = true;
            _edifices[handle] = gone;

            // A bed leaves the sleep chooser's list with the world; its owner goes with it, in
            // that the record nobody will read again still says who it was.
            if (was.Def == CoreContent.EdificeBed) _items.RemoveBed(was.CellIndex);

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

        /// <summary><c>PlaceBuilding(cell, A = building, B = stuff, C = facing)</c>.</summary>
        public IntentRejection HandlePlace(Intent intent) =>
            Place(intent.Cell, intent.A, intent.B, intent.C);

        /// <summary><c>CancelBuilding(cell)</c>.</summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell);

        /// <summary>
        /// <c>AssignBedOwner(cell, A = pawn)</c>, A = -1 to leave the bed unowned. Either cell of
        /// the bed names it. See <see cref="IntentKind.AssignBedOwner"/> for the shape; the rules
        /// live here because this is the one owner of the edifice list.
        /// </summary>
        public IntentRejection HandleAssignOwner(Intent intent)
        {
            if (!_grid.Contains(intent.Cell.X, intent.Cell.Z, intent.Cell.Y))
                return IntentRejection.OutOfBounds;

            int index = _grid.Index(intent.Cell);
            int handle = _grid.Edifice[index];
            if (handle < 0 || handle >= _edifices.Count) return IntentRejection.NotPermitted;

            PlacedEdifice placed = _edifices[handle];
            if (placed.Def != CoreContent.EdificeBed || placed.Removed || !placed.Built)
                return IntentRejection.NotPermitted;

            // -1 is the interface's "release"; 0 is the record's "nobody" — the same answer.
            int pawnId = intent.A < 0 ? 0 : intent.A;
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

            placed.Owner = pawnId;
            _edifices[handle] = placed;
            return IntentRejection.None;
        }

        // ---- the bed, as the sleep chooser and the pane read it --------------------------------

        /// <summary>The tier the bed at this cell finished at, or 0 where no bed stands here.</summary>
        public byte BedQualityAt(int cell)
        {
            PlacedEdifice placed = BedAt(cell);
            return placed.Def == CoreContent.EdificeBed ? placed.Quality : (byte)0;
        }

        /// <summary>Who owns the bed at this cell, or 0 where no bed stands here or it is nobody's.</summary>
        public int BedOwnerAt(int cell) => BedAt(cell).Owner;

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

                BuildingDef def = ConstructionContent.BuildingAt(building);
                writer.AddSite(new SiteView(
                    index, building, _stuff[index],
                    (ushort)_delivered[index],
                    (ushort)def.costCount,
                    _work[index], WorkFor(index),
                    _facing[index], (byte)def.footprint));
            }
        }
    }
}
