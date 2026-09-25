#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The names the simulation publishes the fight under (design 33 §5), as the interface reads
    /// them. String literals and not a shared constant, on the bargain
    /// <see cref="JobLabels.CarryingAspect"/> and <see cref="OrderModel.DraftedAspect"/> make: this
    /// assembly cannot reference <c>Odyssey.Sim</c>, and a test on each side holds its copy to the
    /// literal — <c>CombatAspectNamesTests</c> here, <c>CombatContractTests</c> in the simulation's
    /// suite, which reads <c>Odyssey.Sim.Pawns.CombatAspects</c>.
    ///
    /// <para>Every one is sparse: absent means "nothing to say", never nought.</para>
    /// </summary>
    public static class CombatAspectNames
    {
        /// <summary>Hit points, thousandths. Published while hurt, downed or drafted.</summary>
        public const string Hp = "odyssey.pawn.hp";

        /// <summary>The pool, thousandths, beside <see cref="Hp"/>.</summary>
        public const string HpMax = "odyssey.pawn.hp.max";

        /// <summary>The item def index of the weapon in the hand; absent for bare hands.</summary>
        public const string Weapon = "odyssey.pawn.weapon";

        /// <summary>The held weapon's quality tier; absent for none (design 47 §11).</summary>
        public const string WeaponQuality = "odyssey.pawn.weapon.quality";

        /// <summary>
        /// The <see cref="PawnId"/> value this pawn was ordered to attack by the player; absent for
        /// a fight she started herself and for a rescue (design 33 §18b). What the ring reads.
        /// </summary>
        public const string OrderTarget = "odyssey.pawn.order.target";

        /// <summary>
        /// The colonist's response to danger, 1 Defend or 2 Flee; absent at the default, Fight
        /// back (design 33 §18c). What the pane's response button shows.
        /// </summary>
        public const string Response = "odyssey.pawn.response";

        /// <summary>Present on a downed colonist with no free bed to be carried to (design 33 §11d).</summary>
        public const string RescueNoBed = "odyssey.pawn.rescue.nobed";

        public static readonly AspectKey HpKey = AspectKey.Of(Hp);
        public static readonly AspectKey HpMaxKey = AspectKey.Of(HpMax);
        public static readonly AspectKey WeaponKey = AspectKey.Of(Weapon);
        public static readonly AspectKey WeaponQualityKey = AspectKey.Of(WeaponQuality);
        public static readonly AspectKey OrderTargetKey = AspectKey.Of(OrderTarget);
        public static readonly AspectKey RescueNoBedKey = AspectKey.Of(RescueNoBed);
        public static readonly AspectKey ResponseKey = AspectKey.Of(Response);
    }
}
