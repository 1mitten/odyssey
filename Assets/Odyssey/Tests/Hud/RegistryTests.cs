#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The HUD names nothing itself: every key it shows must be one the naming registry knows,
    /// so a name that is not in <c>docs/design/icon-keys.csv</c> (and so not in the wiki) fails
    /// here, in the fast tier, before it can reach the screen.
    /// </summary>
    public class RegistryTests
    {
        [Test]
        public void EveryJobKeyIsARegisteredName()
        {
            foreach (string key in JobLabels.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            Assert.That(Registry.Labels, Does.ContainKey(JobLabels.IconKey(-1)), "the fallback key must be registered too");
        }

        [Test]
        public void EveryLedgerKeyIsARegisteredName()
        {
            foreach (string key in LedgerModel.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        [Test]
        public void EverySettingsKeyIsARegisteredName()
        {
            foreach (string key in SettingsDirector.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (GraphicsOption option in SettingsDirector.All)
                Assert.That(SettingsDirector.IconKeys, Does.Contain(SettingsDirector.KeyOf(option)),
                    "an option the panel can draw but the registry test does not cover is a label nobody checks");
        }

        /// <summary>
        /// A job with no icon key reads as <i>idle</i>, which is a lie rather than a gap.
        ///
        /// <para>The table is bounds-checked and falls through to <c>ui.status.idle</c>, so
        /// two missing entries are not a compile error — they are every builder and every
        /// porter in the game showing as having nothing to do. That happened when the build
        /// pipeline added two job indices, and this is what stops the next one.</para>
        /// </summary>
        [Test]
        public void EveryJobHasAStatusOfItsOwn()
        {
            Assert.That(JobLabels.IconKeys.Length, Is.EqualTo(JobHandle.Count),
                "a job the table does not cover draws as idle, silently");

            foreach (string key in JobLabels.IconKeys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
        }

        [Test]
        public void EveryBuildableAndMaterialHasARegisteredNameOrNoneAtAll()
        {
            Assert.That(BuildLabels.BuildingKeys.Length, Is.EqualTo(BuildingHandle.Count));
            Assert.That(BuildLabels.StuffKeys.Length, Is.EqualTo(StuffHandle.Count));

            // A blank is deliberate and means "the player can never be shown this" - the
            // three stuffs only the generator stamps, and the zero slot. Anything else must
            // be a name the wiki knows.
            foreach (string key in BuildLabels.BuildingKeys)
                if (key.Length > 0)
                    Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            foreach (string key in BuildLabels.StuffKeys)
                if (key.Length > 0)
                    Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");

            Assert.That(BuildLabels.Building(BuildingHandle.Wall), Is.EqualTo("Wall"));
            Assert.That(BuildLabels.Stuff(StuffHandle.Wood), Is.EqualTo("wood"),
                "a material is read inside a sentence, so it is lower case");
        }

        [Test]
        public void AnUnregisteredKeyShowsItselfRatherThanNothing()
        {
            Assert.That(Registry.Label("ui.status.hauling"), Is.EqualTo("Hauling"));
            Assert.That(Registry.Label("ui.nothing.of.the.kind"), Is.EqualTo("ui.nothing.of.the.kind"),
                "a raw key on screen is a visible fault; a blank is a silent one");
        }
    }
}
