#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What a region of a body feeds (design 43 §2, §3). Three capacities, because the six regions
    /// can say nothing finer: a leg is walking, an arm is working, and the head and the torso are
    /// what keep somebody conscious at all.
    /// </summary>
    public enum BodyCapacity : byte
    {
        None = 0,
        Consciousness = 1,
        Moving = 2,
        Manipulation = 3,
    }

    /// <summary>
    /// What an injury is (design 43 §2). <b>Appended, never inserted</b>: a record's kind is saved
    /// and hashed by this number. Our own words, not the reference's.
    /// </summary>
    public enum AfflictionKind : byte
    {
        /// <summary>A cut, from a sharp blow. The one kind that bleeds, until somebody tends it.</summary>
        Wound = 0,

        /// <summary>From a blunt blow or a short fall. Hurts; never bleeds.</summary>
        Bruise = 1,

        /// <summary>The worst hit of a fall of two layers or more. Hurts; never bleeds.</summary>
        Fracture = 2,
    }

    /// <summary>Which coverage a hit picks its region by: a blow from the side, or the ground coming up.</summary>
    public enum HitSet : byte
    {
        Melee = 0,
        Fall = 1,
    }

    /// <summary>
    /// One region of a body (design 43 §2). The regions' <b>order in <see cref="HealthDef.regions"/>
    /// is their index</b>, and the index is what an injury record saves: appended, never inserted.
    /// </summary>
    public class BodyRegionDef
    {
        /// <summary>The name the interface publishes it under, and what <see cref="overflowTo"/> names.</summary>
        public string name = string.Empty;

        /// <summary>Whole points (a-02 §1): head 25, torso 40, each arm and each leg 30.</summary>
        public int hitPoints = 30;

        /// <summary>Its share of a melee blow, out of the body's total. INVENTED (a-02 could not find the reference's).</summary>
        public int meleeCoverage;

        /// <summary>Its share of a fall's hits: the bottom-facing mirror of the reference's roof rule. INVENTED.</summary>
        public int fallCoverage;

        public BodyCapacity capacity;

        /// <summary>
        /// At nought, consciousness is nought, and so the pawn is down (design 43 §2). Not dead:
        /// death stays the pool's line and blood loss, so an unordered fight still ends in downs
        /// (design 33 §3).
        /// </summary>
        public bool vital;

        /// <summary>
        /// The region that takes what this one cannot hold, by name, or empty to hold it all
        /// (a-02:20: an arm's excess passes to the torso). A limb names the torso; the torso and
        /// the head name nothing, so every point of every blow is on the ledger somewhere.
        /// </summary>
        public string overflowTo = string.Empty;
    }

    /// <summary>A stage of blood loss (a-02:35): at this much lost, consciousness is scaled and capped.</summary>
    public class BloodStage
    {
        /// <summary>Blood lost, per mille, at which this stage begins.</summary>
        public int atPerMille;

        /// <summary>Consciousness is multiplied by this, per mille.</summary>
        public int consciousnessPerMille = 1_000;

        /// <summary>And then capped at this, per mille. 1,000 caps nothing.</summary>
        public int consciousnessCapPerMille = 1_000;
    }

    /// <summary>
    /// A body and every number that makes it hurt, bleed and heal (<c>docs/design/43-health.md</c>).
    /// A species names one by <see cref="SpeciesDef.health"/>; a species that names none keeps the
    /// hit-point pool alone, which is every animal today (design 43 §8) and every content set built
    /// in code.
    ///
    /// <para>Every value is either cited to a line of <c>docs/research/a-02-health.md</c> in
    /// <c>Health.xml</c> or marked INVENTED there. Nothing here is a float.</para>
    /// </summary>
    public class HealthDef : Def
    {
        /// <summary>The body, in index order. See <see cref="BodyRegionDef"/>.</summary>
        public List<BodyRegionDef> regions = new List<BodyRegionDef>();

        // ---- pain and the downed line (design 43 §3) -----------------------------------------

        /// <summary>Pain per whole point of live injury, in tenths of a per mille: 125 is a-02's 1.25 %.</summary>
        public int painPerPointTenths = 125;

        /// <summary>At or above this much pain, per mille, she goes down (a-02:30, a-02:49). 1,001 switches it off.</summary>
        public int painShockPerMille = 800;

        /// <summary>Pain above this, per mille, starts to cost consciousness (a-02:24).</summary>
        public int painConsciousnessFromPerMille = 100;

        /// <summary>The most pain can take off consciousness, per mille (a-02:24: 40 %).</summary>
        public int painConsciousnessMaxPerMille = 400;

        /// <summary>Below this consciousness, per mille, she is down (a-02:48).</summary>
        public int downedConsciousnessBelowPerMille = 300;

        /// <summary>At or below this much moving, per mille, she is down (a-02:50).</summary>
        public int downedMovingAtPerMille = 150;

        // ---- blood (design 43 §4) ------------------------------------------------------------

        /// <summary>
        /// Blood lost per day, per mille, for every whole point of an untended wound. INVENTED:
        /// a-02:160 could not find the reference's conversion. A 10-point cut alone kills in 40 hours.
        /// </summary>
        public int bleedPerPointPerDay = 60;

        /// <summary>Blood back per day, per mille, once nothing bleeds (a-02:37: a third a day).</summary>
        public int bloodRecoveryPerDay = 333;

        /// <summary>Dead at this much lost, per mille (a-02:35).</summary>
        public int bloodDeathAtPerMille = 1_000;

        /// <summary>The stages, ascending (a-02:35).</summary>
        public List<BloodStage> bloodStages = new List<BloodStage>();

        // ---- tending (design 43 §5, §6) ------------------------------------------------------

        /// <summary>Tend quality per mille by Medicine level, before potency (a-02:42).</summary>
        public List<CurvePoint> tendQualityCurve = new List<CurvePoint>();

        /// <summary>Potency with bare hands, per mille (a-02:42: none is 0.3).</summary>
        public int bareHandsPotencyPerMille = 300;

        /// <summary>
        /// Potency with medical supplies, per mille (a-02:42: industrial is 1.0). Supplies are any
        /// item whose Def heals (<c>Medical.NearestSupplies</c>, design 37); one is used per treatment.
        /// </summary>
        public int suppliesPotencyPerMille = 1_000;

        /// <summary>The most quality bare hands can reach, per mille (a-02:42: 70 %).</summary>
        public int bareHandsCapPerMille = 700;

        /// <summary>The most quality medical supplies can reach, per mille (a-02:42: 100 %).</summary>
        public int suppliesCapPerMille = 1_000;

        /// <summary>A tended injury heals this many thousandths of a point a day at nought quality, anywhere (a-02:41: +4).</summary>
        public int tendHealMinPerDay = 4_000;

        /// <summary>And this many at full quality (a-02:41: +12).</summary>
        public int tendHealMaxPerDay = 12_000;

        /// <summary>
        /// A tend's quality at a Medicine level, per mille (a-02:42): the curve, times the
        /// potency of what was used, clamped by it. Integer, so it replays exactly.
        /// </summary>
        public int TendQualityPerMille(int level, bool supplies)
        {
            int skill = CombatDef.Evaluate(tendQualityCurve, level);
            int potency = supplies ? suppliesPotencyPerMille : bareHandsPotencyPerMille;
            int cap = supplies ? suppliesCapPerMille : bareHandsCapPerMille;
            int quality = skill * potency / 1_000;
            if (quality < 0) quality = 0;
            return quality > cap ? cap : quality;
        }

        // ---- falls (design 43 §7) ------------------------------------------------------------

        /// <summary>
        /// A fall of n layers does <c>round(base × n^1.5)</c> whole points (a-02:91). The
        /// exponent is fixed at 1.5 in code, computed by an integer square root, because a
        /// fractional power has no exact integer form and the simulation has no floats.
        /// </summary>
        public int fallDamageBase = 15;

        /// <summary>A fall lands as between this many and <see cref="fallHitsMax"/> hits (a-02:101).</summary>
        public int fallHitsMin = 2;

        public int fallHitsMax = 4;

        /// <summary>Each hit strays this far either way, per mille (a-02:101: ±20 %).</summary>
        public int fallSpreadPerMille = 200;

        /// <summary>A fall of this many layers or more makes its worst hit a fracture.</summary>
        public int fallFractureFromLayers = 2;

        // ---- derived at load, never read from XML --------------------------------------------

        int[]? _overflow;

        /// <summary>The region a region's excess passes to, by index, or -1. Resolved once.</summary>
        public int OverflowOf(int region)
        {
            if (_overflow == null || _overflow.Length != regions.Count)
            {
                var overflow = new int[regions.Count];
                for (int r = 0; r < regions.Count; r++)
                {
                    overflow[r] = -1;
                    string to = regions[r].overflowTo;
                    if (string.IsNullOrEmpty(to)) continue;
                    for (int o = 0; o < regions.Count; o++)
                        if (regions[o].name == to) { overflow[r] = o; break; }
                }
                _overflow = overflow;
            }
            return _overflow[region];
        }

        /// <summary>A region's pool in thousandths.</summary>
        public int RegionMilli(int region) => regions[region].hitPoints * Rates.Scale;

        /// <summary>The body's total coverage for a set, which a roll is drawn against.</summary>
        public int CoverageTotal(HitSet set)
        {
            int total = 0;
            for (int r = 0; r < regions.Count; r++)
                total += set == HitSet.Fall ? regions[r].fallCoverage : regions[r].meleeCoverage;
            return total;
        }

        /// <summary>The region a roll in <c>[0, CoverageTotal)</c> lands on, walking the regions in order.</summary>
        public int RegionAt(HitSet set, int roll)
        {
            for (int r = 0; r < regions.Count; r++)
            {
                roll -= set == HitSet.Fall ? regions[r].fallCoverage : regions[r].meleeCoverage;
                if (roll < 0) return r;
            }
            return regions.Count - 1;
        }

        /// <summary>
        /// Whole points of a fall of <paramref name="layers"/>, in thousandths:
        /// <c>base × n × √n</c>, the root taken to three places by an integer square root, so a
        /// fall of one is exactly 15,000 and a fall of five 167,700 (a-02:93-99).
        /// </summary>
        public int FallDamageMilli(int layers)
        {
            if (layers <= 0) return 0;
            long rootMilli = IntegerSqrt((long)layers * 1_000_000);
            long milli = (long)fallDamageBase * layers * rootMilli;
            return milli > int.MaxValue ? int.MaxValue : (int)milli;
        }

        /// <summary>floor(√value), exactly, for a non-negative value.</summary>
        public static long IntegerSqrt(long value)
        {
            // Newton's method in integers: no float anywhere, so every platform agrees to the bit.
            if (value <= 0) return 0;
            long x = value, y = (x + 1) / 2;
            while (y < x)
            {
                x = y;
                y = (x + value / x) / 2;
            }
            return x;
        }
    }
}
