#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The lock-on ring (design 33 §7b): its clock — 1.6 times the footprint snapping on to the
    /// feet in 0.2 s, a flash as it lands, a faint hold, a fade — and which targets wear one, on
    /// which frame. The draw is presentation's and nothing here can see it; everything that
    /// decides what it looks like is here.
    /// </summary>
    public class LockOnRingTests
    {
        // ---------------------------------------------------------------- the curve

        [Test]
        public void TheRingAppearsAtOnePointSixAndLandsOnTheFeetAtAFifthOfASecond()
        {
            Assert.That(LockOnRing.Scale(0f), Is.EqualTo(1.6f).Within(1e-6f));
            Assert.That(LockOnRing.Scale(0.2f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(LockOnRing.Scale(0.5f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(LockOnRing.Scale(30f), Is.EqualTo(1f).Within(1e-6f));
        }

        /// <summary>
        /// An ease-out: most of the closing happens early, so the ring is already near the feet
        /// half-way through, and it only ever closes — it never overshoots inside them.
        /// </summary>
        [Test]
        public void TheSnapEasesOutAndNeverOvershoots()
        {
            float half = LockOnRing.Scale(0.1f);
            Assert.That(half, Is.LessThan(1.3f), "at half time a linear snap would be at 1.3; an ease-out is closer");

            float previous = LockOnRing.Scale(0f);
            for (int step = 1; step <= 40; step++)
            {
                float scale = LockOnRing.Scale(step * 0.01f);
                Assert.That(scale, Is.LessThanOrEqualTo(previous + 1e-6f), $"the ring grew at {step * 0.01f:0.00} s");
                Assert.That(scale, Is.GreaterThanOrEqualTo(1f - 1e-6f), $"the ring shrank inside the feet at {step * 0.01f:0.00} s");
                previous = scale;
            }
        }

        /// <summary>The flash: the brightest instant of the whole animation is the landing, and it settles to a faint hold.</summary>
        [Test]
        public void ItFlashesOnceAsItLandsAndThenHoldsFaint()
        {
            float landing = LockOnRing.HeldAlpha(LockOnRing.SnapSeconds);
            Assert.That(landing, Is.EqualTo(LockOnRing.FlashAlpha).Within(1e-6f));

            for (int step = 0; step <= 100; step++)
            {
                float t = step * 0.01f;
                if (System.Math.Abs(t - LockOnRing.SnapSeconds) < 1e-4f) continue;
                Assert.That(LockOnRing.HeldAlpha(t), Is.LessThan(landing), $"brighter at {t:0.00} s than at the landing");
            }

            Assert.That(LockOnRing.HeldAlpha(0f), Is.LessThan(landing));
            Assert.That(LockOnRing.HeldAlpha(LockOnRing.SettleSeconds), Is.EqualTo(LockOnRing.HoldAlpha).Within(1e-6f));
            Assert.That(LockOnRing.HeldAlpha(60f), Is.EqualTo(LockOnRing.HoldAlpha).Within(1e-6f));
            Assert.That(LockOnRing.HoldAlpha, Is.LessThan(LockOnRing.SnapAlpha), "the hold is fainter than the ring arriving");
        }

        [Test]
        public void AReleasedRingFadesToNothingAndIsThenGone()
        {
            Assert.That(LockOnRing.Evaluate(5f, -1f, out float scale, out float alpha), Is.True);
            Assert.That((scale, alpha), Is.EqualTo((1f, LockOnRing.HoldAlpha)));

            Assert.That(LockOnRing.Evaluate(5f, 0f, out _, out float atRelease), Is.True);
            Assert.That(atRelease, Is.EqualTo(LockOnRing.HoldAlpha).Within(1e-6f), "the fade starts where the hold was");

            Assert.That(LockOnRing.Evaluate(5f + LockOnRing.FadeSeconds * 0.5f, LockOnRing.FadeSeconds * 0.5f,
                out _, out float halfway), Is.True);
            Assert.That(halfway, Is.EqualTo(LockOnRing.HoldAlpha * 0.5f).Within(1e-5f));

            Assert.That(LockOnRing.Evaluate(5f + LockOnRing.FadeSeconds, LockOnRing.FadeSeconds, out _, out alpha), Is.False);
            Assert.That(alpha, Is.EqualTo(0f));
        }

        /// <summary>Let go during the snap, a ring stops where it was and fades from there; it does not finish closing.</summary>
        [Test]
        public void ARingReleasedMidSnapFadesAtTheSizeItHad()
        {
            float at = 0.05f;
            LockOnRing.Evaluate(at + 0.1f, 0.1f, out float scale, out float alpha);
            Assert.That(scale, Is.EqualTo(LockOnRing.Scale(at)).Within(1e-6f));
            Assert.That(alpha, Is.EqualTo(LockOnRing.HeldAlpha(at) * (1f - 0.1f / LockOnRing.FadeSeconds)).Within(1e-5f));
        }

        [Test]
        public void TheOpacityTakesAtMostThirtyThreeValues()
        {
            var seen = new HashSet<float>();
            for (int step = 0; step <= 10_000; step++) seen.Add(LockOnRing.Quantise(step / 10_000f));
            Assert.That(seen.Count, Is.LessThanOrEqualTo(LockOnRing.AlphaSteps + 1));
            Assert.That(LockOnRing.Quantise(0f), Is.EqualTo(0f));
            Assert.That(LockOnRing.Quantise(1f), Is.EqualTo(1f));
        }

        [Test]
        public void TheFootprintIsHalfTheLongerSide()
        {
            Assert.That(LockOnRing.FootRadius(1.15f, 1.15f), Is.EqualTo(0.575f).Within(1e-6f));
            Assert.That(LockOnRing.FootRadius(0.6f, 1.4f), Is.EqualTo(0.7f).Within(1e-6f), "a hog is longer than it is wide");
        }

        // ---------------------------------------------------------------- the mesh

        /// <summary>
        /// Every triangle of the flat ring faces up, by the rule the cube is held to: a ring wound
        /// the other way is valid geometry, draws nothing from above and reports a healthy draw.
        /// </summary>
        [Test]
        public void TheRingFacesUp()
        {
            int segments = LockOnRing.Segments;
            int[] triangles = LockOnRing.RingTriangles(segments);
            Assert.That(triangles.Length, Is.EqualTo(segments * 6));

            for (int t = 0; t < triangles.Length; t += 3)
            {
                LockOnRing.RingVertex(triangles[t], segments, out float ax, out float az);
                LockOnRing.RingVertex(triangles[t + 1], segments, out float bx, out float bz);
                LockOnRing.RingVertex(triangles[t + 2], segments, out float cx, out float cz);

                // The y of (b - a) x (c - a), with every y nought: (bz - az)(cx - ax) - (bx - ax)(cz - az).
                float up = (bz - az) * (cx - ax) - (bx - ax) * (cz - az);
                Assert.That(up, Is.GreaterThan(0f), $"triangle {t / 3} faces down");
            }
        }

        [Test]
        public void TheRingIsAThinBandOfUnitOuterRadius()
        {
            int segments = LockOnRing.Segments;
            for (int i = 0; i < segments * 2; i++)
            {
                LockOnRing.RingVertex(i, segments, out float x, out float z);
                float radius = (float)System.Math.Sqrt(x * x + z * z);
                Assert.That(radius, Is.EqualTo(i < segments ? 1f : LockOnRing.InnerRadius).Within(1e-5f));
            }
            Assert.That(1f - LockOnRing.InnerRadius, Is.InRange(0.05f, 0.25f), "a line, not a disc");
        }

        // ---------------------------------------------------------------- which targets

        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Raider = new PawnId(10),
            Hog = new PawnId(11), Other = new PawnId(12);

        static readonly object World = new object();

        /// <summary>A frame with Ada and Bo drafted unless said otherwise, and the targets they are attacking.</summary>
        static WorldSnapshot Frame(int adaTarget = 0, int boTarget = 0, bool raiderDown = false,
            bool raiderGone = false, bool adaDrafted = true)
        {
            WorldSnapshot frame = Odyssey.Tests.Hud.Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 700,
                flags: PawnFlags.Person | (adaDrafted ? PawnFlags.Drafted : PawnFlags.None)));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 700,
                flags: PawnFlags.Person | PawnFlags.Drafted));
            if (!raiderGone)
                frame.AddPawn(new PawnView(Raider, new CellRef(5, 1, 1), 800, 800, 700, kind: 3,
                    flags: PawnFlags.Person | PawnFlags.Hostile | (raiderDown ? PawnFlags.Downed : PawnFlags.None)));
            frame.AddPawn(new PawnView(Hog, new CellRef(6, 1, 1), 800, 800, 700, kind: 1, flags: PawnFlags.None));
            frame.AddPawn(new PawnView(Other, new CellRef(7, 1, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            if (adaTarget != 0) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.OrderTargetKey, adaTarget));
            if (boTarget != 0) frame.AddPawnAspect(new PawnAspect(Bo, CombatAspectNames.OrderTargetKey, boTarget));
            return frame;
        }

        /// <summary>
        /// A rescue names its patient as the order's target, and the ring says <i>attack</i>: a
        /// drafted colonist carrying somebody to a bed draws none (design 33 §11e). The control is
        /// the same frame with the attack's job, which draws one.
        /// </summary>
        [Test]
        public void ARescueDrawsNoRing()
        {
            static WorldSnapshot Carrying(int job)
            {
                WorldSnapshot frame = Odyssey.Tests.Hud.Frame.Write();
                frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 700, job,
                    flags: PawnFlags.Person | PawnFlags.Drafted));
                frame.AddPawn(new PawnView(Bo, new CellRef(1, 1, 1), 800, 800, 700, JobHandle.Downed,
                    flags: PawnFlags.Person | PawnFlags.Downed | PawnFlags.Carried));
                frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.OrderTargetKey, Bo.Value));
                return frame;
            }

            var rings = new LockOnRings();
            rings.Update(Carrying(JobHandle.Rescue), AdaOnly, 10f, World);
            Assert.That(rings.Rings.Count, Is.Zero, "a rescue drew the attack's ring");

            var control = new LockOnRings();
            control.Update(Carrying(JobHandle.AttackMelee), AdaOnly, 10f, World);
            Assert.That(control.Rings.Count, Is.EqualTo(1), "the control drew nothing, so the rescue case proves nothing");
        }

        static readonly PawnId[] AdaOnly = { Ada };
        static readonly PawnId[] Both = { Ada, Bo };
        static readonly PawnId[] Nobody = { };

        static LockOnRings.Ring Only(LockOnRings rings)
        {
            Assert.That(rings.Rings.Count, Is.EqualTo(1), "one ring");
            return rings.Rings[0];
        }

        /// <summary>
        /// The order is read off the frame: the ring starts, full size, on the first frame that
        /// publishes the target, and a frame with no target — a refused order — draws nothing.
        /// </summary>
        [Test]
        public void TheRingStartsOnTheFrameTheTargetIsPublished()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 10f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(0));

            // The order was refused: the frame still names nobody, and nothing is drawn.
            rings.Update(Frame(), AdaOnly, 10.1f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(0), "a refused order drew a ring");

            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 10.2f, World);
            LockOnRings.Ring ring = Only(rings);
            Assert.That(ring.Target, Is.EqualTo(Raider));
            Assert.That(ring.Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f), "it did not start wide");
            Assert.That(ring.Holding, Is.True);

            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 10.2f + LockOnRing.SnapSeconds, World);
            ring = Only(rings);
            Assert.That(ring.Scale, Is.EqualTo(1f).Within(1e-6f), "it has not landed at 0.2 s");
            Assert.That(ring.Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.FlashAlpha)));

            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 20f, World);
            Assert.That(Only(rings).Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.HoldAlpha)), "it does not hold faint");
        }

        /// <summary>Only the selection's orders are drawn; an order from a colonist nobody selected is not signal.</summary>
        [Test]
        public void AnUnselectedColonistsOrderDrawsNothing()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(boTarget: Raider.Value), AdaOnly, 0.1f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(0));
        }

        /// <summary>An undrafted colonist hitting back has no order; the published target alone does not make one.</summary>
        [Test]
        public void AnUndraftedColonistsTargetIsNotAnOrder()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(adaDrafted: false), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value, adaDrafted: false), AdaOnly, 0.1f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(0));
        }

        /// <summary>Two colonists on one marauder: one ring. The squad ordered in one click locks on once.</summary>
        [Test]
        public void TwoAttackersShareOneRing()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), Both, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value, boTarget: Raider.Value), Both, 0.05f, World);
            Assert.That(Only(rings).Target, Is.EqualTo(Raider));

            // Bo's order lands a frame after Ada's, mid-snap: still the one ring, still snapping.
            rings = new LockOnRings();
            rings.Update(Frame(), Both, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), Both, 0.05f, World);
            rings.Update(Frame(adaTarget: Raider.Value, boTarget: Raider.Value), Both, 0.1f, World);
            Assert.That(Only(rings).Scale, Is.EqualTo(LockOnRing.Scale(0.05f)).Within(1e-6f),
                "the second order restarted a snap still under way");

            // Ada's order ends and Bo's holds: the ring stays.
            rings.Update(Frame(boTarget: Raider.Value), Both, 5f, World);
            Assert.That(Only(rings).Holding, Is.True);
        }

        /// <summary>A second order on a settled ring is a new order and gets its snap.</summary>
        [Test]
        public void ASecondOrderOnASettledRingSnapsAgain()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), Both, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), Both, 0.1f, World);
            rings.Update(Frame(adaTarget: Raider.Value, boTarget: Raider.Value), Both, 3f, World);
            Assert.That(Only(rings).Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f));
        }

        [Test]
        public void TheRingFadesWhenTheTargetGoesDownAndIsGoneAfterTheFade()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 2f, World);

            // The frame the blow downs it, the attacker may still name it: the down ends the ring anyway.
            rings.Update(Frame(adaTarget: Raider.Value, raiderDown: true), AdaOnly, 2.5f, World);
            LockOnRings.Ring ring = Only(rings);
            Assert.That(ring.Holding, Is.False, "a downed target kept its ring");
            Assert.That(ring.Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.HoldAlpha)), "the fade starts at the hold");

            rings.Update(Frame(raiderDown: true), AdaOnly, 2.5f + LockOnRing.FadeSeconds * 0.5f, World);
            Assert.That(Only(rings).Alpha, Is.LessThan(LockOnRing.HoldAlpha));

            rings.Update(Frame(raiderDown: true), AdaOnly, 2.5f + LockOnRing.FadeSeconds + 0.01f, World);
            Assert.That(rings.Rings.Count, Is.EqualTo(0), "a faded ring was still drawn");
        }

        /// <summary>A corpse is not a pawn: the target leaving the frame ends the ring.</summary>
        [Test]
        public void TheRingFadesWhenTheTargetDies()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaTarget: Raider.Value, raiderGone: true), AdaOnly, 1f, World);
            Assert.That(Only(rings).Holding, Is.False);
        }

        /// <summary>
        /// Ordered on a pawn already down — to finish it — the ring holds while it lies there, and
        /// ends when it dies.
        /// </summary>
        [Test]
        public void AnOrderToFinishADownedPawnHoldsItsRing()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(raiderDown: true), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value, raiderDown: true), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaTarget: Raider.Value, raiderDown: true), AdaOnly, 3f, World);
            Assert.That(Only(rings).Holding, Is.True);
        }

        [Test]
        public void ChangingTheOrderMovesTheRing()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaTarget: Other.Value), AdaOnly, 2f, World);

            Assert.That(rings.Rings.Count, Is.EqualTo(2));
            foreach (LockOnRings.Ring ring in rings.Rings)
            {
                if (ring.Target == Raider) Assert.That(ring.Holding, Is.False, "the old target kept its ring");
                else
                {
                    Assert.That(ring.Target, Is.EqualTo(Other));
                    Assert.That(ring.Holding, Is.True);
                    Assert.That(ring.Scale, Is.EqualTo(LockOnRing.StartScale).Within(1e-6f), "the new target did not snap");
                }
            }
        }

        [Test]
        public void AnAnimalTargetWearsARingToo()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Hog.Value), AdaOnly, 0.1f, World);
            Assert.That(Only(rings).Target, Is.EqualTo(Hog));
        }

        /// <summary>
        /// Selecting a colonist already fighting, or loading a world mid-fight, shows the ring at
        /// rest: nobody gave an order in front of the player, so nothing snaps.
        /// </summary>
        [Test]
        public void AnOrderAlreadyUnderWayIsAdoptedAtRestNotSnapped()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 0f, World);
            LockOnRings.Ring ring = Only(rings);
            Assert.That(ring.Scale, Is.EqualTo(1f).Within(1e-6f), "a loaded world snapped a ring");
            Assert.That(ring.Alpha, Is.EqualTo(LockOnRing.Quantise(LockOnRing.HoldAlpha)));

            rings = new LockOnRings();
            rings.Update(Frame(adaTarget: Raider.Value), Nobody, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 1f, World);
            Assert.That(Only(rings).Scale, Is.EqualTo(1f).Within(1e-6f), "selecting a fighting colonist snapped a ring");

            // A different world object is a load: it adopts too, and forgets the old world's rings.
            rings.Update(Frame(adaTarget: Other.Value), AdaOnly, 2f, new object());
            ring = Only(rings);
            Assert.That(ring.Target, Is.EqualTo(Other));
            Assert.That(ring.Scale, Is.EqualTo(1f).Within(1e-6f));
        }

        /// <summary>Deselecting the attacker lets the ring go; it is the selection's orders that are drawn.</summary>
        [Test]
        public void DeselectingTheAttackerFadesTheRing()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaTarget: Raider.Value), Nobody, 1f, World);
            Assert.That(Only(rings).Holding, Is.False);
        }

        /// <summary>The same order clicked again is quiet in the simulation, so it is quiet here: no second snap.</summary>
        [Test]
        public void TheSameOrderAgainDoesNotSnapAgain()
        {
            var rings = new LockOnRings();
            rings.Update(Frame(), AdaOnly, 0f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 0.1f, World);
            rings.Update(Frame(adaTarget: Raider.Value), AdaOnly, 3f, World);
            Assert.That(Only(rings).Scale, Is.EqualTo(1f).Within(1e-6f));
        }
    }
}
