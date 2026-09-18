#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The line a candidate card is compared on (<see cref="SkillSummary"/>).
    ///
    /// <para>Every rule here exists because the alternative is a card that quietly says nothing.
    /// The screen's whole job is telling three people apart, and it spent the time between U41 and
    /// 2026-09-18 showing a name and an occupation — an occupation being drawn from its own salt
    /// and therefore telling a player nothing at all about what anybody can do.</para>
    /// </summary>
    public class SkillSummaryTests
    {
        static SkillRow Row(string name, int level, bool live = true) =>
            new SkillRow { Name = name, Level = level, Live = live };

        [Test]
        public void TheBestTwoComeOutHighestFirst()
        {
            var rows = new List<SkillRow>
            {
                Row("Hauling", 2), Row("Cutting", 3), Row("Mining", 6), Row("Construction", 1),
            };

            Assert.That(SkillSummary.Line(rows, 2), Is.EqualTo("Mining 6 · Cutting 3"));
        }

        [Test]
        public void ASkillAtZeroIsNotSomethingYouAreGoodAt()
        {
            // "Mining 0 · Cutting 0" is a card that has told the player less than one saying so.
            var rows = new List<SkillRow> { Row("Hauling", 0), Row("Cutting", 4), Row("Mining", 0) };

            Assert.That(SkillSummary.Line(rows, 2), Is.EqualTo("Cutting 4"),
                "a zero was listed to fill the second slot");
        }

        [Test]
        public void AColonistWithNothingToShowSaysSo()
        {
            // About one candidate in forty, measured off the starting roll: 2.6% of draws have
            // every live skill at zero. A blank line and an absent line look identical.
            var rows = new List<SkillRow> { Row("Hauling", 0), Row("Mining", 0) };

            Assert.That(SkillSummary.Line(rows, 2), Is.EqualTo(SkillSummary.Nothing));
            Assert.That(SkillSummary.Line(new List<SkillRow>(), 2), Is.EqualTo(SkillSummary.Nothing));
            Assert.That(SkillSummary.Line(null, 2), Is.EqualTo(SkillSummary.Nothing));
        }

        [Test]
        public void TheNineNothingSimulatesAreNotOfferedAsAnAnswer()
        {
            // The detail pane draws them greyed, which is honest there — the owner asked for it,
            // so the card does not hide that the rest exist. On a two-slot line they would be
            // noise, and a dead skill at level 5 would outrank a live one at 4.
            var rows = new List<SkillRow>
            {
                Row("Shooting", 9, live: false), Row("Cooking", 7, live: false), Row("Mining", 4),
            };

            Assert.That(SkillSummary.Line(rows, 2), Is.EqualTo("Mining 4"));
        }

        [Test]
        public void ATieKeepsReadingOrder()
        {
            // Two skills at one level always come out the same way round, so a reroll that changes
            // nothing about a colonist looks like it changed nothing.
            var rows = new List<SkillRow> { Row("Hauling", 3), Row("Cutting", 3), Row("Mining", 3) };

            Assert.That(SkillSummary.Line(rows, 2), Is.EqualTo("Hauling 3 · Cutting 3"));
            Assert.That(SkillSummary.Line(rows, 2), Is.EqualTo(SkillSummary.Line(rows, 2)));
        }

        [Test]
        public void AskingForFewerSkillsThanThereAreCutsTheLine()
        {
            var rows = new List<SkillRow> { Row("Mining", 6), Row("Cutting", 3) };

            Assert.That(SkillSummary.Line(rows, 1), Is.EqualTo("Mining 6"));
            Assert.That(SkillSummary.Line(rows, 0), Is.EqualTo(SkillSummary.Nothing));
            Assert.That(SkillSummary.Line(rows, 9), Is.EqualTo("Mining 6 · Cutting 3"),
                "asking for more than exist should give what exists, not pad");
        }

        [Test]
        public void TheCardAsksForWhatItsThirdLineHoldsRoomFor()
        {
            // The count is HudLayout's, not the presenter's, so the height of the card and the
            // number of skills drawn in it cannot come apart — which is what happened to both
            // between U41 and 2026-09-18.
            Assert.That(HudLayout.ColonistCardSkills, Is.GreaterThan(0));
            Assert.That(HudLayout.ColonistCard,
                Is.GreaterThanOrEqualTo(HudLayout.StartRow + 2 * HudLayout.StartSeedCaption),
                "the card is not tall enough for the three lines it draws");
        }
    }
}
