#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The marauder arrives armed (design 33 §1: "armed"; the machete is its kind's, §5b). Spawned
    /// the way the debug menu spawns anybody, through <c>IWeaponRules.ArmOnSpawn</c>; the control
    /// throughout is a colonist and an animal spawned by the same intent, who hold nothing.
    /// </summary>
    public class MarauderArmsTests
    {
        [Test]
        public void ADebugSpawnedMarauderHoldsItsMachete()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;

            Pawn marauder = SpawnKind(colony, PawnKindIndex.Marauder);

            Assert.That(marauder.EquippedItem, Is.Not.Zero, "the marauder arrived bare-handed");
            ColonyItem machete = ctx.Items.Get(new ThingId(marauder.EquippedItem))!;
            Assert.That(machete, Is.Not.Null);
            Assert.That(machete.DefIndex, Is.EqualTo(ItemIndex.Machete));
            Assert.That(machete.Cell, Is.EqualTo(-1), "a held weapon keeps a cell");
            Assert.That(machete.CarriedBy, Is.EqualTo(marauder.Id.Value));
            Assert.That(ctx.Items.LooseItems, Does.Not.Contain(machete.Id.Value - 1), "the machete is on a lister");
            Assert.That(ctx.WeaponRules.ArmamentOf(marauder, ctx).ItemDef, Is.EqualTo(ItemIndex.Machete));
        }

        /// <summary>
        /// The interface hears it through the weapon aspect, and nowhere else: no thing view is
        /// published for a machete in a hand, because a thing in a hand is drawn by its holder.
        /// </summary>
        [Test]
        public void TheMachetePublishesAsTheMaraudersWeaponAndNotAsAThing()
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn marauder = SpawnKind(colony, PawnKindIndex.Marauder);
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPawnAspect(marauder.Id, CombatAspects.Weapon, out int def), Is.True);
            Assert.That(def, Is.EqualTo(ItemIndex.Machete));
            foreach (ThingView thing in frame.Things)
                Assert.That(thing.Id.Value, Is.Not.EqualTo(marauder.EquippedItem), "the held machete is drawn on the ground");
        }

        [Test]
        public void TheMacheteLiesAtTheCorpseWhenTheMarauderDies()
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn marauder = SpawnKind(colony, PawnKindIndex.Marauder);
            ColonyItem machete = colony.Pawns.Items.Get(new ThingId(marauder.EquippedItem))!;
            Assume.That(machete, Is.Not.Null);
            ClearGround(colony, marauder.Cell);

            Corpse corpse = Kill(colony, marauder);

            Assert.That(machete.Cell, Is.EqualTo(corpse.Cell));
            Assert.That(machete.CarriedBy, Is.Zero);
            Assert.That(colony.Pawns.Items.ItemAt(corpse.Cell), Is.SameAs(machete));
        }

        [TestCase(PawnKindIndex.Colonist)]
        [TestCase(PawnKindIndex.MiddenHog)]
        [TestCase(PawnKindIndex.DuctRat)]
        public void AColonistOrAnAnimalSpawnedTheSameWayHoldsNothing(int kind)
        {
            var colony = Board();
            colony.World.Tick(30);
            int things = colony.Pawns.Items.Items.Count;

            Pawn pawn = SpawnKind(colony, kind);

            Assert.That(pawn.EquippedItem, Is.Zero);
            Assert.That(colony.Pawns.Items.Items.Count, Is.EqualTo(things), "a thing was made for a pawn that holds nothing");
        }

        /// <summary>
        /// Spawned where there is already a pile, the marauder still arrives armed: the machete is
        /// made on the nearest cell that can take it and taken straight up, never on top of the pile.
        /// </summary>
        [Test]
        public void AMarauderSpawnedOnAPileIsStillArmed()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;
            Pawn near = ctx.Pawns.All[0];
            int cell = ctx.Cells.NearestWalkableInColumn(Size.FromIndex(near.Cell).X, Size.FromIndex(near.Cell).Z, Size.FromIndex(near.Cell).Y);
            if (ctx.Items.ItemAt(cell) == null) ctx.Items.Spawn(ItemIndex.Stone, cell, stack: 5);
            int pile = ctx.Items.ItemAt(cell)!.DefIndex;

            Pawn marauder = SpawnKind(colony, PawnKindIndex.Marauder);
            Assume.That(marauder.Cell, Is.EqualTo(cell));

            Assert.That(marauder.EquippedItem, Is.Not.Zero);
            Assert.That(ctx.Items.ItemAt(cell)!.DefIndex, Is.EqualTo(pile), "the pile was disturbed");
        }

        /// <summary>
        /// A pawn that leaves the board <b>without dying</b> puts its weapon down first (lane D's
        /// open issue, closed at the integration, 2026-09-23). Otherwise the machete stays carried by
        /// an id that no longer exists, with no cell, for ever — no lister holds it and nothing can
        /// fetch it. Nothing leaves armed today, but a marauder that flees off the edge would. The
        /// rule is in <c>PawnRegistry.Despawn</c>, beside the beds it already releases, so every way
        /// off the board lets go of the same things.
        /// </summary>
        [Test]
        public void AMarauderDespawnedWithoutDyingPutsItsMacheteDown()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;
            Pawn marauder = SpawnKind(colony, PawnKindIndex.Marauder);
            ColonyItem machete = ctx.Items.Get(new ThingId(marauder.EquippedItem))!;
            Assume.That(machete, Is.Not.Null);
            Assume.That(machete.Cell, Is.EqualTo(-1));

            ctx.Pawns.Despawn(marauder);

            Assert.That(machete.Despawned, Is.False, "the machete went with the marauder");
            Assert.That(machete.CarriedBy, Is.Zero, "the machete is still held by a pawn that is gone");
            Assert.That(machete.Cell, Is.GreaterThanOrEqualTo(0), "the machete has no cell");
            Assert.That(ctx.Items.ItemAt(machete.Cell), Is.SameAs(machete));
            Assert.That(marauder.EquippedItem, Is.Zero);
        }
    }
}
