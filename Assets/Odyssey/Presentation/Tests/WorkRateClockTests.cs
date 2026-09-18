#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The stroke clock runs at the rate the simulation publishes (design 17 §3d). The one-line
    /// change that makes a master visibly swing faster and a novice labour — tested as the
    /// arithmetic it is, because a figure cannot be built outside a running editor.
    /// </summary>
    public class WorkRateClockTests
    {
        const float Frame = 1f / 60f;

        [Test]
        public void TheClockAdvancesAtThePublishedRate()
        {
            Assert.That(PawnFigureDirector.SwingAdvance(Frame, 1_000), Is.EqualTo(Frame).Within(1e-6f),
                "the standard rate is the stroke everybody is tuned to");
            Assert.That(PawnFigureDirector.SwingAdvance(Frame, 2_650), Is.EqualTo(2.65f * Frame).Within(1e-6f),
                "a level-20 miner");
            Assert.That(PawnFigureDirector.SwingAdvance(Frame, 550), Is.EqualTo(0.55f * Frame).Within(1e-6f),
                "a novice");
        }

        [Test]
        public void TheAspectNameIsTheOneTheDesignPublishes()
        {
            // Both sides spell the name independently — the sim mints it, presentation asks for
            // it — so the name is the contract, and a typo on either side is a figure that
            // always works at today's speed and nothing else ever wrong.
            Assert.That(RateAspects.Work, Is.EqualTo(AspectKey.Of("odyssey.pawn.rate.work")));
            Assert.That(RateAspects.Move, Is.EqualTo(AspectKey.Of("odyssey.pawn.rate.move")));
        }

        [Test]
        public void AnUnpublishedRateIsTodaysSpeed()
        {
            // A frame that predates the aspect, or a pawn that vanished between frames: the
            // figure falls back to the standard rate rather than to nothing.
            var frame = new WorldSnapshot();
            Assert.That(frame.TryGetPawnAspect(new PawnId(1), RateAspects.Work, out int rate),
                Is.False, "nothing published this frame");
            Assert.That(Rates.Scale, Is.EqualTo(1_000),
                "and the fallback answers the standard rate, which is what 1,000 means");
            Assert.That(PawnFigureDirector.SwingAdvance(Frame, rate == 0 ? 1_000 : rate),
                Is.EqualTo(Frame).Within(1e-6f));
        }
    }
}
