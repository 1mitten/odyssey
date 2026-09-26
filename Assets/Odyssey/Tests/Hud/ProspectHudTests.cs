#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface half of prospecting (design 62 §7): where the order is armed from, what the
    /// banner calls it and wears, and what a prospector's activity line reads.
    /// </summary>
    public class ProspectHudTests
    {
        [Test]
        public void TheOrderIsArmedFromStructureAndNamesItself()
        {
            int structure = System.Array.FindIndex(PaletteTools.Categories, c => c.key == "ui.arch.category.structure");
            Assert.That(PaletteTools.Categories[structure].tools, Does.Contain(PaletteTools.Prospect));
            Assert.That(PaletteTools.Pinned, Does.Not.Contain(PaletteTools.Prospect), "an eighth strip button does not fit (1280 x 720)");

            var designate = new DesignateDirector();
            var palette = new BuildPaletteModel(designate);
            Assert.That(PaletteTools.TryGet(PaletteTools.Prospect, out PaletteTool prospect), Is.True, "the tile is live");
            prospect.Arm(designate);

            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.Prospect));
            Assert.That(palette.ArmedOrder, Is.EqualTo(PaletteTools.Prospect), "the banner names the order");
            Assert.That(palette.ArmedWordKey, Is.EqualTo(PaletteTools.Prospect), "and never as Dig or Mine");
            Assert.That(Registry.Label(PaletteTools.Prospect), Is.EqualTo("Prospect"));
            Assert.That(HudTheme.ArmedOrderHue(PaletteTools.Prospect), Is.EqualTo(OrderColours.Hue(DesignateTool.Prospect)));
            Assert.That(HudTheme.PinnedActionHue(PaletteTools.Prospect), Is.Null, "it lights no button on the strip");

            prospect.Arm(designate);
            Assert.That(designate.Tool, Is.EqualTo(DesignateTool.None), "the tile puts the tool down the way it picked it up");
            Assert.That(palette.ArmedOrder, Is.Empty);
        }

        /// <summary>
        /// The table both askers read: every category order is live in its own category, off the
        /// strip, and arms the tool it names — so the banner and its colour cannot disagree.
        /// </summary>
        [Test]
        public void EveryCategoryOrderArmsTheToolItIsListedWith()
        {
            foreach ((string key, DesignateTool tool) in PaletteTools.CategoryOrders)
            {
                Assert.That(PaletteTools.Pinned, Does.Not.Contain(key), key);
                Assert.That(System.Array.Exists(PaletteTools.Categories, c => System.Array.IndexOf(c.tools, key) >= 0),
                    Is.True, $"{key} is in no category");
                Assert.That(PaletteTools.TryGet(key, out PaletteTool live), Is.True, key);

                var designate = new DesignateDirector();
                live.Arm(designate);
                Assert.That(designate.Tool, Is.EqualTo(tool), key);
                Assert.That(PaletteTools.CategoryOrderKey(tool), Is.EqualTo(key));
            }
        }

        [Test]
        public void AProspectorReadsProspecting()
        {
            Assert.That(JobLabels.IconKey(JobHandle.Prospect), Is.EqualTo(InspectModel.ProspectingKey));
            Assert.That(JobLabels.Label(JobHandle.Prospect), Is.EqualTo("Prospecting"));
        }
    }
}
