#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The cover line's gate (design 53 §11, CV9): a skirmish on three seeds — two drafted colonists
    /// with pistols behind a line of sandbags against three pistol bandits, and the same fight on
    /// open ground — run beside a lockstep twin, saved mid-fight and resumed. The colonists behind
    /// the bags take markedly fewer bullets, the bags take the difference and wear, the twin never
    /// parts, and the load resumes the same fight.
    /// </summary>
    public class CoverGateTests
    {
        const int FightTicks = 1_500;

        static int Offset(int cell, int dx, int dz = 0)
        {
            CellRef c = Size.FromIndex(cell);
            return Size.Index(c.X + dx, c.Z + dz, c.Y);
        }

        sealed class Fight
        {
            public ColonyWorld Colony = null!;
            public readonly List<Pawn> Colonists = new List<Pawn>();
            public readonly Tape Tape = new Tape();
            public int SandbagCells;
        }

        static Fight Stage(uint seed, bool sandbags)
        {
            var fight = new Fight { Colony = Board(colonists: 2, seed: seed) };
            ColonyWorld colony = fight.Colony;
            colony.World.Tick();
            int spot = Near(colony, -15, 8);
            for (int i = 0; i < 2; i++)
            {
                Pawn colonist = colony.Pawns.Pawns.All[i];
                fight.Colonists.Add(colonist);
                int at = Offset(spot, 0, i * 2);
                Stand(colony, colonist, at);
                int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, at, ItemIndex.Pistol, 1, JobDriver.DropSearchRadius);
                ThingId pistol = colony.Pawns.Items.Spawn(ItemIndex.Pistol, cell);
                WeaponHand.TakeUp(colonist, colony.Pawns.Items.Get(pistol)!, colony.Pawns);
            }
            if (sandbags)
            {
                for (int dz = -1; dz <= 3; dz++)
                {
                    int bag = Offset(spot, 1, dz);
                    if (colony.Construction.Place(Size.FromIndex(bag), BuildingHandle.Sandbags, StuffHandle.Stone, 0) != IntentRejection.None) continue;
                    if (colony.Construction.Raise(colony.Pawns, bag)) fight.SandbagCells++;
                }
            }
            foreach (Pawn colonist in fight.Colonists) Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            for (int b = 0; b < 3; b++)
                colony.Pawns.Pawns.Spawn(Offset(spot, 11, b * 2 - 1), PawnKindIndex.Bandit, ItemIndex.Pistol);
            return fight;
        }

        static int ShotsTaken(Fight fight)
        {
            int taken = 0;
            foreach (var e in fight.Tape.Of(CombatEventKind.Hit))
                foreach (Pawn colonist in fight.Colonists)
                    if (e.Target == colonist.Id) taken++;
            return taken;
        }

        [Test, Category("Long")]
        public void TheGateBehindSandbagsAgainstTheOpen()
        {
            int coveredTaken = 0, openTaken = 0, coveredEvents = 0, bagDamage = 0;
            var watch = Stopwatch.StartNew();
            foreach (uint seed in new uint[] { 7u, 11u, 23u })
            {
                foreach (bool sandbags in new[] { true, false })
                {
                    Fight run = Stage(seed, sandbags);
                    Fight twin = Stage(seed, sandbags);
                    for (int t = 0; t < FightTicks; t++)
                    {
                        run.Tape.Tick(run.Colony, 1);
                        twin.Colony.World.Tick();
                        if (t % 60 == 59)
                            Assert.That(twin.Colony.World.ComputeStateHash().Value, Is.EqualTo(run.Colony.World.ComputeStateHash().Value),
                                $"seed {seed}, sandbags {sandbags}: the twin parted at tick {t + 1}");
                    }
                    int taken = ShotsTaken(run);
                    if (sandbags)
                    {
                        coveredTaken += taken;
                        coveredEvents += run.Tape.Of(CombatEventKind.Covered).Count;
                        foreach (var row in run.Colony.World.Views.Current.EdificeDamage)
                            if (row.Edifice == EdificeHandle.Sandbags) bagDamage += row.MaxMilli - row.HpMilli;
                    }
                    else openTaken += taken;
                    TestContext.WriteLine($"seed {seed}, sandbags {sandbags} ({run.SandbagCells} cells): colonists took {taken} bullets, " +
                        $"{run.Tape.Of(CombatEventKind.Shot).Count} shots, {run.Tape.Of(CombatEventKind.Covered).Count} caught by cover");
                }
            }
            TestContext.WriteLine($"behind sandbags {coveredTaken} bullets taken against {openTaken} in the open; " +
                $"{coveredEvents} caught by cover, {bagDamage / 1_000} hit points off the bags; {watch.Elapsed.TotalSeconds:F1} s");

            Assert.That(openTaken, Is.GreaterThan(10), "the control fight is a real fight");
            Assert.That(coveredEvents, Is.GreaterThan(0), "the bags took bullets");
            Assert.That(bagDamage, Is.GreaterThan(0), "and wore");
            Assert.That(coveredTaken, Is.LessThan(openTaken * 3 / 4), "cover protects");
        }

        /// <summary>A save taken mid-fight, behind sandbags and in the open, resumes the same fight, tick for tick.</summary>
        [TestCase(true), Category("Long")]
        [TestCase(false)]
        public void ASaveTakenMidFightResumesTheSame(bool sandbags)
        {
            Fight run = Stage(7u, sandbags);
            ColonyWorld colony = run.Colony;
            for (int t = 0; t < 400; t++) colony.World.Tick();

            byte[] saved = colony.Save();
            var restored = Board(colonists: 2, seed: 7u);
            restored.Load(saved);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
            for (int t = 0; t < 600; t++)
            {
                colony.World.Tick();
                restored.World.Tick();
                Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value),
                    $"diverged {t + 1} ticks after the load");
            }
        }
    }
}
