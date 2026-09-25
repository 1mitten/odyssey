#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// How far a shot is (design 47 §2a): centre to centre in millimetres over the anisotropic
    /// cell, and the integer root it is taken with. The root carries its control — the double root
    /// it replaces, caught wrong on a value this one gets right.
    /// </summary>
    public class RangedGeometryTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        [Test]
        public void TheCellIsTheAdrsCell()
        {
            Assert.That(GridSize.CellSizeXZMm, Is.EqualTo(2500));
            Assert.That(GridSize.CellSizeYMm, Is.EqualTo(3000));
        }

        [Test]
        public void EachAxisCountsItsOwnCellSize()
        {
            int o = Size.Index(10, 10, 5);
            Assert.That(RangedGeometry.DistanceMm2(Size, o, Size.Index(11, 10, 5)), Is.EqualTo(2500L * 2500), "one cell in x");
            Assert.That(RangedGeometry.DistanceMm2(Size, o, Size.Index(10, 11, 5)), Is.EqualTo(2500L * 2500), "one cell in z");
            Assert.That(RangedGeometry.DistanceMm2(Size, o, Size.Index(10, 10, 6)), Is.EqualTo(3000L * 3000), "one layer up");
            Assert.That(RangedGeometry.DistanceMm2(Size, o, Size.Index(10, 10, 4)), Is.EqualTo(3000L * 3000), "one layer down");
            Assert.That(RangedGeometry.DistanceMm2(Size, o, o), Is.EqualTo(0L), "a cell from itself");

            // The three together, and either way round.
            int far = Size.Index(13, 14, 7);
            long expected = 7500L * 7500 + 10000L * 10000 + 6000L * 6000;
            Assert.That(RangedGeometry.DistanceMm2(Size, o, far), Is.EqualTo(expected));
            Assert.That(RangedGeometry.DistanceMm2(Size, far, o), Is.EqualTo(expected));

            // A 3-4-5 on the ground is exact.
            Assert.That(RangedGeometry.DistanceMm(Size, o, Size.Index(13, 14, 5)), Is.EqualTo(12_500));
            Assert.That(RangedGeometry.DistanceMm(Size, o, Size.Index(10, 10, 6)), Is.EqualTo(3_000), "height adds the storey");
        }

        [Test]
        public void TheScaleTargetsLongestLineFits()
        {
            GridSize size = GridSize.ScaleTarget;
            int a = size.Index(0, 0, 0), b = size.Index(size.SizeX - 1, size.SizeZ - 1, size.SizeY - 1);
            long d2 = RangedGeometry.DistanceMm2(size, a, b);
            long dx = 249L * 2500, dy = 39L * 3000;
            Assert.That(d2, Is.EqualTo(2 * dx * dx + dy * dy));
            long r = RangedGeometry.DistanceMm(size, a, b);
            Assert.That(r * r <= d2 && (r + 1) * (r + 1) > d2, Is.True, $"{r} is the floor of the root of {d2}");
        }

        [Test]
        public void TheRootIsExactOnPerfectSquaresAndTheFloorOtherwise()
        {
            long[] roots = { 0, 1, 2, 3, 12_500, 46_340, 46_341, 65_535, 65_536, 1_000_000, 3_037_000, int.MaxValue - 1L, int.MaxValue };
            foreach (long k in roots)
            {
                Assert.That(RangedGeometry.ISqrt(k * k), Is.EqualTo(k), $"{k} squared");
                if (k > 0) Assert.That(RangedGeometry.ISqrt(k * k - 1), Is.EqualTo(k - 1), $"one under {k} squared");
                Assert.That(RangedGeometry.ISqrt(k * k + 2 * k), Is.EqualTo(k), $"the last value whose root is {k}");
            }

            var rng = new Random(2026);
            for (int i = 0; i < 20_000; i++)
            {
                long v = (long)(rng.NextDouble() * RangedGeometry.ISqrtMax);
                if (i % 3 == 0) v = rng.Next();
                long r = RangedGeometry.ISqrt(v);
                Assert.That(r * r <= v && (r + 1) * (r + 1) > v, Is.True, $"floor root of {v}, got {r}");
            }
        }

        [Test]
        public void TheRootRefusesWhatHasNoIntRoot()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RangedGeometry.ISqrt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => RangedGeometry.ISqrt(RangedGeometry.ISqrtMax + 1));
            Assert.That(RangedGeometry.ISqrt(RangedGeometry.ISqrtMax), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void TheDoubleRootItReplacesIsWrongWhereThisIsRight()
        {
            // Control: 2^62 − 1 is not a double, rounds up to 2^62, and its double root is 2^31 — one
            // more than the true floor, which does not fit an int.
            long v = RangedGeometry.ISqrtMax;
            long viaDouble = (long)Math.Sqrt(v);
            Assert.That(viaDouble, Is.EqualTo((long)int.MaxValue + 1), "the double root overshoots");
            Assert.That(RangedGeometry.ISqrt(v), Is.EqualTo(int.MaxValue), "the integer root does not");
        }
    }
}
