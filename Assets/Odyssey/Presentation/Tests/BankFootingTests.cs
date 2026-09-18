#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Where a figure is drawn on ground that has a bank in it.
    ///
    /// <para>A bank fills the cell it stands in from the floor to the rim, and that cell is
    /// walkable — it is the cell at the foot of a terrace, which is the take-off cell for the hop
    /// the bank is the picture of. So a colonist drawn at her cell's floor was waist-deep in the
    /// ramp, for exactly the reason a miner used to be waist-deep in a quarry. These are about the
    /// figure standing on the surface the mesher draws rather than inside it.</para>
    ///
    /// <para><b>The continuity tests are the ones that matter.</b> A wrong height reads as a
    /// figure sunk into a slope, which is obvious in a photograph; a height that jumps by a metre
    /// at a cell boundary reads as a teleport, which is obvious only in motion and would be blamed
    /// on the animation.</para>
    /// </summary>
    public class BankFootingTests
    {
        [SetUp]
        public void Reset() => BankLayout.Reset();

        [TearDown]
        public void Restore()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        /// <summary>
        /// The terrace board's low half is at layer 1, so its banks stand in the empty cells at
        /// layer 1. Cell (3, z) is the first low column, hard against the riser at (2, z).
        /// </summary>
        static RenderTestWorld Terrace() =>
            RenderTestWorld.Terrace(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

        static CellRef Bank(int z = 3) => new CellRef(3, z, 2);

        static PawnView Standing(CellRef cell) => Walking(cell, cell, 0);

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent,
                movePerMille: percent * 10);

        /// <summary>
        /// The same at the resolution the figure is actually drawn at: per mille of the step, which
        /// is what the snapshot publishes and what one tick of a dear step advances. Sampling by
        /// whole percent measures a quantisation the game no longer has — see
        /// <see cref="PawnView.MovePerMille"/>.
        /// </summary>
        static PawnView WalkingPerMille(CellRef from, CellRef to, int perMille) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, perMille / 10,
                movePerMille: perMille);

        // ------------------------------------------------------------------ the surface

        [Test]
        public void AThereIsABankToStandOn()
        {
            // The fixture's own claim. Every test below is vacuous if this cell has no bank, and a
            // vacuous test that passes is worse than no test at all.
            RenderTestWorld world = Terrace();
            Assert.That(BankLayout.At(world.Model, Bank()).Exists, Is.True,
                "the cell at the foot of the terrace has no bank in it");
        }

        [Test]
        public void TheSurfaceRunsFromTheFloorToTheTopOfTheStep()
        {
            RenderTestWorld world = Terrace();
            CellRef cell = Bank();
            Vector3 centre = CellMetrics.FloorCentre(cell);

            // Away from the riser is the open edge, which is the floor; into the riser is the top
            // of the step. The exact ends are what make a bank something to walk up rather than a
            // lip to trip over, and they are what keeps a figure continuous with the ground at
            // either end of it.
            float away = BankLayout.RiseAt(world.Model, cell, centre.x + CellMetrics.HalfXZ, centre.z);
            float into = BankLayout.RiseAt(world.Model, cell, centre.x - CellMetrics.HalfXZ, centre.z);

            Assert.That(away, Is.EqualTo(0f).Within(1e-3f), "the open edge of a bank is not its floor");
            Assert.That(into, Is.EqualTo(CellMetrics.SizeY).Within(1e-3f),
                "the riser edge of a bank does not reach the top of the step");
            Assert.That(BankLayout.RiseAt(world.Model, cell, centre.x, centre.z),
                Is.EqualTo(CellMetrics.SizeY * 0.5f).Within(1e-3f),
                "the middle of a bank is not half way up it");
        }

        [Test]
        public void TheShapeIsTurnedOntoItsBearingAndNotAgainstIt()
        {
            // The rotation has to be *undone* to read the field back, and getting that backwards is
            // invisible on this board in one direction and wrong in the other three: a straight
            // slope is symmetric about the axis it is turned around, so a sign error only shows up
            // as a bank that climbs away from its own step. Asking which way the surface rises is
            // the test that catches it.
            RenderTestWorld world = Terrace();
            CellRef cell = Bank();
            Vector3 centre = CellMetrics.FloorCentre(cell);

            float towardsTheStep = BankLayout.RiseAt(world.Model, cell, centre.x - 1f, centre.z);
            float awayFromIt = BankLayout.RiseAt(world.Model, cell, centre.x + 1f, centre.z);

            Assert.That(towardsTheStep, Is.GreaterThan(awayFromIt),
                "the bank climbs away from the step it is meant to climb");
        }

        [Test]
        public void FlatGroundLiftsNobody()
        {
            RenderTestWorld world = RenderTestWorld.Terrace(rise: 0, stepTerrain: NaturalContent.TerrainGrass);
            CellRef cell = new CellRef(3, 3, 2);
            Vector3 centre = CellMetrics.FloorCentre(cell);

            Assert.That(BankLayout.RiseAt(world.Model, cell, centre.x, centre.z), Is.Zero);
        }

        [Test]
        public void TheLeverTakesTheLiftAwayWithTheBank()
        {
            // The two have to move together. A per-renderer lever would have let them disagree —
            // banks off and colonists still hovering 1.5 m over the meadow — which is why these
            // became statics when the decision left the mesher.
            RenderTestWorld world = Terrace();
            CellRef cell = Bank();
            Vector3 centre = CellMetrics.FloorCentre(cell);

            BankLayout.Enabled = false;
            Assert.That(BankLayout.RiseAt(world.Model, cell, centre.x, centre.z), Is.Zero,
                "banks are off and a figure is still being lifted onto one");
        }

        // ------------------------------------------------------------------ the figure

        [Test]
        public void AColonistStandsOnTheBankRatherThanInIt()
        {
            RenderTestWorld world = Terrace();
            CellRef cell = Bank();

            Vector3 sunk = PawnPose.Of(Standing(cell), 0f, 0, out _);
            Vector3 stood = PawnPose.Of(Standing(cell), 0f, 0, out _, world.Model);

            Assert.That(stood.y - sunk.y, Is.EqualTo(CellMetrics.SizeY * 0.5f).Within(1e-3f),
                "a colonist at the foot of a terrace is not standing on the slope drawn there");
            Assert.That(stood.x, Is.EqualTo(sunk.x).Within(1e-4f), "the lift moved the figure sideways");
            Assert.That(stood.z, Is.EqualTo(sunk.z).Within(1e-4f), "the lift moved the figure sideways");
        }

        [Test]
        public void WithoutAWorldNothingChangesAtAll()
        {
            // PawnPoseTests is about the glide between cells and must go on meaning what it meant.
            // The world is the optional argument precisely so that those tests stay pure arithmetic.
            RenderTestWorld world = Terrace();
            CellRef flat = new CellRef(5, 3, 2);

            Assert.That(PawnPose.Of(Standing(flat), 0f, 0, out _, world.Model),
                Is.EqualTo(PawnPose.Of(Standing(flat), 0f, 0, out _)));
        }

        [Test]
        public void ClimbingAStepTheFigureIsNeverInsideIt()
        {
            // The fault this half of the change is for. A hop's chord runs from one cell centre to
            // the next, which for a step up passes a metre and a half *inside* the block being
            // climbed — so the figure waded up through the ground for the second half of every hop,
            // bank or no bank.
            //
            // **Stated over the boundary rather than over the clock**, since 2026-09-19. It used to
            // sample at half the step's *time* and require the figure to be on top by then, which
            // was the same thing while half the time was half the path. `PawnPose.StepPace` spends
            // the time where the climbing is, so at half the clock the figure is two thirds of the
            // way up the ramp and still has a moment to go — and that is the change working, not a
            // regression. What must still hold, and is what this ever meant, is that the figure is
            // on top of the step from the moment it is over the upper cell.
            RenderTestWorld world = Terrace();
            CellRef from = Bank();
            var to = new CellRef(2, from.Z, 3);      // on top of the riser

            float stepTop = CellMetrics.FloorCentre(to).y;
            float boundary = (CellMetrics.FloorCentre(from).x + CellMetrics.FloorCentre(to).x) * 0.5f;

            for (int perMille = 0; perMille <= 1000; perMille++)
            {
                Vector3 at = PawnPose.Of(WalkingPerMille(from, to, perMille), 0f, 0, out _, world.Model);

                // Over the upper cell: the riser runs along x, and `to` is the lower x of the two.
                if (at.x > boundary) continue;

                Assert.That(at.y, Is.GreaterThanOrEqualTo(stepTop - 1e-3f),
                    $"at {perMille} per mille the figure is over the upper cell at {at.y}, inside " +
                    $"the block it is climbing, whose top is {stepTop}");
            }
        }

        // ------------------------------------------------------------------ continuity

        /// <summary>
        /// The largest jump in drawn height between two consecutive frames of one step, in metres.
        ///
        /// <para>Sampled finely rather than at the ends, because every fault this is looking for is
        /// at a boundary in the middle: the cell the figure is over changes at the midpoint, and a
        /// surface that did not tile would show up there and nowhere else.</para>
        /// </summary>
        static float WorstJump(RenderTestWorld world, CellRef from, CellRef to)
        {
            // **Per mille, not per percent**, since 2026-09-18. The figure is drawn from
            // PawnView.MovePerMille and one tick of the dearest step there is advances a few of
            // them, so a thousandth is the finest step the drawn position ever takes. Sampling by
            // whole percent measured a quantisation the game had and has not any more, and it read
            // a deliberate stride up a bank — 13 mm a frame — as a 134 mm teleport.
            const int samples = 1000;
            float worst = 0f;
            float previous = PawnPose.Of(Standing(from), 0f, 0, out _, world.Model).y;

            for (int i = 0; i <= samples; i++)
            {
                int perMille = Mathf.RoundToInt(i * 1000f / samples);
                float y = PawnPose.Of(WalkingPerMille(from, to, perMille), 0f, 0, out _, world.Model).y;
                worst = Mathf.Max(worst, Mathf.Abs(y - previous));
                previous = y;
            }

            float arrived = PawnPose.Of(Standing(to), 0f, 0, out _, world.Model).y;
            return Mathf.Max(worst, Mathf.Abs(arrived - previous));
        }

        /// <summary>
        /// A quarter of a percent of the step, which is four times the 0.25% the sampling itself
        /// takes. Anything a walker would read as a teleport is orders of magnitude above it: the
        /// faults this is guarding against are half a layer, 1.5 m.
        /// </summary>
        const float Smooth = 0.05f;

        [Test]
        public void WalkingAlongTheFootOfATerraceIsSmooth()
        {
            RenderTestWorld world = Terrace();
            Assert.That(WorstJump(world, Bank(2), Bank(3)), Is.LessThan(Smooth),
                "the drawn height jumps between two cells of one run of bank");
        }

        [Test]
        public void WalkingOffTheFootOfATerraceIsSmooth()
        {
            // Out of the bank cell onto the flat ground beyond it. This is the crossing the tiling
            // has to carry: a bank's open edge sits at its floor, which is the height of the ground
            // it opens onto, so the two agree at the boundary by construction rather than by luck.
            RenderTestWorld world = Terrace();
            Assert.That(WorstJump(world, Bank(), new CellRef(4, 3, 2)), Is.LessThan(Smooth),
                "the drawn height jumps on leaving a bank for flat ground");
        }

        [Test]
        public void WalkingOntoTheFootOfATerraceIsSmooth()
        {
            RenderTestWorld world = Terrace();
            Assert.That(WorstJump(world, new CellRef(4, 3, 2), Bank()), Is.LessThan(Smooth),
                "the drawn height jumps on walking onto a bank");
        }

        [Test]
        public void HoppingUpTheStepIsSmooth()
        {
            RenderTestWorld world = Terrace();
            Assert.That(WorstJump(world, Bank(), new CellRef(2, 3, 3)), Is.LessThan(Smooth),
                "the drawn height jumps while hopping up a terrace");
        }

        [Test]
        public void DroppingOffTheStepIsSmooth()
        {
            // Down is the case the ground-following cannot be used for, because the drawn ground is
            // a step function that way round: holding the figure flat to the edge and then dropping
            // it would be a metre and a half in one frame. The lift is faded out over the step
            // instead, which is smooth at both ends.
            RenderTestWorld world = Terrace();
            Assert.That(WorstJump(world, new CellRef(2, 3, 3), Bank()), Is.LessThan(Smooth),
                "the drawn height jumps while dropping off a terrace");
        }

        [Test]
        public void TheRollingGroundIsStillUnderneathItAll()
        {
            // The two lifts compose rather than replacing one another: relief is a property of the
            // column and a bank of the cell, so a terrace on a slope gets both. Checked by moving
            // the field and watching every height move with it.
            RenderTestWorld world = Terrace();
            CellRef cell = Bank();

            GroundRelief.Reset();
            float flat = PawnPose.Of(Standing(cell), 0f, 0, out _, world.Model).y;

            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
            float rolling = PawnPose.Of(Standing(cell), 0f, 0, out _, world.Model).y;
            Vector3 centre = CellMetrics.FloorCentre(cell);

            Assert.That(rolling - flat, Is.EqualTo(GroundRelief.HeightAt(centre.x, centre.z)).Within(1e-3f),
                "a bank on rolling ground does not ride on the roll");
        }
    }
}
