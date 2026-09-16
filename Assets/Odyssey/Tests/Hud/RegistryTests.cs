#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

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
        public void AnUnregisteredKeyShowsItselfRatherThanNothing()
        {
            Assert.That(Registry.Label("ui.status.hauling"), Is.EqualTo("Hauling"));
            Assert.That(Registry.Label("ui.nothing.of.the.kind"), Is.EqualTo("ui.nothing.of.the.kind"),
                "a raw key on screen is a visible fault; a blank is a silent one");
        }
    }
}
