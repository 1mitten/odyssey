#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The bed's drawn shape, as a set of boxes that must touch each other.
    ///
    /// <para><b>Written because the pillow floated.</b> The first cut put the mattress at
    /// <c>z = ±1.02</c> and the pillow at <c>z = −1.75</c>, leaving 0.56 m of open air between
    /// them and the pillow hanging past the end of the bed (owner, 2026-09-17: <i>"the mattress /
    /// base needs to extend to underneath the pillow as the pillow is simply floating in mid
    /// air"</i>). Nothing failed, because six numbers that do not add up are still six numbers —
    /// which is exactly the class of fault a contact sheet finds and a test does not, unless the
    /// test is told what "resting on" means.</para>
    ///
    /// <para>So these assert the relations rather than the numbers: the pillow sits on the
    /// mattress and within it, the mattress sits on the frame and within it, and the whole thing
    /// stays inside the two cells it was ordered in. Any of the six can be tuned freely; none of
    /// them can be tuned into mid-air.</para>
    /// </summary>
    public class BedShapeTests
    {
        const int Facing = Directions.North;

        /// <summary>The axis-aligned box a part occupies, in the bed's own local space.</summary>
        static Bounds LocalBox(int part)
        {
            // An identity root and a north facing, so the matrix is the part's local placement and
            // nothing else: this is about how the three boxes sit against one another, which is
            // true wherever on the board the bed stands.
            //
            // The module's OWN local matrix is composed in, exactly as ChunkRenderer does when it
            // draws a part, because that is where the geometry actually ends up. Leaving it out is
            // not a small error: a part's box then has the module's size divided out of it and its
            // centre left at the origin, and the first version of this fixture failed every
            // assertion for that reason while the bed itself was correct.
            ModuleLibrary.GetFallbackBox(
                BedShape.IsPillow(part) ? ModuleShape.Pillow : ModuleShape.SolidBlock,
                out Vector3 size, out Vector3 centre);
            Matrix4x4 local = Matrix4x4.TRS(centre, Quaternion.identity, size);

            Matrix4x4 at = BedShape.Part(Matrix4x4.identity, Facing, part) * local;

            var box = new Bounds(at.MultiplyPoint3x4(new Vector3(-0.5f, -0.5f, -0.5f)), Vector3.zero);
            for (int i = 1; i < 8; i++)
                box.Encapsulate(at.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? -0.5f : 0.5f,
                    (i & 2) == 0 ? -0.5f : 0.5f,
                    (i & 4) == 0 ? -0.5f : 0.5f)));
            return box;
        }

        [Test]
        public void ThePillowRestsOnTheMattressRatherThanInMidAir()
        {
            Bounds mattress = LocalBox(1);
            Bounds pillow = LocalBox(BedShape.PillowPart);

            Assert.That(pillow.min.y, Is.LessThanOrEqualTo(mattress.max.y),
                "the pillow's underside must reach the mattress, or it floats");
            Assert.That(pillow.min.y, Is.GreaterThan(mattress.min.y),
                "and must not have sunk through it");
        }

        [Test]
        public void TheMattressReachesUnderTheWholePillow()
        {
            Bounds mattress = LocalBox(1);
            Bounds pillow = LocalBox(BedShape.PillowPart);

            Assert.That(mattress.min.z, Is.LessThanOrEqualTo(pillow.min.z),
                "the head of the mattress must reach past the head of the pillow");
            Assert.That(mattress.max.z, Is.GreaterThanOrEqualTo(pillow.max.z));
            Assert.That(mattress.min.x, Is.LessThanOrEqualTo(pillow.min.x),
                "and the pillow must not be wider than what it lies on");
            Assert.That(mattress.max.x, Is.GreaterThanOrEqualTo(pillow.max.x));
        }

        [Test]
        public void TheMattressRestsOnTheFrameAndTheFrameOnTheFloor()
        {
            Bounds frame = LocalBox(0);
            Bounds mattress = LocalBox(1);

            Assert.That(frame.min.y, Is.EqualTo(0f).Within(0.01f), "the frame stands on the floor");
            Assert.That(mattress.min.y, Is.LessThanOrEqualTo(frame.max.y),
                "the mattress must reach the frame it lies on");
            Assert.That(mattress.min.z, Is.GreaterThanOrEqualTo(frame.min.z),
                "and must not overhang it");
            Assert.That(mattress.max.z, Is.LessThanOrEqualTo(frame.max.z));
        }

        /// <summary>
        /// The whole bed stays inside the two cells it was ordered in. A part that reached into a
        /// third cell would be drawn standing in a cell the simulation never reserved — which the
        /// player would read as the bed being there, and the pathfinder would not.
        /// </summary>
        [Test]
        public void TheBedStaysInsideItsOwnTwoCells()
        {
            float halfLength = CellMetrics.SizeXZ;      // two cells, measured from the middle
            float halfWidth = CellMetrics.HalfXZ;

            for (int part = 0; part < BedShape.PartCount; part++)
            {
                Bounds box = LocalBox(part);
                Assert.That(box.min.z, Is.GreaterThanOrEqualTo(-halfLength), $"part {part} runs off the head");
                Assert.That(box.max.z, Is.LessThanOrEqualTo(halfLength), $"part {part} runs off the foot");
                Assert.That(box.min.x, Is.GreaterThanOrEqualTo(-halfWidth), $"part {part} is too wide");
                Assert.That(box.max.x, Is.LessThanOrEqualTo(halfWidth), $"part {part} is too wide");
                Assert.That(box.min.y, Is.GreaterThanOrEqualTo(-0.01f), $"part {part} is below the floor");
            }
        }

        /// <summary>
        /// The pillow is at the <b>head</b> — the cell the order named, which is the cell the
        /// sleep chooser sends a colonist to. A bed drawn with its pillow at the foot would be
        /// telling the player the wrong end of it is the end you lie at.
        /// </summary>
        [Test]
        public void ThePillowIsAtTheHeadEnd()
        {
            Assert.That(LocalBox(BedShape.PillowPart).center.z, Is.LessThan(0f),
                "local -Z is the head, and that is where the pillow goes");
        }

        /// <summary>
        /// The bracket a selection draws turns with the bed, so it is never a box the wrong way
        /// round over a bed the right way round.
        /// </summary>
        [Test]
        public void TheSelectionBracketTurnsWithTheBed()
        {
            BedShape.WorldBounds(4, 4, 1, Directions.North, out _, out Vector3 northward);
            BedShape.WorldBounds(4, 4, 1, Directions.East, out _, out Vector3 eastward);

            Assert.That(northward.z, Is.GreaterThan(northward.x), "a north-facing bed is long in Z");
            Assert.That(eastward.x, Is.GreaterThan(eastward.z), "an east-facing bed is long in X");
            Assert.That(eastward.x, Is.EqualTo(northward.z).Within(0.001f), "and the same bed either way");
        }

        /// <summary>
        /// The pillow is the one part with a module of its own, because it is the one part that is
        /// a different shape and a different colour from the bed it lies on.
        /// </summary>
        [Test]
        public void OnlyThePillowIsLinen()
        {
            Assert.That(BedShape.IsPillow(BedShape.PillowPart), Is.True);
            for (int part = 0; part < BedShape.PartCount; part++)
                if (part != BedShape.PillowPart)
                    Assert.That(BedShape.IsPillow(part), Is.False, $"part {part} is not bedding");
        }
    }
}
