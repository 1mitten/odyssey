#nullable enable

using System.IO;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Grass thinned with distance without a pop (design 38 §21): the rank each clump is given, the
    /// fraction kept at a distance, and the prefix the renderer submits from a rank-sorted bucket.
    /// The shader shrinks by the same rank, so the C# side is pinned here and the HLSL mirror is
    /// read out of the shader file.
    /// </summary>
    public class GrassThinningTests
    {
        [Test]
        public void ARankIsDeterministicAndInRange()
        {
            for (int i = 0; i < 1000; i++)
            {
                float x = i * 0.731f, z = i * 1.917f + 3f;
                float a = GrassThinning.Rank(x, z), b = GrassThinning.Rank(x, z);
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        /// <summary>The rank must spread evenly, or thinning to a fifth keeps a patchwork instead of
        /// an even fifth: the whole reason a multiply-and-shift hash was ruled out (lessons).</summary>
        [Test]
        public void RanksSpreadEvenlyOverAMeadow()
        {
            int below = 0, total = 0;
            for (int z = 0; z < 120; z++)
            for (int x = 0; x < 120; x++)
            for (int slot = 0; slot < 3; slot++)
            {
                GroundScatter.Placement(x, z, slot, out float ox, out float oz, out _, out _);
                float wx = (x + 0.5f + ox) * CellMetrics.SizeXZ, wz = (z + 0.5f + oz) * CellMetrics.SizeXZ;
                if (GrassThinning.Rank(wx, wz) < 0.2f) below++;
                total++;
            }
            Assert.That(below / (float)total, Is.EqualTo(0.2f).Within(0.02f),
                "a fifth of the ranks should fall below 0.2 across a meadow");
        }

        [Test]
        public void TheKeepIsFullNearFallsLinearlyAndHoldsFar()
        {
            Assert.That(GrassThinning.Keep(10f, 70f, 160f, 0.2f), Is.EqualTo(1f));
            Assert.That(GrassThinning.Keep(70f, 70f, 160f, 0.2f), Is.EqualTo(1f));
            Assert.That(GrassThinning.Keep(115f, 70f, 160f, 0.2f), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(GrassThinning.Keep(160f, 70f, 160f, 0.2f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(GrassThinning.Keep(900f, 70f, 160f, 0.2f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(GrassThinning.Keep(900f, 70f, 160f, 1f), Is.EqualTo(1f), "a keep of one never thins");
        }

        [Test]
        public void TheFarFieldIsDrawnAtTheMeadowRung()
        {
            Assert.That(GrassThinning.FarKeepFor(60, 60), Is.EqualTo(1f), "Meadow is never thinned");
            Assert.That(GrassThinning.FarKeepFor(30, 60), Is.EqualTo(1f), "nor anything sparser");
            Assert.That(GrassThinning.FarKeepFor(300, 60), Is.EqualTo(0.2f).Within(1e-6f),
                "Full keeps a fifth far out, which is the Meadow rung's density");
            Assert.That(GrassThinning.FarKeepFor(0, 60), Is.EqualTo(1f));
        }

        [Test]
        public void TheSubmittedPrefixIsExactlyTheClumpsBelowTheKeep()
        {
            var matrices = new Matrix4x4[500];
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = Matrix4x4.Translate(new Vector3(i * 0.37f, 3f, i * 0.91f + 1f));
            float[] keys = new float[4];
            GrassThinning.SortByRank(matrices, matrices.Length, ref keys);

            for (int i = 1; i < matrices.Length; i++)
                Assert.That(GrassThinning.Rank(in matrices[i]), Is.GreaterThanOrEqualTo(GrassThinning.Rank(in matrices[i - 1])),
                    "the bucket was not left in rank order");

            foreach (float keep in new[] { 0f, 0.05f, 0.2f, 0.5f, 0.999f, 1f })
            {
                int naive = 0;
                foreach (Matrix4x4 m in matrices) if (GrassThinning.Rank(in m) < keep) naive++;
                int expected = keep >= 1f ? matrices.Length : naive;
                Assert.That(GrassThinning.CountBelow(matrices, matrices.Length, keep), Is.EqualTo(expected), $"keep {keep}");
            }
        }

        /// <summary>The HLSL mirror (<c>FoliageRank</c>) uses the same quantisation and the same PCG
        /// constants, read out of the shader, as LeafVarietyTests does for the leaf colours.</summary>
        [Test]
        public void TheShaderRanksByTheSameHash()
        {
            string shader = File.ReadAllText("Assets/Odyssey/Presentation/Shaders/OdysseyFoliage.shader");
            foreach (string token in new[] { "747796405u", "2891336453u", "277803737u", "(word >> 22u) ^ word",
                         "floor(xz.x * 16.0)", "floor(xz.y * 16.0)", "FoliagePcg(ix + FoliagePcg(iz))",
                         "(h >> 8) * (1.0 / 16777216.0)", "#define ODYSSEY_THIN_SOFT 0.05" })
                Assert.That(shader, Does.Contain(token), $"the shader's rank no longer mirrors GrassThinning: {token}");
            Assert.That(GrassThinning.Soft, Is.EqualTo(0.05f));
        }
    }
}
