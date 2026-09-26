#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// When a thing coming down makes a sound (design 58 §9): rock under a mining order that is no
    /// longer solid, wood taken apart or broken — and silence for everything that was only
    /// cancelled, repaired, left part-done, or built of something with no sound yet.
    /// </summary>
    public class DemolitionWatchTests
    {
        const ushort Wood = 4;
        const ushort Stone = 5;
        const ushort WallDef = 7;
        const ushort FloorDef = 3;

        sealed class Cells : IDemolitionCells
        {
            public readonly HashSet<int> Solid = new HashSet<int>();
            public readonly Dictionary<int, (ushort def, ushort stuff)> Edifices = new Dictionary<int, (ushort, ushort)>();
            public readonly Dictionary<int, (ushort def, ushort stuff)> Floors = new Dictionary<int, (ushort, ushort)>();

            public bool IsSolid(int cell) => Solid.Contains(cell);
            public ushort EdificeDef(int cell) => Edifices.TryGetValue(cell, out var e) ? e.def : (ushort)0;
            public ushort EdificeStuff(int cell) => Edifices.TryGetValue(cell, out var e) ? e.stuff : (ushort)0;
            public ushort Floor(int cell) => Floors.TryGetValue(cell, out var f) ? f.def : (ushort)0;
            public ushort FloorStuff(int cell) => Floors.TryGetValue(cell, out var f) ? f.stuff : (ushort)0;
        }

        static WorldSnapshot Orders(params (int cell, byte kind)[] orders)
        {
            WorldSnapshot frame = Frame.Write();
            foreach ((int cell, byte kind) in orders) frame.AddOrder(new OrderView(cell, kind, 100));
            return frame;
        }

        static WorldSnapshot Struck(params int[] cells)
        {
            WorldSnapshot frame = Frame.Write();
            foreach (int cell in cells) frame.AddEdificeDamage(new EdificeDamageView(cell, EdificeHandle.Wall, 10_000, 300_000));
            return frame;
        }

        [Test]
        public void AMinedFaceThatGoesSoundsAsRock()
        {
            var cells = new Cells();
            cells.Solid.Add(12);
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            Assert.That(watch.Step(Orders((12, DemolitionWatch.MineOrderKind)), cells, heard), Is.Zero);
            cells.Solid.Remove(12);
            Assert.That(watch.Step(Orders(), cells, heard), Is.EqualTo(1));
            Assert.That(heard[0].CellIndex, Is.EqualTo(12));
            Assert.That(heard[0].Kind, Is.EqualTo(Demolition.Rock));
            Assert.That(watch.Tracking, Is.Zero);
        }

        [Test]
        public void TheMirrorALittleBehindTheOrderIsWaitedFor()
        {
            var cells = new Cells();
            cells.Solid.Add(12);
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            watch.Step(Orders((12, DemolitionWatch.MineOrderKind)), cells, heard);
            Assert.That(watch.Step(Orders(), cells, heard), Is.Zero, "the order went a publish before the rock");
            Assert.That(watch.Step(Orders(), cells, heard), Is.Zero);
            cells.Solid.Remove(12);
            Assert.That(watch.Step(Orders(), cells, heard), Is.EqualTo(1), "the rock went and nothing was heard");
        }

        [Test]
        public void ACancelledOrderIsLetGoInSilence()
        {
            var cells = new Cells();
            cells.Solid.Add(12);
            cells.Edifices[20] = (WallDef, Wood);
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            watch.Step(Orders((12, DemolitionWatch.MineOrderKind), (20, DemolitionWatch.DeconstructOrderKind)), cells, heard);
            int total = 0;
            for (int i = 0; i < DemolitionWatch.WatchFrames; i++) total += watch.Step(Orders(), cells, heard);

            Assert.That(total, Is.Zero, "a cancelled order sounded as if it had been carried out");
            Assert.That(watch.Tracking, Is.Zero, "the watch never let go");
        }

        [Test]
        public void WoodTakenApartOrBrokenSoundsAsWoodAndStoneIsSilent()
        {
            var cells = new Cells();
            cells.Edifices[20] = (WallDef, Wood);     // deconstructed
            cells.Edifices[21] = (WallDef, Wood);     // broken in a fight
            cells.Edifices[22] = (WallDef, Stone);    // deconstructed, no sound yet
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            WorldSnapshot first = Orders((20, DemolitionWatch.DeconstructOrderKind), (22, DemolitionWatch.DeconstructOrderKind));
            first.AddEdificeDamage(new EdificeDamageView(21, EdificeHandle.Wall, 10_000, 300_000));
            watch.Step(first, cells, heard);

            cells.Edifices.Remove(20);
            cells.Edifices.Remove(21);
            cells.Edifices.Remove(22);
            Assert.That(watch.Step(Orders(), cells, heard), Is.EqualTo(2));
            var sounded = new List<int>();
            foreach (Demolished d in heard)
            {
                Assert.That(d.Kind, Is.EqualTo(Demolition.Wood));
                sounded.Add(d.CellIndex);
            }
            Assert.That(sounded, Is.EquivalentTo(new[] { 20, 21 }));
            Assert.That(watch.Tracking, Is.Zero, "the stone wall came down and was never let go");
        }

        [Test]
        public void AWoodFloorTakenApartSoundsAsWood()
        {
            var cells = new Cells();
            cells.Floors[30] = (FloorDef, Wood);
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            watch.Step(Orders((30, DemolitionWatch.DeconstructOrderKind)), cells, heard);
            cells.Floors.Remove(30);
            Assert.That(watch.Step(Orders(), cells, heard), Is.EqualTo(1));
            Assert.That(heard[0].Kind, Is.EqualTo(Demolition.Wood));
        }

        static int eventId;

        static WorldSnapshot Broken(int cell)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddCombatEvent(new CombatEventView(++eventId, 10, CombatEventKind.Demolished, new PawnId(1),
                PawnId.None, frame.Size.FromIndex(cell), EdificeHandle.Wall));
            return frame;
        }

        [Test]
        public void AWoodWallBrokenInOneBlowIsHeardFromTheFightAndTheMirrorsNote()
        {
            var cells = new Cells();
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();
            watch.Step(Orders(), cells, heard);    // armed

            // Never struck before, so never tracked: only the event and the note know.
            watch.NoteRemoved(40, Wood);
            Assert.That(watch.Step(Broken(40), cells, heard), Is.EqualTo(1));
            Assert.That(heard[0].CellIndex, Is.EqualTo(40));
            Assert.That(heard[0].Kind, Is.EqualTo(Demolition.Wood));

            // The other way round: the event a frame ahead of the mirror.
            Assert.That(watch.Step(Broken(41), cells, heard), Is.Zero);
            watch.NoteRemoved(41, Wood);
            Assert.That(watch.Step(Orders(), cells, heard), Is.EqualTo(1), "the note arrived a frame late and was not matched");

            // And stone, broken in one blow, is silent and let go.
            watch.NoteRemoved(42, Stone);
            Assert.That(watch.Step(Broken(42), cells, heard), Is.Zero);
            Assert.That(watch.Tracking, Is.Zero);
        }

        [Test]
        public void AWallStruckAndThenBrokenIsHeardOnce()
        {
            var cells = new Cells();
            cells.Edifices[21] = (WallDef, Wood);
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            watch.Step(Struck(21), cells, heard);
            cells.Edifices.Remove(21);
            watch.NoteRemoved(21, Wood);
            int total = watch.Step(Broken(21), cells, heard);
            for (int i = 0; i < 5; i++) total += watch.Step(Orders(), cells, heard);

            Assert.That(total, Is.EqualTo(1), "tracked and reported, and heard twice");
        }

        [Test]
        public void ALoadedWorldsOldFightsAreNotHeard()
        {
            var cells = new Cells();
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            watch.NoteRemoved(40, Wood);
            Assert.That(watch.Step(Broken(40), cells, heard), Is.Zero, "the first frame's events are history");
        }

        [Test]
        public void ARepairedWallIsSilentAndWhatStoodIsWhatWasFirstSeen()
        {
            var cells = new Cells();
            cells.Edifices[21] = (WallDef, Wood);
            var watch = new DemolitionWatch(Wood);
            var heard = new List<Demolished>();

            watch.Step(Struck(21), cells, heard);
            Assert.That(watch.Step(Orders(), cells, heard), Is.Zero, "repaired: the row went and the wall stands");

            // Listed again, and the mirror already has it gone while still listed: what stood is
            // what was written down the first time, so it still sounds when the row goes.
            watch.Step(Struck(21), cells, heard);
            cells.Edifices.Remove(21);
            watch.Step(Struck(21), cells, heard);
            Assert.That(watch.Step(Orders(), cells, heard), Is.EqualTo(1));
        }
    }
}
