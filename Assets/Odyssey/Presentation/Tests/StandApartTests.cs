#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// **Two people standing on one cell are never drawn at one point** (owner, 2026-09-25:
    /// "Don't have them exactly over each other — that should never happen in any scenario";
    /// <c>docs/design/25-pawn-steering.md</c> §10).
    ///
    /// <para>The simulation lets several pawns share a cell and the pose drew every one of them at
    /// its centre, because the sub-tile sidestep only ever ran for a pawn that was walking. These
    /// pin the rule that replaced it: apart when they share, untouched when alone, and the same
    /// answer every frame whatever order the snapshot lists them in.</para>
    ///
    /// <para>Distances are horizontal. The ground relief is a static field every fixture shares,
    /// and a height difference is not what "drawn at the same point" is about.</para>
    /// </summary>
    public class StandApartTests
    {
        [TearDown]
        public void PutTheModeBack() => PawnCrowdIndex.Mode = CrowdScan.Bucketed;

        static readonly CellRef Fireside = new CellRef(4, 4, 0);

        static PawnView Standing(int id, CellRef cell, bool asleep = false, bool seated = false,
            CellRef workCell = default, PawnFlags flags = PawnFlags.Person) =>
            new PawnView(new PawnId(id), cell, 100, 100, 50, -1, cell, 0,
                workCell: seated ? workCell : cell, asleep: asleep, flags: flags, seated: seated);

        static PawnView Walking(int id, CellRef cell, CellRef next, int perMille) =>
            new PawnView(new PawnId(id), cell, 100, 100, 50, -1, next, 0, movePerMille: perMille);

        static Vector3 Pose(in PawnView pawn, PawnView[] everyone, PawnCrowdIndex? index,
            out Vector3 steer) =>
            PawnPose.Of(in pawn, 0f, 0, out _, null, everyone, index, out steer);

        static Vector3 PoseIndexed(in PawnView pawn, PawnView[] everyone, out Vector3 steer)
        {
            var index = new PawnCrowdIndex();
            index.Rebuild(everyone);
            return Pose(in pawn, everyone, index, out steer);
        }

        static float Apart(Vector3 a, Vector3 b) =>
            new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static void EveryPairApart(PawnView[] everyone, float atLeast)
        {
            var drawn = new Vector3[everyone.Length];
            for (int i = 0; i < everyone.Length; i++)
                drawn[i] = PoseIndexed(in everyone[i], everyone, out _);

            for (int i = 0; i < drawn.Length; i++)
            for (int j = i + 1; j < drawn.Length; j++)
                Assert.That(Apart(drawn[i], drawn[j]), Is.GreaterThanOrEqualTo(atLeast),
                    $"pawns {everyone[i].Id} and {everyone[j].Id} of {everyone.Length} are drawn " +
                    $"{Apart(drawn[i], drawn[j]):F3} m apart");
        }

        [Test]
        public void TwoStandingOnOneCellAreDrawnApart() =>
            EveryPairApart(new[] { Standing(1, Fireside), Standing(2, Fireside) },
                PawnPose.StandApartSpacing - 1e-4f);

        [Test]
        public void ThreeStandingOnOneCellAreDrawnApart() =>
            EveryPairApart(new[] { Standing(7, Fireside), Standing(3, Fireside), Standing(5, Fireside) },
                PawnPose.StandApartSpacing - 1e-4f);

        /// <summary>
        /// However many crowd on to one cell, nobody coincides and nobody is drawn off it. At
        /// twenty the ring is at its cap and the spacing is less than a shoulder, which is a crowd
        /// too big for a cell rather than a reason to put two people at one point.
        /// </summary>
        [Test]
        public void ACrowdOnOneCellStaysDistinctAndInsideTheCell()
        {
            var crowd = new PawnView[20];
            for (int i = 0; i < crowd.Length; i++) crowd[i] = Standing(i + 1, Fireside);

            EveryPairApart(crowd, 0.2f);

            Vector3 centre = CellMetrics.FloorCentre(Fireside);
            for (int i = 0; i < crowd.Length; i++)
            {
                Vector3 at = PoseIndexed(in crowd[i], crowd, out _);
                Assert.That(Apart(at, centre), Is.LessThanOrEqualTo(PawnPose.StandApartMaxRadius + 1e-4f));
            }
        }

        /// <summary>
        /// The common case moves nothing: a pawn alone on its cell is drawn to the bit where it
        /// was drawn before this rule existed, with neighbours standing next door, somebody
        /// walking through its own cell, and somebody asleep on it.
        /// </summary>
        [Test]
        public void APawnAloneOnItsCellIsDrawnExactlyWhereItWas()
        {
            PawnView me = Standing(1, Fireside);
            var everyone = new[]
            {
                me,
                Standing(2, new CellRef(5, 4, 0)),
                Standing(3, new CellRef(4, 5, 0)),
                Standing(4, new CellRef(4, 4, 1)),
                Walking(5, Fireside, new CellRef(5, 4, 0), 300),
                Standing(6, Fireside, asleep: true),
            };

            Vector3 before = PawnPose.Of(in me, 0f, 0, out _);
            foreach (CrowdScan mode in new[] { CrowdScan.Span, CrowdScan.Cached, CrowdScan.Bucketed })
            {
                PawnCrowdIndex.Mode = mode;
                Vector3 after = PoseIndexed(in me, everyone, out Vector3 steer);
                Assert.That(steer, Is.EqualTo(Vector3.zero), $"{mode}: a lone pawn was steered");
                Assert.That(after.x, Is.EqualTo(before.x), $"{mode}: x");
                Assert.That(after.y, Is.EqualTo(before.y), $"{mode}: y");
                Assert.That(after.z, Is.EqualTo(before.z), $"{mode}: z");
            }
        }

        /// <summary>A sleeper is laid by the bed or the ground and is not moved by this.</summary>
        [Test]
        public void ASleeperIsNotMovedBySomebodyStandingOnItsCell()
        {
            PawnView sleeper = Standing(1, Fireside, asleep: true);
            var everyone = new[] { sleeper, Standing(2, Fireside), Standing(3, Fireside) };

            PoseIndexed(in sleeper, everyone, out Vector3 steer);
            Assert.That(steer, Is.EqualTo(Vector3.zero));
        }

        /// <summary>
        /// The same answer every frame, whatever order the snapshot lists the pawns in, and
        /// whichever scan found them — compared exactly.
        /// </summary>
        [Test]
        public void TheSpreadIsTheSameEveryFrameInAnyOrderAndAnyScan()
        {
            var forward = new[] { Standing(2, Fireside), Standing(9, Fireside), Standing(4, Fireside) };
            var backward = new[] { forward[2], forward[1], forward[0] };

            for (int i = 0; i < forward.Length; i++)
            {
                PawnCrowdIndex.Mode = CrowdScan.Bucketed;
                Vector3 first = PoseIndexed(in forward[i], forward, out _);
                Vector3 again = PoseIndexed(in forward[i], forward, out _);
                Vector3 reversed = PoseIndexed(in forward[i], backward, out _);
                PawnCrowdIndex.Mode = CrowdScan.Span;
                Vector3 scanned = Pose(in forward[i], forward, null, out _);

                foreach (Vector3 other in new[] { again, reversed, scanned })
                {
                    Assert.That(other.x, Is.EqualTo(first.x), $"pawn {forward[i].Id}: x");
                    Assert.That(other.y, Is.EqualTo(first.y), $"pawn {forward[i].Id}: y");
                    Assert.That(other.z, Is.EqualTo(first.z), $"pawn {forward[i].Id}: z");
                }
            }
        }

        /// <summary>
        /// A newcomer does not move the lowest id round the ring: she keeps her bearing from the
        /// centre and only steps out as the ring widens. Without this, every arrival would
        /// reshuffle the colonists already standing there.
        /// </summary>
        [Test]
        public void AnArrivalDoesNotTurnTheFirstPlaceRoundTheRing()
        {
            PawnView first = Standing(1, Fireside);
            var two = new[] { first, Standing(5, Fireside) };
            var three = new[] { first, Standing(5, Fireside), Standing(3, Fireside) };

            PoseIndexed(in first, two, out Vector3 before);
            PoseIndexed(in first, three, out Vector3 after);

            Assert.That(before.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(Vector3.Angle(before, after), Is.LessThan(0.01f),
                "the first place turned when a third colonist arrived");
            Assert.That(after.magnitude, Is.GreaterThanOrEqualTo(before.magnitude - 1e-5f));
        }

        /// <summary>
        /// Two sitting at one fire from one cell sit abreast of it, both facing it, rather than
        /// one behind the other — the campfire the owner reported.
        /// </summary>
        [Test]
        public void TwoAtOneFireSitAbreastOfIt()
        {
            var fire = new CellRef(Fireside.X + 1, Fireside.Z, Fireside.Y);
            var everyone = new[]
            {
                Standing(1, Fireside, seated: true, workCell: fire),
                Standing(2, Fireside, seated: true, workCell: fire),
            };

            Vector3 toFire = CellMetrics.FloorCentre(fire) - CellMetrics.FloorCentre(Fireside);
            toFire.y = 0f;
            for (int i = 0; i < everyone.Length; i++)
            {
                PoseIndexed(in everyone[i], everyone, out Vector3 steer);
                Assert.That(steer.magnitude, Is.EqualTo(PawnPose.StandApartSpacing * 0.5f).Within(1e-4f));
                Assert.That(Mathf.Abs(Vector3.Dot(steer.normalized, toFire.normalized)), Is.LessThan(1e-3f),
                    $"pawn {everyone[i].Id} is in front of or behind the other rather than beside");
            }
        }

        /// <summary>
        /// The index's single-bucket walk finds everybody on the cell and nobody else: every pawn
        /// standing on a cell has the same cached position, so they share one bucket.
        /// </summary>
        [Test]
        public void HereVisitsTheOwnBucketOnly()
        {
            var everyone = new[]
            {
                Standing(1, Fireside), Standing(2, Fireside),
                Standing(3, new CellRef(Fireside.X + 3, Fireside.Z, Fireside.Y)),
            };
            var index = new PawnCrowdIndex();
            index.Rebuild(everyone);

            int seen = 0;
            bool sawFarOne = false;
            foreach (int i in index.Here(CellMetrics.FloorCentre(Fireside)))
            {
                seen++;
                if (i == 2) sawFarOne = true;
            }

            Assert.That(sawFarOne, Is.False, "a pawn three cells away was in the same bucket");
            Assert.That(seen, Is.EqualTo(2));
        }

        [Test]
        public void TheRingWidensWithTheCrowdAndClearsATrunk()
        {
            Assert.That(PawnPose.StandApartRadius(1, false), Is.EqualTo(0f));
            Assert.That(PawnPose.StandApartRadius(2, false), Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(PawnPose.StandApartRadius(3, false), Is.GreaterThan(PawnPose.StandApartRadius(2, false)));
            Assert.That(PawnPose.StandApartRadius(2, true), Is.GreaterThanOrEqualTo(SteeringCurve.MaxLateralOffset));
            Assert.That(PawnPose.StandApartRadius(50, true), Is.EqualTo(PawnPose.StandApartMaxRadius));
        }
    }
}
