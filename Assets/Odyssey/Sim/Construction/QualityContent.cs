#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Construction
{
    /// <summary>
    /// One tier of how well a thing was made: Poor, Normal, Decent, Uber or Epic — the owner's
    /// five names, Odyssey's own (docs/design/20-beds.md §6).
    ///
    /// <para><b>What a tier is:</b> a label the player reads on a finished bed, one number that
    /// says how well it rests, and a handle order that is the contract, exactly as
    /// <see cref="BuildingDef"/> is shaped. What a tier is <i>not</i> is a factor on a stat —
    /// rest effectiveness is a level in the same per-cent scale
    /// <c>groundRestEffectiveness</c> already uses (80 for the ground, 100 for a plain bed),
    /// so the numbers stay comparable at a glance.</para>
    /// </summary>
    public class QualityDef : Def
    {
        /// <summary>
        /// How well a colonist sleeps in a bed of this tier, in per cent of a plain bed's rest.
        /// Poor 85, Normal 100, Decent 112, Uber 125, Epic 140 — the owner's interview answers,
        /// provisional by design and tunable here without touching code.
        /// </summary>
        public int restEffectiveness;

        /// <summary>
        /// A weapon of this quality's damage, per mille of its figure (design 47 §11): the
        /// reference's shape, a tenth a step, more at the top.
        /// </summary>
        public int weaponDamagePerMille = 1_000;

        /// <summary>
        /// A weapon of this quality's accuracy, per mille of the hit chance — a swing's as well as a
        /// shot's (design 47 §11).
        /// </summary>
        public int weaponAccuracyPerMille = 1_000;

        /// <summary>The registry key the interface names this tier by. Never a label.</summary>
        public string iconKey = "";
    }

    /// <summary>
    /// The quality table, indexed by <see cref="QualityHandle"/>. The same arrangement the other
    /// content families use: an in-code oracle, an XML mirror beside the buildings
    /// (<c>Defs/Core/World/Quality.xml</c>), and the pair held together by
    /// <c>ConstructionContentDefTests</c>.
    /// </summary>
    public static class QualityContent
    {
        static readonly QualityDef[] QualityTable = BuildQualities();

        public static IReadOnlyList<QualityDef> Qualities => QualityTable;

        public static QualityDef QualityAt(int handle) => QualityTable[handle];

        public static bool IsQuality(int handle) =>
            handle > QualityHandle.None && handle < QualityTable.Length;

        /// <summary>
        /// How well a colonist sleeps in a bed of this tier, in per cent. Tier 0 is "takes no
        /// quality" and answers 100 — a plain bed's rest — so a caller that forgets to check the
        /// def's <see cref="BuildingDef.takesQuality"/> still gets a sane number rather than an
        /// index error.
        /// </summary>
        public static int RestEffectiveness(int tier) =>
            IsQuality(tier) ? QualityTable[tier].restEffectiveness : 100;

        /// <summary>
        /// Roll the tier a thing finished at, from the finishing colonist's Construction skill —
        /// the shape the reference settled (a-04 §4): rolled once, at the moment of completion,
        /// by whoever swung the last tick of work.
        ///
        /// <para><b>The bell, and why it is arithmetic rather than a table.</b> The roll centres
        /// on the tier the skill has earned — <c>1 + skill / 5</c>, so skill 0 centres on Poor
        /// and skill 20 on Epic — and the weight of a tier falls off by two a step from that
        /// centre: 5, 3, 1, then nothing. Nine weights whatever the skill, which means every
        /// colonist can be surprised in both directions and nobody is guaranteed anything: a
        /// novice can never reach Epic and a master can never fall to Poor, and everything
        /// between is luck the table shapes. The endpoints are the contract the tests pin; the
        /// draws are the dice, and no test ever asserts a drawn value.</para>
        /// </summary>
        public static byte Roll(int skill, DeterministicRandom rng)
        {
            int centre = 1 + Math.Max(0, Math.Min(20, skill)) / 5;

            int draw = rng.NextInt(9);
            for (int tier = 1; tier <= 5; tier++)
            {
                draw -= Weight(tier, centre);
                if (draw < 0) return (byte)tier;
            }
            return (byte)centre;
        }

        /// <summary>The bell's one rule: 5 at the centre, 3 one step out, 1 two steps, 0 beyond.</summary>
        static int Weight(int tier, int centre)
        {
            int distance = Math.Abs(tier - centre);
            return Math.Max(0, 5 - 2 * distance);
        }

        /// <summary>See <c>PawnContent.Register</c>: the Def types this content is made of.</summary>
        public static DefLoader Register(DefLoader loader) => loader.Register<QualityDef>();

        /// <summary>
        /// The handle order, which is the contract, as ever: a def's position here is a
        /// <see cref="QualityHandle"/> value, written into a finished record, so this list is what
        /// resolves a name. As with <see cref="ConstructionContent.BuildingOrder"/>, index 0 is
        /// "none" and lives in the table like any other row, because the handle and the position
        /// must not be off by one from each other.
        /// </summary>
        public static readonly string[] QualityOrder =
        {
            "Quality_None", "Quality_Poor", "Quality_Normal", "Quality_Decent", "Quality_Uber", "Quality_Epic",
        };

        /// <summary>The whole tier table, in handle order, read from a loaded pack.</summary>
        public static QualityDef[] QualitiesFromDefs(DefDatabase defs)
        {
            var table = new QualityDef[QualityOrder.Length];
            for (int i = 0; i < QualityOrder.Length; i++) table[i] = One<QualityDef>(defs, QualityOrder[i]);
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

        static QualityDef[] BuildQualities()
        {
            return new[]
            {
                // 0 is "takes no quality", matching the handle's zero default, exactly as
                // Building_None is. Its rest answer is a plain bed's, which is what the scenario's
                // own bed spots have always given.
                new QualityDef
                {
                    defName = "Quality_None", label = "nothing", restEffectiveness = 100, weaponDamagePerMille = 1000, weaponAccuracyPerMille = 1000,
                },

                new QualityDef
                {
                    defName = "Quality_Poor", label = "poor", restEffectiveness = 85, weaponDamagePerMille = 900, weaponAccuracyPerMille = 900,
                    iconKey = "ui.quality.poor",
                },
                new QualityDef
                {
                    defName = "Quality_Normal", label = "normal", restEffectiveness = 100, weaponDamagePerMille = 1000, weaponAccuracyPerMille = 1000,
                    iconKey = "ui.quality.normal",
                },
                new QualityDef
                {
                    defName = "Quality_Decent", label = "decent", restEffectiveness = 112, weaponDamagePerMille = 1100, weaponAccuracyPerMille = 1050,
                    iconKey = "ui.quality.decent",
                },
                new QualityDef
                {
                    defName = "Quality_Uber", label = "uber", restEffectiveness = 125, weaponDamagePerMille = 1200, weaponAccuracyPerMille = 1100,
                    iconKey = "ui.quality.uber",
                },
                new QualityDef
                {
                    defName = "Quality_Epic", label = "epic", restEffectiveness = 140, weaponDamagePerMille = 1350, weaponAccuracyPerMille = 1150,
                    iconKey = "ui.quality.epic",
                },
            };
        }
    }
}
