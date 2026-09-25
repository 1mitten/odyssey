#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using static Odyssey.Tests.Sim.CombatFixture;
using static Odyssey.Tests.Sim.HomeFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Design 43 §4e: a colonist kept home who is outside it walks back from wherever she can get
    /// home from — not only from the layers beside it. Home reaches one layer above and below what
    /// was built, so a colonist two layers down a quarry has no home cell on her own layer or
    /// either neighbour; searching only those left her standing at the bottom for good, because
    /// everything else she might do is gated too.
    /// </summary>
    public class HomeWalkBackTests
    {
        /// <summary>
        /// A bench staircase dug twenty cells east of the start, <paramref name="depth"/> deep: column
        /// <c>i</c> is dug <c>i</c> down, so each step is a one-block hop. Answers the bottom cell.
        /// </summary>
        static int Quarry(ColonyWorld colony, int depth)
        {
            CellRef s = colony.Start;
            for (int i = 1; i <= depth; i++)
                for (int d = 1; d <= i; d++)
                    MineJobDriver.MineCell(colony.Pawns, Size.Index(s.X + 19 + i, s.Z, s.Y - d), 0);
            colony.World.Tick();
            int bottom = Size.Index(s.X + 19 + depth, s.Z, s.Y - depth);
            Assume.That(colony.Pawns.Cells.IsWalkable(bottom), Is.True, "the quarry's floor is not standable");
            return bottom;
        }

        static void Run(ColonyWorld colony, Pawn pawn, int cap)
        {
            HomeArea home = colony.Pawns.Home!;
            for (int t = 0; t < cap && !home.Contains(pawn.Cell); t++)
            {
                pawn.BreakTicksLeft = 0;
                for (int n = 0; n < NeedIndex.Count; n++) pawn.Needs[n] = 800;
                colony.World.Tick();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void KeptHomeAtTheBottomOfAQuarrySheClimbsOutAndWalksBack(int depth)
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Campfire(colony, Size.FromIndex(Near(colony, -3, 4)));
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int bottom = Quarry(colony, depth);
            HomeArea home = colony.Pawns.Home!;
            Assume.That(home.Contains(bottom), Is.False, "the quarry is inside home");

            Send(colony, new Intent(IntentKind.SetPawnArea, default, pawn.Id.Value, (int)PawnArea.Home));
            Stand(colony, pawn, bottom);
            // Twenty-odd cells at about a hundred ticks a cell, and a hop or three.
            Run(colony, pawn, 5_000);
            Assert.That(home.Contains(pawn.Cell), Is.True,
                $"kept home at the bottom of a quarry {depth} deep, she never walked back");
        }

        [Test]
        public void AtAnywhereSheStaysAtTheBottom()
        {
            // Control: the same quarry, the same colonist, not kept home.
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Campfire(colony, Size.FromIndex(Near(colony, -3, 4)));
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int bottom = Quarry(colony, 2);
            Stand(colony, pawn, bottom);
            Run(colony, pawn, 3_000);
            Assert.That(colony.Pawns.Home!.Contains(pawn.Cell), Is.False,
                "at Anywhere she walked home anyway: the test above proves nothing");
        }
    }
}
