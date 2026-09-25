#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The rules that place the Meadow dressing (the look pass, design 38 §17). They are pure
    /// functions of the cell, so everything that matters about them can be pinned without a GPU:
    /// the same answer every time, nothing when the grass ladder is off, the big pieces on their
    /// own lattices so a bush and a stand of grass never share a cell, patches rather than a
    /// uniform sprinkle, and the giant tree kept rare.
    /// </summary>
    public class MeadowDressingTests
    {
        const int Side = 200;

        [Test]
        public void TheSameCellIsDressedTheSameWayEveryTime()
        {
            for (int z = 0; z < 40; z++)
            for (int x = 0; x < 40; x++)
            {
                Assert.That(MeadowDressing.BigPiece(x, z, 1f, woodEdge: false),
                    Is.EqualTo(MeadowDressing.BigPiece(x, z, 1f, woodEdge: false)));
                for (int slot = 0; slot < MeadowDressing.SmallSlots; slot++)
                    Assert.That(MeadowDressing.SmallPiece(x, z, slot, 1f, nearRock: false),
                        Is.EqualTo(MeadowDressing.SmallPiece(x, z, slot, 1f, nearRock: false)));
            }
        }

        [Test]
        public void TheGrassLadderAtOffDressesNothing()
        {
            for (int z = 0; z < 60; z++)
            for (int x = 0; x < 60; x++)
            {
                Assert.That(MeadowDressing.BigPiece(x, z, 0f, woodEdge: true), Is.EqualTo(MeadowDressing.Kind.None));
                for (int slot = 0; slot < MeadowDressing.SmallSlots; slot++)
                    Assert.That(MeadowDressing.SmallPiece(x, z, slot, 0f, nearRock: true), Is.EqualTo(MeadowDressing.Kind.None));
            }
        }

        /// <summary>
        /// The dressing strews no bushes of its own any more: they are the simulation's (design
        /// 45 §4), placed on the even lattice by the undergrowth pass and drawn from their edifice.
        /// The tall grass keeps to its own half of the checkerboard.
        /// </summary>
        [Test]
        public void TheDressingLeavesTheBushLatticeToTheSimulation()
        {
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                MeadowDressing.Kind big = MeadowDressing.BigPiece(x, z, 5f, woodEdge: true);
                Assert.That(big, Is.Not.EqualTo(MeadowDressing.Kind.Bush), $"a drawn bush at ({x},{z})");
                if (big == MeadowDressing.Kind.TallGrass)
                    Assert.That(((x + z) & 1) == 1, Is.True, $"a grass stand at ({x},{z}) is off its lattice");
            }
        }

        /// <summary>
        /// Patches, not confetti: the tall grass is much denser in one part of the board than
        /// another. A uniform sprinkle would show the same share in every block.
        /// </summary>
        [Test]
        public void TallGrassComesInStandsRatherThanASprinkle()
        {
            const int block = 20;
            float lowest = 1f, highest = 0f;
            for (int bz = 0; bz < Side / block; bz++)
            for (int bx = 0; bx < Side / block; bx++)
            {
                int stands = 0, offered = 0;
                for (int z = bz * block; z < (bz + 1) * block; z++)
                for (int x = bx * block; x < (bx + 1) * block; x++)
                {
                    if (((x + z) & 1) == 0) continue;
                    offered++;
                    if (MeadowDressing.BigPiece(x, z, 1f, woodEdge: false) == MeadowDressing.Kind.TallGrass) stands++;
                }
                float share = stands / (float)offered;
                if (share < lowest) lowest = share;
                if (share > highest) highest = share;
            }
            Assert.That(highest - lowest, Is.GreaterThan(0.25f),
                $"tall grass covers {lowest:P0} to {highest:P0} of blocks — too even to read as stands");
        }

        [Test]
        public void MoreOfTheLadderMeansMoreDressing()
        {
            int Count(float density)
            {
                int n = 0;
                for (int z = 0; z < Side; z++)
                for (int x = 0; x < Side; x++)
                {
                    if (MeadowDressing.BigPiece(x, z, density, woodEdge: false) != MeadowDressing.Kind.None) n++;
                    for (int slot = 0; slot < MeadowDressing.SmallSlots; slot++)
                        if (MeadowDressing.SmallPiece(x, z, slot, density, nearRock: false) != MeadowDressing.Kind.None) n++;
                }
                return n;
            }
            int meadow = Count(1f), full = Count(5f);
            Assert.That(meadow, Is.GreaterThan(0));
            Assert.That(full, Is.GreaterThan(meadow * 3 / 2), $"Full placed {full} against Meadow's {meadow}");
        }

        [Test]
        public void StonesGatherBesideRock()
        {
            int near = 0, far = 0;
            for (int z = 0; z < Side; z++)
            for (int x = 0; x < Side; x++)
            {
                if (MeadowDressing.SmallPiece(x, z, 2, 1f, nearRock: true) == MeadowDressing.Kind.Rock) near++;
                if (MeadowDressing.SmallPiece(x, z, 2, 1f, nearRock: false) == MeadowDressing.Kind.Rock) far++;
            }
            // The loose stones are the simulation's now (design 45 §6): stacks of stone at these
            // spots, drawn as the heap they are. The dressing draws none of its own.
            Assert.That(near + far, Is.Zero, "the dressing strews no stones of its own");
        }
    }
}
