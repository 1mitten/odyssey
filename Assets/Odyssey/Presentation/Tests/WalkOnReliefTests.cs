#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Where a figure is drawn while walking over ground the relief has rolled — which is all of
    /// the ground the game actually draws.
    ///
    /// <para><b>Why this is not <see cref="BankFootingTests"/>.</b> That fixture owns the same
    /// question and measures it with the same instrument, and it is thorough: five continuity
    /// cases across a terrace, sampled four hundred times a step. But <c>GroundRelief.Reset()</c>
    /// sets <c>Amplitude</c> to zero, every one of those cases inherits it, and at zero amplitude
    /// <c>GroundRelief.Lift</c> returns its argument unchanged. So the whole continuity suite has
    /// only ever run on a perfectly flat field — and the board the game loads has a 2 m field on
    /// it everywhere. The one test there that does turn the relief on asks where a standing figure
    /// is, not whether a walking one moves smoothly.</para>
    ///
    /// <para><b>What it is looking for.</b> The owner reported (2026-09-17) colonists that jolt
    /// around certain terrain, "like it keeps snapping in 2 directions quickly", most often near
    /// water. The first candidate — an unsmoothed staircase path — was measured in
    /// <c>WalkHeadingMeasurementTests</c> and does not happen. The second is in
    /// <c>PawnPose.OnTheDrawnGround</c>, which decides which cell's relief the figure stands on
    /// with <c>CellRef over = t &lt; 0.5f ? pawn.Cell : pawn.NextCell</c> — a hard switch at the
    /// midpoint of every step, taken between two heights sampled at two cell <em>centres</em>. The
    /// drape's own measurement puts neighbouring centres 73 mm apart typically and 220 mm at
    /// worst.</para>
    ///
    /// <para>These tests assert nothing about what the number should be until one of them has been
    /// read. They are an instrument first, in the idiom of <c>HashTraceTests</c>: they print, and
    /// the one thing they assert is that the instrument is not measuring a flat board by
    /// accident.</para>
    /// </summary>
    public class WalkOnReliefTests
    {
        [SetUp]
        public void Reset()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        [TearDown]
        public void Restore()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        static PawnView Standing(CellRef cell) => Walking(cell, cell, 0);

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent);

        /// <summary>
        /// The largest jump in drawn height between two consecutive samples of one step, in metres,
        /// and where in the step it happened.
        ///
        /// <para>The same instrument <see cref="BankFootingTests"/> uses, returning the phase as
        /// well as the size — because the fault under suspicion is at one specific phase, and a
        /// jump at 0.5 and a jump spread over the whole step are different bugs with the same
        /// magnitude.</para>
        /// </summary>
        static (float worst, float at) WorstJump(WorldRenderModel? model, CellRef from, CellRef to)
        {
            const int samples = 400;
            float worst = 0f;
            float at = 0f;
            float previous = PawnPose.Of(Standing(from), 0f, 0, out _, model).y;

            for (int i = 0; i <= samples; i++)
            {
                int percent = Mathf.RoundToInt(i * 100f / samples);
                float y = PawnPose.Of(Walking(from, to, percent), 0f, 0, out _, model).y;
                float jump = Mathf.Abs(y - previous);
                if (jump > worst)
                {
                    worst = jump;
                    at = i / (float)samples;
                }
                previous = y;
            }

            return (worst, at);
        }

        /// <summary>
        /// A frame's worth of walking, in metres: the yardstick a jump is judged against.
        ///
        /// <para>A colonist walks a 2.5 m cell in 1.67 s, which is 1.5 m/s, so at sixty frames a
        /// second an honest frame moves her 25 mm. A vertical jump of that size in one frame is
        /// motion; several times it is a tick; a tenth of a metre is the thing being reported.</para>
        /// </summary>
        const float FrameTravel = 1.5f / 60f;

        /// <summary>
        /// The instrument's own control, and the whole reason this file exists: prove the relief is
        /// actually on. Every continuity case in <see cref="BankFootingTests"/> is silently flat.
        /// </summary>
        [Test]
        public void TheFieldIsOnAtAll()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            var a = new CellRef(12, 9, 1);
            var b = new CellRef(13, 9, 1);
            float ya = PawnPose.Of(Standing(a), 0f, 0, out _).y;
            float yb = PawnPose.Of(Standing(b), 0f, 0, out _).y;

            Assert.That(Mathf.Abs(ya - yb), Is.GreaterThan(1e-4f),
                "two neighbouring cells are drawn at the same height, so the field is flat and " +
                "every measurement in this file would be vacuous");
        }

        /// <summary>
        /// The measurement: one step across open, rolling ground with no bank anywhere near it.
        ///
        /// <para>No bank on purpose. A jump here cannot be blamed on the bank surfaces failing to
        /// tile, which is what <see cref="BankFootingTests"/> already rules out — it can only be
        /// the midpoint switch itself.</para>
        /// </summary>
        [Test]
        public void HowFarADrawnFigureJumpsCrossingOneCellOfRollingGround()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            // A flat board with no terrace and therefore no banks: the only thing shaping the
            // drawn ground is the relief field.
            RenderTestWorld world = RenderTestWorld.Terrace(
                rise: 0, stepTerrain: NaturalContent.TerrainGrass, n: 8);

            float worstOfAll = 0f;
            float worstAt = 0f;
            CellRef worstFrom = default;

            // Every step on the board, both axes, so the answer is about the field rather than
            // about whichever cell somebody happened to pick.
            for (int z = 1; z < 7; z++)
            for (int x = 1; x < 6; x++)
            {
                var from = new CellRef(x, z, 2);
                foreach (CellRef to in new[] { new CellRef(x + 1, z, 2), new CellRef(x, z + 1, 2) })
                {
                    (float worst, float at) = WorstJump(world.Model, from, to);
                    if (worst > worstOfAll)
                    {
                        worstOfAll = worst;
                        worstAt = at;
                        worstFrom = from;
                    }
                }
            }

            TestContext.WriteLine(
                $"MEASURED rolling ground, no banks: worst drawn-height jump in one sample is " +
                $"{worstOfAll * 1000f:F1} mm, at phase {worstAt:F3} of the step from " +
                $"({worstFrom.X},{worstFrom.Z}). One frame of walking is {FrameTravel * 1000f:F1} mm. " +
                $"The midpoint switch in PawnPose.OnTheDrawnGround is at phase 0.500.");

            // Measured at 81.9 mm before the fix and 1.6 mm after it, so a frame of honest travel
            // is a threshold with an order of magnitude of room on both sides — it cannot fail on
            // sampling noise, and it cannot pass on a return of the fault.
            Assert.That(worstOfAll, Is.LessThan(FrameTravel),
                "a walking figure's drawn height moves further in one sample than a whole frame of " +
                "walking would carry her. That reads as a jolt. Check whether something is again " +
                "comparing a height sampled at a cell centre with one sampled where the walker is.");
        }

        /// <summary>
        /// The same question asked directly rather than by sampling: how far the drawn height moves
        /// across the midpoint, where the cell whose relief is being read changes.
        ///
        /// <para>Sampling can miss a discontinuity that falls between two samples, or blur it
        /// across two. This brackets the switch as tightly as a float allows, so the number it
        /// reports is the jump itself and not an estimate of it.</para>
        /// </summary>
        [Test]
        public void HowFarTheDrawnHeightMovesAcrossTheMidpointItself()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            RenderTestWorld world = RenderTestWorld.Terrace(
                rise: 0, stepTerrain: NaturalContent.TerrainGrass, n: 8);

            float worst = 0f;
            CellRef worstFrom = default;
            CellRef worstTo = default;

            for (int z = 1; z < 7; z++)
            for (int x = 1; x < 6; x++)
            {
                var from = new CellRef(x, z, 2);
                foreach (CellRef to in new[] { new CellRef(x + 1, z, 2), new CellRef(x, z + 1, 2) })
                {
                    // 49 and 51 per cent: the integer either side of the switch, which is the
                    // finest a PawnView can express without the frame's own extrapolation.
                    float before = PawnPose.Of(Walking(from, to, 49), 0f, 0, out _, world.Model).y;
                    float after = PawnPose.Of(Walking(from, to, 51), 0f, 0, out _, world.Model).y;
                    float jump = Mathf.Abs(after - before);
                    if (jump > worst)
                    {
                        worst = jump;
                        worstFrom = from;
                        worstTo = to;
                    }
                }
            }

            TestContext.WriteLine(
                $"MEASURED across the midpoint (49% -> 51%): worst move is {worst * 1000f:F1} mm, " +
                $"on the step ({worstFrom.X},{worstFrom.Z}) -> ({worstTo.X},{worstTo.Z}). " +
                $"Two per cent of a step is {2.5f * 0.02f * 1000f:F0} mm of honest travel.");

            // Two per cent of a step is 50 mm of real travel, so the drawn height crossing the
            // switch must move less than that or the switch is adding motion of its own. It was
            // 81.9 mm — more than the travel — and is now 3.2 mm.
            Assert.That(worst, Is.LessThan(2.5f * 0.02f),
                "the drawn height moves further across the midpoint of a step than the walker does");
        }

        /// <summary>
        /// The five bank crossings <see cref="BankFootingTests"/> already covers, run again with
        /// the field on — which is the combination that was missing and the geometry the owner's
        /// report is about.
        ///
        /// <para>A bank stands where ground meets a step, and the shore of a pond is exactly that,
        /// so "around water" and "where there are banks" name the same cells. The bank surfaces
        /// tile, which is why the flat versions of these pass; the question here is whether they
        /// still tile once the whole board is riding on a 2 m swell.</para>
        /// </summary>
        [TestCase(3, 2, 3, 3, TestName = "along the foot of a terrace, on rolling ground")]
        [TestCase(3, 3, 4, 3, TestName = "off the foot of a terrace, on rolling ground")]
        [TestCase(4, 3, 3, 3, TestName = "onto the foot of a terrace, on rolling ground")]
        public void CrossingABankOnRollingGroundIsSmooth(int fromX, int fromZ, int toX, int toZ)
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            RenderTestWorld world = RenderTestWorld.Terrace(
                rise: 1, stepTerrain: NaturalContent.TerrainGrass);

            var from = new CellRef(fromX, fromZ, 2);
            var to = new CellRef(toX, toZ, 2);
            (float worst, float at) = WorstJump(world.Model, from, to);

            // The same crossing on a flat field, so the report can say whether the relief caused
            // this or merely revealed something the bank geometry was doing all along. Measuring
            // the control is cheaper than arguing about which it is.
            GroundRelief.Amplitude = 0f;
            (float flat, float flatAt) = WorstJump(world.Model, from, to);
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            TestContext.WriteLine(
                $"MEASURED bank crossing ({fromX},{fromZ}) -> ({toX},{toZ}): " +
                $"rolling {worst * 1000f:F1} mm at phase {at:F3}, " +
                $"flat {flat * 1000f:F1} mm at phase {flatAt:F3}.");

            // **The relief must add nothing**, which is a sharper claim than any absolute budget
            // and the one this test was written to make. Measured 2026-09-17: rolling 28.7 mm
            // against flat 30.0 mm on the two crossings between a bank cell and the flat ground
            // beside it, so the field is not merely innocent here, it is marginally kinder.
            //
            // An absolute threshold was tried first and rejected: at one frame of travel it fails
            // on the flat control too, so it would have been a test that blamed the relief for
            // something the relief does not do. **The 30 mm belongs to the bank surface and is
            // older than this file** — it sits under the 50 mm budget `BankFootingTests` has always
            // asserted, which is why nothing has ever reported it. It is about one and a fifth
            // frames of walking, against the 3.3 frames the midpoint fault was worth, and it is
            // recorded as its own open question rather than folded in here.
            Assert.That(worst, Is.LessThanOrEqualTo(flat + 1e-3f),
                "the rolling field makes this bank crossing jump further than the flat one does, " +
                "so the relief is adding a discontinuity of its own");
        }
    }
}
