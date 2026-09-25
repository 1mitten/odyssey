#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Drafted colonists sent up to another floor (design 33 §19b; owner, 2026-09-24: <i>"it seemed
    /// tricky to draft then move my colonists to another floor in the building - just double check
    /// that"</i>).
    ///
    /// <para>Measured in the owner's own save: with the squad selected and a right-click on the upper
    /// floor of the house, 49 of 160 orders (four colonists, forty upstairs cells) named a cell on
    /// another layer. The first colonist goes where the click was; the others are spread to free
    /// cells round it (<c>JobSystem.Spread</c>, design 33 §8c), and the spread lifted each ring cell
    /// by the <b>click's</b> rule — that cell, else the one above, else the one below. The ladder's
    /// open shaft and the air past the edge of the upper floor are not places to stand, so the
    /// rule dropped a layer and sent those colonists to the room below, or out on to the ground
    /// beside the house. The click's rule is for a click, which names a block a colonist stands on
    /// top of; a spread already knows which floor it is on.</para>
    /// </summary>
    public class DraftOrderLevelTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(int colonists)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
        }

        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static void RaiseNow(ColonyWorld colony, int cell, int building)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None), $"the order for a {building} at {Size.FromIndex(cell)} was refused");
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        /// <summary>
        /// A storey up: a three-by-three block of walls with a floor on each, and a ladder beside its
        /// west side whose shaft opens on to the floor. Returns the upper floor's nine cells and the
        /// layer it is on; the colonists stand on the ground to the west.
        /// </summary>
        static (ColonyWorld colony, List<Pawn> squad, List<int> upstairs, int upper, int corner) AStoreyUpALadder(int colonists)
        {
            var colony = Board(colonists);
            colony.World.Tick();
            CellRef s = colony.Start;
            int y = s.Y, x0 = s.X + 6, z0 = s.Z;

            var upstairs = new List<int>();
            for (int dz = 0; dz < 3; dz++)
            for (int dx = 0; dx < 3; dx++)
                RaiseNow(colony, Size.Index(x0 + dx, z0 + dz, y), BuildingHandle.Wall);
            colony.World.Tick();
            for (int dz = 0; dz < 3; dz++)
            for (int dx = 0; dx < 3; dx++)
            {
                int floor = Size.Index(x0 + dx, z0 + dz, y + 1);
                RaiseNow(colony, floor, BuildingHandle.Floor);
                upstairs.Add(floor);
            }
            colony.World.Tick();
            RaiseNow(colony, Size.Index(x0 - 1, z0 + 1, y), BuildingHandle.Ladder);
            colony.World.Tick();

            var squad = new List<Pawn>();
            var pawns = colony.Pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                CombatFixture.Stand(colony, p, colony.Pawns.Cells.NearestWalkableInColumn(s.X, s.Z + i, y));
                Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, p.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
                squad.Add(p);
            }

            int corner = Size.Index(x0, z0, y + 1);
            Assert.That(colony.Pawns.Reachable(squad[0], corner, TraverseMode.Colonist), Is.True,
                "the control: the upper floor can be reached, up the ladder");
            return (colony, squad, upstairs, y + 1, corner);
        }

        /// <summary>Where the order sent her: the cell she is walking to, or the one she holds.</summary>
        static int SentTo(Pawn pawn) =>
            pawn.CurrentJob is { DefIndex: JobIndex.Goto } job ? job.TargetCell : pawn.Cell;

        /// <summary>
        /// The squad sent to the corner of the upper floor, beside the ladder's shaft and the edge:
        /// every one is sent to a cell on that floor, and every one gets there. Before the fix the
        /// second was spread to the ground past the floor's edge, a layer down.
        /// </summary>
        [Test]
        public void ASquadSentUpstairsIsSpreadOnTheFloorItWasSentTo()
        {
            var (colony, squad, upstairs, upper, corner) = AStoreyUpALadder(3);

            // One right-click with the squad selected: an order each, to the one cell (OrderModel.RightClick).
            colony.World.Intents.ClearRejected();
            foreach (Pawn p in squad) colony.World.Intents.Submit(new Intent(IntentKind.OrderMove, Size.FromIndex(corner), p.Id.Value));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected, Is.Empty, "an order up the ladder was refused");

            var sent = new HashSet<int>();
            foreach (Pawn p in squad)
            {
                int to = SentTo(p);
                Assert.That(Size.FromIndex(to).Y, Is.EqualTo(upper),
                    $"colonist {p.Id.Value} was sent to {Size.FromIndex(to)}, off the floor the click named");
                Assert.That(upstairs, Has.Member(to), $"colonist {p.Id.Value} was sent off the upper floor");
                sent.Add(to);
            }
            Assert.That(sent.Count, Is.EqualTo(squad.Count), "two were sent to one cell");

            for (int t = 0; t < 4_000; t++)
            {
                bool all = true;
                foreach (Pawn p in squad) if (p.CurrentJob?.DefIndex != JobIndex.DraftHold) all = false;
                if (all) break;
                colony.World.Tick();
            }
            foreach (Pawn p in squad)
            {
                Assert.That(p.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), $"colonist {p.Id.Value} never got there");
                Assert.That(upstairs, Has.Member(p.Cell), $"colonist {p.Id.Value} holds at {Size.FromIndex(p.Cell)}, not upstairs");
            }
        }

        /// <summary>
        /// The control on the rule the fix leaves alone: the click itself is still lifted by the
        /// click's rule. A click that names the wall under the floor — the block the player pointed
        /// at — sends her to the floor on top of it, one layer up.
        /// </summary>
        [Test]
        public void AClickOnTheWallUnderTheFloorStillSendsHerOnToIt()
        {
            var (colony, squad, upstairs, upper, corner) = AStoreyUpALadder(1);
            int wall = corner - Size.LayerStride;
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(wall), squad[0].Id.Value)), Is.EqualTo(IntentRejection.None));
            Assert.That(SentTo(squad[0]), Is.EqualTo(corner), "a click on the block did not send her on to it");
            _ = upstairs;
            _ = upper;
        }
    }
}
