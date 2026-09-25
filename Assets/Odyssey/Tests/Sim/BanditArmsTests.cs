#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.WeaponFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The bandit arrives armed (design 33 §1: "armed"), with a crowbar or a bat and never a blade
    /// (design 42 §3; owner, 2026-09-24: "not swords - not their style"). Spawned the way the debug
    /// menu spawns anybody, through <c>IWeaponRules.ArmOnSpawn</c>; the control throughout is a
    /// colonist and an animal spawned by the same intent, who hold nothing.
    /// </summary>
    public class BanditArmsTests
    {
        static bool IsBanditsWeapon(int def) => def == ItemIndex.Crowbar || def == ItemIndex.Bat;

        [Test]
        public void ADebugSpawnedBanditHoldsACrowbarOrABat()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;

            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);

            Assert.That(bandit.EquippedItem, Is.Not.Zero, "the bandit arrived bare-handed");
            ColonyItem weapon = ctx.Items.Get(new ThingId(bandit.EquippedItem))!;
            Assert.That(weapon, Is.Not.Null);
            Assert.That(IsBanditsWeapon(weapon.DefIndex), Is.True, $"a bandit arrived holding item {weapon.DefIndex}");
            Assert.That(weapon.DefIndex, Is.EqualTo(ctx.Content.WeaponFor(PawnKindIndex.Bandit, bandit.Id.Value, bandit.RollSeed)),
                "the hand is not the pawn's own deal");
            Assert.That(weapon.Cell, Is.EqualTo(-1), "a held weapon keeps a cell");
            Assert.That(weapon.CarriedBy, Is.EqualTo(bandit.Id.Value));
            Assert.That(ctx.Items.LooseItems, Has.No.Member(weapon.Id.Value - 1), "the weapon is on a lister");
            Assert.That(ctx.WeaponRules.ArmamentOf(bandit, ctx).ItemDef, Is.EqualTo(weapon.DefIndex));
        }

        /// <summary>
        /// Both are dealt across a gang, so the kind's list is not decoration: twelve bandits from
        /// one board hold both, and never anything else. The deal is a hash of the pawn rather than
        /// a draw, so the same world deals the same gang (design 42 §3).
        /// </summary>
        [Test]
        public void AGangOfBanditsCarriesBothAndNothingElse()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;

            bool crowbar = false, bat = false;
            for (int i = 0; i < 12; i++)
            {
                Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
                int held = ctx.Items.Get(new ThingId(bandit.EquippedItem))!.DefIndex;
                Assert.That(IsBanditsWeapon(held), Is.True, $"bandit {i} held item {held}");
                crowbar |= held == ItemIndex.Crowbar;
                bat |= held == ItemIndex.Bat;
            }
            Assert.That(crowbar && bat, Is.True, $"twelve bandits were dealt one weapon only (crowbar {crowbar}, bat {bat})");
        }

        /// <summary>The deal is a pure function of the pawn, and drawing it moves no other roll.</summary>
        [Test]
        public void TheDealIsAFunctionOfThePawn()
        {
            PawnContent content = Board().Pawns.Content;
            for (int id = 1; id < 40; id++)
                Assert.That(content.WeaponFor(PawnKindIndex.Bandit, id, 12345u),
                    Is.EqualTo(content.WeaponFor(PawnKindIndex.Bandit, id, 12345u)));
        }

        /// <summary>
        /// The interface hears it through the weapon aspect, and nowhere else: no thing view is
        /// published for a weapon in a hand, because a thing in a hand is drawn by its holder.
        /// </summary>
        [Test]
        public void TheWeaponPublishesAsTheBanditsWeaponAndNotAsAThing()
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPawnAspect(bandit.Id, CombatAspects.Weapon, out int def), Is.True);
            Assert.That(IsBanditsWeapon(def), Is.True);
            foreach (ThingView thing in frame.Things)
                Assert.That(thing.Id.Value, Is.Not.EqualTo(bandit.EquippedItem), "the held machete is drawn on the ground");
        }

        [Test]
        public void TheMacheteLiesAtTheCorpseWhenTheBanditDies()
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            ColonyItem machete = colony.Pawns.Items.Get(new ThingId(bandit.EquippedItem))!;
            Assume.That(machete, Is.Not.Null);
            ClearGround(colony, bandit.Cell);

            Corpse corpse = Kill(colony, bandit);

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
        /// Spawned where there is already a pile, the bandit still arrives armed: the machete is
        /// made on the nearest cell that can take it and taken straight up, never on top of the pile.
        /// </summary>
        [Test]
        public void ABanditSpawnedOnAPileIsStillArmed()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;
            Pawn near = ctx.Pawns.All[0];
            int cell = ctx.Cells.NearestWalkableInColumn(Size.FromIndex(near.Cell).X, Size.FromIndex(near.Cell).Z, Size.FromIndex(near.Cell).Y);
            if (ctx.Items.ItemAt(cell) == null) ctx.Items.Spawn(ItemIndex.Stone, cell, stack: 5);
            int pile = ctx.Items.ItemAt(cell)!.DefIndex;

            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            Assume.That(bandit.Cell, Is.EqualTo(cell));

            Assert.That(bandit.EquippedItem, Is.Not.Zero);
            Assert.That(ctx.Items.ItemAt(cell)!.DefIndex, Is.EqualTo(pile), "the pile was disturbed");
        }

        /// <summary>
        /// A pawn that leaves the board <b>without dying</b> puts its weapon down first (lane D's
        /// open issue, closed at the integration, 2026-09-23). Otherwise the machete stays carried by
        /// an id that no longer exists, with no cell, for ever — no lister holds it and nothing can
        /// fetch it. Nothing leaves armed today, but a bandit that flees off the edge would. The
        /// rule is in <c>PawnRegistry.Despawn</c>, beside the beds it already releases, so every way
        /// off the board lets go of the same things.
        /// </summary>
        [Test]
        public void ABanditDespawnedWithoutDyingPutsItsMacheteDown()
        {
            var colony = Board();
            colony.World.Tick(30);
            PawnContext ctx = colony.Pawns;
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            ColonyItem machete = ctx.Items.Get(new ThingId(bandit.EquippedItem))!;
            Assume.That(machete, Is.Not.Null);
            Assume.That(machete.Cell, Is.EqualTo(-1));

            ctx.Pawns.Despawn(bandit);

            Assert.That(machete.Despawned, Is.False, "the machete went with the bandit");
            Assert.That(machete.CarriedBy, Is.Zero, "the machete is still held by a pawn that is gone");
            Assert.That(machete.Cell, Is.GreaterThanOrEqualTo(0), "the machete has no cell");
            Assert.That(ctx.Items.ItemAt(machete.Cell), Is.SameAs(machete));
            Assert.That(bandit.EquippedItem, Is.Zero);
        }
    }
}
