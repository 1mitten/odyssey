#nullable enable
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A marauder a day for ten days (design 33 §6A), on the soak's own board and colony: the
    /// fight left running unattended, with the colony's ordinary life around it. What it holds is
    /// what a two-minute test cannot see — that nothing throws, that the fight's invariants hold at
    /// every sample (a pawn past its death line never outlives its tick, a downed pawn is on
    /// <c>Job_Downed</c>, the reservation table agrees with the pawns), that every death left
    /// exactly one corpse, that the fight actually happened, and that a save taken in the middle of
    /// it resumes on the same hash a day later.
    ///
    /// <para><b>Fists only until lane D's merge</b> arms the marauder with its machete; the
    /// integrator's run after that merge is the one with steel in it. Nothing here rescues a
    /// downed colonist (C4), so the colony wears down over the ten days, which is the honest
    /// picture of C2 alone.</para>
    /// </summary>
    public class MarauderSoakTests
    {
        static readonly GridSize PlaySize = new GridSize(120, 120, 16);
        const int Day = 60_000;
        const int SampleEvery = 500;

        [Test, Category("Long")]
        public void AMarauderADayForTenDays()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.stockpileCells = 9;
            ColonyWorld colony = ColonyWorld.Build(PlaySize, 1u, scenario);
            var hooks = new CombatFixture.HookCounter();
            colony.Pawns.CombatHooks.Add(hooks);
            var rules = new CombatFixture.RecordingRules();
            colony.Pawns.MeleeRules = rules;

            int spawned = 0, recovered = 0;
            ulong resumed = 0, original = 0;
            byte[]? midway = null;
            var tape = new CombatFixture.Tape();
            var watch = Stopwatch.StartNew();

            for (int day = 0; day < 10; day++)
            {
                // A quarter into the day, twenty cells out along a heading that turns each day.
                colony.World.Tick((Day / 4 - colony.World.CurrentTick % Day + Day) % Day);
                int dx = (day % 4 < 2 ? 1 : -1) * 20, dz = (day % 2 == 0 ? 1 : -1) * 20;
                CellRef at = colony.Start;
                int before = colony.Pawns.Pawns.Count;
                colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn,
                    new CellRef(at.X + dx, at.Z + dz, at.Y), PawnKindIndex.Marauder));
                colony.World.Tick();
                if (colony.Pawns.Pawns.Count == before + 1) spawned++;

                if (day == 5)
                {
                    midway = colony.Save();
                }

                for (int t = colony.World.CurrentTick % Day; t < Day - 1; t += SampleEvery)
                {
                    int step = System.Math.Min(SampleEvery, Day - 1 - t);
                    for (int s = 0; s < step; s++)
                    {
                        colony.World.Tick();
                        tape.Read(colony);
                    }
                    Invariants(colony, day);
                }

                if (day == 5)
                {
                    // A day on from the save: the loaded world must have lived the same day.
                    var loaded = ColonyWorld.Build(PlaySize, 1u, scenario);
                    loaded.Load(midway!);
                    loaded.World.Tick(colony.World.CurrentTick - loaded.World.CurrentTick);
                    resumed = loaded.World.ComputeStateHash().Value;
                    original = colony.World.ComputeStateHash().Value;
                }
            }
            watch.Stop();

            recovered = tape.Of(CombatEventKind.Recovered).Count;
            int standing = 0, downed = 0, marauders = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                if (pawn.IsHostile) marauders++;
                if (pawn.Downed) downed++; else standing++;
            }

            TestContext.WriteLine(
                $"marauder soak: {watch.Elapsed.TotalSeconds:F1} s wall; {spawned} marauders spawned, {marauders} left on the board; " +
                $"{rules.Swings.Count} swings resolved, {tape.Of(CombatEventKind.Hit).Count} hits, " +
                $"{hooks.DownedCount} downed, {hooks.DiedCount} died, {recovered} got up; " +
                $"{standing} standing and {downed} down at the end; jobs failed — attack " +
                $"{colony.Jobs.FailedOf(JobIndex.AttackMelee)}, downed {colony.Jobs.FailedOf(JobIndex.Downed)}, " +
                $"flee {colony.Jobs.FailedOf(JobIndex.Flee)}");

            Assert.That(spawned, Is.EqualTo(10), "a marauder could not be spawned");
            Assert.That(rules.Swings.Count, Is.GreaterThan(100), "ten marauders and hardly a blow: the fight never happened");
            Assert.That(hooks.DownedCount, Is.GreaterThan(0), "ten days of fighting and nobody went down");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(hooks.DiedCount), "a death without its corpse, or a corpse without a death");
            Assert.That(colony.Jobs.FailedOf(JobIndex.Downed), Is.EqualTo(0), "Job_Downed failed: somebody got up by the wrong door");
            Assert.That(resumed, Is.EqualTo(original), "a save taken mid-fight did not resume the same");
        }

        static void Invariants(ColonyWorld colony, int day)
        {
            int held = 0;
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
            {
                held += pawn.HeldReservations.Count;
                string who = $"day {day}, tick {colony.World.CurrentTick}, pawn {pawn.Id.Value}";
                Assert.That(Melee.IsDead(pawn), Is.False, $"{who}: dead and still on the board");
                Assert.That(pawn.HpMilli, Is.LessThanOrEqualTo(pawn.HpMaxMilli), $"{who}: over its pool");
                if (pawn.HpMilli <= 0) Assert.That(pawn.Downed, Is.True, $"{who}: at {pawn.HpMilli} and standing");
                if (pawn.Downed)
                    Assert.That((long)pawn.HpMilli * 1_000, Is.LessThan((long)pawn.HpMaxMilli * pawn.Content.Combat.downedRecoverAtPerMille),
                        $"{who}: down past the line it gets up at");
                if (pawn.Downed && pawn.FinishingStepTo < 0)
                    Assert.That(pawn.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Downed), $"{who}: down and doing something else");
                Assert.That(pawn.Downed && pawn.Drafted, Is.False, $"{who}: down and drafted");
            }
            Assert.That(colony.Pawns.Reservations.ActiveClaims, Is.EqualTo(held), $"day {day}: the reservation table disagrees with the pawns");
        }
    }
}
