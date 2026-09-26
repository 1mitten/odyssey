#nullable enable

namespace Odyssey.Sim.Pawns
{
    // The prisoner line's drivers (design 58 §7), JobHandle 28 to 35, claimed together by the
    // contracts step (P3) so that every table a save depends on is extended once. Each is a stub
    // that fails the tick it starts until its own unit writes it, so nothing can run one yet.

    /// <summary><c>Job_Capture</c>: carry a downed pawn to a free prison bed. Written by P6.</summary>
    public class CaptureJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_FeedPrisoner</c>: carry a meal to a prisoner. Written by P7.</summary>
    public class FeedPrisonerJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Chat</c>: a warden talks a prisoner round. Written by P8.</summary>
    public class ChatJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Escort</c>: a warden walks a released prisoner out. Written by P10.</summary>
    public class EscortJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_GoToCell</c>: a surrendered raider walks to a prison bed. Written by P11.</summary>
    public class GoToCellJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Escape</c>: bash the door, run for the edge. Written by P9.</summary>
    public class EscapeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_LeaveFree</c>: a pawn let go walks off the board. Written by P10.</summary>
    public class LeaveFreeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Arrest</c>: walk to a colonist and take her. Written by P12.</summary>
    public class ArrestJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
