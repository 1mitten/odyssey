#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The hash that decides where grass grows.
    ///
    /// Two things have to hold and neither announces itself when broken. It must be **stable**,
    /// because a chunk is re-meshed whenever anything in it changes and grass that moved every
    /// time a wall went up would look like the world twitching. And it must be **patternless**,
    /// because a weak hash over grid coordinates produces stripes and checkerboards, which in a
    /// field of grass the eye finds instantly and reads as a worldgen fault rather than as a hash
    /// being reused past its strength.
    /// </summary>
    public class GroundScatterTests
    {
        [Test]
        public void TheSameCellAlwaysGetsTheSameGrass()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.That(GroundScatter.CountFor(17, 42, 120),
                    Is.EqualTo(GroundScatter.CountFor(17, 42, 120)));
            }

            GroundScatter.Placement(17, 42, 1, out float x1, out float z1, out float yaw1, out float s1);
            GroundScatter.Placement(17, 42, 1, out float x2, out float z2, out float yaw2, out float s2);

            Assert.That(x2, Is.EqualTo(x1));
            Assert.That(z2, Is.EqualTo(z1));
            Assert.That(yaw2, Is.EqualTo(yaw1));
            Assert.That(s2, Is.EqualTo(s1));
        }

        [Test]
        public void ZeroDensityIsBareGround()
        {
            for (int x = 0; x < 40; x++)
            for (int z = 0; z < 40; z++)
                Assert.That(GroundScatter.CountFor(x, z, 0), Is.Zero);
        }

        [Test]
        public void AWholeDensityIsExact()
        {
            // 100 means one each, with no coin to toss. Worth pinning: an off-by-one in the
            // remainder would give a field that is 1% bald and nobody would ever spot it.
            for (int x = 0; x < 40; x++)
            for (int z = 0; z < 40; z++)
                Assert.That(GroundScatter.CountFor(x, z, 100), Is.EqualTo(1), $"at {x},{z}");
        }

        [Test]
        public void AFractionalDensityAveragesOut()
        {
            int total = 0;
            const int Side = 120;
            for (int x = 0; x < Side; x++)
            for (int z = 0; z < Side; z++)
                total += GroundScatter.CountFor(x, z, 120);

            float mean = total / (float)(Side * Side);
            Assert.That(mean, Is.EqualTo(1.2f).Within(0.03f));
        }

        [Test]
        public void DensityIsCappedRatherThanUnbounded()
        {
            for (int x = 0; x < 40; x++)
            for (int z = 0; z < 40; z++)
                Assert.That(GroundScatter.CountFor(x, z, 100000),
                    Is.LessThanOrEqualTo(GroundScatter.MaxPerCell));
        }

        [Test]
        public void TuftsStayInsideTheirOwnCell()
        {
            for (int x = -30; x < 30; x++)
            for (int z = -30; z < 30; z++)
            for (int slot = 0; slot < GroundScatter.MaxPerCell; slot++)
            {
                GroundScatter.Placement(x, z, slot,
                    out float ox, out float oz, out float yaw, out float scale);

                // Half a cell would put a tuft's centre on the boundary. Stopping short is what
                // hides the grid rather than drawing attention to it.
                Assert.That(Mathf.Abs(ox), Is.LessThan(0.5f), $"x at {x},{z} slot {slot}");
                Assert.That(Mathf.Abs(oz), Is.LessThan(0.5f), $"z at {x},{z} slot {slot}");
                Assert.That(yaw, Is.InRange(0f, 360f));
                Assert.That(scale, Is.InRange(0.7f, 1.4f));
            }
        }

        [Test]
        public void NoTuftStandsWhereSomethingElseWould()
        {
            // Colonists, crates and ration stacks are all drawn at the cell centre, and a clump is
            // nearly two metres across. Scattered over the whole cell, clumps landed on top of
            // them and grass grew through people's legs — which reads as green light coming off
            // the grass and was reported as exactly that. The middle of a cell is spoken for.
            for (int x = -30; x < 30; x++)
            for (int z = -30; z < 30; z++)
            for (int slot = 0; slot < GroundScatter.MaxPerCell; slot++)
            {
                GroundScatter.Placement(x, z, slot, out float ox, out float oz, out _, out _);

                float distance = Mathf.Sqrt(ox * ox + oz * oz);
                Assert.That(distance, Is.GreaterThanOrEqualTo(GroundScatter.InnerRadius - 1e-4f),
                    $"tuft at {x},{z} slot {slot} is standing in the middle of the cell");
                Assert.That(distance, Is.LessThanOrEqualTo(GroundScatter.OuterRadius + 1e-4f),
                    $"tuft at {x},{z} slot {slot} has wandered out of its ring");
            }
        }

        [Test]
        public void NeighbouringCellsDoNotShareAPlacement()
        {
            // The failure this catches is a hash whose low bits track its input, which lays the
            // whole field out in diagonal stripes.
            GroundScatter.Placement(10, 10, 0, out float ax, out float az, out _, out _);
            GroundScatter.Placement(11, 10, 0, out float bx, out float bz, out _, out _);
            GroundScatter.Placement(10, 11, 0, out float cx, out float cz, out _, out _);

            Assert.That(Mathf.Abs(ax - bx) + Mathf.Abs(az - bz), Is.GreaterThan(0.02f));
            Assert.That(Mathf.Abs(ax - cx) + Mathf.Abs(az - cz), Is.GreaterThan(0.02f));
        }

        [Test]
        public void TheFieldHasNoCheckerboardInIt()
        {
            // A cheap, specific test for the specific way this goes wrong. Split the map by the
            // parity of x + z and count tufts in each half: a hash that leaks its input into its
            // low bits lands them almost entirely on one colour of the board.
            int even = 0, odd = 0;
            for (int x = 0; x < 120; x++)
            for (int z = 0; z < 120; z++)
            {
                int count = GroundScatter.CountFor(x, z, 150);
                if (((x + z) & 1) == 0) even += count; else odd += count;
            }

            float ratio = even / (float)odd;
            Assert.That(ratio, Is.EqualTo(1f).Within(0.05f),
                $"even {even} against odd {odd}: the scatter is following the grid");
        }

        [Test]
        public void EveryVariantGetsUsed()
        {
            var seen = new bool[3];
            for (int x = 0; x < 40; x++)
            for (int z = 0; z < 40; z++)
                seen[GroundScatter.VariantFor(x, z, 0, 3)] = true;

            Assert.That(seen, Is.All.True, "a variant that never comes up is art nobody ever sees");
        }

        [Test]
        public void ASingleVariantNeverIndexesPastItself()
        {
            Assert.That(GroundScatter.VariantFor(5, 9, 2, 1), Is.Zero);
        }
    }

}
