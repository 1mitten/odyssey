#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The butcher against four colonists with bats (design 62 §10): drafted, ordered on to it, and
    /// left to fight for a game minute and a half on three seeds. <b>A probe, not a gate</b> — it
    /// asserts nothing about who wins, only what must hold whoever does: nobody ends a tick inside
    /// something they cannot stand in, the fight ends in downs rather than deaths (design 33 §3's
    /// unordered-fight rule does not apply to an ordered fight, so a death is logged, not failed),
    /// and a lockstep twin run beside it hashes the same at the end. What it prints is the number
    /// the owner tunes against: flings, slams, colonists down and the tick the butcher fell.
    /// </summary>
    public class ButcherBalanceProbe
    {
        const int Ticks = 5_400;

        static readonly GridSize ProbeSize = new GridSize(60, 60, 16);

        sealed class Fight
        {
            public ColonyWorld Colony = null!;
            public Pawn Butcher = null!;
            public Pawn[] People = null!;
            public Tape Tape = new Tape();
        }

        static Fight Stage(uint seed, int kind)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 4;
            scenario.beds = 4;
            var colony = ColonyWorld.Build(ProbeSize, seed, scenario, barren: true, wooded: false);
            colony.World.Tick();
            Pawn[] people = colony.Pawns.Pawns.All.Take(4).ToArray();
            var arms = new HeldWeapon();
            foreach (Pawn p in people) arms.Give(p, ItemHandle.Bat);
            colony.Pawns.WeaponRules = arms;

            CellRef s = colony.Start;
            int at = colony.Pawns.Cells.NearestWalkableInColumn(s.X + 8, s.Z + 8, s.Y);
            Pawn butcher = Spawn(colony, kind, at);
            foreach (Pawn p in people)
            {
                Assert.That(Draft(colony, p), Is.EqualTo(IntentRejection.None));
                Assert.That(Attack(colony, p, butcher), Is.EqualTo(IntentRejection.None));
            }
            return new Fight { Colony = colony, Butcher = butcher, People = people };
        }

        [Test, Category("Long")]
        [TestCase(1u, PawnKindIndex.Butcher)]
        [TestCase(2u, PawnKindIndex.Butcher)]
        [TestCase(3u, PawnKindIndex.Butcher)]
        [TestCase(1u, PawnKindIndex.ButcherScarred)]
        [TestCase(1u, PawnKindIndex.ButcherBlood)]
        [TestCase(1u, PawnKindIndex.ButcherKing)]
        public void OneButcherAgainstFourBats(uint seed, int kind)
        {
            Fight fight = Stage(seed, kind), twin = Stage(seed, kind);
            int fell = -1;
            for (int t = 0; t < Ticks; t++)
            {
                fight.Colony.World.Tick();
                twin.Colony.World.Tick();
                fight.Tape.Read(fight.Colony);
                if (fell < 0 && (fight.Butcher.Downed || Melee.IsDead(fight.Butcher))) fell = fight.Colony.World.CurrentTick;

                if (t % 60 != 0) continue;
                foreach (Pawn p in fight.Colony.Pawns.Pawns.All)
                {
                    if (Melee.IsDead(p) || p.CarriedBy != 0) continue;
                    Assert.That(fight.Colony.Pawns.Cells.IsWalkable(p.Cell), Is.True,
                        $"seed {seed}, tick {fight.Colony.World.CurrentTick}: pawn {p.Id.Value} in a cell it cannot stand in");
                }
            }

            Assert.That(twin.Colony.World.ComputeStateHash().Value, Is.EqualTo(fight.Colony.World.ComputeStateHash().Value),
                "the lockstep twin diverged");

            var mine = fight.Tape.Events.Where(e => e.Attacker == fight.Butcher.Id).ToList();
            int swings = mine.Count(e => e.Kind == CombatEventKind.Swing || e.Kind == CombatEventKind.SwingCritical);
            int blows = mine.Count(e => e.Kind == CombatEventKind.Hit);
            int flings = mine.Count(e => e.Kind == CombatEventKind.KnockedBack);
            int slams = mine.Count(e => e.Kind == CombatEventKind.Slam);
            int down = fight.People.Count(p => p.Downed);
            int dead = fight.People.Count(p => Melee.IsDead(p) || fight.Colony.Pawns.Pawns.Get(p.Id) == null);
            TestContext.WriteLine(
                $"kind {kind}, seed {seed}: {swings} swings, {blows} blows landed, {flings} flings, {slams} slams; " +
                $"colonists down {down}, dead {dead}; butcher {fight.Butcher.HpMilli / 1000}/{fight.Butcher.HpMaxMilli / 1000} hp, " +
                (fell >= 0 ? $"fell at tick {fell}" : "still standing"));
            Assert.That(swings, Is.GreaterThan(0), "the butcher never swung: the probe measured nothing");
        }
    }
}
