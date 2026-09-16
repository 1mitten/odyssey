#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a cloned material carries that the art's own material does not.
    ///
    /// The order things are drawn in is invisible in the code that submits them: a queue number
    /// on a material decides it, and a wrong number fails as a look — grass under the ink, or a
    /// ghosted storey vanishing over a meadow — with nothing logged. These pin the numbers.
    /// </summary>
    public class MaterialCacheTests
    {
        [Test]
        public void FoliageIsDrawnAfterTheOpaquesAndBeforeTheGhosts()
        {
            // After the opaque range, so it is after the depth copy the outline reads and after
            // the outline pass itself; before the ghost material, which URP's Transparent queue
            // holds, so a ghosted layer above still blends over the grass beneath it.
            Assert.That(MaterialCache.DefaultFoliageQueue, Is.GreaterThan((int)RenderQueue.GeometryLast));
            Assert.That(MaterialCache.DefaultFoliageQueue, Is.LessThan((int)RenderQueue.Transparent));
        }

        [Test]
        public void AFoliageCloneTakesTheFoliageQueueAndNothingElseDoes()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Assume.That(lit, Is.Not.Null, "URP Lit is what the fallback material is built from");
            var art = new Material(lit) { name = "art" };
            int artQueue = art.renderQueue;

            var cache = new MaterialCache();
            try
            {
                Material foliage = cache.Get(art, Color.white, Color.black, ghost: false, alpha: 1f, foliage: true);
                Material solid = cache.Get(art, Color.white, Color.black, ghost: false, alpha: 1f);

                Assert.That(foliage.renderQueue, Is.EqualTo(MaterialCache.FoliageQueue));
                Assert.That(solid.renderQueue, Is.EqualTo(artQueue), "a non-foliage clone keeps the art's own queue");
                Assert.That(art.renderQueue, Is.EqualTo(artQueue), "the licensed source material is never written to");
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(art);
            }
        }
    }
}
