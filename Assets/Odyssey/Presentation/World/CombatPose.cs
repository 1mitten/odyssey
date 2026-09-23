#nullable enable

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The fight's computed poses (design 33 §1): the fallback for every role the Sword Combat
    /// pack's clips play when the pack is present — a swing for each <c>AttackStyle</c> timed so
    /// its impact lands on the attack's <c>windupTicks</c>, a hit react, a dodge, a stun, the
    /// downed lie and the death pose — and the punch and the bite the pack does not have at all.
    /// Pure functions of a phase and a rig, like <c>WorkSwing</c>, <c>CarryPose</c> and
    /// <c>SleepPose</c>, so EditMode tests can hold them without the pack. <b>Lane B's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>Empty from the contracts step.</b></para>
    /// </summary>
    public static class CombatPose
    {
    }
}
