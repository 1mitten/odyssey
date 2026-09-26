#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Construction
{
    /// <summary>
    /// Something that can be built.
    ///
    /// <para><b>Two tables, not one.</b> A wall is a wall whether it is wood or stone — same
    /// footprint, same edifice, same thing a floor will one day rest on — and wood is wood whether
    /// it is a wall or a door. Folding the two into one table of "wooden wall, stone wall" looks
    /// simpler while there are two of each and multiplies the moment there are six. It is also the
    /// shape <see cref="PlacedEdifice"/> has carried since the ruined city was written: a def and a
    /// stuff, side by side.</para>
    /// </summary>
    public class BuildingDef : Def
    {
        /// <summary>The <c>CoreContent.Edifice*</c> value this becomes when it is finished.</summary>
        public ushort edifice;

        /// <summary>Whether the finished thing stops a colonist walking through the cell.</summary>
        public bool blocking = true;

        /// <summary>
        /// Is the finished thing a <b>slab at the cell's lower boundary</b> rather than an edifice
        /// standing in the cell?
        ///
        /// <para>This one bool is the whole difference between building a wall and building a
        /// floor. Everything else — the order, the material carried to it, the work applied, the
        /// reservation, the two work givers, the cancel, the refund — is the same pipeline, which
        /// is the point of having the tables at all (U29). <c>ConstructionGrid.Raise</c> is the one
        /// place that reads it.</para>
        /// </summary>
        public bool slab;

        /// <summary>
        /// Is this slab a <b>covering</b> — laid on ground that is already there — rather than
        /// structure spanning a gap?
        ///
        /// <para>Only meaningful with <see cref="slab"/>, and it inverts exactly one question.
        /// A structural slab wants a cell with <b>no</b> floor and asks the support rule whether it
        /// could stand; a covering wants a cell that <b>has</b> one and asks nothing, because
        /// something is already holding it up and it can never fall. Everything else — the order,
        /// the material, the work, the refund, the drawing — is untouched (U42,
        /// <c>docs/design/18-paving.md</c>).</para>
        ///
        /// <para>The owner's report that produced it: <i>"I should be able to just build a
        /// floor."</i> On the played board only 21 of the 441 cells within ten of the start will
        /// take a structural slab, and none of them is the grass you are standing on.</para>
        /// </summary>
        public bool covering;

        /// <summary>
        /// Cells the finished thing occupies, in a line along its facing. One for everything
        /// until the bed; two for the bed, whose one record both cells point at
        /// (<c>EdificeFootprint</c> derives the second, so nothing stores it twice).
        /// </summary>
        public int footprint = 1;

        /// <summary>
        /// Whether the ghost may be turned before placing. A thing that does not rotate ignores
        /// the facing it is handed, and one that does cannot be placed without one.
        /// </summary>
        public bool rotates;

        /// <summary>
        /// Whether the thing's completion rolls a quality tier (Poor to Epic) off the finishing
        /// colonist's Construction skill.
        ///
        /// <para><b>Furniture yes, structures no, for ever</b> — the reference's own split
        /// (a-04 §3: walls, doors and production buildings never carry quality; furniture, art and
        /// apparel do). A wall is as good as its material and nothing else, which is why a wall
        /// built by a master and by a novice are the same wall.</para>
        /// </summary>
        public bool takesQuality;

        /// <summary>
        /// Whether the cell must be empty of <b>items</b> before this can be ordered there.
        ///
        /// <para>Furniture, and nothing else so far. A bed is broad, low and open, so a stack of
        /// meals left on the ground stands straight up through the mattress (owner, 2026-09-18,
        /// with a screenshot of exactly that). A wall fills its cell and a slab is laid at the
        /// boundary under it, so neither shows what is lying there — and refusing those too would
        /// stop a colonist walling a corner because somebody dropped a log in it.</para>
        ///
        /// <para>A field rather than a rule in <c>Allows</c> because that is the difference: it is
        /// a fact about the shape of the thing, and the table is where facts about things live.</para>
        /// </summary>
        public bool needsClearCell;

        /// <summary>
        /// Units of stuff a site swallows before any work can start.
        ///
        /// <para>Five for a wall, which is the reference's number for a wall of any material
        /// (a-04-building-and-materials.md section 6). It is quoted in <i>stuff</i>, so a wooden
        /// wall costs five wood and a stone wall five stone, rather than five of some third
        /// thing.</para>
        /// </summary>
        public int costCount = 5;

        /// <summary>
        /// Base ticks of work, before the material's own factor.
        ///
        /// <para>135 is the reference's wall (a-04 section 6). Deliberately short beside felling's
        /// 800: the interesting part of building a wall is fetching the wood for it, and a wall
        /// that took a morning to raise would make a hut a week's work.</para>
        /// </summary>
        public int workToBuild = 135;

        /// <summary>
        /// How many stacks this thing holds, or 0 for anything that is not a store.
        ///
        /// <para>A field rather than a rule keyed off the edifice id, for the same reason
        /// <see cref="needsClearCell"/> is one: it is a fact about the shape of the thing, and the
        /// table is where facts about things live. It is also what makes a second, larger store one
        /// row of content rather than a second code path.</para>
        /// </summary>
        public int storageSlots;

        /// <summary>Construction level a colonist needs before it may take the job. 0 for a wall.</summary>
        public int minSkill;

        /// <summary>
        /// Heat the finished thing pushes into its room each thermal pass, in centi-degree-cells
        /// (design 28 §7): energy, not temperature, so the same campfire is an oven in a broom
        /// cupboard and a warm corner in a hall. Zero for everything that is not a heat source,
        /// which is everything until the campfire.
        /// </summary>
        public int heatPerPass;

        /// <summary>
        /// Is this a <b>power line</b> rather than a thing standing in the cell (design 32 §3)?
        ///
        /// <para>A line lives in its own per-cell layer, owned by <c>PowerGrid</c>, so it can run
        /// through a wall or under a floor without taking the cell's one edifice slot. Everything a
        /// player does to order one — arm, ghost, drag, place — is a build like any other, which is
        /// why it is a row here at all; <c>ConstructionGrid.Place</c> is the one place that reads
        /// this and hands the order on.</para>
        /// </summary>
        public bool conduit;

        /// <summary>
        /// A second payment, in one fixed item whatever the thing is made of — the scrap metal in a
        /// generator's workings, or the whole of a power line (design 32 §14). -1 for anything
        /// that is paid for in its material alone, which is everything before power.
        ///
        /// <para>Kept apart from <see cref="costCount"/> because the two answer different
        /// questions: the material is the player's choice and the part is not. A site banks them
        /// separately, is a frame only when both are in, and gives each back by its own rule.</para>
        /// </summary>
        public int partItem = -1;

        /// <summary>Units of <see cref="partItem"/> a site swallows before work can start.</summary>
        public int partCount;

        /// <summary>Does this thing take a second, fixed payment besides its material?</summary>
        public bool HasParts => partItem >= 0 && partCount > 0;

        /// <summary>Watts this makes while it runs, or 0 for anything that is not a generator (design 32 §5).</summary>
        public int powerOutputW;

        /// <summary>Watts this wants while it is switched on, or 0 for anything that is not a consumer.</summary>
        public int powerDrawW;

        /// <summary>The <see cref="ItemHandle"/> this burns, or -1 for anything that burns nothing.</summary>
        public int fuelItem = -1;

        /// <summary>How many of <see cref="fuelItem"/> the hopper holds, in whole units.</summary>
        public int fuelCapacity;

        /// <summary>
        /// Units of fuel a day <b>at full load</b>. The burn is in proportion to the load carried,
        /// so this is the ceiling, not the rate (design 32 §6, decision 8).
        /// </summary>
        public int fuelPerDay;

        /// <summary>Does a power net care about this — does it make power or spend it?</summary>
        public bool IsPowered => powerOutputW > 0 || powerDrawW > 0;
        /// How much warmer the thing's <b>own cell</b> is than the air around it, in
        /// centi-degrees (design 36). Zero for everything that is not a heat source.
        ///
        /// <para><b>A different thing from <see cref="heatPerPass"/>, and the pair is the point.</b>
        /// That one is energy pushed into the room's air — slow, shared by the whole room, and
        /// the same campfire is an oven in a cupboard and a warm corner in a hall. This is
        /// <i>radiance</i>: what a fire does to you by shining on you, which is immediate, local,
        /// and no different in a cupboard than in a hall. Design 28 models the air and refuses to
        /// store anything per cell; this is a pure function of how far away you are standing, so
        /// it needs no storage at all.</para>
        ///
        /// <para>Tuning the two apart is why they are separate fields. Making one tile read hot by
        /// raising <c>heatPerPass</c> would cook the whole hut.</para>
        /// </summary>
        public int radiantC;

        /// <summary>The registry key the interface names it by. Never a label, never a filename.</summary>
        public string iconKey = "";

        /// <summary>
        /// Hit points the finished thing has when it is struck (design 33 §4, C6), in whole points.
        /// Nought for nothing. A building nobody has hit is at this and carries no row anywhere;
        /// what is left of a struck one is <c>EdificeDamage</c>'s. INVENTED, per thing, before any
        /// material scaling — C6's to tune. Claimed by the combat contracts step; the campfire,
        /// conduit, generator and heater were given theirs by C6 (design 33 §13c). The pool is this
        /// times <see cref="StuffDef.hitPointsFactorPerMille"/> (<c>BuildingTargets.MaxMilliOf</c>),
        /// and only an edifice standing in a cell is ever struck, so a slab's and a line's number
        /// is not read yet.
        /// </summary>
        public int maxHitPoints;

        /// <summary>
        /// What the finished thing is worth as cover to a pawn standing beside it, per mille
        /// (design 53 §3): the base the angle, the shooter's distance and the descent then scale.
        /// Nought for a thing that is not cover, or whose cover is the full-fill rule's (a wall is
        /// worth <c>CombatDef.fullFillCoverPerMille</c> because it fills its cell, and says nothing
        /// here). Read only through <c>Cover.BaseAt</c>, the one owner. INVENTED per thing.
        /// </summary>
        public int coverPerMille;

        /// <summary>
        /// Is it <b>tall</b> cover — a shot has to come down steeply to get over it — rather than
        /// low? Design 53 §2b's two classes. False for every piece of furniture so far.
        /// </summary>
        public bool coverTall;

        /// <summary>
        /// Crossed but never stood on (design 53 §5, the owner): a pawn may walk over it at
        /// <see cref="crossCost"/> and may never stop in its cell — rest, work, wait and aim all
        /// happen beside it. The sandbag's and the barricade's rule, so cover is always beside a
        /// pawn and never under her, and a line of it seals nobody in.
        /// </summary>
        public bool passThrough;

        /// <summary>
        /// What crossing the cell costs on top of the step, in the path's units (a step is 100, a
        /// bush adds 50). Read by navigation through the cell's cost class, never directly.
        /// </summary>
        public int crossCost;

        /// <summary>
        /// The share of its cost left on the cell when fighting destroys it, per mille (design 53
        /// §2e, the owner: a quarter). Nought for everything built before cover, which keeps
        /// design 33's "a building destroyed in combat leaves nothing" for them.
        /// </summary>
        public int wreckRefundPerMille;

        /// <summary>
        /// The one material it is always built of, as a <see cref="StuffHandle"/>, or
        /// <see cref="StuffHandle.None"/> for "the one the player chose". The sandbags' rule
        /// (design 53 §4): filled bags are not a choice of wood or stone, and the placeholder
        /// recipe is five stone. <c>ConstructionGrid.Place</c> takes this over whatever the order
        /// named, so the palette need not offer a material at all.
        /// </summary>
        public int fixedStuff;
    }

    /// <summary>
    /// A material something can be built of: the bridge between what a colonist carries and what a
    /// building is made of.
    ///
    /// <para><b>This is the join that did not exist.</b> Wood and stone were <c>ItemDef</c>s —
    /// things on the floor with a stack limit — while walls were made of <c>CoreContent.Stuff*</c>
    /// values the generator stamped into <see cref="PlacedEdifice"/>. Nothing anywhere said that a
    /// pile of wood could become a wooden wall, which is why nothing could be built although both
    /// halves had been in the project for weeks.</para>
    /// </summary>
    public class StuffDef : Def
    {
        /// <summary>The <c>Stuff*</c> value a thing built of this carries. See <see cref="StuffHandle"/>.</summary>
        public ushort stuff;

        /// <summary>
        /// The <see cref="ItemHandle"/> a colonist carries it as, or -1 for a material that is not
        /// carried at all. See <see cref="ConstructionContent.IsBuildable"/>.
        /// </summary>
        public int item = -1;

        /// <summary>
        /// The material's effect on how long a thing takes to build, in thousandths.
        ///
        /// <para>Per-mille integers rather than floats, for the reason skill experience is stored
        /// in thousandths of a point: a factor multiplied into a tick count has to give the same
        /// answer on every machine, and integer arithmetic is the only way to promise that.</para>
        /// </summary>
        public int workFactorPerMille = 1000;

        /// <summary>
        /// A flat addition to the factored work, in ticks — the second term of U27's
        /// <c>stat = base × factor + offset</c> (a-04-building-and-materials.md section 3).
        ///
        /// <para>In the same unit as <see cref="BuildingDef.workToBuild"/>, deliberately, rather
        /// than another per-mille figure: it is added after the factor has already turned ticks
        /// into ticks, so a second fraction would just be a factor with extra steps. It stands for
        /// a cost the <em>factor</em> cannot express — a fixed dressing-and-fitting pass that a
        /// stone block wants and a plank does not, paid once per site whatever the building's own
        /// size, where the factor alone would only ever scale with it.</para>
        /// </summary>
        public int workOffsetTicks;

        /// <summary>
        /// The material's effect on how much punishment the finished thing takes, in thousandths.
        ///
        /// <para><b>Read since C6</b> (design 33 §13c): a struck building's pool is its
        /// <see cref="BuildingDef.maxHitPoints"/> times this, so a stone wall stands half as long
        /// again as a wooden one. <b>Factor-only, still</b>: an offset beside it would be a second
        /// number nobody has asked for, and U27's argument for the work offset — a flat cost the
        /// factor cannot express — has no counterpart here yet.</para>
        /// </summary>
        public int hitPointsFactorPerMille = 1000;

        /// <summary>
        /// How hard a <b>sharp</b> blow bites a thing built of this, in thousandths of the blow's
        /// rolled damage (design 33 §14d; the owner, 2026-09-24: blunt against stone, sharp against
        /// wood). Read by <c>BuildingTargets.DamageFactorPerMille</c> and nothing else.
        ///
        /// <para><b>On the material, not the building row</b>, because the owner's rule is wood
        /// against stone: a wooden door and a wooden wall answer a machete alike. Defaults to the
        /// blow as it comes, which is every material the rule has no opinion on — the ruined
        /// city's, left for now (§14a (b)).</para>
        /// </summary>
        public int sharpDamagePerMille = 1000;

        /// <summary>As <see cref="sharpDamagePerMille"/>, for a <b>blunt</b> blow — a bat, a crowbar or fists.</summary>
        public int bluntDamagePerMille = 1000;

        /// <summary>
        /// The material's effect on how much heat crosses a wall made of it, in thousandths of
        /// the standard material's conductance (design 28 §6).
        ///
        /// <para><b>The one place building material changes the weather indoors.</b> The
        /// reference's walls are all equally warm — a log cabin and a granite bunker hold heat
        /// identically — and our stuff table already exists to make material a decision, so it
        /// is a decision: wood insulates best of the buildables, stone is the standard the
        /// numbers are quoted against, and the city's concrete and steel bleed heat, which is
        /// the ruined city's problem and one day a salvage line's opportunity.</para>
        /// </summary>
        public int thermalConductancePerMille = 1_000;

        /// <summary>The registry key the interface names it by.</summary>
        public string iconKey = "";
    }

    /// <summary>
    /// The buildable tables, indexed by <see cref="BuildingHandle"/> and <see cref="StuffHandle"/>.
    ///
    /// <para>Static, like <see cref="NaturalContent"/> and <see cref="CoreContent"/>, and read by
    /// the job drivers exactly as <c>MineJobDriver</c> reads <c>NaturalContent.TerrainAt</c>. The
    /// XML under <c>Defs/Core/World/Buildings.xml</c> mirrors it and
    /// <c>ConstructionContentDefTests</c> holds the two together, which is the arrangement OQ-16
    /// settled for terrain.</para>
    /// </summary>
    public static class ConstructionContent
    {
        static readonly BuildingDef[] BuildingTable = BuildBuildings();
        static readonly StuffDef[] StuffTable = BuildStuffs();

        public static IReadOnlyList<BuildingDef> Buildings => BuildingTable;

        public static IReadOnlyList<StuffDef> Stuffs => StuffTable;

        public static BuildingDef BuildingAt(int handle) => BuildingTable[handle];

        public static StuffDef StuffAt(int handle) => StuffTable[handle];

        public static bool IsBuilding(int handle) =>
            handle > BuildingHandle.None && handle < BuildingTable.Length;

        /// <summary>
        /// Whether this material may be built with at all.
        ///
        /// <para>Concrete, steel and composite are what the ruined city is <i>made of</i>, not what
        /// a colony builds with: nothing produces them, no colonist can carry one, and offering
        /// them would be offering an order that can never be filled. They keep their stuff indices,
        /// because every stamped wall in the city carries one; they are simply not on the menu. A
        /// salvage line that turns rubble into steel is what would give one an item and put it
        /// there, and it would need no other change.</para>
        /// </summary>
        /// <summary>
        /// Is this slab kind one the colony laid, rather than one the generator stamped?
        ///
        /// <para>The question `PlacedEdifice.Built` answers for a wall, one level down. The
        /// generator's three — structural decks, plaza decks, roofs — belong to the ruined city and
        /// to whatever line of work claims ruins; a floor we built and paving we laid are ours to
        /// take up again. One place, so deconstruct's rule and its edit cannot come to disagree
        /// about which is which (U42 added the second kind).</para>
        /// </summary>
        public static bool IsOurs(ushort slab) =>
            slab == CoreContent.SlabBuilt || slab == CoreContent.SlabPaved;

        /// <summary>Which building a slab kind of ours came from, or <see cref="BuildingHandle.None"/>.</summary>
        public static int BuildingForSlab(ushort slab) =>
            slab == CoreContent.SlabBuilt ? BuildingHandle.Floor
            : slab == CoreContent.SlabPaved ? BuildingHandle.DeckPlate
            : BuildingHandle.None;

        public static bool IsBuildable(int handle) =>
            handle > StuffHandle.None && handle < StuffTable.Length && StuffTable[handle].item >= 0;

        /// <summary>What one site of this thing, in this material, costs in ticks of work.</summary>
        public static int WorkFor(int building, int stuff) => WorkFor(BuildingAt(building), StuffAt(stuff));

        /// <summary>
        /// As <see cref="WorkFor(int, int)"/>, on the defs directly rather than on handles into the
        /// shipped tables — the seam a test uses to exercise the formula on values of its own,
        /// without a fixture needing a place in <see cref="BuildingOrder"/> or
        /// <see cref="StuffOrder"/>.
        ///
        /// <para><c>stat = base × factor + offset</c> (U27, a-04 section 3): the factor is applied
        /// first, by integer division so it is the same number on every machine, and the offset is
        /// added to the result rather than folded into the multiplication — it is a flat cost the
        /// factor cannot express, not a second factor. The floor of one tick applies after both
        /// terms and is not defensive tidiness: a total small enough to round to nothing would make
        /// a site that can never be finished, because the driver compares work done against the
        /// total and would find it already met before the first swing — a colonist standing at a
        /// wall that never goes up, which is the exact shape of the hop bug that stopped mining
        /// dead.</para>
        /// </summary>
        public static int WorkFor(BuildingDef building, StuffDef stuff)
        {
            int work = building.workToBuild * stuff.workFactorPerMille / 1000 + stuff.workOffsetTicks;
            return work < 1 ? 1 : work;
        }

        /// <summary>
        /// Which building this edifice is, or <see cref="BuildingHandle.None"/>.
        ///
        /// <para>The reverse of <see cref="BuildingDef.edifice"/>, and it exists because a wall
        /// that is <i>standing</i> is a <c>PlacedEdifice</c> and has forgotten which row of this
        /// table raised it. Building never needed to ask; deconstruct does, because what it costs
        /// and what it gives back are both priced off that row. A linear walk over a table of two,
        /// called once when a job is given rather than per tick.</para>
        /// </summary>
        public static int BuildingForEdifice(ushort edifice)
        {
            for (int i = 1; i < BuildingTable.Length; i++)
                if (BuildingTable[i].edifice == edifice) return i;

            return BuildingHandle.None;
        }

        /// <summary>
        /// Which material this is, or <see cref="StuffHandle.None"/>. The reverse of
        /// <see cref="StuffDef.stuff"/>, and here for the same reason as
        /// <see cref="BuildingForEdifice"/>: a refund is paid in the material the thing was made
        /// of, and a standing building carries the raw value rather than the handle.
        /// </summary>
        public static int StuffForValue(ushort stuff)
        {
            for (int i = 1; i < StuffTable.Length; i++)
                if (StuffTable[i].stuff == stuff) return i;
            return StuffHandle.None;
        }

        /// <summary>
        /// What taking this thing apart costs in ticks.
        ///
        /// <para>Derived from what it took to build rather than given its own number, so a thing
        /// that is expensive to raise is slow to pull down without anyone maintaining two figures
        /// that have to agree. The clamp is the reference's (<c>a-04</c> §1): nothing is instant,
        /// and nothing takes a day. With one building in the game neither bound can be reached, and
        /// the shape is right before it can be reached rather than after.</para>
        /// </summary>
        public static int WorkToDeconstruct(int building, int stuff)
        {
            int work = WorkFor(building, stuff);
            if (work < MinDeconstructTicks) return MinDeconstructTicks;
            return work > MaxDeconstructTicks ? MaxDeconstructTicks : work;
        }

        public const int MinDeconstructTicks = 20;
        public const int MaxDeconstructTicks = 3000;

        /// <summary>See <c>PawnContent.Register</c>: the Def types this content is made of.</summary>
        public static DefLoader Register(DefLoader loader) =>
            QualityContent.Register(loader.Register<BuildingDef>().Register<StuffDef>());

        /// <summary>
        /// The handle order, which is the contract. A def's position here is a
        /// <see cref="BuildingHandle"/> value, written into the published frame and into every
        /// save, so this list — never the table's own sorted order — is what resolves a name.
        /// </summary>
        /// <summary>
        /// Does the thing standing as this edifice keep items out of its cells? Asked of an
        /// edifice id rather than a building handle, because a standing thing is a
        /// <c>PlacedEdifice</c> and the handle it was ordered from is not kept.
        /// </summary>
        /// <summary>
        /// How many stacks the thing standing as this edifice holds, or 0 where it is not a store.
        /// Asked by edifice id because that is what a cell carries.
        /// </summary>
        public static int SlotsOf(ushort edifice)
        {
            for (int i = 0; i < Buildings.Count; i++)
                if (Buildings[i].edifice == edifice && Buildings[i].storageSlots > 0)
                    return Buildings[i].storageSlots;
            return 0;
        }

        public static bool NeedsClearCell(ushort edifice)
        {
            for (int i = 0; i < Buildings.Count; i++)
                if (Buildings[i].edifice == edifice && Buildings[i].needsClearCell) return true;
            return false;
        }

        public static readonly string[] BuildingOrder =
        {
            "Building_None", "Building_Wall", "Building_Floor", "Building_DeckPlate", "Building_Ladder",
            "Building_Bed", "Building_Door", "Building_Shelf", "Building_Campfire",
            "Building_Conduit", "Building_Generator", "Building_Heater",
            "Building_Galley",
            // Cover (design 53 §4), BuildingHandle 13. The barricade that followed it was taken
            // out on the owner's first look (design 53 §13); if it comes back it takes the next
            // free handle, since 14 and 15 are the pillar and the stair.
            "Building_Sandbags",
            // Roofs and stairs (designs 59 and 60), BuildingHandle 14 and 15.
            "Building_Pillar", "Building_Stair",
        };

        /// <summary>As <see cref="BuildingOrder"/>, for <see cref="StuffHandle"/>.</summary>
        public static readonly string[] StuffOrder =
        {
            "Stuff_None", "Stuff_Concrete", "Stuff_Steel", "Stuff_Composite", "Stuff_Wood", "Stuff_Stone",
        };

        /// <summary>The whole buildable table, in handle order, read from a loaded pack.</summary>
        public static BuildingDef[] BuildingsFromDefs(DefDatabase defs)
        {
            var table = new BuildingDef[BuildingOrder.Length];
            for (int i = 0; i < BuildingOrder.Length; i++) table[i] = One<BuildingDef>(defs, BuildingOrder[i]);
            return table;
        }

        /// <summary>The whole material table, in handle order, read from a loaded pack.</summary>
        public static StuffDef[] StuffsFromDefs(DefDatabase defs)
        {
            var table = new StuffDef[StuffOrder.Length];
            for (int i = 0; i < StuffOrder.Length; i++) table[i] = One<StuffDef>(defs, StuffOrder[i]);
            return table;
        }

        static T One<T>(DefDatabase defs, string defName) where T : Def
        {
            if (!defs.HasTable<T>())
                throw new DefLoadException($"the content has no {typeof(T).Name} at all, and '{defName}' is required.");
            if (!defs.Table<T>().TryGetHandle(defName, out var handle))
                throw new DefLoadException($"the content has no {typeof(T).Name} named '{defName}'.");
            return defs.Table<T>()[handle];
        }

        static BuildingDef[] BuildBuildings()
        {
            return new[]
            {
                // 0 is "nothing", matching the grid's zero default, exactly as TerrainAir is 0.
                new BuildingDef { defName = "Building_None", label = "nothing", edifice = CoreContent.EdificeNone },

                new BuildingDef
                {
                    defName = "Building_Wall", label = "wall", edifice = CoreContent.EdificeWall,
                    blocking = true, costCount = 5, workToBuild = 135, minSkill = 0,
                    iconKey = "ui.arch.tool.wall", maxHitPoints = 300,
                },

                // A slab at the cell's lower boundary rather than an edifice in the cell (U29), and
                // `slab` is the whole of the difference: no edifice, nothing to walk into, and the
                // same order-deliver-work-raise pipeline that builds a wall. Less material and less
                // work than a wall, because a slab is less of both — neither number derives from
                // anything and nothing derives from them.
                new BuildingDef
                {
                    defName = "Building_Floor", label = "floor", edifice = CoreContent.EdificeNone,
                    slab = true, blocking = false, costCount = 4, workToBuild = 120, minSkill = 0,
                    iconKey = "ui.arch.tool.roof", maxHitPoints = 250,
                },

                // Paving: the same slab, laid on ground that is already there (U42). `covering` is
                // the one field that separates it from the floor above, and it inverts exactly one
                // question — this wants a cell that IS floored and never asks the support rule,
                // because the ground holds it up and it cannot fall. Cheapest and quickest in the
                // table: a surface carries no load and is laid over an area rather than a line.
                new BuildingDef
                {
                    defName = "Building_DeckPlate", label = "deck plate", edifice = CoreContent.EdificeNone,
                    slab = true, covering = true, blocking = false, costCount = 3, workToBuild = 60,
                    minSkill = 0, iconKey = "ui.arch.tool.deckplate", maxHitPoints = 150,
                },

                // The way up (U43). An edifice like a wall, and `blocking = false` is what makes it
                // one you can stand in: a ladder you cannot enter is a decoration. The connector
                // that actually joins the two layers is registered by ConstructionGrid.Raise,
                // because a NavGraph is not something a content table can reach.
                new BuildingDef
                {
                    defName = "Building_Ladder", label = "ladder", edifice = CoreContent.EdificeLadder,
                    blocking = false, rotates = true, costCount = 4, workToBuild = 90, minSkill = 0,
                    iconKey = "ui.arch.tool.ladder", maxHitPoints = 80,
                },

                // The first furniture (docs/design/20-beds.md). Two cells, passable, rotatable at
                // the ghost, finished at a rolled quality — the one consumer of takesQuality so
                // far, and the thing that finally gives U26's outstanding success roll something
                // to land on. 5 wood like a wall: it is a frame and a pad, not a fortress. Work
                // 180 over the wall's 135 because joinery is fussier than stacking, but still well
                // under felling's 800 — a bed is an evening, not a day.
                new BuildingDef
                {
                    defName = "Building_Bed", label = "bed", edifice = CoreContent.EdificeBed,
                    blocking = false, footprint = 2, rotates = true, takesQuality = true,
                    needsClearCell = true, costCount = 5, workToBuild = 180, minSkill = 0,
                    iconKey = "ui.arch.tool.bed", maxHitPoints = 120, coverPerMille = 300,
                },

                // The door. Edifice 2 is CoreContent.EdificeDoor. Passable, takes no quality,
                // matching wall costs (5 stuff, 135 ticks).
                new BuildingDef
                {
                    defName = "Building_Door", label = "door", edifice = CoreContent.EdificeDoor,
                    blocking = false, rotates = true, costCount = 5, workToBuild = 135, minSkill = 0,
                    iconKey = "ui.arch.tool.door", maxHitPoints = 160,
                },

                // The shelf: one cell of furniture that holds an inventory rather than standing in
                // the way of one (docs/design/26-storage.md). Passable like the bed, because a
                // blocking shelf is a wall a player built by accident and every placement would be
                // the 1.19 ms one-cell NavGraph.Rebuild the baseline audit measured. needsClearCell
                // because the cell it stands in stops taking loose stacks the moment it is raised:
                // what is at a shelf's cell is in the shelf. The bed's cost and work exactly — a
                // shelf is joinery of the same order, and eight stacks for five wood is a trade a
                // player can see the point of without it ending the storage game.
                new BuildingDef
                {
                    defName = "Building_Shelf", label = "shelf", edifice = CoreContent.EdificeShelf,
                    blocking = false, rotates = true, needsClearCell = true, storageSlots = 8,
                    costCount = 5, workToBuild = 180, minSkill = 0, iconKey = "ui.arch.tool.shelf",
                    maxHitPoints = 100, coverPerMille = 500,
                },

                // The first heat source (design 28 §7). Edifice 13, the next free id after the
                // bed's. Blocking — nobody stands in a fire — and wanting a clear cell like the
                // bed does, for the same reason with worse graphics. heatPerPass 1200 holds a
                // 6×6 room comfortably above deepest Rime and overshoots in Wash, which is the
                // brazier-in-a-broom-cupboard lesson arriving for free. 3 stuff and 60 ticks:
                // kindling and a ring of stones. Fuel is a recorded hook — v1 burns steadily.
                new BuildingDef
                {
                    defName = "Building_Campfire", label = "campfire", edifice = CoreContent.EdificeCampfire,
                    blocking = true, needsClearCell = true, heatPerPass = 1_200, radiantC = 2_600,
                    costCount = 3, workToBuild = 60, minSkill = 0,
                    iconKey = "ui.arch.tool.campfire", maxHitPoints = 60, coverPerMille = 250,
                },

                // A power line (design 32 §3, §14). Not an edifice — `conduit` sends the order to
                // the power grid and the line into a layer of its own — and all part: no material
                // to choose, one scrap metal a cell, fetched and spent by the colonist who lays it.
                // 40 ticks of work, a-07's 35 rounded to the table's tens.
                new BuildingDef
                {
                    defName = "Building_Conduit", label = "conduit", edifice = CoreContent.EdificeNone,
                    conduit = true, blocking = false, costCount = 0,
                    partItem = ItemHandle.Salvage, partCount = 1, workToBuild = 40, minSkill = 0,
                    iconKey = "ui.arch.tool.conduit", maxHitPoints = 40,
                },

                // The wood-fired generator (design 32 §6). a-07's output, hopper and full-load burn
                // — 1,000 W, 75 wood (exactly one stack, so one trip fills it), 22 a day — with the
                // burn in proportion to load, which is the owner's departure. Two cells because the
                // footprint allows no more; blocking and wanting a clear cell like the campfire.
                // heatPerPass is the heat at full load and scales with the load like the burn: the
                // power grid owns it, never the thermal pass's per-def table. 30 stuff and 600
                // ticks: the first expensive thing in the table.
                new BuildingDef
                {
                    defName = "Building_Generator", label = "generator", edifice = CoreContent.EdificeGenerator,
                    blocking = true, footprint = 2, rotates = true, needsClearCell = true,
                    powerOutputW = 1_000, fuelItem = ItemHandle.Wood, fuelCapacity = 75, fuelPerDay = 22,
                    heatPerPass = 400, costCount = 30, partItem = ItemHandle.Salvage, partCount = 20,
                    workToBuild = 600, minSkill = 0,
                    iconKey = "ui.arch.tool.generator", maxHitPoints = 300, coverPerMille = 500,
                },

                // The electric heater (design 32 §7): a-07's 175 W, and 1,000 heat a pass into its
                // room only while powered and switched on — the campfire's shape behind a gate.
                // One cell, blocking, wanting a clear cell; 10 stuff and 240 ticks. It rotates
                // (§14c): the facing is drawing only, and backs on to a wall where there is one.
                new BuildingDef
                {
                    defName = "Building_Heater", label = "heater", edifice = CoreContent.EdificeHeater,
                    blocking = true, rotates = true, needsClearCell = true, powerDrawW = 175, heatPerPass = 1_000,
                    costCount = 10, partItem = ItemHandle.Salvage, partCount = 5,
                    workToBuild = 240, minSkill = 0,
                    iconKey = "ui.arch.tool.heater", maxHitPoints = 100, coverPerMille = 400,
                },

                // The galley (design 48 §5): the electric cooker. a-18's 350 W, drawn whenever it is
                // switched on, as the heater's is. One cell, blocking, rotating, wanting a clear
                // cell; 15 stuff, 10 scrap and 300 ticks, the heater a little heavier.
                new BuildingDef
                {
                    defName = "Building_Galley", label = "galley", edifice = CoreContent.EdificeGalley,
                    blocking = true, rotates = true, needsClearCell = true, powerDrawW = 350,
                    costCount = 15, partItem = ItemHandle.Salvage, partCount = 10,
                    workToBuild = 300, minSkill = 0,
                    iconKey = "ui.arch.tool.galley", maxHitPoints = 100, coverPerMille = 500,
                },

                // Sandbags (design 53 §4): the cheap, quick cover. One cell, dragged as a line like a
                // wall, crossed at +150 and never stood on, 55 % low cover. Always stone — five, the
                // placeholder the owner asked for until the recipes are decided — so the palette
                // offers no material. The shelf's work; a quarter left behind when fighting destroys
                // one. All INVENTED but the 55, which is the reference's.
                new BuildingDef
                {
                    defName = "Building_Sandbags", label = "sandbags", edifice = CoreContent.EdificeSandbags,
                    blocking = false, needsClearCell = true, passThrough = true, crossCost = 150,
                    costCount = 5, fixedStuff = StuffHandle.Stone, workToBuild = 180, minSkill = 0,
                    iconKey = "ui.arch.tool.sandbag", maxHitPoints = 300, coverPerMille = 550,
                    wreckRefundPerMille = 250,
                },

                // The support pillar (RF1, docs/design/59-roofs.md §5). A column in one cell that
                // holds up the slab above it, and the thing that makes a hall roofable: measured,
                // a room with an 8-cell interior takes nine holes in its roof and a 10-cell one
                // takes twenty-five, because support decays one per cell from a wall and a slab
                // stands at most three cells from anything holding it up.
                //
                // `edifice` is CoreContent.EdificePillar, which has existed since worldgen stamped
                // its first colonnade. The support solver needs NO change at all: IsGrounded ends
                // at `Edifice[below] >= 0`, so a pillar has grounded the slab over it for as long
                // as the solver has run - there was simply no way to build one.
                //
                // `blocking` true, and it is a decision rather than a detail. A pillar fills its
                // 2.5 m cell, so span is paid for in the floor it stands on; you cannot walk
                // through a column. A non-blocking pillar would make a pillared hall strictly
                // better than an unpillared one and the choice free. The solver reads
                // `Edifice[below] >= 0` and not the flag, so either would hold the roof up.
                //
                // 3 and 90 against a wall's 5 and 135 and a slab's 4 and 120: less material than a
                // 3 m wall panel, dearer per cell than a slab. Neither number is derived from
                // anything and nothing derives from them; they are the owner's to tune.
                new BuildingDef
                {
                    defName = "Building_Pillar", label = "support pillar",
                    edifice = CoreContent.EdificePillar, blocking = true,
                    costCount = 3, workToBuild = 90, minSkill = 0,
                    // INVENTED on merging combat (design 33 §4): two thirds of a wall's, since it is
                    // a column rather than a panel. No coverPerMille: it fills its cell, so its
                    // cover is the full-fill rule's, as a wall's is.
                    iconKey = "ui.arch.tool.pillar", maxHitPoints = 200,
                },

                // The way up that carries something (docs/design/60-stairs.md). ONE cell, climbing
                // a full 3.0 m layer to the floor above - owner, 2026-09-21, after playing the
                // two-cell version: "It should be able to go up a flight in one square for ease -
                // but it's not - it's not flush with the floor above either."
                //
                // One edifice value of its own, EdificeStairFull, and NOT worldgen's Lower/Upper
                // pair. The pair stays exactly as it is for the stamped city; this is the thing a
                // colonist builds and it is a different thing. What that buys: footprint 1, so
                // every second-cell path in Place, Raise and Demolish falls out on SecondCell ==
                // -1; a shaft rule about one cell above instead of two; and the ladder's own
                // one-cell connector.
                //
                // blocking false for the ladder's reason exactly: a stair you cannot enter is a
                // decoration. 6 and 150 against a wall's 5 and 135 and a ladder's 4 and 90 - a
                // flight of carpentry, and the thing a colony saves up for rather than knocks
                // together. The numbers are unchanged from the two-cell version deliberately: a
                // stair still costs a stair, and neither number derives from anything nor has
                // anything derive from it. They are the owner's to tune.
                new BuildingDef
                {
                    defName = "Building_Stair", label = "stair",
                    edifice = CoreContent.EdificeStairFull,
                    rotates = true, blocking = false,
                    costCount = 6, workToBuild = 150, minSkill = 0,
                    // INVENTED on merging combat (design 33 §4): sturdier than a ladder's 80, a bed's
                    // worth. No cover: it is stood on, not behind.
                    iconKey = "ui.arch.tool.stair", maxHitPoints = 120,
                },
            };
        }

        static StuffDef[] BuildStuffs()
        {
            return new[]
            {
                new StuffDef { defName = "Stuff_None", label = "nothing", stuff = CoreContent.StuffNone },
                new StuffDef { defName = "Stuff_Concrete", label = "concrete", stuff = CoreContent.StuffConcrete, thermalConductancePerMille = 1100 },
                new StuffDef { defName = "Stuff_Steel", label = "steel", stuff = CoreContent.StuffSteel, thermalConductancePerMille = 1400 },
                new StuffDef { defName = "Stuff_Composite", label = "composite", stuff = CoreContent.StuffComposite, thermalConductancePerMille = 800 },

                // Wood carries no offset: nailing and lashing a plank into place has no separate
                // fitting step for the factor to leave out, so the whole of wood's cost is the
                // factor (U27; a-04's own wood row carries no offset either).
                new StuffDef
                {
                    defName = "Stuff_Wood", label = "wood", stuff = NaturalContent.StuffWood,
                    item = ItemHandle.Wood, workFactorPerMille = 1000, workOffsetTicks = 0,
                    hitPointsFactorPerMille = 1000, iconKey = "ui.res.wood",
                    thermalConductancePerMille = 600,
                    // Design 33 §14d, INVENTED: an edge bites wood, a club does no better on it
                    // than on anything else.
                    sharpDamagePerMille = 1250, bluntDamagePerMille = 1000,
                },

                // 1.7x the work and 1.5x the hit points: the reference's own relation between a
                // wooden wall and a stone one (a-04 section 3, read off its material table). It is
                // the one number that makes the choice of material a decision rather than a colour,
                // which is why it is carried in ahead of the rest of the stat block.
                //
                // workOffsetTicks 15 (U27) is Odyssey's own number, not the reference's: a flat
                // dressing-and-fitting pass a stone block wants regardless of the wall it joins,
                // on top of the 1.7x per-unit factor. The reference's own stone rows carry a much
                // larger offset (+140 against a base of 135 — roughly the whole of the base again)
                // because it is stacked with a x5-6 factor across many more stone materials; 15
                // ticks is proportionate to the one factor Odyssey chose (a tenth of the wall's own
                // base, about a tenth of the factored total) rather than copied from it.
                new StuffDef
                {
                    defName = "Stuff_Stone", label = "stone", stuff = NaturalContent.StuffStone,
                    item = ItemHandle.Stone, workFactorPerMille = 1700, workOffsetTicks = 15,
                    hitPointsFactorPerMille = 1500, iconKey = "ui.res.stone",
                    thermalConductancePerMille = 1000,
                    // Design 33 §14d, INVENTED: an edge turns on stone, a club breaks it.
                    sharpDamagePerMille = 500, bluntDamagePerMille = 1250,
                },
            };
        }
    }
}
