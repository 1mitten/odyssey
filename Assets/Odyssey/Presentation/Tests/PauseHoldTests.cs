#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a drawn colonist does while the game is paused: nothing, and then carries on.
    ///
    /// The owner's report was that pausing reset every figure and that some carried on for a
    /// moment first, and both halves came out of presentation guessing at the pause rather than
    /// being told about it. The guess had a quarter of a second of grace in it, which is fifteen
    /// frames at sixty and 24% of the axe's 1.15 s stroke — the moment that carried on. And the
    /// guess only ever gated the swing, so everything else went on easing: with the pawn no
    /// longer moving, the measured speed was nought, and the figure's own speed was smoothed
    /// towards it at 0.35 a frame. Against the real gait speeds that is 73.5% walk weight down to
    /// 99% idle in ten frames, 0.167 s — a figure snapping into a standing pose. Not a reset, but
    /// indistinguishable from one.
    ///
    /// A figure cannot be built outside a running editor, so what is tested here is the two
    /// decisions that produced it: the speed a frame with no time in it reports, and which gait
    /// the blend then picks. The third part of the fix — the clip rate going to zero so Unity's
    /// own graph stops advancing on wall-clock time — can only be seen in Play.
    /// </summary>
    public class PauseHoldTests
    {
        /// <summary>Measured off the pack's root-motion clips, scaled by the 1.4 a colonist is
        /// drawn at. Idle, walk, run. Same numbers as <see cref="GaitBlendTests"/>.</summary>
        static readonly float[] Colonist = { 0f, 2.04f, 3.63f };

        const float Frame = 1f / 60f;

        /// <summary>A 2.5 m cell every hundred ticks at sixty ticks a second.</summary>
        const float Pace = 1.5f;

        static Vector3 At(float metres) => new Vector3(metres, 0f, 0f);

        [Test]
        public void AFrameWithNoTimeInItReportsNoSpeedAtAll()
        {
            // Not a speed of zero: no answer. The pawn had no opportunity to move, so the frame
            // says nothing about how fast the figure is going and the last answer stands.
            float held = PawnFigureDirector.ObserveSpeed(
                Pace, At(0f), At(0f), deltaTime: 0f, settled: true);
            Assert.That(held, Is.EqualTo(Pace));
        }

        [Test]
        public void AWalkingFigureKeepsItsStrideRightThroughAPause()
        {
            // Two seconds of pause is a hundred and twenty frames, and the pawn does not move in
            // any of them. Before the fix the tenth frame was already standing still.
            float speed = Pace;
            Vector3 stood = At(4f);
            for (int frame = 0; frame < 120; frame++)
                speed = PawnFigureDirector.ObserveSpeed(speed, stood, stood, deltaTime: 0f, settled: true);

            Assert.That(speed, Is.EqualTo(Pace).Within(1e-5f));

            GaitBlend blend = GaitBlend.Solve(Colonist, speed);
            Assert.That(blend.WeightOf(0), Is.EqualTo(0.265f).Within(0.01f), "idle");
            Assert.That(blend.WeightOf(1), Is.EqualTo(0.735f).Within(0.01f), "walk");
        }

        [Test]
        public void AndPicksTheWalkStraightBackUpWhenTheWorldMovesAgain()
        {
            // "Continue", not "restart": the frame after the pause is an ordinary frame, and it
            // starts from the speed the figure was held at rather than from a standstill.
            float speed = Pace;
            Vector3 stood = At(4f);
            for (int frame = 0; frame < 60; frame++)
                speed = PawnFigureDirector.ObserveSpeed(speed, stood, stood, deltaTime: 0f, settled: true);

            float resumed = PawnFigureDirector.ObserveSpeed(
                speed, stood, stood + At(Pace * Frame), Frame, settled: true);

            Assert.That(resumed, Is.EqualTo(Pace).Within(0.01f),
                "a figure that was walking before the pause is walking on the first frame after it");
        }

        [Test]
        public void TheOldInferenceIsWhatMadeAPauseLookLikeAReset()
        {
            // The negative control, and the measurement the fix is against. Smoothing towards a
            // measured nought — which is what the director used to do on every frame, pause or
            // no pause — reaches the idle in ten frames.
            float speed = Pace;
            int frames = 0;
            while (GaitBlend.Solve(Colonist, speed).WeightOf(0) <= 0.99f && frames < 600)
            {
                speed = Mathf.Lerp(speed, 0f, PawnFigureDirector.SpeedSmoothing);
                frames++;
            }

            Assert.That(frames, Is.EqualTo(10));
            Assert.That(frames * Frame, Is.LessThan(0.2f),
                "a fifth of a second is a snap, not a blend, which is why it read as a reset");
        }

        [Test]
        public void APublishedFrameSaysWhetherTheWorldIsRunning()
        {
            // The contract the director now reads instead of counting frames since the last tick.
            Assert.That(new WorldSnapshot().Running, Is.True);
        }
    }
}
