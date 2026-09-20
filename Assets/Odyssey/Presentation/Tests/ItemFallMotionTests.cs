#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    [TestFixture]
    public class ItemFallMotionTests
    {
        [Test]
        public void DurationScalesWithFallHeight()
        {
            Assert.That(ItemFallMotion.Duration(0f), Is.EqualTo(0f));
            Assert.That(ItemFallMotion.Duration(-1f), Is.EqualTo(0f));

            float oneLayer = ItemFallMotion.Duration(3.0f);
            float twoLayers = ItemFallMotion.Duration(6.0f);
            float fourLayers = ItemFallMotion.Duration(12.0f);

            Assert.That(oneLayer, Is.EqualTo(ItemFallMotion.BaseLayerDuration).Within(1e-4f));
            Assert.That(twoLayers, Is.GreaterThan(oneLayer));
            Assert.That(fourLayers, Is.GreaterThan(twoLayers));
            Assert.That(fourLayers, Is.LessThanOrEqualTo(ItemFallMotion.MaxDuration));
        }

        [Test]
        public void StartsAtUpperPointAndEndsAtTargetFloor()
        {
            Vector3 start = new Vector3(10f, 9f, 10f);
            Vector3 target = new Vector3(10f, 3f, 10f);
            float duration = ItemFallMotion.Duration(6.0f);

            Vector3 startOffset = ItemFallMotion.FallingOffset(start, target, 0f, duration);
            Assert.That(startOffset.y, Is.EqualTo(6f).Within(1e-4f), "at t=0, offset equals vertical distance");

            Vector3 endOffset = ItemFallMotion.FallingOffset(start, target, duration, duration);
            Assert.That(endOffset, Is.EqualTo(Vector3.zero), "at t=duration, offset is zero");
        }

        [Test]
        public void GravitationalAccelerationDownward()
        {
            float duration = ItemFallMotion.Duration(3.0f);
            float halfTime = duration * 0.5f;

            float progressAtHalf = ItemFallMotion.Fallen(halfTime, duration);
            Assert.That(progressAtHalf, Is.EqualTo(0.25f).Within(1e-4f),
                "quadratic gravity means at half duration only a quarter of the distance is covered");
        }

        [Test]
        public void LandingEventTriggersExactlyOnce()
        {
            float duration = ItemFallMotion.Duration(3.0f);
            const float step = 1f / 60f;

            int landings = 0;
            float before = 0f;
            for (float after = step; after < duration * 2f; after += step)
            {
                if (ItemFallMotion.FallLanded(before, after, duration)) landings++;
                before = after;
            }

            Assert.That(landings, Is.EqualTo(1), "the touchdown trigger fires on exactly one frame");
        }

        [Test]
        public void FallLandedHandlesLongFrame()
        {
            float duration = ItemFallMotion.Duration(3.0f);
            Assert.That(ItemFallMotion.FallLanded(0f, duration * 3f, duration), Is.True);
            Assert.That(ItemFallMotion.FallLanded(duration, duration * 3f, duration), Is.False);
        }
    }
}
