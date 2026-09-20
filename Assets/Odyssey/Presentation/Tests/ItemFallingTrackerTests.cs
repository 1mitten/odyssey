#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    [TestFixture]
    public class ItemFallingTrackerTests
    {
        static WorldSnapshot MakeSnapshot(params (int id, int x, int z, int y)[] items)
        {
            var snap = new WorldSnapshot();
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                snap.AddThing(new ThingView(new ThingId(it.id), new CellRef(it.x, it.z, it.y), 0, 0, 1));
            }
            return snap;
        }

        [Test]
        public void NewFallIsDetectedWhenItemYDrops()
        {
            var tracker = new ItemFallingTracker();

            // Frame 1: Item 42 is on layer 3
            tracker.UpdateSnapshot(MakeSnapshot((42, 5, 5, 3)));
            Assert.That(tracker.ActiveFallCount, Is.EqualTo(0));

            // Frame 2: Item 42 drops to layer 1
            tracker.UpdateSnapshot(MakeSnapshot((42, 5, 5, 1)));
            Assert.That(tracker.ActiveFallCount, Is.EqualTo(1));

            Assert.That(tracker.TryGetFallingOffset(42, out Vector3 offset), Is.True);
            // Height drop is (3 - 1) * 3m = 6m
            Assert.That(offset.y, Is.EqualTo(6.0f).Within(1e-4f));
        }

        [Test]
        public void OffsetReachesZeroAndFinishes()
        {
            var tracker = new ItemFallingTracker();
            tracker.UpdateSnapshot(MakeSnapshot((10, 2, 2, 2)));
            tracker.UpdateSnapshot(MakeSnapshot((10, 2, 2, 1)));

            float duration = ItemFallMotion.Duration(3.0f);

            // Advance halfway: quadratic easing means offset is 75% of total height (25% fallen)
            tracker.Advance(duration * 0.5f);
            Assert.That(tracker.TryGetFallingOffset(10, out Vector3 midOffset), Is.True);
            Assert.That(midOffset.y, Is.EqualTo(3.0f * 0.75f).Within(1e-4f));

            // Advance to end of fall
            tracker.Advance(duration * 0.5f);
            Assert.That(tracker.ActiveFallCount, Is.EqualTo(0), "fall is finished and cleared");
            Assert.That(tracker.TryGetFallingOffset(10, out _), Is.False);
        }

        [Test]
        public void TouchdownEventFiresOnLandingFrame()
        {
            var tracker = new ItemFallingTracker();
            tracker.UpdateSnapshot(MakeSnapshot((7, 4, 4, 2)));
            tracker.UpdateSnapshot(MakeSnapshot((7, 4, 4, 1)));

            float duration = ItemFallMotion.Duration(3.0f);
            const float step = 1f / 60f;

            int landedCount = 0;
            Vector3 landedPos = Vector3.zero;
            tracker.ItemLanded += pos =>
            {
                landedCount++;
                landedPos = pos;
            };

            for (float elapsed = 0f; elapsed < duration * 1.5f; elapsed += step)
            {
                tracker.Advance(step);
            }

            Assert.That(landedCount, Is.EqualTo(1), "landing event triggered exactly once");
            Vector3 expectedLanding = CellMetrics.FloorCentre(4, 4, 1);
            Assert.That(landedPos, Is.EqualTo(expectedLanding));
        }

        [Test]
        public void HorizontalOrUpwardMoveDoesNotTriggerFall()
        {
            var tracker = new ItemFallingTracker();

            // Horizontal move
            tracker.UpdateSnapshot(MakeSnapshot((1, 5, 5, 2)));
            tracker.UpdateSnapshot(MakeSnapshot((1, 6, 5, 2)));
            Assert.That(tracker.ActiveFallCount, Is.EqualTo(0));

            // Upward move
            tracker.UpdateSnapshot(MakeSnapshot((1, 6, 5, 3)));
            Assert.That(tracker.ActiveFallCount, Is.EqualTo(0));
        }
    }
}
