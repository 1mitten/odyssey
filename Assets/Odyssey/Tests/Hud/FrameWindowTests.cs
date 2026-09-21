#nullable enable

using System;
using NUnit.Framework;
using Odyssey.Hud.Diagnostics;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What a second of frames adds up to.
    ///
    /// <para><b>Why percentiles and not the mean this project has always used.</b>
    /// <c>FrameTimeTests.TimeFrames</c> reports a mean and a worst and nothing between, and every
    /// number in <c>docs/design/06-rendering-and-camera.md</c> came out of it. A mean hides the
    /// thing a player actually notices: one frame in a hundred taking four times as long is
    /// invisible in a mean and is the whole of what "it stutters" means. So the window keeps its
    /// raw samples and ranks them, exactly as <c>Odyssey.Sim.Diagnostics.PhaseTrace</c> does for
    /// the tick — same nearest-rank rule, deliberately, so two percentiles in this repo can never
    /// mean two different things.</para>
    /// </summary>
    public class FrameWindowTests
    {
        static FrameWindow WindowOf(params double[] frameMs)
        {
            var window = new FrameWindow(sections: 2);
            Span<double> sections = stackalloc double[2];
            foreach (double ms in frameMs)
            {
                sections[0] = 1d;
                sections[1] = 2d;
                window.Add(ms, gpuMs: ms / 2d, submitMs: ms / 4d, tickMs: 0.1d, sections);
            }
            return window;
        }

        [Test]
        public void ThePercentileIsNearestRankOnTheSortedSamples()
        {
            // Ten distinct samples, shuffled, so sorted they are 1..10 and the expected value is
            // its own index plus one. The rank for a fraction f is ceil(f * 10) - 1, so p50 is
            // index 4 — the value 5 — and p95 is index 9, the value 10.
            //
            // Distinct on purpose. The first draft of this test used a list with a repeated 1 and
            // asserted 5 for the median, which is the answer you get by counting the *distinct*
            // values rather than the samples. The code was right and the test was wrong, which is
            // the easy way to get a percentile convention quietly changed to match a mistake.
            FrameWindow window = WindowOf(3, 7, 4, 1, 5, 9, 2, 6, 10, 8);

            Assert.That(window.Frames, Is.EqualTo(10));
            Assert.That(window.FramePercentile(0.50), Is.EqualTo(5d).Within(1e-9));
            Assert.That(window.FramePercentile(0.95), Is.EqualTo(10d).Within(1e-9));
            Assert.That(window.FrameMax, Is.EqualTo(10d).Within(1e-9));
        }

        /// <summary>
        /// The samples are ranked on a copy, so a window can be asked for two percentiles and for
        /// the sections afterwards without the second answer being taken from a reordered list.
        /// </summary>
        [Test]
        public void RankingDoesNotDisturbTheWindow()
        {
            FrameWindow window = WindowOf(3, 1, 2);

            double first = window.FramePercentile(0.50);
            window.FramePercentile(0.99);

            Assert.That(window.FramePercentile(0.50), Is.EqualTo(first).Within(1e-9));
            Assert.That(window.SectionMean(0), Is.EqualTo(1d).Within(1e-9), "the sections moved");
            Assert.That(window.SectionMean(1), Is.EqualTo(2d).Within(1e-9));
        }

        /// <summary>
        /// An empty window answers zero, never NaN.
        ///
        /// <para>Not a nicety. A zero and a NaN are both wrong, but a NaN reaches the JSON as
        /// <c>NaN</c>, which is not valid JSON, so one empty window would make a whole trace
        /// unreadable by the tool that is the point of writing it.</para>
        /// </summary>
        [Test]
        public void AnEmptyWindowAnswersZeroRatherThanNaN()
        {
            var window = new FrameWindow(sections: 2);

            Assert.That(window.Frames, Is.Zero);
            Assert.That(window.FramePercentile(0.50), Is.Zero);
            Assert.That(window.FrameMax, Is.Zero);
            Assert.That(window.GpuPercentile(0.50), Is.Zero);
            Assert.That(window.SubmitPercentile(0.50), Is.Zero);
            Assert.That(window.TickPercentile(0.50), Is.Zero);
            Assert.That(window.SectionMean(0), Is.Zero);
            Assert.That(window.Over(33d), Is.Zero);
        }

        [Test]
        public void SpikesAreCountedAtTheThresholdNotBelowIt()
        {
            FrameWindow window = WindowOf(10, 33, 33.5, 60, 200);

            Assert.That(window.Over(33d), Is.EqualTo(3), "33.0 is not over 33");
            Assert.That(window.Over(50d), Is.EqualTo(2));
        }

        /// <summary>
        /// A window outlives its second and is reused, so resetting must leave nothing behind —
        /// including the section sums, which are the easiest half to forget.
        /// </summary>
        [Test]
        public void ResetLeavesNothingOfTheSecondBefore()
        {
            FrameWindow window = WindowOf(10, 20, 30);
            window.Reset();

            Assert.That(window.Frames, Is.Zero);
            Assert.That(window.FramePercentile(0.50), Is.Zero);
            Assert.That(window.SectionMean(0), Is.Zero, "the section sums survived the reset");
            Assert.That(window.Over(1d), Is.Zero);

            Span<double> sections = stackalloc double[2];
            sections[0] = 4d;
            sections[1] = 8d;
            window.Add(5d, 1d, 1d, 1d, sections);

            Assert.That(window.Frames, Is.EqualTo(1));
            Assert.That(window.SectionMean(0), Is.EqualTo(4d).Within(1e-9));
        }

        /// <summary>
        /// More frames than the window was built for is a long second, not an error, and it must
        /// not quietly drop samples — a dropped sample is one that would have been the spike.
        /// </summary>
        [Test]
        public void AVeryLongSecondGrowsRatherThanDroppingSamples()
        {
            var window = new FrameWindow(sections: 1, capacity: 4);
            Span<double> sections = stackalloc double[1];

            for (int i = 1; i <= 100; i++)
            {
                sections[0] = 1d;
                window.Add(i, gpuMs: 0d, submitMs: 0d, tickMs: 0d, sections);
            }

            Assert.That(window.Frames, Is.EqualTo(100));
            Assert.That(window.FrameMax, Is.EqualTo(100d).Within(1e-9));
            Assert.That(window.FramePercentile(0.50), Is.EqualTo(50d).Within(1e-9));
        }

        /// <summary>The section mean is a mean, because a section is a cost and not an experience.</summary>
        [Test]
        public void SectionsAreMeanedAndFramesAreRanked()
        {
            var window = new FrameWindow(sections: 1);
            Span<double> sections = stackalloc double[1];

            sections[0] = 1d;
            window.Add(10d, 0d, 0d, 0d, sections);
            sections[0] = 3d;
            window.Add(20d, 0d, 0d, 0d, sections);

            Assert.That(window.SectionMean(0), Is.EqualTo(2d).Within(1e-9));
            Assert.That(window.FramePercentile(0.50), Is.EqualTo(10d).Within(1e-9));
        }
    }
}
