#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The birds' Unity half (design 50): where the render mirror says a bird may sit, and what the
    /// director submits. The flock itself is fast-tier tested (<c>BirdTests</c>); this is the seam
    /// it is stepped against in the game and the two calls it ends in.
    /// </summary>
    public class BirdDirectorTests
    {
        const long Noon = Calendar.TicksPerHour * 12L;

        /// <summary>
        /// Ground on layer 0 everywhere; a tree standing on layer 1 at (3, 3); a roof over open air
        /// at (5, 5, 2); a floor laid straight on the ground at (6, 6, 1).
        /// </summary>
        static RenderTestWorld Board()
        {
            var world = new RenderTestWorld(10, 10, 4);
            for (int x = 0; x < 10; x++)
            for (int z = 0; z < 10; z++)
                world.Solid(x, z, 0);
            world.Edifice(3, 3, 1, NaturalContent.EdificeTreeMeadow, NaturalContent.StuffWood, blocking: true);
            world.Slab(5, 5, 2);
            world.Slab(6, 6, 1);
            return world.Publish();
        }

        [Test]
        public void ATreeIsAPerchAtItsCrownAndTheOpenGroundIsNot()
        {
            var perches = new BirdPerches(Board().Model);

            Assert.That(perches.TryPerchAt(3, 3, out BirdPerch crown), Is.True);
            Assert.That(crown.Y, Is.GreaterThan(CellMetrics.SizeY + 0.5f),
                "the perch is up in the crown, above the floor the trunk stands on");
            Assert.That(perches.TryPerchAt(8, 8, out _), Is.False, "a bird never sits on bare ground");
            Assert.That(perches.Holds(crown), Is.True);
        }

        [Test]
        public void ARoofIsAPerchAndAFloorLaidOnTheGroundIsNot()
        {
            var perches = new BirdPerches(Board().Model);

            Assert.That(perches.TryPerchAt(5, 5, out BirdPerch roof), Is.True);
            Assert.That(roof.Y, Is.EqualTo(2 * CellMetrics.SizeY + CellMetrics.SlabLift).Within(0.01f));
            Assert.That(perches.TryPerchAt(6, 6, out _), Is.False,
                "a patio is a slab to the rain, and not somewhere a flock comes down");
        }

        [Test]
        public void APerchAboveWhatTheSliceDrawsDoesNotHold()
        {
            var perches = new BirdPerches(Board().Model);
            Assert.That(perches.TryPerchAt(5, 5, out BirdPerch roof), Is.True);

            perches.HighestVisibleLayer = 1;
            Assert.That(perches.Holds(roof), Is.False, "a rook is not left sitting on a roof that has been cut away");
            Assert.That(perches.TryPerchAt(5, 5, out _), Is.False);
        }

        [Test]
        public void TheNearestPerchesComeFirst()
        {
            var perches = new BirdPerches(Board().Model);
            var found = new BirdPerch[4];
            int n = perches.Near(5 * CellMetrics.SizeXZ, 5 * CellMetrics.SizeXZ, 30f, found);

            Assert.That(n, Is.EqualTo(2), "the tree and the roof, and not the patio");
            float d0 = Mathf.Abs(found[0].X - 5 * CellMetrics.SizeXZ) + Mathf.Abs(found[0].Z - 5 * CellMetrics.SizeXZ);
            float d1 = Mathf.Abs(found[1].X - 5 * CellMetrics.SizeXZ) + Mathf.Abs(found[1].Z - 5 * CellMetrics.SizeXZ);
            Assert.That(d0, Is.LessThanOrEqualTo(d1));
        }

        [Test]
        public void TheMeshIsTheShapeWithItsColoursAndWingWeights()
        {
            Mesh mesh = BirdDirector.BuildMesh(BirdSpecies.Rook);
            try
            {
                Assert.That(mesh.triangles.Length / 3, Is.EqualTo(BirdShape.TriangleCount(BirdSpecies.Rook)));
                Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), "the wing weight rides in uv.x");
                Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(0.1f), "a unit wingspan; the matrix sizes it");
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void EverySpeciesIsOneCallAndNothingIsDrawnBelowTheSurface()
        {
            var director = new BirdDirector(Board().Model);
            try
            {
                if (!director.Available) Assert.Ignore("Odyssey/Bird did not load in this editor");

                director.Sync(0.1f, Noon, WeatherView.None, default, 60f, underground: false, highestVisibleLayer: 3);
                int inSky = director.Sky.Count - director.Sky.CountIn(BirdState.Away);
                Assert.That(director.LastDrawn, Is.EqualTo(inSky));
                Assert.That(director.LastDrawCalls, Is.EqualTo(BirdSpecies.All.Length), "rooks in one call, the buzzard in another");

                director.Sync(0.1f, Noon, WeatherView.None, default, 60f, underground: true, highestVisibleLayer: 3);
                Assert.That(director.LastDrawCalls, Is.Zero);
            }
            finally
            {
                director.Dispose();
            }
        }

        [Test]
        public void ABirdGrowsAsTheCameraPullsBack()
        {
            var director = new BirdDirector(Board().Model);
            try
            {
                director.Sync(0.1f, Noon, WeatherView.None, default, 15f, false, 3);
                Assert.That(director.LastScale, Is.EqualTo(1f));
                director.Sync(0.1f, Noon, WeatherView.None, default, 160f, false, 3);
                Assert.That(director.LastScale, Is.EqualTo(BirdScale.FarScale));
            }
            finally
            {
                director.Dispose();
            }
        }
    }
}
