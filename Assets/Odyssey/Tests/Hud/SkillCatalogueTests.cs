#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// SK5: <b>a row's liveness is a claim about the simulation, and this is what checks it.</b>
    ///
    /// <para>The catalogue decides whether a skill on the colonist pane shows a number or is
    /// greyed out with an excuse beside it. That decision is a statement about
    /// <c>Odyssey.Sim</c>, which this assembly cannot reference at all — and for months nothing
    /// tested it, so <b>Construction stayed greyed out from U26 and Growing from U47</b>, the one
    /// screen whose job is saying what a colonist can do denying half of what she does. Growing
    /// was the worse of the two: one branch added the skill, the jobs and the row, and left its
    /// own row dead.</para>
    ///
    /// <para><b>The guard is a pair of pins facing each other across the aspect name</b>, because
    /// neither assembly can see the other. <c>Odyssey.Tests.Sim.SkillTests</c> pins
    /// <c>SkillIndex.Names</c> and fails the moment the simulation grows a skill, with a message
    /// sending the author here; this end pins what the catalogue then says about it. Each list is
    /// written out in full rather than read off the thing under test, which is the idiom the
    /// skills tests already use: a test that asks the code what it is called agrees with itself
    /// whatever either half has been renamed to, and the name is the whole contract.</para>
    /// </summary>
    public class SkillCatalogueTests
    {
        /// <summary>
        /// The simulation's own names for the skills it trains, minus hauling — which is a work
        /// type and not a skill in the design's list (15-skills §6), so it has no row at all.
        /// Copied from <c>SkillIndex.Names</c> by hand; the two are only allowed to agree by
        /// agreeing on the spelling.
        /// </summary>
        static readonly string[] Simulated = { "cutting", "mining", "construction", "growing", "melee", "medicine" };

        [Test]
        public void TheLiveRowsAreExactlyTheSkillsTheSimulationTrains()
        {
            var live = SkillCatalogue.All.Where(e => e.Live).Select(e => e.Skill).ToArray();

            Assert.That(live, Is.EquivalentTo(Simulated),
                "the catalogue and the simulation disagree about what a colonist can learn. If " +
                "Odyssey.Tests.Sim.SkillTests.EverySkillTheSimulationTrainsIsNamedHere is also " +
                "failing, a skill has been added or removed and this is the row that goes with " +
                "it; if it is passing, a row here is lying about the simulation.");

            Assert.That(live, Is.Unique, "two rows are reading the same skill's numbers");
        }

        /// <summary>
        /// A live row has no excuse on it and a dead one always does. The excuse is the whole
        /// reason a dead row is drawn at all rather than hidden — it is how the pane shows the
        /// shape of the game without pretending the system is there.
        /// </summary>
        [Test]
        public void ALiveRowCarriesNoExcuseAndADeadOneAlwaysDoes()
        {
            foreach (SkillCatalogue.Entry entry in SkillCatalogue.All)
            {
                if (entry.Live)
                    Assert.That(entry.Reason, Is.Empty,
                        $"{entry.Key} is live and still says why it is not");
                else
                    Assert.That(entry.Reason, Is.Not.Empty,
                        $"{entry.Key} is greyed out without saying why, which the catalogue forbids");
            }
        }

        /// <summary>
        /// Every live row mints the four names its numbers arrive under, spelled the way
        /// <c>Odyssey.Sim.Pawns.SkillAspects</c> spells them, and a dead row mints none — so a
        /// greyed row cannot quietly pick up a colonist's numbers through a key nobody meant it
        /// to have.
        /// </summary>
        [Test]
        public void ALiveRowMintsTheFourNamesTheSimulationPublishesUnder()
        {
            foreach (SkillCatalogue.Entry entry in SkillCatalogue.All)
            {
                if (!entry.Live)
                {
                    Assert.That(entry.Level, Is.EqualTo(default(AspectKey)),
                        $"{entry.Key} is greyed out and still reads a level");
                    Assert.That(entry.Progress, Is.EqualTo(default(AspectKey)),
                        $"{entry.Key} is greyed out and still reads a bar");
                    continue;
                }

                string stem = "odyssey.pawn.skill." + entry.Skill + ".";
                Assert.That(entry.Level, Is.EqualTo(AspectKey.Of(stem + "level")));
                Assert.That(entry.Passion, Is.EqualTo(AspectKey.Of(stem + "passion")));
                Assert.That(entry.Experience, Is.EqualTo(AspectKey.Of(stem + "experience")));
                Assert.That(entry.Progress, Is.EqualTo(AspectKey.Of(stem + "progress")),
                    "SK2: the bar's per-mille arrives under its own name, beside the level");
            }
        }

        /// <summary>
        /// Both orders hold the same rows. <c>All</c> is the planning order the file is a record
        /// of and <c>ReadingOrder</c> is what a player scans; a skill that fell out of one of them
        /// would be invisible on one of the two grids that draw it.
        /// </summary>
        [Test]
        public void TheReadingOrderIsTheSameRowsInADifferentOrder()
        {
            Assert.That(SkillCatalogue.ReadingOrder.Select(e => e.Key),
                Is.EquivalentTo(SkillCatalogue.All.Select(e => e.Key)));
            Assert.That(SkillCatalogue.All.Select(e => e.Key), Is.Unique);
        }
    }
}
