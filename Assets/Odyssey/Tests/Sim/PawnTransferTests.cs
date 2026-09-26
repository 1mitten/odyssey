#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Expeditions;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// EX0 and EX3 (design 64 §6b, §6e): a colonist taken off one board and put on another, whole.
    ///
    /// <para>EX0 was the spike that did it naively — <c>Despawn</c> on one board, <c>Adopt</c> on
    /// another — to measure what breaks before sizing the unit; what it found is in design 64 §15a.
    /// The same test is kept as the regression for <see cref="PawnTransfer"/>.</para>
    /// </summary>
    public class PawnTransferTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);

        /// <summary>Home with three colonists, armed, one of them hurt and one carrying a load.</summary>
        static ColonyWorld Home(uint seed = 41u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            // Somewhere to haul the meals to, so somebody picks something up (the bare scenario
            // has no store, and with nowhere to take a thing nobody lifts it).
            scenario.stockpileCells = 9;
            return ColonyWorld.Build(Size, seed, scenario);
        }

        /// <summary>A second board on the same clock, the same content and the same id counter.</summary>
        static ColonyWorld Away(ColonyWorld home, uint seed = 42u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 0;
            scenario.beds = 0;
            return ColonyWorld.Build(new ColonyRequest
            {
                Size = Size,
                Seed = seed,
                Scenario = scenario,
                StartTick = home.World.CurrentTick,
                Content = home.Pawns.Content,
                PawnIds = home.Pawns.Pawns.Ids,
            });
        }

        /// <summary>Tick home until somebody is holding something, and return her.</summary>
        static Pawn Carrier(ColonyWorld home)
        {
            for (int t = 0; t < 4_000; t++)
            {
                home.World.Tick();
                foreach (Pawn pawn in home.Pawns.Pawns.All)
                    if (pawn.IsColonist && pawn.CurrentJob != null && pawn.CurrentJob.CarriedItem >= 0) return pawn;
            }
            Assert.Fail("nobody picked anything up in 4,000 ticks");
            return null!;
        }

        static void Arm(ColonyWorld colony)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.DebugArmColonists));
            colony.World.Tick();
        }

        static void Bruise(ColonyWorld colony, Pawn pawn)
        {
            colony.Pawns.Combat!.Hurt(pawn, null, 6_000, AfflictionKind.Bruise, HitSet.Melee, weapon: -1,
                tick: colony.World.CurrentTick);
        }

        /// <summary>What a colonist is, apart from where she is: the fields that must survive a trip.</summary>
        static string Portrait(Pawn pawn)
        {
            var parts = new List<string>
            {
                "id " + pawn.Id.Value, "seed " + pawn.RollSeed, "kind " + pawn.Kind,
                "hp " + pawn.HpMilli, "mood " + pawn.Mood, "response " + pawn.Response, "area " + pawn.Area,
                "skills " + string.Join(",", pawn.Skills), "passions " + string.Join(",", pawn.Passions),
                "priorities " + string.Join(",", pawn.WorkPriorities), "schedule " + string.Join(",", pawn.ScheduleHours),
                "needs " + string.Join(",", pawn.Needs), "memories " + pawn.Memories.Count,
                "injuries " + (pawn.Health == null ? 0 : pawn.Health.Count),
                "bloodloss " + (pawn.Health == null ? 0 : pawn.Health.BloodLossMicro),
            };
            return string.Join(" | ", parts);
        }

        [Test]
        public void AColonistCrossesToAnotherBoardWithEverythingSheIs()
        {
            ColonyWorld home = Home();
            Arm(home);
            Pawn traveller = Carrier(home);
            Bruise(home, traveller);
            Assume.That(traveller.HasHealthState, Is.True, "the bruise landed");
            Assume.That(traveller.EquippedItem, Is.Not.EqualTo(0), "she is armed");
            ColonyItem? weapon = home.Pawns.Items.Get(new ThingId(traveller.EquippedItem));
            ColonyItem load = home.Pawns.Items.Get(new ThingId(traveller.CurrentJob!.CarriedItem))!;
            int weaponDef = weapon!.DefIndex, weaponQuality = weapon.Quality, loadDef = load.DefIndex, loadStack = load.Stack;
            int bed = home.Construction.BedOwnerAt(home.Pawns.Items.Beds[0]) == traveller.Id.Value ? home.Pawns.Items.Beds[0] : -1;
            string before = Portrait(traveller);

            ColonyWorld away = Away(home);
            List<CargoLine> cargo = PawnTransfer.Detach(home, traveller);

            Assert.That(home.Pawns.Pawns.Get(traveller.Id), Is.Null, "she is off home");
            Assert.That(traveller.HeldReservations, Is.Empty, "and holds nothing there");
            Assert.That(cargo, Has.Some.Matches<CargoLine>(c => c.IsWeapon && c.DefIndex == weaponDef && c.Quality == weaponQuality),
                "the weapon went with her, quality and all");
            Assert.That(cargo, Has.Some.Matches<CargoLine>(c => !c.IsWeapon && c.DefIndex == loadDef && c.Stack == loadStack),
                "and so did what she was carrying");
            if (bed >= 0) Assert.That(home.Construction.BedOwnerAt(bed), Is.EqualTo(traveller.Id.Value), "she keeps her bed");

            int onTheGround = CountOf(away, loadDef);
            int arrival = PawnTransfer.EdgeCell(away, BoardSide.West);
            PawnTransfer.Arrive(away, traveller, arrival, cargo);

            Assert.That(away.Pawns.Pawns.Get(traveller.Id), Is.SameAs(traveller));
            Assert.That(Portrait(traveller), Is.EqualTo(before), "she is who she was");
            ColonyItem? held = WeaponHand.Held(traveller, away.Pawns);
            Assert.That(held, Is.Not.Null, "armed on arrival");
            Assert.That(held!.DefIndex, Is.EqualTo(weaponDef));
            Assert.That(held.Quality, Is.EqualTo(weaponQuality));
            Assert.That(CountOf(away, loadDef) - onTheGround, Is.EqualTo(loadStack), "the load is on the ground where she came in");

            // A day on both boards, then a save and a load of the one she is on.
            for (int t = 0; t < 2_500; t++) { home.World.Tick(); away.World.Tick(); }
            Assert.That(away.Pawns.Pawns.Get(traveller.Id), Is.Not.Null, "she is still there a while later");
            byte[] saved = away.Save();
            ColonyWorld reloaded = Away(home);
            reloaded.Load(saved);
            Assert.That(reloaded.World.ComputeStateHash().Value, Is.EqualTo(away.World.ComputeStateHash().Value),
                "a board with a traveller on it round-trips");
        }

        [Test]
        public void ADownedOrBleedingColonistCannotBeDetached()
        {
            ColonyWorld home = Home(43u);
            Pawn pawn = home.Pawns.Pawns.All[0];
            home.Pawns.Combat!.Hurt(pawn, null, 3_000, AfflictionKind.Wound, HitSet.Melee, weapon: -1,
                tick: home.World.CurrentTick);
            Assume.That(pawn.Health!.BleedingSeverityMilli, Is.GreaterThan(0));
            Assert.That(PawnTransfer.CanDepart(pawn), Is.EqualTo(DepartRefusal.Bleeding));
        }

        /// <summary>
        /// <b>The codec round-trips a colonist exactly</b> (design 64 §12): every colonist of a colony
        /// that has been working, fighting and bleeding for an hour, detached and then written and
        /// read, hashes the same as she did detached. The hash is the pawn's own <c>ContributeTo</c>,
        /// which is what the board's state hash is built from.
        /// </summary>
        [Test]
        public void TheCodecRoundTripsEveryColonistToTheSameHash()
        {
            ColonyWorld home = Home(44u);
            Arm(home);
            home.World.Tick(2_000);
            Pawn first = home.Pawns.Pawns.All[0];
            Bruise(home, first);
            home.Pawns.Combat!.Hurt(home.Pawns.Pawns.All[1], null, 2_000, AfflictionKind.Wound, HitSet.Melee,
                weapon: -1, tick: home.World.CurrentTick);
            home.World.Tick(500);

            var travellers = new List<Pawn>();
            foreach (Pawn pawn in home.Pawns.Pawns.All) if (pawn.IsColonist) travellers.Add(pawn);
            Assume.That(travellers.Count, Is.EqualTo(3));
            foreach (Pawn pawn in travellers) PawnTransfer.Detach(home, pawn);

            foreach (Pawn pawn in travellers)
            {
                byte[] bytes;
                using (var stream = new System.IO.MemoryStream())
                {
                    using (var binary = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                        PawnRecord.Write(new Odyssey.Sim.Saving.SaveWriter(binary), pawn);
                    bytes = stream.ToArray();
                }
                Pawn back;
                using (var stream = new System.IO.MemoryStream(bytes))
                using (var binary = new System.IO.BinaryReader(stream))
                    back = PawnRecord.Read(new Odyssey.Sim.Saving.SaveReader(binary,
                        Odyssey.Sim.Saving.WorldSave.CurrentFormatVersion), home.Pawns.Content);

                Assert.That(Hash(back), Is.EqualTo(Hash(pawn)), $"colonist {pawn.Id.Value} changed on the way through the codec");
                Assert.That(Portrait(back), Is.EqualTo(Portrait(pawn)));
            }
        }

        static ulong Hash(Pawn pawn)
        {
            StateHash hash = StateHash.New();
            pawn.ContributeTo(ref hash);
            return hash.Value;
        }

        static int CountOf(ColonyWorld colony, int def)
        {
            int total = 0;
            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.DefIndex == def && item.Cell >= 0) total += item.Stack;
            return total;
        }
    }
}
