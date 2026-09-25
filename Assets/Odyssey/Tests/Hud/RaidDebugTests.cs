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

        static readonly GridSize Size = new GridSize(40, 40, 4);

        static WorldSnapshot Frame(RaidPhase phase, int standing, CellRef centre)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddRaid(new RaidView(1, phase, 2, 20, standing, centre, new CellRef(20, 1, 20)));
            return frame;
        }

        static AlertRow? RaidRow(AlertModel model)
        {
            foreach (AlertRow row in model.Rows)
                if (row.Key == AlertModel.RaidKey) return row;
            return null;
        }

        /// <summary>
        /// The Raid alert is raised by the assault and not by the gathering (the control), counts the
        /// standing raiders, goes to their middle, and goes when the band does.
        /// </summary>
        [Test]
        public void TheRaidAlertIsRaisedWhileABandAssaults()
        {
            var model = new AlertModel();
            model.Refresh(Frame(RaidPhase.Gathering, 12, new CellRef(3, 1, 20)), 0);
            Assert.That(RaidRow(model), Is.Null, "a band gathering at the edge raised the alert");

            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(9, 1, 20)), 1);
            AlertRow? row = RaidRow(model);
            Assert.That(row, Is.Not.Null, "an assault raised no alert");
            Assert.That(row!.Value.Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(row.Value.Count, Is.EqualTo(12));
            Assert.That(row.Value.Cell, Is.EqualTo(new CellRef(9, 1, 20)));
            Assert.That(row.Value.Detail, Is.EqualTo("Mixed"));

            model.Refresh(Frame(RaidPhase.Assaulting, 0, new CellRef(9, 1, 20)), 2);
            Assert.That(RaidRow(model), Is.Null, "a band with nobody standing still raised the alert");
        }

        /// <summary>A dismissed Raid alert stays dismissed while the band moves, and a later raid raises it again.</summary>
        [Test]
        public void ADismissedRaidAlertStaysDismissedAsTheBandMoves()
        {
            var model = new AlertModel();
            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(9, 1, 20)), 0);
            model.Dismiss(RaidRow(model)!.Value.DismissKey);
            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(30, 1, 20)), 1);
            Assert.That(RaidRow(model), Is.Null, "the dismiss lasted only until the band moved");

            var empty = new WorldSnapshot();
            empty.BeginWrite(tick: 0, Size, sliceLayer: 1);
            model.Refresh(empty, 2);
            model.Refresh(Frame(RaidPhase.Assaulting, 5, new CellRef(9, 1, 20)), 3);
            Assert.That(RaidRow(model), Is.Not.Null, "the next raid was dismissed in advance");
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
