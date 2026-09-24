#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The grass lies flat round what is on the ground (owner, 2026-09-24: "flatten grass so items
    /// can be seen clearer", <c>docs/design/38-meadow-overhaul.md</c> §19).
    /// </summary>
    public class GroundClearanceTests
    {
        [Test]
        public void AnItemsRingIsItsFootprintAndHalfAMetre()
        {
            Assert.That(ChunkRenderer.ItemRing(1.2f, 0.5f, 0.55f), Is.EqualTo(1.7f).Within(1e-5f));
        }

        [Test]
        public void ASmallItemNeverClearsLessThanTheOldRing()
        {
            Assert.That(ChunkRenderer.ItemRing(0f, 0.5f, 0.55f), Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(ChunkRenderer.ItemRing(0.02f, 0.0f, 0.55f), Is.EqualTo(0.55f).Within(1e-5f));
        }

        [Test]
        public void TheWiderRingClearsGrassTheOldOneLeft()
        {
            using var field = new GrassClearance();
            Vector3 at = new Vector3(40f, 0f, 40f);
            field.Begin(at);
            field.Stamp(at, ChunkRenderer.ItemRing(1.2f, 0.5f, 0.55f));

            Assert.That(field.At(at + new Vector3(1.3f, 0f, 0f)), Is.GreaterThan(0.5f),
                "grass 1.3 m from a stack's centre, inside its footprint ring, was not laid flat");
            Assert.That(field.At(at + new Vector3(3f, 0f, 0f)), Is.EqualTo(0f),
                "the ring reached past the footprint and the margin");
        }

        [Test]
        public void ADefaultRendererFlattensMoreThanTheOldRing()
        {
            var world = new RenderTestWorld(4, 4, 2).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            Assert.That(renderer.ItemMargin, Is.GreaterThan(0f));
            Assert.That(renderer.LyingClearance, Is.GreaterThan(renderer.ItemClearance),
                "a body lying down should clear more grass than a small item");
            Assert.That(renderer.ForceLyingForAPhotograph, Is.False, "the photograph switch shipped on");
        }

        [Test]
        public void NothingIsUnderABushOnAWorldWithoutDressing()
        {
            var world = new RenderTestWorld(30, 30, 3).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            renderer.Render(1, new Odyssey.Presentation.CameraRig.SliceSettings());
            Assert.That(renderer.UnderBush(new Vector3(10f, 3f, 10f), 1), Is.False);
            Assert.That(renderer.TryNearestBush(new Vector3(10f, 3f, 10f), 0, out _), Is.False);
        }
    }
}
