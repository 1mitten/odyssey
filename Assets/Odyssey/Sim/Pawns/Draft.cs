#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// A drafted colonist's mind (design 33 §2b): the hold, or the walk it was sent on — and
    /// nothing else, which is the whole of "a drafted colonist does not eat or sleep". It sits
    /// directly after <see cref="MentalStateThinkNode"/> and above <see cref="CriticalNeedsThinkNode"/>;
    /// moved below the needs branch, a drafted colonist would wander off to eat.
    ///
    /// <para>A move is not chosen here. It is a forced job started by <c>OrderMove</c>, and when
    /// it ends the tree runs and this node gives the hold again: there is no order queue.</para>
    /// </summary>
    public sealed class DraftedThinkNode : ThinkNode
    {
        public override string Name => "Drafted";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.Drafted) return false;

            // Exhaustion outranks the draft (design 33 §2b): at zero rest the colonist goes down
            // where it stands, through the needs branch's collapse rule, instead of being held
            // upright for ever by an order. Between jobs here, so there is nothing to interrupt.
            if (pawn.Needs[NeedIndex.Rest] <= 0)
            {
                pawn.Drafted = false;
                return false;
            }

            job.Reset(JobIndex.DraftHold);
            return true;
        }
    }

    /// <summary>
    /// Stand where you were put (design 33 §2c). Claims nothing and never expires; it ends when the
    /// draft has been quiet for <see cref="PawnContent.DraftQuietTicks"/>, undrafting the colonist
    /// on the way out, and the tree then gives it work.
    ///
    /// <para>The quiet clock is read here rather than swept by a system, so it costs nothing for a
    /// colony nobody has drafted and one comparison a tick for each colonist who is.</para>
    /// </summary>
    public class DraftHoldJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx)
        {
            if (!Pawn.Drafted) return JobStatus.Succeeded;

            // Exhaustion, asked here as well as in the node: a colonist already holding never
            // thinks again, so the node alone would never see her rest reach nought.
            bool spent = Pawn.Needs[NeedIndex.Rest] <= 0;
            if (!spent && ctx.CurrentTick - Pawn.DraftQuietSinceTick < ctx.Content.DraftQuietTicks)
                return JobStatus.Ongoing;

            Pawn.Drafted = false;
            return JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// Walk to the cell the player named, and stop (design 33 §2c). The wander's walk, except that
    /// a walk that cannot be made is a failure rather than a shrug: the player asked for it.
    /// </summary>
    public class GotoJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => GotoCell(ctx, Job.TargetCell);
    }

    // The draft's aspect names moved to Combat/CombatAspects.cs with the combat contracts step
    // (design 33 §5): this file now belongs to the fight's lane, which fills the drafted
    // colonist's adjacent auto-attack into the hold above, and a contract should not live in a
    // file somebody is rewriting.
}
