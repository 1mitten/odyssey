#nullable enable
namespace Odyssey.Hud
{
    /// <summary>
    /// One mouse button held down: did the pointer travel far enough for this to have been a drag,
    /// or was it a click?
    ///
    /// <para><b>Why this is a type and not four lines in the camera rig.</b> The rig already
    /// answers exactly this question for the left button, inline, and it is the only part of the
    /// input path the fast tier cannot see: the PlayMode harness still cannot deliver a synthetic
    /// mouse to a <c>MonoBehaviour</c>, so anything decided in there ships untested. The arithmetic
    /// comes out here — UnityEngine-free, like everything in this assembly — and the rig is left
    /// holding the wiring. It is the same lift that made <see cref="DesignateDirector"/> testable,
    /// for the same reason.</para>
    ///
    /// <para><b>Travel latches.</b> Once a press has gone past the threshold it was a drag, even if
    /// the pointer comes back to where it started — an orbit that returns to its own origin is
    /// still an orbit, and a player who swung the camera around and back has not clicked
    /// anything.</para>
    /// </summary>
    public struct PressGesture
    {
        /// <summary>
        /// How far a right-button press may travel and still count as a click rather than an
        /// orbit.
        ///
        /// <para>Six pixels, which is the same figure the left button uses for "did the player mean
        /// to draw a box" — and it is deliberately a <em>separate</em> number, because it answers a
        /// different question. A box threshold is about the smallest area a player might mean to
        /// select; this one is about the hand's own tremor on a button that is usually held and
        /// swept. If either wants moving it should move on its own, judged at the keyboard.</para>
        /// </summary>
        public const float OrbitThresholdPixels = 6f;

        float _anchorX;
        float _anchorY;

        /// <summary>Is the button down right now?</summary>
        public bool Down { get; private set; }

        /// <summary>Has this press already travelled far enough to be a drag?</summary>
        public bool Travelled { get; private set; }

        // The threshold is a constant and not a field, which is not laziness: this is a struct
        // held as a field on a MonoBehaviour, and `new PressGesture()` runs the implicit
        // all-zeroes constructor rather than any constructor declared here with optional
        // parameters. A threshold kept as state would therefore be zero in exactly the place it is
        // used, and every press would read as a drag. The tests caught it; the type no longer
        // allows it. If the right button's threshold ever needs to differ from the left's, this
        // constant is the one line to move.

        /// <summary>The button went down here.</summary>
        public void Press(float x, float y)
        {
            _anchorX = x;
            _anchorY = y;
            Down = true;
            Travelled = false;
        }

        /// <summary>The pointer is here now. Ignored unless the button is down.</summary>
        public void MoveTo(float x, float y)
        {
            if (!Down || Travelled) return;
            float dx = x - _anchorX;
            float dy = y - _anchorY;
            if (dx * dx + dy * dy >= OrbitThresholdPixels * OrbitThresholdPixels) Travelled = true;
        }

        /// <summary>
        /// The button came up. True when the press never travelled — that is, it was a click.
        /// False for a drag, and false when there was no press to release, so a button released
        /// without ever having been pressed on the world cannot be mistaken for a click on it.
        /// </summary>
        public bool Release()
        {
            bool click = Down && !Travelled;
            Down = false;
            Travelled = false;
            return click;
        }

        /// <summary>
        /// Throw the press away without it counting as anything — the pointer left the world, or
        /// something else took the gesture.
        /// </summary>
        public void Abandon()
        {
            Down = false;
            Travelled = false;
        }
    }
}
