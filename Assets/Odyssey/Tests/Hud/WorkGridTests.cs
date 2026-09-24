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

        /// <summary>
        /// A colony of any size, for the paging tests. Deliberately not <see cref="Frame"/> with a
        /// count: that one's first argument is a mining priority and has been since the grid was
        /// written.
        /// </summary>
        static WorldSnapshot Colony(int colonists)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(64, 64, 4), sliceLayer: 1);
            for (int i = 0; i < colonists; i++)
                snapshot.AddPawn(new PawnView(new PawnId(i + 1), new CellRef(i % 60 + 1, 1, 1),
                    food: 900, rest: 800, mood: 50));
            return snapshot;
        }

        /// <summary>
        /// A colony with a chosen mining level and hauling priority per colonist, for the sort
        /// tests. The arrays are read in the roster's order, so index 0 is the first row.
        /// </summary>
        static WorldSnapshot ColonyWith(int[] miningLevels, int[] haulPriorities)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(64, 64, 4), sliceLayer: 1);
            WorkCatalogue.Entry mining = WorkCatalogue.All[Mining];
            WorkCatalogue.Entry haul = WorkCatalogue.All[Hauling];

            for (int i = 0; i < miningLevels.Length; i++)
            {
                var id = new PawnId(i + 1);
                snapshot.AddPawn(new PawnView(id, new CellRef(i + 1, 1, 1),
                    food: 900, rest: 800, mood: 50));
                snapshot.AddPawnAspect(new PawnAspect(id, mining.Capable, 1));
                snapshot.AddPawnAspect(new PawnAspect(id, mining.Level, miningLevels[i]));
                snapshot.AddPawnAspect(new PawnAspect(id, haul.Capable, 1));
                snapshot.AddPawnAspect(new PawnAspect(id, haul.Priority, haulPriorities[i]));
            }
            return snapshot;
        }

        static List<int> RowIds(WorkGridModel model)
        {
            var ids = new List<int>();
            for (int r = 0; r < model.Rows.Count; r++) ids.Add(model.Rows[r].Id.Value);
            return ids;
        }

        /// <summary>The roster order the shell would hand the model: everybody, as published.</summary>
        static IReadOnlyList<PawnId> OrderOf(WorldSnapshot frame)
        {
            var order = new List<PawnId>();
            var pawns = frame.Pawns;
            for (int i = 0; i < pawns.Length; i++) order.Add(pawns[i].Id);
            return order;
        }

        static WorkGridModel Model(WorldSnapshot frame)
        {
            var model = new WorkGridModel();
            model.Refresh(frame, Order, new[] { Ada });
            return model;
        }

        // ------------------------------------------------------------------ the columns

        /// <summary>
        /// The grid draws the design's list, not the simulation's — and the gap between the two
        /// is the thing that is supposed to close.
        ///
        /// <para>It was four live columns of twenty-two when the panel was written and is five
        /// since the growing zones landed on 2026-09-20. <b>The live count is asserted rather than
        /// left loose</b> so that a work type reaching the simulation and not reaching this
        /// catalogue fails here: the column would otherwise go on drawing itself as <i>not built
        /// yet</i> over work the colony can now actually do, and nobody would find out by
        /// looking.</para>
        /// </summary>
        [Test]
        public void TheGridDrawsTheDesignsTwentyTwoAndNotTheSimulationsFive()
        {
            Assert.That(WorkCatalogue.All.Count, Is.EqualTo(22));
            // Six since the combat contracts step (design 33 §5): Rescue's column went live with
            // Work_Rescue, whose giver answers no until C4 fills it.
            Assert.That(WorkCatalogue.LiveCount, Is.EqualTo(6),
                "Construction, Chopping, Mining, Hauling, Growing and Rescue are what WorkTypes.xml runs.");

            // And every one the simulation runs has a column: the two counts are the same list
            // seen from two sides, so a fifth work type with no column is this test failing.
            Assert.That(WorkCatalogue.LiveCount, Is.EqualTo(WorkHandle.Count),
                "a work type the simulation runs with no live column here is a column still " +
                "telling the player it does not exist yet");
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
        public void HaulingAndRescueAreTheWorkTypesWithNoSkill()
        {
            // Rescue joined hauling with the combat contracts step (design 33 §5): carrying a body
            // is a walk, priced by the pawn's pace, as a haul is.
            foreach (WorkCatalogue.Entry entry in WorkCatalogue.All)
            {
                if (!entry.Live) continue;
                bool noSkill = entry.Key == "ui.work.hauling" || entry.Key == "ui.work.rescue";
                Assert.That(entry.HasSkill, Is.EqualTo(!noSkill),
                    entry.Key + " — WorkTypes.xml gives only Work_Haul and Work_Rescue no rateSkill.");
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
                "The label stands taller than the band reserved for it, so it would climb into " +
                "the title strip above it — an acceptance criterion in its own right.");

            // The band is the title strip and the labels, and nothing else. The icon tiles that
            // used to sit under every label are gone (owner, 2026-09-20) and the label runs down
            // into the space they left, which is what freed the strip for the pagers and the
            // Schedule title.
            Assert.That(WorkGridLayout.HeaderBand,
                Is.EqualTo(WorkGridLayout.TitleStrip + WorkGridLayout.LabelBand));
            Assert.That(WorkGridLayout.TitleStrip, Is.GreaterThanOrEqualTo(20),
                "the strip has to hold a 20px pager button");
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

            Assert.That(model.TryClick(0, Mining, back: false, out Intent intent), Is.True);
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

            Assert.That(model.TryClick(0, Mining, back: false, out _), Is.False);
            Assert.That(model.TryClick(0, Research, back: false, out _), Is.False);
            Assert.That(model.CellIsInteractive(0, Research), Is.False);
            Assert.That(model.CellIsInteractive(0, -1), Is.False, "and it bounds-checks");
            Assert.That(model.CellIsInteractive(99, Mining), Is.False);
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
        /// <b>The panel is one width, always, and it fits.</b>
        ///
        /// <para>It was a scroller and a percentage cap until the owner's call on 2026-09-20:
        /// <i>"remove the scroll bars — this isn't a good interface — replace with pagination
        /// similar to the roster pagination"</i>. A scroller made the panel's shape a function of
        /// the window, so the control resized under the player; a page does not. The number that
        /// matters is that one page of columns plus the whole day clears a 1366-wide window, which
        /// is the smallest thing anybody would play this on.</para>
        /// </summary>
        [Test]
        public void ThePanelIsOneFixedWidthAndItFitsASmallScreen()
        {
            Assert.That(WorkGridLayout.PanelWidth, Is.EqualTo(1383));
            Assert.That(WorkGridLayout.PanelWidth,
                Is.EqualTo(WorkGridLayout.CombinedWidthFor(WorkGridLayout.ColumnsPerPage)),
                "the panel is exactly one page of columns and the whole day, and nothing else");

            // 1,383 and two for the frame, from HudLayout.Edge, which is zero. So the floor is a
            // 1440-wide window — the owner chose eleven columns over eight knowing that, because
            // eight would have fitted 1366 and split the catalogue into three ragged pages.
            Assert.That(WorkGridLayout.PanelWidth + 2, Is.LessThanOrEqualTo(1440));
            Assert.That(WorkGridLayout.PanelWidth + 2,
                Is.LessThanOrEqualTo(HudLayout.ReferenceWidth * 3 / 4),
                "and it leaves a quarter of the reference screen showing the world beside it");

            // And the whole catalogue does not fit, which is why there is more than one page.
            Assert.That(WorkGridLayout.CombinedWidthFor(WorkCatalogue.All.Count),
                Is.GreaterThan(1440),
                "if all twenty-two columns ever fit a small screen, the column pager is dead weight");
        }

        /// <summary>
        /// Twenty-two columns over eleven is two full pages, and the slot-to-column map that the
        /// shell aims its eleven cells with agrees at both ends of both of them.
        /// </summary>
        [Test]
        public void TheColumnsPageInTwoAndEverySlotKnowsItsColumn()
        {
            var model = Model(Frame());

            Assert.That(WorkGridLayout.ColumnsPerPage, Is.EqualTo(11));
            Assert.That(WorkCatalogue.All.Count % WorkGridLayout.ColumnsPerPage, Is.Zero,
                "eleven was chosen because twenty-two divides by it; a catalogue that no longer " +
                "does leaves a ragged last page, which is legal but was not the deal");
            Assert.That(model.ColumnPageCount, Is.EqualTo(2));

            Assert.That(model.ColumnPage, Is.Zero, "it opens on the first page");
            Assert.That(model.ColumnAt(0), Is.Zero);
            Assert.That(model.ColumnAt(10), Is.EqualTo(10));
            Assert.That(model.VisibleColumns, Is.EqualTo(11));

            model.SetColumnPage(1);
            Assert.That(model.ColumnAt(0), Is.EqualTo(11));
            Assert.That(model.ColumnAt(10), Is.EqualTo(21));
            Assert.That(model.VisibleColumns, Is.EqualTo(11));

            // Clamped at both ends rather than wrapping: a pager arrow that does nothing is
            // better than one that jumps to the other end of the table.
            model.SetColumnPage(9);
            Assert.That(model.ColumnPage, Is.EqualTo(1));
            model.SetColumnPage(-3);
            Assert.That(model.ColumnPage, Is.Zero);

            // A slot past the catalogue is -1 and never an index into it.
            Assert.That(model.ColumnAt(WorkGridLayout.ColumnsPerPage), Is.EqualTo(-1));
            Assert.That(model.ColumnAt(-1), Is.EqualTo(-1));
        }

        // ============================================================ paging

        /// <summary>
        /// <b>A page of rows is twelve, whatever the colony is.</b>
        ///
        /// <para>This is the performance half of the owner's pagination call and the reason it is
        /// asserted rather than assumed: before it, <c>Refresh</c> built a row and two lists for
        /// every colonist alive and the shell built twenty-two cells for each of them, so the
        /// panel's cost grew with the colony and clipped, unreachable, at about twenty-four rows.
        /// Now the model's work is bounded and so is the element count behind it.</para>
        /// </summary>
        [Test]
        public void APageOfRowsIsTwelveHoweverLargeTheColonyIs()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = Colony(40);
            IReadOnlyList<PawnId> order = OrderOf(frame);

            model.Refresh(frame, order, null);

            Assert.That(model.TotalRows, Is.EqualTo(40), "the colony is all of them");
            Assert.That(model.Rows.Count, Is.EqualTo(WorkGridLayout.RowsPerPage),
                "and the page is twelve of them");
            Assert.That(model.RowPageCount, Is.EqualTo(4), "40 over 12 is four pages");

            // The last page is the remainder and not a padded twelve.
            model.SetRowPage(3);
            model.Refresh(frame, order, null);
            Assert.That(model.Rows.Count, Is.EqualTo(4));
            Assert.That(model.RowPage, Is.EqualTo(3));

            // Clamped at both ends, like the columns.
            model.SetRowPage(99);
            Assert.That(model.RowPage, Is.EqualTo(3));
            model.SetRowPage(-1);
            Assert.That(model.RowPage, Is.Zero);
        }

        /// <summary>
        /// Each page holds the colonists it should, in the roster's order, with nobody repeated
        /// and nobody missed.
        /// </summary>
        [Test]
        public void EveryColonistIsOnExactlyOnePage()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = Colony(40);
            IReadOnlyList<PawnId> order = OrderOf(frame);

            var seen = new List<PawnId>();
            for (int page = 0; page < 4; page++)
            {
                model.SetRowPage(page);
                model.Refresh(frame, order, null);
                for (int r = 0; r < model.Rows.Count; r++) seen.Add(model.Rows[r].Id);
            }

            Assert.That(seen.Count, Is.EqualTo(40));
            Assert.That(seen, Is.EqualTo(order).AsCollection,
                "the pages laid end to end are the roster, in the roster's order");
        }

        /// <summary>
        /// Selecting somebody brings their page up. <c>RosterModel.EnsurePageFor</c>'s job, and
        /// the panel would look broken without it: a selection it answered with a page the
        /// colonist is not on is a selection that appears to have done nothing.
        /// </summary>
        [Test]
        public void SelectingAColonistBringsTheirPageUp()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = Colony(40);
            IReadOnlyList<PawnId> order = OrderOf(frame);
            model.Refresh(frame, order, null);

            Assert.That(model.EnsureRowPageFor(order[25]), Is.True, "page 2 is not page 0");
            Assert.That(model.RowPage, Is.EqualTo(2), "25 / 12 is page two");

            Assert.That(model.EnsureRowPageFor(order[26]), Is.False,
                "already on their page, so nothing moves and nothing is redrawn");

            Assert.That(model.EnsureRowPageFor(default), Is.False, "nobody is on no page");
        }

        /// <summary>
        /// A colony that shrinks under a player reading its last page does not leave them on a
        /// page that no longer exists.
        /// </summary>
        [Test]
        public void APageThatStopsExistingClampsRatherThanEmptying()
        {
            var model = new WorkGridModel();
            WorldSnapshot big = Colony(40);
            model.Refresh(big, OrderOf(big), null);
            model.SetRowPage(3);
            model.Refresh(big, OrderOf(big), null);
            Assert.That(model.Rows.Count, Is.EqualTo(4));

            WorldSnapshot small = Colony(2);
            model.Refresh(small, OrderOf(small), null);

            Assert.That(model.RowPage, Is.Zero, "there is only one page now");
            Assert.That(model.Rows.Count, Is.EqualTo(2), "and it is not empty");
        }

        /// <summary>
        /// <b>A steady refresh allocates no rows.</b> ADR 0003's flip condition F1 is about
        /// per-frame allocation, and this panel refreshes on the mid bucket for as long as it is
        /// open — so the rows are recycled. A page is twelve at most, which is what makes a pool
        /// worth having: unbounded, it would only have been a list that never shrank.
        /// </summary>
        [Test]
        public void RefreshingReusesItsRowsRatherThanBuildingNewOnes()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = Colony(8);
            IReadOnlyList<PawnId> order = OrderOf(frame);

            model.Refresh(frame, order, null);
            var first = new List<WorkRow>(model.Rows);

            for (int i = 0; i < 5; i++) model.Refresh(frame, order, null);

            Assert.That(model.Rows.Count, Is.EqualTo(first.Count));
            for (int r = 0; r < model.Rows.Count; r++)
                Assert.That(first, Has.Member(model.Rows[r]),
                    "row " + r + " is a new object, so every refresh is allocating again");

            // And the rows are still right after five trips through the pool.
            for (int r = 0; r < model.Rows.Count; r++)
            {
                Assert.That(model.Rows[r].Id, Is.EqualTo(order[r]));
                Assert.That(model.Rows[r].Cells.Count, Is.EqualTo(WorkCatalogue.All.Count),
                    "a recycled row is emptied and refilled, not appended to");
                Assert.That(model.Rows[r].Hours.Count, Is.EqualTo(ScheduleHandle.Hours));
            }
        }

        // ============================================================ sorting

        /// <summary>
        /// Clicking a skill column orders the colony by that skill, highest first.
        ///
        /// <para>Owner, 2026-09-20: <i>"when I click on the skill, I expect it to sort the list by
        /// highest skill in the colony ... sort by desc for highest first"</i>.</para>
        /// </summary>
        [Test]
        public void SortingByASkillPutsTheBestOfTheColonyFirst()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = ColonyWith(
                miningLevels:    new[] { 3, 14, 8, 0, 11 },
                haulPriorities:  new[] { 3, 3, 3, 3, 3 });
            IReadOnlyList<PawnId> order = OrderOf(frame);

            model.Refresh(frame, order, null);
            Assert.That(RowIds(model), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }).AsCollection,
                "unsorted, it is the roster's order");

            Assert.That(model.SortBy(Mining), Is.True);
            model.Refresh(frame, order, null);

            // levels 14, 11, 8, 3, 0 -> pawns 2, 5, 3, 1, 4
            Assert.That(RowIds(model), Is.EqualTo(new[] { 2, 5, 3, 1, 4 }).AsCollection);

            model.ClearSort();
            model.Refresh(frame, order, null);
            Assert.That(RowIds(model), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }).AsCollection,
                "the reset puts the roster's order back");
        }

        /// <summary>
        /// Hauling is the one live column with no skill, so it sorts by priority instead — most
        /// urgent first, and never last.
        /// </summary>
        [Test]
        public void SortingByHaulingUsesThePriorityBecauseThereIsNoSkill()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = ColonyWith(
                miningLevels:   new[] { 0, 0, 0, 0, 0 },
                haulPriorities: new[] { 4, 0, 1, 3, 2 });
            IReadOnlyList<PawnId> order = OrderOf(frame);

            Assert.That(model.SortBy(Hauling), Is.True);
            model.Refresh(frame, order, null);

            // priority 1, 2, 3, 4, then never -> pawns 3, 5, 4, 1, 2
            Assert.That(RowIds(model), Is.EqualTo(new[] { 3, 5, 4, 1, 2 }).AsCollection,
                "1 is the most urgent and 0 means never, so never sorts last rather than first");
        }

        /// <summary>A column the simulation does not run has nothing to sort by and says so.</summary>
        [Test]
        public void AColumnThatIsNotBuiltYetCannotBeSortedBy()
        {
            var model = Model(Frame());

            Assert.That(model.SortBy(Research), Is.False);
            Assert.That(model.SortColumn, Is.EqualTo(WorkGridModel.NoSort));
            Assert.That(model.SortBy(-1), Is.False);
            Assert.That(model.SortBy(999), Is.False);
        }

        /// <summary>
        /// <b>Equal skill keeps the roster's order, and keeps it every refresh.</b>
        ///
        /// <para><c>List.Sort</c> is not stable, and this panel refreshes five times a second for
        /// as long as it is open — so an unstable sort would shuffle everyone on the same level
        /// under the cursor, continuously. The roster position is the tie-break.</para>
        /// </summary>
        [Test]
        public void EqualSkillKeepsTheRostersOrderAndDoesNotShuffle()
        {
            var model = new WorkGridModel();
            WorldSnapshot frame = ColonyWith(
                miningLevels:   new[] { 5, 5, 5, 5, 5, 5, 5, 5 },
                haulPriorities: new[] { 3, 3, 3, 3, 3, 3, 3, 3 });
            IReadOnlyList<PawnId> order = OrderOf(frame);

            model.SortBy(Mining);
            model.Refresh(frame, order, null);
            var first = RowIds(model);
            Assert.That(first, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }).AsCollection);

            for (int i = 0; i < 10; i++)
            {
                model.Refresh(frame, order, null);
                Assert.That(RowIds(model), Is.EqualTo(first).AsCollection,
                    "refresh " + i + " reordered people who are equally skilled");
            }
        }

        /// <summary>The sort runs over the colony and not over the page, or page one would hold
        /// the best of page one.</summary>
        [Test]
        public void TheSortOrdersTheColonyBeforeItIsPaged()
        {
            // Twenty-one colonists at levels 0 to 20, worst first in the roster. Twenty-one and
            // not thirty because a level is 0..20 and ReadCell clamps to it: levels above 20 all
            // land on 20, and a stable sort then orders them by roster position rather than by
            // the number the fixture meant. The clamp found the first draft of this test.
            var levels = new int[21];
            var priorities = new int[21];
            for (int i = 0; i < 21; i++) { levels[i] = i; priorities[i] = 3; }

            var model = new WorkGridModel();
            WorldSnapshot frame = ColonyWith(levels, priorities);
            model.SortBy(Mining);
            model.Refresh(frame, OrderOf(frame), null);

            Assert.That(model.Rows.Count, Is.EqualTo(WorkGridLayout.RowsPerPage));
            Assert.That(model.Rows[0].Id.Value, Is.EqualTo(21),
                "the best miner in the colony is on page one, not the best on page one");
            Assert.That(model.Rows[11].Id.Value, Is.EqualTo(10),
                "and page one runs down from the best, twelve deep");
        }

        // ============================================================ the armed block

        /// <summary>
        /// A key entry arms a block; an hour then takes it instead of cycling. Pressing the armed
        /// entry again disarms it and the cycle comes back.
        /// </summary>
        [Test]
        public void AnArmedBlockPaintsAndDisarmingBringsTheCycleBack()
        {
            var model = Model(Frame());
            Assert.That(model.ArmedBlock, Is.EqualTo(WorkGridModel.NoBlock), "nothing in hand to start");

            // Nothing armed: the hour cycles, as it always did.
            Assert.That(model.TryClickHour(0, 12, back: false, out Intent cycled), Is.True);
            Assert.That(cycled.C, Is.EqualTo(ScheduleCatalogue.Cycle(model.Rows[0].Hours[12])));

            model.ArmBlock(ScheduleHandle.Meditate);
            Assert.That(model.ArmedBlock, Is.EqualTo(ScheduleHandle.Meditate));

            Assert.That(model.TryClickHour(0, 12, back: false, out Intent painted), Is.True);
            Assert.That(painted.C, Is.EqualTo(ScheduleHandle.Meditate));

            // And it keeps painting rather than walking on to the next block.
            Assert.That(model.TryClickHour(0, 13, back: true, out Intent again), Is.True);
            Assert.That(again.C, Is.EqualTo(ScheduleHandle.Meditate),
                "an armed block paints on either button; the ring is not being walked");

            model.ArmBlock(ScheduleHandle.Meditate);
            Assert.That(model.ArmedBlock, Is.EqualTo(WorkGridModel.NoBlock),
                "pressing the armed entry again puts it down");

            Assert.That(model.TryClickHour(0, 12, back: false, out Intent back), Is.True);
            Assert.That(back.C, Is.EqualTo(ScheduleCatalogue.Cycle(model.Rows[0].Hours[12])));
        }

        /// <summary>Arming a second block replaces the first rather than holding both.</summary>
        [Test]
        public void ArmingASecondBlockReplacesTheFirst()
        {
            var model = Model(Frame());

            model.ArmBlock(ScheduleHandle.Sleep);
            model.ArmBlock(ScheduleHandle.Work);
            Assert.That(model.ArmedBlock, Is.EqualTo(ScheduleHandle.Work));

            model.DisarmBlock();
            Assert.That(model.ArmedBlock, Is.EqualTo(WorkGridModel.NoBlock));
        }

        // ============================================================ the panel's own box

        /// <summary>
        /// <b>The panel's width has to carry the chrome the stylesheet puts inside it.</b>
        ///
        /// <para>UI Toolkit's <c>width</c> is a border box and <c>.panel</c> has 12px of padding and
        /// a 1px border, so a panel set to the grid's own width has a content box 26px too small
        /// and the schedule half hangs out of the right of it, over the world. That is what the
        /// owner reported on 2026-09-20 and neither tier could see it: nothing in either asserts a
        /// layout number against the stylesheet. This is that assertion.</para>
        /// </summary>
        [Test]
        public void ThePanelIsWideEnoughForItsOwnPaddingAndBorder()
        {
            int chrome = 2 * (HudLayout.Pad + HudTheme.BorderWidth);

            Assert.That(WorkGridLayout.PanelOuterWidth - chrome,
                Is.EqualTo(WorkGridLayout.PanelWidth),
                "the content box has to come to exactly the grid's width");
            Assert.That(WorkGridLayout.PanelOuterWidth, Is.EqualTo(1409));
            Assert.That(WorkGridLayout.PanelOuterWidth,
                Is.LessThanOrEqualTo(HudLayout.ReferenceWidth),
                "and the whole panel still fits the reference screen");
        }

        /// <summary>
        /// The key's two halves add up to the grid's two halves, so the rule between them lands
        /// on the same x as the seam above it (owner, 2026-09-20).
        /// </summary>
        [Test]
        public void TheKeySeamSitsOnTheGridSeam()
        {
            Assert.That(WorkGridLayout.KeyWorkWidth,
                Is.EqualTo(WorkGridLayout.LeftColumn +
                           WorkGridLayout.ColumnsPerPage * WorkGridLayout.Pitch),
                "the work key spans the names and the columns, which is where the seam is");

            Assert.That(
                WorkGridLayout.KeyWorkWidth + WorkGridLayout.SectionDivider +
                WorkGridLayout.KeyScheduleWidth,
                Is.EqualTo(WorkGridLayout.PanelWidth),
                "and the two halves and their rule come to the whole panel");
        }
    }
}
