#nullable enable
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Growing
{
    /// <summary>
    /// A crop the colony can grow: what it needs, how long it takes, and what it is worth.
    ///
    /// <para>World content like a <c>TerrainDef</c> or an <c>ItemDef</c>, and for the same
    /// reason: a zone record carries its plant as a handle, so the names live in XML and the
    /// numbers live beside them (docs/design/22-growing.md §2). The yield names an
    /// <see cref="ItemDef"/> rather than embedding nutrition or a stack size, so the carrot the
    /// planter grows and the carrot the eater eats are provably the same commodity —
    /// <c>[DefReference]</c> proves the name at load, where a typo would otherwise surface as a
    /// harvest that spawned nothing.</para>
    ///
    /// <para>The module ids are strings and stay strings, as every presentation reference in
    /// the simulation is: a clone without the licensed packs loads every plant and runs
    /// headless exactly as a checkout with them.</para>
    /// </summary>
    public class PlantDef : Def
    {
        /// <summary>Growing-window ticks from seed to ripe. See <see cref="growWindowStartTick"/>.</summary>
        [DefRequired] public int growTicks = 130_000;

        /// <summary>Ticks of work to break the ground and put a seed in, grass clearing included.</summary>
        [DefRequired] public int sowWorkTicks = 170;

        /// <summary>Ticks of work to cut a ripe crop and gather what it yields.</summary>
        [DefRequired] public int harvestWorkTicks = 200;

        /// <summary>How many of <see cref="yields"/> one harvest spawns.</summary>
        [DefRequired] public int yieldCount = 5;

        /// <summary>
        /// The least fertile ground the seed takes, in the per cent of ordinary soil that
        /// <c>TerrainDef.fertility</c> speaks. 70 admits grass and bare earth and refuses
        /// gravel: at the 50 the first sketch had, the gravel under the terrace risers was
        /// plantable, and carrots in gravel read as a bug (docs/design/22-growing.md §2).
        /// </summary>
        [DefRequired] public int minFertility = 70;

        /// <summary>The item one harvest yields <see cref="yieldCount"/> of.</summary>
        [DefReference(typeof(ItemDef))] public string yields = string.Empty;

        /// <summary>
        /// The daylight window, in ticks of the day: growth happens only inside
        /// <c>[growWindowStartTick, growWindowEndTick)</c>. It is a property of the clock, not
        /// of the sky — v1 has no light model, and this window is what stands in for one rather
        /// than pretending otherwise (docs/design/22-growing.md §3, §8).
        /// </summary>
        public int growWindowStartTick = 15_000;
        public int growWindowEndTick = 47_500;

        /// <summary>
        /// The temperature band a crop grows in, in centi-degrees (design 28 §8): full speed
        /// between <see cref="minGrowTempC"/> and <see cref="optimalGrowTempC"/>, falling off
        /// linearly to nothing at absolute zero of growth below the one and at
        /// <see cref="maxGrowTempC"/> above the other. The defaults are the reference's own
        /// 6/42/58 °C shape (a-06 §2), carried as content so a hardy or a delicate crop is a row
        /// of XML rather than a code change.
        /// </summary>
        public int minGrowTempC = 600;
        public int optimalGrowTempC = 4_200;
        public int maxGrowTempC = 5_800;

        /// <summary>Presentation module ids, one per drawn stage. Never resolved in the simulation.</summary>
        public string moduleIdSmall = "odyssey.module.carrot.s";
        public string moduleIdMedium = "odyssey.module.carrot.m";
        public string moduleIdLarge = "odyssey.module.carrot.l";

        /// <summary>Is the crop in daylight at this tick of the day?</summary>
        public bool GrowsAt(int tickOfDay) =>
            tickOfDay >= growWindowStartTick && tickOfDay < growWindowEndTick;

        /// <summary>
        /// How fast the crop grows at a temperature, per-mille: the reference's own response
        /// (a-06 §2) — linear from frozen at <see cref="minGrowTempC"/> to full speed at
        /// comfortable, full through the band, then linear again down to nothing at
        /// <see cref="maxGrowTempC"/>. Integer division floors toward zero, so below freezing of
        /// the band the answer is exactly stopped rather than fractionally creeping.
        /// </summary>
        public int GrowRatePerMille(int tempC)
        {
            if (tempC <= 0) return 0;
            if (tempC < minGrowTempC) return tempC * 1_000 / minGrowTempC;
            if (tempC <= optimalGrowTempC) return 1_000;
            if (tempC >= maxGrowTempC) return 0;
            return (maxGrowTempC - tempC) * 1_000 / (maxGrowTempC - optimalGrowTempC);
        }

        /// <summary>How far through growing this many accumulated ticks is, 0–1000.</summary>
        public int Milligrowth(int ticks)
        {
            int milli = growTicks <= 0 ? 1000 : (int)((long)ticks * 1000 / growTicks);
            return milli > 1000 ? 1000 : milli;
        }

        /// <summary>
        /// The drawn stage for this many accumulated ticks: <b>0 — the seed day</b> below 25 per
        /// cent grown, then 1 below 45, 2 below 85, 3 from there. Stage 0 draws nothing but the
        /// seed specks: a seed that went into the soil this morning should not stand a sprout by
        /// the afternoon, and the sprout art arriving at growth nought was exactly that fault
        /// (owner, 2026-09-19: *"the seeds should stay there at first — at those flowers
        /// sprouting or whatever should appear after a day"*). 25 per cent is one daylight
        /// window's worth of the carrot's four — 32,500 of 130,000 ticks — so the sprout comes
        /// up after the first full day of light, and a seed sown at dusk waits the night out
        /// first, which is what a seed does.
        ///
        /// <para>Before the seed day was a stage, 1 began at growth nought; the third band was
        /// a third wide, and for more than a day of it the field stood full of full-size carrots
        /// that were correctly not ripe — which read, in play, as sowers planting beside a
        /// harvest nobody was taking (owner, 2026-09-18). The big art now arrives close enough
        /// to ripeness that what looks pickable nearly is.</para>
        ///
        /// <para>Stages are what re-meshes — <see cref="PlantGrowthSystem"/> marks a chunk
        /// dirty on nothing else — and the seed day makes the crop's lifetime four of them, the
        /// new one spending its whole mark on bare soil.</para>
        /// </summary>
        public int StageOfTicks(int ticks)
        {
            int milli = Milligrowth(ticks);
            return milli < 250 ? 0 : milli < 450 ? 1 : milli < 850 ? 2 : 3;
        }
    }
}
