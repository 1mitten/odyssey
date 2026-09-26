#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The raid's two debug controls and its Events row (design 55 §7, §9): what the row sends, what
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
        /// A refused raid says why on its row (design 55 §9): too many for the room, with the room;
        /// and with room enough, the one other reason the door refuses. Never a negative room.
        /// </summary>
        [Test]
        public void ARefusedRaidSaysWhy()
        {
            Assert.That(DebugDirector.RaidRefusal(200, 37), Is.EqualTo("Refused: 200 will not fit, room for 37 under the ceiling"));
            Assert.That(DebugDirector.RaidRefusal(10, -3), Does.Contain("room for 0 "));
            Assert.That(DebugDirector.RaidRefusal(10, 40), Is.EqualTo("Refused: no edge the band can reach"));
        }

        static readonly GridSize Size = new GridSize(40, 40, 4);

        // CellRef is (x, z, y): these bands stand on layer 1 of a four-layer board.
        static WorldSnapshot Frame(RaidPhase phase, int standing, CellRef centre, int id = 1)
        {
            var frame = new WorldSnapshot();
            frame.BeginWrite(tick: 0, Size, sliceLayer: 1);
            frame.AddRaid(new RaidView(id, phase, 2, 20, standing, centre, new CellRef(20, 20, 1)));
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
            model.Refresh(Frame(RaidPhase.Gathering, 12, new CellRef(3, 20, 1)), 0);
            Assert.That(RaidRow(model), Is.Null, "a band gathering at the edge raised the alert");

            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(9, 20, 1)), 1);
            AlertRow? row = RaidRow(model);
            Assert.That(row, Is.Not.Null, "an assault raised no alert");
            Assert.That(row!.Value.Severity, Is.EqualTo(AlertSeverity.Danger));
            Assert.That(row.Value.Count, Is.EqualTo(12));
            Assert.That(row.Value.Cell, Is.EqualTo(new CellRef(9, 20, 1)));
            Assert.That(row.Value.Detail, Is.EqualTo("Mixed"));

            model.Refresh(Frame(RaidPhase.Assaulting, 0, new CellRef(9, 20, 1)), 2);
            Assert.That(RaidRow(model), Is.Null, "a band with nobody standing still raised the alert");
        }

        /// <summary>A dismissed Raid alert stays dismissed while the band moves, and a later raid raises it again.</summary>
        [Test]
        public void ADismissedRaidAlertStaysDismissedAsTheBandMoves()
        {
            var model = new AlertModel();
            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(9, 20, 1)), 0);
            model.Dismiss(RaidRow(model)!.Value.DismissKey);
            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(30, 20, 1)), 1);
            Assert.That(RaidRow(model), Is.Null, "the dismiss lasted only until the band moved");

            var empty = new WorldSnapshot();
            empty.BeginWrite(tick: 0, Size, sliceLayer: 1);
            model.Refresh(empty, 2);
            model.Refresh(Frame(RaidPhase.Assaulting, 5, new CellRef(9, 20, 1)), 3);
            Assert.That(RaidRow(model), Is.Not.Null, "the next raid was dismissed in advance");
        }

        /// <summary>
        /// A raid whose assault begins in the same refresh the dismissed one's ends is its own alert:
        /// the dismiss belongs to the band, not to the key. The same band carrying on is the control.
        /// </summary>
        [Test]
        public void ADismissedRaidDoesNotDismissTheRaidAfterIt()
        {
            var model = new AlertModel();
            model.Refresh(Frame(RaidPhase.Assaulting, 12, new CellRef(9, 20, 1), id: 1), 0);
            model.Dismiss(RaidRow(model)!.Value.DismissKey);
            model.Refresh(Frame(RaidPhase.Assaulting, 11, new CellRef(9, 20, 1), id: 1), 1);
            Assert.That(RaidRow(model), Is.Null, "the control: the dismissed band is still dismissed");

            model.Refresh(Frame(RaidPhase.Assaulting, 8, new CellRef(30, 20, 1), id: 2), 2);
            Assert.That(RaidRow(model), Is.Not.Null, "the next band inherited the last one's dismiss");
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
