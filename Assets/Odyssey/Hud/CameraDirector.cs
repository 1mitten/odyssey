#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Where the camera has been asked to go, per <c>09-ui-and-input.md</c> §3 row 17.
    ///
    /// The director holds the request as a cell and nothing about how to get there: the rig in
    /// the Unity assembly glides to it on its own smoothing and reports back when it has arrived,
    /// so the decision is testable without a camera and the motion is tuned where the camera is.
    /// A pan cancels a jump, because a player taking the camera back is the player winning.
    /// Follow-selection belongs here too and arrives with the command that asks for it.
    /// </summary>
    public sealed class CameraDirector
    {
        /// <summary>The cell the camera is heading for, or null when it is where it was asked to be.</summary>
        public CellRef? JumpTarget { get; private set; }

        public void JumpTo(CellRef cell) => JumpTarget = cell;

        /// <summary>The player moved the camera themselves: whatever it was heading for, it stops.</summary>
        public void Cancel() => JumpTarget = null;

        /// <summary>The rig has landed on the target.</summary>
        public void Arrived() => JumpTarget = null;
    }
}
