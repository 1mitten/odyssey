#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The schedule half of the combined Work tab (design 27 §12–§14): the six blocks, the day a
    /// row reads out of the aspects, and the two numbers the layout turns on.
    /// </summary>
    public class ScheduleGridTests
    {
        static readonly PawnId Ada = new PawnId(1);

        static WorldSnapshot Frame(int tick = 0, params (int Hour, int Block)[] day)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(10, 10, 4), sliceLayer: 1);
            snapshot.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), food: 900, rest: 800, mood: 50));
            foreach ((int hour, int block) in day)
                snapshot.AddPawnAspect(new PawnAspect(Ada, ScheduleKeys.Hour[hour], block));
            return snapshot;
        }

        static WorkGridModel Model(WorldSnapshot frame)
        {
            var model = new WorkGridModel();
            model.Refresh(frame, new[] { Ada }, System.Array.Empty<PawnId>());
            return model;
        }

        // ------------------------------------------------------------------ the six blocks

        [Test]
        public void ThereAreSixBlocksAndAnythingIsTheOneThatMeansNothing()
        {
            Assert.That(ScheduleCatalogue.All.Count, Is.EqualTo(ScheduleHandle.Count));
            Assert.That(ScheduleCatalogue.All[0].Handle, Is.EqualTo(ScheduleHandle.Anything),
                "Anything is first and is handle zero, so an unscheduled colonist reads as " +
                "'no instruction' rather than as an instruction somebody forgot to give.");
        }

        [Test]
        public void NoTwoBandsLookAlike()
        {
            // These sit edge to edge in an unbroken band, which is a harder test than two chips in
            // a legend. The first pass put Anything 77 from Sleep — a flat grey against a dark
            // indigo, the one pair a player has to tell apart at a glance in a night row.
            IReadOnlyList<ScheduleCatalogue.Entry> all = ScheduleCatalogue.All;
            for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
            {
                int distance = ScheduleCatalogue.Distance(all[i].Colour, all[j].Colour);
                Assert.That(distance, Is.GreaterThanOrEqualTo(ScheduleCatalogue.MinimumDistance),
                    $"{all[i].Key} and {all[j].Key} are {distance} channel-points apart, which is " +
                    "one colour to somebody scanning a row of twenty-four.");
            }
        }

        [Test]
        public void NoBandIsMistakenForTheNowLine()
        {
            // The accent is the only other saturated thing on the panel, and it is drawn *over*
            // the bands. A band that matched it would swallow the line.
            foreach (ScheduleCatalogue.Entry block in ScheduleCatalogue.All)
                Assert.That(ScheduleCatalogue.Distance(block.Colour, HudTheme.Accent),
                    Is.GreaterThanOrEqualTo(ScheduleCatalogue.MinimumDistance), block.Key);
        }

        [Test]
        public void TheCycleIsARingInBothDirections()
        {
            int at = ScheduleHandle.Anything;
            for (int i = 0; i < ScheduleCatalogue.All.Count; i++) at = ScheduleCatalogue.Cycle(at);
            Assert.That(at, Is.EqualTo(ScheduleHandle.Anything), "six clicks return to the start");

            Assert.That(ScheduleCatalogue.CycleBack(ScheduleCatalogue.Cycle(ScheduleHandle.Work)),
                Is.EqualTo(ScheduleHandle.Work), "back undoes forward");
        }

        [Test]
        public void AnUnknownBlockFallsBackToAnythingRatherThanThrowing()
        {
            Assert.That(ScheduleCatalogue.Of(99).Handle, Is.EqualTo(ScheduleHandle.Anything));
            Assert.That(ScheduleCatalogue.ColourOf(99).Hex,
                Is.EqualTo(ScheduleCatalogue.ColourOf(ScheduleHandle.Anything).Hex));
        }

        // ------------------------------------------------------------------ reading a day

        [Test]
        public void ARowReadsItsDayOutOfTheAspects()
        {
            WorkRow row = Model(Frame(0, (0, ScheduleHandle.Sleep), (9, ScheduleHandle.Work),
                (19, ScheduleHandle.Recreation))).Rows[0];

            Assert.That(row.Hours.Count, Is.EqualTo(WorkGridLayout.Hours));
            Assert.That(row.Hours[0], Is.EqualTo(ScheduleHandle.Sleep));
            Assert.That(row.Hours[9], Is.EqualTo(ScheduleHandle.Work));
            Assert.That(row.Hours[19], Is.EqualTo(ScheduleHandle.Recreation));
        }

        [Test]
        public void AnHourNobodyPublishedIsAnythingAndNotAnError()
        {
            // The frame a panel meets in a build that does not publish schedules at all.
            WorkRow row = Model(Frame()).Rows[0];

            for (int h = 0; h < row.Hours.Count; h++)
                Assert.That(row.Hours[h], Is.EqualTo(ScheduleHandle.Anything));
        }

        [Test]
        public void AClickOnAnHourEmitsThePawnTheHourAndTheNextBlock()
        {
            WorkGridModel model = Model(Frame(0, (7, ScheduleHandle.Sleep)));

            Assert.That(model.TryClickHour(0, 7, back: false, out Intent forward), Is.True);
            Assert.That(forward.Kind, Is.EqualTo(IntentKind.SetScheduleBlock));
            Assert.That(forward.A, Is.EqualTo(Ada.Value));
            Assert.That(forward.B, Is.EqualTo(7));
            Assert.That(forward.C, Is.EqualTo(ScheduleCatalogue.Cycle(ScheduleHandle.Sleep)));

            Assert.That(model.TryClickHour(0, 7, back: true, out Intent backward), Is.True);
            Assert.That(backward.C, Is.EqualTo(ScheduleCatalogue.CycleBack(ScheduleHandle.Sleep)));
        }

        [Test]
        public void AnHourOutsideTheDayEmitsNothing()
        {
            WorkGridModel model = Model(Frame());

            Assert.That(model.TryClickHour(0, -1, back: false, out _), Is.False);
            Assert.That(model.TryClickHour(0, WorkGridLayout.Hours, back: false, out _), Is.False);
            Assert.That(model.TryClickHour(5, 0, back: false, out _), Is.False, "no such row");
        }

        [Test]
        public void EveryHourIsClickableBecauseNobodyIsIncapableOfATimeOfDay()
        {
            // The one place the two halves deliberately differ: a work cell can be inert, an hour
            // never can. Worth pinning, because copying the work half's guard across would have
            // made a colonist who cannot mine also unable to be sent to bed.
            WorkGridModel model = Model(Frame());

            for (int h = 0; h < WorkGridLayout.Hours; h++)
                Assert.That(model.TryClickHour(0, h, back: false, out _), Is.True, "hour " + h);
        }

        // ------------------------------------------------------------------ the geometry

        [Test]
        public void BothHalvesShareOnePitch()
        {
            // The whole claim of the combined table: a priority cell and an hour block are the
            // same click target, so the eye reads one continuous row.
            Assert.That(WorkGridLayout.HourPitch, Is.EqualTo(WorkGridLayout.Pitch));
        }

        /// <summary>
        /// One page of work columns and the whole day, side by side, inside the reference screen.
        ///
        /// <para><b>The day is never paged and that is the decision this test guards</b> (owner,
        /// 2026-09-20). Work pages in two; the twenty-four hours stay whole on both of them, so a
        /// row is still one colonist's whole day — which is the entire claim of folding Schedule
        /// into Work. A day split across pages would be two answers to "and when" again.</para>
        /// </summary>
        [Test]
        public void OnePageOfWorkAndTheWholeDayFitTheReferenceScreen()
        {
            int width = WorkGridLayout.CombinedWidthFor(WorkGridLayout.ColumnsPerPage);

            Assert.That(width, Is.EqualTo(192 + 11 * 34 + 1 + 24 * 34));
            Assert.That(width, Is.EqualTo(WorkGridLayout.PanelWidth));

            Assert.That(WorkGridLayout.ScheduleWidth,
                Is.EqualTo(ScheduleHandle.Hours * WorkGridLayout.HourPitch),
                "every hour, on every page: the day does not page");

            Assert.That(width, Is.LessThanOrEqualTo(HudLayout.ReferenceWidth),
                $"one page plus the day is {width}px against a {HudLayout.ReferenceWidth}px " +
                "reference. At the supplied spec's 40px pitch the same table is wider still — our " +
                "narrow type is what makes one row of both halves possible.");
        }

        [Test]
        public void TheNowLineSitsOnTheCentreOfItsOwnHour()
        {
            // Relative to the schedule container, never the panel: measured from the panel it is
            // one frozen name column out, which lands it on a different hour and reads as a bug
            // in the clock rather than in the layout.
            Assert.That(WorkGridLayout.NowLineCentre(0), Is.EqualTo(WorkGridLayout.HourPitch / 2f));
            Assert.That(WorkGridLayout.NowLineCentre(13),
                Is.EqualTo(13 * WorkGridLayout.HourPitch + WorkGridLayout.HourPitch / 2f));

            float last = WorkGridLayout.NowLineCentre(WorkGridLayout.Hours - 1);
            Assert.That(last, Is.LessThan(WorkGridLayout.ScheduleWidth),
                "the last hour's line must land inside the half, not past its right edge");
        }

        [Test]
        public void TheNowHourComesFromTheClockAndNotFromAGuess()
        {
            Assert.That(WorkGridModel.NowHour(Frame(tick: 0)), Is.EqualTo(0));
            Assert.That(WorkGridModel.NowHour(Frame(tick: GameClock.TicksPerHour * 13)),
                Is.EqualTo(13));
            Assert.That(WorkGridModel.NowHour(Frame(tick: GameClock.TicksPerDay + GameClock.TicksPerHour * 5)),
                Is.EqualTo(5), "the second day is the same day on the clock face");
        }

        // ------------------------------------------------------------------ the tab it replaced

        [Test]
        public void ScheduleIsNotOnTheCommandBarAnyMore()
        {
            foreach (HudCommand command in HudCommands.All)
                Assert.That(command.Key, Is.Not.EqualTo("ui.tab.schedule"),
                    "Work and Schedule are one table now; two tabs would be two answers to " +
                    "'what is this colonist doing', read one after the other.");
        }

        [Test]
        public void TheScheduleKeysAreTheOnesTheSimulationSpells()
        {
            // The interface cannot reference Odyssey.Sim, so this string is the whole of the seam.
            // ScheduleAspects mints the same one; a Sim-side test holds the other end.
            Assert.That(ScheduleKeys.Name(0), Is.EqualTo("odyssey.pawn.schedule.h00"));
            Assert.That(ScheduleKeys.Name(23), Is.EqualTo("odyssey.pawn.schedule.h23"));
            Assert.That(ScheduleKeys.Hour.Length, Is.EqualTo(ScheduleHandle.Hours));
        }

        [Test]
        public void EveryBlockIsNamedFromTheRegistry()
        {
            foreach (ScheduleCatalogue.Entry block in ScheduleCatalogue.All)
                Assert.That(block.Label, Is.Not.Empty.And.Not.EqualTo(block.Key),
                    block.Key + " has no name in icon-keys.csv, so the legend would draw its key.");
        }
    }
}
