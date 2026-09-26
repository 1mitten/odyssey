#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>The Animals tab's model (design 30 §6): what is out there, by kind and by distance.</summary>
    public class AnimalsModelTests
    {
        static WorldSnapshot Board()
        {
            var size = new GridSize(40, 40, 4);
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, size, 1);
            // Two colonists at (10,10) and (14,10): the colony's centre is (12,10).
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(10, 10, 1), 600, 800, 800, JobHandle.Wait));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(14, 10, 1), 600, 800, 800, JobHandle.Haul));
            // A rat close by, a hog far off, a hog nearer, a rat on another layer.
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(13, 12, 1), 800, 800, 800, JobHandle.Wander, kind: 2));
            snapshot.AddPawn(new PawnView(new PawnId(4), new CellRef(30, 30, 1), 800, 800, 800, JobHandle.Wait, kind: 1));
            snapshot.AddPawn(new PawnView(new PawnId(5), new CellRef(20, 10, 1), 800, 800, 800, JobHandle.Wander, kind: 1));
            snapshot.AddPawn(new PawnView(new PawnId(6), new CellRef(12, 30, 3), 800, 800, 800, JobHandle.Wait, kind: 2));
            return snapshot;
        }

        static readonly IReadOnlyList<PawnId> Nobody = new List<PawnId>();

        static List<int> Ids(AnimalsModel model)
        {
            var ids = new List<int>();
            foreach (AnimalRow row in model.All) ids.Add(row.Id.Value);
            return ids;
        }

        [Test]
        public void RowsAreTheAnimalsByKindThenByDistanceFromWhereThePeopleStand()
        {
            var model = new AnimalsModel();
            model.Refresh(Board(), Nobody);

            Assert.That(model.HomeX, Is.EqualTo(12));
            Assert.That(model.HomeZ, Is.EqualTo(10));
            Assert.That(model.TotalCount, Is.EqualTo(4), "four animals; the two people are not rows");
            Assert.That(Ids(model), Is.EqualTo(new[] { 5, 4, 3, 6 }), "hogs first (kind 1), nearer first; then the rats");

            Assert.That(model.All[0].Away, Is.EqualTo(8), "the near hog is eight cells out");
            Assert.That(model.All[1].Away, Is.EqualTo(20));
            Assert.That(model.All[2].Away, Is.EqualTo(2));
            Assert.That(model.All[3].Layer, Is.EqualTo(3));
            Assert.That(model.All[0].KindKey, Is.EqualTo("ui.pawn.hog"));
            Assert.That(model.All[2].ActivityKey, Is.EqualTo(PawnKindLabels.Wandering));
            Assert.That(model.All[3].ActivityKey, Is.EqualTo(PawnKindLabels.Resting));
        }

        [Test]
        public void SortingByDoingGroupsTheStatesAndDistanceStillBreaksTies()
        {
            var model = new AnimalsModel();
            model.Refresh(Board(), Nobody);
            Assert.That(model.Sort, Is.EqualTo(AnimalsSort.Kind), "the default");
            Assert.That(model.SortBy(AnimalsSort.Kind), Is.False, "the sort it already has says nothing");

            Assert.That(model.SortBy(AnimalsSort.Doing), Is.True);
            // The resting ones together, then the wandering ones; within a state, kind then distance.
            Assert.That(Ids(model), Is.EqualTo(new[] { 4, 6, 5, 3 }));
            Assert.That(model.Rows.Count, Is.EqualTo(4), "and the page follows the new order");

            model.Refresh(Board(), Nobody);
            Assert.That(model.Sort, Is.EqualTo(AnimalsSort.Doing), "a refresh keeps the sort");
            Assert.That(Ids(model), Is.EqualTo(new[] { 4, 6, 5, 3 }));
        }

        [Test]
        public void TheCountStripCountsEachKind()
        {
            var model = new AnimalsModel();
            model.Refresh(Board(), Nobody);

            Assert.That(model.Counts.Count, Is.EqualTo(2));
            Assert.That(model.Counts[0].Kind, Is.EqualTo(1));
            Assert.That(model.Counts[0].Count, Is.EqualTo(2));
            Assert.That(model.Counts[0].KindKey, Is.EqualTo("ui.pawn.hog"));
            Assert.That(model.Counts[1].Kind, Is.EqualTo(2));
            Assert.That(model.Counts[1].Count, Is.EqualTo(2));

            model.SortBy(AnimalsSort.Doing);
            model.Refresh(Board(), Nobody);
            Assert.That(model.Counts[0].Kind, Is.EqualTo(1), "the strip is by kind whatever the rows are sorted by");
        }

        [Test]
        public void TheSelectionIsMarkedAndItsPageIsFound()
        {
            var model = new AnimalsModel { PageCapacity = 2 };
            model.Refresh(Board(), new List<PawnId> { new PawnId(6) });

            Assert.That(model.PageCount, Is.EqualTo(2));
            Assert.That(model.Rows.Count, Is.EqualTo(2), "the first page");
            Assert.That(model.Rows[0].Selected, Is.False);

            Assert.That(model.EnsurePageFor(new PawnId(6)), Is.True);
            Assert.That(model.Page, Is.EqualTo(1));
            Assert.That(model.Rows[1].Id, Is.EqualTo(new PawnId(6)));
            Assert.That(model.Rows[1].Selected, Is.True);

            Assert.That(model.EnsurePageFor(new PawnId(1)), Is.False, "a colonist has no row to find");
            model.SetPage(9);
            Assert.That(model.Page, Is.EqualTo(1), "clamped to the last page");
        }

        [Test]
        public void ThePagerAppearsAtThirteenAndNotAtTwelve()
        {
            var size = new GridSize(60, 60, 2);
            for (int n = 12; n <= 13; n++)
            {
                var snapshot = new WorldSnapshot();
                snapshot.BeginWrite(0, size, 0);
                for (int i = 1; i <= n; i++)
                    snapshot.AddPawn(new PawnView(new PawnId(i), new CellRef(i, 0, 0), 800, 800, 800, JobHandle.Wait, kind: 1));
                var model = new AnimalsModel();
                model.Refresh(snapshot, Nobody);
                Assert.That(model.PageCount, Is.EqualTo(n == 12 ? 1 : 2), $"{n} animals");
                Assert.That(model.Rows.Count, Is.EqualTo(12));
            }
        }

        [Test]
        public void AnEmptyBoardIsOnePageOfNothingAndNoColonyIsTheOrigin()
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 2), 0);
            snapshot.AddPawn(new PawnView(new PawnId(7), new CellRef(4, 6, 0), 800, 800, 800, JobHandle.Wait, kind: 1));
            var model = new AnimalsModel();
            model.Refresh(snapshot, Nobody);
            Assert.That(model.HomeX, Is.Zero);
            Assert.That(model.All[0].Away, Is.EqualTo(6), "from the origin, a number rather than a lie");

            var empty = new WorldSnapshot();
            empty.BeginWrite(0, new GridSize(10, 10, 2), 0);
            model.Refresh(empty, Nobody);
            Assert.That(model.TotalCount, Is.Zero);
            Assert.That(model.PageCount, Is.EqualTo(1));
            Assert.That(model.Rows, Is.Empty);
            Assert.That(model.Counts, Is.Empty);
        }

        [Test]
        public void TheDirectorOpensClosesAndSaysSoOnce()
        {
            var animals = new AnimalsDirector();
            int changed = 0;
            animals.Changed += () => changed++;
            Assert.That(animals.Open, Is.False);
            animals.Toggle();
            Assert.That(animals.Open, Is.True);
            animals.SetOpen(true);
            Assert.That(changed, Is.EqualTo(1), "setting what is already set says nothing");
            animals.Toggle();
            Assert.That(changed, Is.EqualTo(2));
        }

        /// <summary>The brief's arithmetic: 560 less the panel's padding and border is the three columns.</summary>
        [Test]
        public void TheWindowIsTheGridPlusItsOwnPaddingAndBorder()
        {
            Assert.That(AnimalsLayout.GridWidth, Is.EqualTo(534));
            Assert.That(AnimalsLayout.ExpectedGridWidth, Is.EqualTo(AnimalsLayout.GridWidth),
                "a UI Toolkit width is a border box; the columns must add up to the window less its chrome");
            Assert.That(AnimalsLayout.TabWidth, Is.EqualTo(560));
        }

        [Test]
        public void EscapeClosesTheAnimalsTabAtTheWorkTabsRung()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(false, false, false, false, false, animalsOpen: true, null),
                Is.EqualTo(EscapeAction.CloseAnimals));
            Assert.That(settings.Escape(false, false, false, true, false, animalsOpen: true, null),
                Is.EqualTo(EscapeAction.CloseWork), "Work first, as the two never share the corner");
            Assert.That(settings.Escape(false, false, false, false, true, animalsOpen: true, null),
                Is.EqualTo(EscapeAction.CloseAnimals), "before the Almanac, which floats");
        }

        [Test]
        public void TheBarsAnimalsItemIsLiveOnF5AndWildlifeIsNotOnTheBar()
        {
            HudCommand? animals = null, wildlife = null;
            foreach (HudCommand command in HudCommands.All)
            {
                if (command.Key == HudCommands.AnimalsKey) animals = command;
                if (command.Key == "ui.tab.wildlife") wildlife = command;
            }
            Assert.That(animals, Is.Not.Null);
            Assert.That(animals!.Value.Live, Is.True);
            Assert.That(animals.Value.Hotkey, Is.EqualTo("F5"));
            Assert.That(wildlife, Is.Null, "wildlife left the bar (owner, 2026-09-23); what is out there is the Animals tab");
        }

        [Test]
        public void TheInspectPanesInfoButtonOpensAnAnimalsFaunaEntry()
        {
            var inspect = new InspectModel { Subject = InspectSubject.Colonist, IsAnimal = true, KindIconKey = "ui.pawn.hog" };
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("Fauna", Registry.Label("ui.pawn.hog"))));
            inspect.KindIconKey = "ui.pawn.rat";
            Assert.That(AlmanacDirector.ResolveSelection(inspect), Is.EqualTo(("Fauna", Registry.Label("ui.pawn.rat"))));
        }

        /// <summary>The Almanac's Fauna is the three animals the game has, by their registry names, and nothing invented.</summary>
        [Test]
        public void TheAlmanacsFaunaAreTheAnimalsInTheGame()
        {
            AlmanacCategory? fauna = AlmanacCatalogue.GetCategory("Fauna");
            Assert.That(fauna, Is.Not.Null);
            var names = new List<string>();
            foreach (AlmanacEntry entry in fauna!.Entries) names.Add(entry.Name);
            Assert.That(names, Is.EquivalentTo(new[] { Registry.Label("ui.pawn.hog"), Registry.Label("ui.pawn.rat"), Registry.Label("ui.pawn.frog") }));
            foreach (AlmanacEntry entry in fauna.Entries)
            {
                Assert.That(entry.Definition, Does.Not.Contain("Tame chance"), "taming is not in the game");
                Assert.That(entry.PrimaryAction, Does.Contain("Animals"), "the live half of the entry is the Animals tab");
            }
        }
    }
}
