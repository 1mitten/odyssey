#nullable enable

using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What a graphics preference means to the renderer, asked of the one place that says
    /// (<c>SettingsPresenter.ApplyRendererLevers</c>; design 38 §9, M6).
    ///
    /// <para><b>Why it exists.</b> Until 2026-09-24 nothing put the player's preferences on a
    /// session's renderer: the presenter attaches at the start screen, before there is one, so a
    /// stored "shadows off" was applied to nothing and every new game came up drawn as the scene's
    /// fields said. With presets that would have been a Low machine coming back as High after every
    /// restart. The composition root now calls this mapping as it builds a renderer; these hold the
    /// mapping itself.</para>
    /// </summary>
    public class GraphicsLeverTests
    {
        /// <summary>The vegetation ladder's top rung is full cover, and full cover is the scatter's
        /// own ceiling — held here because the settings assembly cannot see the scatter.</summary>
        [Test]
        public void TheTopGrassRungIsFullCover()
        {
            int[] rungs = SettingsDirector.RungsOf(GraphicsLadder.VegetationDensity);
            Assert.That(rungs[rungs.Length - 1], Is.EqualTo(GroundScatter.MaxPerCell * 100),
                "the Full rung is not what the scatter can place on every cell");
        }

        [Test]
        public void EveryRendererPreferenceReachesARenderer()
        {
            RenderTestWorld world = new RenderTestWorld(4, 4, 2).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };

            var settings = new SettingsDirector();
            settings.ApplyPreset(QualityPreset.Low);
            settings.Set(GraphicsOption.FoliageShadows, true);
            SettingsPresenter.ApplyRendererLevers(renderer, settings);

            Assert.That(renderer.CastShadows, Is.True);
            Assert.That(renderer.FoliageCastsShadows, Is.True);
            Assert.That(renderer.Skirt.Enabled, Is.False, "Low drops the surrounding land");
            Assert.That(renderer.ScatterDensity, Is.EqualTo(30));
            Assert.That(renderer.FoliageDrawDistance, Is.EqualTo(60f));

            settings.ApplyPreset(QualityPreset.Ultra);
            settings.Set(GraphicsOption.Shadows, false);
            SettingsPresenter.ApplyRendererLevers(renderer, settings);

            Assert.That(renderer.CastShadows, Is.False);
            Assert.That(renderer.Skirt.Enabled, Is.True);
            Assert.That(renderer.ScatterDensity, Is.EqualTo(GroundScatter.MaxPerCell * 100));
            Assert.That(float.IsPositiveInfinity(renderer.FoliageDrawDistance), Is.True,
                "the unlimited rung must mean no cut-off at all, not a very long one");
        }
    }
}
