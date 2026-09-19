#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names a carried load is published under, minted the way <see cref="RateAspects"/> and
    /// <c>SkillAspects</c> mint theirs (design 24 §5b).
    ///
    /// <para>A pawn aspect rather than two more fields on <c>PawnView</c>, which is the OQ-45 seam
    /// doing exactly what it is for: <c>Sim.Contracts</c> never hears that carrying exists, and the
    /// two things that need to know — the renderer, which draws the load, and the inspect pane,
    /// which names it — ask for it by name. <see cref="PawnAspect"/>'s own documentation offers
    /// <c>"odyssey.pawn.carrying"</c> as its worked example, so this is the mechanism being used as
    /// designed rather than stretched.</para>
    ///
    /// <para><b>Sparse, and that is the point.</b> Most colonists are carrying nothing most of the
    /// time, and a pawn with nothing in its hands publishes no row at all. A field on the view
    /// would cost two ints per pawn per publish for ever.</para>
    ///
    /// <para><b>Not saved and not hashed</b>, exactly as <c>PawnGesture</c> is not. The state that
    /// belongs in the save is <c>Job.CarriedItem</c>, which already is; this is a report derived
    /// from it.</para>
    /// </summary>
    public static class CarryAspects
    {
        /// <summary>
        /// The item def index the pawn has in its arms. Published only while it has one, so the
        /// absence of the row is the answer "empty-handed" — there is deliberately no -1.
        /// </summary>
        public static readonly AspectKey Carrying = AspectKey.Of("odyssey.pawn.carrying");

        /// <summary>
        /// How many are in the load.
        ///
        /// <para><b>The arms ignore this and it is still published</b> (design 24 §3a). The owner's
        /// decision is that the armful is constant whatever the stack — one pebble walking forty
        /// metres reads as a bug — so the amount moved into words, and the inspect pane is the
        /// thing that needs it. Publishing it beside the def rather than making the pane fetch the
        /// item by id keeps presentation out of the item store.</para>
        /// </summary>
        public static readonly AspectKey Stack = AspectKey.Of("odyssey.pawn.carrying.stack");
    }
}
