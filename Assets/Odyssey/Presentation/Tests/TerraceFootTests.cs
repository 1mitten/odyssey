#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// <b>One rule, two owners, held together here.</b>
    ///
    /// <para><see cref="TerraceFoot"/> is the simulation's copy of the question
    /// <c>BankLayout.At(...).Exists</c> answers: is this cell the foot of a terrace step, the cell a
    /// bank ramp fills? It exists because worldgen has to refuse to grow a tree in one and worldgen
    /// has no render mirror to ask — the two read different data structures, so the rule could not
    /// simply be called from one place.</para>
    ///
    /// <para>This project has been bitten by a rule with two owners often enough to have a pattern
    /// for it (<c>docs/bug-patterns.md</c>), and the shape of the bite is always the same: both
    /// copies are individually correct, they disagree in one case nobody enumerated, and the symptom
    /// looks like an art fault. So the copies are not trusted to agree — they are <i>required</i> to,
    /// over every cell of every board below, including the ones where the interesting answer is
    /// "no bank": a two-layer riser, a sheer rock face, a quarry, ground under a roof.</para>
    ///
    /// <para>If one of these fails, fix whichever copy is wrong. Do not relax the assertion: a
    /// disagreement is a tree sheared off by a hillside, or a bank with a hole in it.</para>
    /// </summary>
    public class TerraceFootTests
    {
        [SetUp]
        public void Reset() => BankLayout.Reset();

        [TearDown]
        public void Restore() => BankLayout.Reset();

        /// <summary>
        /// Walk the whole board and require the two answers to be the same in every cell, naming
        /// the cell and both answers when they are not.
        /// </summary>
        static void AssertAgrees(RenderTestWorld world, string board)
        {
            int feet = 0;

            for (int y = 0; y < world.Size.SizeY; y++)
            for (int z = 0; z < world.Size.SizeZ; z++)
            for (int x = 0; x < world.Size.SizeX; x++)
            {
                bool drawn = BankLayout.At(world.Model, x, z, y).Exists;
                bool simulated = TerraceFoot.IsFoot(world.Grid, x, z, y);

                Assert.That(simulated, Is.EqualTo(drawn),
                    $"{board}: at ({x}, {z}, {y}) the mesher says bank={drawn} and the simulation " +
                    $"says foot={simulated}; the two copies of the terrace rule have drifted");

                if (drawn) feet++;
            }

            // A board on which neither owner finds anything proves nothing about their agreement,
            // and three of the fixtures below are deliberately of that kind. The callers that
            // expect banks say so themselves; this only guards against a fixture that quietly
            // stopped having a terrace in it.
            TestContext.WriteLine($"{board}: {feet} terrace feet");
        }

        [Test]
        public void AOneLayerStepAgrees()
        {
            RenderTestWorld world = RenderTestWorld.Terrace(rise: 1, NaturalContent.TerrainGrass);
            AssertAgrees(world, "a one-layer step");
            Assert.That(TerraceFeet(world), Is.EqualTo(6), "the step is six cells long");
        }

        [Test]
        public void FlatGroundHasNoFeetAtAll()
        {
            RenderTestWorld world = RenderTestWorld.Terrace(rise: 0, NaturalContent.TerrainGrass);
            AssertAgrees(world, "flat ground");
            Assert.That(TerraceFeet(world), Is.Zero, "flat ground grew a terrace foot");
        }

        [Test]
        public void ATwoLayerRiserIsNotAStep()
        {
            // The hop is one layer. A two-layer riser is a cliff, nothing draws a ramp up it, and
            // a tree at its foot is standing at the bottom of a wall rather than inside a wedge.
            RenderTestWorld world = RenderTestWorld.Terrace(rise: 2, NaturalContent.TerrainGrass);
            AssertAgrees(world, "a two-layer riser");
            Assert.That(TerraceFeet(world), Is.Zero, "a cliff was treated as a step");
        }

        [Test]
        public void ARockFaceIsNotAStep()
        {
            // Earth spills; stone does not. This is the case that made NaturalContent.IsEarth the
            // one list — a second copy of it here would have been the drift this file exists to
            // catch, one level down.
            RenderTestWorld world = RenderTestWorld.Terrace(rise: 1, CoreContent.TerrainRock);
            AssertAgrees(world, "a rock face");
            Assert.That(TerraceFeet(world), Is.Zero, "a sheer rock face grew a ramp");
        }

        [Test]
        public void APlateauCornerAgreesAllTheWayRound()
        {
            // Both corner cases at once: the inside corner where two runs meet, and the cell
            // diagonally off the plateau, which touches no step orthogonally and still carries a
            // hip. The diagonal is the one a hand-written guard would have missed.
            RenderTestWorld world = Plateau();
            AssertAgrees(world, "a plateau corner");
            Assert.That(TerraceFeet(world), Is.EqualTo(7),
                "the plateau's two faces and its diagonal are seven cells");
        }

        [Test]
        public void ANotchAgrees()
        {
            const int n = 5;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x == 0 && z == 0 ? 1 : 2;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
            }
            world.Publish();

            AssertAgrees(world, "a notch in the high ground");
            Assert.That(TerraceFeet(world), Is.EqualTo(1), "the notch is one cell");
        }

        [Test]
        public void AQuarryAgrees()
        {
            // Nothing spills into a hole the colony cut, on either side of the seam. The floor a
            // miner leaves is discovered, and that is the mark both copies read.
            RenderTestWorld world = RenderTestWorld.Terrace(rise: 0, NaturalContent.TerrainGrass);
            world.Mine(2, 2, 1);
            world.Publish();

            AssertAgrees(world, "a quarry");
            Assert.That(TerraceFeet(world), Is.Zero, "a working grew a ramp");
        }

        [Test]
        public void GroundUnderARoofAgrees()
        {
            // A bank is an outdoor thing, and a slab overhead is what says the cell is not. The
            // grid stores a slab on the cell above the boundary it occupies, so both copies have
            // to walk upwards the same way — which is the part worth pinning.
            const int n = 6;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < n / 2 ? 2 : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
                world.Slab(x, z, 4);
            }
            world.Publish();

            AssertAgrees(world, "ground under a roof");
            Assert.That(TerraceFeet(world), Is.Zero, "a roofed step grew a ramp");
        }

        /// <summary>The plateau of <c>BankMeshTests</c>: a high quadrant, so the terrace turns a
        /// corner both ways.</summary>
        static RenderTestWorld Plateau()
        {
            const int n = 7;
            var world = new RenderTestWorld(n, n, 8);

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < 3 && z < 3 ? 2 : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, top, NaturalContent.TerrainGrass);
            }

            return world.Publish();
        }

        static int TerraceFeet(RenderTestWorld world)
        {
            int count = 0;
            for (int y = 0; y < world.Size.SizeY; y++)
            for (int z = 0; z < world.Size.SizeZ; z++)
            for (int x = 0; x < world.Size.SizeX; x++)
                if (TerraceFoot.IsFoot(world.Grid, x, z, y)) count++;
            return count;
        }
    }
}
