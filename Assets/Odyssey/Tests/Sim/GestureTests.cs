#nullable enable
using System.IO;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The simulation's whole half of a drawn gesture: a kind, a serial, and the promise that
    /// neither of them means anything to the simulation.
    ///
    /// <para>Picking a stack up takes no simulation time — one toil, one tick, the item changes
    /// hands — and the owner's decision (2026-09-16) is that it stays that way, so the stoop and
    /// the rise are drawn over an instant that has already passed. That leaves one question the
    /// contract has to answer honestly: <em>how does the thing drawing it find out?</em></para>
    ///
    /// <para>These tests are the answer, and the one that matters most is
    /// <see cref="TheReportStaysUpSoThatNoFrameCanMissIt"/>. A flag raised for a single tick is
    /// unobservable: presentation reads one snapshot a frame and the simulation runs several ticks
    /// between frames at speed three, so the lift would play at slow speeds, fail to play at fast
    /// ones, and look for all the world like a rendering bug.</para>
    /// </summary>
    public class GestureTests
    {
        const int Wood = ItemIndex.Wood;

        /// <summary>A colony with one colonist, a pile, and one stack of wood loose on the floor.</summary>
        static (Colony colony, Pawn pawn, int store) Hauling(uint seed = 4242u)
        {
            var colony = Colony.Build(seed: seed);
            int store = colony.Cell(10, 10, 0);
            colony.Stockpile(1, store);
            colony.Ctx.Items.Spawn(Wood, colony.Cell(4, 4, 0), stack: 5);
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(5, 5, 0));
            return (colony, pawn, store);
        }

        /// <summary>Run until the predicate holds, or give up. Returns whether it held.</summary>
        static bool RunUntil(Colony colony, System.Func<bool> until, int limit = 5_000)
        {
            for (int tick = 0; tick < limit; tick++)
            {
                if (until()) return true;
                colony.World.Tick();
            }
            return until();
        }

        // ---- the two moments ---------------------------------------------------------------

        [Test]
        public void AHaulReportsALiftWhenItTakesTheLoadAndAStowWhenItSetsItDown()
        {
            var (colony, pawn, store) = Hauling();

            Assert.That(pawn.Gesture, Is.EqualTo(PawnGesture.None), "nothing has happened yet");
            Assert.That(pawn.GestureSerial, Is.EqualTo(0));

            Assert.That(RunUntil(colony, () => pawn.Gesture == PawnGesture.Lift), Is.True,
                "the colonist never picked the wood up");

            byte atLift = pawn.GestureSerial;
            Assert.That(atLift, Is.EqualTo(1), "one gesture has begun, so the serial has moved once");

            Assert.That(RunUntil(colony, () => pawn.Gesture == PawnGesture.Stow), Is.True,
                "the colonist never put the wood down");

            Assert.That(pawn.GestureSerial, Is.EqualTo((byte)(atLift + 1)));
            Assert.That(colony.Ctx.Items.ItemAt(store), Is.Not.Null, "and the haul really finished");
        }

        [Test]
        public void TheReportStaysUpSoThatNoFrameCanMissIt()
        {
            // The trap this contract is shaped around. Presentation reads the latest snapshot once
            // a frame; at speed three the simulation has run several ticks since the last one. A
            // gesture reported for only the tick it happened on is therefore missed routinely, and
            // missed *more often the faster the game runs* — which presents as a glitch rather than
            // as a contract that cannot be observed.
            var (colony, pawn, _) = Hauling();
            Assert.That(RunUntil(colony, () => pawn.Gesture == PawnGesture.Lift), Is.True);

            byte serial = pawn.GestureSerial;
            colony.World.Tick(60);

            Assert.That(pawn.Gesture, Is.EqualTo(PawnGesture.Lift),
                "a reader that looked a second later must still learn the lift happened");
            Assert.That(pawn.GestureSerial, Is.EqualTo(serial),
                "and must not be told it happened twice");
        }

        [Test]
        public void ThePublishedViewCarriesBoth()
        {
            var (colony, pawn, _) = Hauling();
            Assert.That(RunUntil(colony, () => pawn.Gesture == PawnGesture.Lift), Is.True);
            colony.World.Tick();

            var pawns = colony.World.Views.Current.Pawns;
            bool found = false;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i].Id.Value != pawn.Id.Value) continue;
                found = true;
                Assert.That(pawns[i].Gesture, Is.EqualTo(pawn.Gesture));
                Assert.That(pawns[i].GestureSerial, Is.EqualTo(pawn.GestureSerial));
            }

            Assert.That(found, Is.True, "the pawn was not in the snapshot at all");
        }

        // ---- the serial ---------------------------------------------------------------------

        [Test]
        public void TheSerialAdvancesEvenWhenTheSameGestureRepeats()
        {
            // Stickiness alone cannot distinguish one lift from two: the kind reads Lift across
            // both. Two stacks picked up in a row is the ordinary case, not a corner one.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(5, 5, 0));

            pawn.BeginGesture(PawnGesture.Lift);
            byte first = pawn.GestureSerial;
            pawn.BeginGesture(PawnGesture.Lift);

            Assert.That(pawn.GestureSerial, Is.Not.EqualTo(first));
        }

        [Test]
        public void TheSerialWrapsRatherThanOverflowing()
        {
            // A byte, and a long-lived colonist will go round it many times. The reader tests for
            // a *different* value and never a greater one, so the wrap costs nothing — but an
            // unchecked increment that threw would end the game on the 256th thing picked up.
            var colony = Colony.Build();
            var pawn = colony.Ctx.Pawns.Spawn(colony.Cell(5, 5, 0));
            pawn.GestureSerial = 255;

            Assert.DoesNotThrow(() => pawn.BeginGesture(PawnGesture.Stow));
            Assert.That(pawn.GestureSerial, Is.EqualTo(0));
        }

        // ---- and it must mean nothing to the simulation --------------------------------------

        [Test]
        public void AGestureIsNotInTheStateHash()
        {
            // The promise that makes all of the above free. A report about something that has
            // already happened must not be able to change what happens next, and a field on Pawn
            // that quietly reached the hash would be a save-format change nobody decided to make.
            var (colony, pawn, _) = Hauling();
            colony.World.Tick(200);

            ulong before = colony.World.ComputeStateHash().Value;

            foreach (var other in colony.Ctx.Pawns.All) other.BeginGesture(PawnGesture.Stow);

            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(before),
                "the look of the game has become part of its determinism contract");
            Assert.That(pawn.Gesture, Is.EqualTo(PawnGesture.Stow), "and the gesture really was set");
        }

        [Test]
        public void AGestureDoesNotSurviveASave()
        {
            // Correct rather than merely tolerated: a colonist should not resume a stoop it began
            // before the game was closed. It also means the reader has to treat a pawn it has
            // never seen as having gestured nothing — otherwise every colonist plays one phantom
            // lift on the first frame after a load.
            var (colony, pawn, _) = Hauling();
            Assert.That(RunUntil(colony, () => pawn.Gesture == PawnGesture.Lift), Is.True);

            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            byte[] bytes = stream.ToArray();

            var restored = Colony.Build(seed: 4242u);
            using var input = new MemoryStream(bytes);
            WorldSave.Load(restored.World, input, restored.SaveComponents);

            foreach (var loaded in restored.Ctx.Pawns.All)
            {
                Assert.That(loaded.Gesture, Is.EqualTo(PawnGesture.None));
                Assert.That(loaded.GestureSerial, Is.EqualTo(0));
            }
        }
    }
}
