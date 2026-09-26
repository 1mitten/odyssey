#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// C6, buildings as targets (design 33 §13): what a target is, its pool, the order, the
    /// driver's building mode, the blow that always lands, demolition with no refund through the
    /// one removal path, the damage row cleared by every route, what is published, and a save
    /// taken with a blow at a wall in the air.
    /// </summary>
    public class BuildingTargetTests
    {
        // ---- the board ----------------------------------------------------------------------

        /// <summary>Order and raise one building at the walkable cell dx, dz from the start, and return its cell.</summary>
        static int Raise(ColonyWorld colony, int building, int dx, int dz, int stuff = StuffHandle.Wood, int facing = 0)
        {
            int cell = Near(colony, dx, dz);
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, facing),
                Is.EqualTo(IntentRejection.None), $"could not order building {building} at {Size.FromIndex(cell)}");
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True, $"building {building} was not raised");
            return cell;
        }

        /// <summary>Two drafted colonists standing apart, a wall six cells east of the start, and a tape.</summary>
        static (ColonyWorld colony, Pawn a, Pawn b, int wall) AWall(int stuff = StuffHandle.Wood)
        {
            var colony = Board();
            colony.World.Tick();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Stand(colony, a, Near(colony, 0, 0));
            Stand(colony, b, Near(colony, 0, 2));
            int wall = Raise(colony, BuildingHandle.Wall, 6, 0, stuff);
            colony.World.Tick();
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, b), Is.EqualTo(IntentRejection.None));
            return (colony, a, b, wall);
        }

        static IntentRejection AttackCell(ColonyWorld colony, Pawn attacker, int cell) =>
            Send(colony, new Intent(IntentKind.OrderAttack, Size.FromIndex(cell), attacker.Id.Value, 0));

        static int OnTheGround(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        static void TickUntil(ColonyWorld colony, System.Func<bool> done, int limit, string what)
        {
            for (int t = 0; t < limit && !done(); t++) colony.World.Tick();
            Assert.That(done(), Is.True, what);
        }

        // ---- what a target is -----------------------------------------------------------------

        /// <summary>
        /// An edifice standing in the cell whose row has hit points (design 33 §13b). Every building
        /// that stands in a cell is one; a two-cell bed or generator answers from either half with
        /// its head; a deck plate — a slab, not an edifice — and bare ground are not.
        /// </summary>
        [Test]
        public void TheTargetsAreWhatStandsInTheCellWithHitPoints()
        {
            var colony = Board();
            colony.World.Tick();
            var ctx = colony.Pawns;

            int wall = Raise(colony, BuildingHandle.Wall, 6, 0);
            int door = Raise(colony, BuildingHandle.Door, 6, 3);
            int shelf = Raise(colony, BuildingHandle.Shelf, 6, 6);
            int campfire = Raise(colony, BuildingHandle.Campfire, 9, 0);
            int heater = Raise(colony, BuildingHandle.Heater, 9, 3);
            int bed = Raise(colony, BuildingHandle.Bed, 12, 0, facing: 1);
            int generator = Raise(colony, BuildingHandle.Generator, 12, 4, facing: 1);
            int plate = Raise(colony, BuildingHandle.DeckPlate, 9, 6);
            int ground = Near(colony, -6, -6);

            foreach (int cell in new[] { wall, door, shelf, campfire, heater, bed, generator })
            {
                Assert.That(BuildingTargets.TryFind(ctx, cell, out BuildingTarget target), Is.True, $"cell {Size.FromIndex(cell)}");
                Assert.That(target.Anchor, Is.EqualTo(cell));
                Assert.That(target.MaxMilli, Is.GreaterThan(0));
            }

            foreach (int head in new[] { bed, generator })
            {
                Assert.That(BuildingTargets.TryFind(ctx, head, out BuildingTarget whole), Is.True);
                Assert.That(whole.Second, Is.GreaterThanOrEqualTo(0), "a two-cell thing has a second cell");
                Assert.That(BuildingTargets.TryFind(ctx, whole.Second, out BuildingTarget other), Is.True, "struck on its far half");
                Assert.That(other.Anchor, Is.EqualTo(head), "either half is the one building, keyed on its head");
                Assert.That(other.Handle, Is.EqualTo(whole.Handle));
            }

            Assert.That(BuildingTargets.TryFind(ctx, plate, out _), Is.False, "a deck plate is a slab, not an edifice");
            Assert.That(BuildingTargets.TryFind(ctx, ground, out _), Is.False, "bare ground");
            Assert.That(BuildingTargets.TryFind(ctx, -1, out _), Is.False);
        }

        /// <summary>
        /// Which edifice ids have hit points, off the content (design 33 §13b): every building that
        /// stands in a cell, and nothing the generator stamps that has no building row — trees,
        /// windows, pillars, stairs.
        /// </summary>
        [Test]
        public void OnlyABuildingRowGivesAnEdificeHitPoints()
        {
            Assert.That(BuildingTargets.MaxHitPointsOf(EdificeHandle.Wall), Is.EqualTo(300));
            Assert.That(BuildingTargets.MaxHitPointsOf(EdificeHandle.Door), Is.EqualTo(160));
            Assert.That(BuildingTargets.MaxHitPointsOf(EdificeHandle.Campfire), Is.EqualTo(60));
            Assert.That(BuildingTargets.MaxHitPointsOf(EdificeHandle.Generator), Is.EqualTo(300));
            Assert.That(BuildingTargets.MaxHitPointsOf(EdificeHandle.Heater), Is.EqualTo(100));
            foreach (int none in new[] { EdificeHandle.None, EdificeHandle.TreeBirch, EdificeHandle.TreeMeadow,
                         EdificeHandle.Window, EdificeHandle.Pillar, EdificeHandle.StairLower })
                Assert.That(BuildingTargets.MaxHitPointsOf((ushort)none), Is.Zero, $"edifice {none}");
        }

        /// <summary>
        /// The pool is the row's points times the material's factor, in thousandths (design 33
        /// §13c): a wooden wall 300, a stone one 450 — the stuff table's 1.5, read at last.
        /// </summary>
        [Test]
        public void APoolIsTheRowTimesItsMaterial()
        {
            var colony = Board();
            colony.World.Tick();
            int wood = Raise(colony, BuildingHandle.Wall, 6, 0, StuffHandle.Wood);
            int stone = Raise(colony, BuildingHandle.Wall, 6, 3, StuffHandle.Stone);

            Assert.That(BuildingTargets.TryFind(colony.Pawns, wood, out BuildingTarget w), Is.True);
            Assert.That(BuildingTargets.TryFind(colony.Pawns, stone, out BuildingTarget s), Is.True);
            Assert.That(w.MaxMilli, Is.EqualTo(300_000));
            Assert.That(s.MaxMilli, Is.EqualTo(450_000));
            Assert.That(BuildingTargets.HpMilli(colony.Pawns, w), Is.EqualTo(300_000), "never struck is whole, with no row");
            Assert.That(colony.Pawns.EdificeDamage.Count, Is.Zero);
        }

        /// <summary>
        /// Blunt against stone, sharp against wood (design 33 §14d; the owner: yes). The numbers are
        /// the material's, in content — wood ×1.25 sharp and ×1 blunt, stone ×0.5 sharp and ×1.25
        /// blunt, everything else ×1 — and <see cref="BuildingTargets.Resolve"/> applies them to the
        /// rolled damage, so the figure held for the impact is the multiplied one. Fists are blunt.
        /// </summary>
        [Test]
        public void TheBlowsKindMeetsTheMaterial()
        {
            var woodDef = ConstructionContent.StuffAt(StuffHandle.Wood);
            var stoneDef = ConstructionContent.StuffAt(StuffHandle.Stone);
            Assert.That(woodDef.sharpDamagePerMille, Is.EqualTo(1_250));
            Assert.That(woodDef.bluntDamagePerMille, Is.EqualTo(1_000));
            Assert.That(stoneDef.sharpDamagePerMille, Is.EqualTo(500));
            Assert.That(stoneDef.bluntDamagePerMille, Is.EqualTo(1_250));
            // Steel since the smelter made it buildable (design 62 §9): an edge barely marks it and
            // a club does less than to stone. INVENTED, like wood's and stone's.
            var steelDef = ConstructionContent.StuffAt(StuffHandle.Steel);
            Assert.That(steelDef.sharpDamagePerMille, Is.EqualTo(400));
            Assert.That(steelDef.bluntDamagePerMille, Is.EqualTo(750));
            foreach (int other in new[] { StuffHandle.None, StuffHandle.Concrete, StuffHandle.Composite })
            {
                var def = ConstructionContent.StuffAt(other);
                Assert.That(def.sharpDamagePerMille, Is.EqualTo(1_000), def.defName);
                Assert.That(def.bluntDamagePerMille, Is.EqualTo(1_000), def.defName);
            }
            Assert.That(BuildingTargets.DamageFactorPerMille(DamageKind.Sharp, woodDef.stuff), Is.EqualTo(1_250));
            Assert.That(BuildingTargets.DamageFactorPerMille(DamageKind.Blunt, stoneDef.stuff), Is.EqualTo(1_250));
            Assert.That(BuildingTargets.DamageFactorPerMille(DamageKind.Sharp, 0xFFFF), Is.EqualTo(1_000),
                "a material the table does not know takes the blow as it comes");

            var (colony, a, _, woodWall) = AWall(StuffHandle.Wood);
            int stoneWall = Raise(colony, BuildingHandle.Wall, 6, 4, StuffHandle.Stone);
            PawnContext ctx = colony.Pawns;
            Assert.That(BuildingTargets.TryFind(ctx, woodWall, out BuildingTarget wood), Is.True);
            Assert.That(BuildingTargets.TryFind(ctx, stoneWall, out BuildingTarget stone), Is.True);
            Assert.That(ctx.Content.Combat.fists.damageKind, Is.EqualTo(DamageKind.Blunt), "fists are blunt");

            int tick = colony.World.CurrentTick;
            Armament machete = Weapon(ctx, ItemIndex.Machete), fists = Fists(ctx);
            Assert.That(machete.Attack.damageKind, Is.EqualTo(DamageKind.Sharp));
            int Raw(in Armament armament) => MeleeRules.DamageMilli(armament.Attack, ctx,
                DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.MeleeDamage ^ (uint)a.Id.Value));

            Assert.That(BuildingTargets.Resolve(a, machete, wood, ctx, tick).DamageMilli, Is.EqualTo(Raw(machete) * 1_250 / 1_000),
                "sharp against wood");
            Assert.That(BuildingTargets.Resolve(a, machete, stone, ctx, tick).DamageMilli, Is.EqualTo(Raw(machete) * 500 / 1_000),
                "sharp against stone");
            Assert.That(BuildingTargets.Resolve(a, fists, wood, ctx, tick).DamageMilli, Is.EqualTo(Raw(fists)),
                "blunt against wood");
            Assert.That(BuildingTargets.Resolve(a, fists, stone, ctx, tick).DamageMilli, Is.EqualTo(Raw(fists) * 1_250 / 1_000),
                "blunt against stone");
        }

        // ---- the order and the fight ------------------------------------------------------------

        /// <summary>
        /// The whole journey (design 33 §13d–§13g): a drafted colonist ordered on a wall walks to it,
        /// beats it down, and it goes — no refund, nothing left pointing at it, the cell walkable,
        /// and she holds again, still drafted.
        /// </summary>
        [Test]
        public void ADraftedColonistBeatsAWallDownAndGetsNothingBack()
        {
            var (colony, a, _, wall) = AWall();
            int woodBefore = OnTheGround(colony, ItemIndex.Wood);
            var tape = new Tape();

            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(a.CombatTarget, Is.Zero, "a building is target 0");

            for (int t = 0; t < 20_000 && colony.Grid.Edifice[wall] >= 0; t++)
            {
                colony.World.Tick();
                tape.Read(colony);
                if (colony.Pawns.EdificeDamage.Count > 0)
                    Assert.That(colony.Pawns.EdificeDamage.TryGet(wall, out _), Is.True, "the row is on the wall's own cell");
            }

            Assert.That(colony.Grid.Edifice[wall], Is.LessThan(0), "the wall never came down");
            Assert.That(colony.Grid.IsBlockedByEdifice(wall), Is.False);
            Assert.That(colony.Pawns.EdificeDamage.Count, Is.Zero, "the row outlived the wall");
            Assert.That(OnTheGround(colony, ItemIndex.Wood), Is.EqualTo(woodBefore), "demolished, and something came back");

            List<CombatEventView> hits = tape.By(a, CombatEventKind.Hit);
            Assert.That(hits.Count, Is.GreaterThan(1));
            foreach (CombatEventView hit in hits)
            {
                Assert.That(hit.Target.IsValid, Is.False, "a building is reported as target 0");
                Assert.That(Size.Index(hit.Cell), Is.EqualTo(wall));
            }
            Assert.That(tape.Of(CombatEventKind.Demolished).Count, Is.EqualTo(1));
            Assert.That(tape.Of(CombatEventKind.Demolished)[0].Amount, Is.EqualTo(EdificeHandle.Wall), "what came down");

            colony.World.Tick(5);
            Assert.That(a.Drafted, Is.True);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), "she holds again");
        }

        /// <summary>
        /// A building never dodges and is never missed; a blow at one does nothing but its damage
        /// (design 33 §13f). At melee 0 a blow at a pawn misses half the time, so a tape with no
        /// miss is the rule, not the luck. And the pawn rules are never asked.
        /// </summary>
        [Test]
        public void EveryBlowAtABuildingLandsForItsDamageAlone()
        {
            var (colony, a, _, wall) = AWall(StuffHandle.Stone);
            SetMelee(a, 0);
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            var tape = new Tape();

            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            tape.Tick(colony, 3_000);

            List<int> landed = tape.Landed(a);
            Assert.That(landed.Count, Is.GreaterThanOrEqualTo(15), "the control: she swung at it");
            Assert.That(tape.By(a, CombatEventKind.Hit).Count, Is.EqualTo(landed.Count), "a blow at a wall did not land");
            foreach (CombatEventKind none in new[] { CombatEventKind.Miss, CombatEventKind.Dodge, CombatEventKind.Stun,
                         CombatEventKind.Critical, CombatEventKind.KnockedBack, CombatEventKind.SwingCritical })
                Assert.That(tape.Of(none), Is.Empty, $"a building took a {none}");

            // Fists are blunt and the wall is stone, so every blow is its roll times the material's
            // ×1.25 (design 33 §14d).
            int figure = colony.Pawns.Content.Combat.fists.damage * 1_000;
            int spread = figure * colony.Pawns.Content.Combat.damageSpreadPerMille / 1_000;
            int factor = ConstructionContent.StuffAt(StuffHandle.Stone).bluntDamagePerMille;
            foreach (CombatEventView hit in tape.By(a, CombatEventKind.Hit))
                Assert.That(hit.Amount, Is.InRange((figure - spread) * factor / 1_000, (figure + spread) * factor / 1_000));

            Assert.That(rules.Swings, Is.Empty, "a blow at a building was decided as a swing at a pawn");

            BuildingTargets.TryFind(colony.Pawns, wall, out BuildingTarget target);
            int dealt = 0;
            foreach (CombatEventView hit in tape.By(a, CombatEventKind.Hit)) dealt += hit.Amount;
            Assert.That(BuildingTargets.HpMilli(colony.Pawns, target), Is.EqualTo(target.MaxMilli - dealt),
                "what is left is the pool less every blow");
        }

        /// <summary>
        /// A blow at a building trains nothing (design 33 §14c; the owner: no — a wall is not a
        /// practice dummy). Asked of the one method and of a whole order carried to the wall's fall.
        /// The control is a swing at a pawn, which trains Melee landed or not (§6A.1).
        /// </summary>
        [Test]
        public void ABlowAtABuildingTrainsNoMelee()
        {
            var (colony, a, b, wall) = AWall();
            SetMelee(a, 0);
            int before = a.Skills[SkillIndex.Melee];
            BuildingTargets.TryFind(colony.Pawns, wall, out BuildingTarget target);

            colony.Pawns.Combat!.StrikeBuilding(a, target, wall, Fists(colony.Pawns), Blow(5_000), colony.World.CurrentTick);
            Assert.That(a.Skills[SkillIndex.Melee], Is.EqualTo(before), "a blow at a wall trained Melee");

            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => colony.Grid.Edifice[wall] < 0, 20_000, "the wall never came down");
            Assert.That(a.Skills[SkillIndex.Melee], Is.EqualTo(before), "beating a wall down trained Melee");

            colony.Pawns.Combat!.ApplySwing(a, b, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Miss),
                colony.World.CurrentTick);
            Assert.That(a.Skills[SkillIndex.Melee], Is.GreaterThan(before), "the control: a swing at a pawn trains");
        }

        /// <summary>
        /// Refused (design 33 §13d) for an undrafted colonist, a cell with nothing standing in it, a
        /// deck plate, and a cell off the board. Accepted — the control — for the wall.
        /// </summary>
        [Test]
        public void TheOrderIsRefusedForWhatIsNotABuilding()
        {
            var (colony, a, b, wall) = AWall();
            int plate = Raise(colony, BuildingHandle.DeckPlate, -4, 0);
            colony.World.Tick();

            Assert.That(Draft(colony, b, on: false), Is.EqualTo(IntentRejection.None));
            Assert.That(AttackCell(colony, b, wall), Is.EqualTo(IntentRejection.NotPermitted), "undrafted");
            Assert.That(AttackCell(colony, a, Near(colony, -6, -6)), Is.EqualTo(IntentRejection.NotPermitted), "bare ground");
            Assert.That(AttackCell(colony, a, plate), Is.EqualTo(IntentRejection.NotPermitted), "a deck plate");
            Assert.That(Send(colony, new Intent(IntentKind.OrderAttack, new CellRef(-1, 0, 0), a.Id.Value, 0)),
                Is.EqualTo(IntentRejection.NotPermitted), "off the board");
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None), "the control: the wall");
        }

        /// <summary>
        /// A wall nobody can reach a side of is refused: here every cell beside it is filled with
        /// wall, so there is nowhere to stand. The control is the same wall with one side opened.
        /// </summary>
        [Test]
        public void AWallWithNoSideAnybodyCanReachIsRefused()
        {
            var (colony, a, _, wall) = AWall();
            CellRef at = Size.FromIndex(wall);
            var ring = new List<int>();
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood),
                    Is.EqualTo(IntentRejection.None));
                colony.Construction.Raise(colony.Pawns, cell);
                ring.Add(cell);
            }
            colony.World.Tick();

            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.NotPermitted), "walled in on every side");

            colony.Construction.Demolish(colony.Pawns, ring[0], out _);
            colony.World.Tick();
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None), "the control: one side open");
        }

        /// <summary>The same wall again is a confirm-click and changes nothing (design 33 §13d); another wall is a new order.</summary>
        [Test]
        public void TheSameWallAgainChangesNothing()
        {
            var (colony, a, _, wall) = AWall();
            int other = Raise(colony, BuildingHandle.Wall, 6, 4);
            colony.World.Tick();

            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            int started = a.JobStartTick;
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(a.JobStartTick, Is.EqualTo(started), "the same order restarted the job");
            Assert.That(AttackCell(colony, a, other), Is.EqualTo(IntentRejection.None), "the control: another wall");
        }

        /// <summary>
        /// Two ordered on one wall stand on two sides of it (design 33 §13e, the pawn rule of §7c):
        /// never one tile, and both strike it.
        /// </summary>
        [Test]
        public void TwoOnOneWallStandOnTwoSides()
        {
            var (colony, a, b, wall) = AWall(StuffHandle.Stone);
            // Both west of the wall on its own row, so the nearest side is the same cell for each:
            // only the other's claim sends the second round to another.
            Stand(colony, b, Near(colony, 1, 0));
            var tape = new Tape();
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            Assert.That(AttackCell(colony, b, wall), Is.EqualTo(IntentRejection.None));

            // SideBySideTests' invariant: walkers may pass over one another's cells — the simulation
            // has no collision — so what is compared is the claim, and the cell once both stand.
            for (int t = 0; t < 2_000; t++)
            {
                colony.World.Tick();
                tape.Read(colony);
                if (a.FinishingStepTo >= 0 || b.FinishingStepTo >= 0) continue;
                if (a.Destination < 0 && b.Destination < 0)
                    Assert.That(a.Cell, Is.Not.EqualTo(b.Cell), $"tick {t}: two stand on one tile");
                Assert.That(Melee.SideOf(a), Is.Not.EqualTo(Melee.SideOf(b)), $"tick {t}: two claim one tile");
            }
            Assert.That(tape.By(a, CombatEventKind.Hit).Count, Is.GreaterThan(2));
            Assert.That(tape.By(b, CombatEventKind.Hit).Count, Is.GreaterThan(2));
            BuildingTargets.TryFind(colony.Pawns, wall, out BuildingTarget target);
            Assert.That(BuildingTargets.InReach(colony.Pawns, a.Cell, target), Is.True);
            Assert.That(BuildingTargets.InReach(colony.Pawns, b.Cell, target), Is.True);
        }

        /// <summary>
        /// A stone wall with fists takes longer than the draft's four quiet hours (design 33 §13e);
        /// every blow is activity, so she is still drafted and holding when it falls.
        /// </summary>
        [Test]
        public void TheDraftDoesNotLapseWhileSheBeatsAWall()
        {
            var (colony, a, _, wall) = AWall(StuffHandle.Stone);
            a.Needs[NeedIndex.Rest] = 1_000;
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            int ordered = colony.World.CurrentTick;

            TickUntil(colony, () => colony.Grid.Edifice[wall] < 0, 30_000, "the stone wall never came down");
            Assert.That(colony.World.CurrentTick - ordered, Is.GreaterThan(colony.Pawns.Content.DraftQuietTicks),
                "the control: the fight outlasted the quiet hours");
            colony.World.Tick(3);
            Assert.That(a.Drafted, Is.True, "she was undrafted the moment the wall fell");
        }

        /// <summary>
        /// The building going by any other route ends the attack as done (design 33 §13e): here it is
        /// taken down under her, and she holds again.
        /// </summary>
        [Test]
        public void TheBuildingGoingEndsTheAttack()
        {
            var (colony, a, _, wall) = AWall();
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(20);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the control: still at it");

            Assert.That(colony.Construction.Demolish(colony.Pawns, wall, out _), Is.True);
            colony.World.Tick(2);
            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold));
        }

        /// <summary>
        /// A wall pulled down and another raised on the same cell is a new building: the order was on
        /// the first, by its record, and does not carry on into the second (design 33 §13d).
        /// </summary>
        [Test]
        public void ARebuiltWallIsNotTheOneSheWasSentAt()
        {
            var (colony, a, _, wall) = AWall();
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(10);

            Assert.That(colony.Construction.Demolish(colony.Pawns, wall, out _), Is.True);
            Assert.That(colony.Construction.Place(Size.FromIndex(wall), BuildingHandle.Wall, StuffHandle.Wood),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, wall), Is.True);
            colony.World.Tick(2);

            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.DraftHold), "the order carried on into a new wall");
        }

        // ---- the one removal path ---------------------------------------------------------------

        /// <summary>
        /// <c>ConstructionGrid.Demolish</c> is the one owner of "the building has gone" (design 33
        /// §13h): it clears the damage row and any deconstruct order on the building, whoever calls
        /// it — here the deconstruct path's own call.
        /// </summary>
        [Test]
        public void DemolishClearsTheRowAndTheDeconstructOrder()
        {
            var (colony, _, _, wall) = AWall();
            colony.Pawns.EdificeDamage.Set(wall, 120_000);
            Assert.That(colony.Designations.Designate(Size.FromIndex(wall), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            Assert.That(colony.Construction.Demolish(colony.Pawns, wall, out _), Is.True);

            Assert.That(colony.Pawns.EdificeDamage.Count, Is.Zero, "the row outlived the wall");
            Assert.That(colony.Designations.At(wall), Is.EqualTo(DesignationKind.None), "an order on nothing");
        }

        /// <summary>
        /// A wall worn down and then taken apart by the colony's own deconstructor leaves no row
        /// behind — the route that is not a fight at all.
        /// </summary>
        [Test]
        public void AStruckWallTakenApartLeavesNoRow()
        {
            var (colony, a, b, wall) = AWall();
            Assert.That(Draft(colony, a, on: false), Is.EqualTo(IntentRejection.None));
            Assert.That(Draft(colony, b, on: false), Is.EqualTo(IntentRejection.None));
            colony.Pawns.EdificeDamage.Set(wall, 120_000);
            Assert.That(colony.Designations.Designate(Size.FromIndex(wall), DesignationKind.Deconstruct),
                Is.EqualTo(IntentRejection.None));

            TickUntil(colony, () => colony.Grid.Edifice[wall] < 0, 20_000, "nobody took the wall apart");
            Assert.That(colony.Pawns.EdificeDamage.Count, Is.Zero);
        }

        /// <summary>
        /// Demolition is the deconstruct path's own removal (design 33 §13g), so everything a building
        /// held goes with it: a bed leaves the bed index, a door its navigation flag.
        /// </summary>
        [Test]
        public void ADemolishedBedAndDoorLeaveNothingPointingAtThem()
        {
            var (colony, a, _, _) = AWall();
            int bed = Raise(colony, BuildingHandle.Bed, 3, -4, facing: 1);
            int door = Raise(colony, BuildingHandle.Door, -3, -4);
            colony.World.Tick();
            Assert.That(colony.Pawns.Items.Beds, Does.Contain(bed), "the control: a bed");
            Assert.That(colony.Pawns.Nav.Grid.Flags[door] & NavFlags.Door, Is.Not.EqualTo(NavFlags.None), "the control: a door");

            foreach (int cell in new[] { bed, door })
            {
                Assert.That(BuildingTargets.TryFind(colony.Pawns, cell, out BuildingTarget target), Is.True);
                colony.Pawns.Combat!.StrikeBuilding(a, target, cell, Fists(colony.Pawns), Blow(target.MaxMilli), colony.World.CurrentTick);
            }
            colony.World.Tick();

            Assert.That(colony.Grid.Edifice[bed], Is.LessThan(0));
            Assert.That(colony.Grid.Edifice[door], Is.LessThan(0));
            Assert.That(colony.Pawns.Items.Beds, Has.No.Member(bed), "a demolished bed is still a bed");
            Assert.That(colony.Pawns.Nav.Grid.Flags[door] & NavFlags.Door, Is.EqualTo(NavFlags.None), "a demolished door is still a door");
            Assert.That(colony.Pawns.EdificeDamage.Count, Is.Zero);
        }

        /// <summary>Only the blow that crosses nought demolishes (design 33 §13g): two the same tick report one fall.</summary>
        [Test]
        public void OnlyTheBlowThatCrossesNoughtDemolishes()
        {
            var (colony, a, b, wall) = AWall();
            var tape = new Tape();
            BuildingTargets.TryFind(colony.Pawns, wall, out BuildingTarget target);
            int tick = colony.World.CurrentTick;

            colony.Pawns.Combat!.StrikeBuilding(a, target, wall, Fists(colony.Pawns), Blow(target.MaxMilli), tick);
            colony.Pawns.Combat!.StrikeBuilding(b, target, wall, Fists(colony.Pawns), Blow(5_000), tick);
            colony.World.Tick();
            tape.Read(colony);

            Assert.That(tape.Of(CombatEventKind.Demolished).Count, Is.EqualTo(1));
            Assert.That(tape.Of(CombatEventKind.Hit).Count, Is.EqualTo(1), "a blow landed on a wall already falling");
            Assert.That(colony.Grid.Edifice[wall], Is.LessThan(0));
        }

        // ---- what is published ----------------------------------------------------------------

        /// <summary>
        /// A building attack publishes its order line (design 33 §5d, §13i): <c>order.cell</c> on a
        /// cell of the building, and no pawn target.
        /// </summary>
        [Test]
        public void TheOrderLineIsDrawnToTheBuilding()
        {
            var (colony, a, _, wall) = AWall();
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            WorldSnapshot frame = colony.World.Views.Current;

            Assert.That(frame.TryGetPawnAspect(a.Id, CombatAspects.OrderCell, out int cell), Is.True, "no order line");
            Assert.That(cell, Is.EqualTo(wall));
            Assert.That(frame.TryGetPawnAspect(a.Id, CombatAspects.OrderTarget, out _), Is.False, "a pawn target for a wall");
        }

        /// <summary>
        /// The struck are published, and the content's table says which edifices are targets
        /// (design 33 §13i) — what the interface reads instead of a copy of the rule.
        /// </summary>
        [Test]
        public void StruckBuildingsArePublishedAndTheTableNamesTheTargets()
        {
            var (colony, a, _, wall) = AWall();
            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.EdificeDamage.Length, Is.Zero, "the control: nothing struck");
            Assert.That(frame.EdificeHitPoints(EdificeHandle.Wall), Is.EqualTo(300));
            Assert.That(frame.EdificeHitPoints(EdificeHandle.Bed), Is.EqualTo(120));
            Assert.That(frame.EdificeHitPoints(EdificeHandle.Campfire), Is.EqualTo(60));
            Assert.That(frame.EdificeHitPoints(EdificeHandle.TreeBirch), Is.Zero);
            Assert.That(frame.EdificeHitPoints(EdificeHandle.None), Is.Zero);
            Assert.That(frame.EdificeHitPoints(999), Is.Zero);

            BuildingTargets.TryFind(colony.Pawns, wall, out BuildingTarget target);
            colony.Pawns.Combat!.StrikeBuilding(a, target, wall, Fists(colony.Pawns), Blow(40_000), colony.World.CurrentTick);
            colony.World.Tick();
            frame = colony.World.Views.Current;

            Assert.That(frame.EdificeDamage.Length, Is.EqualTo(1));
            EdificeDamageView row = frame.EdificeDamage[0];
            Assert.That(row.CellIndex, Is.EqualTo(wall));
            Assert.That(row.Edifice, Is.EqualTo(EdificeHandle.Wall));
            Assert.That(row.HpMilli, Is.EqualTo(260_000));
            Assert.That(row.MaxMilli, Is.EqualTo(300_000));
            Assert.That(frame.TryGetEdificeDamage(wall, out EdificeDamageView found), Is.True);
            Assert.That(found.HpMilli, Is.EqualTo(260_000));
        }

        // ---- the hash, the save, the knockback -----------------------------------------------

        /// <summary>
        /// Nothing is hashed for a building until one is struck (design 33 §6): a colony with a wall
        /// nobody has touched hashes as it did before C6, and one blow moves the hash.
        /// </summary>
        [Test]
        public void AWallIsHashedOnlyOnceStruck()
        {
            var (colony, a, _, wall) = AWall();
            ulong untouched = colony.World.ComputeStateHash().Value;
            colony.Pawns.EdificeDamage.Set(wall, 300_000 - 1);
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(untouched), "a struck wall is not in the hash");
            colony.Pawns.EdificeDamage.Clear(wall);
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(untouched), "an empty store hashed something");
            _ = a;
        }

        /// <summary>
        /// A save taken with a blow at a wall in the air, and the wall already struck, resumes on the
        /// same hash then and 600 ticks on. The control forgets the wall's damage and parts.
        /// </summary>
        [Test]
        public void ASaveTakenMidBlowAtAWallResumesTheSame()
        {
            var (colony, a, _, wall) = AWall(StuffHandle.Stone);
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => colony.Pawns.EdificeDamage.Count > 0
                && a.Driver is AttackMeleeJobDriver { InWindup: true } s && s.ToilProgress > 2_000, 3_000, "no second wind-up");

            byte[] saved = colony.Save();
            var restored = Board();
            restored.Load(saved);
            var forgetful = Board();
            forgetful.Load(saved);
            forgetful.Pawns.EdificeDamage.Clear(wall);

            Pawn back = restored.Pawns.Pawns.Get(a.Id)!;
            Assert.That(back.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee));
            Assert.That(back.CombatTarget, Is.Zero);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            colony.World.Tick(600);
            restored.World.Tick(600);
            forgetful.World.Tick(600);
            Assert.That(forgetful.World.ComputeStateHash().Value, Is.Not.EqualTo(colony.World.ComputeStateHash().Value),
                "the control: a forgotten row resumes the same, so this test could not see one");
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                "the fight with the wall resumed differently");
            restored.Pawns.EdificeDamage.TryGet(wall, out int left);
            colony.Pawns.EdificeDamage.TryGet(wall, out int want);
            Assert.That(left, Is.EqualTo(want));
        }

        /// <summary>
        /// A knockback keeps the player's building order, as it keeps a pawn order (design 33
        /// §13e): knocked off her side, she goes back to the wall.
        /// </summary>
        [Test]
        public void AKnockedBackColonistGoesBackToTheWall()
        {
            var (colony, a, _, wall) = AWall();
            Assert.That(AttackCell(colony, a, wall), Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => colony.Pawns.EdificeDamage.Count > 0, 3_000, "she never struck the wall");
            int handle = a.CurrentJob!.DestCell;

            Pawn raider = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -8, -8));
            Stand(colony, raider, NeighbourOf(colony, a));
            Assert.That(colony.Pawns.Combat!.KnockBack(a, raider, -1, colony.World.CurrentTick), Is.True, "nowhere to knock her");

            Assert.That(a.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the order was lost in the fall");
            Assert.That(a.CurrentJob!.PlayerForced, Is.True);
            Assert.That(a.CurrentJob!.DestCell, Is.EqualTo(handle));
            Assert.That(a.CombatTarget, Is.Zero);
        }

        /// <summary>
        /// A walkable cell beside the pawn on its layer whose opposite cell is open too, so a blow
        /// from it has somewhere to knock her — never from the wall's side.
        /// </summary>
        static int NeighbourOf(ColonyWorld colony, Pawn pawn)
        {
            CellRef at = Size.FromIndex(pawn.Cell);
            foreach (var (dx, dz) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                int beyond = Size.Index(at.X - dx, at.Z - dz, at.Y);
                if (colony.Grid.IsWalkable(cell) && !colony.Grid.IsBlockedByEdifice(cell)
                    && colony.Grid.IsWalkable(beyond) && !colony.Grid.IsBlockedByEdifice(beyond)) return cell;
            }
            Assert.Fail("no cell beside her");
            return -1;
        }

        // ---- a bandit breaks in (design 33 §14b) ------------------------------------------------

        /// <summary>Colonists and no bed, so no building stands on the board but the ones a test raises.</summary>
        static ColonyWorld Bare(int colonists = 1)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick();
            return colony;
        }

        /// <summary>Raise one building on exactly this cell.</summary>
        static void RaiseAt(ColonyWorld colony, int cell, int building = BuildingHandle.Wall, int stuff = StuffHandle.Wood)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, 0), Is.EqualTo(IntentRejection.None),
                $"could not order building {building} at {Size.FromIndex(cell)}");
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        /// <summary>
        /// A colonist sealed in a ring of eight wooden walls, and the cells of the ring by their offset
        /// from her: east is (1, 0), west (-1, 0).
        /// </summary>
        static Dictionary<(int, int), int> WallIn(ColonyWorld colony, Pawn pawn)
        {
            CellRef at = Size.FromIndex(pawn.Cell);
            var ring = new Dictionary<(int, int), int>();
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                RaiseAt(colony, cell);
                ring[(dx, dz)] = cell;
            }
            colony.World.Tick();
            return ring;
        }

        static bool AttackingBuilding(Pawn pawn, int handle) =>
            pawn.CurrentJob is { DefIndex: JobIndex.AttackMelee } job && pawn.CombatTarget == 0 && job.DestCell == handle;

        /// <summary>
        /// The owner's rule (design 33 §14b): with no colonist to reach, a bandit attacks the nearest
        /// colony building it can reach — here the wall of the ring nearest it — through C6's own
        /// building mode, unforced; and once the wall is down and she can be reached, it turns on her.
        /// </summary>
        [Test]
        public void ABanditWithNobodyToReachBreaksInThroughTheNearestWall()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            var ring = WallIn(colony, colonist);
            int east = ring[(1, 0)];
            CellRef at = Size.FromIndex(colonist.Cell);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Size.Index(at.X + 5, at.Z, at.Y));
            int handle = colony.Grid.Edifice[east];

            TickUntil(colony, () => AttackingBuilding(bandit, handle), 60, "the bandit never went for the east wall");
            Assert.That(bandit.CurrentJob!.PlayerForced, Is.False, "a bandit's own choice is not an order");

            TickUntil(colony, () => colony.Grid.Edifice[east] < 0, 20_000, "the bandit never broke the wall down");
            foreach (var side in ring)
                if (side.Value != east)
                    Assert.That(colony.Pawns.EdificeDamage.TryGet(side.Value, out _), Is.False, $"it struck the wall at {side.Key} too");

            TickUntil(colony, () => bandit.CombatTarget == colonist.Id.Value, 600, "through the wall, and it did not turn on her");
        }

        /// <summary>
        /// The control on the order of the rule (design 33 §14b): a colonist it can reach comes before
        /// any building, however near the building.
        /// </summary>
        [Test]
        public void ABanditGoesForAColonistItCanReachBeforeAnyBuilding()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, -12, 0));
            CellRef at = Size.FromIndex(Near(colony, 6, 0));
            RaiseAt(colony, Size.Index(at.X + 1, at.Z, at.Y));
            colony.World.Tick();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Size.Index(at.X, at.Z, at.Y));

            TickUntil(colony, () => bandit.CurrentJob?.DefIndex == JobIndex.AttackMelee, 60, "the bandit did nothing");
            Assert.That(bandit.CombatTarget, Is.EqualTo(colonist.Id.Value), "it went for the wall beside it, not the colonist");
        }

        /// <summary>
        /// With every colonist down (design 33 §14b), the nearest colony building it can reach: a tie
        /// goes to the older record, and a wall no colonist raised — the ruined city's — is passed over
        /// however near.
        /// </summary>
        [Test]
        public void WithEveryColonistDownItTakesTheNearestColonyBuildingAndTheOlderOnATie()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            CellRef at = Size.FromIndex(Near(colony, 8, 0));
            int older = Size.Index(at.X, at.Z + 3, at.Y), newer = Size.Index(at.X, at.Z - 3, at.Y);
            int city = Size.Index(at.X + 2, at.Z, at.Y);
            RaiseAt(colony, older);
            RaiseAt(colony, newer);
            RaiseAt(colony, city);
            var records = colony.Construction.Edifices.Records;
            int cityHandle = colony.Grid.Edifice[city];
            var stamped = records[cityHandle];
            stamped.Built = false;
            records[cityHandle] = stamped;
            colony.World.Tick();

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Size.Index(at.X, at.Z, at.Y));
            Strike(colony, bandit, colonist, colonist.HpMilli);
            Assert.That(colonist.Downed, Is.True, "the control: she is down");
            Assert.That(colony.Pawns.Distance(bandit.Cell, older), Is.EqualTo(colony.Pawns.Distance(bandit.Cell, newer)),
                "the control: a tie");
            Assert.That(colony.Grid.Edifice[older], Is.LessThan(colony.Grid.Edifice[newer]), "the control: raised first, lower handle");

            TickUntil(colony, () => bandit.CurrentJob?.DefIndex == JobIndex.AttackMelee, 60, "with nobody standing, it did nothing");
            Assert.That(AttackingBuilding(bandit, colony.Grid.Edifice[older]), Is.True,
                "it did not take the older of two equally near walls, passing over the city's");
        }

        /// <summary>
        /// A bandit at a wall looks up (design 33 §14b): every <c>rechooseTicks</c> between swings
        /// it thinks again, so a colonist who can now be reached — here another wall of her ring taken
        /// down — is gone for long before the wall it was striking would have fallen.
        /// </summary>
        [Test]
        public void ABanditAtAWallLooksUpWhenAColonistCanBeReached()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            var ring = WallIn(colony, colonist);
            CellRef at = Size.FromIndex(colonist.Cell);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Size.Index(at.X + 5, at.Z, at.Y));
            int east = ring[(1, 0)];

            TickUntil(colony, () => colony.Pawns.EdificeDamage.TryGet(east, out _), 2_000, "the bandit never struck the east wall");
            colony.Construction.Demolish(colony.Pawns, ring[(-1, 0)], out _);
            int opened = colony.World.CurrentTick;

            TickUntil(colony, () => bandit.CombatTarget == colonist.Id.Value, colony.Pawns.Content.Combat.rechooseTicks + 150,
                "it kept at the wall with a way in open");
            Assert.That(colony.Grid.Edifice[east], Is.GreaterThanOrEqualTo(0), "the control: the east wall still stands");
            TestContext.WriteLine($"turned on her {colony.World.CurrentTick - opened} ticks after the way in opened");
        }

        /// <summary>
        /// A building it cannot get beside is passed over (design 33 §14b), however near: here a shelf
        /// sealed inside a ring of the city's walls — which a bandit passes over too — and a colony
        /// wall further off that it can reach, which is the one it takes.
        /// </summary>
        [Test]
        public void ABuildingItCannotGetBesideIsPassedOver()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            CellRef at = Size.FromIndex(Near(colony, 8, 0));
            int shelf = Size.Index(at.X + 3, at.Z, at.Y);
            RaiseAt(colony, shelf, BuildingHandle.Shelf);
            CellRef s = Size.FromIndex(shelf);
            var records = colony.Construction.Edifices.Records;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int cell = Size.Index(s.X + dx, s.Z + dz, s.Y);
                RaiseAt(colony, cell);
                int handle = colony.Grid.Edifice[cell];
                var stamped = records[handle];
                stamped.Built = false;
                records[handle] = stamped;
            }
            int far = Size.Index(at.X - 7, at.Z, at.Y);
            RaiseAt(colony, far);
            colony.World.Tick();

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Size.Index(at.X, at.Z, at.Y));
            Strike(colony, bandit, colonist, colonist.HpMilli);
            Assert.That(colony.Pawns.Distance(bandit.Cell, shelf), Is.LessThan(colony.Pawns.Distance(bandit.Cell, far)),
                "the control: the shelf is the nearer");

            TickUntil(colony, () => bandit.CurrentJob?.DefIndex == JobIndex.AttackMelee, 60, "with nobody standing, it did nothing");
            Assert.That(AttackingBuilding(bandit, colony.Grid.Edifice[far]), Is.True,
                "it did not take the wall it could reach over the shelf it could not");
        }
    }
}
