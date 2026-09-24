#nullable enable
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A critical blow knocks its target back a tile (design 33 §9b; owner, 2026-09-23: <i>"a chance
    /// you can fall back on to the next tile"</i>). Directly away from the attacker, diagonals
    /// included; on the same layer or one terrace step down, never two, never up, never into water,
    /// never on to a tile another fighter holds. The target lies there for 90 ticks, doing nothing,
    /// published as <see cref="PawnFlags.KnockedDown"/>, then stands. The blows are landed exactly
    /// through <see cref="CombatSystem.ApplySwing"/> with a critical that rolled its knockback, so
    /// each case is about the ground and not the dice.
    /// </summary>
    public class KnockbackTests
    {
        /// <summary>A critical blow that rolled its knockback, for this much damage.</summary>
        static SwingOutcome Knock(int damageMilli = 1_000, bool knockback = true) =>
            new SwingOutcome(CombatEventKind.Hit, damageMilli, 0, critical: true, knockback: knockback);

        /// <summary>A cell <paramref name="dx"/>, <paramref name="dz"/>, <paramref name="dy"/> from a spot clear of the starting kit.</summary>
        static int At(ColonyWorld colony, int dx, int dz, int dy = 0)
        {
            CellRef s = colony.Start;
            return Size.Index(s.X + 10 + dx, s.Z + 10 + dz, s.Y + dy);
        }

        /// <summary>Two colonists, drafted so they hold still, the attacker at (0, 0) and the target beside her.</summary>
        static (ColonyWorld colony, Pawn a, Pawn t) Pair(int tx, int tz, int dy = 0, int targetKind = -1, System.Action<ColonyWorld>? ground = null)
        {
            var colony = Board(colonists: 2);
            colony.World.Tick();
            ground?.Invoke(colony);
            Pawn a = colony.Pawns.Pawns.All[0], t = colony.Pawns.Pawns.All[1];
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Stand(colony, a, At(colony, 0, 0, dy));
            if (targetKind >= 0)
            {
                Stand(colony, t, At(colony, -12, -12));
                t = Spawn(colony, targetKind, At(colony, tx, tz, dy));
            }
            else
            {
                Assert.That(Draft(colony, t), Is.EqualTo(IntentRejection.None));
            }
            Stand(colony, t, At(colony, tx, tz, dy));
            Assert.That(colony.Pawns.Cells.IsWalkable(a.Cell) && colony.Pawns.Cells.IsWalkable(t.Cell), Is.True,
                "the fixture stood them somewhere they cannot stand");
            return (colony, a, t);
        }

        static void Blow(ColonyWorld colony, Pawn a, Pawn t, SwingOutcome outcome) =>
            colony.Pawns.Combat!.ApplySwing(a, t, Fists(colony.Pawns), outcome, colony.World.CurrentTick);

        static PawnFlags FlagsOf(ColonyWorld colony, Pawn pawn)
        {
            foreach (PawnView v in colony.World.Views.Current.Pawns.ToArray())
                if (v.Id == pawn.Id) return v.Flags;
            return PawnFlags.None;
        }

        /// <summary>Paint a cell's terrain and tell the graph, the way <c>AnimalTests</c> does.</summary>
        static void Paint(ColonyWorld colony, int index, ushort terrain)
        {
            var grid = colony.Grid;
            grid.Terrain[index] = terrain;
            CellRef at = grid.FromIndex(index);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (grid.Size.Contains(at.X + dx, at.Z + dz, at.Y))
                    colony.Pawns.Nav.MarkDirty(grid.Size.Index(at.X + dx, at.Z + dz, at.Y));
            if (NaturalContent.IsSolid(terrain)) grid.Flags[index] |= CellFlags.SolidTerrain;
            else grid.Flags[index] &= ~CellFlags.SolidTerrain;
            if (NaturalContent.IsImpassable(terrain)) grid.Flags[index] |= CellFlags.ImpassableTerrain;
            else grid.Flags[index] &= ~CellFlags.ImpassableTerrain;
        }

        /// <summary>Raise the ground under a 3 × 3 patch round (0, 0) by <paramref name="layers"/>, so it stands that much higher.</summary>
        static System.Action<ColonyWorld> Plateau(int layers, int minX = -1, int maxX = 1) => colony =>
        {
            for (int y = 0; y < layers; y++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = minX; dx <= maxX; dx++)
                Paint(colony, At(colony, dx, dz, y), NaturalContent.TerrainGrass);
            colony.Pawns.Nav.Rebuild();
        };

        /// <summary>Straight back, one tile: orthogonal and diagonal, in every direction.</summary>
        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        public void AKnockbackPutsItOneTileStraightBack(int dx, int dz)
        {
            var (colony, a, t) = Pair(dx, dz);
            int from = t.Cell, tick = colony.World.CurrentTick;
            var tape = new Tape();
            Blow(colony, a, t, Knock());
            tape.Tick(colony, 1);

            Assert.That(t.Cell, Is.EqualTo(At(colony, 2 * dx, 2 * dz)), "not one tile straight back");
            var mine = tape.Events.Where(e => e.Target == t.Id).ToList();
            Assert.That(mine.Select(e => e.Kind), Is.EqualTo(new[] { CombatEventKind.Hit, CombatEventKind.Critical, CombatEventKind.KnockedBack }));
            Assert.That(mine.All(e => e.Tick == tick && e.Attacker == a.Id), Is.True);
            Assert.That(mine[1].Amount, Is.EqualTo(0), "the Critical carries no amount");
            Assert.That(mine[2].Cell, Is.EqualTo(Size.FromIndex(t.Cell)), "KnockedBack names where it landed");
            Assert.That(mine[2].Amount, Is.EqualTo(from), "KnockedBack names where it came from");
        }

        /// <summary>The control for every refusal below: a critical that did not roll its knockback leaves it where it was.</summary>
        [Test]
        public void ACriticalWithoutItsKnockbackOnlyStaggers()
        {
            var (colony, a, t) = Pair(1, 0);
            int from = t.Cell;
            var tape = new Tape();
            Blow(colony, a, t, Knock(knockback: false));
            tape.Tick(colony, 1);
            Assert.That(t.Cell, Is.EqualTo(from));
            Assert.That(tape.Of(CombatEventKind.Critical), Has.Count.EqualTo(1), "the stagger is drawn off the Critical");
            Assert.That(tape.Of(CombatEventKind.KnockedBack), Is.Empty);
        }

        /// <summary>
        /// It lies where it landed for 90 ticks — no job, no step, <see cref="PawnFlags.KnockedDown"/>
        /// in every frame — and on the ninetieth it stands and its mind gives it something to do.
        /// With the job pipeline's hold withheld it is given a job the tick after it lands (measured).
        /// </summary>
        [Test]
        public void ItLiesThereForASecondAndAHalfThenStands()
        {
            var (colony, a, t) = Pair(1, 0);
            Blow(colony, a, t, Knock());
            int land = t.Cell;
            Assert.That(land, Is.EqualTo(At(colony, 2, 0)));
            int ticks = colony.Pawns.Content.Combat.knockedDownTicks;
            Assert.That(ticks, Is.EqualTo(90));

            for (int i = 0; i < ticks; i++)
            {
                colony.World.Tick();
                Assert.That(t.Cell, Is.EqualTo(land), $"tick {i}: it moved while down");
                Assert.That(t.CurrentJob, Is.Null, $"tick {i}: it was given a job while down");
                Assert.That(t.MoveProgress, Is.EqualTo(0));
                Assert.That(FlagsOf(colony, t) & PawnFlags.KnockedDown, Is.EqualTo(PawnFlags.KnockedDown), $"tick {i}: not published");
            }

            colony.World.Tick();
            Assert.That(FlagsOf(colony, t) & PawnFlags.KnockedDown, Is.EqualTo(PawnFlags.None), "still down after 90 ticks");
            Assert.That(t.CurrentJob, Is.Not.Null, "it stood and did nothing");
            Assert.That(t.KnockedDownUntilTick, Is.EqualTo(0), "the clock did not run back to nought");
            Assert.That(t.Drafted, Is.True, "the knockback undrafted her");
        }

        /// <summary>One terrace step down it may go: off the edge of a raised patch, landing a layer lower.</summary>
        [Test]
        public void OneTerraceStepDown()
        {
            var (colony, a, t) = Pair(1, 0, dy: 1, ground: Plateau(1));
            Blow(colony, a, t, Knock());
            Assert.That(t.Cell, Is.EqualTo(At(colony, 2, 0, 0)), "not knocked off the step on to the ground below");
        }

        /// <summary>Never two layers down: off a patch raised two, it stays on top.</summary>
        [Test]
        public void NeverTwoLayersDown()
        {
            var (colony, a, t) = Pair(1, 0, dy: 2, ground: Plateau(2));
            int from = t.Cell;
            var tape = new Tape();
            Blow(colony, a, t, Knock());
            tape.Tick(colony, 1);
            Assert.That(t.Cell, Is.EqualTo(from), "knocked off a two-layer drop");
            Assert.That(tape.Of(CombatEventKind.KnockedBack), Is.Empty);
            Assert.That(tape.Of(CombatEventKind.Critical), Has.Count.EqualTo(1));
        }

        /// <summary>Never up: a rise behind it stops it where it is.</summary>
        [Test]
        public void NeverUp()
        {
            var (colony, a, t) = Pair(1, 0, ground: c =>
            {
                for (int dz = -1; dz <= 1; dz++) Paint(c, At(c, 2, dz), NaturalContent.TerrainGrass);
                c.Pawns.Nav.Rebuild();
            });
            int from = t.Cell;
            Blow(colony, a, t, Knock());
            Assert.That(t.Cell, Is.EqualTo(from), "knocked up on to a rise");
        }

        /// <summary>Never into water: a stream behind it stops it where it is.</summary>
        [Test]
        public void NeverIntoWater()
        {
            var (colony, a, t) = Pair(1, 0, ground: c =>
            {
                Paint(c, At(c, 2, 0), NaturalContent.TerrainShallowWater);
                c.Pawns.Nav.Rebuild();
            });
            int from = t.Cell;
            Assert.That(colony.Pawns.Nav.IsLegalStep(from, At(colony, 2, 0), t.Species.traverseMode), Is.True,
                "the control: she could wade into it");
            Blow(colony, a, t, Knock());
            Assert.That(t.Cell, Is.EqualTo(from), "knocked into water");
        }

        /// <summary>Never off a step into water below either.</summary>
        [Test]
        public void NeverDownIntoWater()
        {
            var (colony, a, t) = Pair(1, 0, dy: 1, ground: c =>
            {
                Plateau(1)(c);
                Paint(c, At(c, 2, 0), NaturalContent.TerrainShallowWater);
                c.Pawns.Nav.Rebuild();
            });
            int from = t.Cell;
            Blow(colony, a, t, Knock());
            Assert.That(t.Cell, Is.EqualTo(from), "knocked off a step into water");
        }

        /// <summary>
        /// Never on to a tile another fighter holds (§8c's rule): a colonist attacking the target
        /// from behind it holds that tile. The control is the same colonist standing there without
        /// fighting, which is no fighter's tile, and the blow knocks the target on to it.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void NeverOnToATileAFighterHolds(bool fighting)
        {
            var colony = Board(colonists: 3);
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], t = colony.Pawns.Pawns.All[1], c = colony.Pawns.Pawns.All[2];
            foreach (Pawn p in new[] { a, t, c }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            Stand(colony, a, At(colony, 0, 0));
            Stand(colony, t, At(colony, 1, 0));
            Stand(colony, c, At(colony, 2, 0));
            if (fighting)
            {
                Assert.That(Attack(colony, c, t), Is.EqualTo(IntentRejection.None));
                Assume.That(Melee.IsInAnAttack(c) && c.Cell == At(colony, 2, 0) && t.Cell == At(colony, 1, 0), Is.True,
                    "the fixture: she is not attacking from behind it");
            }
            Blow(colony, a, t, Knock());
            if (fighting) Assert.That(t.Cell, Is.EqualTo(At(colony, 1, 0)), "knocked on to a fighter's tile");
            else Assert.That(t.Cell, Is.EqualTo(At(colony, 2, 0)), "the control: nobody fighting holds it");
        }

        /// <summary>
        /// Never on to the tile of the one it is itself fighting. <see cref="Melee.Holds"/> does not
        /// count a pawn's own claims against her, and her target's tile is held only by her claim,
        /// so the knockback could lay her on it — and a knocked-down fighter lies where she lands.
        /// Found by <c>FightGuardTests.MixedBrawlsOnManySeeds</c> once bandits carried crowbars
        /// (design 42): a drafted colonist beating a rat was knocked on to the rat by a bandit's
        /// critical and lay there 90 ticks. The control is the same colonist behind her, not being
        /// fought, whose tile the blow does knock her on to.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void NeverOnToTheTileOfTheOneItIsFighting(bool fighting)
        {
            // A rat, as in the sweep: it does not fight back, so its tile is held by her claim
            // alone. A colonist behind her would turn on her and hold its own side.
            var colony = Board(colonists: 2);
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], t = colony.Pawns.Pawns.All[1];
            foreach (Pawn p in new[] { a, t }) Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
            Stand(colony, a, At(colony, 0, 0));
            Stand(colony, t, At(colony, 1, 0));
            Pawn c = Spawn(colony, PawnKindIndex.DuctRat, At(colony, 2, 0));
            if (fighting)
            {
                Assert.That(Attack(colony, t, c), Is.EqualTo(IntentRejection.None));
                Assume.That(Melee.IsInAnAttack(t) && t.Cell == At(colony, 1, 0) && c.Cell == At(colony, 2, 0), Is.True,
                    "the fixture: she is not fighting the rat behind her");
                Assume.That(Melee.IsInAnAttack(c), Is.False, "the fixture: the rat fights back");
            }
            Blow(colony, a, t, Knock());
            if (fighting) Assert.That(t.Cell, Is.EqualTo(At(colony, 1, 0)), "knocked on to the tile of the one she fights");
            else Assert.That(t.Cell, Is.EqualTo(At(colony, 2, 0)), "the control: nobody she fights stands there");
        }

        /// <summary>
        /// The downed and the dead are not knocked back: the fall and the death are resolved first.
        /// The control is the same blow one thousandth short of downing.
        /// </summary>
        [Test]
        public void TheDownedAndTheDeadAreNotKnockedBack()
        {
            var (colony, a, t) = Pair(1, 0);
            int from = t.Cell;
            Blow(colony, a, t, Knock(t.HpMilli));
            Assert.That(t.Downed, Is.True);
            Assert.That(t.Cell, Is.EqualTo(from), "a downed colonist was knocked back");
            Assert.That(t.KnockedDownUntilTick, Is.EqualTo(0));

            var (c2, a2, t2) = Pair(1, 0);
            Blow(c2, a2, t2, Knock(t2.HpMilli - t2.DeathAtMilli));
            Assert.That(Melee.IsDead(t2), Is.True);
            Assert.That(t2.Cell, Is.EqualTo(At(c2, 1, 0)), "a dead colonist was knocked back");

            var (c3, a3, t3) = Pair(1, 0);
            Blow(c3, a3, t3, Knock(t3.HpMilli - 1));
            Assert.That(t3.Cell, Is.EqualTo(At(c3, 2, 0)), "the control: standing, she is knocked back");
        }

        /// <summary>An animal is knocked back too: a hog, off its tile and lying for the same time.</summary>
        [Test]
        public void AnAnimalIsKnockedBackToo()
        {
            var (colony, a, hog) = Pair(0, 1, targetKind: PawnKindIndex.MiddenHog);
            Blow(colony, a, hog, Knock());
            Assert.That(hog.Cell, Is.EqualTo(At(colony, 0, 2)));
            Assert.That(hog.KnockedDownAt(colony.World.CurrentTick), Is.True);
        }

        /// <summary>
        /// A drafted colonist ordered on to a foe goes back at it when she stands: the order
        /// outlives the fall, and waits out the knock-down with her. The hold's own blow does not
        /// need it — her hold comes back from the draft.
        /// </summary>
        [Test]
        public void AnAttackOrderOutlivesTheFall()
        {
            var (colony, a, t) = Pair(1, 0);
            Assert.That(Attack(colony, t, a), Is.EqualTo(IntentRejection.None));
            Assume.That(t.Cell, Is.EqualTo(At(colony, 1, 0)));
            Blow(colony, a, t, Knock());
            Assert.That(t.Cell, Is.EqualTo(At(colony, 2, 0)));
            Assert.That(t.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the order was lost with the fall");
            Assert.That(t.CurrentJob!.PlayerForced && t.CombatTarget == a.Id.Value, Is.True);
            int land = t.Cell;
            colony.World.Tick(60);
            Assert.That(t.Cell, Is.EqualTo(land), "she went back at it while still down");
            for (int i = 0; i < 300 && !Melee.InReach(colony.Pawns, t, a, t.Species.traverseMode); i++) colony.World.Tick();
            Assert.That(Melee.InReach(colony.Pawns, t, a, t.Species.traverseMode), Is.True, "she never went back at it");
        }

        /// <summary>
        /// A save taken while a pawn lies knocked down resumes on an equal hash, and 300 ticks on it
        /// still agrees. The control is the same save with the knock-down forgotten, which stands up
        /// at once and parts from the original.
        /// </summary>
        [Test]
        public void ASaveTakenMidKnockDownResumesTheSame()
        {
            var (colony, a, t) = Pair(1, 0);
            Blow(colony, a, t, Knock());
            colony.World.Tick(30);
            Assert.That(t.KnockedDownAt(colony.World.CurrentTick), Is.True);

            byte[] saved = colony.Save();
            var restored = Board(colonists: 2);
            restored.Load(saved);
            var forgetful = Board(colonists: 2);
            forgetful.Load(saved);
            forgetful.Pawns.Pawns.Get(t.Id)!.KnockedDownUntilTick = 0;

            Assert.That(restored.Pawns.Pawns.Get(t.Id)!.KnockedDownUntilTick, Is.EqualTo(t.KnockedDownUntilTick));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(300);
            restored.World.Tick(300);
            forgetful.World.Tick(300);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the knock-down resumed differently");
            Assert.That(forgetful.World.ComputeStateHash().Value, Is.Not.EqualTo(colony.World.ComputeStateHash().Value),
                "the control: a forgotten knock-down resumes the same, so this test could not see one");
        }

        /// <summary>
        /// The combat section's layout 2 still loads (design 33 §9b): its pawn comes back with nobody
        /// knocked down and no swing in the air, whatever the pawn held before. The control is the
        /// same record at layout 3, whose four new fields are read.
        /// </summary>
        [TestCase(2)]
        [TestCase(3)]
        public void AnOlderCombatSectionLoadsWithNobodyKnockedDown(int layout)
        {
            var colony = Board(colonists: 2);
            Pawn pawn = colony.Pawns.Pawns.All[0];

            var bytes = new MemoryStream();
            using (var binary = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var writer = new SaveWriter(binary);
                writer.Write(layout);
                writer.Write(1);
                writer.Write(pawn.Id.Value);
                writer.Write(0);         // flags
                writer.Write(0);         // quiet since
                writer.Write(-1);        // no finishing step
                writer.Write(40_000);    // hit points
                writer.Write(0);         // next swing
                writer.Write(0);         // stun
                writer.Write(0);         // retaliation
                writer.Write(0);         // its end
                writer.Write(0);         // weapon
                writer.Write(0);         // target
                writer.Write(0);         // carrier
                if (layout >= 3)
                {
                    writer.Write(77);                 // knocked down until
                    writer.Write((1 << 16) | 2);      // a hit in the air
                    writer.Write(4_500);
                    writer.Write(0);
                }
            }
            bytes.Position = 0;
            pawn.KnockedDownUntilTick = 99;
            pawn.PendingSwing = 5;
            new CombatSection(colony.Pawns.Pawns).Load(
                new SaveReader(new BinaryReader(bytes), WorldSave.CurrentFormatVersion));

            Assert.That(pawn.HpMilli, Is.EqualTo(40_000));
            if (layout < 3)
            {
                Assert.That(pawn.KnockedDownUntilTick, Is.EqualTo(0));
                Assert.That(pawn.HasPendingSwing, Is.False);
                return;
            }
            Assert.That(pawn.KnockedDownUntilTick, Is.EqualTo(77));
            Assert.That(pawn.HeldSwing.Landed && pawn.HeldSwing.DamageMilli == 4_500, Is.True);
        }

        /// <summary>
        /// A pawn knocked back with a load in its arms puts it down through its job's own cleanup,
        /// where the blow found it, and lets go of what it had claimed.
        /// </summary>
        [Test]
        public void AHaulerKnockedBackPutsHerLoadDown()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.stockpileCells = 9;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick();
            Pawn hauler = colony.Pawns.Pawns.All[0];
            ThingId scrap = colony.Pawns.Items.Spawn(ItemIndex.Salvage, Near(colony, 8, 3));
            for (int i = 0; i < 3_000 && hauler.CurrentJob?.CarriedItem != scrap.Value; i++) colony.World.Tick();
            Assume.That(hauler.CurrentJob?.CarriedItem, Is.EqualTo(scrap.Value), "the fixture: she never picked it up");

            // Struck from whichever side leaves open ground behind her.
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -15, -15));
            CellRef h = Size.FromIndex(hauler.Cell);
            int struckAt = hauler.Cell;
            bool knocked = false;
            foreach (var (dx, dz) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int stand = Size.Index(h.X + dx, h.Z + dz, h.Y);
                if (!colony.Pawns.Cells.IsWalkable(stand)) continue;
                bandit.Cell = stand;
                if (CombatSystem.KnockbackCell(colony.Pawns, bandit, hauler) < 0) continue;
                colony.Pawns.Combat!.ApplySwing(bandit, hauler, Fists(colony.Pawns), Knock(), colony.World.CurrentTick);
                knocked = true;
                break;
            }
            Assume.That(knocked, Is.True, "the fixture: nowhere to knock her");

            Assert.That(hauler.Cell, Is.Not.EqualTo(struckAt));
            ColonyItem item = colony.Pawns.Items.Get(scrap)!;
            Assert.That(item.Cell, Is.GreaterThanOrEqualTo(0), "the load was not put down");
            Assert.That(colony.Pawns.Distance(item.Cell, struckAt), Is.LessThanOrEqualTo(colony.Pawns.Distance(item.Cell, hauler.Cell)),
                "the load was put down where she landed, not where she was struck");
            Assert.That(hauler.HeldReservations, Is.Empty, "her claims were kept");
        }
    }
}
