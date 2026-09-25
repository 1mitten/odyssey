#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Traits on the screen (design 51 §5f, TM4): the words a trait is written in, the pane's rows
    /// under the needs, the trait's mood on the Thoughts tab, and the select card's lines. All
    /// from numbers the simulation published; the control in each case is a frame without them.
    /// </summary>
    public class TraitRowsTests
    {
        static readonly PawnId Ada = new PawnId(1);

        static WorldSnapshot Frame()
        {
            WorldSnapshot snapshot = global::Odyssey.Tests.Hud.Frame.Write();
            snapshot.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 500, JobHandle.Wait,
                flags: PawnFlags.Person));
            return snapshot;
        }

        static void Slot(WorldSnapshot snapshot, int slot, int handle, int mood = 0, int nerve = 0,
            int learn = 1000, int work = 1000, int cannot = 0)
        {
            snapshot.AddPawnAspect(new PawnAspect(Ada, MindAspectNames.TraitKey[slot], handle));
            if (mood != 0) snapshot.AddPawnAspect(new PawnAspect(Ada, MindAspectNames.TraitMoodKey[slot], mood));
            if (nerve != 0) snapshot.AddPawnAspect(new PawnAspect(Ada, MindAspectNames.TraitNerveKey[slot], nerve));
            if (learn != 1000) snapshot.AddPawnAspect(new PawnAspect(Ada, MindAspectNames.TraitLearnKey[slot], learn));
            if (work != 1000) snapshot.AddPawnAspect(new PawnAspect(Ada, MindAspectNames.TraitWorkKey[slot], work));
            if (cannot != 0) snapshot.AddPawnAspect(new PawnAspect(Ada, MindAspectNames.TraitCannotKey[slot], cannot));
        }

        static InspectModel Pane(WorldSnapshot snapshot)
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(snapshot);
            return pane;
        }

        [Test]
        public void ATraitIsWrittenAsWhatItDoes()
        {
            Assert.That(TraitSummary.Of(0, 0, 1000, 1200, 0), Is.EqualTo(Registry.Label("ui.mind.work") + " +20%"));
            Assert.That(TraitSummary.Of(0, 0, 1000, 800, 0), Is.EqualTo(Registry.Label("ui.mind.work") + " -20%"));
            Assert.That(TraitSummary.Of(60, 0, 1000, 1000, 0), Is.EqualTo(Registry.Label("ui.need.mood") + " +6"));
            Assert.That(TraitSummary.Of(0, 80, 1000, 1000, 0), Is.EqualTo(Registry.Label("ui.mind.breakssooner")));
            Assert.That(TraitSummary.Of(0, -90, 1000, 1000, 0), Is.EqualTo(Registry.Label("ui.mind.breakslater")));
            Assert.That(TraitSummary.Of(0, 0, 1750, 1000, 0), Is.EqualTo(Registry.Label("ui.mind.learns") + " x1.75"));
            Assert.That(TraitSummary.Of(0, 0, 400, 1000, 0), Is.EqualTo(Registry.Label("ui.mind.learns") + " x0.4"));
            Assert.That(TraitSummary.Of(0, 0, 1000, 1000, 1 << WorkHandle.Mining),
                Is.EqualTo(Registry.Label("ui.mind.cannot") + ": " + Registry.Label("ui.work.mining")));
            Assert.That(TraitSummary.Of(0, 0, 1000, 1350, 1 << WorkHandle.Mining),
                Does.Contain("+35%").And.Contain(Registry.Label("ui.work.mining")), "two effects, both said");
        }

        [Test]
        public void ATraitIsTintedByWhetherItCostsOrBuys()
        {
            Assert.That(TraitSummary.Tint(0, 0, 1000, 1000, 1 << WorkHandle.Mining), Is.EqualTo(HudTheme.Bad));
            Assert.That(TraitSummary.Tint(0, 80, 1000, 1000, 0), Is.EqualTo(HudTheme.Bad), "breaking sooner is a cost");
            Assert.That(TraitSummary.Tint(0, -90, 1000, 1000, 0), Is.EqualTo(HudTheme.Good));
            Assert.That(TraitSummary.Tint(0, 0, 1750, 1000, 0), Is.EqualTo(HudTheme.Good));
            Assert.That(TraitSummary.Tint(0, 0, 1000, 1350, 1 << WorkHandle.Mining), Is.Null, "both at once is neither");
        }

        [Test]
        public void ThePaneListsHerTraitsFromThePublishedSlots()
        {
            WorldSnapshot snapshot = Frame();
            Slot(snapshot, 0, TraitHandle.Diligent, work: 1200);
            Slot(snapshot, 1, TraitHandle.SoftHands, cannot: 1 << WorkHandle.Mining);

            InspectModel pane = Pane(snapshot);

            Assert.That(pane.TraitRows.Select(r => r.Name),
                Is.EqualTo(new[] { Registry.Label("ui.trait.diligent"), Registry.Label("ui.trait.softhands") }),
                "in the order she was dealt them, so a row never moves");
            Assert.That(pane.TraitRows[1].Value, Does.Contain(Registry.Label("ui.work.mining")));
            Assert.That(pane.TraitRows[1].Tooltip, Is.EqualTo(Registry.Describe("ui.trait.softhands")));

            Assert.That(Pane(Frame()).TraitRows, Is.Empty, "the control: a colonist from before traits has none shown");
        }

        [Test]
        public void TheSelectCardWritesATraitALine()
        {
            var traits = new[]
            {
                TraitSummary.Row(TraitHandle.Cheerful, 60, 0, 1000, 1000, 0),
                TraitSummary.Row(TraitHandle.BlackThumb, 0, 0, 1000, 1000, 1 << WorkHandle.Growing),
            };
            var candidate = new Candidate(5u, "Wrenn", 30, "Scrapper", null, traits);
            string[] lines = candidate.TraitLines.Split('\n');
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Is.EqualTo(Registry.Label("ui.trait.cheerful") + " - " + Registry.Label("ui.need.mood") + " +6"));
            Assert.That(lines[1], Does.StartWith(Registry.Label("ui.trait.blackthumb")));

            Assert.That(new Candidate(5u, "Wrenn", 30, "Scrapper", null).TraitLines, Is.Empty);
        }

        [Test]
        public void EveryTraitHasANameAndADescription()
        {
            for (int t = 0; t < TraitHandle.Count; t++)
            {
                string key = TraitSummary.Key(t);
                Assert.That(Registry.Label(key), Is.Not.EqualTo(key), key + " is not in the registry");
                Assert.That(Registry.Describe(key), Is.Not.Empty, key + " has no description");
            }
        }

        [Test]
        public void TheTraitsFitUnderTheNeeds()
        {
            // The pane is one height whatever tab is showing (design 14); the traits are drawn in
            // the Needs tab's slack, a heading and a row each, so they may not grow it.
            int needs = HudLayout.InspectNeedRows * HudLayout.NeedRow + (HudLayout.InspectNeedRows - 1) * HudLayout.NeedRowGap;
            int traits = (1 + TraitHandle.MaxPerPawn) * HudLayout.CellRow;
            Assert.That(needs + traits, Is.LessThanOrEqualTo(HudLayout.InspectTabBody));
        }

        [Test]
        public void EveryWordIsDrawable()
        {
            string text = TraitSummary.Of(-60, 80, 400, 800, (1 << WorkHandle.Mining) | (1 << WorkHandle.Growing));
            foreach (char c in text)
                Assert.That(c, Is.LessThan((char)128), "ASCII only: a character neither font has is a blank (P13)");
        }
    }
}
