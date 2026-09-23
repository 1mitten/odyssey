#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>The Wildlife panel's model (design 30 §6): what is out there, by kind and by distance.</summary>
    public class WildlifeModelTests
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

        [Test]
        public void RowsAreTheAnimalsByKindThenByDistanceFromWhereThePeopleStand()
        {
            var model = new WildlifeModel();
            model.Refresh(Board(), Nobody);

            Assert.That(model.HomeX, Is.EqualTo(12));
            Assert.That(model.HomeZ, Is.EqualTo(10));
            Assert.That(model.TotalCount, Is.EqualTo(4), "four animals; the two people are not rows");

            var ids = new List<int>();
            foreach (WildlifeRow row in model.All) ids.Add(row.Id.Value);
            Assert.That(ids, Is.EqualTo(new[] { 5, 4, 3, 6 }), "hogs first (kind 1), nearer first; then the rats");

            Assert.That(model.All[0].Away, Is.EqualTo(8), "the near hog is eight cells out");
            Assert.That(model.All[1].Away, Is.EqualTo(20));
            Assert.That(model.All[2].Away, Is.EqualTo(2));
            Assert.That(model.All[3].Layer, Is.EqualTo(3));
            Assert.That(model.All[0].KindKey, Is.EqualTo("ui.pawn.hog"));
            Assert.That(model.All[2].ActivityKey, Is.EqualTo(PawnKindLabels.Wandering));
            Assert.That(model.All[3].ActivityKey, Is.EqualTo(PawnKindLabels.Resting));
        }

        [Test]
        public void TheHeaderCountsEachKind()
        {
            var model = new WildlifeModel();
            model.Refresh(Board(), Nobody);

            Assert.That(model.Counts.Count, Is.EqualTo(2));
            Assert.That(model.Counts[0].Kind, Is.EqualTo(1));
            Assert.That(model.Counts[0].Count, Is.EqualTo(2));
            Assert.That(model.Counts[0].KindKey, Is.EqualTo("ui.pawn.hog"));
            Assert.That(model.Counts[1].Kind, Is.EqualTo(2));
            Assert.That(model.Counts[1].Count, Is.EqualTo(2));
        }

        [Test]
        public void TheSelectionIsMarkedAndItsPageIsFound()
        {
            var model = new WildlifeModel { PageCapacity = 2 };
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
        public void AnEmptyBoardIsOnePageOfNothingAndNoColonyIsTheOrigin()
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 2), 0);
            snapshot.AddPawn(new PawnView(new PawnId(7), new CellRef(4, 6, 0), 800, 800, 800, JobHandle.Wait, kind: 1));
            var model = new WildlifeModel();
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
            var wildlife = new WildlifeDirector();
            int changed = 0;
            wildlife.Changed += () => changed++;
            Assert.That(wildlife.Open, Is.False);
            wildlife.Toggle();
            Assert.That(wildlife.Open, Is.True);
            wildlife.SetOpen(true);
            Assert.That(changed, Is.EqualTo(1), "setting what is already set says nothing");
            wildlife.Toggle();
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void ThePanelIsWideEnoughForItsOwnPaddingAndBorder()
        {
            Assert.That(WildlifeLayout.PanelOuterWidth,
                Is.EqualTo(WildlifeLayout.PanelWidth + 2 * (HudLayout.Pad + HudTheme.BorderWidth)));
        }

        [Test]
        public void EscapeClosesTheWildlifePanelAtTheWorkTabsRung()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Escape(false, false, false, false, false, wildlifeOpen: true, null),
                Is.EqualTo(EscapeAction.CloseWildlife));
            Assert.That(settings.Escape(false, false, false, true, false, wildlifeOpen: true, null),
                Is.EqualTo(EscapeAction.CloseWork), "Work first, as the two never share the corner");
            Assert.That(settings.Escape(false, false, false, false, true, wildlifeOpen: true, null),
                Is.EqualTo(EscapeAction.CloseWildlife), "before the Almanac, which floats");
        }
    }
}
