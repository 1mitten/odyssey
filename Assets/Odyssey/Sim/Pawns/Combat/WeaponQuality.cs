#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// How well a weapon was made (design 47 §11; owner, 2026-09-25: <i>"give the guns quality like
    /// you do with beds (and apply this to all weapons) depending on the spawn/who crafted them"</i>).
    /// The beds' five tiers (<see cref="QualityHandle"/>) and the beds' roll
    /// (<see cref="QualityContent.Roll"/>, a tier centred on the maker's skill), carried on the item
    /// (<see cref="ColonyItem.Quality"/>) and applied through the <see cref="Armament"/>: a tier scales
    /// the weapon's damage and its hit chance, swung or shot, by the factors on its
    /// <see cref="QualityDef"/>.
    ///
    /// <para><b>Who made it.</b> Nothing is crafted yet, so every weapon is a find, and the maker's
    /// skill stands in for how good a find is: <see cref="FoundSkill"/> for a weapon that turns up
    /// — the debug menu's grant and the Arm row — and <see cref="BanditSkill"/> for a bandit's own
    /// gear, which is poorer. A crafted weapon, the day there is a bench, rolls on the crafter's
    /// skill through the same <see cref="Assign"/>.</para>
    ///
    /// <para><b>Rolled once, when the item is made</b>, on its own stream salted by the thing's id,
    /// and kept for life: saved on the item, hashed only when set, published on the thing and on the
    /// hand that holds it. A weapon with no tier — one from a save before quality, or made by a path
    /// that never rolled — fights as <see cref="QualityHandle.Normal"/>, the weapon its Def says.</para>
    /// </summary>
    public static class WeaponQuality
    {
        /// <summary>The skill a found weapon is rolled at: mostly normal, sometimes poor or decent. INVENTED.</summary>
        public const int FoundSkill = 6;

        /// <summary>The skill a bandit's own weapon is rolled at: mostly poor. INVENTED.</summary>
        public const int BanditSkill = 2;

        /// <summary>
        /// Roll <paramref name="item"/>'s tier as made by a maker of <paramref name="makerSkill"/>, if
        /// it is a weapon and has none yet. Deterministic: the world's seed, this tick, and the thing's
        /// own id.
        /// </summary>
        public static void Assign(PawnContext ctx, ColonyItem? item, int makerSkill)
        {
            if (item == null || item.Quality != 0) return;
            if ((uint)item.DefIndex >= (uint)ctx.Content.Items.Length || ctx.Content.Items[item.DefIndex].weapon == null) return;
            var rng = DeterministicRandom.ForTick(ctx.Seed, ctx.CurrentTick, PawnPurpose.WeaponQuality ^ (uint)item.Id.Value);
            item.Quality = QualityContent.Roll(makerSkill, rng);
        }

        /// <summary>The tier a weapon fights at: its own, or normal when it has none.</summary>
        public static int TierOf(byte quality) =>
            QualityContent.IsQuality(quality) ? quality : QualityHandle.Normal;

        /// <summary>A weapon's damage at its tier, per mille of its figure.</summary>
        public static int DamagePerMille(in Armament armament) =>
            armament.Armed ? QualityContent.QualityAt(TierOf(armament.Quality)).weaponDamagePerMille : 1_000;

        /// <summary>A weapon's hit chance at its tier, per mille of what it would otherwise be.</summary>
        public static int AccuracyPerMille(in Armament armament) =>
            armament.Armed ? QualityContent.QualityAt(TierOf(armament.Quality)).weaponAccuracyPerMille : 1_000;

        /// <summary><paramref name="damageMilli"/> at the armament's tier.</summary>
        public static int Damage(int damageMilli, in Armament armament) =>
            (int)((long)damageMilli * DamagePerMille(armament) / 1_000);

        /// <summary><paramref name="chancePerMille"/> at the armament's tier, never over a certainty.</summary>
        public static int Accuracy(int chancePerMille, in Armament armament)
        {
            long chance = (long)chancePerMille * AccuracyPerMille(armament) / 1_000;
            return chance > 1_000 ? 1_000 : (int)chance;
        }
    }
}
