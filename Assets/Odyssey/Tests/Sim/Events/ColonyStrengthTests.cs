#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// Colony strength (design 68 §4): people and weapons only. Each input raises it; the things
    /// the owner ruled out — buildings, sandbags, animals, weapons in a stockpile — leave it where it
    /// was; a downed colonist counts nothing; and the storyteller remembers a peak that outlives an
    /// hour of disarming.
    /// </summary>
    public class ColonyStrengthTests
    {
        static ColonyWorld Colony(uint seed = 7u)
        {
            ColonyWorld colony = Board(colonists: 2, seed: seed);
            colony.World.Tick();
            return colony;
        }

        static Pawn First(ColonyWorld colony) => colony.Pawns.Pawns.All.First(p => p.IsColonist);

        static ThingId Arm(ColonyWorld colony, Pawn pawn, int item)
        {
            int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, pawn.Cell, item, 1, JobDriver.DropSearchRadius);
            ThingId id = colony.Pawns.Items.Spawn(item, cell);
            WeaponHand.TakeUp(pawn, colony.Pawns.Items.Get(id)!, colony.Pawns);
            return id;
        }

        [Test]
        public void AWeaponRaisesIt()
        {
            ColonyWorld colony = Colony();
            int bare = ColonyStrength.Of(colony.Pawns);
            Arm(colony, First(colony), ItemIndex.Crowbar);
            Assert.That(ColonyStrength.Of(colony.Pawns), Is.GreaterThan(bare));
        }

        [Test]
        public void SkillRaisesIt()
        {
            ColonyWorld colony = Colony();
            Pawn pawn = First(colony);
            SetMelee(pawn, 0);
            int low = ColonyStrength.Of(colony.Pawns);
            SetMelee(pawn, 15);
            Assert.That(ColonyStrength.Of(colony.Pawns), Is.GreaterThan(low));
        }

        [Test]
        public void HurtLowersItAndDownedIsNothing()
        {
            ColonyWorld colony = Colony();
            Pawn pawn = First(colony);
            int whole = ColonyStrength.PowerOf(pawn, colony.Pawns);
            RaiseHp(pawn, pawn.HpMaxMilli / 2);
            Assert.That(ColonyStrength.PowerOf(pawn, colony.Pawns), Is.LessThan(whole));

            colony.Pawns.Combat!.Down(pawn, null, -1, colony.World.CurrentTick);
            Assert.That(ColonyStrength.PowerOf(pawn, colony.Pawns), Is.Zero);
        }

        [Test]
        public void FortificationsAnimalsAndStockpilesAreNeverRead()
        {
            ColonyWorld colony = Colony();
            int before = ColonyStrength.Of(colony.Pawns);

            int at = Near(colony, 6, 6), bags = 0, walls = 0;
            for (int i = 0; i < 3; i++)
            {
                int bag = at + i;
                if (colony.Construction.Place(Size.FromIndex(bag), BuildingHandle.Sandbags, StuffHandle.Stone, 0) == IntentRejection.None
                    && colony.Construction.Raise(colony.Pawns, bag)) bags++;
                int wall = at + i + 2 * Size.SizeX;
                if (colony.Construction.Place(Size.FromIndex(wall), BuildingHandle.Wall, StuffHandle.Stone, 0) == IntentRejection.None
                    && colony.Construction.Raise(colony.Pawns, wall)) walls++;
            }
            Assert.That(bags, Is.GreaterThan(0), "the control: no sandbag went up");
            Assert.That(walls, Is.GreaterThan(0), "the control: no wall went up");
            Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -4, 4));
            colony.Pawns.Items.Spawn(ItemIndex.Pistol, Near(colony, 3, -3));
            colony.Pawns.Items.Spawn(ItemIndex.Crowbar, Near(colony, 4, -3));

            Assert.That(ColonyStrength.Of(colony.Pawns), Is.EqualTo(before),
                "a wall, a sandbag, an animal or a weapon on the ground moved the strength; building a defence would summon a bigger raid");
        }

        [Test]
        public void ARaiderIsPricedByItsMix()
        {
            ColonyWorld colony = Colony();
            IncidentContent content = colony.Incidents.Content;
            int bandits = ColonyStrength.RaiderPowerOf(colony.Pawns.Content, content.Mixes[0]);
            int gunmen = ColonyStrength.RaiderPowerOf(colony.Pawns.Content, content.Mixes[1]);
            int mixed = ColonyStrength.RaiderPowerOf(colony.Pawns.Content, content.Mixes[2]);
            Assert.That(gunmen, Is.GreaterThan(bandits), "a pistol outhits a bat");
            Assert.That(mixed, Is.InRange(bandits, gunmen));
        }

        [Test]
        public void ThePeakOutlivesAnHourOfDisarmingAndFadesOverDays()
        {
            ColonyWorld colony = Colony();
            Pawn pawn = First(colony);
            Arm(colony, pawn, ItemIndex.Pistol);
            Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob));
            Storyteller teller = colony.Pawns.Storyteller!;
            int armed = teller.StrengthPeak;
            Assert.That(armed, Is.EqualTo(ColonyStrength.Of(colony.Pawns)), "the peak starts at the strength of the moment");

            WeaponHand.PutDown(pawn, colony.Pawns, pawn.Cell);
            Assert.That(ColonyStrength.Of(colony.Pawns), Is.LessThan(armed), "the control: disarmed is weaker");

            colony.World.Tick(Calendar.TicksPerHour * 2);
            Assert.That(teller.StrengthPeak, Is.GreaterThan(ColonyStrength.Of(colony.Pawns)),
                "stowing the gun bought a smaller raid within the hour");

            colony.World.Tick(Calendar.TicksPerDay * 3);
            Assert.That(teller.StrengthPeak, Is.LessThan(armed), "the peak never let go");
        }

        [Test]
        public void ADebugAutoRaidIsTheStorytellersRaid()
        {
            ColonyWorld colony = Colony();
            IncidentContent content = colony.Incidents.Content;
            RaidParams p = content.Defs[IncidentHandle.Raid].raid!;
            int untold = RaidWorker.SizeFor(colony.Pawns, content, p, 0, -1, colony.World.CurrentTick);
            Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob));
            int told = RaidWorker.SizeFor(colony.Pawns, content, p, 0, -1, colony.World.CurrentTick);
            Assert.That(told, Is.EqualTo(untold), "at Normal, at the first tension and the first peak, the two sums are one");

            // Harder difficulty, bigger raid.
            Send(colony, new Intent(IntentKind.SetDifficulty, default, 6, 300 | (100 << 16), 100 | (1 << 16)));
            int hard = RaidWorker.SizeFor(colony.Pawns, content, p, 0, -1, colony.World.CurrentTick);
            Assert.That(hard, Is.GreaterThan(told));
        }
    }
}
