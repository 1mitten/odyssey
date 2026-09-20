#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Storage;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <b>Everything the player has told the colony to do survives a save.</b>
    ///
    /// <para>Written to an owner ask (2026-09-20: <i>"make sure saved games save orders assigned
    /// as well"</i>), and deliberately as one file per <i>kind</i> of order rather than as one
    /// more hash comparison. <c>WorldRoundTripTests.TheRoundTripReproducesTheStateExactly</c>
    /// already asserts that the state hash comes back, and that is a strong test of exactly the
    /// things the hash covers and no test at all of the things it does not — which is how this
    /// project lost the whole world out of its hash for a year (OQ-50). So each order is
    /// round-tripped and then <b>read back by name</b>: the designation is still on the cell, the
    /// blueprint still knows its material, the bed still knows whose it is, the zone still takes
    /// what it took.</para>
    ///
    /// <para>The negative control is <see cref="EachOrderMovesTheStateHash"/>: an order that the
    /// hash cannot see is an order the round-trip test above cannot vouch for, and it found two
    /// on the day this file was written — the stockpile zones and the bed list, both saved since
    /// they existed and neither hashed.</para>
    /// </summary>
    public class OrdersSurviveASaveTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);
        const uint Seed = 20260920;

        static ColonyWorld Fresh()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            return ColonyWorld.Build(Size, Seed, scenario);
        }

        static ColonyWorld RoundTrip(ColonyWorld colony)
        {
            ColonyWorld restored = Fresh();
            restored.Load(colony.Save());
            return restored;
        }

        /// <summary>A walkable cell near the start that nothing else has claimed.</summary>
        static int OpenCell(ColonyWorld colony, HashSet<int> taken)
        {
            CellRef start = colony.Start;
            for (int radius = 1; radius < 12; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;
                int x = start.X + dx, z = start.Z + dz;
                if (!Size.Contains(x, z, start.Y)) continue;

                int cell = Size.Index(x, z, start.Y);
                if (!taken.Add(cell)) continue;
                if (!colony.Construction.Allows(cell, BuildingHandle.Wall)) continue;
                return cell;
            }

            return -1;
        }

        /// <summary>A cell of solid rock, for a mine order to stand on.</summary>
        static int SolidCell(ColonyWorld colony)
        {
            for (int i = 0; i < Size.CellCount; i++)
                if (colony.Grid.IsSolidTerrain(i) && colony.Designations.Allows(i, DesignationKind.Mine))
                    return i;
            return -1;
        }

        // ---- the standing orders ---------------------------------------------------------------

        /// <summary>
        /// A mine order, a deconstruct order and the work already cut out of the face all come
        /// back. The half-cut matters as much as the order: a save that restored the designation
        /// and dropped its progress would hand the player back a morning's digging.
        /// </summary>
        [Test]
        public void ADesignationAndItsProgressComeBack()
        {
            ColonyWorld colony = Fresh();
            int rock = SolidCell(colony);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "no rock on this board to mine");

            Assert.That(colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine),
                Is.EqualTo(IntentRejection.None));
            colony.Designations.AddWork(rock, 250 * Rates.Scale);
            int work = colony.Designations.WorkDone(rock);
            Assume.That(work, Is.GreaterThan(0));

            ColonyWorld restored = RoundTrip(colony);
            Assert.That(restored.Designations.At(rock), Is.EqualTo(DesignationKind.Mine),
                "the order the player gave is gone");
            Assert.That(restored.Designations.WorkDone(rock), Is.EqualTo(work),
                "the order came back and the digging already done did not");
        }

        /// <summary>
        /// A blueprint comes back knowing what it is, what it is made of, which way it faces and
        /// how much wood has already been carried to it.
        /// </summary>
        [Test]
        public void ABlueprintComesBackWithItsMaterialAndItsDeliveries()
        {
            ColonyWorld colony = Fresh();
            var taken = new HashSet<int>();
            int cell = OpenCell(colony, taken);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));

            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Stone),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(cell, 2);
            colony.Construction.AddWork(cell, 40 * Rates.Scale);

            int delivered = colony.Construction.Delivered(cell);
            int work = colony.Construction.WorkDone(cell);
            Assume.That(delivered, Is.GreaterThan(0));

            ColonyWorld restored = RoundTrip(colony);
            Assert.That(restored.Construction.SiteAt(Size.FromIndex(cell)), Is.EqualTo(cell),
                "the blueprint is gone");
            Assert.That(restored.Construction.At(cell), Is.EqualTo(BuildingHandle.Wall));
            Assert.That(restored.Construction.StuffAt(cell), Is.EqualTo(StuffHandle.Stone),
                "it came back as a wall of the wrong stuff");
            Assert.That(restored.Construction.Delivered(cell), Is.EqualTo(delivered),
                "the wood carried to it was lost");
            Assert.That(restored.Construction.WorkDone(cell), Is.EqualTo(work));
        }

        /// <summary>
        /// Who a bed belongs to is an order the player gave, and it comes back — together with
        /// the bed itself, which is one record behind two cells either side of the save.
        /// </summary>
        [Test]
        public void ABedsOwnerComesBack()
        {
            ColonyWorld colony = Fresh();
            Assume.That(colony.Pawns.Items.Beds.Count, Is.GreaterThan(0), "the colony started with no beds");

            int bed = colony.Pawns.Items.Beds[0];
            var pawn = colony.Pawns.Pawns.All[0];
            colony.World.Intents.Submit(new Intent(IntentKind.AssignBedOwner, Size.FromIndex(bed), pawn.Id.Value));
            colony.World.Tick();
            Assume.That(colony.Construction.BedOwnerAt(bed), Is.EqualTo(pawn.Id.Value));

            ColonyWorld restored = RoundTrip(colony);
            Assert.That(restored.Construction.BedOwnerAt(bed), Is.EqualTo(pawn.Id.Value),
                "the colonist woke up in somebody else's colony");
            Assert.That(restored.Pawns.Items.Beds, Does.Contain(bed),
                "the bed is still owned and the sleep chooser can no longer find it");
        }

        /// <summary>
        /// A stockpile is an order about where things go: its cells, its priority and — the one
        /// most easily lost, because it is the only part that is not a position — its filter.
        /// </summary>
        [Test]
        public void AStockpileComesBackWithItsCellsPriorityAndFilter()
        {
            ColonyWorld colony = Fresh();
            var zones = colony.Pawns.Storage!;
            Assume.That(zones.ZoneCount, Is.GreaterThan(0));

            StorageSettings pile = zones.SettingsOf(0);
            pile.Priority = StoragePriority.Preferred;
            pile.ApplyPreset(StoragePreset.Nothing);
            pile.SetDef(ItemIndex.Stone, true);
            var cells = new List<int>(zones.CellsOf(0));

            ColonyWorld restored = RoundTrip(colony);
            var restoredZones = restored.Pawns.Storage!;
            Assert.That(restoredZones.ZoneCount, Is.EqualTo(1));

            StorageSettings back = restoredZones.SettingsOf(0);
            Assert.That(restoredZones.CellsOf(0), Is.EqualTo(cells), "the zone came back a different shape");
            Assert.That(back.Priority, Is.EqualTo(StoragePriority.Preferred), "the priority the player set was lost");
            Assert.That(back.Accepts(ItemIndex.Stone), Is.True, "the filter came back accepting nothing");
            Assert.That(back.Accepts(ItemIndex.Meal), Is.False,
                "the filter came back open, which silently undoes every zone the player narrowed");

            foreach (int cell in cells)
                Assert.That(restored.Pawns.Items.IsStockpileCell(cell), Is.True,
                    "the cell index was not rebuilt, so the zone exists and nothing is in it");
        }

        /// <summary>
        /// Forbidding a thing is an order too, and the flag rides the item.
        /// </summary>
        [Test]
        public void AForbiddenThingStaysForbidden()
        {
            ColonyWorld colony = Fresh();
            var items = colony.Pawns.Items.Items;
            Assume.That(items.Count, Is.GreaterThan(0));

            ThingId id = items[0].Id;
            colony.World.Intents.Submit(new Intent(IntentKind.SetForbidden, a: id.Value, b: 1));
            colony.World.Tick();
            Assume.That(items[0].Forbidden, Is.True);

            ColonyWorld restored = RoundTrip(colony);
            var back = restored.Pawns.Items.Items;
            bool found = false;
            for (int i = 0; i < back.Count; i++)
            {
                if (back[i].Id.Value != id.Value) continue;
                found = true;
                Assert.That(back[i].Forbidden, Is.True, "the thing came back allowed");
            }

            Assert.That(found, Is.True, "the thing is not in the save at all");
        }

        /// <summary>
        /// And the work priorities a player sets on the roster, which are the standing order every
        /// other one is scheduled against.
        /// </summary>
        [Test]
        public void WorkPrioritiesComeBack()
        {
            ColonyWorld colony = Fresh();
            var pawn = colony.Pawns.Pawns.All[0];
            Assume.That(pawn.WorkPriorities.Length, Is.GreaterThan(1));

            for (int w = 0; w < pawn.WorkPriorities.Length; w++)
                pawn.WorkPriorities[w] = (byte)(w % 4 + 1);
            byte[] wanted = (byte[])pawn.WorkPriorities.Clone();

            ColonyWorld restored = RoundTrip(colony);
            Assert.That(restored.Pawns.Pawns.All[0].WorkPriorities, Is.EqualTo(wanted));
        }

        // ---- the control -----------------------------------------------------------------------

        /// <summary>
        /// <b>Every order is visible to the state hash.</b> This is the control, and it is the
        /// half that earns the file: the round-trip tests above each read one order back by name,
        /// so they can only cover the orders somebody thought to write a test for. The hash is
        /// what covers the rest — <c>WorldRoundTripTests</c> compares it, the goldens pin it and
        /// the determinism harness rests on it — and an order the hash cannot see is an order all
        /// three are silently blind to.
        ///
        /// <para>It found two the day it was written. <c>ColonyItems</c> hashed its items and not
        /// its stockpiles or its bed list, both of which it had been <i>saving</i> since they
        /// existed: a zone whose filter failed to round-trip would have come back accepting
        /// everything, and every test that checks a save by its hash would have passed.</para>
        /// </summary>
        [Test]
        public void EachOrderMovesTheStateHash()
        {
            ulong Hash(ColonyWorld c) => c.World.ComputeStateHash().Value;

            // A designation.
            {
                ColonyWorld colony = Fresh();
                int rock = SolidCell(colony);
                Assume.That(rock, Is.GreaterThanOrEqualTo(0));
                ulong before = Hash(colony);
                Assume.That(colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine),
                    Is.EqualTo(IntentRejection.None));
                Assert.That(Hash(colony), Is.Not.EqualTo(before), "a mine order is invisible to the hash");
            }

            // A blueprint.
            {
                ColonyWorld colony = Fresh();
                int cell = OpenCell(colony, new HashSet<int>());
                Assume.That(cell, Is.GreaterThanOrEqualTo(0));
                ulong before = Hash(colony);
                Assume.That(colony.Construction.Place(
                        Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Stone),
                    Is.EqualTo(IntentRejection.None));
                Assert.That(Hash(colony), Is.Not.EqualTo(before), "a blueprint is invisible to the hash");
            }

            // A bed's owner.
            {
                ColonyWorld colony = Fresh();
                Assume.That(colony.Pawns.Items.Beds.Count, Is.GreaterThan(0));
                ulong before = Hash(colony);
                Assume.That(colony.Construction.AssignOwnerAt(
                        colony.Pawns.Items.Beds[0], colony.Pawns.Pawns.All[0].Id.Value),
                    Is.EqualTo(IntentRejection.None));
                Assert.That(Hash(colony), Is.Not.EqualTo(before), "who owns a bed is invisible to the hash");
            }

            // A zone's filter.
            {
                ColonyWorld colony = Fresh();
                Assume.That(colony.Pawns.Storage!.ZoneCount, Is.GreaterThan(0));
                StorageSettings pile = colony.Pawns.Storage!.SettingsOf(0);
                Assume.That(pile.Allow.Length, Is.GreaterThan(0));
                ulong before = Hash(colony);
                pile.Allow[ItemIndex.Meal] = !pile.Allow[ItemIndex.Meal];
                Assert.That(Hash(colony), Is.Not.EqualTo(before),
                    "what a stockpile accepts is invisible to the hash");
            }

            // A zone's priority.
            {
                ColonyWorld colony = Fresh();
                Assume.That(colony.Pawns.Storage!.ZoneCount, Is.GreaterThan(0));
                ulong before = Hash(colony);
                colony.Pawns.Storage!.SettingsOf(0).Priority += 1;
                Assert.That(Hash(colony), Is.Not.EqualTo(before),
                    "a stockpile's priority is invisible to the hash");
            }

            // Forbidding.
            {
                ColonyWorld colony = Fresh();
                Assume.That(colony.Pawns.Items.Items.Count, Is.GreaterThan(0));
                ulong before = Hash(colony);
                colony.World.Intents.Submit(
                    new Intent(IntentKind.SetForbidden, a: colony.Pawns.Items.Items[0].Id.Value, b: 1));
                colony.World.Tick();
                Assert.That(Hash(colony), Is.Not.EqualTo(before), "forbidding is invisible to the hash");
            }

            // A work priority.
            {
                ColonyWorld colony = Fresh();
                var pawn = colony.Pawns.Pawns.All[0];
                ulong before = Hash(colony);
                pawn.WorkPriorities[0] = (byte)(pawn.WorkPriorities[0] == 1 ? 2 : 1);
                Assert.That(Hash(colony), Is.Not.EqualTo(before), "a work priority is invisible to the hash");
            }
        }
    }
}
