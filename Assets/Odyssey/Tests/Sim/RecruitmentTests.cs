#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;
using static Odyssey.Tests.Sim.PrisonFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Recruitment (design 58 §8): a warden talks a prisoner in Recruit mode round, a visible bar
    /// filled by a deterministic gain from the warden's Social, the prisoner's mood and how she is
    /// kept. No hidden roll: the hours the pane shows are the hours the chats take, and when the bar
    /// is full she joins — free, dealt a colonist's skills, her prison bed given back.
    /// </summary>
    public class RecruitmentTests
    {
        static ColonyWorld Colony() => Board(colonists: 1, beds: 0);

        static Pawn Warden(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        static void SetSocial(ColonyWorld colony, Pawn pawn, int level)
        {
            var def = colony.Pawns.Content.Skills[SkillIndex.Social];
            int xp = 0;
            while (def.Level(xp) < level) xp += 100;
            pawn.Skills[SkillIndex.Social] = xp;
            Assume.That(pawn.SkillLevel(SkillIndex.Social), Is.EqualTo(level));
        }

        static void Recruit(ColonyWorld colony, Pawn prisoner) =>
            Assume.That(Send(colony, new Intent(IntentKind.SetPrisonMode, default, prisoner.Id.Value, (int)PrisonMode.Recruit)),
                Is.EqualTo(IntentRejection.None));

        static void WellKept(ColonyWorld colony, Pawn prisoner)
        {
            prisoner.Needs[NeedIndex.Food] = colony.Pawns.Content.Needs[NeedIndex.Food].max;
            prisoner.Mood = 500;
        }

        // ---- the arithmetic --------------------------------------------------------------------

        [TestCase(0, 400)]
        [TestCase(5, 1_000)]
        [TestCase(10, 1_600)]
        public void SocialScalesTheGain(int level, int s)
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            WellKept(colony, prisoner);
            Pawn warden = Warden(colony);
            SetSocial(colony, warden, level);

            RecruitFactors f = Recruitment.Factors(prisoner, warden, colony.Pawns);
            Assert.That(f.Social, Is.EqualTo(s));
            Assert.That(f.Mood, Is.EqualTo(1_000), "mood 500 is the unit");
            Assert.That(f.Treatment, Is.EqualTo(1_000), "fed, tended, a normal cell");
            Assert.That(f.Gain, Is.EqualTo(50 * s), "gain = 50,000 x S x 1 x 1 / 1000");
        }

        [Test]
        public void MoodHasAFloorAndIsNamedWhenLow()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            WellKept(colony, prisoner);
            prisoner.Mood = 0;
            RecruitFactors f = Recruitment.Factors(prisoner, Warden(colony), colony.Pawns);
            Assert.That(f.Mood, Is.EqualTo(250), "a quarter speed, never nothing");
            Assert.That(f.Blockers & RecruitBlockers.LowMood, Is.EqualTo(RecruitBlockers.LowMood));

            prisoner.Mood = 800;
            f = Recruitment.Factors(prisoner, Warden(colony), colony.Pawns);
            Assert.That(f.Mood, Is.EqualTo(1_450));
            Assert.That(f.Blockers & RecruitBlockers.LowMood, Is.EqualTo(RecruitBlockers.None));
        }

        [Test]
        public void HungerAndShacklesSlowItAndSaySo()
        {
            ColonyWorld colony = Colony();
            colony.World.Tick();
            int bed = ShackleBed(colony, -8);
            Pawn prisoner = HeldOn(colony, bed, Near(colony, -6, 0));
            WellKept(colony, prisoner);
            prisoner.Needs[NeedIndex.Food] = 1;

            RecruitFactors f = Recruitment.Factors(prisoner, Warden(colony), colony.Pawns);
            Assert.That(f.Treatment, Is.EqualTo(500 * 600 / 1_000), "hungry x shackled");
            Assert.That(f.Blockers, Is.EqualTo(RecruitBlockers.Hungry | RecruitBlockers.Shackled | RecruitBlockers.LowSocial));
        }

        [Test]
        public void NoWardenMeansNoETAAndTheReasonIsNamed()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            Recruit(colony, prisoner);
            Warden(colony).WorkPriorities[WorkTypeIndex.Warden] = 0;

            Assert.That(Recruitment.BestWarden(colony.Pawns), Is.Null);
            Assert.That(Recruitment.HoursToJoin(prisoner, colony.Pawns), Is.EqualTo(-1));
            Assert.That(Recruitment.Factors(prisoner, null, colony.Pawns).Blockers & RecruitBlockers.NoWarden,
                Is.EqualTo(RecruitBlockers.NoWarden));
        }

        /// <summary>
        /// <b>The ETA a player reads is the arithmetic the chats run.</b> The published hours are
        /// what <see cref="Recruitment.HoursToJoin"/> says, and that many hours of six-hourly chats
        /// fill the bar exactly — one chat fewer does not.
        /// </summary>
        [Test]
        public void ThePublishedHoursAreTheChatsItTakes()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            WellKept(colony, prisoner);
            Recruit(colony, prisoner);
            Pawn warden = Warden(colony);
            SetSocial(colony, warden, 5);
            colony.World.Tick();

            Assert.That(colony.World.Views.Current.TryGetPawnAspect(prisoner.Id, PrisonAspects.Hours, out int hours), Is.True);
            Assert.That(hours, Is.EqualTo(Recruitment.HoursToJoin(prisoner, colony.Pawns)));
            int chats = hours / 6;
            int gain = Recruitment.Factors(prisoner, warden, colony.Pawns).Gain;
            Assert.That((long)chats * gain, Is.GreaterThanOrEqualTo(Recruitment.Full - prisoner.Prison!.Willingness));
            Assert.That((long)(chats - 1) * gain, Is.LessThan(Recruitment.Full - prisoner.Prison.Willingness),
                "and not one chat more than it needs");
        }

        // ---- the chat ---------------------------------------------------------------------------

        [Test]
        public void AWardenTalksToAPrisonerInRecruitModeAndSocialTrains()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            WellKept(colony, prisoner);
            Recruit(colony, prisoner);
            Pawn warden = Warden(colony);
            int social = warden.Skills[SkillIndex.Social];

            for (int t = 0; t < 4_000 && prisoner.Prison!.Willingness == 0; t++) colony.World.Tick();
            Assert.That(prisoner.Prison!.Willingness, Is.GreaterThan(0), "a chat was had");
            Assert.That(prisoner.Prison.LastChatTick, Is.GreaterThan(0));
            Assert.That(warden.Skills[SkillIndex.Social], Is.GreaterThan(social), "and the warden learnt from it");

            int after = prisoner.Prison.Willingness;
            for (int t = 0; t < Recruitment.ChatIntervalTicks / 2; t++) colony.World.Tick();
            Assert.That(prisoner.Prison.Willingness, Is.EqualTo(after), "not again inside six hours");
        }

        [Test]
        public void APrisonerOnHoldIsNotTalkedTo()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            WellKept(colony, prisoner);
            Assume.That(prisoner.Prison!.Mode, Is.EqualTo(PrisonMode.Hold), "nothing is recruited the player did not choose");
            for (int t = 0; t < 4_000; t++) colony.World.Tick();
            Assert.That(prisoner.Prison.Willingness, Is.Zero);
        }

        [Test]
        public void WhenTheBarIsFullSheJoins()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            WellKept(colony, prisoner);
            Recruit(colony, prisoner);
            prisoner.Prison!.Willingness = Recruitment.Full - 1;
            int recruited = colony.Incidents.Ledger.Fires(IncidentHandle.Recruited);

            for (int t = 0; t < 4_000 && prisoner.Custody != PawnCustody.Free; t++) colony.World.Tick();
            Assert.That(prisoner.Custody, Is.EqualTo(PawnCustody.Free));
            Assert.That(prisoner.IsColonist, Is.True, "one of ours");
            Assert.That(prisoner.IsHostile, Is.False);
            Assert.That(prisoner.Prison.Joined, Is.True);
            Assert.That(prisoner.Prison.Dressed, Is.False, "out of the jumpsuit");
            Assert.That(colony.Construction.BedOwnerAt(cell.Bed), Is.Not.EqualTo(prisoner.Id.Value), "the prison bed goes back");
            Assert.That(prisoner.Skills.Any(s => s > 0), Is.True, "a raider is dealt the skills a raid never gave her");
            Assert.That(prisoner.WorkPriorities.All(p => p == 3), Is.True);
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Recruited), Is.EqualTo(recruited + 1), "and the colony is told");
        }

        [Test]
        public void TheModeIntentRefusesRansomAndTheFree()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            Assert.That(Send(colony, new Intent(IntentKind.SetPrisonMode, default, prisoner.Id.Value, (int)PrisonMode.Ransom)),
                Is.EqualTo(IntentRejection.NotPermitted), "ransom is a seam until factions exist");
            Assert.That(Send(colony, new Intent(IntentKind.SetPrisonMode, default, Warden(colony).Id.Value, (int)PrisonMode.Recruit)),
                Is.EqualTo(IntentRejection.NotPermitted), "a free colonist has no mode");
            Assert.That(Send(colony, new Intent(IntentKind.SetPrisonMode, default, prisoner.Id.Value, (int)PrisonMode.Hold)),
                Is.EqualTo(IntentRejection.AlreadyInThatState));
        }
    }
}
