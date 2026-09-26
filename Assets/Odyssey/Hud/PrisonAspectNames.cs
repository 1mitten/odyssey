using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The prison's pawn aspects as the interface reads them (design 58 §11): the simulation's
    /// <c>PrisonAspects</c> names, copied because this assembly cannot see that one, and held to
    /// agree by spelling.
    /// </summary>
    public static class PrisonAspectNames
    {
        /// <summary>Present on a downed pawn waiting for a prison bed when none is free.</summary>
        public const string NoBed = "odyssey.pawn.prison.nobed";

        /// <summary>Present on a person marked for capture and not yet taken.</summary>
        public const string CaptureMark = "odyssey.pawn.prison.capture";

        public static readonly AspectKey NoBedKey = AspectKey.Of(NoBed);
        public static readonly AspectKey CaptureMarkKey = AspectKey.Of(CaptureMark);
    }
}
