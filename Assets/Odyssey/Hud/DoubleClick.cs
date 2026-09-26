#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Two clicks on the same colonist, close enough in time to be one gesture.
    ///
    /// <para><b>One threshold for every surface.</b> <see cref="Seconds"/> is the world pick's
    /// double click (<c>SelectionPresenter</c>: everyone on screen) and the roster card's
    /// (owner, 2026-09-25: a double click on a card closes in on her). They were one literal in
    /// one file; a second copy would drift the first time somebody tuned it.</para>
    ///
    /// <para>The clock is the caller's — <c>Time.unscaledTime</c>, so a double click still works
    /// while the game is paused — which keeps this Unity-free and fast-tier tested.</para>
    /// </summary>
    public sealed class DoubleClick
    {
        /// <summary>Two presses closer than this, on the same colonist, are a double click.</summary>
        public const float Seconds = 0.35f;

        float _at;
        PawnId _pawn;

        /// <summary>
        /// A click on <paramref name="pawn"/> at <paramref name="now"/> seconds. True when it
        /// completes a double click; the pair is then spent, so a third click starts a new one
        /// rather than making a second double.
        /// </summary>
        public bool Click(PawnId pawn, float now)
        {
            if (pawn.IsValid && pawn == _pawn && now - _at <= Seconds)
            {
                _pawn = PawnId.None;
                return true;
            }
            _at = now;
            _pawn = pawn;
            return false;
        }

        /// <summary>Forget the first click: something else happened in between (a sweep, a Shift-click).</summary>
        public void Forget() => _pawn = PawnId.None;
    }
}
