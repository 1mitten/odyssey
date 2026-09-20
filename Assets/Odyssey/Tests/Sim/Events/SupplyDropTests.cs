#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// The first event, end to end (design 23 §6): a stack of meals is launched, falls for the
    /// ticks the Def promised, lands on something with sky above it, and is then an ordinary
    /// thing a hauler fetches.
    /// </summary>
    public class SupplyDropTests
    {
        const int Drop = IncidentHandle.SupplyDrop;

        [Test]
        public void InvokingLaunchesTheLoadAndWritesTheLedgerBeforeAnythingLands()
        {
            var colony = Harness.Build();
            colony.Invoke();
            colony.World.Tick();

            Assert.That(colony.World.Intents.Rejected, Is.Empty);
            Assert.That(colony.Incidents.Skyfallers.InFlight, Has.Count.EqualTo(1));
            Assert.That(colony.MealsOnBoard(), Is.Zero, "nothing exists until it lands");

            var ledger = colony.Incidents.Ledger;
            Assert.That(ledger.Count, Is.EqualTo(1));
            Assert.That(ledger[0].Id, Is.EqualTo(1));
            Assert.That(ledger[0].IncidentDef, Is.EqualTo(Drop));
            Assert.That(ledger[0].Tick, Is.EqualTo(0), "fired during the drain of tick 0");
            Assert.That(ledger[0].Cell, Is.EqualTo(colony.Incidents.Skyfallers.InFlight[0].LandingCell));
            Assert.That(ledger.Fires(Drop), Is.EqualTo(1));
            Assert.That(ledger.LastFiredTick(Drop), Is.EqualTo(0));
        }

        [Test]
        public void TheLoadLandsOnTheTickTheLaunchPromisedAndNotBefore()
        {
            var colony = Harness.Build();
            IncidentDef def = colony.Incidents.Content.Defs[Drop];
            colony.Invoke();
            colony.World.Tick();

            var entry = colony.Incidents.Skyfallers.InFlight[0];
            Assert.That(entry.LandTick - entry.LaunchTick, Is.EqualTo(def.fallTicks));

            colony.World.Tick(def.fallTicks - 1);
            Assert.That(colony.MealsOnBoard(), Is.Zero, "one tick early");

            colony.World.Tick();
            Assert.That(colony.Incidents.Skyfallers.InFlight, Is.Empty);
            Assert.That(colony.Incidents.Skyfallers.Landed, Is.EqualTo(1));

            ColonyItem? landed = colony.Pawns.Items.ItemAt(entry.LandingCell);
            Assert.That(landed, Is.Not.Null, "the load is not where the launch said it would be");
            Assert.That(landed!.DefIndex, Is.EqualTo(ItemIndex.Meal));
            Assert.That(landed.Stack, Is.InRange(def.stackMin, def.stackMax));
        }

        [Test]
        public void TheLandingCellIsWalkableWithNothingButSkyAboveIt()
        {
            var colony = Harness.Build();
            colony.Invoke();
            colony.World.Tick();

            int cell = colony.Incidents.Skyfallers.InFlight[0].LandingCell;
            CellRef at = colony.Size.FromIndex(cell);
            Assert.That(colony.Cells.IsWalkable(cell), Is.True);
            Assert.That(colony.Cells.SkyLanding(at.X, at.Z), Is.EqualTo(cell));
        }

        [Test]
        public void TheSameSeedAndTickLandTheSameLoadOnTheSameCell()
        {
            var one = Harness.Build(seed: 77u);
            var two = Harness.Build(seed: 77u);
            one.Invoke();
            two.Invoke();
            one.World.Tick();
            two.World.Tick();

            var a = one.Incidents.Skyfallers.InFlight[0];
            var b = two.Incidents.Skyfallers.InFlight[0];
            Assert.That(a.LandingCell, Is.EqualTo(b.LandingCell));
            Assert.That(a.Stack, Is.EqualTo(b.Stack));
            Assert.That(one.World.ComputeStateHash().Value, Is.EqualTo(two.World.ComputeStateHash().Value));
        }

        [Test]
        public void AForcedCellIsHonouredWhenItQualifiesAndRefusedWhenItDoesNot()
        {
            var colony = Harness.Build();
            var forced = new CellRef(3, 4, 0);
            Assert.That(colony.Incidents.TryFire(new IncidentParms(Drop, cell: forced)), Is.True);
            Assert.That(colony.Incidents.Skyfallers.InFlight[0].LandingCell, Is.EqualTo(colony.Size.Index(forced)));

            int wall = colony.Size.Index(5, 5, 0);
            colony.Cells.Edifice[wall] = 0;
            colony.Cells.Flags[wall] |= CellFlags.BlockingEdifice;
            Assert.That(colony.Incidents.CanFire(new IncidentParms(Drop, cell: new CellRef(5, 5, 0))), Is.False);
            Assert.That(colony.Incidents.TryFire(new IncidentParms(Drop, cell: new CellRef(5, 5, 0))), Is.False);
            Assert.That(colony.Incidents.Ledger.Count, Is.EqualTo(1), "a refusal writes nothing down");
        }

        [Test]
        public void ABoardWithNowhereToLandRefusesAsNotPermittedAndWritesNothing()
        {
            var colony = Harness.Build();
            for (int i = 0; i < colony.Size.CellCount; i++) colony.Cells.Flags[i] |= CellFlags.SolidTerrain;

            colony.Invoke();
            colony.World.Tick();

            Assert.That(colony.World.Intents.Rejected, Has.Count.EqualTo(1));
            Assert.That(colony.World.Intents.Rejected[0].Reason, Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(colony.Incidents.Skyfallers.InFlight, Is.Empty);
            Assert.That(colony.Incidents.Ledger.Count, Is.Zero);
        }

        /// <summary>
        /// The corner the launch-time check cannot close: something else took the cell during
        /// the fall. The load widens outward the way every other spawn does.
        /// </summary>
        [Test]
        public void ALoadWhoseCellWasTakenDuringTheFallLandsBesideIt()
        {
            var colony = Harness.Build();
            colony.Invoke();
            colony.World.Tick();
            var entry = colony.Incidents.Skyfallers.InFlight[0];
            colony.Pawns.Items.Spawn(ItemIndex.Wood, entry.LandingCell, 1);

            colony.World.Tick(colony.Incidents.Content.Defs[Drop].fallTicks);

            Assert.That(colony.Incidents.Skyfallers.Landed, Is.EqualTo(1));
            Assert.That(colony.Incidents.Skyfallers.Lost, Is.Zero);
            Assert.That(colony.MealsOnBoard(), Is.EqualTo(colony.LandedStack()));
            Assert.That(colony.Pawns.Items.ItemAt(entry.LandingCell)!.DefIndex, Is.EqualTo(ItemIndex.Wood));
        }

        [Test]
        public void ALandedDropIsHauledToAStockpileThatAcceptsIt()
        {
            var colony = Harness.Build();
            int pile = colony.Size.Index(8, 8, 0);
            colony.Stockpile(pile);
            colony.Pawns.Pawns.Spawn(colony.Size.Index(2, 2, 0));

            colony.Invoke();
            colony.World.Tick(colony.Incidents.Content.Defs[Drop].fallTicks + 1);
            Assert.That(colony.MealsOnBoard(), Is.GreaterThan(0));

            for (int i = 0; i < 6_000 && colony.Pawns.Items.ItemAt(pile) == null; i++) colony.World.Tick();

            ColonyItem? stored = colony.Pawns.Items.ItemAt(pile);
            Assert.That(stored, Is.Not.Null, "nobody fetched the drop in 6,000 ticks");
            Assert.That(stored!.DefIndex, Is.EqualTo(ItemIndex.Meal));
        }

        [Test]
        public void WithNoStockpileTheLoadLiesWhereItFell()
        {
            var colony = Harness.Build();
            colony.Pawns.Pawns.Spawn(colony.Size.Index(2, 2, 0));
            colony.Invoke();
            colony.World.Tick();
            int landing = colony.Incidents.Skyfallers.InFlight[0].LandingCell;

            colony.World.Tick(2_000);

            ColonyItem? lying = colony.Pawns.Items.ItemAt(landing);
            Assert.That(lying, Is.Not.Null);
            Assert.That(lying!.DefIndex, Is.EqualTo(ItemIndex.Meal));
        }

        /// <summary>
        /// A flat board wired through <see cref="ColonyComposition.AddColony"/>, because that is
        /// the one place the events are attached and so the only place worth testing them in.
        /// Floored on layer 0 only, so the sky is real: <c>PawnTests.Colony</c> floors every
        /// layer, which would land every drop on the top of the world.
        /// </summary>
        sealed class Harness
        {
            public PawnContext Pawns = null!;
            public SimWorld World = null!;
            public CellGrid Cells = null!;
            public Incidents Incidents = null!;

            public GridSize Size => Cells.Size;

            public static Harness Build(uint seed = 11u, int sx = 16, int sz = 16, int sy = 4)
            {
                var size = new GridSize(sx, sz, sy);
                var cells = new CellGrid(size);
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                    cells.Floor[size.Index(x, z, 0)] = 1;

                var nav = new NavGraph(cells);
                nav.Rebuild();
                var pawns = new PawnContext(cells, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());
                var solver = new SupportSolver(cells);
                var support = new SupportSystem(cells, solver);
                var designations = new DesignationGrid(cells, Array.Empty<PlacedEdifice>());

                var builder = new SimWorldBuilder().WithSeed(seed).WithSize(size);
                builder.AddColony(pawns, designations, support, nav, new List<PlacedEdifice>(), out _);
                SimWorld world = builder.Build();
                pawns.Seed = seed;

                return new Harness { Cells = cells, Pawns = pawns, World = world, Incidents = pawns.Incidents! };
            }

            public void Invoke() =>
                World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.SupplyDrop));

            public void Stockpile(params int[] cells)
            {
                for (int i = 0; i < cells.Length; i++)
                    Pawns.Storage!.Designate(
                        Pawns.Size.FromIndex(cells[i]), cells[0], Odyssey.Sim.Storage.StoragePreset.Everything);
            }

            public int MealsOnBoard()
            {
                int total = 0;
                var items = Pawns.Items.Items;
                for (int i = 0; i < items.Count; i++)
                    if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Meal) total += items[i].Stack;
                return total;
            }

            public int LandedStack()
            {
                var items = Pawns.Items.Items;
                for (int i = items.Count - 1; i >= 0; i--)
                    if (!items[i].Despawned && items[i].DefIndex == ItemIndex.Meal) return items[i].Stack;
                return 0;
            }
        }
    }
}
