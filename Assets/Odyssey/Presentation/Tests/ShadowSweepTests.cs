#nullable enable

using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The shadow margin swept towards the sun (<c>docs/design/38-meadow-overhaul.md</c> §18).
    ///
    /// <para>A chunk outside the frustum still matters if its casters can shadow what is inside
    /// it, and a caster can only shadow along the light's path. <see cref="ChunkRenderer.SweptInside"/>
    /// is the whole of that rule; these pin its geometry. The picture proof is
    /// <c>FrameTimeTests.CullingDoesNotChangeThePicture</c>, noon and a low sun.</para>
    /// </summary>
    public class ShadowSweepTests
    {
        /// <summary>A box-shaped "frustum": the slab 0 ≤ x ≤ 10, all y and z, as two inward planes.</summary>
        static readonly Plane[] Slab =
        {
            new Plane(Vector3.right, Vector3.zero),
            new Plane(Vector3.left, new Vector3(10f, 0f, 0f)),
        };

        [Test]
        public void ABoxInsideIsInsideWithoutASweep()
        {
            Assert.That(ChunkRenderer.SweptInside(Slab, new Vector3(5f, 0f, 0f), Vector3.one, Vector3.zero), Is.True);
        }

        [Test]
        public void ABoxOutsideIsOutsideWithoutASweep()
        {
            Assert.That(ChunkRenderer.SweptInside(Slab, new Vector3(-20f, 0f, 0f), Vector3.one, Vector3.zero), Is.False);
        }

        /// <summary>Up-sun of the view: its shadow falls into it, so it is kept.</summary>
        [Test]
        public void ABoxWhoseShadowFallsIntoTheViewIsKept()
        {
            Assert.That(ChunkRenderer.SweptInside(Slab, new Vector3(-20f, 0f, 0f), Vector3.one, new Vector3(30f, 0f, 0f)),
                Is.True);
        }

        /// <summary>Down-sun of the view: its shadow falls away from it, so the old shell kept it
        /// for nothing and the sweep does not.</summary>
        [Test]
        public void ABoxWhoseShadowFallsAwayIsDropped()
        {
            Assert.That(ChunkRenderer.SweptInside(Slab, new Vector3(-20f, 0f, 0f), Vector3.one, new Vector3(-30f, 0f, 0f)),
                Is.False);
        }

        /// <summary>Too short a sweep does not reach: the length is a real bound, not a direction only.</summary>
        [Test]
        public void ASweepThatStopsShortIsDropped()
        {
            Assert.That(ChunkRenderer.SweptInside(Slab, new Vector3(-20f, 0f, 0f), Vector3.one, new Vector3(5f, 0f, 0f)),
                Is.False);
        }

        /// <summary>Straight through: a box on the far side swept across the whole slab still touches it.</summary>
        [Test]
        public void ASweepAcrossTheWholeViewIsKept()
        {
            Assert.That(ChunkRenderer.SweptInside(Slab, new Vector3(-20f, 0f, 0f), Vector3.one, new Vector3(60f, 0f, 0f)),
                Is.True);
        }
    }
}
