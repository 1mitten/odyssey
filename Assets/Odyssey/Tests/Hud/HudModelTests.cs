#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A frame writer for tests, standing in for the simulation's publish phase. The models
    /// under test read <see cref="WorldSnapshot"/> and nothing else, so the fixture is exactly
    /// the contract: pawns, things and one slice of cells.
    /// </summary>
    static class Frame
    {
        public static WorldSnapshot Write(int layers = 4, int sliceLayer = 1, int tick = 0)
        {
            var size = new GridSize(10, 10, layers);
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, size, sliceLayer);
            return snapshot;
        }
    }

    public class GameClockTests
    {
        [Test]
        public void StartOfTimeIsFirstHourOfFirstDayOfLarkspur()
        {
            Assert.That(GameClock.HourOfDay(0), Is.Zero);
            Assert.That(GameClock.DayOfMonth(0), Is.EqualTo(1));
            Assert.That(GameClock.Describe(0), Is.EqualTo("00h · Day 1 · Larkspur · Wash"));
        }

        [Test]
        public void BoundariesRollOverInOrder()
        {
            long day = GameClock.TicksPerDay;
            Assert.That(GameClock.DayOfMonth(day), Is.EqualTo(2));

            long month = GameClock.TicksPerMonth;
            Assert.That(GameClock.MonthName(month), Is.EqualTo("Tansy"));
            Assert.That(GameClock.DayOfMonth(month), Is.EqualTo(1));
            Assert.That(GameClock.SeasonName(month), Is.EqualTo("Wash"));

            long year = GameClock.TicksPerMonth * 6;
            Assert.That(GameClock.MonthName(year), Is.EqualTo("Larkspur"));
            Assert.That(GameClock.SeasonName(GameClock.TicksPerMonth * 2), Is.EqualTo("Glare"));
        }
    }

    public class ColonistNamesTests
    {
        /// <summary>
        /// A seed and an id together name somebody, and the same pair always names the same
        /// somebody. Seed zero is the pool read from the top, which is the old id-only behaviour
        /// and is what a pawn with no published seed falls back to.
        ///
        /// <para><b>Written against the generated pool rather than against literals</b>, since the
        /// pool went from eight hand-written names to 244 generated from
        /// <c>docs/design/colonist-names.csv</c>. A test that pins "Wrenn" pins the CSV's first
        /// row, which is content the owner is free to reorder; what is worth pinning is that the
        /// arithmetic reads the pool from the top.</para>
        /// </summary>
        [Test]
        public void NamesAreStableAndStartAtTheTopOfThePool()
        {
            Assert.That(ColonistNames.Of(0u, new PawnId(1)),
                Is.EqualTo(ColonistNamePool.Names[0]));
            Assert.That(ColonistNames.Of(0u, new PawnId(ColonistNamePool.Names.Length)),
                Is.EqualTo(ColonistNamePool.Names[ColonistNamePool.Names.Length - 1]));
            Assert.That(ColonistNames.Of(7u, new PawnId(1)), Is.EqualTo(ColonistNames.Of(7u, new PawnId(1))),
                "the same seed and id must always answer the same name");
        }

        /// <summary>
        /// The pool is big enough that no colony this game builds can reach the end of it.
        ///
        /// <para>It was eight, with a comment promising "about forty at M2, when pawn generation
        /// needs a pool that does not repeat in a colony of fifty". Fifty is the number that
        /// mattered, so fifty is what is asserted — against the pool rather than against 244, so
        /// the guarantee survives the owner cutting names as well as adding them.</para>
        /// </summary>
        [Test]
        public void ThePoolOutlastsAnyColonyThisGameBuilds()
        {
            const int BiggestColony = 50;
            Assert.That(ColonistNamePool.Names.Length, Is.GreaterThan(BiggestColony),
                "a colony of fifty would wrap the pool and start appending cycle numbers");

            var seen = new HashSet<string>();
            for (int id = 1; id <= BiggestColony; id++)
                Assert.That(seen.Add(ColonistNames.Of(4242u, new PawnId(id))), Is.True,
                    $"colonist {id} took a name somebody in the same colony already has");
        }

        [Test]
        public void PastThePoolTheCycleNumberIsAppendedNotInvented()
        {
            // Past the end of a 244-name pool, which is past any colony — the branch is kept
            // because it is what makes the method total, and this is what holds it honest.
            int past = ColonistNamePool.Names.Length + 1;
            Assert.That(ColonistNames.Of(0u, new PawnId(past)),
                Is.EqualTo(ColonistNamePool.Names[0] + " 2"));
            Assert.That(ColonistNames.Of(0u, new PawnId(0)), Is.EqualTo("nobody"));
        }

        /// <summary>
        /// Every name in the pool is well formed and none is absurdly long.
        ///
        /// <para><b>This is a coarse guard and says so, because characters are not pixels.</b> It
        /// was written as "no name over twelve characters" and <i>Christopher</i> — eleven —
        /// sailed through it and then failed
        /// <c>HudGeometryTests.TheCardIsWideEnoughForItsRowsAndNoWider</c>, which asks the real
        /// text engine what a string really draws. That test is the gate; this one only catches
        /// the entries a 244-row CSV can gain without anybody noticing: a blank, a stray space, a
        /// sentence pasted into the wrong column.</para>
        /// </summary>
        [Test]
        public void NoNameInThePoolIsLongerThanACardBudgetsFor()
        {
            foreach (string name in ColonistNamePool.Names)
            {
                Assert.That(name, Is.Not.Empty);
                Assert.That(name.Trim(), Is.EqualTo(name), $"'{name}' has stray whitespace");
                Assert.That(name.Length, Is.LessThanOrEqualTo(12),
                    $"'{name}' is far longer than any name the card was sized for — the pixel " +
                    "measurement is HudGeometryTests', and this only catches the absurd");
            }
        }

        /// <summary>
        /// <b>The guarantee that survived the move from id to seed (U40).</b> Every colonist a
        /// world places itself shares that world's seed, so the id is what has to walk them apart
        /// — and a plain hash of the pair would have called two of five the same thing better than
        /// half the time. Checked over many seeds rather than one, because one lucky seed proves
        /// nothing about the scheme.
        /// </summary>
        [Test]
        public void ColonistsOfOneColonyNeverShareAName()
        {
            for (uint seed = 0; seed < 200; seed++)
            {
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int id = 1; id <= 8; id++)
                    Assert.That(seen.Add(ColonistNames.Of(seed, new PawnId(id))), Is.True,
                        $"world seed {seed} names two of its eight colonists the same thing");
            }
        }

        /// <summary>
        /// A reroll is meant to hand you a different person, so the name has to move with the
        /// seed — not on every single draw, since a pool of eight will repeat, but plainly more
        /// often than not.
        /// </summary>
        [Test]
        public void ADifferentSeedUsuallyMeansADifferentName()
        {
            string first = ColonistNames.Of(0u, new PawnId(1));
            int moved = 0;
            for (uint seed = 1; seed <= 100; seed++)
                if (ColonistNames.Of(seed, new PawnId(1)) != first) moved++;

            Assert.That(moved, Is.GreaterThan(70),
                "rerolling barely changes the name, so the pool is not being reached into");
        }
    }

    public class RosterModelTests
    {
        [Test]
        public void CardsFollowTheFrameAndMarkTheSelection()
        {
            var snapshot = Frame.Write();
            // Thousandths, which is the scale the simulation publishes. These read 80 and 30
            // until 2026-09-16 and so agreed with the bug they were meant to catch: a mood of
            // 30 is not "breaking" in the game, it is a colonist one point off death, and the
            // band function happened to call it breaking because it was reading the number as
            // a percentage.
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 2), 400, 900, 800, JobHandle.Haul));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 2, 0), 950, 100, 300, JobHandle.Sleep));

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: new PawnId(2));

            Assert.That(roster.Cards.Count, Is.EqualTo(2));
            Assert.That(roster.Cards[0].Name, Is.EqualTo(ColonistNamePool.Names[0]));
            Assert.That(roster.Cards[0].Layer, Is.EqualTo(2));
            Assert.That(roster.Cards[0].Selected, Is.False);
            Assert.That(roster.Cards[1].Name, Is.EqualTo(ColonistNamePool.Names[1]));
            Assert.That(roster.Cards[1].Selected, Is.True);
            Assert.That(MoodBands.Band(roster.Cards[1].Mood), Is.EqualTo("breaking"));
        }

        /// <summary>
        /// The roster is the colony's people (design 29 §2). An animal is a pawn in the same
        /// snapshot with a kind that is not the colonist's, and it gets no card, no name and no
        /// slot — the control is the colonist beside it, who keeps hers.
        /// </summary>
        [Test]
        public void AnAnimalHasNoCardOnTheRoster()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 0), 600, 800, 800, JobHandle.Wait));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 1, 0), 600, 800, 800, JobHandle.Wander,
                kind: 1));
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 1, 0), 600, 800, 800, JobHandle.Wait));

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: new PawnId(2));

            Assert.That(roster.TotalCount, Is.EqualTo(2), "two people; the animal is not counted");
            Assert.That(roster.Cards.Count, Is.EqualTo(2));
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(1)));
            Assert.That(roster.Cards[1].Id, Is.EqualTo(new PawnId(3)));
            Assert.That(new List<PawnId>(roster.CustomOrder), Has.No.Member(new PawnId(2)),
                "and it holds no slot to be dragged into");
        }

        [Test]
        public void PaginationDividesColonistsIntoDiscretePagesAndClampsPage()
        {
            var snapshot = Frame.Write();
            for (int i = 1; i <= 7; i++)
            {
                snapshot.AddPawn(new PawnView(new PawnId(i), new CellRef(i, 0, 0), 600, 800, 800, JobHandle.Wait));
            }

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: PawnId.None, capacity: 3);

            Assert.That(roster.TotalCount, Is.EqualTo(7));
            Assert.That(roster.PageCapacity, Is.EqualTo(3));
            Assert.That(roster.PageCount, Is.EqualTo(3));
            Assert.That(roster.Page, Is.EqualTo(0));
            Assert.That(roster.Cards.Count, Is.EqualTo(3));
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(1)));
            Assert.That(roster.Cards[2].Id, Is.EqualTo(new PawnId(3)));

            // Switch to page 1
            roster.SetPage(1);
            roster.Refresh(snapshot, selected: PawnId.None, capacity: 3);
            Assert.That(roster.Page, Is.EqualTo(1));
            Assert.That(roster.Cards.Count, Is.EqualTo(3));
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(4)));
            Assert.That(roster.Cards[2].Id, Is.EqualTo(new PawnId(6)));

            // Switch to page 2 (remainder)
            roster.SetPage(2);
            roster.Refresh(snapshot, selected: PawnId.None, capacity: 3);
            Assert.That(roster.Page, Is.EqualTo(2));
            Assert.That(roster.Cards.Count, Is.EqualTo(1));
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(7)));

            // Page clamping on overflow and underflow
            roster.SetPage(99);
            Assert.That(roster.Page, Is.EqualTo(2));
            roster.SetPage(-5);
            Assert.That(roster.Page, Is.EqualTo(0));
        }

        [Test]
        public void EnsurePageForSwitchesActivePageToTargetColonist()
        {
            var snapshot = Frame.Write();
            for (int i = 1; i <= 8; i++)
            {
                snapshot.AddPawn(new PawnView(new PawnId(i), new CellRef(i, 0, 0), 600, 800, 800, JobHandle.Wait));
            }

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: PawnId.None, capacity: 3);
            Assert.That(roster.Page, Is.EqualTo(0));

            // Pawn 7 is at index 6, which is page 2 (indices 6, 7)
            bool found = roster.EnsurePageFor(new PawnId(7));
            Assert.That(found, Is.True);
            Assert.That(roster.Page, Is.EqualTo(2));

            roster.Refresh(snapshot, selected: new PawnId(7), capacity: 3);
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(7)));
            Assert.That(roster.Cards[0].Selected, Is.True);

            // Searching for non-existent pawn returns false and leaves page untouched
            Assert.That(roster.EnsurePageFor(new PawnId(999)), Is.False);
            Assert.That(roster.Page, Is.EqualTo(2));
        }

        [Test]
        public void SwapExchangesTwoColonistPositionsInOrder()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 0, 0), 600, 800, 800, JobHandle.Wait));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 0, 0), 600, 800, 800, JobHandle.Wait));
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 0, 0), 600, 800, 800, JobHandle.Wait));

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: PawnId.None);

            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(1)));
            Assert.That(roster.Cards[2].Id, Is.EqualTo(new PawnId(3)));

            // Swap 1 and 3
            bool swapped = roster.Swap(new PawnId(1), new PawnId(3));
            Assert.That(swapped, Is.True);

            roster.Refresh(snapshot, selected: PawnId.None);
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(3)));
            Assert.That(roster.Cards[1].Id, Is.EqualTo(new PawnId(2)));
            Assert.That(roster.Cards[2].Id, Is.EqualTo(new PawnId(1)));
        }

        [Test]
        public void OrderReconciliationHandlesArrivalsAndDepartures()
        {
            var snapshot1 = Frame.Write();
            snapshot1.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 0, 0), 600, 800, 800, JobHandle.Wait));
            snapshot1.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 0, 0), 600, 800, 800, JobHandle.Wait));
            snapshot1.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 0, 0), 600, 800, 800, JobHandle.Wait));

            var roster = new RosterModel();
            roster.Refresh(snapshot1, selected: PawnId.None);

            // Customise order: put 3 first -> 3, 1, 2
            roster.Swap(new PawnId(1), new PawnId(3));

            // Snapshot 2: pawn 1 dies, pawn 4 arrives
            var snapshot2 = Frame.Write();
            snapshot2.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 0, 0), 600, 800, 800, JobHandle.Wait));
            snapshot2.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 0, 0), 600, 800, 800, JobHandle.Wait));
            snapshot2.AddPawn(new PawnView(new PawnId(4), new CellRef(4, 0, 0), 600, 800, 800, JobHandle.Wait));

            roster.Refresh(snapshot2, selected: PawnId.None);

            Assert.That(roster.Cards.Count, Is.EqualTo(3));
            Assert.That(roster.Cards[0].Id, Is.EqualTo(new PawnId(3)));
            Assert.That(roster.Cards[1].Id, Is.EqualTo(new PawnId(2)));
            Assert.That(roster.Cards[2].Id, Is.EqualTo(new PawnId(4)));
        }

        /// <summary>
        /// <b>Loading another colony gives the same slot a different person, and the card has to
        /// say so.</b>
        ///
        /// <para>The owner, 2026-09-18: <i>"the colonist info card and the roster top bar names
        /// don't match up ... maybe to do with loading and saving another game"</i>. Every colony
        /// numbers its pawns from one, so the first slot holds <c>PawnId(1)</c> in every game there
        /// has ever been. The roster card is a slot that re-reads itself only when the colonist in
        /// it changes, and it was asking the id alone — so after a load nothing had changed by that
        /// test, the name and face were never rewritten, and the bar went on showing the colony the
        /// player had left while the inspect pane, which reads afresh, showed the present one.</para>
        ///
        /// <para>This is the model half, which is where the fix belongs: the card publishes the
        /// seed its name came from, so the view compares a person rather than a number. The view
        /// half is one <c>||</c> in <c>HudShell.RefreshStrip</c> and cannot be reached from this
        /// tier.</para>
        /// </summary>
        [Test]
        public void TheSameIdInAnotherColonyIsAnotherColonist()
        {
            RosterCard First(uint rollSeed)
            {
                var snapshot = Frame.Write();
                var id = new PawnId(1);
                snapshot.AddPawn(new PawnView(id, new CellRef(1, 1, 1), 600, 600, 600, JobHandle.Haul));
                snapshot.AddPawnAspect(new PawnAspect(
                    id, AspectKey.Of(ColonistNames.RollSeedAspect), unchecked((int)rollSeed)));

                var roster = new RosterModel();
                roster.Refresh(snapshot, selected: PawnId.None);
                return roster.Cards[0];
            }

            RosterCard before = First(12345u);
            RosterCard after = First(98765u);

            Assert.That(after.Id, Is.EqualTo(before.Id),
                "the premise: a new colony reuses the same small pawn ids, which is why the id " +
                "alone cannot be what a roster slot keys on");
            Assert.That(after.Name, Is.Not.EqualTo(before.Name),
                "and the two are different people, because a name is the seed and the id together");
            Assert.That(after.Seed, Is.Not.EqualTo(before.Seed),
                "so the card must publish the seed its name came from. Without it the strip has " +
                "nothing to notice, and keeps the previous colony's names and faces after a load");
        }

        /// <summary>
        /// The bands are read against the scale the simulation actually publishes.
        ///
        /// Everything about mood in the interface was wrong until 2026-09-16 and none of it was
        /// visible in a test, because the fixtures used a scale the game does not produce. A
        /// colonist starts at 600 of 1000; against bands of 60 and 35 that is "content", and it
        /// stays "content" all the way down to 35 — which is to say the whole of the range a
        /// player would ever see was one band, every mood bar drew full, and a colonist could
        /// not go red before they were practically dead.
        ///
        /// So this test names the simulation's own numbers rather than round ones: a colonist as
        /// placed, a colonist in trouble, and the floor.
        /// </summary>
        [Test]
        public void MoodBandsReadTheScaleTheSimulationPublishes()
        {
            // Mood_Default in the content pack: baseMood 500, a colonist is placed at 600.
            Assert.That(MoodBands.Band(600), Is.EqualTo("content"), "a colonist as placed is content");
            Assert.That(MoodBands.Band(500), Is.EqualTo("strained"), "the mood base is not contentment");
            Assert.That(MoodBands.Band(200), Is.EqualTo("breaking"), "a colonist in real trouble is breaking");
            Assert.That(MoodBands.Band(0), Is.EqualTo("breaking"));

            // And the bands have to divide the range a player can see, or the bar says one thing
            // for the whole game. Three distinct answers across the middle of the scale.
            Assert.That(MoodBands.Band(900), Is.Not.EqualTo(MoodBands.Band(500)));
            Assert.That(MoodBands.Band(500), Is.Not.EqualTo(MoodBands.Band(200)));
        }
    }

    public class InspectModelTests
    {
        [Test]
        public void ColonistPaneShowsNeedsAndDisabledTabsAndCommands()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(4, 5, 1), 620, 710, 72, JobHandle.Eat));

            var pane = new InspectModel();
            pane.SetColonist(new PawnId(3));
            pane.Refresh(snapshot);

            Assert.That(pane.Subject, Is.EqualTo(InspectSubject.Colonist));
            Assert.That(pane.Title, Is.EqualTo(ColonistNamePool.Names[2]));
            Assert.That(pane.Job, Is.EqualTo("Eating"), "the registry's word for ui.status.eating");
            Assert.That(pane.Layer, Is.EqualTo(1));

            Assert.That(pane.Tabs.Count, Is.EqualTo(7));
            Assert.That(pane.Tabs[0].Name, Is.EqualTo("Needs"));
            Assert.That(pane.Tabs[0].Enabled, Is.True);
            Assert.That(pane.Tabs[1].Name, Is.EqualTo("Skills"));
            Assert.That(pane.Tabs.Count(t => t.Enabled), Is.EqualTo(2),
                "Needs and Skills are live; Gear, Thoughts, Social, Health and Log are visible " +
                "with reasons");

            Assert.That(pane.Commands, Is.Not.Empty);
            Assert.That(pane.Commands.Count(c => c.Enabled), Is.Zero,
                "no colonist command is wired yet, and none may pretend to be");
        }

        /// <summary>
        /// A clicked animal (design 29 §8): the pane says its species, what it is doing and
        /// where it is, and carries nothing a person has — no name, no tabs, no commands, no
        /// skills. The control is the colonist pane above, which has all of them.
        /// </summary>
        [Test]
        public void AnAnimalPaneSaysSpeciesActivityAndWhereAndNothingAPersonHas()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(4), new CellRef(6, 7, 2), 800, 800, 600, JobHandle.Wander,
                kind: 1));
            snapshot.AddPawn(new PawnView(new PawnId(5), new CellRef(6, 8, 2), 800, 800, 600, JobHandle.Wait,
                kind: 2));

            var pane = new InspectModel();
            pane.SetColonist(new PawnId(4));
            pane.Refresh(snapshot);

            Assert.That(pane.IsAnimal, Is.True);
            Assert.That(pane.Title, Is.EqualTo(Registry.Label("ui.pawn.hog")), "the species, not a person's name");
            Assert.That(pane.Subtitle, Is.EqualTo("animal"));
            Assert.That(pane.Job, Is.EqualTo(Registry.Label("ui.status.wandering")), "a leg reads as wandering, never as idle");
            Assert.That(pane.KindIconKey, Is.EqualTo("ui.pawn.hog"));
            Assert.That(pane.Layer, Is.EqualTo(2));
            Assert.That(pane.Tabs, Is.Empty);
            Assert.That(pane.Commands, Is.Empty);
            Assert.That(pane.Skills, Is.Empty);

            pane.SetColonist(new PawnId(5));
            pane.Refresh(snapshot);
            Assert.That(pane.Title, Is.EqualTo(Registry.Label("ui.pawn.rat")));
            Assert.That(pane.Job, Is.EqualTo(Registry.Label("ui.status.resting")), "a rest reads as resting");

            // And back to a person: the flag is cleared and the pane is hers again.
            snapshot.AddPawn(new PawnView(new PawnId(6), new CellRef(1, 1, 0), 620, 710, 720, JobHandle.Eat));
            pane.SetColonist(new PawnId(6));
            pane.Refresh(snapshot);
            Assert.That(pane.IsAnimal, Is.False);
            Assert.That(pane.Tabs, Is.Not.Empty);
        }

        /// <summary>
        /// The Skills tab lists every skill the design names, and carries a number only for the
        /// ones the simulation can actually train (owner, 2026-09-17).
        /// </summary>
        [Test]
        public void TheSkillsTabListsTheWholeDesignAndNumbersOnlyWhatIsSimulated()
        {
            var snapshot = Frame.Write();
            var id = new PawnId(3);
            snapshot.AddPawn(new PawnView(id, new CellRef(4, 5, 1), 620, 710, 720, JobHandle.Mine));

            // Published by the name the simulation publishes it under, spelled out in full rather
            // than taken from SkillCatalogue. Reading the key from the thing under test would make
            // this agree with itself whatever either side had been renamed to, and the name is the
            // whole contract: this assembly cannot reference Odyssey.Sim at all.
            Skill(snapshot, id, "mining", level: 7, passion: 2, experience: 9_500, progress: 375);
            Skill(snapshot, id, "cutting", level: 4, passion: 1, experience: 3_100);
            Skill(snapshot, id, "hauling", level: 2, passion: 0, experience: 1_400);
            Skill(snapshot, id, "construction", level: 3, passion: 0, experience: 2_200);
            Skill(snapshot, id, "growing", level: 1, passion: 2, experience: 1_100);

            var pane = new InspectModel();
            pane.SetColonist(id);
            pane.Refresh(snapshot);

            Assert.That(pane.Skills.Count, Is.EqualTo(SkillCatalogue.All.Length));
            Assert.That(pane.Skills.Count(s => s.Live), Is.EqualTo(4),
                "mining, chopping, construction and growing are the four the simulation backs; " +
                "hauling is a work type and not a skill in the design's list");

            SkillRow mining = pane.Skills.Single(s => s.IconKey == "ui.skill.mining");
            Assert.That(mining.Name, Is.EqualTo("Mining"), "the registry's word for ui.skill.mining");
            Assert.That(mining.Level, Is.EqualTo(7));
            Assert.That(mining.Passion, Is.EqualTo(2));
            Assert.That(mining.Experience, Is.EqualTo(9_500));
            Assert.That(mining.Progress, Is.EqualTo(375), "the bar arrives already divided (SK2)");

            // Chopping has a row of its own since 2026-09-18. It used to wear Growing's, so a
            // colonist who spent a day with an axe levelled up a skill called Growing and there
            // was no Chopping anywhere on screen — which is how the owner found it, by watching
            // WS2's curve work and going looking for the number behind it.
            SkillRow chopping = pane.Skills.Single(s => s.IconKey == "ui.skill.cutting");
            Assert.That(chopping.Name, Is.EqualTo("Chopping"), "the registry's word for ui.skill.cutting");
            Assert.That(chopping.Level, Is.EqualTo(4), "felling trains chopping");

            // Growing and Construction came off the dead list on 2026-09-20 (SK5). Both had been
            // greyed out here long after the simulation began training them — construction since
            // U26 and growing since U47 — so the one screen that says what a colonist can do was
            // denying two of the four things she actually does.
            SkillRow growing = pane.Skills.Single(s => s.IconKey == "ui.skill.growing");
            Assert.That(growing.Live, Is.True, "sowing and harvest both train growing (U47)");
            Assert.That(growing.Level, Is.EqualTo(1));

            SkillRow construction = pane.Skills.Single(s => s.IconKey == "ui.skill.construction");
            Assert.That(construction.Live, Is.True, "building, delivery and deconstruction all train it");
            Assert.That(construction.Level, Is.EqualTo(3));
            // No row borrows another skill's work any more. The Note field stays — it is what a
            // row uses to explain itself when the mapping is not obvious — but nothing needs it,
            // and asserting a borrow that no longer exists would pin the very thing this change
            // removed.
            Assert.That(pane.Skills.Where(s => s.Live).Select(s => s.IconKey),
                Is.EquivalentTo(new[]
                {
                    "ui.skill.mining", "ui.skill.cutting",
                    "ui.skill.construction", "ui.skill.growing",
                }),
                "the live rows are the simulation's own skills, each under its own name");

            foreach (SkillRow row in pane.Skills.Where(s => !s.Live))
            {
                Assert.That(row.Level, Is.Zero, $"{row.IconKey} has no simulation and no number");
                Assert.That(row.Progress, Is.Zero, $"{row.IconKey} has no simulation and no bar");
                Assert.That(row.Reason, Is.Not.Empty,
                    $"{row.IconKey} is disabled without saying why, which the catalogue forbids");
            }

            foreach (SkillRow row in pane.Skills.Where(s => s.Live))
                Assert.That(row.Reason, Is.Empty,
                    $"{row.IconKey} is live and still carries an excuse for not being");
        }

        /// <summary>
        /// Publish one colonist's standing in one skill, under the names
        /// <c>Odyssey.Sim.Pawns.SkillAspects</c> uses. The literals are the contract.
        /// </summary>
        static void Skill(WorldSnapshot snapshot, PawnId pawn, string skill,
                          int level, int passion, int experience, int progress = 0)
        {
            snapshot.AddPawnAspect(new PawnAspect(
                pawn, AspectKey.Of("odyssey.pawn.skill." + skill + ".level"), level));
            snapshot.AddPawnAspect(new PawnAspect(
                pawn, AspectKey.Of("odyssey.pawn.skill." + skill + ".passion"), passion));
            snapshot.AddPawnAspect(new PawnAspect(
                pawn, AspectKey.Of("odyssey.pawn.skill." + skill + ".experience"), experience));
            snapshot.AddPawnAspect(new PawnAspect(
                pawn, AspectKey.Of("odyssey.pawn.skill." + skill + ".progress"), progress));
        }

        /// <summary>
        /// A dead tab does nothing, and a live one switches. The pane's tab is model state, not
        /// view state, so it survives the refresh that follows every click.
        /// </summary>
        [Test]
        public void OnlyALiveTabCanBeShown()
        {
            var snapshot = Frame.Write();
            var id = new PawnId(1);
            snapshot.AddPawn(new PawnView(id, new CellRef(1, 1, 1), 600, 600, 600, JobHandle.Haul));

            var pane = new InspectModel();
            pane.SetColonist(id);
            pane.Refresh(snapshot);

            Assert.That(pane.ActiveTabName, Is.EqualTo("Needs"), "the pane opens on Needs");

            pane.ShowTab(1);
            Assert.That(pane.ActiveTabName, Is.EqualTo("Skills"));

            int gear = pane.Tabs.FindIndex(t => t.Name == "Gear");
            pane.ShowTab(gear);
            Assert.That(pane.ActiveTabName, Is.EqualTo("Skills"),
                "a disabled tab must not become the active one");

            pane.ShowTab(99);
            Assert.That(pane.ActiveTabName, Is.EqualTo("Skills"), "and neither may a tab that is not there");

            pane.Refresh(snapshot);
            Assert.That(pane.ActiveTabName, Is.EqualTo("Skills"),
                "the chosen tab survives a refresh, or clicking it would undo itself");
        }

        [Test]
        public void ColonistGoneFromTheFrameTombstonesInsteadOfClosing()
        {
            var withPawn = Frame.Write();
            withPawn.AddPawn(new PawnView(new PawnId(3), new CellRef(4, 5, 1), 620, 710, 72));

            var pane = new InspectModel();
            pane.SetColonist(new PawnId(3));
            pane.Refresh(withPawn);
            string lastName = pane.Title;

            pane.Refresh(Frame.Write()); // next frame has no such pawn

            Assert.That(pane.Tombstoned, Is.True);
            Assert.That(pane.Title, Is.EqualTo(lastName), "last-known values stay visible, greyed");
            Assert.That(pane.Commands.Count(c => c.Enabled), Is.Zero);
        }

        [Test]
        public void NoSelectionSummarisesTheColony()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 1), 500, 500, 50, JobHandle.Haul));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 2, 1), 500, 500, 50, JobHandle.Haul));

            var pane = new InspectModel();
            pane.ClearSelection();
            pane.Refresh(snapshot);

            Assert.That(pane.Subject, Is.EqualTo(InspectSubject.None));
            Assert.That(pane.ColonySize, Is.EqualTo(2));
            Assert.That(pane.JobCounts[JobHandle.Haul], Is.EqualTo(2));
            Assert.That(pane.JobCounts[JobHandle.Sleep], Is.Zero);
        }
    }

    public class LayerRulerModelTests
    {
        [Test]
        public void RowsRunTopFirstAndCountPawnsPerLayer()
        {
            var snapshot = Frame.Write(layers: 4, sliceLayer: 1);
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 3), 0, 0, 50));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 2, 3), 0, 0, 50));
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 3, 1), 0, 0, 50));

            var ruler = new LayerRulerModel();
            ruler.Refresh(snapshot, activeLayer: 1, surfaceLayer: 1);

            Assert.That(ruler.Rows.Count, Is.EqualTo(4));
            Assert.That(ruler.Rows[0].Layer, Is.EqualTo(3), "top layer first, so up is up");
            Assert.That(ruler.Rows[0].Pawns, Is.EqualTo(2));
            Assert.That(ruler.Rows[2].Layer, Is.EqualTo(1));
            Assert.That(ruler.Rows[2].Active, Is.True);
            Assert.That(ruler.Rows[2].Surface, Is.True);
        }

        [Test]
        public void OccupancyIsKnownOnlyForThePublishedSlice()
        {
            var snapshot = Frame.Write(layers: 3, sliceLayer: 2);
            var slice = snapshot.BeginSlice(10 * 10);
            for (int i = 0; i < slice.Length; i++) slice[i] = i < 25 ? (byte)1 : (byte)0;

            var ruler = new LayerRulerModel();
            ruler.Refresh(snapshot, activeLayer: 2, surfaceLayer: 2);

            Assert.That(ruler.Rows[0].Active, Is.True);          // layer 2, top of the list
            Assert.That(ruler.Rows[0].Occupancy, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(ruler.Rows[2].Occupancy, Is.EqualTo(-1f),
                "a layer whose cells the frame does not carry must not claim to be empty");
        }
    }

    public class LedgerModelTests
    {
        [Test]
        public void RealRowsCountStacksAndPlannedRowsStayGreyed()
        {
            // Two piles of meals, twenty and four, are twenty-four meals, not two; the ledger is
            // the number the player decides on, and a pile is not a number.
            var snapshot = Frame.Write();
            snapshot.AddThing(new ThingView(new ThingId(1), new CellRef(1, 1, 1), ItemHandle.Meal, 0, stack: 20));
            snapshot.AddThing(new ThingView(new ThingId(2), new CellRef(2, 2, 1), ItemHandle.Meal, 0, stack: 4));
            snapshot.AddThing(new ThingView(new ThingId(3), new CellRef(3, 3, 1), ItemHandle.Salvage, 0));
            snapshot.AddThing(new ThingView(new ThingId(4), new CellRef(4, 4, 1), ItemHandle.Wood, 0, stack: 20));

            var ledger = new LedgerModel();
            ledger.Refresh(snapshot);

            var meals = ledger.Rows.Find(r => r.Name == "Meal");
            Assert.That(meals.Real, Is.True, "the row is named as the registry names ui.res.meal");
            Assert.That(meals.Quantity, Is.EqualTo(24));
            Assert.That(ledger.Rows.Find(r => r.Name == "Wood").Quantity, Is.EqualTo(20), "felled wood is a real row");

            var scrap = ledger.Rows.FindAll(r => r.IconKey == "ui.res.scrap");
            Assert.That(scrap.Count, Is.EqualTo(1), "a commodity has one row, never a real and a planned one");
            Assert.That(scrap[0].Real, Is.True);
            Assert.That(ledger.Rows.Find(r => r.Name == "Alloy").Real, Is.False);
            Assert.That(ledger.Rows.Find(r => r.Name == "Alloy").Quantity, Is.Zero);
        }

        /// <summary>
        /// The field crop is counted, and it is counted wherever it lies.
        ///
        /// <para>It was not, until 2026-09-20, and the gap was reported as a bug in the
        /// simulation: a harvest left a pile, somebody hauled it to the store or ate it, and no
        /// readout anywhere showed a carrot afterwards — which from the keyboard is
        /// indistinguishable from the crop vanishing (owner: *"they seemed to disappear now"*).
        /// The crop was never lost; the ten-day field soak accounts for all 580 of them. There
        /// was simply nowhere on screen for the player to see one, which for a food crop is
        /// the same fault wearing a different coat.</para>
        /// </summary>
        [Test]
        public void TheFieldCropIsCountedWhereverItLies()
        {
            var snapshot = Frame.Write();
            // One pile still out in the field, one already carried into the store. Both are the
            // colony's carrots and the ledger is the colony's count, not the storeroom's.
            snapshot.AddThing(new ThingView(new ThingId(1), new CellRef(1, 1, 1), ItemHandle.Carrots, 0, stack: 5));
            snapshot.AddThing(new ThingView(new ThingId(2), new CellRef(9, 9, 1), ItemHandle.Carrots, 0, stack: 15));

            var ledger = new LedgerModel();
            ledger.Refresh(snapshot);

            var carrots = ledger.Rows.Find(r => r.IconKey == "ui.res.carrots");
            Assert.That(carrots, Is.Not.Null, "a harvested crop the player cannot see counted has, to them, vanished");
            Assert.That(carrots.Real, Is.True, "carrots exist in the game, so the row is not a planned one");
            Assert.That(carrots.Quantity, Is.EqualTo(20), "both piles are the colony's carrots");
        }

        /// <summary>An empty larder still shows the row, so the player can see it is empty
        /// rather than wonder whether the game has forgotten the crop.</summary>
        [Test]
        public void TheCropRowStandsEvenWithNoCarrotsInIt()
        {
            var ledger = new LedgerModel();
            ledger.Refresh(Frame.Write());

            var carrots = ledger.Rows.Find(r => r.IconKey == "ui.res.carrots");
            Assert.That(carrots, Is.Not.Null);
            Assert.That(carrots.Quantity, Is.Zero);
        }
    }
}
