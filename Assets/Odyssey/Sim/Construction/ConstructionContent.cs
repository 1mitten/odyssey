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

        /// <summary>Construction level a colonist needs before it may take the job. 0 for a wall.</summary>
        public int minSkill;

        /// <summary>The registry key the interface names it by. Never a label, never a filename.</summary>
        public string iconKey = "";
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

        /// <summary>The material's effect on how much punishment the finished thing takes, in thousandths.</summary>
        public int hitPointsFactorPerMille = 1000;

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
        public static bool IsBuildable(int handle) =>
            handle > StuffHandle.None && handle < StuffTable.Length && StuffTable[handle].item >= 0;

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

        /// <summary>
        /// What one site of this thing, in this material, costs in ticks of work.
        ///
        /// <para>Integer division, so it is the same number on every machine. The floor of one tick
        /// is not defensive tidiness: a factor small enough to round to nothing would make a site
        /// that can never be finished, because the driver compares work done against the total and
        /// would find it already met before the first swing — a colonist standing at a wall that
        /// never goes up, which is the exact shape of the hop bug that stopped mining dead.</para>
        /// </summary>
        public static int WorkFor(int building, int stuff)
        {
            int work = BuildingAt(building).workToBuild * StuffAt(stuff).workFactorPerMille / 1000;
            return work < 1 ? 1 : work;
        }

        /// <summary>See <c>PawnContent.Register</c>: the Def types this content is made of.</summary>
        public static DefLoader Register(DefLoader loader) =>
            loader.Register<BuildingDef>().Register<StuffDef>();

        /// <summary>
        /// The handle order, which is the contract. A def's position here is a
        /// <see cref="BuildingHandle"/> value, written into the published frame and into every
        /// save, so this list — never the table's own sorted order — is what resolves a name.
        /// </summary>
        public static readonly string[] BuildingOrder = { "Building_None", "Building_Wall" };

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
                    iconKey = "ui.arch.tool.wall",
                },
            };
        }

        static StuffDef[] BuildStuffs()
        {
            return new[]
            {
                new StuffDef { defName = "Stuff_None", label = "nothing", stuff = CoreContent.StuffNone },
                new StuffDef { defName = "Stuff_Concrete", label = "concrete", stuff = CoreContent.StuffConcrete },
                new StuffDef { defName = "Stuff_Steel", label = "steel", stuff = CoreContent.StuffSteel },
                new StuffDef { defName = "Stuff_Composite", label = "composite", stuff = CoreContent.StuffComposite },

                new StuffDef
                {
                    defName = "Stuff_Wood", label = "wood", stuff = NaturalContent.StuffWood,
                    item = ItemHandle.Wood, workFactorPerMille = 1000, hitPointsFactorPerMille = 1000,
                    iconKey = "ui.res.wood",
                },

                // 1.7x the work and 1.5x the hit points: the reference's own relation between a
                // wooden wall and a stone one (a-04 section 3, read off its material table). It is
                // the one number that makes the choice of material a decision rather than a colour,
                // which is why it is carried in ahead of the rest of the stat block.
                new StuffDef
                {
                    defName = "Stuff_Stone", label = "stone", stuff = NaturalContent.StuffStone,
                    item = ItemHandle.Stone, workFactorPerMille = 1700, hitPointsFactorPerMille = 1500,
                    iconKey = "ui.res.stone",
                },
            };
        }
    }
}
