#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// How broken a thing looks (design 57): three stages at a quarter, a half and three quarters
    /// gone, one rule for a struck wall and for rock being mined.
    /// </summary>
    public class CrackModelTests
    {
        [Test]
        public void TheStagesStartAtAQuarterAHalfAndThreeQuartersGone()
        {
            Assert.That(CrackModel.StageOf(0), Is.Zero);
            Assert.That(CrackModel.StageOf(249), Is.Zero, "intact until a quarter is gone");
            Assert.That(CrackModel.StageOf(250), Is.EqualTo(1));
            Assert.That(CrackModel.StageOf(499), Is.EqualTo(1));
            Assert.That(CrackModel.StageOf(500), Is.EqualTo(2));
            Assert.That(CrackModel.StageOf(749), Is.EqualTo(2));
            Assert.That(CrackModel.StageOf(750), Is.EqualTo(3));
            Assert.That(CrackModel.StageOf(1000), Is.EqualTo(3));
            Assert.That(CrackModel.StageOf(1500), Is.EqualTo(3), "past the end is still the last stage");
        }

        [Test]
        public void AWallAndASandbagCrackAtTheSameShareOfTheirPool()
        {
            // 300-point wall and 55-point sandbag, each at 40 % left: one stage for both.
            Assert.That(CrackModel.StageOf(CrackModel.BrokenOfHitPoints(120_000, 300_000)), Is.EqualTo(2));
            Assert.That(CrackModel.StageOf(CrackModel.BrokenOfHitPoints(22_000, 55_000)), Is.EqualTo(2));

            Assert.That(CrackModel.BrokenOfHitPoints(300_000, 300_000), Is.Zero, "whole");
            Assert.That(CrackModel.BrokenOfHitPoints(-3_000, 160_000), Is.EqualTo(1000), "coming down this tick");
            Assert.That(CrackModel.BrokenOfHitPoints(1, 0), Is.Zero, "no pool, nothing to divide by");
        }

        [Test]
        public void ACutsProgressByteIsItsShareGone()
        {
            Assert.That(CrackModel.BrokenOfProgress(0), Is.Zero);
            Assert.That(CrackModel.BrokenOfProgress(255), Is.EqualTo(1000));
            Assert.That(CrackModel.StageOf(CrackModel.BrokenOfProgress(64)), Is.EqualTo(1), "a quarter through");
            Assert.That(CrackModel.StageOf(CrackModel.BrokenOfProgress(63)), Is.Zero, "just short of a quarter");
        }

        [Test]
        public void RockClimbsSixLevelsAndAWallsStagesAreEveryOtherRung()
        {
            Assert.That(CrackModel.RockLevelOf(99), Is.Zero, "untouched until a tenth");
            Assert.That(CrackModel.RockLevelOf(100), Is.EqualTo(1));
            Assert.That(CrackModel.RockLevelOf(250), Is.EqualTo(2));
            Assert.That(CrackModel.RockLevelOf(549), Is.EqualTo(3));
            Assert.That(CrackModel.RockLevelOf(550), Is.EqualTo(4));
            Assert.That(CrackModel.RockLevelOf(849), Is.EqualTo(5));
            Assert.That(CrackModel.RockLevelOf(850), Is.EqualTo(CrackModel.Levels));
            Assert.That(CrackModel.RockLevelOf(1000), Is.EqualTo(CrackModel.Levels));

            Assert.That(CrackModel.LevelOfStage(1), Is.EqualTo(2));
            Assert.That(CrackModel.LevelOfStage(2), Is.EqualTo(4));
            Assert.That(CrackModel.LevelOfStage(3), Is.EqualTo(CrackModel.Levels), "a crumbling wall is the top rung");
        }

        [Test]
        public void GatherListsStruckWallsAndCutRockAndNothingElse()
        {
            WorldSnapshot frame = Frame.Write();
            GridSize size = frame.Size;
            int wall = size.Index(new CellRef(2, 2, 1));
            int scratched = size.Index(new CellRef(3, 2, 1));
            int door = size.Index(new CellRef(4, 2, 1));
            int mining = size.Index(new CellRef(5, 2, 0));
            int felling = size.Index(new CellRef(6, 2, 1));
            int left = size.Index(new CellRef(7, 2, 0));
            int untouched = size.Index(new CellRef(8, 2, 0));

            frame.AddEdificeDamage(new EdificeDamageView(wall, EdificeHandle.Wall, 100_000, 300_000));
            frame.AddEdificeDamage(new EdificeDamageView(scratched, EdificeHandle.Wall, 290_000, 300_000));
            frame.AddEdificeDamage(new EdificeDamageView(door, EdificeHandle.Door, 10_000, 160_000));
            frame.AddOrder(new OrderView(mining, CrackModel.MineOrderKind, 200));
            frame.AddOrder(new OrderView(untouched, CrackModel.MineOrderKind, 0));
            frame.AddOrder(new OrderView(felling, 3, 250));
            frame.AddPartMined(new PartMinedView(left, 140));

            var cells = new List<CrackedCell>();
            Assert.That(CrackModel.Gather(frame, cells, 0, 3), Is.EqualTo(3));

            Assert.That(cells[0].CellIndex, Is.EqualTo(wall));
            Assert.That(cells[0].Level, Is.EqualTo(CrackModel.LevelOfStage(2)), "two thirds gone");
            Assert.That(cells[0].Ground, Is.False);
            Assert.That(cells[1].CellIndex, Is.EqualTo(mining));
            Assert.That(cells[1].Level, Is.EqualTo(5), "200 of 255 is 784 thousandths");
            Assert.That(cells[1].Ground, Is.True);
            Assert.That(cells[2].CellIndex, Is.EqualTo(left));
            Assert.That(cells[2].Level, Is.EqualTo(3), "140 of 255 is 549 thousandths; the cut kept after a cancel still shows");
            Assert.That(cells[2].Ground, Is.True);

            // The controls, by name: a scratch under a quarter, a door (walls only for now), a
            // felling order and an untouched mining order are all absent.
            var listed = new List<int>();
            foreach (CrackedCell c in cells) listed.Add(c.CellIndex);
            Assert.That(listed, Has.No.Member(scratched).And.No.Member(door).And.No.Member(felling).And.No.Member(untouched));
        }

        [Test]
        public void GatherKeepsToTheDrawnLayers()
        {
            WorldSnapshot frame = Frame.Write();
            GridSize size = frame.Size;
            frame.AddEdificeDamage(new EdificeDamageView(size.Index(new CellRef(2, 2, 3)), EdificeHandle.Wall, 0, 300_000));
            frame.AddOrder(new OrderView(size.Index(new CellRef(3, 3, 0)), CrackModel.MineOrderKind, 255));

            var cells = new List<CrackedCell>();
            Assert.That(CrackModel.Gather(frame, cells, 1, 2), Is.Zero);
            Assert.That(CrackModel.Gather(frame, cells, 0, 3), Is.EqualTo(2), "the control");
        }
    }
}
