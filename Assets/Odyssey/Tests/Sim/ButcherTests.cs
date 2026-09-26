#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The butcher (design 62): a hostile person of its own species, one cell drawn huge, whose
    /// swing sweeps the front arc of three and usually flings whoever it lands on two cells back —
    /// slamming into what stops it, falling off a ledge. Never stunned, never knocked back, and not
    /// downed by a colonist's pain. The blows are landed exactly through
    /// <see cref="CombatSystem.ApplySwing"/> and <see cref="CombatSystem.SweepFlanks"/> so each case
    /// is about the rule and the ground, not the dice; the last cases run the real fight.
    /// </summary>
    public class ButcherTests
    {
        // ---- fixture ------------------------------------------------------------------------

        /// <summary>A cell <paramref name="dx"/>, <paramref name="dz"/>, <paramref name="dy"/> from a spot clear of the starting kit.</summary>
        static int At(ColonyWorld colony, int dx, int dz, int dy = 0)
        {
            CellRef s = colony.Start;
            return Size.Index(s.X + 10 + dx, s.Z + 10 + dz, s.Y + dy);
        }

        /// <summary>
        /// A butcher at (0, 0) and <paramref name="colonists"/> drafted colonists, all stood well
        /// away until a test places them. The ground is shaped first, then the graph rebuilt.
        /// </summary>
        static (ColonyWorld colony, Pawn butcher, Pawn[] people) Scene(int colonists, int dy = 0,
            System.Action<ColonyWorld>? ground = null)
        {
            var colony = Board(colonists: colonists);
            colony.World.Tick();
            ground?.Invoke(colony);
            Pawn[] people = colony.Pawns.Pawns.All.Take(colonists).ToArray();
            for (int i = 0; i < people.Length; i++)
            {
                Assert.That(Draft(colony, people[i]), Is.EqualTo(IntentRejection.None));
                Stand(colony, people[i], At(colony, -14 + i, -14));
            }
            Pawn butcher = Spawn(colony, PawnKindIndex.Butcher, At(colony, 0, 0, dy));
            Stand(colony, butcher, At(colony, 0, 0, dy));
            return (colony, butcher, people);
        }

        /// <summary>The butcher's own cleaver.</summary>
        static Armament Cleaver(ColonyWorld colony, Pawn butcher) =>
            colony.Pawns.WeaponRules.ArmamentOf(butcher, colony.Pawns).Melee;

        /// <summary>A blow that lands for this much and flings.</summary>
        static SwingOutcome Fling(int damageMilli = 1_000, bool knockback = true) =>
            new SwingOutcome(CombatEventKind.Hit, damageMilli, 0, critical: false, knockback: knockback);

        static void Blow(ColonyWorld colony, Pawn by, Pawn target, SwingOutcome outcome) =>
            colony.Pawns.Combat!.ApplySwing(by, target, Cleaver(colony, by), outcome, colony.World.CurrentTick);

        static SweepDef Sweep(ColonyWorld colony) => colony.Pawns.Content.Species[4].sweep!;

        /// <summary>Paint a cell's terrain and tell the graph, the way <c>KnockbackTests</c> does.</summary>
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

        /// <summary>A wall — rock terrain, two layers tall so nothing steps up on to it — at (dx, dz).</summary>
        static System.Action<ColonyWorld> WallAt(int dx, int dz) => colony =>
        {
            Paint(colony, At(colony, dx, dz), NaturalContent.TerrainGrass);
            Paint(colony, At(colony, dx, dz, 1), NaturalContent.TerrainGrass);
            colony.Pawns.Nav.Rebuild();
        };

        /// <summary>Raise the ground under x −1..1, z −1..1 by <paramref name="layers"/>, so it stands that much higher.</summary>
        static System.Action<ColonyWorld> Plateau(int layers) => colony =>
        {
            for (int y = 0; y < layers; y++)
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                Paint(colony, At(colony, dx, dz, y), NaturalContent.TerrainGrass);
            colony.Pawns.Nav.Rebuild();
        };

        // ---- what it is ---------------------------------------------------------------------

        [Test]
        public void TheButcherIsKindSixAHostilePersonOfItsOwnSpecies()
        {
            PawnContent content = ContentPack.Pawns();
            Assert.That(content.Kinds[PawnKindIndex.Butcher].defName, Is.EqualTo("PawnKind_Butcher"));
            Assert.That(content.KindOf(PawnKindIndex.Butcher).faction, Is.EqualTo(Faction.Hostile));
            SpeciesDef species = content.SpeciesOf(PawnKindIndex.Butcher);
            Assert.That(species.defName, Is.EqualTo("Species_Butcher"));
            Assert.That(species.person, Is.True);
            Assert.That(species.unstoppable, Is.True);
            Assert.That(species.sweep, Is.Not.Null);
            Assert.That(species.sweep!.distance, Is.EqualTo(2), "the owner's two cells");
            Assert.That(content.ModeOf(PawnKindIndex.Butcher), Is.EqualTo(TraverseMode.Animal),
                "bulky: no ladder, no door it opens (design 62 §3)");
            Assert.That(content.HealthOf(PawnKindIndex.Butcher)!.defName, Is.EqualTo("Health_Brute"));
            Assert.That(species.naturalAttack!.damageKind, Is.EqualTo(DamageKind.Sharp));

            // Nobody else sweeps, nobody else is unstoppable.
            for (int s = 0; s < content.Species.Length; s++)
                if (content.Species[s] != species)
                    Assert.That(content.Species[s].sweep == null && !content.Species[s].unstoppable, Is.True,
                        content.Species[s].defName);
        }

        [Test]
        public void ASpawnedButcherHasItsPoolItsCleaverAndItsFixedLevel()
        {
            var (colony, butcher, _) = Scene(1);
            Assert.That(butcher.HpMaxMilli, Is.EqualTo(500_000));
            Assert.That(butcher.IsHostile && butcher.IsPerson, Is.True);
            Armament armament = Cleaver(colony, butcher);
            Assert.That(armament.Attack, Is.SameAs(butcher.Species.naturalAttack), "the cleaver is its own, not an item");
            Assert.That(armament.ItemDef, Is.EqualTo(-1));
            Assert.That(colony.Pawns.MeleeRules.MeleeLevel(butcher), Is.EqualTo(14),
                "a species that names a level fights at it, however the person rolled");
        }

        [Test]
        public void AColonistsLevelIsStillHerSkill()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn her = colony.Pawns.Pawns.All[0];
            SetMelee(her, 3);
            Assert.That(colony.Pawns.MeleeRules.MeleeLevel(her), Is.EqualTo(3));
        }

        /// <summary>
        /// A colonist is down from pain at 64 points of live injury (design 43 §3); the brute's body
        /// takes 320 (design 62 §6). Seventy points in blows of ten: the colonist is the control.
        /// </summary>
        [Test]
        public void SeventyPointsDownAColonistAndNotTheButcher()
        {
            var (colony, butcher, people) = Scene(1);
            int tick = colony.World.CurrentTick;
            for (int i = 0; i < 7; i++)
            {
                colony.Pawns.Combat!.Hurt(butcher, null, 10_000, AfflictionKind.Wound, HitSet.Melee, -1, tick);
                colony.Pawns.Combat!.Hurt(people[0], null, 10_000, AfflictionKind.Wound, HitSet.Melee, -1, tick);
            }
            Assert.That(people[0].Downed, Is.True, "the control: a colonist goes down");
            Assert.That(butcher.Downed, Is.False, "the butcher went down from a colonist's pain");
        }

        [Test]
        public void ItIsNeverStunnedAndNeverKnockedBack()
        {
            var (colony, butcher, people) = Scene(1);
            Pawn her = people[0];
            Stand(colony, her, At(colony, -1, 0));
            int from = butcher.Cell;
            colony.Pawns.Combat!.ApplySwing(her, butcher, Fists(colony.Pawns),
                new SwingOutcome(CombatEventKind.Hit, 1_000, 90, critical: true, knockback: true), colony.World.CurrentTick);
            Assert.That(butcher.StunnedUntilTick, Is.EqualTo(0), "stunned");
            Assert.That(butcher.Cell, Is.EqualTo(from), "knocked back");
            Assert.That(butcher.KnockedDownUntilTick, Is.EqualTo(0), "knocked down");
        }

        // ---- the fling ----------------------------------------------------------------------

        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(-1, -1)]
        [TestCase(1, -1)]
        public void AFlingCarriesItTwoCellsStraightBack(int dx, int dz)
        {
            var (colony, butcher, people) = Scene(1);
            Pawn her = people[0];
            Stand(colony, her, At(colony, dx, dz));
            int from = her.Cell, tick = colony.World.CurrentTick;
            var tape = new Tape();
            Blow(colony, butcher, her, Fling());
            tape.Tick(colony, 1);

            Assert.That(her.Cell, Is.EqualTo(At(colony, 3 * dx, 3 * dz)), "not two cells straight back");
            SweepDef sweep = Sweep(colony);
            Assert.That(her.KnockedDownUntilTick, Is.EqualTo(tick + sweep.knockedDownTicks));
            Assert.That(her.KnockbackImmuneUntilTick, Is.EqualTo(tick + sweep.immunityTicks));
            var back = tape.Of(CombatEventKind.KnockedBack).Single();
            Assert.That(back.Amount, Is.EqualTo(from));
            Assert.That(tape.Of(CombatEventKind.Slam), Is.Empty, "slammed into open ground");
        }

        [Test]
        public void AFlingThatDidNotRollLeavesItWhereItWas()
        {
            var (colony, butcher, people) = Scene(1);
            Pawn her = people[0];
            Stand(colony, her, At(colony, 1, 0));
            int from = her.Cell;
            Blow(colony, butcher, her, Fling(knockback: false));
            Assert.That(her.Cell, Is.EqualTo(from));
            Assert.That(her.KnockbackImmuneUntilTick, Is.EqualTo(0));
        }

        /// <summary>A wall right behind her: she does not move, is still knocked down, and takes the slam on top of the blow.</summary>
        [Test]
        public void AWallBehindHerSlamsHerWhereSheStands()
        {
            var (colony, butcher, people) = Scene(1, ground: WallAt(2, 0));
            Pawn her = people[0];
            MakeWhole(her);
            Stand(colony, her, At(colony, 1, 0));
            int from = her.Cell, hp = her.HpMilli, tick = colony.World.CurrentTick;
            var tape = new Tape();
            Blow(colony, butcher, her, Fling(1_000));
            tape.Tick(colony, 1);

            Assert.That(her.Cell, Is.EqualTo(from), "flung through a wall");
            Assert.That(her.KnockedDownUntilTick, Is.EqualTo(tick + Sweep(colony).knockedDownTicks), "not knocked down");
            Assert.That(hp - her.HpMilli, Is.EqualTo(1_000 + Sweep(colony).slamDamage * 1_000), "the blow and the slam");
            Assert.That(tape.Of(CombatEventKind.Slam).Single().Target, Is.EqualTo(her.Id));
        }

        [Test]
        public void OneCellThenAWallLandsHerAgainstIt()
        {
            var (colony, butcher, people) = Scene(1, ground: WallAt(3, 0));
            Pawn her = people[0];
            MakeWhole(her);
            Stand(colony, her, At(colony, 1, 0));
            int hp = her.HpMilli;
            Blow(colony, butcher, her, Fling(1_000));
            Assert.That(her.Cell, Is.EqualTo(At(colony, 2, 0)));
            Assert.That(hp - her.HpMilli, Is.EqualTo(1_000 + Sweep(colony).slamDamage * 1_000));
        }

        [Test]
        public void ABodyInTheWaySlamsThemBoth()
        {
            var (colony, butcher, people) = Scene(2);
            Pawn her = people[0], him = people[1];
            MakeWhole(her);
            MakeWhole(him);
            Stand(colony, her, At(colony, 1, 0));
            Stand(colony, him, At(colony, 2, 0));
            int hers = her.HpMilli, his = him.HpMilli, slam = Sweep(colony).slamDamage * 1_000;
            var tape = new Tape();
            Blow(colony, butcher, her, Fling(1_000));
            tape.Tick(colony, 1);

            Assert.That(her.Cell, Is.EqualTo(At(colony, 1, 0)), "flung through a body");
            Assert.That(hers - her.HpMilli, Is.EqualTo(1_000 + slam));
            Assert.That(his - him.HpMilli, Is.EqualTo(slam), "the body in the way took nothing");
            Assert.That(tape.Of(CombatEventKind.Slam).Select(e => e.Target), Is.EquivalentTo(new[] { her.Id, him.Id }));
        }

        [Test]
        public void WaterStopsHerShortWithNoSlam()
        {
            var (colony, butcher, people) = Scene(1, ground: c =>
            {
                Paint(c, At(c, 3, 0), NaturalContent.TerrainShallowWater);
                c.Pawns.Nav.Rebuild();
            });
            Pawn her = people[0];
            MakeWhole(her);
            Stand(colony, her, At(colony, 1, 0));
            int hp = her.HpMilli;
            Blow(colony, butcher, her, Fling(1_000));
            Assert.That(her.Cell, Is.EqualTo(At(colony, 2, 0)), "not stopped at the water's edge");
            Assert.That(hp - her.HpMilli, Is.EqualTo(1_000), "slammed by water");
        }

        /// <summary>
        /// Off a patch raised two layers: the one-tile knockback never goes (KnockbackTests), a fling
        /// falls to the ground and takes the fall (design 43 §7) on top of the blow.
        /// </summary>
        [Test]
        public void OffATwoLayerLedgeSheFalls()
        {
            var (colony, butcher, people) = Scene(1, dy: 2, ground: Plateau(2));
            Pawn her = people[0];
            MakeWhole(her);
            Stand(colony, her, At(colony, 1, 0, 2));
            int hp = her.HpMilli;
            Blow(colony, butcher, her, Fling(1_000));
            Assert.That(her.Cell, Is.EqualTo(At(colony, 2, 0, 0)), "not fallen to the ground below");
            Assert.That(hp - her.HpMilli, Is.GreaterThan(1_000), "the fall did nothing");
        }

        [Test]
        public void OneTerraceStepDownEndsTheFlingWithNoFall()
        {
            var (colony, butcher, people) = Scene(1, dy: 1, ground: Plateau(1));
            Pawn her = people[0];
            MakeWhole(her);
            Stand(colony, her, At(colony, 1, 0, 1));
            int hp = her.HpMilli;
            Blow(colony, butcher, her, Fling(1_000));
            Assert.That(her.Cell, Is.EqualTo(At(colony, 2, 0, 0)));
            Assert.That(hp - her.HpMilli, Is.EqualTo(1_000), "a step down is not a fall");
        }

        /// <summary>Flung once, she cannot be flung again for the immunity — nor knocked back by a critical.</summary>
        [Test]
        public void WhileImmuneSheIsStruckButNotMoved()
        {
            var (colony, butcher, people) = Scene(2);
            Pawn her = people[0], other = people[1];
            Stand(colony, her, At(colony, 1, 0));
            Blow(colony, butcher, her, Fling());
            int landed = her.Cell, hp = her.HpMilli;
            Assert.That(landed, Is.EqualTo(At(colony, 3, 0)));

            Stand(colony, butcher, At(colony, 2, 0));
            Blow(colony, butcher, her, Fling(1_000));
            Assert.That(her.Cell, Is.EqualTo(landed), "flung twice in a row");
            Assert.That(hp - her.HpMilli, Is.EqualTo(1_000), "the blow itself still lands");

            Stand(colony, other, At(colony, 4, 0));
            colony.Pawns.Combat!.ApplySwing(other, her, Fists(colony.Pawns),
                new SwingOutcome(CombatEventKind.Hit, 1_000, 0, critical: true, knockback: true), colony.World.CurrentTick);
            Assert.That(her.Cell, Is.EqualTo(landed), "a critical knocked her back while immune");
        }

        [Test]
        public void TheImmunityRunsBackToNought()
        {
            var (colony, butcher, people) = Scene(1);
            Pawn her = people[0];
            Stand(colony, her, At(colony, 1, 0));
            Blow(colony, butcher, her, Fling());
            int until = her.KnockbackImmuneUntilTick;
            Stand(colony, butcher, At(colony, -12, -12));
            colony.World.Tick(until - colony.World.CurrentTick + 1);
            Assert.That(her.KnockbackImmuneUntilTick, Is.EqualTo(0));
        }

        // ---- the sweep ----------------------------------------------------------------------

        /// <summary>Every flank blow lands for a point: the sweep's geometry, not its dice.</summary>
        sealed class SureFlanks : MeleeRules
        {
            public override SwingOutcome ResolveFlank(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick) =>
                new SwingOutcome(CombatEventKind.Hit, 1_000);
        }

        /// <summary>
        /// Facing east at (1, 0): the flanks are north-east and south-east. Behind, beside and two
        /// cells out are untouched; the target itself is not struck by the sweep a second time.
        /// </summary>
        [Test]
        public void BothFlanksAreStruckAndNothingElse()
        {
            var (colony, butcher, people) = Scene(6);
            colony.Pawns.MeleeRules = new SureFlanks();
            int[] cells =
            {
                At(colony, 1, 0),   // the target
                At(colony, 1, 1),   // flank
                At(colony, 1, -1),  // flank
                At(colony, -1, 0),  // behind
                At(colony, 0, 1),   // beside
                At(colony, 2, 1),   // two out
            };
            for (int i = 0; i < 6; i++)
            {
                MakeWhole(people[i]);
                Stand(colony, people[i], cells[i]);
            }
            int facing = SweepArc.Facing(Size, butcher.Cell, people[0].Cell);
            Assert.That(facing, Is.EqualTo(1));

            colony.Pawns.Combat!.SweepFlanks(butcher, facing, people[0].Id.Value, Cleaver(colony, butcher), colony.World.CurrentTick);

            bool[] struck = people.Select(p => p.HpMilli < p.HpMaxMilli).ToArray();
            Assert.That(struck, Is.EqualTo(new[] { false, true, true, false, false, false }));
        }

        [Test]
        public void FacingNorthEastTheFlanksAreNorthAndEast()
        {
            Assert.That(SweepArc.Left(2), Is.EqualTo(3));
            Assert.That(SweepArc.Right(2), Is.EqualTo(1));
            Assert.That(SweepArc.Left(8), Is.EqualTo(1));
            Assert.That(SweepArc.Right(1), Is.EqualTo(8));
            for (int f = 1; f <= 8; f++)
            {
                var (dx, dz) = SweepArc.Step(f);
                int from = Size.Index(10, 10, 3), to = Size.Index(10 + dx, 10 + dz, 3);
                Assert.That(SweepArc.Facing(Size, from, to), Is.EqualTo(f));
            }
            Assert.That(SweepArc.Facing(Size, Size.Index(10, 10, 3), Size.Index(10, 10, 3)), Is.EqualTo(0));
            Assert.That(SweepArc.Facing(Size, Size.Index(10, 10, 3), Size.Index(11, 10, 4)), Is.EqualTo(0));
        }

        /// <summary>The gang is not cleaved: a bandit in the arc is spared, and a colonist down on the ground is left there.</summary>
        [Test]
        public void AHostileAndTheDownedAreSpared()
        {
            var (colony, butcher, people) = Scene(1);
            colony.Pawns.MeleeRules = new SureFlanks();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, At(colony, 1, 1));
            Stand(colony, bandit, At(colony, 1, 1));
            Pawn her = people[0];
            Stand(colony, her, At(colony, 1, -1));
            MakeWhole(bandit);
            her.HpMilli = -1_000;
            her.Downed = true;
            int hers = her.HpMilli;

            colony.Pawns.Combat!.SweepFlanks(butcher, 1, 0, Cleaver(colony, butcher), colony.World.CurrentTick);
            Assert.That(bandit.HpMilli, Is.EqualTo(bandit.HpMaxMilli), "the gang was cleaved");
            Assert.That(her.HpMilli, Is.EqualTo(hers), "the downed were struck");
        }

        /// <summary>
        /// Each victim rolls on its own streams (design 62 §5): two flank victims at the same tick,
        /// by the same swinger, do not share their dice. Over a hundred ticks their outcomes differ.
        /// </summary>
        [Test]
        public void EachFlankVictimRollsItsOwnDice()
        {
            var (colony, butcher, people) = Scene(2);
            var rules = new MeleeRules();
            Armament cleaver = Cleaver(colony, butcher);
            int differ = 0;
            for (int tick = 1; tick <= 100; tick++)
            {
                SwingOutcome a = rules.ResolveFlank(butcher, people[0], cleaver, colony.Pawns, tick);
                SwingOutcome b = rules.ResolveFlank(butcher, people[1], cleaver, colony.Pawns, tick);
                SwingOutcome p = rules.Resolve(butcher, people[0], cleaver, colony.Pawns, tick);
                if (a.Result != b.Result || a.DamageMilli != b.DamageMilli) differ++;
                if (a.DamageMilli == p.DamageMilli && a.Result == p.Result && a.Knockback == p.Knockback && a.Landed)
                    Assert.That(a.DamageMilli, Is.Not.EqualTo(0));
            }
            Assert.That(differ, Is.GreaterThan(50), "the flank victims share their dice");
        }

        /// <summary>Its landed blows fling about three times in four, where a person's only on a critical.</summary>
        [Test]
        public void MostOfItsLandedBlowsFling()
        {
            var (colony, butcher, people) = Scene(1);
            var rules = new MeleeRules();
            Armament cleaver = Cleaver(colony, butcher);
            int landed = 0, flung = 0;
            for (int tick = 1; tick <= 4_000; tick++)
            {
                SwingOutcome o = rules.Resolve(butcher, people[0], cleaver, colony.Pawns, tick);
                if (!o.Landed) continue;
                landed++;
                if (o.Knockback) flung++;
            }
            Assert.That(landed, Is.GreaterThan(2_000));
            Assert.That(flung * 1_000 / landed, Is.InRange(700, 800));
        }

        /// <summary>A bandit's swing carries no facing, so its word — saved and hashed — is what it always was.</summary>
        [Test]
        public void OnlyASweepKeepsAFacing()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick();
            Pawn her = colony.Pawns.Pawns.All[0];
            Assert.That(Draft(colony, her), Is.EqualTo(IntentRejection.None));
            Stand(colony, her, At(colony, 1, 0));
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, At(colony, 0, 0));
            Stand(colony, bandit, At(colony, 0, 0));
            for (int i = 0; i < 400 && !bandit.HasPendingSwing; i++) colony.World.Tick();
            Assume.That(bandit.HasPendingSwing, Is.True, "the bandit never swung");
            Assert.That(bandit.HeldFacing, Is.EqualTo(0));
        }

        // ---- the real fight -----------------------------------------------------------------

        /// <summary>
        /// Three colonists drafted in a row in front of it: its own mind picks one, winds up, and at
        /// the impact the cleaver reaches at least two of them — the target and a flank.
        /// </summary>
        [Test]
        public void InAFightItsSwingReachesMoreThanOne()
        {
            var (colony, butcher, people) = Scene(3);
            for (int i = 0; i < 3; i++) Stand(colony, people[i], At(colony, 1, i - 1));
            var tape = new Tape();
            int first = -1;
            for (int i = 0; i < 600 && first < 0; i++)
            {
                tape.Tick(colony, 1);
                var mine = tape.Events.Where(e => e.Attacker == butcher.Id
                    && (e.Kind == CombatEventKind.Hit || e.Kind == CombatEventKind.Miss || e.Kind == CombatEventKind.Dodge)).ToList();
                if (mine.Count > 0) first = mine[0].Tick;
            }
            Assert.That(first, Is.GreaterThanOrEqualTo(0), "it never swung");
            var struckAtOnce = tape.Events.Where(e => e.Attacker == butcher.Id && e.Tick == first
                && (e.Kind == CombatEventKind.Hit || e.Kind == CombatEventKind.Miss || e.Kind == CombatEventKind.Dodge))
                .Select(e => e.Target).Distinct().ToList();
            Assert.That(struckAtOnce.Count, Is.GreaterThanOrEqualTo(2), "one swing reached one colonist");
        }

        /// <summary>A save taken mid-wind-up keeps the facing and the immunity, and the two worlds hash the same.</summary>
        [Test]
        public void TheFacingAndTheImmunitySurviveASave()
        {
            var (colony, butcher, people) = Scene(2);
            Stand(colony, people[0], At(colony, 1, 0));
            for (int i = 0; i < 600 && !butcher.HasPendingSwing; i++) colony.World.Tick();
            Assume.That(butcher.HasPendingSwing, Is.True, "it never wound up");
            Assert.That(butcher.HeldFacing, Is.InRange(1, 8));
            people[1].KnockbackImmuneUntilTick = colony.World.CurrentTick + 200;

            var restored = Board(colonists: 2);
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(butcher.Id)!;
            Assert.That(back.HeldFacing, Is.EqualTo(butcher.HeldFacing));
            Assert.That(restored.Pawns.Pawns.Get(people[1].Id)!.KnockbackImmuneUntilTick,
                Is.EqualTo(people[1].KnockbackImmuneUntilTick));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(120);
            restored.World.Tick(120);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the two diverged after the swing landed");
        }

        [Test]
        public void TheHashSeesTheImmunity()
        {
            var (colony, _, people) = Scene(1);
            ulong whole = colony.World.ComputeStateHash().Value;
            people[0].KnockbackImmuneUntilTick = 99;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(whole));
            people[0].KnockbackImmuneUntilTick = 0;
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(whole));
        }
    }
}
