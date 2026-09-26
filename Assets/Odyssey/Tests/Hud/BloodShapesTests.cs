#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The outlines of the blood marks (design 33 §10a): flat, facing up, inside a unit radius so a
    /// placement's scale is the mark's radius, and a cut's splatter thrown ahead along the blow.
    /// </summary>
    public class BloodShapesTests
    {
        static readonly BloodShape[] Every = { BloodShape.Splatter, BloodShape.Spot, BloodShape.Pool };

        [Test]
        public void EveryTriangleFacesUp()
        {
            foreach (BloodShape shape in Every)
            {
                BloodShapes.Build(shape, out float[] x, out float[] z, out int[] t);
                Assert.That(t.Length % 3, Is.Zero);
                Assert.That(t.Length, Is.GreaterThan(0), $"{shape} has no triangles");
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    // The y of (b - a) x (c - a) with every y nought, as LockOnRingTests.TheRingFacesUp.
                    float up = (z[b] - z[a]) * (x[c] - x[a]) - (x[b] - x[a]) * (z[c] - z[a]);
                    Assert.That(up, Is.GreaterThan(0f), $"{shape} triangle {i / 3} faces down");
                }
            }
        }

        [Test]
        public void EveryShapeFitsInsideAUnitRadius()
        {
            foreach (BloodShape shape in Every)
            {
                BloodShapes.Build(shape, out float[] x, out float[] z, out _);
                float furthest = 0f;
                for (int i = 0; i < x.Length; i++)
                    furthest = Math.Max(furthest, (float)Math.Sqrt(x[i] * x[i] + z[i] * z[i]));
                Assert.That(furthest, Is.LessThanOrEqualTo(1f), $"{shape} spills past its radius");
                Assert.That(furthest, Is.GreaterThan(0.75f), $"{shape} is much smaller than its radius says");
            }
        }

        [Test]
        public void ASplatterIsThrownAheadAlongTheBlow()
        {
            BloodShapes.Build(BloodShape.Splatter, out float[] x, out _, out _);
            float ahead = 0f, behind = 0f;
            foreach (float v in x)
            {
                ahead = Math.Max(ahead, v);
                behind = Math.Max(behind, -v);
            }
            Assert.That(ahead, Is.GreaterThan(behind), "the satellite drops must lie ahead, the way the blow went");
        }

        [Test]
        public void TheShapesAreTheSameEveryTime()
        {
            foreach (BloodShape shape in Every)
            {
                BloodShapes.Build(shape, out float[] x1, out float[] z1, out int[] t1);
                BloodShapes.Build(shape, out float[] x2, out float[] z2, out int[] t2);
                Assert.That(x2, Is.EqualTo(x1));
                Assert.That(z2, Is.EqualTo(z1));
                Assert.That(t2, Is.EqualTo(t1));
            }
        }
    }
}
