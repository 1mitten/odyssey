#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Drawn or sheathed (design 33 §8b, owner 2026-09-23): the one rule
    /// (<see cref="WeaponDraw"/>) published as <see cref="PawnFlags.Drawn"/>. Each reason to draw
    /// has a test that isolates it — the other three kept false — so taking that reason out of the
    /// rule fails exactly that test (the negative controls, each run once and seen to fail: §8b).
    /// The two seconds before a weapon goes back to the hip are presentation's and are tested in
    /// the Hud tier (<c>WeaponSheathTests</c>).
    /// </summary>
    public class WeaponDrawTests
    {
        static bool Drawn(ColonyWorld colony, Pawn pawn) =>
            colony.World.Views.Current.TryGetPawn(pawn.Id, out PawnView view) && view.IsWeaponDrawn;

        /// <summary>Put a weapon of <paramref name="def"/> in the pawn's hand, through the one door that does.</summary>
        static void Arm(ColonyWorld colony, Pawn pawn, int def = ItemIndex.Machete)
        {
            PawnContext ctx = colony.Pawns;
            int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, pawn.Cell, def, 1, maxRadius: 8);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0), "nowhere to make the weapon");
            ColonyItem item = ctx.Items.Get(ctx.Items.Spawn(def, cell))!;
            WeaponHand.TakeUp(pawn, item, ctx);
            Assume.That(WeaponHand.Held(pawn, ctx), Is.SameAs(item));
        }

        /// <summary>A cell <paramref name="dx"/> east of <paramref name="of"/> on its own layer.</summary>
        static int East(ColonyWorld colony, Pawn of, int dx)
        {
            CellRef at = Size.FromIndex(of.Cell);
            int cell = colony.Pawns.Cells.NearestWalkableInColumn(at.X + dx, at.Z, at.Y);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0));
            Assume.That(Size.FromIndex(cell).Y, Is.EqualTo(at.Y), "the barren board is one level");
            return cell;
        }

        [Test]
        public void ADraftedColonistsWeaponIsDrawnAndBareHandsDrawNothing()
        {
            var colony = Board();
            Pawn armed = colony.Pawns.Pawns.All[0];
            Pawn bare = colony.Pawns.Pawns.All[1];
            colony.World.Tick(30);
            Arm(colony, armed);
            colony.World.Tick();
            Assert.That(Drawn(colony, armed), Is.False, "the control: undrafted, at peace, it was out");

            Assert.That(Draft(colony, armed), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, bare), Is.EqualTo(IntentRejection.None));
            Assert.That(Drawn(colony, armed), Is.True, "drafted, and the weapon stayed at the hip");
            Assert.That(Drawn(colony, bare), Is.False, "drafted with bare hands, and something was drawn");
            Assert.That(WeaponDraw.HasReason(colony.Pawns, bare, colony.World.CurrentTick), Is.True,
                "the rule without the hand still says she is ready to fight");
        }

        [Test]
        public void ReleasedFromTheDraftWithNoFightNearTheWeaponIsSheathedAtOnce()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            Arm(colony, pawn);
            Draft(colony, pawn);
            colony.World.Tick(60);
            Assume.That(Drawn(colony, pawn), Is.True);

            Assert.That(Draft(colony, pawn, on: false), Is.EqualTo(IntentRejection.None));
            Assert.That(Drawn(colony, pawn), Is.False,
                "released with nobody near, and the simulation kept the weapon out; the hold is presentation's");
        }

        [Test]
        public void AnUndraftedColonistGoingAboutHerDayKeepsItSheathed()
        {
            var colony = Board(colonists: 3);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            Arm(colony, pawn, ItemIndex.Bat);

            int jobs = 0, last = -2;
            for (int t = 0; t < 3_000; t++)
            {
                colony.World.Tick();
                Assert.That(Drawn(colony, pawn), Is.False, $"out at tick {colony.World.CurrentTick} with nobody to fight");
                int job = pawn.CurrentJob?.DefIndex ?? -1;
                if (job != last && job >= 0) jobs++;
                last = job;
            }
            Assert.That(jobs, Is.GreaterThan(0), "she did nothing, so this proved nothing");
            Assert.That(pawn.EquippedItem, Is.Not.Zero, "the bat left her hand");
        }

        /// <summary>
        /// The attack's target at three tiles keeps the weapon sheathed and at two draws it. The
        /// attacker is not drafted (the draft would draw it by itself) and has not been struck, so
        /// the distance is the only reason in play. The target is a drafted colonist, who holds.
        /// </summary>
        [Test]
        public void AnAttacksTargetAtThreeTilesKeepsItSheathedAndAtTwoDrawsIt()
        {
            var colony = Board(colonists: 3);
            Pawn attacker = colony.Pawns.Pawns.All[0];
            Pawn target = colony.Pawns.Pawns.All[1];
            colony.World.Tick(30);
            Arm(colony, attacker);
            Stand(colony, target, Near(colony, 0, 0));
            Draft(colony, target);

            bool At(int dx)
            {
                Draft(colony, attacker);
                Stand(colony, attacker, East(colony, target, dx));
                Assert.That(Attack(colony, attacker, target), Is.EqualTo(IntentRejection.None));
                attacker.Drafted = false;   // the attack runs on; only the distance is left
                int stood = attacker.Cell;
                colony.World.Tick();
                Assert.That(attacker.Cell, Is.EqualTo(stood), "she landed a step inside the test");
                Assert.That(attacker.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the attack ended");
                Assert.That(attacker.RetaliateAgainst, Is.Zero, "struck, which is another reason");
                return Drawn(colony, attacker);
            }

            Assert.That(At(3), Is.False, "the target three tiles off drew the weapon");
            Assert.That(At(2), Is.True, "the target two tiles off left the weapon at the hip");
            Assert.That(At(5), Is.False, "the control: five tiles off drew it");
        }

        [Test]
        public void NearIsTwoTilesOnTheGroundAndOneLayerUpOrDown()
        {
            GridSize size = Size;
            int at = size.Index(new CellRef(20, 20, 5));
            Assert.That(WeaponDraw.Within(size, at, size.Index(new CellRef(22, 18, 5)), 2), Is.True, "the corner at two");
            Assert.That(WeaponDraw.Within(size, at, size.Index(new CellRef(23, 20, 5)), 2), Is.False, "three along x");
            Assert.That(WeaponDraw.Within(size, at, size.Index(new CellRef(20, 17, 5)), 2), Is.False, "three along z");
            Assert.That(WeaponDraw.Within(size, at, size.Index(new CellRef(21, 21, 6)), 2), Is.True, "the layer above");
            Assert.That(WeaponDraw.Within(size, at, size.Index(new CellRef(21, 21, 4)), 2), Is.True, "the layer below");
            Assert.That(WeaponDraw.Within(size, at, size.Index(new CellRef(20, 20, 7)), 2), Is.False, "two layers up");
        }

        /// <summary>
        /// Struck, she fights back and her weapon comes out — even with the one who struck her five
        /// tiles away, where the distance alone would not draw it. The retaliation window is the
        /// only reason in play; an armed colonist nobody struck is the control.
        /// </summary>
        [Test]
        public void StruckAndFightingBackTheWeaponIsDrawn()
        {
            var colony = Board(colonists: 3);
            Pawn victim = colony.Pawns.Pawns.All[0];
            Pawn striker = colony.Pawns.Pawns.All[1];
            Pawn bystander = colony.Pawns.Pawns.All[2];
            colony.World.Tick(30);
            Arm(colony, victim);
            Arm(colony, bystander, ItemIndex.Bat);
            Stand(colony, victim, Near(colony, 0, 0));
            colony.World.Tick();
            Assume.That(Drawn(colony, victim), Is.False);

            Strike(colony, striker, victim, 1_000);
            Assert.That(victim.RetaliateAgainst, Is.EqualTo(striker.Id.Value), "the blow opened no window");
            Stand(colony, striker, East(colony, victim, 5));
            Draft(colony, striker);   // holds him five tiles off, and ticks the world once

            Assert.That(victim.Drafted, Is.False);
            Assert.That(WeaponDraw.TargetNear(colony.Pawns, victim), Is.False, "the distance is a second reason");
            Assert.That(Drawn(colony, victim), Is.True, "struck and fighting back, and the weapon stayed at the hip");
            Assert.That(Drawn(colony, bystander), Is.False, "the control: nobody struck her, and hers came out");
        }

        /// <summary>
        /// A bandit's weapon is always out — stood ten tiles from anybody, so its hunt's target is
        /// no reason, and never struck. A bandit with its weapon taken from it draws nothing.
        /// </summary>
        [Test]
        public void ABanditAlwaysHasItsWeaponDrawn()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            Pawn bandit = WeaponFixture.SpawnKind(colony, PawnKindIndex.Bandit);
            Assume.That(WeaponHand.Held(bandit, colony.Pawns), Is.Not.Null, "spawned unarmed");
            Assert.That(Drawn(colony, bandit), Is.True, "spawned with its weapon sheathed");

            Stand(colony, colonist, Near(colony, 0, 0));
            Stand(colony, bandit, East(colony, colonist, 10));
            for (int t = 0; t < 60; t++)
            {
                colony.World.Tick();
                Assume.That(WeaponDraw.TargetNear(colony.Pawns, bandit), Is.False);
                Assert.That(Drawn(colony, bandit), Is.True, $"a bandit sheathed its weapon at tick {colony.World.CurrentTick}");
            }

            WeaponHand.PutDown(bandit, colony.Pawns, bandit.Cell);
            colony.World.Tick();
            Assert.That(Drawn(colony, bandit), Is.False, "the control: bare-handed, and something was drawn");
        }
    }
}
