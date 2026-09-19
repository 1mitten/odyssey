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
            int small = ItemHeap.Armful(7u, Vector3.zero, stone, placements);
            int large = ItemHeap.Armful(7u, Vector3.zero, stone, placements);

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
            int rocks = ItemHeap.Armful(3u, Vector3.zero, stone, placements);

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
            int rocks = ItemHeap.Armful(5u, Vector3.zero, stone, placements);
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
            int a = ItemHeap.Armful(99u, Vector3.one, stone, first);
            int b = ItemHeap.Armful(99u, Vector3.one, stone, again);

            Assert.That(b, Is.EqualTo(a));
            for (int i = 0; i < a; i++) Assert.That(again[i], Is.EqualTo(first[i]));
        }

        static readonly VectorComparer Vectors = new VectorComparer();

        class VectorComparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-8f;
            public int GetHashCode(Vector3 v) => v.GetHashCode();
        }
    }
}
