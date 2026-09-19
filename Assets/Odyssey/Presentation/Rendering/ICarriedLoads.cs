#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Whatever knows where each colonist's load is being held this frame. Design 24 §5a.
    ///
    /// <para><b>An interface because the renderer must not depend on the figure director.</b> The
    /// dependency runs the other way round everywhere else — the director is one of the things the
    /// camera rig binds, and the renderer is a level below it. The one fact the renderer needs is
    /// a point in space per carrying pawn, and it is a fact only the director can supply, because
    /// the cradle is measured off palms that exist only after the pose pass has run.</para>
    ///
    /// <para>Optional, and a null implementation is a supported state rather than a degraded one:
    /// a scene with no animated figures at all draws every load at the stand-in offset, which is
    /// what a clone with no licensed art gets and what the harnesses get.</para>
    /// </summary>
    public interface ICarriedLoads
    {
        /// <summary>
        /// The item def, the stack, the world point the load's base sits on and the yaw it is
        /// turned to, for one pawn. False when this pawn has no live figure, or is empty-handed,
        /// or is holding something that is deliberately not being drawn — a swimmer's, for now.
        /// </summary>
        bool TryGetCarried(int pawnId, out int def, out int stack, out Vector3 at, out float yaw);

        /// <summary>
        /// A thing that has just been put down and is still falling the last third of a metre out
        /// of the hands that held it: where those hands were, and how long ago.
        ///
        /// <para>The renderer supplies the other end — the cell the thing landed in — because
        /// that is the half it owns. False for everything that is simply lying there, which is
        /// almost every item on the board almost all of the time.</para>
        /// </summary>
        bool TryGetSettling(int thingId, out Vector3 from, out float elapsed);

        /// <summary>
        /// Whether this pawn is drawn as a live figure at all.
        ///
        /// <para>Distinct from <see cref="TryGetCarried"/> returning false, and the distinction is
        /// what stops a load being drawn twice or not at all: a figure that is carrying nothing
        /// and a pawn that has no figure both answer false there, but only the second wants the
        /// renderer's own stand-in placement.</para>
        /// </summary>
        bool HasFigureFor(int pawnId);
    }
}
