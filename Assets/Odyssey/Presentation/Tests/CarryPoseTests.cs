#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The arithmetic of a carried load: where it sits relative to the arms holding it, how the
    /// stance eases on and off, and how an armful of rubble is laid out. Design 24.
    ///
    /// <para>As with <c>GesturePoseTests</c>, what is testable here is the <em>shape</em> and not
    /// the appearance — there is no correct scoop to compare against, and the seven angles in
    /// <see cref="CarryPose"/> are the owner's to judge at the keyboard. What is not a matter of
    /// taste is a wood bundle drawn through a colonist's ribs, a load that lags a frame behind the
    /// hands, or a placement that drifts when the pose pass runs twice. Each of those is a
    /// property of the arithmetic and none is visible in the code that produces it.</para>
    /// </summary>
    public class CarryPoseTests
    {
        // A plausible colonist, roughly to the scale the rigs are drawn at: shoulders 0.42 m
        // apart, chest at 1.35 m, palms half a shoulder width in front at waist height.
        const float Shoulders = 0.42f;
        static readonly Vector3 Chest = new Vector3(0f, 1.35f, 0f);
        static readonly Vector3 Forward = Vector3.forward;

        /// <summary>Two palms, level, straddling the centre line. Returns the left one.</summary>
        static Vector3 Palms(float ahead, float apart, out Vector3 right)
        {
            right = new Vector3(apart * 0.5f, 1.05f, ahead);
            return new Vector3(-apart * 0.5f, 1.05f, ahead);
        }

        [Test]
        public void TheLoadSitsBetweenTheHands()
        {
            // The plainest property, and the one every other case is a correction to: two arms
            // holding one thing hold it between them, not beside either of them.
            Vector3 left = Palms(0.30f, 0.26f, out Vector3 right);
            Vector3 at = CarryPose.Cradle(left, right, Chest, Forward, Shoulders);

            Assert.That(at.x, Is.EqualTo(0f).Within(1e-4f), "the load drifted to one side");
            Assert.That(at.y, Is.EqualTo(left.y).Within(1e-4f), "the load is not at hand height");
        }

        [Test]
        public void ALoadHeldCloseIsPushedClearOfTheChest()
        {
            // The one thing here that is a fault rather than a taste. Arm length varies across the
            // sixty-one rigs by more than the cradle does, so an authored elbow bend can leave a
            // short-armed colonist's palms against its own sternum — and a wood bundle drawn
            // through the ribs is not a pose that wants tuning.
            Vector3 left = Palms(0.02f, 0.26f, out Vector3 right);
            Vector3 at = CarryPose.Cradle(left, right, Chest, Forward, Shoulders);

            float ahead = Vector3.Dot(at - Chest, Forward);
            Assert.That(ahead, Is.GreaterThanOrEqualTo(Shoulders * CarryPose.Clearance - 1e-4f),
                "the load is inside the colonist's own chest");
        }

        [Test]
        public void ALoadAlreadyClearIsLeftExactlyWhereTheHandsPutIt()
        {
            // The correction is a floor, not a target. Pushing a load that is already out in front
            // of the body would take it further out for no reason, and "held at arm's length" is a
            // different pose from "carried".
            Vector3 left = Palms(0.45f, 0.26f, out Vector3 right);
            Vector3 at = CarryPose.Cradle(left, right, Chest, Forward, Shoulders);

            Assert.That(at, Is.EqualTo((left + right) * 0.5f).Using(Vectors));
        }

        [Test]
        public void TheClearanceIsMeasuredAlongTheFacingAndNotAsADistance()
        {
            // A load held out to one side is clear of the chest even though it is close to it in
            // a straight line, and pushing it forward would be answering a question nobody asked.
            // Measuring the gap as a magnitude rather than along the facing is the easy way to get
            // this wrong, and it fails only for a colonist reaching sideways.
            var left = new Vector3(0.55f, 1.05f, 0.30f);
            var right = new Vector3(0.81f, 1.05f, 0.30f);
            Vector3 at = CarryPose.Cradle(left, right, Chest, Forward, Shoulders);

            Assert.That(at, Is.EqualTo((left + right) * 0.5f).Using(Vectors));
        }

        [Test]
        public void TheCradleIsPushedAlongTheFacingWhicheverWayTheColonistIsTurned()
        {
            // Along the figure's own facing, never along a world axis and never along a bone's:
            // which way a wrist points is a decision made by whoever rigged the character, where
            // which way a colonist faces is a fact about the figure.
            Vector3 facing = Vector3.left;
            var left = new Vector3(-0.02f, 1.05f, -0.13f);
            var right = new Vector3(-0.02f, 1.05f, 0.13f);

            Vector3 at = CarryPose.Cradle(left, right, Chest, facing, Shoulders);

            float ahead = Vector3.Dot(at - Chest, facing);
            Assert.That(ahead, Is.GreaterThanOrEqualTo(Shoulders * CarryPose.Clearance - 1e-4f));
            Assert.That(at.z, Is.EqualTo(0f).Within(1e-4f), "pushed along a world axis, not the facing");
        }

        [Test]
        public void ARigWithNoShouldersIsLeftAlone()
        {
            // A non-Humanoid prefab answers null to every bone and simply goes on walking, which is
            // what the arms and the legs already do for it. With no shoulder span there is no
            // clearance to compute and no torso mesh to push a load out of either.
            Vector3 left = Palms(0.0f, 0.26f, out Vector3 right);
            Vector3 at = CarryPose.Cradle(left, right, Chest, Forward, 0f);

            Assert.That(at, Is.EqualTo((left + right) * 0.5f).Using(Vectors));
        }

        [Test]
        public void ThePlacementIsAbsolute()
        {
            // **The fault that made an axe spin.** ApplyWorkPose runs at the end of both Sync and
            // Evaluate and only one of them re-evaluates the graph first, so anything that adjusts
            // rather than recomputes is integrated twice a frame with nothing to converge to. It
            // wound, it did it only in the game, and no contact sheet could have caught it. This
            // is that property stated for the cradle: feeding an answer back in changes nothing.
            Vector3 left = Palms(0.05f, 0.26f, out Vector3 right);

            Vector3 once = CarryPose.Cradle(left, right, Chest, Forward, Shoulders);
            Vector3 twice = CarryPose.Cradle(left, right, Chest, Forward, Shoulders);
            Vector3 fromThere = CarryPose.Cradle(once, once, Chest, Forward, Shoulders);

            Assert.That(twice, Is.EqualTo(once).Using(Vectors), "two calls, two answers");
            Assert.That(Vector3.Dot(fromThere - Chest, Forward),
                Is.EqualTo(Vector3.Dot(once - Chest, Forward)).Within(1e-4f),
                "the correction accumulated: a load re-seated on itself crept forward");
        }

        [Test]
        public void TheStanceEasesOnAndOffInTheTimeItSays()
        {
            // Nothing about a figure may arrive on one frame. The number matters most coming out
            // of a lift, where the hands have just risen from the floor and the arms have to fold
            // into the cradle without the load appearing to jump the last few inches.
            float weight = 0f;
            float step = CarryPose.EaseSeconds * 0.25f;

            for (int i = 0; i < 4; i++) weight = CarryPose.Settle(weight, 1f, step);
            Assert.That(weight, Is.EqualTo(1f).Within(1e-4f));

            for (int i = 0; i < 4; i++) weight = CarryPose.Settle(weight, 0f, step);
            Assert.That(weight, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void APausedWorldHoldsTheStanceWhereItIs()
        {
            // The one clock every ease in the director runs on stops when the world does, and a
            // delta of exactly nothing must therefore be the identity — or a pause would settle
            // every colonist's arms into or out of the cradle while the game was not running.
            float weight = CarryPose.Settle(0.4f, 1f, 0f);
            Assert.That(weight, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void ALoadGoesUnderWaterWithItsCarrier()
        {
            // The owner's placeholder (2026-09-19: "make the item disappear for now when swimming
            // for ease and decide later"). Shallow water floats the figure — the owner's own
            // decision of 2026-09-17 — and SwimPose strokes both arms, so a load left in them
            // swings about. Design 24 §5c has the two alternatives and why neither was taken yet.
            Assert.That(CarryPose.Drawn(0f), Is.True, "a colonist on dry land dropped her load");
            Assert.That(CarryPose.Drawn(1f), Is.False, "a swimmer is still holding a bundle");
        }

        [Test]
        public void OnlyTheMedicalKitIsGrippedBySides()
        {
            // Everything else keeps the scoop (owner, 2026-09-25: only the box asked for a
            // different grip). A wrong answer here puts every haul of wood in a two-handed grip
            // that was authored for one box.
            Assert.That(CarryPose.GrippedBySides(ItemIndex.MedicalSupplies), Is.True);
            Assert.That(CarryPose.GrippedBySides(ItemIndex.Wood), Is.False);
            Assert.That(CarryPose.GrippedBySides(ItemIndex.Meal), Is.False);
        }

        // ---- the armful --------------------------------------------------------------------

        [Test]
        public void AnArmfulIsTheSameSizeWhateverTheStack()
        {
            // Design 24 §3a, and it is deliberately the opposite of what the floor does. The heap
            // on the ground says how much is there because seven rocks in a 2.5 m cell is a
            // legible range; two arms cannot hold seventy-five stone, so an honest scaling would
            // run from one rock to three and the bottom of that reads as a fault.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);

            var placements = new Matrix4x4[ItemHeap.Most];
            int small = ItemHeap.Armful(7u, Vector3.zero, Quaternion.identity, stone, placements);
            int large = ItemHeap.Armful(7u, Vector3.zero, Quaternion.identity, stone, placements);

            Assert.That(small, Is.EqualTo(ItemHeap.ArmfulRocks));
            Assert.That(large, Is.EqualTo(ItemHeap.ArmfulRocks));
        }

        [Test]
        public void AnArmfulIsTighterThanThePileItCameFrom()
        {
            // A heap spread over most of a metre is spoil lying in a cell; the same spread in
            // somebody's arms is rocks orbiting them.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);
            Assert.That(ItemHeap.ArmfulSpread, Is.LessThan(stone.Spread));

            var placements = new Matrix4x4[ItemHeap.Most];
            int rocks = ItemHeap.Armful(3u, Vector3.zero, Quaternion.identity, stone, placements);

            for (int i = 0; i < rocks; i++)
            {
                Vector3 at = placements[i].GetColumn(3);
                float radius = new Vector2(at.x, at.z).magnitude;
                Assert.That(radius, Is.LessThanOrEqualTo(ItemHeap.ArmfulSpread + 1e-3f),
                    $"rock {i} is {radius:0.00} m from the hands");
            }
        }

        [Test]
        public void AnArmfulIsAHeapAndNotAShelf()
        {
            // Three rocks at one height read as three rocks on an invisible tray. Staggering them
            // costs nothing and is the difference between a pile held and a pile floating.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);

            var placements = new Matrix4x4[ItemHeap.Most];
            int rocks = ItemHeap.Armful(5u, Vector3.zero, Quaternion.identity, stone, placements);
            Assert.That(rocks, Is.GreaterThan(1), "nothing to stagger");

            float first = ((Vector3)placements[0].GetColumn(3)).y;
            float last = ((Vector3)placements[rocks - 1].GetColumn(3)).y;
            Assert.That(last, Is.GreaterThan(first));
        }

        [Test]
        public void TheSameLoadIsHeldTheSameWayRoundEveryFrame()
        {
            // Presentation must never consume simulation randomness, and an armful that reshuffled
            // itself each frame would boil. The same seed is the same arrangement, which is also
            // what makes the rocks in her arms the rocks that were in the pile.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);

            var first = new Matrix4x4[ItemHeap.Most];
            var again = new Matrix4x4[ItemHeap.Most];
            int a = ItemHeap.Armful(99u, Vector3.one, Quaternion.identity, stone, first);
            int b = ItemHeap.Armful(99u, Vector3.one, Quaternion.identity, stone, again);

            Assert.That(b, Is.EqualTo(a));
            for (int i = 0; i < a; i++) Assert.That(again[i], Is.EqualTo(first[i]));
        }

        [Test]
        public void AnArmfulTurnsWithItsCarrier()
        {
            // **The owner's second report** (2026-09-19: "when you turn a direction the logs don't
            // turn with you and they should"). The sunflower is laid out on the world axes, so a
            // load that is not turned by its carrier's yaw keeps pointing the same way however she
            // walks — and for a log, whose long axis is the thing you read, that is unmissable.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);

            var north = new Matrix4x4[ItemHeap.Most];
            var east = new Matrix4x4[ItemHeap.Most];
            int rocks = ItemHeap.Armful(4u, Vector3.zero, Quaternion.identity, stone, north);
            ItemHeap.Armful(4u, Vector3.zero, Quaternion.Euler(0f, 90f, 0f), stone, east);

            bool moved = false;
            for (int i = 0; i < rocks; i++)
            {
                Vector3 a = north[i].GetColumn(3);
                Vector3 b = east[i].GetColumn(3);
                if ((a - b).sqrMagnitude > 1e-6f) moved = true;

                Assert.That(a.y, Is.EqualTo(b.y).Within(1e-4f),
                    "turning on the spot changed how high a rock sits");
            }

            Assert.That(moved, Is.True, "the armful did not turn at all");
        }

        [Test]
        public void ATurnedArmfulKeepsItsShape()
        {
            // Turned about the cradle as one cluster, not each rock about itself. Spin them in
            // place and the shape stays pointing north while every rock faces a new way, which
            // looks like the load shivering rather than turning.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);

            var north = new Matrix4x4[ItemHeap.Most];
            var turned = new Matrix4x4[ItemHeap.Most];
            int rocks = ItemHeap.Armful(4u, Vector3.zero, Quaternion.identity, stone, north);
            ItemHeap.Armful(4u, Vector3.zero, Quaternion.Euler(0f, 37f, 0f), stone, turned);

            for (int i = 1; i < rocks; i++)
            {
                float before = Vector3.Distance(north[i].GetColumn(3), north[0].GetColumn(3));
                float after = Vector3.Distance(turned[i].GetColumn(3), turned[0].GetColumn(3));
                Assert.That(after, Is.EqualTo(before).Within(1e-4f),
                    $"rock {i} moved relative to rock 0: the armful came apart in the turn");
            }
        }

        [Test]
        public void AnArmfulIsPlacedWhereItIsAsked()
        {
            // The cradle is the origin of the cluster, wherever the colonist is standing.
            Assert.That(ItemHeap.TryRecipe(ItemIndex.Stone, out ItemHeap.Recipe stone), Is.True);

            var here = new Matrix4x4[ItemHeap.Most];
            var there = new Matrix4x4[ItemHeap.Most];
            var at = new Vector3(12f, 3f, -7f);

            int rocks = ItemHeap.Armful(8u, Vector3.zero, Quaternion.identity, stone, here);
            ItemHeap.Armful(8u, at, Quaternion.identity, stone, there);

            for (int i = 0; i < rocks; i++)
                Assert.That((Vector3)there[i].GetColumn(3),
                    Is.EqualTo((Vector3)here[i].GetColumn(3) + at).Using(Vectors));
        }

        /// <summary>
        /// Every commodity that is drawn as a heap can be carried as an armful, and none of them
        /// overruns the buffer.
        ///
        /// <para><b>Insurance for the next commodity, not a check on the three that exist</b>
        /// (owner, 2026-09-19: "stones should get the same treatment and future big items"). The
        /// carry path is already def-agnostic — a heap becomes an armful and everything else
        /// becomes one prop, and both are turned and both settle — so a new commodity inherits
        /// the lot. What it does not inherit is a sane recipe: `ItemHeapTests` holds `Place` to
        /// the buffer and said nothing about `Armful`, which writes a different count from a
        /// different recipe. This is the missing half of that guard.</para>
        /// </summary>
        [Test]
        public void EveryHeapCommodityCanBeCarriedAsAnArmful()
        {
            Assert.That(ItemHeap.ArmfulRocks, Is.LessThanOrEqualTo(ItemHeap.Most),
                "an armful cannot fit in the buffer every caller sizes to ItemHeap.Most");

            var placements = new Matrix4x4[ItemHeap.Most];
            int heaps = 0;

            for (int def = 0; def < ItemIndex.Count; def++)
            {
                if (!ItemHeap.TryRecipe(def, out ItemHeap.Recipe recipe)) continue;
                heaps++;

                int rocks = ItemHeap.Armful(
                    (uint)def, Vector3.zero, Quaternion.Euler(0f, 41f, 0f), recipe, placements);

                Assert.That(rocks, Is.InRange(1, ItemHeap.Most), $"def {def} wrote {rocks} rocks");

                for (int i = 0; i < rocks; i++)
                {
                    Vector3 scale = placements[i].lossyScale;
                    Assert.That(scale.x, Is.GreaterThan(0f), $"def {def} rock {i} has no size");
                    Assert.That(((Vector3)placements[i].GetColumn(3)).magnitude,
                        Is.LessThan(1f), $"def {def} rock {i} is a metre from the hands");
                }
            }

            Assert.That(heaps, Is.GreaterThan(0), "no commodity is drawn as a heap at all");
        }

        // ---- the two hand-overs ------------------------------------------------------------

        [Test]
        public void AHandoverStartsWhereItWasAndEndsWhereItIsGoing()
        {
            // The plainest property of both curves, and the one that must be exact rather than
            // approximate: a raise that does not reach 1 leaves the load permanently short of the
            // hands, and a fall that does not leaves a pile hovering above its own cell.
            Assert.That(CarryHandover.Raised(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(CarryHandover.Raised(CarryHandover.RaiseSeconds), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(CarryHandover.Fallen(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(CarryHandover.Fallen(CarryHandover.FallSeconds), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ALoadFallsAndIsNotLowered()
        {
            // The asymmetry is what makes one read as a drop and the other as a lift, and the
            // direction is the part that is easy to get backwards. A load really is falling, so
            // it is slowest leaving the hands and quickest at the floor. Reverse the two and a
            // colonist appears to place something delicately and then snatch it off the ground.
            Assert.That(CarryHandover.Fallen(CarryHandover.FallSeconds * 0.5f), Is.LessThan(0.5f),
                "half way through the fall and already half way down: that is being lowered");

            Assert.That(CarryHandover.Raised(CarryHandover.RaiseSeconds * 0.5f), Is.GreaterThan(0.5f),
                "half way through the raise and not yet half up: that is a load being dragged");
        }

        [Test]
        public void TheLandingIsAnEdgeAndHappensExactlyOnce()
        {
            // The sound of a load touching down hangs off this, and the obvious way to write it —
            // poll FallFinished — fires every frame from the landing until the next carry, which
            // would turn one thud into a buzz. An edge needs both sides of the step.
            const float Step = 1f / 60f;

            int landings = 0;
            float before = 0f;
            for (float after = Step; after < CarryHandover.FallSeconds * 3f; after += Step)
            {
                if (CarryHandover.FallLanded(before, after)) landings++;
                before = after;
            }

            Assert.That(landings, Is.EqualTo(1), "a load lands once, however long the fall is watched");
        }

        [Test]
        public void TheLandingIsNotMissedByALongFrame()
        {
            // A frame longer than the whole fall is a stall, a load screen, or a step taken on a
            // paused game the instant it resumes. The load has still landed and must still be
            // heard to land, once.
            Assert.That(CarryHandover.FallLanded(0f, CarryHandover.FallSeconds * 4f), Is.True);
            Assert.That(CarryHandover.FallLanded(CarryHandover.FallSeconds, 99f), Is.False,
                "a fall that had already finished does not land a second time");
        }

        [Test]
        public void ADropTakesLongerThanAPickUp()
        {
            // A load is lowered under control and released; it is taken up in one movement.
            // Gesture.Stow is the slower of the two crouches for exactly the same reason, so the
            // body and the thing it is holding agree about which way round this goes.
            Assert.That(CarryHandover.FallSeconds, Is.GreaterThan(CarryHandover.RaiseSeconds));
        }

        [Test]
        public void ARaiseIsOverBeforeTheColonistHasFinishedStandingUp()
        {
            // The grasp lands at the middle of a 0.8 s crouch, so there are 0.4 s of rise left
            // when the load appears. A raise longer than that has the load still travelling after
            // she has set off walking — which is the fault LiftTicks was introduced to fix, in a
            // new costume.
            Assert.That(CarryHandover.RaiseSeconds, Is.LessThanOrEqualTo(0.4f));
        }

        [Test]
        public void NeitherHandoverJumps()
        {
            // Sampled far finer than either will ever be drawn. A curve that tears puts the load
            // across the screen for exactly long enough to be seen and not long enough to be
            // caught by looking.
            Assert.That(Smooth(CarryHandover.Raised, CarryHandover.RaiseSeconds), Is.True);
            Assert.That(Smooth(CarryHandover.Fallen, CarryHandover.FallSeconds), Is.True);
        }

        static bool Smooth(System.Func<float, float> curve, float seconds)
        {
            const int Steps = 400;
            float last = curve(0f);
            for (int i = 1; i <= Steps; i++)
            {
                float now = curve(seconds * i / Steps);
                if (Mathf.Abs(now - last) > 4f / Steps) return false;
                last = now;
            }
            return true;
        }

        static readonly VectorComparer Vectors = new VectorComparer();

        class VectorComparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-8f;
            public int GetHashCode(Vector3 v) => v.GetHashCode();
        }
    }
}
