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

        /// <summary>The <see cref="PawnId"/> value this pawn is ordered to attack or rescue.</summary>
        public const string OrderTarget = "odyssey.pawn.order.target";

        public static readonly AspectKey HpKey = AspectKey.Of(Hp);
        public static readonly AspectKey HpMaxKey = AspectKey.Of(HpMax);
        public static readonly AspectKey WeaponKey = AspectKey.Of(Weapon);
        public static readonly AspectKey OrderTargetKey = AspectKey.Of(OrderTarget);
    }
}
