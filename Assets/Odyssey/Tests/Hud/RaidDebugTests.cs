#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The raid's two debug controls and its Events row (design 50 §7, §9): what the row sends, what
    /// the size reads, and what the bulletin says.
    /// </summary>
    public class RaidDebugTests
    {
        [Test]
        public void TheRaidRowSendsTheSizeAndTheMixItHolds()
        {
            var debug = new DebugDirector();
            Intent auto = debug.RaidIntent(IncidentHandle.Raid);
            Assert.That(auto.Kind, Is.EqualTo(IntentKind.InvokeIncident));
            Assert.That(auto.A, Is.EqualTo(IncidentHandle.Raid));
            Assert.That(auto.B, Is.Zero, "a fresh panel leaves the size to the incident");
            Assert.That(auto.C, Is.EqualTo(RaidMixLabels.Default + 1), "a fresh panel names the Mixed band");

            debug.SetRaidSize(37);
            debug.SetRaidMix(1);
            Intent chosen = debug.RaidIntent(IncidentHandle.Raid);
            Assert.That(chosen.B, Is.EqualTo(37));
            Assert.That(chosen.C, Is.EqualTo(2));
        }

        [Test]
        public void TheSizeIsClampedAndAMixOffTheListIsIgnored()
        {
            var debug = new DebugDirector();
            debug.SetRaidSize(-5);
            Assert.That(debug.RaidSize, Is.Zero);
            debug.SetRaidSize(DebugDirector.RaidSizeMax + 50);
            Assert.That(debug.RaidSize, Is.EqualTo(DebugDirector.RaidSizeMax));
            Assert.That(DebugDirector.RaidSizeMax, Is.EqualTo(200), "the owner's slider runs to 200");

            debug.SetRaidMix(0);
            debug.SetRaidMix(RaidMixLabels.Keys.Length);
            Assert.That(debug.RaidMix, Is.Zero, "a mix past the list moved the choice");
        }

        [Test]
        public void TheSizeReadsAutoAtNoughtAndTheNumberOtherwise()
        {
            Assert.That(DebugDirector.RaidSizeText(0), Is.EqualTo(Registry.Label(DebugDirector.RaidAutoKey)));
            Assert.That(DebugDirector.RaidSizeText(0), Is.EqualTo("Auto"));
            Assert.That(DebugDirector.RaidSizeText(120), Is.EqualTo("120"));
        }

        /// <summary>The mix names, in the content's order: bandits, gunmen, mixed. Every one is registered.</summary>
        [Test]
        public void EveryMixIsARegisteredNameInTheContentsOrder()
        {
            Assert.That(RaidMixLabels.Keys, Is.EqualTo(new[] { "ui.raid.mix.bandits", "ui.raid.mix.gunmen", "ui.raid.mix.mixed" }));
            foreach (string key in RaidMixLabels.Keys)
                Assert.That(Registry.Labels, Does.ContainKey(key), $"{key} is not in the registry");
            Assert.That(RaidMixLabels.Keys[RaidMixLabels.Default], Is.EqualTo("ui.raid.mix.mixed"));
        }

        /// <summary>
        /// A raid's row names its mix and size, "Raid warning · Mixed × 20", and never reads its mix
        /// as an item the way a theft's subject is read. The theft's title is the control.
        /// </summary>
        [Test]
        public void ARaidsEventsRowNamesItsMixAndSize()
        {
            string key = IncidentLabels.IconKey(IncidentHandle.Raid);
            Assert.That(key, Is.EqualTo("ui.bulletin.raidincoming"));
            Assert.That(BulletinModel.RaidTitle(key, 2, 20), Is.EqualTo("Raid warning · Mixed × 20"));
            Assert.That(BulletinModel.RaidTitle(key, -1, 0), Is.EqualTo("Raid warning"));
            Assert.That(BulletinModel.Title(key, 2, 20), Is.Not.EqualTo(BulletinModel.RaidTitle(key, 2, 20)),
                "the generic title reads the mix as an item");
        }
    }
}
