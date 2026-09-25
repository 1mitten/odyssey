#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_AttackRanged</c> (design 47 §2d): stand where the line to the target is open and
    /// shoot at it. <b>A stub until R3</b>: the contracts step claims the handle, the Def and the
    /// driver slot so every table a save depends on is extended once, and the job fails the tick it
    /// starts, so nothing can run it yet.
    /// </summary>
    public class AttackRangedJobDriver : JobDriver
    {
        /// <summary>Walking until the line opens, or standing with it open waiting for the clock.</summary>
        public const int Approach = 0;

        /// <summary>Aiming; <see cref="JobDriver.ToilProgress"/> counts the aim in milliwork.</summary>
        public const int Aim = 1;

        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.CombatTarget = 0;
    }
}
