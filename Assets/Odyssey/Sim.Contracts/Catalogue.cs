#nullable enable
namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// Def-index constants that published views carry. <see cref="PawnView.JobDef"/> and
    /// <see cref="ThingView.DefIndex"/> are indices into the simulation's job and item tables,
    /// and an index is only a contract if both sides agree what it counts. These constants are
    /// that agreement: the simulation's tables are defined in this order, and interface code
    /// (which may not reference the simulation at all) turns the indices into words against
    /// these numbers and no others.
    /// </summary>
    public static class JobHandle
    {
        public const int Haul = 0;
        public const int Eat = 1;
        public const int Sleep = 2;
        public const int Wander = 3;
        public const int Wait = 4;
        public const int Fell = 5;
        public const int Mine = 6;

        /// <summary>Carry a load of material to a building site that is waiting for it.</summary>
        public const int Deliver = 7;

        /// <summary>Work at a site that has its materials, until the thing stands.</summary>
        public const int Build = 8;

        /// <summary>Take one of our own buildings apart, for half of what it cost.</summary>
        public const int Deconstruct = 9;

        /// <summary>Break the ground of an unplanted zone cell and put a seed in it.</summary>
        public const int Sow = 10;

        /// <summary>Cut a ripe crop and gather what it yields.</summary>
        public const int Harvest = 11;

        /// <summary>A drafted colonist standing where it was put (design 33 §2c).</summary>
        public const int DraftHold = 12;

        /// <summary>A drafted colonist walking to the cell the player named (design 33 §2c).</summary>
        public const int Goto = 13;

        /// <summary>Fetch one wood and lay an ordered power line with it (design 32 §3).</summary>
        public const int LayConduit = 14;

        /// <summary>Take up a power line marked for removal.</summary>
        public const int RemoveConduit = 15;

        /// <summary>Carry fuel to a generator below half and fill it (design 32 §6).</summary>
        public const int Refuel = 16;

        // The combat line's five (design 33 §5), claimed together by the contracts step so that
        // no two lanes could each append a job and both call it 14. **Renumbered 14-18 -> 17-21 at
        // the merge with main (2026-09-24)**: power's three shipped first and a shipped handle is a
        // save contract, so the unshipped ones moved. Saves taken on the combat branches before this
        // merge do not load; no save from main is affected.

        /// <summary>Close on a target and swing at it until one of the two goes down (C2).</summary>
        public const int AttackMelee = 17;

        /// <summary>Run from whatever hurt it (C2): an animal that did not turn on its attacker.</summary>
        public const int Flee = 18;

        /// <summary>Lying where it fell, at nought hit points or less, until healed, rescued or dead (C2).</summary>
        public const int Downed = 19;

        /// <summary>Walk to a weapon and take it into the hand (C3).</summary>
        public const int Equip = 20;

        /// <summary>Carry a downed colonist to a bed (C4).</summary>
        public const int Rescue = 21;

        public const int Count = 22;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: plant species, as the zone and plant channels carry them.
    /// <para>Like every table here it is append-only: a zone record stores its plant as one of
    /// these numbers, so the order a save depends on never shifts.</para>
    /// </summary>
    public static class PlantHandle
    {
        public const int Carrot = 0;
        public const int Count = 1;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: work-type indices, as <see cref="IntentKind.SetWorkPriority"/>
    /// carries them.
    ///
    /// <para><b>Why this had to exist before the Work tab could emit anything.</b> Skills reach
    /// the interface as named pawn aspects and never cross this assembly at all, which is the
    /// point of that mechanism and is why <c>SkillIndex</c> is deliberately <i>not</i> mirrored
    /// here. A work priority cannot do the same: it is <em>written</em> as well as read, and an
    /// <see cref="Intent"/> carries three integers and no strings. Without an agreed index the
    /// interface would have to send its own column number and the simulation would have to know
    /// the order of a list that lives in the HUD — which is the coupling the aspect seam was built
    /// to avoid, arriving through the other door.</para>
    ///
    /// <para>The simulation's <c>WorkTypeIndex</c> aliases these, exactly as <c>JobIndex</c>
    /// aliases <see cref="JobHandle"/>, so there is one order and it is written down here.</para>
    /// </summary>
    public static class WorkHandle
    {
        public const int Haul = 0;
        public const int Cutting = 1;
        public const int Mining = 2;

        /// <summary>Carrying material to a building site, and working at one. Both.</summary>
        public const int Construction = 3;

        /// <summary>
        /// Breaking ground in a growing zone and cutting what ripens there — one work type for
        /// both ends of the crop. Arrived with the growing zones on 2026-09-20 and took the next
        /// number; the Work tab drew it as <i>not built yet</i> until that landed.
        /// </summary>
        public const int Growing = 4;

        /// <summary>
        /// Carrying a downed colonist to a bed (design 33 §4, C4). Claimed by the combat
        /// contracts step with the rest of the line's handles; its giver is an emergency one and
        /// answers no until C4 fills it.
        /// </summary>
        public const int Rescue = 5;

        public const int Count = 6;

        /// <summary>What a work type the simulation does not run answers to. Never sent.</summary>
        public const int None = -1;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: what a colonist is told to be doing in one hour of the day, as
    /// <see cref="IntentKind.SetScheduleBlock"/> carries it.
    ///
    /// <para><b>Six, and <see cref="Anything"/> is the one that means "no instruction".</b> It is
    /// deliberately index 0 so that a zeroed array is a colonist nobody has scheduled, and so that
    /// the day the job system reads this, an unscheduled hour behaves exactly as today.</para>
    ///
    /// <para><b>Nothing reads these yet</b> (design 27 §12). The schedule is authored, saved,
    /// published and editable; the hour a colonist sleeps is still decided by their rest need. The
    /// unit that makes the job system obey this is the one that puts the schedule into the state
    /// hash and re-bakes the goldens.</para>
    /// </summary>
    public static class ScheduleHandle
    {
        /// <summary>No instruction. Work, rest or idle as needs dictate — today's behaviour.</summary>
        public const int Anything = 0;

        public const int Work = 1;
        public const int Sleep = 2;
        public const int Recreation = 3;
        public const int Eat = 4;

        /// <summary>Quiet hours. Named now because the grid draws six colours; what it will mean
        /// is not this unit's business.</summary>
        public const int Meditate = 5;

        public const int Count = 6;

        /// <summary>Hours in a scheduled day, which is the clock's own <c>HoursPerDay</c>.</summary>
        public const int Hours = 24;
    }

    /// <summary>See <see cref="JobHandle"/>: item def indices as <see cref="ThingView"/> carries them.</summary>
    public static class ItemHandle
    {
        public const int Meal = 0;
        public const int Salvage = 1;
        public const int Wood = 2;

        /// <summary>Broken rock. Plentiful, heavy, and not yet good for anything but a pile.</summary>
        public const int Stone = 3;

        public const int IronOre = 4;
        public const int Coal = 5;

        /// <summary>
        /// The first raw food. Eaten straight from the field or the pile — the eater scans for
        /// nutrition and does not care that it is not a meal (docs/design/22-growing.md §5).
        /// Six, not two: the item table grew five commodities before the carrot existed, and
        /// handle order is the save contract.
        /// </summary>
        public const int Carrots = 6;

        // The four melee weapons (design 33 §1, C3), appended together by the combat contracts
        // step. Real items: one to a stack, category Weapons, each with a weapon block in its Def.

        /// <summary>Blunt, and a chance to stun.</summary>
        public const int Bat = 7;

        /// <summary>Blunt, heavier, and a better chance to stun.</summary>
        public const int Crowbar = 8;

        /// <summary>Sharp and quick.</summary>
        public const int Machete = 9;

        /// <summary>Sharp, and the best thing a colonist can hold.</summary>
        public const int ArcBlade = 10;

        public const int Count = 11;
    }

    /// <summary>
    /// What kind of thing a commodity is, as <c>ItemDef.category</c> declares it and a storage
    /// filter groups by. The owner's six, in the owner's order (2026-09-20).
    ///
    /// <para>An enum rather than a handle table, unlike everything else in this file, because it
    /// is the one of these a <b>Def declares by name</b> — the loader parses an enum from its
    /// spelling, so the XML reads <c>&lt;category&gt;Food&lt;/category&gt;</c> instead of a number
    /// nobody can check. The handle tables next to it are indices into content that content itself
    /// defines; this is a fixed set the code knows.</para>
    ///
    /// <para><b>Four of them have no members yet</b>, and that is on purpose rather than an
    /// oversight: the names are what the wiki prints and what a filter row says, and a category
    /// that arrives with its first commodity is a content commit that also has to touch the
    /// registry, the CSV and the wiki. They are cheaper here, empty, than added one at a time.
    /// What is deliberately <em>not</em> here because of it is the tri-state category tree — a
    /// roll-up over four empty branches compresses nothing (docs/plans/storage.md decisions 23,
    /// 30 and 31).</para>
    ///
    /// <para>Order is the filter's row order and the wiki's, not a save contract in itself — a
    /// zone's filter is stored per item def, never per category — but it is pinned by
    /// <c>ItemCategoryTests</c> all the same, because the wiki prints it.</para>
    /// </summary>
    public enum ItemCategory : byte
    {
        /// <summary>Anything a colonist can eat, cooked or raw.</summary>
        Food = 0,

        /// <summary>What tending draws on.</summary>
        Medicine = 1,

        /// <summary>What things are made of, and what mining and felling leave behind.</summary>
        Materials = 2,

        /// <summary>Research obtained elsewhere rather than produced (owner, 2026-09-20).</summary>
        Books = 3,

        /// <summary>Made things that are carried, worn or used, and are not weapons.</summary>
        Items = 4,

        Weapons = 5,
    }

    /// <summary>How many <see cref="ItemCategory"/> values there are. One place says so, and a test holds it to the enum.</summary>
    public static class ItemCategories
    {
        public const int Count = 6;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: incident def indices, as <see cref="BulletinView"/> and
    /// <see cref="Intent"/> carry them. The order is <c>IncidentContent.Order</c> in the
    /// simulation and <c>IncidentLabels.Keys</c> in the interface, and a test on each side holds
    /// its list to this count.
    /// </summary>
    public static class IncidentHandle
    {
        /// <summary>A stack of meals falling out of the sky on to whatever is under it.</summary>
        public const int SupplyDrop = 0;

        /// <summary>A stack of scrap metal falling out of the sky (design 32 §14): the supply drop's worker, another cargo.</summary>
        public const int ScrapDrop = 1;

        public const int Count = 2;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: terrain kinds, as <see cref="CellDetail"/> carries them.
    ///
    /// <para>The order is <c>WorldContent.TerrainOrder</c> — the one list that decides which name
    /// is which index, and the one a save and a state hash already depend on. These constants are
    /// that order restated here so the interface can turn an index into a word without referencing
    /// the simulation, exactly as <see cref="JobHandle"/> does for jobs. A Sim-side test holds the
    /// two tables to each other, because nothing in either assembly can.</para>
    /// </summary>
    public static class TerrainHandle
    {
        public const int Air = 0;
        public const int Pavement = 1;
        public const int CrackedPavement = 2;
        public const int Rubble = 3;
        public const int Soil = 4;
        public const int Gravel = 5;
        public const int EngineeredFill = 6;
        public const int Rock = 7;
        public const int BuriedSeam = 8;
        public const int Salvage = 9;

        /// <summary>The wilderness half, continuing the city's numbering.</summary>
        public const int Grass = 10;
        public const int BareEarth = 11;
        public const int PackedGravel = 12;
        public const int Sand = 13;
        public const int Subsoil = 14;
        public const int Bedrock = 15;
        public const int IronOre = 16;
        public const int CoalSeam = 17;
        public const int ShallowWater = 18;
        public const int DeepWater = 19;
        public const int Marsh = 20;

        public const int Count = 21;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: what can stand in a cell, as <see cref="CellDetail"/> carries
    /// it. The order is the simulation's own edifice numbering — <c>CoreContent</c>'s ids 0 to 9,
    /// the trees continuing from 10 — restated here for the interface to name.
    /// </summary>
    public static class EdificeHandle
    {
        public const int None = 0;
        public const int Wall = 1;
        public const int Door = 2;
        public const int Window = 3;
        public const int Pillar = 4;
        public const int StairLower = 5;
        public const int StairUpper = 6;
        public const int Ladder = 7;
        public const int VaultWall = 8;
        public const int UtilityTap = 9;

        public const int TreeConifer = 10;
        public const int TreeBroadleaf = 11;

        /// <summary>
        /// The bed, and the first edifice id the interface names that no generator stamps: 12,
        /// after the trees' ten and eleven. See <c>CoreContent.EdificeBed</c> for why it is a
        /// literal and not an offset.
        /// </summary>
        public const int Bed = 12;

        /// <summary>
        /// The shelf: the second edifice the interface names that no generator stamps, and the
        /// colony's first buildable store. See <c>CoreContent.EdificeShelf</c>.
        /// </summary>
        public const int Shelf = 13;

        /// <summary>The campfire, the third id the interface names that no generator stamps:
        /// 14, after the shelf, and like it the reason <c>CoreContent.EdificeCampfire</c> spells
        /// the literal beside the ones it must not collide with (design 28 §7).</summary>
        public const int Campfire = 14;

        /// <summary>The wood-fired generator (design 32 §6): two cells, the first thing that makes
        /// power. 15, after the campfire; see <c>CoreContent.EdificeGenerator</c>.</summary>
        public const int Generator = 15;

        /// <summary>The electric heater (design 32 §7): one cell, the first thing that spends
        /// power. See <c>CoreContent.EdificeHeater</c>.</summary>
        public const int Heater = 16;

        public const int Count = 17;
    }

    /// <summary>
    /// See <see cref="JobHandle"/>: buildable things, as <c>SiteView</c> and the
    /// <c>PlaceBuilding</c> intent carry them. 0 is "nothing", matching the grid default.
    /// </summary>
    public static class BuildingHandle
    {
        public const int None = 0;
        public const int Wall = 1;

        /// <summary>
        /// A slab at the cell's lower boundary: the floor you stand on and the roof of whatever is
        /// beneath it. One thing, of a material — see docs/design/17-floors-and-collapse.md.
        /// </summary>
        public const int Floor = 2;

        /// <summary>
        /// A floor covering laid on ground that is already there (U42). A slab like
        /// <see cref="Floor"/>, and the opposite of it about what must be underneath: this one
        /// wants a floor already and never asks the support rule, because it cannot fall.
        /// </summary>
        public const int DeckPlate = 3;

        /// <summary>
        /// A ladder: the first thing a colony can build that goes <b>up</b>. One cell, joining the
        /// floor it stands on to the floor directly above it.
        ///
        /// <para>Until U43 a second storey was decorative — measured, every slab came back
        /// walkable and unreachable — because vertical movement goes through a
        /// <c>Pathing.Connector</c> and connectors only ever came from worldgen.</para>
        /// </summary>
        public const int Ladder = 4;

        /// <summary>
        /// The first furniture: two cells, passable, rotatable, finished at a rolled quality
        /// (docs/design/20-beds.md).
        ///
        /// <para><b>Five, not two.</b> The bed was written against a table that ended at the wall
        /// and took the next number; U29's floor, U42's paving and U43's ladder reached main first
        /// and took 2, 3 and 4. Handle order is the save contract and positions are append-only,
        /// so the later branch is the one that moves — which is only safe because no save written
        /// with a bed in it has ever left this branch.</para>
        /// </summary>
        public const int Bed = 5;
        public const int Door = 6;

        /// <summary>
        /// A shelf: one cell of furniture that holds an inventory rather than standing in the way
        /// of one (docs/design/26-storage.md, the S2 branch of the storage line).
        ///
        /// <para><b>The colony's first buildable store.</b> A stockpile is painted and a shelf is
        /// raised, and the difference underneath is where the things go: a zone leaves them on the
        /// floor one stack to a cell, while a shelf holds eight stacks in an inventory of its own.
        /// The ground's one-stack-per-cell rule is therefore never touched — six write paths throw
        /// on a second stack and all six are left alone.</para>
        ///
        /// <para>Seven because the door reached main first and took six. Handle order is the save
        /// contract and positions are append-only.</para>
        /// </summary>
        public const int Shelf = 7;

        /// <summary>The first heat source (design 28 §7): one cell, blocking, and the one
        /// building whose <c>heatPerPass</c> is not zero.
        ///
        /// <para>Eight because the shelf reached main first and took seven, the same
        /// rule the shelf's own note records against the door. Handle order is the save
        /// contract and positions are append-only.</para>
        /// </summary>
        public const int Campfire = 8;

        /// <summary>
        /// A power line (design 32 §3). <b>Not an edifice</b>: a line lives in its own per-cell
        /// layer, so it can run through a wall or under a floor, and the order for one is handed
        /// to the power grid rather than taking a construction site of its own. It is a building
        /// handle all the same because it is armed, ghosted, dragged and ordered exactly as a wall
        /// is — one intent, one cursor, one palette row.
        /// </summary>
        public const int Conduit = 9;

        /// <summary>The wood-fired generator (design 32 §6).</summary>
        public const int Generator = 10;

        /// <summary>The electric heater (design 32 §7).</summary>
        public const int Heater = 11;

        public const int Count = 12;
    }

    /// <summary>
    /// The tier a quality-bearing thing finished at, as <see cref="Pawns.PlacedEdifice.Quality"/>
    /// carries it. 0 is "takes no quality at all" — every wall, for ever. The five names are the
    /// owner's (docs/design/20-beds.md §2).
    /// </summary>
    public static class QualityHandle
    {
        public const int None = 0;
        public const int Poor = 1;
        public const int Normal = 2;
        public const int Decent = 3;
        public const int Uber = 4;
        public const int Epic = 5;
        public const int Count = 6;
    }

    /// <summary>
    /// What a built thing is made of. These are <c>CoreContent.Stuff*</c> values: the same
    /// numbers the ruined city stamps into <c>PlacedEdifice.Stuff</c>, so a wall a colonist
    /// builds and a wall the generator laid are the same kind of record.
    /// </summary>
    public static class StuffHandle
    {
        public const int None = 0;
        public const int Concrete = 1;
        public const int Steel = 2;
        public const int Composite = 3;
        public const int Wood = 4;
        public const int Stone = 5;
        public const int Count = 6;
    }
}
