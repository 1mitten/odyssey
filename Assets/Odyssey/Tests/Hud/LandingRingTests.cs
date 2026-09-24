#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The landing ring (design 33 §20): the ring that replaced the floor bracket on the cell a
    /// selected colonist was sent to. Its curve is the lock-on ring's (§7b), asserted here to be
    /// the same curve rather than a copy; everything else — which colonists wear one, on which
    /// frame, and when it lets go — is its own and is here.
    /// </summary>
    public class LandingRingTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Raider = new PawnId(10);
        static readonly AspectKey Drafted = AspectKey.Of(OrderModel.DraftedAspect);
        static readonly AspectKey OrderCell = AspectKey.Of(OrderModel.OrderCellAspect);
        static readonly object World = new object();

        static readonly PawnId[] AdaOnly = { Ada };
        static readonly PawnId[] Both = { Ada, Bo };
        static readonly PawnId[] Nobody = { };

        const int CellA = 21, CellB = 44, CellC = 57;

        /// <summary>
        /// A frame with Ada and Bo drafted and walking to the cells given (-1: standing), and a
        /// marauder. <paramref name="adaDrafted"/> false publishes Ada's cell with no drafted row
        /// before it — how the simulation says she is fetching a weapon (§7a).
        /// </summary>
        static WorldSnapshot Frame(int adaCell = -1, int boCell = -1, bool adaDrafted = true, int raiderCell = -1)
        {
            WorldSnapshot frame = Odyssey.Tests.Hud.Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 700,
                flags: PawnFlags.Person | (adaDrafted ? PawnFlags.Drafted : PawnFlags.None)));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 700,
                flags: PawnFlags.Person | PawnFlags.Drafted));
            frame.AddPawn(new PawnView(Raider, new CellRef(5, 1, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            if (adaDrafted) frame.AddPawnAspect(new PawnAspect(Ada, Drafted, 1));
            if (adaCell >= 0) frame.AddPawnAspect(new PawnAspect(Ada, OrderCell, adaCell));
            frame.AddPawnAspect(new PawnAspect(Bo, Drafted, 1));
            if (boCell >= 0) frame.AddPawnAspect(new PawnAspect(Bo, OrderCell, boCell));
            if (raiderCell >= 0) frame.AddPawnAspect(new PawnAspect(Raider, OrderCell, raiderCell));
            return frame;
        }

        static LandingRings.Ring Only(LandingRings rings)
        {
            Assert.That(rings.Rings.Count, Is.EqualTo(1), "one ring");
            return rings.Rings[0];
        }

        static LandingRings.Ring For(LandingRings rings, PawnId pawn, int cell)
        {
            for (int i = 0; i < rings.Rings.Count; i++)
                if (rings.Rings[i].Pawn == pawn && rings.Rings[i].Cell == cell) return rings.Rings[i];
            Assert.Fail($"no ring for pawn {pawn.Value} on cell {cell}");
            return default;
        }

        /// <summary>
        /// Read off the frame: nothing while no cell is published (a refused move), then the ring
        /// starts wide on the first frame that publishes one, lands on its size at 0.2 s with the
        /// flash, and holds faint while she walks.
        /// </summary>
        [Test]
        public void TheRingStartsOnTheFrameTheCellIsPublishedAndLandsWithAFlash()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 10f, World);
            rings.Update(Frame(), AdaOnly, 10.1f, World);
            Assert.That(rings.Rings.Count, Is.Zero, "a refused move drew a ring");

            rings.Update(Frame(adaCell: CellA), AdaOnly, 10.2f, World);
            LandingRings.Ring ring = Only(rings);
            Assert.That((ring.Pawn, ring.Cell), Is.EqualTo((Ada, CellA)));
            Assert.That(ring.Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f), "it did not snap in from wide");
            Assert.That(ring.Holding, Is.True);

            rings.Update(Frame(adaCell: CellA), AdaOnly, 10.2f + LockOnRing.SnapSeconds, World);
            ring = Only(rings);
            Assert.That(ring.Scale, Is.EqualTo(1f).Within(1e-6f), "it has not landed at 0.2 s");
            Assert.That(ring.Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.FlashAlpha)), "no flash as it landed");

            rings.Update(Frame(adaCell: CellA), AdaOnly, 20f, World);
            Assert.That(Only(rings).Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.HoldAlpha)), "it does not hold faint");
        }

        /// <summary>
        /// One family with the lock-on: at every instant of the snap, the flash and the fade the
        /// landing ring is exactly <see cref="LockOnRing.Evaluate"/>. A second curve written here
        /// would drift from the first at the first retune.
        /// </summary>
        [Test]
        public void TheRingRunsOnTheLockOnsClock()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            for (int step = 1; step <= 40; step++)
            {
                float now = step * 0.01f;
                rings.Update(Frame(adaCell: CellA), AdaOnly, now, World);
                LockOnRing.Evaluate(now - 1 * 0.01f, -1f, out float scale, out float alpha);
                LandingRings.Ring ring = Only(rings);
                Assert.That(ring.Scale, Is.EqualTo(scale).Within(1e-6f), $"scale at {now:0.00} s");
                Assert.That(ring.Alpha, Is.EqualTo(LockOnRing.Quantise(alpha)), $"alpha at {now:0.00} s");
            }

            // Arrived at 0.40 s, so let go on the next frame: the fade is the lock-on's too.
            float orderedAt = 1 * 0.01f, releasedAt = 0.40f + 1 * 0.01f;
            for (int step = 1; step <= 30; step++)
            {
                float now = 0.40f + step * 0.01f;
                rings.Update(Frame(), AdaOnly, now, World);
                bool alive = LockOnRing.Evaluate(now - orderedAt, now - releasedAt, out _, out float alpha);
                if (!alive)
                {
                    Assert.That(rings.Rings.Count, Is.Zero, $"still drawn at {now:0.00} s after the lock-on's fade");
                    continue;
                }
                Assert.That(Only(rings).Alpha, Is.EqualTo(LockOnRing.Quantise(alpha)), $"fade alpha at {now:0.00} s");
            }
        }

        /// <summary>
        /// She arrives: the simulation stops publishing the cell, and the ring lets go at once,
        /// fades from the faint hold, and is gone after the fade. Undrafting her publishes no
        /// cell either (the move needs the draft), so it is the same frame to the ring.
        /// </summary>
        [Test]
        public void ArrivingFadesTheRingAndThenItIsGone()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 5f, World);

            rings.Update(Frame(), AdaOnly, 5.1f, World);
            LandingRings.Ring ring = Only(rings);
            Assert.That(ring.Holding, Is.False, "the ring still holds after she arrived");
            Assert.That(ring.Cell, Is.EqualTo(CellA), "it fades where it was");

            rings.Update(Frame(), AdaOnly, 5.1f + LockOnRing.FadeSeconds * 0.5f, World);
            Assert.That(Only(rings).Alpha, Is.LessThan(LockOnRing.Quantise(LockOnRing.HoldAlpha)), "it is not fading");

            rings.Update(Frame(), AdaOnly, 5.1f + LockOnRing.FadeSeconds, World);
            Assert.That(rings.Rings.Count, Is.Zero, "the ring outlived its fade");
        }

        /// <summary>
        /// A squad moved in one click is spread over several cells (§2d), and each colonist wears
        /// her own ring on her own cell — both snapping, since both are orders given.
        /// </summary>
        [Test]
        public void EachColonistOfASquadWearsARingOnHerOwnCell()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), Both, 0f, World);
            rings.Update(Frame(adaCell: CellA, boCell: CellB), Both, 0.1f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(2));
            Assert.That(For(rings, Ada, CellA).Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f));
            Assert.That(For(rings, Bo, CellB).Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f));
        }

        /// <summary>
        /// Sent somewhere else mid-walk: the old ring fades where it was and the new one snaps in
        /// on the new cell, both drawn for the length of the fade.
        /// </summary>
        [Test]
        public void SentElsewhereTheOldRingFadesAndTheNewOneSnaps()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 3f, World);

            rings.Update(Frame(adaCell: CellC), AdaOnly, 3.1f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(2));
            Assert.That(For(rings, Ada, CellA).Holding, Is.False, "the old destination still holds");
            LandingRings.Ring fresh = For(rings, Ada, CellC);
            Assert.That(fresh.Holding, Is.True);
            Assert.That(fresh.Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f), "the new order did not snap");

            rings.Update(Frame(adaCell: CellC), AdaOnly, 3.1f + LockOnRing.FadeSeconds, World);
            Assert.That(Only(rings).Cell, Is.EqualTo(CellC));
        }

        /// <summary>Only the selection's orders are drawn (§2g): twenty rings across the board is noise.</summary>
        [Test]
        public void AnUnselectedColonistsOrderDrawsNothing()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(boCell: CellB), AdaOnly, 0.1f, World);
            Assert.That(rings.Rings.Count, Is.Zero);
        }

        /// <summary>Deselecting her lets the ring go, as the order line goes.</summary>
        [Test]
        public void DeselectingFadesTheRing()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaCell: CellA), Nobody, 1f, World);
            Assert.That(Only(rings).Holding, Is.False);
        }

        /// <summary>
        /// Selecting a colonist already walking, or loading a world with one, shows her ring at
        /// rest: nobody gave an order in front of the player, so nothing snaps or flashes.
        /// </summary>
        [Test]
        public void AnOrderAlreadyUnderWayIsAdoptedAtRestNotSnapped()
        {
            var rings = new LandingRings();
            rings.Update(Frame(adaCell: CellA), AdaOnly, 0f, World);
            LandingRings.Ring ring = Only(rings);
            Assert.That(ring.Scale, Is.EqualTo(1f).Within(1e-6f), "a loaded world snapped a ring");
            Assert.That(ring.Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.HoldAlpha)));

            rings = new LandingRings();
            rings.Update(Frame(adaCell: CellA), Nobody, 0f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 1f, World);
            Assert.That(Only(rings).Scale, Is.EqualTo(1f).Within(1e-6f), "selecting a walking colonist snapped a ring");

            // Another world object is a load: it adopts, and forgets the old world's rings.
            rings.Update(Frame(adaCell: CellC), AdaOnly, 2f, new object());
            ring = Only(rings);
            Assert.That(ring.Cell, Is.EqualTo(CellC));
            Assert.That(ring.Scale, Is.EqualTo(1f).Within(1e-6f));
        }

        /// <summary>The same move clicked again is quiet in the simulation, so it is quiet here.</summary>
        [Test]
        public void TheSameOrderAgainDoesNotSnapAgain()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 3f, World);
            Assert.That(Only(rings).Scale, Is.EqualTo(1f).Within(1e-6f));
        }

        /// <summary>
        /// Sent back to a cell whose ring is still fading: the order is new, so it snaps again
        /// rather than being revived at rest.
        /// </summary>
        [Test]
        public void SentBackToAFadingRingSnapsAgain()
        {
            var rings = new LandingRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 3f, World);
            rings.Update(Frame(), AdaOnly, 3.05f, World);
            rings.Update(Frame(adaCell: CellA), AdaOnly, 3.1f, World);
            LandingRings.Ring ring = Only(rings);
            Assert.That(ring.Holding, Is.True);
            Assert.That(ring.Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f));
        }

        /// <summary>
        /// The Equip order shares the marker (§7a): a colonist sent for a weapon, drafted or not,
        /// wears the ring on the weapon's cell, since the simulation publishes it as her order
        /// cell. A marauder never does — the ring is the colony's orders, asked of colonists only.
        /// </summary>
        [Test]
        public void AnEquipOrderWearsTheRingAndAMarauderNever()
        {
            var rings = new LandingRings();
            rings.Update(Frame(adaDrafted: false), AdaOnly, 0f, World);
            rings.Update(Frame(adaCell: CellA, adaDrafted: false), AdaOnly, 0.1f, World);
            Assert.That(Only(rings).Cell, Is.EqualTo(CellA), "an undrafted colonist fetching a weapon drew no ring");

            var hostile = new LandingRings();
            PawnId[] raider = { Raider };
            hostile.Update(Frame(), raider, 0f, World);
            hostile.Update(Frame(raiderCell: CellB), raider, 0.1f, World);
            Assert.That(hostile.Rings.Count, Is.Zero, "a marauder's cell drew a ring");
        }

        [Test]
        public void TheRingIsAPlaceWiderThanAPersonAndInsideItsCell()
        {
            Assert.That(LandingRings.Radius * 2f, Is.LessThan(2.5f), "wider than the cell");
            Assert.That(LandingRings.Radius, Is.GreaterThan(LockOnRing.FootRadius(1.15f, 1.15f)),
                "no wider than a person's lock-on ring, so it reads as a body rather than a place");
        }
    }
}
