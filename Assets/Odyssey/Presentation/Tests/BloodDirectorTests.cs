#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Blood, drawn (design 33 §10): a hit leaves one mark where its lead drop lands and nothing
    /// where there is no ground; a pool waits for the fall and goes under the body; marks age by
    /// the tick and not the clock; the slice hides them; a floor taken away takes them; and two
    /// hundred of them cost draws in fade steps, never in marks (P10).
    /// </summary>
    public class BloodDirectorTests
    {
        const int Ground = 0;   // the solid layer
        const int Floor = 1;    // the layer everyone stands on

        [SetUp]
        public void SetUp() => GroundRelief.Reset();

        [TearDown]
        public void TearDown() => GroundRelief.Reset();

        /// <summary>A board of solid ground at layer 0 up to and including <paramref name="lastX"/>, open air beyond it.</summary>
        static RenderTestWorld Board(int lastX = 9)
        {
            var world = new RenderTestWorld(10, 10, 4);
            for (int z = 0; z < 10; z++)
            for (int x = 0; x <= lastX; x++)
                world.Solid(x, z, Ground);
            return world.Publish();
        }

        static Vector3 FeetAt(int x, int z) => CellMetrics.FloorCentre(x, z, Floor);

        static Vector3 WoundOver(Vector3 feet) => feet + Vector3.up * 1.3f;

        /// <summary>Let everything in the air come down: a few seconds of game time at one tick.</summary>
        static void Settle(BloodDirector blood, long tick, float seconds = 3f)
        {
            for (float t = 0f; t < seconds; t += 0.02f) blood.Step(0.02f, tick);
        }

        [Test]
        public void AHitLeavesOneMarkWhereItsLeadDropLandsAlongTheBlow()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);

            blood.Step(0f, 100);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Assert.That(blood.DropsInFlight, Is.EqualTo(BloodSpray.Drops(10f, sharp: true)));
            Assert.That(blood.Marks.Count, Is.Zero, "nothing lies on the ground before a drop lands");

            Settle(blood, 100);

            Assert.That(blood.DropsInFlight, Is.Zero, "every drop came down");
            Assert.That(blood.Marks.Count, Is.EqualTo(1), "one mark for the hit, not one per drop");
            BloodMarkRecord mark = blood.Marks[0];
            Assert.That(mark.Shape, Is.EqualTo(BloodShape.Splatter));
            Assert.That(mark.X, Is.GreaterThan(feet.x + 0.5f), "the mark lands ahead, the way the blow went");
            Assert.That(mark.Z, Is.EqualTo(feet.z).Within(1e-3f), "straight along it");
            Assert.That(mark.Layer, Is.EqualTo(Floor));
            Assert.That(mark.Born, Is.EqualTo(100));
            Assert.That(mark.Y, Is.EqualTo(Floor * CellMetrics.SizeY + BloodDirector.MarkLift).Within(1e-4f));
        }

        [Test]
        public void ABlowLeavesASmallerRoundSpot()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.forward, 10f, sharp: false);
            Settle(blood, 0);

            Assert.That(blood.Marks.Count, Is.EqualTo(1));
            Assert.That(blood.Marks[0].Shape, Is.EqualTo(BloodShape.Spot));
            Assert.That(blood.Marks[0].Stretch, Is.EqualTo(1f));
            Assert.That(blood.Marks[0].Radius, Is.LessThan(BloodSpray.MarkRadius(10f, sharp: true)));
        }

        [Test]
        public void APauseHoldsTheDropsInTheAir()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            int thrown = blood.DropsInFlight;

            for (int i = 0; i < 300; i++) blood.Step(0f, 0);

            Assert.That(blood.DropsInFlight, Is.EqualTo(thrown), "a paused frame moved the drops");
            Assert.That(blood.Marks.Count, Is.Zero);
        }

        [Test]
        public void NothingIsLaidOffTheEdgeIntoWaterOrAgainstAWall()
        {
            // Ground stops after column 3: a cut from the middle of column 3 throws its lead drop
            // over the edge into column 4.
            var edge = Board(lastX: 3);
            var blood = new BloodDirector(edge.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);
            Assert.That(blood.DropsInFlight, Is.Zero, "a drop over the edge still comes to an end");
            Assert.That(blood.Marks.Count, Is.Zero, "a stain was left hanging over the drop");

            var pond = Board();
            for (int z = 0; z < 10; z++) pond.Surface(4, z, Floor, NaturalContent.TerrainShallowWater);
            pond.Publish();
            blood = new BloodDirector(pond.Model);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);
            Assert.That(blood.Marks.Count, Is.Zero, "a stain was left on the water");

            var walled = Board();
            for (int z = 0; z < 10; z++) walled.Solid(4, z, Floor);
            walled.Publish();
            blood = new BloodDirector(walled.Model);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);
            Assert.That(blood.Marks.Count, Is.Zero, "a stain was left inside the wall");

            // And the control: the same cut on open ground does leave one.
            var open = Board();
            blood = new BloodDirector(open.Model);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);
            Assert.That(blood.Marks.Count, Is.EqualTo(1), "the control left nothing, so the three above prove nothing");
        }

        [Test]
        public void AMarkOnAFloorSitsOnTheFloorAndGoesWhenTheGroundUnderItDoes()
        {
            // A slab over open air at column 3, row 3: blood on it stands on the slab.
            var world = new RenderTestWorld(10, 10, 4);
            world.Slab(3, 3, Floor);
            world.Publish();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 1f, sharp: false);
            Settle(blood, 0);
            Assert.That(blood.Marks.Count, Is.EqualTo(1), "blood on a slab over air");
            Assert.That(blood.Marks[0].Y,
                Is.EqualTo(Floor * CellMetrics.SizeY + CellMetrics.SlabLift + BloodDirector.MarkLift).Within(1e-4f),
                "on the slab's surface, not the plane under it");

            // Ground mined out from under a mark takes the mark.
            var dug = Board();
            blood = new BloodDirector(dug.Model);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);
            Assert.That(blood.Marks.Count, Is.EqualTo(1));
            int column = Mathf.FloorToInt(blood.Marks[0].X / CellMetrics.SizeXZ);
            using (var renderer = new ChunkRenderer(dug.Model) { SubmitToGpu = false })
            {
                blood.Draw(renderer, 0, 3);
                Assert.That(blood.Marks.Count, Is.EqualTo(1), "a mark on standing ground went");

                dug.Mine(column, 3, Ground).Publish();
                blood.Draw(renderer, 0, 3);
                Assert.That(blood.Marks.Count, Is.Zero, "a stain hung in the air where the ground was");
            }
        }

        [Test]
        public void APoolWaitsForTheFallAndGoesUnderTheBody()
        {
            var world = Board();
            Vector3 feet = FeetAt(4, 4);
            Vector3 middle = feet + new Vector3(0.9f, 0f, 0f);
            var who = new PawnId(7);
            var blood = new BloodDirector(world.Model, (PawnId pawn, out Vector3 at) =>
            {
                at = middle;
                return pawn == who;
            });

            blood.Step(0f, 500);
            blood.Pool(who, feet, 1f, BloodSpray.PersonLength);
            blood.Step(BloodSpray.PoolWaitSeconds - 0.1f, 500);
            Assert.That(blood.Marks.Count, Is.Zero, "a pool before the body has finished falling");
            Assert.That(blood.PoolsWaiting, Is.EqualTo(1));

            blood.Step(0.2f, 500);
            Assert.That(blood.PoolsWaiting, Is.Zero);
            Assert.That(blood.Marks.Count, Is.EqualTo(1));
            BloodMarkRecord pool = blood.Marks[0];
            Assert.That(pool.Shape, Is.EqualTo(BloodShape.Pool));
            Assert.That(pool.X, Is.EqualTo(middle.x).Within(1e-4f), "under the body's middle, not its feet");
            Assert.That(pool.Radius, Is.EqualTo(BloodSpray.PoolRadius(BloodSpray.PersonLength, 1f)).Within(1e-5f));
            Assert.That(pool.Y, Is.EqualTo(Floor * CellMetrics.SizeY + BloodDirector.PoolLift).Within(1e-4f));

            // Nobody to ask: the feet it was given.
            var alone = new BloodDirector(world.Model);
            alone.Pool(new PawnId(8), feet, 0.6f, BloodSpray.PersonLength);
            alone.Step(BloodSpray.PoolWaitSeconds + 0.1f, 0);
            Assert.That(alone.Marks.Count, Is.EqualTo(1));
            Assert.That(alone.Marks[0].X, Is.EqualTo(feet.x).Within(1e-4f));
        }

        [Test]
        public void APoolSpreadsByTheTick()
        {
            var mark = new BloodMarkRecord
            {
                X = 5f, Y = 3f, Z = 5f, Radius = 0.8f, Stretch = 1f, Shape = BloodShape.Pool, Born = 1_000, Layer = 1,
            };
            float Width(long now) => BloodDirector.PlacementOf(mark, now).GetColumn(0).magnitude;

            Assert.That(Width(1_000), Is.EqualTo(0.8f * BloodSpray.PoolStartFraction).Within(1e-4f));
            Assert.That(Width(1_000 + BloodSpray.PoolSpreadTicks), Is.EqualTo(0.8f).Within(1e-4f));

            mark.Shape = BloodShape.Splatter;
            mark.Stretch = 1.7f;
            Assert.That(Width(1_000), Is.EqualTo(0.8f * 1.7f).Within(1e-4f), "a hit's mark is its size at once, stretched along the blow");
        }

        [Test]
        public void AMarkAgesByTheTickAndNotByTheClock()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);
            Assert.That(blood.Marks.Count, Is.EqualTo(1));

            // An hour of frames on one tick is a paused game, or a very long frame: no older.
            for (int i = 0; i < 1_000; i++) blood.Step(3.6f, 0);
            Assert.That(blood.Marks.Count, Is.EqualTo(1), "the clock aged a mark the tick had not");

            blood.Step(0f, BloodLedger.LifetimeTicks - 1);
            Assert.That(blood.Marks.Count, Is.EqualTo(1));
            blood.Step(0f, BloodLedger.LifetimeTicks);
            Assert.That(blood.Marks.Count, Is.Zero, "a day's ticks and it is gone");
        }

        [Test]
        public void TheSliceHidesAMarkWithoutTakingItUp()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            Settle(blood, 0);

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            blood.Draw(renderer, 2, 3);
            Assert.That(blood.LastMarksDrawn, Is.Zero, "a mark on an undrawn layer was drawn");
            Assert.That(blood.Marks.Count, Is.EqualTo(1), "and it must still be there");

            blood.Draw(renderer, 0, 3);
            Assert.That(blood.LastMarksDrawn, Is.EqualTo(1), "shown again when its layer is");
        }

        [Test]
        public void TwoHundredMarksCostDrawsInFadeStepsAndNoBloodCostsNothing()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);

            using (var empty = new ChunkRenderer(world.Model) { SubmitToGpu = false })
            {
                blood.Draw(empty, 0, 3);
                Assert.That(blood.LastDrawCalls, Is.Zero, "a colony with no blood submitted something");
            }

            // Two hundred and fifty hits over most of a day, on every cell of the board, sharp and
            // blunt, with pools among them: every shape at every fade step.
            long tick = 0;
            for (int i = 0; i < 250; i++, tick += 200)
            {
                Vector3 feet = FeetAt(1 + i % 7, 1 + (i / 7) % 7);
                blood.Step(0f, tick);
                if (i % 5 == 0) blood.Pool(new PawnId(i + 1), feet, 1f, BloodSpray.PersonLength);
                else blood.Spurt(feet, WoundOver(feet), i % 2 == 0 ? Vector3.right : Vector3.back, 5f + i % 20, sharp: i % 3 != 0);
                Settle(blood, tick, 1.6f);
            }

            Assert.That(blood.Marks.Count, Is.EqualTo(BloodLedger.Cap), "the cap holds at two hundred");

            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            blood.Draw(renderer, 0, 3);
            Assert.That(blood.LastMarksDrawn, Is.EqualTo(BloodLedger.Cap));
            Assert.That(blood.LastDrawCalls, Is.LessThanOrEqualTo(3 * BloodLedger.FadeSteps + 1),
                "two hundred marks must cost draws in shapes and fade steps, never one per mark");
            Assert.That(blood.LastDrawCalls, Is.GreaterThan(1), "and they were drawn at all");
        }

        [Test]
        public void ClearTakesEverythingUp()
        {
            var world = Board();
            var blood = new BloodDirector(world.Model);
            Vector3 feet = FeetAt(3, 3);
            blood.Spurt(feet, WoundOver(feet), Vector3.right, 10f, sharp: true);
            blood.Pool(new PawnId(1), feet, 1f, BloodSpray.PersonLength);
            Settle(blood, 0, 0.3f);
            Assert.That(blood.DropsInFlight + blood.PoolsWaiting + blood.Marks.Count, Is.GreaterThan(0));

            blood.Clear();
            Assert.That(blood.DropsInFlight, Is.Zero);
            Assert.That(blood.PoolsWaiting, Is.Zero);
            Assert.That(blood.Marks.Count, Is.Zero);
        }

        [Test]
        public void AMarkIsTurnedAlongTheBlow()
        {
            foreach (Vector3 blow in new[] { Vector3.right, Vector3.forward, Vector3.back, new Vector3(-1f, 0f, 1f).normalized })
            {
                Vector3 along = Quaternion.Euler(0f, BloodDirector.YawOf(blow), 0f) * Vector3.right;
                Assert.That(Vector3.Distance(along, blow), Is.LessThan(1e-4f), $"a blow towards {blow} laid its mark towards {along}");
            }
        }
    }
}
