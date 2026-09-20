#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The Work tab's model half, which is the half the fast tier can prove.
    ///
    /// <para>Three of these are acceptance criteria from the owner's supplied specification,
    /// restated as arithmetic: the rotated labels must not collide, an incapable cell must contain
    /// exactly one glyph, and switching Simple to Detailed must change nothing but the glyph.
    /// <c>docs/design/27-work-tab.md</c> §9 is the full list and says which two are deliberately
    /// not met.</para>
    /// </summary>
    public class WorkGridTests
    {
        static readonly PawnId Ada = new PawnId(1);
        static readonly PawnId Bram = new PawnId(2);

        static int Column(string key)
        {
            for (int i = 0; i < WorkCatalogue.All.Count; i++)
                if (WorkCatalogue.All[i].Key == key) return i;
            return -1;
        }

        static readonly int Mining = Column("ui.work.mining");
        static readonly int Hauling = Column("ui.work.hauling");
        static readonly int Research = Column("ui.work.research");

        /// <summary>A frame with two colonists and whatever numbers a test wants on Ada.</summary>
        static WorldSnapshot Frame(int miningPriority = 3, int miningLevel = 12,
            int miningPassion = 2, bool capable = true)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 4), sliceLayer: 1);
            snapshot.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), food: 900, rest: 800, mood: 50));
            snapshot.AddPawn(new PawnView(Bram, new CellRef(2, 1, 1), food: 500, rest: 400, mood: 20));

            WorkCatalogue.Entry mining = WorkCatalogue.All[Mining];
            snapshot.AddPawnAspect(new PawnAspect(Ada, mining.Priority, miningPriority));
            snapshot.AddPawnAspect(new PawnAspect(Ada, mining.Capable, capable ? 1 : 0));
            snapshot.AddPawnAspect(new PawnAspect(Ada, mining.Level, miningLevel));
            snapshot.AddPawnAspect(new PawnAspect(Ada, mining.Passion, miningPassion));

            WorkCatalogue.Entry haul = WorkCatalogue.All[Hauling];
            snapshot.AddPawnAspect(new PawnAspect(Ada, haul.Priority, 1));
            snapshot.AddPawnAspect(new PawnAspect(Ada, haul.Capable, 1));
            return snapshot;
        }

        static IReadOnlyList<PawnId> Order => new[] { Ada, Bram };

        static WorkGridModel Model(WorldSnapshot frame)
        {
            var model = new WorkGridModel();
            model.Refresh(frame, Order, new[] { Ada });
            return model;
        }

        // ------------------------------------------------------------------ the columns

        [Test]
        public void TheGridDrawsTheDesignsTwentyTwoAndNotTheSimulationsFour()
        {
            Assert.That(WorkCatalogue.All.Count, Is.EqualTo(22));
            Assert.That(WorkCatalogue.LiveCount, Is.EqualTo(4),
                "Construction, Chopping, Mining and Hauling are what WorkTypes.xml runs.");
        }

        [Test]
        public void EveryLiveColumnCarriesAHandleAndEveryDeadOneCarriesAReason()
        {
            foreach (WorkCatalogue.Entry entry in WorkCatalogue.All)
            {
                if (entry.Live)
                {
                    Assert.That(entry.Handle, Is.InRange(0, WorkHandle.Count - 1),
                        entry.Key + " is live, so it must name the work handle the intent carries.");
                    Assert.That(entry.Reason, Is.Empty, entry.Key + " is live and needs no excuse.");
                }
                else
                {
                    Assert.That(entry.Handle, Is.EqualTo(WorkHandle.None));
                    Assert.That(entry.Reason, Is.Not.Empty,
                        entry.Key + " is not built, and a column with no reason is a column that " +
                        "looks broken.");
                }
            }
        }

        [Test]
        public void NoTwoLiveColumnsClaimTheSameHandle()
        {
            var seen = new HashSet<int>();
            foreach (WorkCatalogue.Entry entry in WorkCatalogue.All)
            {
                if (!entry.Live) continue;
                Assert.That(seen.Add(entry.Handle), Is.True,
                    entry.Key + " shares a work handle with another column, so every click on one " +
                    "would land on the other.");
            }
            Assert.That(seen.Count, Is.EqualTo(WorkHandle.Count));
        }

        [Test]
        public void HaulingIsTheOneWorkTypeWithNoSkill()
        {
            foreach (WorkCatalogue.Entry entry in WorkCatalogue.All)
            {
                if (!entry.Live) continue;
                bool isHaul = entry.Key == "ui.work.hauling";
                Assert.That(entry.HasSkill, Is.EqualTo(!isHaul),
                    entry.Key + " — WorkTypes.xml gives only Work_Haul no rateSkill.");
            }
        }

        // ------------------------------------------------------------------ the rotation

        [Test]
        public void LabelsClearTheirNeighbours()
        {
            string longest = WorkGridLayout.LongestLabel();
            float footprint = WorkGridLayout.Footprint(longest);

            Assert.That(WorkGridLayout.LongestLabelFits(), Is.True,
                $"\"{longest}\" projects {footprint:0.0}px across a {WorkGridLayout.Pitch}px pitch " +
                $"at {WorkGridLayout.LabelAngleDegrees}°, so it overlaps the column beside it. " +
                $"The shallowest angle this pitch allows is " +
                $"{WorkGridLayout.ShallowestAngleFor(WorkGridLayout.LabelWidth(longest)):0.0}°.");
        }

        [Test]
        public void TheHeaderBandIsTallEnoughForTheLabelItHasToHold()
        {
            float rise = WorkGridLayout.LabelRise(WorkGridLayout.LongestLabel());

            Assert.That(rise, Is.LessThanOrEqualTo(WorkGridLayout.LabelBand),
                "The label stands taller than the band reserved for it, so it would cross its " +
                "own icon tile — which is an acceptance criterion in its own right.");
            Assert.That(WorkGridLayout.HeaderBand,
                Is.EqualTo(WorkGridLayout.LabelBand + WorkGridLayout.LabelGap + WorkGridLayout.IconTile));
        }

        [Test]
        public void TheCellIsCentredInThePitch()
        {
            int slack = WorkGridLayout.Pitch - WorkGridLayout.Cell;

            Assert.That(slack, Is.GreaterThan(0));
            Assert.That(slack % 2, Is.Zero, "An odd slack cannot centre a cell in its column.");
        }

        // ------------------------------------------------------------------ reading a cell

        [Test]
        public void ACellReadsItsFourSignalsOutOfTheAspects()
        {
            WorkCell cell = Model(Frame()).Rows[0].Cells[Mining];

            Assert.That(cell.Built, Is.True);
            Assert.That(cell.Capable, Is.True);
            Assert.That(cell.Priority, Is.EqualTo(3));
            Assert.That(cell.Level, Is.EqualTo(12));
            Assert.That(cell.Passion, Is.EqualTo(2));
            Assert.That(cell.Band, Is.EqualTo(ProficiencyBand.Skilled));
        }

        [Test]
        public void AnIncapableCellIsInertAtTheModelAndNotInTheStylesheet()
        {
            WorkCell cell = Model(Frame(miningPriority: 2, miningPassion: 2, capable: false))
                .Rows[0].Cells[Mining];

            Assert.That(cell.Capable, Is.False);
            Assert.That(cell.Priority, Is.Zero, "A priority behind an incapable cell leaks.");
            Assert.That(cell.Passion, Is.Zero);
            Assert.That(cell.Level, Is.EqualTo(-1));
            Assert.That(cell.Interactive, Is.False);
            Assert.That(cell.Glyph(WorkGridMode.Detailed), Is.EqualTo("—"));
            Assert.That(cell.Glyph(WorkGridMode.Simple), Is.EqualTo("—"),
                "The em-dash is the same in both modes: incapability is not a mode.");
        }

        [Test]
        public void AnUnbuiltColumnIsEmptyRatherThanIncapable()
        {
            WorkCell cell = Model(Frame()).Rows[0].Cells[Research];

            Assert.That(cell.Built, Is.False);
            Assert.That(cell.Interactive, Is.False);
            Assert.That(cell.Glyph(WorkGridMode.Detailed), Is.Empty,
                "An unbuilt column must not wear the incapable em-dash — they are different facts.");
        }

        [Test]
        public void HaulingHasNoBandAndNoFlame()
        {
            WorkCell cell = Model(Frame()).Rows[0].Cells[Hauling];

            Assert.That(cell.Built, Is.True);
            Assert.That(cell.Level, Is.EqualTo(-1));
            Assert.That(cell.Passion, Is.Zero);
            Assert.That(cell.Band, Is.EqualTo(ProficiencyBand.None));
            Assert.That(WorkBands.ColourOf(cell.Band).Hex, Is.EqualTo(HudTheme.TextFaint.Hex));
        }

        [Test]
        public void AMissingPriorityReadsAsTheDefaultAndNotAsNever()
        {
            // The frame a panel meets before the first publish, or a build where the aspect is
            // absent. Reading it as zero would silently un-assign the whole colony.
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 4), sliceLayer: 1);
            snapshot.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), food: 900, rest: 800, mood: 50));

            WorkCell cell = WorkGridModel.ReadCell(snapshot, Ada, Mining);

            Assert.That(cell.Priority, Is.EqualTo(WorkGridModel.DefaultPriority));
            Assert.That(cell.Capable, Is.True, "Absent capability means capable, not incapable.");
        }

        // ------------------------------------------------------------------ the gestures

        [Test]
        public void DetailedClickWalksTheRingForwardAndRightClickWalksItBack()
        {
            var model = new WorkGridModel { Mode = WorkGridMode.Detailed };

            Assert.That(model.Cycle(1), Is.EqualTo(2));
            Assert.That(model.Cycle(4), Is.EqualTo(WorkGridModel.Never));
            Assert.That(model.Cycle(WorkGridModel.Never), Is.EqualTo(1));

            Assert.That(model.CycleBack(2), Is.EqualTo(1));
            Assert.That(model.CycleBack(1), Is.EqualTo(WorkGridModel.Never));
            Assert.That(model.CycleBack(WorkGridModel.Never), Is.EqualTo(4));
        }

        [Test]
        public void SimpleModeWritesTheDefaultRatherThanInventingAPriority()
        {
            var model = new WorkGridModel { Mode = WorkGridMode.Simple };

            Assert.That(model.Cycle(WorkGridModel.Never), Is.EqualTo(WorkGridModel.DefaultPriority));
            Assert.That(model.Cycle(2), Is.EqualTo(WorkGridModel.Never));
        }

        [Test]
        public void SwitchingModeChangesTheGlyphAndNothingElse()
        {
            var model = Model(Frame(miningPriority: 2));
            WorkCell cell = model.Rows[0].Cells[Mining];

            Assert.That(cell.Glyph(WorkGridMode.Detailed), Is.EqualTo("2"));
            Assert.That(cell.Glyph(WorkGridMode.Simple), Is.Empty,
                "Simple's answer is a drawn mark, so the label must not also carry one");
            Assert.That(cell.Mark(WorkGridMode.Simple), Is.EqualTo(WorkMark.Will));
            Assert.That(cell.Mark(WorkGridMode.Detailed), Is.EqualTo(WorkMark.None));

            // The three signals the mode must not touch.
            Assert.That(cell.Band, Is.EqualTo(ProficiencyBand.Skilled));
            Assert.That(cell.Passion, Is.EqualTo(2));
            Assert.That(cell.Capable, Is.True);
        }

        [Test]
        public void ABlankButCapableCellStillShowsItsSkill()
        {
            WorkCell cell = Model(Frame(miningPriority: 0, miningLevel: 17)).Rows[0].Cells[Mining];

            Assert.That(cell.Capable, Is.True);
            Assert.That(cell.Glyph(WorkGridMode.Detailed), Is.Empty);
            Assert.That(cell.Glyph(WorkGridMode.Simple), Is.Empty);
            Assert.That(cell.Mark(WorkGridMode.Simple), Is.EqualTo(WorkMark.Wont));
            Assert.That(cell.Band, Is.EqualTo(ProficiencyBand.Master),
                "Blank is a priority, not an absence of a colonist.");
        }

        [Test]
        public void AClickEmitsTheWorkHandleAndNotThePanelsColumnNumber()
        {
            var model = Model(Frame(miningPriority: 3));

            Assert.That(model.TryClick(0, Mining, out Intent intent), Is.True);
            Assert.That(intent.Kind, Is.EqualTo(IntentKind.SetWorkPriority));
            Assert.That(intent.A, Is.EqualTo(Ada.Value));
            Assert.That(intent.B, Is.EqualTo(WorkHandle.Mining));
            Assert.That(intent.B, Is.Not.EqualTo(Mining),
                "The column number and the handle must not be conflated; they differ here on purpose.");
            Assert.That(intent.C, Is.EqualTo(4));
        }

        [Test]
        public void AnInertCellEmitsNothing()
        {
            var model = Model(Frame(capable: false));

            Assert.That(model.TryClick(0, Mining, out _), Is.False);
            Assert.That(model.TryClick(0, Research, out _), Is.False);
        }

        [Test]
        public void TheWorkGridIsAppliedWhilePaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.SetWorkPriority), Is.True,
                "A grid that took twenty clicks and applied none until you pressed play.");
        }

        // ------------------------------------------------------------------ the bands

        [Test]
        public void EveryProficiencyBandIsReachableAndDistinct()
        {
            Assert.That(WorkBands.BandOf(0), Is.EqualTo(ProficiencyBand.Novice));
            Assert.That(WorkBands.BandOf(3), Is.EqualTo(ProficiencyBand.Novice));
            Assert.That(WorkBands.BandOf(4), Is.EqualTo(ProficiencyBand.Apprentice));
            Assert.That(WorkBands.BandOf(6), Is.EqualTo(ProficiencyBand.Apprentice));
            Assert.That(WorkBands.BandOf(7), Is.EqualTo(ProficiencyBand.Competent));
            Assert.That(WorkBands.BandOf(10), Is.EqualTo(ProficiencyBand.Competent));
            Assert.That(WorkBands.BandOf(11), Is.EqualTo(ProficiencyBand.Skilled));
            Assert.That(WorkBands.BandOf(14), Is.EqualTo(ProficiencyBand.Skilled));
            Assert.That(WorkBands.BandOf(15), Is.EqualTo(ProficiencyBand.Master));
            Assert.That(WorkBands.BandOf(20), Is.EqualTo(ProficiencyBand.Master));

            var hexes = new HashSet<string>();
            for (int b = 0; b <= 4; b++)
                Assert.That(hexes.Add(WorkBands.ColourOf((ProficiencyBand)b).Hex), Is.True,
                    "Two bands share a colour, so two skill ranges are indistinguishable.");
        }

        [Test]
        public void ThePriorityInksDescendInBrightness()
        {
            // The reading rule: one is the brightest and four is the dimmest, so a scan down a
            // column reads urgency without reading the digits.
            int Luma(HudColour c) => c.R * 299 + c.G * 587 + c.B * 114;

            Assert.That(Luma(WorkBands.InkOf(1)), Is.GreaterThan(Luma(WorkBands.InkOf(2))));
            Assert.That(Luma(WorkBands.InkOf(2)), Is.GreaterThan(Luma(WorkBands.InkOf(3))));
            Assert.That(Luma(WorkBands.InkOf(3)), Is.GreaterThan(Luma(WorkBands.InkOf(4))));
        }

        [Test]
        public void ABlankCellIsLighterThanAnAssignedOneSoAnEmptyColumnIsNotAHole()
        {
            Assert.That(WorkBands.BlankFill.A, Is.LessThan(WorkBands.AssignedFill.A));
        }

        // ------------------------------------------------------------------ the words

        [Test]
        public void ACellSaysItsFourSignalsInWords()
        {
            var model = Model(Frame(miningPriority: 2, miningLevel: 7, miningPassion: 1));

            Assert.That(model.Describe(0, Mining),
                Does.Contain("priority 2").And.Contain("level 7").And.Contain("interested"));
            Assert.That(model.Describe(0, Hauling), Does.Contain("hauling has no skill"));
            Assert.That(model.Describe(0, Research), Does.Contain(WorkCatalogue.All[Research].Reason));
        }

        [Test]
        public void TheSubtitleSaysWhatTheWholeTableIs()
        {
            // "what they do, and when" rather than naming the two halves: the claim of the
            // combined table is that they are one question, not two panels side by side.
            Assert.That(Model(Frame()).Subtitle(), Is.EqualTo("2 colonists · what they do, and when"));
            Assert.That(Model(Frame()).Subtitle(nowHour: 13),
                Is.EqualTo("2 colonists · what they do, and when · 13h"));
        }

        /// <summary>
        /// The day has one owner and it is <see cref="ScheduleHandle.Hours"/>.
        ///
        /// <para>The layout's own doc said so while the code said 24, which is the shape of fault
        /// this project keeps meeting: one rule, two owners, and a disagreement that fails
        /// silently because nothing ever asks the two the same question.</para>
        /// </summary>
        [Test]
        public void TheDayIsTwentyFourHoursInOnlyOnePlace()
        {
            Assert.That(WorkGridLayout.Hours, Is.EqualTo(ScheduleHandle.Hours));
            Assert.That(WorkGridLayout.ScheduleWidth,
                Is.EqualTo(ScheduleHandle.Hours * WorkGridLayout.HourPitch));

            // And the clock's day, which is the third holder of this number and the only one that
            // can move it: the now-line is placed by GameClock.HourOfDay and lands on a column
            // this layout drew. ScheduleTests says the same thing from the simulation side and
            // has to assert the literal, because that assembly cannot see GameClock at all — this
            // is the assembly where the two are both in scope, so this is where they are joined.
            Assert.That(GameClock.HoursPerDay, Is.EqualTo(ScheduleHandle.Hours));
            Assert.That(GameClock.HourOfDay(GameClock.TicksPerDay - 1),
                Is.EqualTo(WorkGridLayout.Hours - 1),
                "the last tick of the day must land on the last column the schedule half draws");
        }

        /// <summary>
        /// The combined table wants more width than a small screen has, and the panel has to
        /// answer that by scrolling rather than by hanging off the edge.
        ///
        /// <para>The panel is pinned to the left edge and absolutely positioned, so a fixed width
        /// wider than the window does not shrink it — it puts the schedule half past the right of
        /// the screen where no scroller can reach it. This pins the two facts the cap rests on:
        /// the table fits the 1,920 reference, and it does not fit a 1,600 one, which is why
        /// <see cref="WorkGridLayout.MaxWidthPercent"/> is not decoration.</para>
        /// </summary>
        [Test]
        public void TheTableFitsTheReferenceScreenAndNotASmallerOne()
        {
            int columns = WorkCatalogue.All.Count;

            Assert.That(WorkGridLayout.CombinedWidthFor(columns), Is.LessThanOrEqualTo(1920),
                "the whole point of the 34px pitch is that 22 work types and a day fit at 1920");
            Assert.That(WorkGridLayout.FitsScreen(columns, 1920), Is.True);
            Assert.That(WorkGridLayout.FitsScreen(columns, 1600), Is.False,
                "if this ever passes the cap has stopped earning its keep and the scroller with it");
            Assert.That(WorkGridLayout.MaxWidthPercent, Is.InRange(50, 100));
        }
    }
}
