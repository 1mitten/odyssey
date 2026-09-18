#nullable enable

namespace Odyssey.Sim
{
    /// <summary>
    /// The scale rates and accumulators count in, and the one place the save's version rule lives.
    ///
    /// <para><b>A rate is an integer in thousandths, where 1,000 means the speed everything is
    /// tuned at today</b> (design 17 §2). A rate never changes what a thing costs; it changes how
    /// fast a pawn pays for it. The accumulators that receive one — <c>DesignationGrid._work</c>,
    /// <c>ConstructionGrid._work</c>, <c>Job.ToilProgress</c> and <c>Pawn.MoveProgress</c> — all
    /// count the same unit, so a cell worked by two colonists of different speed accumulates in a
    /// unit that means the same thing to both, and every comparison reads <c>cost ×
    /// <see cref="Scale"/></c>.</para>
    ///
    /// <para><b>The scale is internal.</b> Everything that crosses the sim→UI contract, and
    /// everything a human reads, stays in ticks at the standard rate: <c>CellDetail.WorkToClear</c>,
    /// <c>SiteView.WorkDone/WorkTotal</c> and <c>WorkFor()</c> are all published in ticks, and the
    /// accumulators are divided back where they reach the interface.</para>
    ///
    /// <para><b>The state hash is not one of those places.</b> It reads every accumulator whole,
    /// at the resolution the accumulator is kept at. The division was there briefly so WS1 could
    /// land without a golden moving, and once WS3 re-baked them it was only a blind spot a
    /// thousand milliwork wide.</para>
    ///
    /// <para><b>One unit, everywhere, including the toils that have no rate.</b> A toil that
    /// nothing can speed up — eating, sleeping, standing down — pays at exactly
    /// <see cref="Scale"/> a tick rather than counting plain ticks. A single field carrying two
    /// units is what <see cref="FromSave"/> cannot read, and what makes half a hash blind.</para>
    /// </summary>
    public static class Rates
    {
        /// <summary>Thousandths per tick-at-standard-rate. 1,000 is today's speed.</summary>
        public const int Scale = 1_000;

        /// <summary>
        /// Read an accumulator out of a save. Before format 5 the four accumulators counted raw
        /// ticks; from 5 they count thousandths of a tick, so an old value is read at the old
        /// scale — the sensible reading of a number written by a build whose only speed was the
        /// standard one.
        /// </summary>
        public static int FromSave(int savedValue, int formatVersion) =>
            formatVersion < 5 ? savedValue * Scale : savedValue;
    }
}
