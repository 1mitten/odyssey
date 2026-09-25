#nullable enable
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The combat contracts step (design 33 §5, <c>docs/plans/combat-contracts.md</c>): every
    /// handle, field, seam and channel the four lanes fill, pinned before any of them starts.
    ///
    /// <para><b>What these hold is the shape, never the fight.</b> Nothing here swings: the drivers
    /// are stubs that fail, the orders are refused, the think nodes decline. What is asserted is
    /// that the tables were appended and not inserted, that a colony which has never fought saves
    /// and hashes exactly as it did, that the new state round-trips, and that the published views
    /// say what the interface will read — each with the control that shows it would have failed
    /// without the thing it tests.</para>
    /// </summary>
    public class CombatContractTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board(int colonists = 2, uint seed = 7u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        static Pawn SpawnKind(ColonyWorld colony, int kind)
        {
            Pawn near = colony.Pawns.Pawns.All[0];
            int before = colony.Pawns.Pawns.Count;
            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, Size.FromIndex(near.Cell), kind)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));
            return colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];
        }

        // ---- the handles --------------------------------------------------------------------

        [Test]
        public void TheAppendOnlyTablesWereAppendedAndNotInserted()
        {
            // Every number a save already depends on, unmoved.
            Assert.That(JobHandle.Goto, Is.EqualTo(13));
            Assert.That(JobHandle.Refuel, Is.EqualTo(16));
            Assert.That(ItemHandle.Carrots, Is.EqualTo(6));
            Assert.That(WorkHandle.Growing, Is.EqualTo(4));
            Assert.That(SkillIndex.Growing, Is.EqualTo(4));
            Assert.That(PawnKindIndex.DuctRat, Is.EqualTo(2));

            // And the combat line's, at the end, in the order the plan claims them. They were 14-18
            // until the merge with main put power's three jobs first; nothing combat shipped had saved them.
            Assert.That(new[] { JobHandle.AttackMelee, JobHandle.Flee, JobHandle.Downed, JobHandle.Equip, JobHandle.Rescue },
                Is.EqualTo(new[] { 17, 18, 19, 20, 21 }));
            // One more after them since design 33 §17: the thief's, appended.
            Assert.That(JobHandle.Steal, Is.EqualTo(22));
            // 25 since medical supplies appended Job_Treat and Job_Patient at 23 and 24 (design
            // 37), after the thief's — bandits shipped first at the merge with main — and 26 since
            // the kitchen appended Job_Cook at 25 (design 48).
            Assert.That(JobHandle.Treat, Is.EqualTo(23));
            Assert.That(JobHandle.Cook, Is.EqualTo(25));
            Assert.That(JobHandle.Count, Is.EqualTo(26));
            Assert.That(new[] { ItemHandle.Bat, ItemHandle.Crowbar, ItemHandle.Machete, ItemHandle.ArcBlade },
                Is.EqualTo(new[] { 7, 8, 9, 10 }));
            // 12 since medical supplies were appended at 11 (design 37), and 15 since the
            // kitchen's three meals were appended at 12 to 14 (design 48).
            Assert.That(ItemHandle.MedicalSupplies, Is.EqualTo(11));
            Assert.That(new[] { ItemHandle.CookedMeal, ItemHandle.VegetableMeal, ItemHandle.BurntMeal },
                Is.EqualTo(new[] { 12, 13, 14 }));
            Assert.That(ItemHandle.Count, Is.EqualTo(15));
            Assert.That(WorkHandle.Rescue, Is.EqualTo(5));
            Assert.That(WorkHandle.Doctor, Is.EqualTo(6));
            // 8 since the kitchen appended Work_Cooking at 7 (design 48).
            Assert.That(WorkHandle.Cooking, Is.EqualTo(7));
            Assert.That(WorkHandle.Count, Is.EqualTo(8));
            Assert.That(SkillIndex.Melee, Is.EqualTo(5));
            // 7 since medical supplies appended Skill_Medicine at 6 (design 37), and 8 since the
            // kitchen appended Skill_Cooking at 7 (design 48).
            Assert.That(SkillIndex.Medicine, Is.EqualTo(6));
            Assert.That(SkillIndex.Cooking, Is.EqualTo(7));
            Assert.That(SkillIndex.Count, Is.EqualTo(8));
            Assert.That(PawnKindIndex.Bandit, Is.EqualTo(3));
            Assert.That(PawnKindIndex.Count, Is.EqualTo(4));

            // IntentKind is an enum whose numbers an intent log carries: the three orders are
            // together and after everything main shipped first (power's four, since the merge of
            // 2026-09-24), and the debug arming follows them as the last.
            Assert.That((int)IntentKind.OrderAttack, Is.EqualTo((int)IntentKind.CancelConduit + 1));
            Assert.That((int)IntentKind.OrderEquip, Is.EqualTo((int)IntentKind.OrderAttack + 1));
            Assert.That((int)IntentKind.OrderRescue, Is.EqualTo((int)IntentKind.OrderAttack + 2));
            Assert.That((int)IntentKind.DebugArmColonists, Is.EqualTo((int)IntentKind.OrderAttack + 3));
        }

        [Test]
        public void EveryTableIsFilledByNameInHandleOrder()
        {
            PawnContent content = ContentPack.Pawns();
            Assert.That(content.Jobs.Skip(17).Select(j => j.defName), Is.EqualTo(new[]
                { "Job_AttackMelee", "Job_Flee", "Job_Downed", "Job_Equip", "Job_Rescue", "Job_Steal",
                  "Job_Treat", "Job_Patient", "Job_Cook" }));
            for (int i = 0; i < content.Jobs.Length; i++)
                Assert.That(content.Jobs[i].driver, Is.EqualTo(i), content.Jobs[i].defName + " names another driver");
            Assert.That(content.Jobs[JobIndex.AttackMelee].trainsSkill, Is.EqualTo(SkillIndex.Melee));

            Assert.That(content.Skills[SkillIndex.Melee].defName, Is.EqualTo("Skill_Melee"));
            Assert.That(content.WorkTypes[WorkTypeIndex.Rescue].defName, Is.EqualTo("Work_Rescue"));
            Assert.That(WorkTypeIndex.Names[WorkTypeIndex.Rescue], Is.EqualTo("rescue"));
            Assert.That(SkillIndex.Names[SkillIndex.Melee], Is.EqualTo("melee"));
            Assert.That(content.Items.Skip(7).Take(4).Select(i => i.defName), Is.EqualTo(new[]
                { "Item_Bat", "Item_Crowbar", "Item_Machete", "Item_ArcBlade" }));
            Assert.That(content.Kinds[PawnKindIndex.Bandit].defName, Is.EqualTo("PawnKind_Bandit"));
        }

        [Test]
        public void EveryDriverInThePoolIsTheOneItsJobNames()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            Assert.That(pawn.DriverPool.Length, Is.EqualTo(JobHandle.Count));
            Assert.That(pawn.DriverPool[JobIndex.AttackMelee], Is.TypeOf<AttackMeleeJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.Flee], Is.TypeOf<FleeJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.Downed], Is.TypeOf<DownedJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.Equip], Is.TypeOf<EquipJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.Rescue], Is.TypeOf<RescueJobDriver>());
            Assert.That(pawn.DriverPool[JobIndex.Steal], Is.TypeOf<StealJobDriver>());
        }

        /// <summary>
        /// A stub driver fails on its first tick rather than standing there: a lane that forgot to
        /// fill one finds out the first time anything starts it. The control is the job counter,
        /// which would not move for a driver that never ran.
        /// </summary>
        [TestCase(JobIndex.AttackMelee)]
        [TestCase(JobIndex.Flee)]
        [TestCase(JobIndex.Downed)]
        [TestCase(JobIndex.Equip)]
        [TestCase(JobIndex.Rescue)]
        public void AStubDriverFailsOnItsFirstTick(int jobDef)
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(5);
            int failedBefore = colony.Jobs.FailedOf(jobDef);

            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            Job job = pawn.JobBuffer;
            job.Reset(jobDef);
            Assert.That(colony.Jobs.StartJob(pawn, job, colony.World.CurrentTick), Is.True);
            colony.World.Tick();

            Assert.That(colony.Jobs.FailedOf(jobDef), Is.EqualTo(failedBefore + 1));
            Assert.That(pawn.CurrentJob?.DefIndex, Is.Not.EqualTo(jobDef));
        }

        // ---- the orders ---------------------------------------------------------------------

        /// <summary>
        /// Each order has a handler from this commit and refuses until its lane writes it. The
        /// control is an intent kind nobody handles, which the world refuses as unknown — the
        /// answer these three would give had the handlers not been registered.
        /// </summary>
        [TestCase(IntentKind.OrderAttack)]
        [TestCase(IntentKind.OrderEquip)]
        [TestCase(IntentKind.OrderRescue)]   // written (C4), and still refused here: nobody is down
        public void EachCombatOrderIsHandledAndRefusedUntilItsLaneWritesIt(IntentKind kind)
        {
            var colony = Board();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Assert.That(Send(colony, new Intent(kind, Size.FromIndex(b.Cell), a.Id.Value, b.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted));

            Assert.That(Send(colony, new Intent((IntentKind)9_999, default, a.Id.Value)),
                Is.EqualTo(IntentRejection.UnknownIntent), "the control: an unhandled kind");
        }

        [Test]
        public void TheCombatOrdersApplyWhilePaused()
        {
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderAttack), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderEquip), Is.True);
            Assert.That(PausedIntents.AppliesWhilePaused(IntentKind.OrderRescue), Is.True);
        }

        // ---- the minds ----------------------------------------------------------------------

        [Test]
        public void TheAnimalAndHostileMindsHaveTheirSeamsInOrder()
        {
            Assert.That(JobSystem.AnimalMind.Select(n => n.Name),
                Is.EqualTo(new[] { "Downed", "AnimalCombat", "AnimalShelter", "AnimalIdle" }));
            Assert.That(JobSystem.HostileMind.Select(n => n.Name),
                Is.EqualTo(new[] { "Downed", "Hostile", "Idle" }));
        }

        [Test]
        public void TheCombatSystemRunsInThePawnsPhaseBetweenJobsAndMovement()
        {
            var colony = Board();
            CombatSystem? combat = colony.Pawns.Combat;
            Assert.That(combat, Is.Not.Null, "the composition did not register the combat system");
            Assert.That(combat!.Phase, Is.EqualTo(TickPhase.Pawns));
            Assert.That(combat.Order, Is.GreaterThan(colony.Jobs.Order));
            Assert.That(combat.Order, Is.LessThan(new MovementSystem(colony.Pawns).Order));
            Assert.That(colony.World.Systems.PawnSystems, Has.Member(combat));
            Assert.That(combat.Jobs, Is.SameAs(colony.Jobs), "a fight starts and ends jobs through the colony's own pipeline");
        }

        [Test]
        public void TheRescueGiverIsAnEmergencyAndAnswersNoWhenNobodyIsDown()
        {
            var colony = Board();
            WorkGiver rescue = colony.Jobs.Givers.Single(g => g.Name == "Rescue");
            Assert.That(rescue.WorkType, Is.EqualTo(WorkTypeIndex.Rescue));
            Assert.That(rescue.Emergency, Is.True);
            Assert.That(colony.Jobs.Givers[0], Is.SameAs(rescue), "an emergency scans first");
            Assert.That(rescue.TryGiveJob(colony.Pawns.Pawns.All[0], colony.Pawns, new Job()), Is.False);
        }

        // ---- the bandit ---------------------------------------------------------------------

        [Test]
        public void ABanditIsAPersonAndNotOurs()
        {
            var colony = Board();
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            Pawn colonist = colony.Pawns.Pawns.All[0];

            Assert.That(bandit.IsPerson, Is.True);
            Assert.That(bandit.IsHostile, Is.True);
            Assert.That(bandit.IsColonist, Is.False);
            Assert.That(colonist.IsColonist, Is.True, "the control: a colonist is one of ours");

            // Nobody's to draft: the colonist beside it is.
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, bandit.Id.Value, 1)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, colonist.Id.Value, 1)),
                Is.EqualTo(IntentRejection.None));
        }

        /// <summary>
        /// A bandit's needs do not tick, and nor do a downed colonist's (the C2 default). The
        /// control is the standing colonist beside them, whose food falls over the same ticks.
        /// </summary>
        [Test]
        public void ABanditAndADownedColonistHaveNoNeedsTick()
        {
            var colony = Board();
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            Pawn downed = colony.Pawns.Pawns.All[0];
            Pawn standing = colony.Pawns.Pawns.All[1];
            downed.Downed = true;

            int banditFood = bandit.Needs[NeedIndex.Food];
            int downedFood = downed.Needs[NeedIndex.Food];
            int standingFood = standing.Needs[NeedIndex.Food];
            colony.World.Tick(3_000);

            Assert.That(standing.Needs[NeedIndex.Food], Is.LessThan(standingFood), "the control's food did not fall");
            Assert.That(bandit.Needs[NeedIndex.Food], Is.EqualTo(banditFood));
            Assert.That(downed.Needs[NeedIndex.Food], Is.EqualTo(downedFood));
        }

        // ---- the state, the hash and the save ---------------------------------------------------

        [Test]
        public void EveryPawnIsBornWholeWithNothingToSayAboutAFight()
        {
            var colony = Board();
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            Pawn rat = SpawnKind(colony, PawnKindIndex.DuctRat);
            Pawn colonist = colony.Pawns.Pawns.All[0];

            Assert.That(colonist.HpMilli, Is.EqualTo(100_000));
            Assert.That(hog.HpMilli, Is.EqualTo(60_000));
            Assert.That(rat.HpMilli, Is.EqualTo(15_000));
            Assert.That(colonist.DeathAtMilli, Is.EqualTo(-50_000));
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assert.That(pawn.HasCombatState, Is.False, $"pawn {pawn.Id.Value} was born with combat state");
        }

        /// <summary>
        /// Each field reaches the hash while it is set and the hash is exactly as before once it is
        /// cleared — which is what lets C5 and C6 assert that no golden moves.
        /// </summary>
        [Test]
        public void TheFightIsInTheHashOnlyWhileItIsSet()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(30);
            ulong whole = Hash(colony);

            void Moves(string what, System.Action set, System.Action clear)
            {
                set();
                Assert.That(Hash(colony), Is.Not.EqualTo(whole), $"the hash cannot see {what}");
                clear();
                Assert.That(Hash(colony), Is.EqualTo(whole), $"clearing {what} did not restore the hash");
            }

            int hp = pawn.HpMilli;
            Moves("a hurt pawn", () => pawn.HpMilli = hp - 1, () => pawn.HpMilli = hp);
            Moves("the downed flag", () => pawn.Downed = true, () => pawn.Downed = false);
            Moves("the swing clock", () => pawn.NextSwingTick = 99, () => pawn.NextSwingTick = 0);
            Moves("a stun", () => pawn.StunnedUntilTick = 99, () => pawn.StunnedUntilTick = 0);
            Moves("a retaliation", () => pawn.RetaliateAgainst = 2, () => pawn.RetaliateAgainst = 0);
            Moves("a weapon", () => pawn.EquippedItem = 5, () => pawn.EquippedItem = 0);
            Moves("an order's target", () => pawn.CombatTarget = 2, () => pawn.CombatTarget = 0);
            Moves("a carrier", () => pawn.CarriedBy = 2, () => pawn.CarriedBy = 0);
            Moves("a treatment cooldown", () => pawn.TreatedUntilTick = 99, () => pawn.TreatedUntilTick = 0);
            Moves("a struck building", () => colony.Pawns.EdificeDamage.Set(123, 4_000),
                () => colony.Pawns.EdificeDamage.Clear(123));

            // A corpse is never cleared — the dead stay where they fell — so it is the last thing
            // checked: an empty registry hashed nothing above, and one corpse is seen.
            colony.Pawns.Corpses.Add(pawn, 30, 2);
            Assert.That(Hash(colony), Is.Not.EqualTo(whole), "the hash cannot see a corpse");
        }

        [Test]
        public void TheCombatStateSurvivesTheRoundTrip()
        {
            var colony = Board();
            colony.World.Tick(30);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            a.HpMilli = -12_345;
            a.Downed = true;
            a.NextSwingTick = 400;
            a.StunnedUntilTick = 90;
            a.RetaliateAgainst = b.Id.Value;
            a.RetaliateUntilTick = 1_230;
            a.EquippedItem = 77;
            a.CombatTarget = b.Id.Value;
            a.CarriedBy = b.Id.Value;
            a.TreatedUntilTick = 15_030; // layout 4, medical supplies (design 37)
            colony.Pawns.Corpses.Add(b, 31, 5);
            colony.Pawns.EdificeDamage.Set(1_234, 55_000);
            colony.Pawns.EdificeDamage.Set(99, 1);

            var restored = Board();
            restored.Load(colony.Save());

            Pawn back = restored.Pawns.Pawns.Get(a.Id)!;
            Assert.That(back.HpMilli, Is.EqualTo(-12_345));
            Assert.That(back.Downed, Is.True);
            Assert.That(back.NextSwingTick, Is.EqualTo(400));
            Assert.That(back.StunnedUntilTick, Is.EqualTo(90));
            Assert.That(back.RetaliateAgainst, Is.EqualTo(b.Id.Value));
            Assert.That(back.RetaliateUntilTick, Is.EqualTo(1_230));
            Assert.That(back.EquippedItem, Is.EqualTo(77));
            Assert.That(back.CombatTarget, Is.EqualTo(b.Id.Value));
            Assert.That(back.CarriedBy, Is.EqualTo(b.Id.Value));
            Assert.That(back.TreatedUntilTick, Is.EqualTo(15_030));

            Assert.That(restored.Pawns.Corpses.Count, Is.EqualTo(1));
            Assert.That(restored.Pawns.Corpses[0].Pawn, Is.EqualTo(b.Id.Value));
            Assert.That(restored.Pawns.Corpses[0].Facing, Is.EqualTo(5));
            Assert.That(restored.Pawns.EdificeDamage.TryGet(1_234, out int left) && left == 55_000, Is.True);
            Assert.That(restored.Pawns.EdificeDamage.Count, Is.EqualTo(2));

            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)));
        }

        /// <summary>
        /// The loader builds every pawn as a colonist and only then reads its kind, and a colonist's
        /// pool is 100 where a hog's is 60 — so a reloaded hog carried 100 of 60 hit points, which
        /// is combat state, saved and hashed. <c>Pawn.Kind</c>'s setter keeps a whole pawn whole
        /// across the change; with it withheld this round trip disagreed with itself (measured).
        /// </summary>
        [Test]
        public void AnAnimalReloadedIsWhole()
        {
            var colony = Board();
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            colony.World.Tick(10);

            var restored = Board();
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(hog.Id)!;

            Assert.That(back.Kind, Is.EqualTo(PawnKindIndex.MiddenHog));
            Assert.That(back.HpMilli, Is.EqualTo(60_000));
            Assert.That(back.HasCombatState, Is.False);
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)));
        }

        /// <summary>C1's layout-1 record still loads, and its pawn comes back whole.</summary>
        [Test]
        public void ALayoutOneSectionLoadsItsPawnWhole()
        {
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];

            var bytes = new MemoryStream();
            using (var binary = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var writer = new SaveWriter(binary);
                writer.Write(1);             // layout
                writer.Write(1);             // one record
                writer.Write(pawn.Id.Value);
                writer.Write(1);             // drafted
                writer.Write(12);            // quiet since
                writer.Write(-1);            // no finishing step
            }
            bytes.Position = 0;
            pawn.HpMilli = 5;
            new CombatSection(colony.Pawns.Pawns).Load(
                new SaveReader(new BinaryReader(bytes), WorldSave.CurrentFormatVersion));

            Assert.That(pawn.Drafted, Is.True);
            Assert.That(pawn.DraftQuietSinceTick, Is.EqualTo(12));
            Assert.That(pawn.HpMilli, Is.EqualTo(pawn.HpMaxMilli), "a layout-1 pawn is whole");
        }

        // ---- the published views ------------------------------------------------------------

        [Test]
        public void EveryPawnViewSaysWhatItIs()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Pawn other = colony.Pawns.Pawns.All[1];
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            other.Downed = true;
            other.StunnedUntilTick = colony.World.CurrentTick + 1_000;
            other.CarriedBy = colonist.Id.Value;
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, colonist.Id.Value, 1)),
                Is.EqualTo(IntentRejection.None));

            WorldSnapshot frame = colony.World.Views.Current;
            PawnFlags Of(Pawn pawn) => frame.TryGetPawn(pawn.Id, out PawnView view) ? view.Flags : (PawnFlags)0xFF;

            Assert.That(Of(colonist), Is.EqualTo(PawnFlags.Person | PawnFlags.Drafted));
            Assert.That(Of(other), Is.EqualTo(PawnFlags.Person | PawnFlags.Downed | PawnFlags.Stunned | PawnFlags.Carried));
            Assert.That(Of(hog), Is.EqualTo(PawnFlags.None));
            Assert.That(Of(bandit), Is.EqualTo(PawnFlags.Person | PawnFlags.Hostile | PawnFlags.Drawn),
                "a bandit spawns armed and always has its weapon out (design 33 §8b)");

            Assert.That(frame.TryGetPawn(bandit.Id, out PawnView m) && m.IsPerson && !m.IsColonist && !m.IsAnimal, Is.True,
                "a bandit is kind 3 and a person: exactly the case 'kind 0 or an animal' got wrong");
        }

        /// <summary>
        /// A view built by hand — a test, an editor tool — reads as every view did before the flags:
        /// kind 0 a person, anything else an animal. The simulation always says.
        /// </summary>
        [Test]
        public void AViewBuiltWithoutFlagsReadsItsKindTheOldWay()
        {
            Assert.That(new PawnView(new PawnId(1), default, 0, 0, 0).IsColonist, Is.True);
            Assert.That(new PawnView(new PawnId(2), default, 0, 0, 0, kind: 2).IsAnimal, Is.True);
            Assert.That(new PawnView(new PawnId(3), default, 0, 0, 0, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile).IsColonist, Is.False);
        }

        [Test]
        public void TheAspectNamesAreSpelledAsTheInterfaceReadsThem()
        {
            // Held to the literals Odyssey.Hud.CombatAspectNames and OrderModel read;
            // CombatAspectNamesTests holds the other side.
            Assert.That(CombatAspects.DraftedName, Is.EqualTo("odyssey.pawn.drafted"));
            Assert.That(CombatAspects.OrderCellName, Is.EqualTo("odyssey.pawn.order.cell"));
            Assert.That(CombatAspects.HpName, Is.EqualTo("odyssey.pawn.hp"));
            Assert.That(CombatAspects.HpMaxName, Is.EqualTo("odyssey.pawn.hp.max"));
            Assert.That(CombatAspects.WeaponName, Is.EqualTo("odyssey.pawn.weapon"));
            Assert.That(CombatAspects.OrderTargetName, Is.EqualTo("odyssey.pawn.order.target"));
            Assert.That(CombatAspects.RescueNoBedName, Is.EqualTo("odyssey.pawn.rescue.nobed"));
            // Design 33 §18: the response is read by the interface; the patient by presentation,
            // which reads this constant itself.
            Assert.That(CombatAspects.ResponseName, Is.EqualTo("odyssey.pawn.response"));
            Assert.That(CombatAspects.RescuePatientName, Is.EqualTo("odyssey.pawn.rescue.patient"));
        }

        /// <summary>
        /// Sparse: a whole, undrafted colonist publishes none of the fight; a hurt one its hit
        /// points; one under orders its target; one holding a weapon the weapon's def.
        /// </summary>
        [Test]
        public void TheFightsAspectsArePublishedOnlyWhileTheyHaveSomethingToSay()
        {
            var colony = Board();
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            colony.World.Tick();
            WorldSnapshot before = colony.World.Views.Current;
            Assert.That(before.TryGetPawnAspect(a.Id, CombatAspects.Hp, out _), Is.False);
            Assert.That(before.TryGetPawnAspect(a.Id, CombatAspects.OrderTarget, out _), Is.False);
            Assert.That(before.TryGetPawnAspect(a.Id, CombatAspects.Weapon, out _), Is.False);

            a.HpMilli = 40_000;
            a.CombatTarget = b.Id.Value;
            int free = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, b.Cell, ItemIndex.ArcBlade, 1, maxRadius: 8);
            Assume.That(free, Is.GreaterThanOrEqualTo(0), "nowhere to put the blade down");
            ThingId blade = colony.Pawns.Items.Spawn(ItemIndex.ArcBlade, free);
            a.EquippedItem = blade.Value;
            colony.World.Tick();
            WorldSnapshot after = colony.World.Views.Current;

            Assert.That(after.TryGetPawnAspect(a.Id, CombatAspects.Hp, out int hp) && hp == 40_000, Is.True);
            Assert.That(after.TryGetPawnAspect(a.Id, CombatAspects.HpMax, out int max) && max == 100_000, Is.True);
            Assert.That(after.TryGetPawnAspect(a.Id, CombatAspects.Weapon, out int weapon) && weapon == ItemIndex.ArcBlade, Is.True);
            Assert.That(after.TryGetPawnAspect(b.Id, CombatAspects.Hp, out _), Is.False, "the whole one said something");

            // A target with no order behind it publishes nothing since design 33 §18b: the aspect
            // means an attack the player ordered. The order is the control.
            Assert.That(after.TryGetPawnAspect(a.Id, CombatAspects.OrderTarget, out _), Is.False,
                "a target nobody ordered was published as an order");
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, a.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, new Intent(IntentKind.OrderAttack, colony.Pawns.Size.FromIndex(b.Cell), a.Id.Value, b.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(a.Id, CombatAspects.OrderTarget, out int target)
                && target == b.Id.Value, Is.True, "the order's target was not published");
        }

        /// <summary>
        /// The pool is published for every person, hurt or whole, so the Health tab can say
        /// "100 / 100" of a colonist nobody has touched — and for an animal only beside its hit
        /// points, because an animal has no Health tab. An absent hp with a pool reads as whole.
        /// </summary>
        [Test]
        public void EveryPersonPublishesItsPoolAndAWholeAnimalDoesNot()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            colony.World.Tick();
            WorldSnapshot whole = colony.World.Views.Current;

            Assert.That(whole.TryGetPawnAspect(colonist.Id, CombatAspects.HpMax, out int max) && max == 100_000, Is.True,
                "a whole colonist's pool was not published");
            Assert.That(whole.TryGetPawnAspect(colonist.Id, CombatAspects.Hp, out _), Is.False,
                "a whole colonist published hit points, which is what says a bar is owed");
            Assert.That(whole.TryGetPawnAspect(bandit.Id, CombatAspects.HpMax, out _), Is.True);
            Assert.That(whole.TryGetPawnAspect(hog.Id, CombatAspects.HpMax, out _), Is.False,
                "a whole animal published a pool nothing reads");

            hog.HpMilli = 30_000;
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(hog.Id, CombatAspects.HpMax, out int hogMax)
                && hogMax == 60_000, Is.True, "a hurt animal's pool goes out beside its hit points");
        }

        // ---- seams the seam review found (2026-09-23) -------------------------------------------

        /// <summary>
        /// The bandit is armed (design 33 §1), and the content says with what: the kind names a
        /// weapon, and every spawn of a kind that names one passes through
        /// <see cref="IWeaponRules.ArmOnSpawn"/> — lane D's seam, which puts it in the hand. The
        /// control is the hog spawned the same way, whose kind names nothing and never reaches it.
        /// </summary>
        [Test]
        public void ABanditIsSpawnedThroughTheArmingSeamAndAnAnimalIsNot()
        {
            var colony = Board();
            PawnContent content = colony.Pawns.Content;
            Assert.That(content.KindWeapons[PawnKindIndex.Bandit],
                Is.EquivalentTo(new[] { ItemIndex.Crowbar, ItemIndex.Bat }),
                "a bandit carries a crowbar or a bat, never a blade (design 42)");
            Assert.That(content.ArmsOnSpawn(0), Is.False, "a colonist arrives bare-handed");
            Assert.That(content.ArmsOnSpawn(PawnKindIndex.MiddenHog), Is.False);
            Assert.That(content.WeaponFor(0, 1, 7u), Is.EqualTo(-1));
            foreach (int def in content.KindWeapons[PawnKindIndex.Bandit])
                Assert.That(content.Items[def].weapon, Is.Not.Null, "the bandit's weapon is a weapon");

            var arming = new ArmingRecorder();
            colony.Pawns.WeaponRules = arming;
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);
            Assert.That(arming.Armed, Is.Empty, "a kind that names no weapon was armed");

            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            Assert.That(arming.Armed, Is.EqualTo(new[] { bandit.Id.Value }));
            Assert.That(arming.KnownWhenArmed, Is.True, "armed before the registry knew the pawn");
            Assert.That(hog.EquippedItem, Is.EqualTo(0));

            // Lane D filled the hand (design 33 §6D): the real rules arm the bandit with the
            // kind's own weapon. BanditArmsTests holds the rest of it.
            colony.Pawns.WeaponRules = new WeaponRules();
            Pawn armed = SpawnKind(colony, PawnKindIndex.Bandit);
            Assert.That(colony.Pawns.Items.Get(new ThingId(armed.EquippedItem))?.DefIndex,
                Is.EqualTo(content.WeaponFor(PawnKindIndex.Bandit, armed.Id.Value, armed.RollSeed)));
        }

        sealed class ArmingRecorder : WeaponRules
        {
            public readonly System.Collections.Generic.List<int> Armed = new System.Collections.Generic.List<int>();
            public bool KnownWhenArmed = true;

            public override void ArmOnSpawn(Pawn pawn, PawnContext ctx)
            {
                Armed.Add(pawn.Id.Value);
                if (ctx.Pawns.Get(pawn.Id) != pawn || pawn.Cell < 0) KnownWhenArmed = false;
            }
        }

        static bool OneStepApart(int a, int b)
        {
            CellRef p = Size.FromIndex(a), q = Size.FromIndex(b);
            return System.Math.Abs(p.X - q.X) <= 1 && System.Math.Abs(p.Z - q.Z) <= 1 && System.Math.Abs(p.Y - q.Y) <= 1;
        }

        /// <summary>A drafted colonist walking a long order east, part way along it.</summary>
        static Pawn Walking(ColonyWorld colony)
        {
            Pawn pawn = colony.Pawns.Pawns.All[0];
            colony.World.Tick(60);
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 400 && pawn.HasPath; t++) colony.World.Tick();
            CellRef at = Size.FromIndex(pawn.Cell);
            int goal = colony.Pawns.Cells.NearestWalkableInColumn(System.Math.Min(at.X + 20, Size.SizeX - 2), at.Z, at.Y);
            Assume.That(goal, Is.GreaterThanOrEqualTo(0), "nowhere east to walk to");
            Assert.That(Send(colony, new Intent(IntentKind.OrderMove, Size.FromIndex(goal), pawn.Id.Value)), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(30);
            Assume.That(pawn.HasPath, Is.True, "the order is not being walked");
            return pawn;
        }

        /// <summary>
        /// A stun is a pause, not an interrupt (design 33 §4 C3, §5c): the colonist lands the step
        /// she was part way through and takes no other, her job is neither ticked nor ended, and
        /// she carries on when it wears off. The control is the same colonist on the same seed,
        /// unstunned, who walks several cells over the same ticks.
        /// </summary>
        [Test]
        public void AStunHoldsTheStepAndTheJobAndLetsGo()
        {
            var control = Board();
            Pawn free = Walking(control);
            int freeFrom = free.Cell;
            control.World.Tick(300);
            Assert.That(OneStepApart(freeFrom, free.Cell), Is.False, "the control did not walk, so the hold would prove nothing");

            var colony = Board();
            Pawn pawn = Walking(colony);
            int from = pawn.Cell;
            Job job = pawn.CurrentJob!;
            int started = pawn.JobStartTick;
            pawn.StunnedUntilTick = colony.World.CurrentTick + 300;
            colony.World.Tick(300);

            Assert.That(OneStepApart(from, pawn.Cell), Is.True, "a stunned colonist took more than the step in hand");
            Assert.That(pawn.CurrentJob, Is.SameAs(job), "the stun ended her job");
            Assert.That(pawn.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.Goto));
            Assert.That(pawn.JobStartTick, Is.EqualTo(started));

            int held = pawn.Cell;
            colony.World.Tick(300);
            Assert.That(pawn.Cell, Is.Not.EqualTo(held), "she did not carry on when the stun wore off");
        }

        /// <summary>
        /// A stunned pawn does not think either: one between jobs stays between them until the stun
        /// wears off. The control is the same undraft without the stun, which gives her work on the
        /// same tick.
        /// </summary>
        [Test]
        public void AStunnedColonistTakesNoNewJob()
        {
            foreach (bool stunned in new[] { false, true })
            {
                var colony = Board();
                Pawn pawn = colony.Pawns.Pawns.All[0];
                colony.World.Tick(60);
                Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
                for (int t = 0; t < 400 && pawn.HasPath; t++) colony.World.Tick();

                if (stunned) pawn.StunnedUntilTick = colony.World.CurrentTick + 200;
                Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, pawn.Id.Value, 0)), Is.EqualTo(IntentRejection.None));

                if (!stunned)
                {
                    Assert.That(pawn.CurrentJob, Is.Not.Null, "the control: an undrafted colonist thinks on the same tick");
                    continue;
                }
                Assert.That(pawn.CurrentJob, Is.Null, "a stunned colonist thought");
                colony.World.Tick(100);
                Assert.That(pawn.CurrentJob, Is.Null, "a stunned colonist thought");
                colony.World.Tick(150);
                Assert.That(pawn.CurrentJob, Is.Not.Null, "she never thought again after the stun");
            }
        }

        /// <summary>
        /// A pawn leaving the board leaves its bed to nobody (design 33 §5c) — until death, only
        /// animals were ever despawned, and a dead colonist would have kept hers for ever under an
        /// id that no longer exists.
        /// </summary>
        [Test]
        public void ADespawnedColonistOwnsNoBed()
        {
            var colony = Board();
            Pawn gone = colony.Pawns.Pawns.All[0];
            Pawn stays = colony.Pawns.Pawns.All[1];
            int bed = -1;
            for (int cell = 0; cell < Size.CellCount && bed < 0; cell++)
                if (colony.Construction.AssignOwnerAt(cell, gone.Id.Value) == IntentRejection.None) bed = cell;
            Assume.That(bed, Is.GreaterThanOrEqualTo(0), "the board has no bed to own");
            Assert.That(colony.Construction.PawnOwnsABed(gone.Id.Value), Is.True, "the control: she owned it");

            colony.Pawns.Pawns.Despawn(gone);

            Assert.That(colony.Construction.PawnOwnsABed(gone.Id.Value), Is.False, "the dead keep their beds");
            Assert.That(colony.Construction.BedOwnerAt(bed), Is.EqualTo(0));
            Assert.That(colony.Construction.AssignOwnerAt(bed, stays.Id.Value), Is.EqualTo(IntentRejection.None),
                "the bed could not be given to the living");
        }

        /// <summary>
        /// A forced order is for a standing colonist of ours (design 33 §5c): a downed colonist's
        /// Job_Downed is never interruptible, and a bandit is nobody's to command. The control is
        /// the same colonist, standing, who can be sent to the same frame.
        /// </summary>
        [Test]
        public void OnlyAStandingColonistCanBeForced()
        {
            var colony = Board();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);

            int site = -1;
            CellRef at = Size.FromIndex(colonist.Cell);
            for (int dx = -2; dx <= 2 && site < 0; dx++)
            for (int dz = -2; dz <= 2 && site < 0; dz++)
            {
                if (System.Math.Abs(dx) != 2 && System.Math.Abs(dz) != 2) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                if (!colony.Construction.Allows(cell)) continue;
                if (FellJobDriver.StandBeside(colony.Pawns, colonist, cell) < 0) continue;
                site = cell;
            }
            Assume.That(site, Is.GreaterThanOrEqualTo(0), "nowhere to put a frame");
            Assert.That(colony.Construction.Place(Size.FromIndex(site), BuildingHandle.Wall, StuffHandle.Wood), Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(site, 5);

            Assert.That(JobSystem.CanForce(colonist, colony.Pawns, JobIndex.Build, site), Is.True, "the control");
            Assert.That(JobSystem.CanForce(bandit, colony.Pawns, JobIndex.Build, site), Is.False, "a bandit took an order");
            colonist.Downed = true;
            Assert.That(JobSystem.CanForce(colonist, colony.Pawns, JobIndex.Build, site), Is.False, "a downed colonist took an order");
        }

        [Test]
        public void TheCombatLogPublishesItsTailInIdOrder()
        {
            var colony = Board();
            CombatLog log = colony.Pawns.CombatLog;
            for (int i = 0; i < 40; i++)
                log.Report(CombatEventKind.Hit, new PawnId(1), new PawnId(2), new CellRef(3, 4, 1), i, amount: i);
            colony.World.Tick();

            var events = colony.World.Views.Current.CombatEvents;
            Assert.That(events.Length, Is.EqualTo(CombatEventView.PublishedTail));
            for (int i = 0; i < events.Length; i++)
                Assert.That(events[i].Id, Is.EqualTo(40 - CombatEventView.PublishedTail + 1 + i));
            Assert.That(events[events.Length - 1].Amount, Is.EqualTo(39));
        }

        [Test]
        public void ACorpseIsPublishedAsWhoItWas()
        {
            var colony = Board();
            Pawn bandit = SpawnKind(colony, PawnKindIndex.Bandit);
            colony.Pawns.Corpses.Add(bandit, colony.World.CurrentTick, 9);
            colony.World.Tick();

            var corpses = colony.World.Views.Current.Corpses;
            Assert.That(corpses.Length, Is.EqualTo(1));
            Assert.That(corpses[0].Pawn, Is.EqualTo(bandit.Id));
            Assert.That(corpses[0].Kind, Is.EqualTo(PawnKindIndex.Bandit));
            Assert.That(corpses[0].RollSeed, Is.EqualTo(bandit.RollSeed));
            Assert.That(corpses[0].Facing, Is.EqualTo(1), "a facing is one of eight");
            Assert.That(corpses[0].Flags, Is.EqualTo(PawnFlags.Person | PawnFlags.Hostile));
        }

        // ---- the content ----------------------------------------------------------------------

        [Test]
        public void TheContentCarriesTheOwnersNumbers()
        {
            PawnContent content = ContentPack.Pawns();
            SpeciesDef person = content.Species[0], hog = content.Species[1], rat = content.Species[2];

            Assert.That(new[] { person.healthPoints, hog.healthPoints, rat.healthPoints }, Is.EqualTo(new[] { 100, 60, 15 }));
            Assert.That(new[] { person.deathAtPerMille, hog.deathAtPerMille, rat.deathAtPerMille },
                Is.EqualTo(new[] { -500, -500, -500 }));
            Assert.That(hog.revengePerMille, Is.GreaterThan(500), "a hog usually turns");
            Assert.That(rat.revengePerMille, Is.LessThan(500), "a rat usually runs");
            Assert.That(person.naturalAttack, Is.Null, "a person fights with fists");
            Assert.That(hog.naturalAttack, Is.Not.Null);
            Assert.That(rat.naturalAttack, Is.Not.Null);

            CombatDef combat = content.Combat;
            Assert.That(new[] { combat.HitChancePerMille(0), combat.HitChancePerMille(10), combat.HitChancePerMille(20) },
                Is.EqualTo(new[] { 500, 800, 900 }));
            Assert.That(new[] { combat.DodgeChancePerMille(0), combat.DodgeChancePerMille(10), combat.DodgeChancePerMille(20) },
                Is.EqualTo(new[] { 0, 100, 300 }));
            Assert.That(combat.fists.damage, Is.EqualTo(4));
            Assert.That(combat.fists.cooldownTicks, Is.EqualTo(120), "about every two seconds");

            foreach (int item in new[] { ItemIndex.Bat, ItemIndex.Crowbar, ItemIndex.Machete, ItemIndex.ArcBlade })
            {
                ItemDef def = content.Items[item];
                Assert.That(def.category, Is.EqualTo(ItemCategory.Weapons), def.defName);
                Assert.That(def.stackLimit, Is.EqualTo(1), def.defName);
                Assert.That(def.weapon, Is.Not.Null, def.defName);
                Assert.That(def.weapon!.damage, Is.InRange(7, 10), def.defName + ": the owner's 7 to 10");
                Assert.That(def.weapon.cooldownTicks, Is.InRange(96, 144), def.defName + ": the owner's 1.6 to 2.4 s");
                bool blunt = item == ItemIndex.Bat || item == ItemIndex.Crowbar;
                Assert.That(def.weapon.damageKind, Is.EqualTo(blunt ? DamageKind.Blunt : DamageKind.Sharp), def.defName);
                Assert.That(def.weapon.stunPerMille > 0, Is.EqualTo(blunt), def.defName + ": blunt stuns, sharp does not");
            }
            Assert.That(content.Items[ItemIndex.Meal].weapon, Is.Null, "a meal is not a weapon");

            Assert.That(content.Kinds[PawnKindIndex.Colonist].faction, Is.EqualTo(Faction.Colony));
            Assert.That(content.Kinds[PawnKindIndex.MiddenHog].faction, Is.EqualTo(Faction.Wild));
            Assert.That(content.Kinds[PawnKindIndex.DuctRat].faction, Is.EqualTo(Faction.Wild));
            Assert.That(content.Kinds[PawnKindIndex.Bandit].faction, Is.EqualTo(Faction.Hostile));
            Assert.That(content.SpeciesOf(PawnKindIndex.Bandit).person, Is.True);
        }

        [Test]
        public void ACurveIsLinearBetweenItsPointsAndFlatBeyondThem()
        {
            CombatDef combat = ContentPack.Pawns().Combat;
            Assert.That(combat.HitChancePerMille(5), Is.EqualTo(650));
            Assert.That(combat.HitChancePerMille(15), Is.EqualTo(850));
            Assert.That(combat.HitChancePerMille(-3), Is.EqualTo(500));
            Assert.That(combat.HitChancePerMille(40), Is.EqualTo(900));
            Assert.That(CombatDef.Evaluate(new System.Collections.Generic.List<CurvePoint>(), 7), Is.EqualTo(0));
        }

        // ---- the rules' seams ------------------------------------------------------------------

        [Test]
        public void TheRulesReadALevelAndAnArmamentFromContent()
        {
            var colony = Board();
            PawnContext ctx = colony.Pawns;
            Pawn colonist = ctx.Pawns.All[0];
            Pawn hog = SpawnKind(colony, PawnKindIndex.MiddenHog);

            Assert.That(ctx.MeleeRules.MeleeLevel(hog), Is.EqualTo(hog.Species.meleeSkill));
            Assert.That(ctx.MeleeRules.MeleeLevel(colonist), Is.EqualTo(colonist.SkillLevel(SkillIndex.Melee)));
            Assert.That(ctx.MeleeRules.HitChancePerMille(hog, ctx),
                Is.EqualTo(ctx.Content.Combat.HitChancePerMille(hog.Species.meleeSkill)));

            Armament fists = ctx.WeaponRules.ArmamentOf(colonist, ctx);
            Assert.That(fists.Attack, Is.SameAs(ctx.Content.Combat.fists));
            Assert.That(fists.Armed, Is.False);
            Assert.That(ctx.WeaponRules.ArmamentOf(hog, ctx).Attack, Is.SameAs(hog.Species.naturalAttack));
        }

        [Test]
        public void TheHooksCallTheirListenersInTheOrderTheyWereAdded()
        {
            var hooks = new CombatHooks();
            var heard = new System.Collections.Generic.List<string>();
            hooks.Add(new Ear("first", heard));
            hooks.Add(new Ear("second", heard));
            var colony = Board();
            Pawn pawn = colony.Pawns.Pawns.All[0];

            hooks.RaiseDamageApplied(new DamageReport(pawn, null, 1_000, -1, 5));
            hooks.RaiseDowned(pawn, null, 6);
            hooks.RaiseDied(pawn, null, 1, 7);

            Assert.That(heard, Is.EqualTo(new[]
                { "first:damage", "second:damage", "first:downed", "second:downed", "first:died", "second:died" }));
            Assert.Throws<System.InvalidOperationException>(() => hooks.Add(hooks.Listeners[0]));
        }

        sealed class Ear : ICombatListener
        {
            readonly string _name;
            readonly System.Collections.Generic.List<string> _heard;

            public Ear(string name, System.Collections.Generic.List<string> heard)
            {
                _name = name;
                _heard = heard;
            }

            public void SwingResolved(in SwingReport report) { }
            public void DamageApplied(in DamageReport report) => _heard.Add(_name + ":damage");
            public void Downed(Pawn pawn, Pawn? by, int tick) => _heard.Add(_name + ":downed");
            public void Died(Pawn pawn, Pawn? by, int corpseId, int tick) => _heard.Add(_name + ":died");
        }
    }
}
