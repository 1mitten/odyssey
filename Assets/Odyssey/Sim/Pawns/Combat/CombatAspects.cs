#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names the draft and the fight are published under (design 33 §2e, §5), minted the way
    /// <see cref="RateAspects"/> mints the rates: <c>Sim.Contracts</c> never hears that drafting or
    /// hit points exist, and the interface asks for them by name. Every one is <b>sparse</b> —
    /// published only while it has something to say — so a colony nobody drafts and nobody hurts
    /// publishes nothing new.
    ///
    /// <para><b>The spellings are a contract with the interface.</b> The Hud assembly cannot see
    /// this one, so it keeps literal copies (<c>Odyssey.Hud.CombatAspectNames</c>, and
    /// <c>OrderModel</c> for the draft's two), and a test on each side holds its copy to the
    /// literal: <c>CombatContractTests.TheAspectNamesAreSpelledAsTheInterfaceReadsThem</c> here
    /// and <c>CombatAspectNamesTests</c> there.</para>
    ///
    /// <para>Moved out of <c>Draft.cs</c> by the contracts step, because the draft's file now
    /// belongs to the fight's lane and a contract should not live in a file somebody is
    /// rewriting.</para>
    /// </summary>
    public static class CombatAspects
    {
        /// <summary>1 while the colonist is drafted; absent otherwise.</summary>
        public const string DraftedName = "odyssey.pawn.drafted";

        /// <summary>
        /// The cell index a drafted colonist is walking to, or the building it is ordered to
        /// strike (C6); absent while it holds. Also the cell of a weapon a colonist was sent for
        /// from the context menu, <b>drafted or not</b> (design 33 §7a), so an undrafted colonist
        /// publishes it with no drafted row before it.
        /// </summary>
        public const string OrderCellName = "odyssey.pawn.order.cell";

        /// <summary>
        /// Hit points, in thousandths. Published while the pawn is hurt, downed or drafted — the
        /// pawns a health bar is drawn over (design 33 §1) — for animals as well as people.
        /// </summary>
        public const string HpName = "odyssey.pawn.hp";

        /// <summary>
        /// The pool, in thousandths: for <b>every person</b>, always (the Health tab reads "x / 100"
        /// for a whole colonist), and for an animal beside <see cref="HpName"/> whenever that is.
        /// A person with this and no <see cref="HpName"/> is whole.
        /// </summary>
        public const string HpMaxName = "odyssey.pawn.hp.max";

        /// <summary>The item def index of the weapon in the hand (C3); absent for bare hands.</summary>
        public const string WeaponName = "odyssey.pawn.weapon";

        /// <summary>
        /// The <c>PawnId</c> value of the pawn this one is <b>under the player's orders</b> to
        /// attack; absent otherwise (design 33 §18b). Only a forced attack on a pawn
        /// (<c>PawnRegistry.OrderTargetOf</c>): a fight she started herself — the hold's blow,
        /// fighting back, a join — keeps its target on the pawn and publishes nothing, because
        /// what reads this is the lock-on ring, and the ring means <i>you sent her</i>.
        /// </summary>
        public const string OrderTargetName = "odyssey.pawn.order.target";

        /// <summary>
        /// The <c>PawnId</c> value of the downed colonist this one is rescuing, ordered or of her
        /// own accord; absent otherwise (design 33 §11e, §18b). What presentation finds a carrier
        /// by. Its own aspect since §18, when the order target stopped meaning "or rescue".
        /// </summary>
        public const string RescuePatientName = "odyssey.pawn.rescue.patient";

        /// <summary>
        /// A colonist's <see cref="HostilityResponse"/> as its number, 1 Defend or 2 Flee; absent
        /// at the default, Fight back (design 33 §18c). What the pane's button shows.
        /// </summary>
        public const string ResponseName = "odyssey.pawn.response";

        /// <summary>
        /// 1 on a downed colonist lying where she fell for whom no bed is free, so nobody can be
        /// sent to carry her; absent otherwise (design 33 §11d). What the "no bed" alert reads.
        /// </summary>
        public const string RescueNoBedName = "odyssey.pawn.rescue.nobed";

        public static readonly AspectKey Drafted = AspectKey.Of(DraftedName);
        public static readonly AspectKey OrderCell = AspectKey.Of(OrderCellName);
        public static readonly AspectKey Hp = AspectKey.Of(HpName);
        public static readonly AspectKey HpMax = AspectKey.Of(HpMaxName);
        public static readonly AspectKey Weapon = AspectKey.Of(WeaponName);
        public static readonly AspectKey OrderTarget = AspectKey.Of(OrderTargetName);
        public static readonly AspectKey RescueNoBed = AspectKey.Of(RescueNoBedName);
        public static readonly AspectKey RescuePatient = AspectKey.Of(RescuePatientName);
        public static readonly AspectKey Response = AspectKey.Of(ResponseName);
    }
}
