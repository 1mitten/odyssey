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

        /// <summary>
        /// How far back the camera should stand when it lands, in metres, or null to keep the
        /// zoom the player has. Only a jump that asks for it carries one (<see cref="JumpTo(CellRef, float)"/>).
        /// </summary>
        public float? JumpDistance { get; private set; }

        /// <summary>
        /// Counts requests, so the rig can tell a second request for the cell it is already
        /// heading to — the same colonist double-clicked twice, a zoom asked for mid-glide — from
        /// the one it has already taken.
        /// </summary>
        public int JumpSerial { get; private set; }

        /// <summary>
        /// The distance a close-up jump stands back (roster double-click, 2026-09-25): near enough
        /// that one colonist fills a good part of the view, and above the rig's own 10 m floor,
        /// which it clamps to in any case.
        /// </summary>
        public const float CloseUpMetres = 14f;

        /// <summary>A glide to the cell at the current zoom.</summary>
        public void JumpTo(CellRef cell)
        {
            JumpTarget = cell;
            JumpDistance = null;
            JumpSerial++;
        }

        /// <summary>A glide to the cell that also zooms to <paramref name="distance"/> metres on the way.</summary>
        public void JumpTo(CellRef cell, float distance)
        {
            JumpTarget = cell;
            JumpDistance = distance;
            JumpSerial++;
        }

        /// <summary>The player moved the camera themselves: whatever it was heading for, it stops.</summary>
        public void Cancel()
        {
            JumpTarget = null;
            JumpDistance = null;
        }

        /// <summary>The rig has landed on the target.</summary>
        public void Arrived()
        {
            JumpTarget = null;
            JumpDistance = null;
        }
    }
}
