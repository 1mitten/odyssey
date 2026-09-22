#nullable enable

namespace Odyssey.Hud
{
    /// <summary>
    /// What a speed request actually means, and what a pause comes back to.
    ///
    /// <para>The clock has four buttons and four keys, and Space is a toggle: asking for pause
    /// while already paused means "start again". The question that toggle has to answer is
    /// <em>start again at what</em>. Until now the answer was the literal 1, so a player at triple
    /// speed who paused to give an order was dropped back to normal on every unpause, silently
    /// undoing a choice they had made a moment earlier (owner, 2026-09-21). The answer is the
    /// speed they were last actually running at.</para>
    ///
    /// <para>It lives here rather than in the composition root because it is a rule and this
    /// assembly is the one the fast tier compiles. One instance owns it; every path that changes
    /// the speed goes through <see cref="Resolve"/>, and the one path that does not — restoring a
    /// saved view, which writes the world's speed directly — tells it with <see cref="Remember"/>
    /// so a colony loaded at triple speed and then paused comes back to triple.</para>
    /// </summary>
    public sealed class SpeedControl
    {
        /// <summary>0 paused, 1 normal, 2 fast, 3 very fast — the rig's own convention.</summary>
        public const int Normal = 1;

        /// <summary>The speed an unpause returns to. Never zero: a pause cannot resume into a
        /// pause, so a world that has only ever been paused starts at normal.</summary>
        public int Resume { get; private set; } = Normal;

        /// <summary>
        /// The speed to actually set, given what was asked for and what the world is running at.
        /// A request for a real speed is granted and remembered; a request to pause a running
        /// world remembers the speed it was running at; a request to pause an already-paused
        /// world is the toggle, and returns <see cref="Resume"/>.
        /// </summary>
        public int Resolve(int requested, int current)
        {
            if (requested > 0)
            {
                Resume = requested;
                return requested;
            }

            if (current > 0)
            {
                Resume = current;
                return 0;
            }

            return Resume;
        }

        /// <summary>A speed set by something other than a request — the saved view being
        /// restored. A zero is ignored, because the memory is of running and not of stopping.</summary>
        public void Remember(int speed)
        {
            if (speed > 0) Resume = speed;
        }
    }
}
