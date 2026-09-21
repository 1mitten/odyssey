#nullable enable

using System;

namespace Odyssey.Hud.Diagnostics
{
    /// <summary>
    /// One second of frames, kept as raw samples so the percentiles are real.
    ///
    /// <para><b>Why this exists rather than another mean.</b> Every performance number in this
    /// project has been a mean over 180 frames — <c>FrameTimeTests.TimeFrames</c> reports a mean
    /// and a worst, and everything in <c>docs/design/06-rendering-and-camera.md</c> came out of it.
    /// A mean cannot see the thing a player calls stutter: one frame in a hundred at four times
    /// the cost moves a mean by three per cent and is the entire complaint. So the window keeps
    /// what it was given and ranks it.</para>
    ///
    /// <para><b>The ranking rule is deliberately the one already in the repository.</b>
    /// <c>Odyssey.Sim.Diagnostics.PhaseTrace</c> ranks by nearest rank on a sorted copy, and this
    /// does the same, so a p95 in a trace and a p95 in a tick benchmark mean the same thing. Two
    /// percentile conventions in one repository is the "one rule, two owners" fault
    /// <c>docs/bug-patterns.md</c> opens with, applied to arithmetic.</para>
    ///
    /// <para><b>A copy, not a sort in place.</b> A window is asked for several percentiles and
    /// then for its section means, and sorting the samples would silently reorder what the next
    /// question reads. The copy is one allocation per question, once a second, off the hot path.
    /// </para>
    ///
    /// <para>UnityEngine-free, like everything in <c>Odyssey.Hud</c>, so the fast tier proves it.
    /// It knows nothing about what a section *is*: the names live with the thing that has them
    /// (<c>OdysseyBootstrap.FrameSection</c>) and arrive here as a count and an order.</para>
    /// </summary>
    public sealed class FrameWindow
    {
        double[] _frame;
        double[] _gpu;
        double[] _submit;
        double[] _tick;

        /// <summary>Running totals, because a section is a cost and wants a mean, not a rank.</summary>
        readonly double[] _sectionTotals;

        int _count;

        /// <param name="sections">How many per-frame sections each sample carries.</param>
        /// <param name="capacity">
        /// Frames expected in a window. 512 covers a second at any frame rate anyone will see; it
        /// grows rather than dropping if a second turns out to be longer, because the sample that
        /// would be dropped is exactly the one worth keeping.
        /// </param>
        public FrameWindow(int sections, int capacity = 512)
        {
            if (sections < 0) throw new ArgumentOutOfRangeException(nameof(sections));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));

            _frame = new double[capacity];
            _gpu = new double[capacity];
            _submit = new double[capacity];
            _tick = new double[capacity];
            _sectionTotals = new double[sections];
        }

        /// <summary>Frames added since the last <see cref="Reset"/>.</summary>
        public int Frames => _count;

        public int SectionCount => _sectionTotals.Length;

        /// <summary>
        /// Take one frame. Allocates nothing once the arrays have grown to the frame rate, which
        /// is within the first second.
        /// </summary>
        public void Add(double frameMs, double gpuMs, double submitMs, double tickMs,
            ReadOnlySpan<double> sections)
        {
            if (_count == _frame.Length) Grow();

            _frame[_count] = frameMs;
            _gpu[_count] = gpuMs;
            _submit[_count] = submitMs;
            _tick[_count] = tickMs;
            _count++;

            int n = sections.Length < _sectionTotals.Length ? sections.Length : _sectionTotals.Length;
            for (int i = 0; i < n; i++) _sectionTotals[i] += sections[i];
        }

        void Grow()
        {
            Array.Resize(ref _frame, _frame.Length * 2);
            Array.Resize(ref _gpu, _gpu.Length * 2);
            Array.Resize(ref _submit, _submit.Length * 2);
            Array.Resize(ref _tick, _tick.Length * 2);
        }

        /// <summary>Forget the second just written. Keeps the arrays, so the next second allocates nothing.</summary>
        public void Reset()
        {
            _count = 0;
            for (int i = 0; i < _sectionTotals.Length; i++) _sectionTotals[i] = 0d;
        }

        public double FramePercentile(double fraction) => Percentile(_frame, fraction);
        public double GpuPercentile(double fraction) => Percentile(_gpu, fraction);
        public double SubmitPercentile(double fraction) => Percentile(_submit, fraction);
        public double TickPercentile(double fraction) => Percentile(_tick, fraction);

        public double FrameMax => Max(_frame);
        public double GpuMax => Max(_gpu);
        public double SubmitMax => Max(_submit);

        /// <summary>
        /// The worst single tick in the window.
        ///
        /// <para>The tick runs outside every <c>FrameSection</c>, so this is the only figure that
        /// can tell an expensive tick apart from an expensive engine frame.</para>
        /// </summary>
        public double TickMax => Max(_tick);

        /// <summary>The mean of one section over the window, or 0 where nothing was added.</summary>
        public double SectionMean(int section)
        {
            if (section < 0 || section >= _sectionTotals.Length || _count == 0) return 0d;
            return _sectionTotals[section] / _count;
        }

        /// <summary>
        /// How many frames were strictly longer than a threshold.
        ///
        /// <para>Strictly: a frame of exactly 33 ms is a 30 Hz frame and is not over budget. The
        /// boundary is asserted, because an off-by-one here would put a spike count on a
        /// perfectly paced 30 Hz session.</para>
        /// </summary>
        public int Over(double thresholdMs)
        {
            int over = 0;
            for (int i = 0; i < _count; i++) if (_frame[i] > thresholdMs) over++;
            return over;
        }

        double Percentile(double[] samples, double fraction)
        {
            if (_count == 0) return 0d;

            var copy = new double[_count];
            Array.Copy(samples, copy, _count);
            Array.Sort(copy);
            return copy[Rank(_count, fraction)];
        }

        double Max(double[] samples)
        {
            double max = 0d;
            for (int i = 0; i < _count; i++) if (samples[i] > max) max = samples[i];
            return max;
        }

        /// <summary>Nearest rank, the same arithmetic as <c>PhaseTrace.Rank</c>.</summary>
        static int Rank(int n, double fraction)
        {
            int rank = (int)Math.Ceiling(fraction * n) - 1;
            if (rank < 0) rank = 0;
            if (rank >= n) rank = n - 1;
            return rank;
        }
    }
}
