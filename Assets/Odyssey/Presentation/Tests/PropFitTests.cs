#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How power's machines stand in their footprint (design 32 §14c): the generator fills its two
    /// cells, the heater stands with its back to a wall.
    ///
    /// <para><b>Why this file exists.</b> The owner's first look at the art: <i>"the generator and
    /// heater don't rotate or blueprint/place flush with the current walls/doors … it places with
    /// spacing that is awkward"</i> (2026-09-23). The fit was uniform, so the generator — 0.61 by
    /// 0.91 m as modelled — stood 3.1 m long in 5 m of footprint with a metre of daylight at each
    /// end, and the heater was centred in its cell and could not be turned. The sizes below are
    /// the two models' own, measured from their FBX files, so these tests hold without the packs.
    /// </para>
    /// </summary>
    public class PropFitTests
    {
        static readonly Vector3 GeneratorSize = new Vector3(0.6136f, 0.5825f, 0.9076f);
        static readonly Vector3 HeaterSize = new Vector3(0.9393f, 0.6966f, 0.6421f);

        static ModuleEntry GeneratorRow() => new ModuleEntry
        {
            fitFootprint = new Vector2(2.4f, 4.9f), fitHeight = 2.1f, fitStretch = true,
        };

        static ModuleEntry HeaterRow() => new ModuleEntry
        {
            fitFootprint = new Vector2(2.4f, 2.4f), fitHeight = 2.2f, fitAgainstBack = true,
        };

        [Test]
        public void AStretchedFitFillsTheFootprintEdgeToEdge()
        {
            Assert.That(ModuleLibrary.FitScale(GeneratorRow(), GeneratorSize, out Vector3 scale), Is.True);
            Vector3 drawn = Vector3.Scale(GeneratorSize, scale);

            Assert.That(drawn.x, Is.EqualTo(2.4f).Within(1e-4f), "across the footprint, 5 cm off each side");
            Assert.That(drawn.z, Is.EqualTo(4.9f).Within(1e-4f), "along both cells, 5 cm off each end");
            Assert.That(drawn.y, Is.EqualTo(2.1f).Within(1e-4f), "and as tall as the row says");
        }

        /// <summary>The control: the same model fitted uniformly leaves the gap the owner saw.</summary>
        [Test]
        public void AUniformFitOfTheGeneratorLeavesAMetreAtEachEnd()
        {
            ModuleEntry row = GeneratorRow();
            row.fitStretch = false;
            ModuleLibrary.FitScale(row, GeneratorSize, out Vector3 scale);
            float gap = (4.9f - GeneratorSize.z * scale.z) * 0.5f;

            Assert.That(scale.x, Is.EqualTo(scale.z), "uniform");
            Assert.That(gap, Is.GreaterThan(0.5f), "the fault: the long side fits well short of the second cell");
        }

        [Test]
        public void AUniformFitKeepsTheHeaterInProportionAndAsWideAsTheRow()
        {
            ModuleLibrary.FitScale(HeaterRow(), HeaterSize, out Vector3 scale);
            Vector3 drawn = Vector3.Scale(HeaterSize, scale);

            Assert.That(scale.x, Is.EqualTo(scale.y).And.EqualTo(scale.z), "not stretched");
            Assert.That(drawn.x, Is.EqualTo(2.4f).Within(1e-4f), "its wide face runs across the cell, not along it");
            Assert.That(drawn.z, Is.LessThan(2.4f));
            Assert.That(drawn.y, Is.LessThanOrEqualTo(2.2f + 1e-4f));
        }

        [Test]
        public void AgainstTheBackPutsTheModelsBackOnTheFootprintsBackEdge()
        {
            ModuleLibrary.FitScale(HeaterRow(), HeaterSize, out Vector3 scale);
            float depth = HeaterSize.z * scale.z;
            float offset = ModuleLibrary.BackOffset(2.4f, depth);

            Assert.That(offset - depth * 0.5f, Is.EqualTo(-1.2f).Within(1e-4f),
                "the back 5 cm off the cell's back edge, which is the face of the wall behind it");
            Assert.That(offset, Is.LessThan(0f), "moved back, not forward");
        }

        [Test]
        public void ARowThatAsksForNoFitIsLeftAlone()
        {
            Assert.That(ModuleLibrary.FitScale(new ModuleEntry(), HeaterSize, out Vector3 scale), Is.False);
            Assert.That(scale, Is.EqualTo(Vector3.one));
        }

        // ---- the heater's facing ----------------------------------------------------------------

        static RenderTestWorld Room(int facing) =>
            new RenderTestWorld(8, 8, 4)
                .Solid(4, 4, 0, NaturalContent.TerrainSubsoil)
                .Edifice(4, 4, 1, CoreContent.EdificeHeater, CoreContent.StuffNone,
                    blocking: true, facing: facing);

        [Test]
        public void AHeaterBesideAWallTurnsItsBackToIt()
        {
            // A wall to the east, and the player never touched R: it looks west, out of the wall.
            var world = Room(Directions.North).Solid(5, 4, 1).Publish();

            Assert.That(world.Model.BackedFacing(world.Index(4, 4, 1)), Is.EqualTo(Directions.West));
        }

        /// <summary>
        /// In a corner R still means something — it chooses which wall — which is where this parts
        /// company with the ladder's rule, under which the wall wins outright and R does nothing.
        /// </summary>
        [Test]
        public void InACornerTheRotationChoosesTheWall()
        {
            var answers = new System.Collections.Generic.HashSet<int>();
            foreach (int chosen in new[] { Directions.North, Directions.East, Directions.South, Directions.West })
            {
                var world = Room(chosen).Solid(5, 4, 1).Solid(4, 5, 1).Publish();
                int facing = world.Model.BackedFacing(world.Index(4, 4, 1));
                int back = Directions.Opposite(facing);
                Assert.That(back == Directions.East || back == Directions.North, Is.True,
                    $"turned to {chosen}, its back is to a wall");
                answers.Add(facing);
            }

            Assert.That(answers, Is.EquivalentTo(new[] { Directions.West, Directions.South }), "both walls are reachable by R");
        }

        [Test]
        public void InTheOpenAHeaterFacesTheWayItWasTurned()
        {
            foreach (int chosen in new[] { Directions.North, Directions.East, Directions.South, Directions.West })
            {
                var world = Room(chosen).Publish();
                Assert.That(world.Model.BackedFacing(world.Index(4, 4, 1)), Is.EqualTo(chosen));
            }
        }

        [Test]
        public void TheCursorAsksTheSameQuestionOfACellWithNothingInIt()
        {
            // The ghost has no record to read a facing off, so it passes its own; the wall must
            // still win the same way it does for the built heater.
            var world = new RenderTestWorld(8, 8, 4)
                .Solid(4, 4, 0, NaturalContent.TerrainSubsoil)
                .Solid(3, 4, 1).Publish();

            Assert.That(world.Model.BackedFacing(world.Index(4, 4, 1), Directions.North), Is.EqualTo(Directions.East));
        }
    }
}
