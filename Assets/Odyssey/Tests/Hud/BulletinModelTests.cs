#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Events panel's model (design 23 §5). An event is immutable once raised, so the whole
    /// job here is to raise each one exactly once, keep it until dismissed, and never announce
    /// history as news.
    /// </summary>
    public class BulletinModelTests
    {
        static WorldSnapshot Frame(params (int id, int tick)[] entries)
        {
            WorldSnapshot snapshot = Odyssey.Tests.Hud.Frame.Write(tick: entries.Length == 0 ? 0 : entries[^1].tick);
            foreach ((int id, int tick) in entries)
                snapshot.AddBulletin(new BulletinView(id, IncidentHandle.SupplyDrop, new CellRef(3, 4, 1), tick, favourability: 1));
            return snapshot;
        }

        [Test]
        public void AQuietWorldHasNoRows()
        {
            var model = new BulletinModel();
            model.Refresh(Frame());
            Assert.That(model.Rows, Is.Empty);
            Assert.That(model.Arrived, Is.Zero);
        }

        [Test]
        public void AnEntryBecomesARowOnceAndStaysAcrossRefreshes()
        {
            var model = new BulletinModel();
            model.Refresh(Frame());
            model.Refresh(Frame((1, 2_500)));
            Assert.That(model.Rows.Count, Is.EqualTo(1));
            Assert.That(model.Arrived, Is.EqualTo(1));
            Assert.That(model.ArrivedFavourability, Is.EqualTo(1));

            model.Refresh(Frame((1, 2_500)));
            model.Refresh(Frame((1, 2_500)));
            Assert.That(model.Rows.Count, Is.EqualTo(1), "the same entry was raised again");
            Assert.That(model.Arrived, Is.Zero, "a row that is merely still there is not news");
        }

        /// <summary>
        /// A second colony's ids start at one again. Without the reset its first events were all
        /// "already seen": no row, no chime, and a raid that should pause the game did not.
        /// </summary>
        [Test]
        public void ANewColonyStartsItsIdsAgain()
        {
            var model = new BulletinModel();
            model.Refresh(Frame());
            model.Refresh(Frame((1, 100), (2, 200), (3, 300)));
            Assert.That(model.Rows.Count, Is.EqualTo(3));

            model.Reset();
            Assert.That(model.Rows, Is.Empty, "the last colony's rows stayed on the panel");
            model.Refresh(Frame());
            model.Refresh(Frame((1, 50)));
            Assert.That(model.Rows.Count, Is.EqualTo(1), "the new colony's first event was taken as already seen");
            Assert.That(model.Arrived, Is.EqualTo(1));

            // A loaded tail after a reset is history, not news.
            model.Reset();
            model.Refresh(Frame((1, 50), (2, 60), (3, 70), (4, 80)));
            Assert.That(model.Arrived, Is.Zero, "a loaded colony's history chimed as it arrived");
        }

        [Test]
        public void TheRowSaysWhatWhenAndWhere()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, GameClock.TicksPerDay * 2 + GameClock.TicksPerHour * 14)));

            BulletinRow row = model.Rows[0];
            Assert.That(row.Key, Is.EqualTo("ui.bulletin.supplydrop"));
            Assert.That(row.Title, Is.EqualTo(Registry.Label("ui.bulletin.supplydrop")));
            Assert.That(row.Stamp, Is.EqualTo("Day 3 · 14h"));
            Assert.That(row.Cell, Is.EqualTo(new CellRef(3, 4, 1)));
            Assert.That(row.Favourability, Is.EqualTo(1));
        }

        [Test]
        public void NewestFirstAndOnlyTheUnseenAreNews()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100)));
            model.Refresh(Frame((1, 100), (2, 200), (3, 300)));

            Assert.That(model.Arrived, Is.EqualTo(2));
            Assert.That(model.Rows[0].Id, Is.EqualTo(3));
            Assert.That(model.Rows[2].Id, Is.EqualTo(1));
        }

        /// <summary>
        /// The panel refreshes four times a second and the world may tick sixty; several entries
        /// can arrive between two looks, and the tail may already have dropped the oldest of them.
        /// Everything above the highest seen id is raised, in one refresh.
        /// </summary>
        [Test]
        public void SeveralEntriesBetweenTwoRefreshesAreAllRaised()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100)));
            model.Refresh(Frame((2, 200), (3, 300), (4, 400)));

            Assert.That(model.Rows.Count, Is.EqualTo(4));
            Assert.That(model.Arrived, Is.EqualTo(3));
        }

        [Test]
        public void ADismissedRowStaysDismissedHoweverOftenThePanelRefreshes()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100), (2, 200)));
            model.Dismiss(1);
            Assert.That(model.Rows.Count, Is.EqualTo(1));
            Assert.That(model.Rows[0].Id, Is.EqualTo(2));

            model.Refresh(Frame((1, 100), (2, 200)));
            Assert.That(model.Rows.Count, Is.EqualTo(1));
            Assert.That(model.IsDismissed(1), Is.True);

            model.DismissAll();
            Assert.That(model.Rows, Is.Empty);
            model.Refresh(Frame((1, 100), (2, 200)));
            Assert.That(model.Rows, Is.Empty);
        }

        /// <summary>Loading a save shows what already happened as history, silently.</summary>
        [Test]
        public void TheFirstRefreshPrimesWithoutAnnouncing()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100), (2, 200), (3, 300)));

            Assert.That(model.Rows.Count, Is.EqualTo(3), "history is shown");
            Assert.That(model.Arrived, Is.Zero, "and not announced");
        }

        [Test]
        public void ThePanelHoldsOnlySoManyAndTheOldestFallOff()
        {
            var model = new BulletinModel();
            model.Refresh(Frame());
            var entries = new (int, int)[BulletinModel.MaxRows + 3];
            for (int i = 0; i < entries.Length; i++) entries[i] = (i + 1, (i + 1) * 100);
            model.Refresh(Frame(entries));

            Assert.That(model.Rows.Count, Is.EqualTo(BulletinModel.MaxRows));
            Assert.That(model.Rows[0].Id, Is.EqualTo(entries.Length), "the newest is kept");
            Assert.That(model.Rows[^1].Id, Is.EqualTo(entries.Length - BulletinModel.MaxRows + 1));
        }

        /// <summary>
        /// The autosave's line: something the game did, on the panel for things that happened.
        /// </summary>
        [Test]
        public void ANoticeSitsOnThePanelWithoutBeingAnEvent()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100)));

            model.PostNotice("ui.session.autosave", "Autosaved", "Day 2");

            Assert.That(model.Rows[0].Id, Is.EqualTo(BulletinModel.NoticeId));
            Assert.That(BulletinModel.IsNotice(model.Rows[0]), Is.True);
            Assert.That(model.Rows[0].Id, Is.LessThan(0),
                "negative, so it can never collide with a ledger id");
            Assert.That(model.Arrived, Is.Zero, "the game saving itself does not chime");
        }

        [Test]
        public void ThereIsOnlyEverOneNotice()
        {
            // Six rows is the whole panel, and a week of autosaves would otherwise crowd out every
            // event. What a player wants from it has one current value, not a history.
            var model = new BulletinModel();
            model.PostNotice("ui.session.autosave", "Autosaved", "Day 2");
            model.PostNotice("ui.session.autosave", "Autosaved", "Day 3");
            model.PostNotice("ui.session.autosave", "Autosaved", "Day 4");

            Assert.That(model.Rows.Count, Is.EqualTo(1));
            Assert.That(model.Rows[0].Stamp, Is.EqualTo("Day 4"));
        }

        [Test]
        public void ANoticeDismissedComesBackWhenTheNextOneIsWritten()
        {
            // Dismissing is "I have read this", not "never tell me again": the id is fixed, so
            // without this the first dismissal would silence the autosave for the session.
            var model = new BulletinModel();
            model.PostNotice("ui.session.autosave", "Autosaved", "Day 2");
            model.Dismiss(BulletinModel.NoticeId);
            Assert.That(model.Rows, Is.Empty);

            model.PostNotice("ui.session.autosave", "Autosaved", "Day 3");
            Assert.That(model.Rows.Count, Is.EqualTo(1));
        }

        [Test]
        public void ANoticeDoesNotStopTheLedgerBeingRead()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100)));
            model.PostNotice("ui.session.autosave", "Autosaved", "Day 2");
            model.Refresh(Frame((1, 100), (2, 200)));

            Assert.That(model.Arrived, Is.EqualTo(1), "the real event still arrives and still chimes");
            Assert.That(model.Rows.Count, Is.EqualTo(3));
        }

        /// <summary>
        /// A theft (design 33 §17) says what was taken and how many, as the activity line writes a
        /// load; one of a thing has no count; an empty-handed leaving names nothing. The words are the
        /// registry's, and a theft chimes as a blow.
        /// </summary>
        [Test]
        public void ATheftSaysWhatWasTakenAndHowMany()
        {
            var model = new BulletinModel();
            model.Refresh(Frame());
            WorldSnapshot snapshot = Odyssey.Tests.Hud.Frame.Write(tick: 300);
            snapshot.AddBulletin(new BulletinView(1, IncidentHandle.Theft, new CellRef(0, 7, 1), 100, favourability: 2,
                subject: ItemHandle.Meal, amount: 12));
            snapshot.AddBulletin(new BulletinView(2, IncidentHandle.Theft, new CellRef(0, 7, 1), 200, favourability: 2,
                subject: ItemHandle.Machete, amount: 1));
            snapshot.AddBulletin(new BulletinView(3, IncidentHandle.BanditLeft, new CellRef(0, 7, 1), 300));
            model.Refresh(snapshot);

            string theft = Registry.Label("ui.bulletin.theft");
            Assert.That(model.Rows[2].Key, Is.EqualTo("ui.bulletin.theft"));
            Assert.That(model.Rows[2].Title, Is.EqualTo(theft + " · " + ItemLabels.Label(ItemHandle.Meal) + " × 12"));
            Assert.That(model.Rows[1].Title, Is.EqualTo(theft + " · " + ItemLabels.Label(ItemHandle.Machete)));
            Assert.That(model.Rows[0].Key, Is.EqualTo("ui.bulletin.banditleft"));
            Assert.That(model.Rows[0].Title, Is.EqualTo(Registry.Label("ui.bulletin.banditleft")));
            Assert.That(model.ArrivedFavourability, Is.EqualTo(2), "a theft did not chime as a blow");
        }

        [Test]
        public void VersionMovesOnlyWhenTheRowsDo()
        {
            var model = new BulletinModel();
            model.Refresh(Frame((1, 100)));
            int version = model.Version;

            model.Refresh(Frame((1, 100)));
            Assert.That(model.Version, Is.EqualTo(version));

            model.Refresh(Frame((1, 100), (2, 200)));
            Assert.That(model.Version, Is.Not.EqualTo(version));
        }
    }
}
