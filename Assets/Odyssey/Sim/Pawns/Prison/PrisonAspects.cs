using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What the prison publishes about a pawn (design 58 §11), as sparse pawn aspects: a pawn the
    /// colony has nothing to say about publishes none. The interface reads the same names from
    /// <c>Odyssey.Hud.PrisonAspectNames</c>, which cannot see this assembly.
    /// </summary>
    public static class PrisonAspects
    {
        /// <summary>1 on a downed pawn the colony means to hold for whom no prison bed is free. The alert reads it.</summary>
        public const string NoBedName = "odyssey.pawn.prison.nobed";

        /// <summary>1 on a person marked for capture and not yet taken.</summary>
        public const string CaptureMarkName = "odyssey.pawn.prison.capture";

        public static readonly AspectKey NoBed = AspectKey.Of(NoBedName);
        public static readonly AspectKey CaptureMark = AspectKey.Of(CaptureMarkName);

        /// <summary>Everything the prison says about one pawn, into the publish.</summary>
        public static void Publish(SnapshotWriter writer, Pawn pawn, PawnContext ctx)
        {
            if (pawn.Prison != null && pawn.Prison.CaptureMark) writer.AddPawnAspect(pawn.Id, CaptureMark, 1);
            if (pawn.Downed && CaptureRules.WantsCapture(pawn, ctx) && CaptureRules.BedFor(pawn, pawn, ctx) < 0)
                writer.AddPawnAspect(pawn.Id, NoBed, 1);
        }
    }
}
