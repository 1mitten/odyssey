#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The health bar on a roster card (design 33 §9f, owner: <i>"Their health needs to be also
    /// displayed on their colony stats as it appears above them"</i>). Always shown; read from the
    /// two aspects the bar over the head reads; coloured by the one owner of that bar's colours;
    /// empty and red with "Downed" while downed.
    /// </summary>
    public class RosterHealthTests
    {
        const int Pool = 100_000;
        static readonly PawnId Ada = new PawnId(1);

        /// <summary>One colonist with the pool every person always publishes, and <paramref name="hp"/> beside it if given.</summary>
        static WorldSnapshot Board(int? hp, PawnFlags flags = PawnFlags.Person, bool pool = true)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Wait, flags: flags));
            if (pool) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpMaxKey, Pool));
            if (hp.HasValue) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpKey, hp.Value));
            return frame;
        }

        static RosterCard Card(WorldSnapshot frame)
        {
            var roster = new RosterModel();
            roster.Refresh(frame, PawnId.None);
            Assert.That(roster.Cards.Count, Is.EqualTo(1), "the colonist has no card");
            return roster.Cards[0];
        }

        /// <summary>
        /// Always shown: a colonist nobody has hurt and nobody has drafted has no <c>hp</c> in the
        /// frame and no bar over her head, and her card still carries a full green bar — a pool
        /// with nothing beside it is a whole colonist (design 33 §5d).
        /// </summary>
        [Test]
        public void AWholeColonistsCardIsFullAndGreen()
        {
            RosterCard card = Card(Board(hp: null));

            Assert.That(card.Health, Is.EqualTo(1000), "a pool with no hit points beside it is whole");
            Assert.That(card.HealthInk, Is.EqualTo(CombatFeedbackModel.HealthGood));
            Assert.That(card.Downed, Is.False);
            Assert.That(card.HealthWord, Is.Empty);
        }

        /// <summary>The fill is the hit points over the pool, and the three bands are the overhead bar's: 60 % and 40 %.</summary>
        [TestCase(73_000, 730, "good")]
        [TestCase(60_000, 600, "good")]
        [TestCase(59_999, 599, "warn")]
        [TestCase(40_000, 400, "warn")]
        [TestCase(39_999, 399, "bad")]
        [TestCase(5_000, 50, "bad")]
        public void AHurtColonistsCardReadsTheHitPointsInTheBarsBands(int hp, int perMille, string band)
        {
            RosterCard card = Card(Board(hp));

            // The ink is the table's (design 59); the band names the side of the ramp it falls on.
            HudColour expected = StatInks.Ink(StatInks.Health, perMille, StatPalette.World);
            Assert.That(card.Health, Is.EqualTo(perMille));
            Assert.That(card.HealthInk, Is.EqualTo(expected), $"{hp} of {Pool} should be in the {band} band");
            Assert.That(card.Downed, Is.False);
            Assert.That(card.HealthWord, Is.Empty);
        }

        /// <summary>
        /// "As it appears above them": wherever the bar over the head is owed, the card says the
        /// same fraction in the same ink, across the whole range a pawn's hit points can take —
        /// down to the −50 % at which it dies, and a published value past the pool.
        /// </summary>
        [Test]
        public void TheCardAgreesWithTheBarOverHerHeadAtEveryHitPoint()
        {
            for (int hp = -Pool / 2; hp <= Pool + 1000; hp += 250)
            {
                WorldSnapshot frame = Board(hp);
                RosterCard card = Card(frame);
                Assert.That(frame.TryGetPawn(Ada, out PawnView pawn), Is.True);
                Assert.That(CombatFeedbackModel.HealthBar(frame, pawn, out int barHp, out int barMax), Is.True);

                Assert.That(card.Health, Is.EqualTo((int)((long)barHp * 1000 / barMax)), $"fill at hp {hp}");
                Assert.That(card.HealthInk, Is.EqualTo(CombatFeedbackModel.HealthBarColour(barHp, barMax)), $"ink at hp {hp}");
            }
        }

        /// <summary>
        /// Downed: empty, red, and the word. Asked of the flag rather than of the hit points, so a
        /// downed pawn reads down whatever the number below nought says — and a frame that still
        /// carries a positive number for one tick does not draw a downed colonist as standing.
        /// </summary>
        [TestCase(-20_000)]
        [TestCase(0)]
        [TestCase(35_000)]
        public void ADownedColonistsCardIsEmptyRedAndSaysDowned(int hp)
        {
            RosterCard card = Card(Board(hp, PawnFlags.Person | PawnFlags.Downed));

            Assert.That(card.Downed, Is.True);
            Assert.That(card.Health, Is.EqualTo(0), "a downed colonist's bar is empty");
            Assert.That(card.HealthInk, Is.EqualTo(CombatFeedbackModel.HealthBad));
            Assert.That(card.HealthWord, Is.EqualTo(Registry.Label("ui.status.downed")));
            Assert.That(card.HealthWord, Is.EqualTo("Downed"), "the registry's word for the status");
        }

        /// <summary>
        /// The control for the one above: the same number without the flag is a hurt colonist on
        /// her feet, with no word — so the word comes from the flag and nothing else.
        /// </summary>
        [Test]
        public void TheSameHitPointsWithoutTheFlagAreNotDowned()
        {
            RosterCard card = Card(Board(35_000));

            Assert.That(card.Downed, Is.False);
            Assert.That(card.Health, Is.EqualTo(350));
            Assert.That(card.HealthWord, Is.Empty);
        }

        /// <summary>
        /// A frame with no pool (one from before combat, or built by hand) says nothing about
        /// health, and the card says nothing either: −1, the empty track, never a guess at whole.
        /// </summary>
        [Test]
        public void NoPoolIsNoReadingRatherThanAGuess()
        {
            RosterCard card = Card(Board(hp: null, pool: false));

            Assert.That(card.Health, Is.EqualTo(-1));
            Assert.That(card.Downed, Is.False);
        }

        /// <summary>Every card on a page carries its own colonist's reading, not its neighbour's.</summary>
        [Test]
        public void EachCardReadsItsOwnColonist()
        {
            WorldSnapshot frame = Frame.Write();
            var bram = new PawnId(2);
            var cato = new PawnId(3);
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(bram, new CellRef(2, 1, 1), 800, 800, 800, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(cato, new CellRef(3, 1, 1), 800, 800, 800,
                flags: PawnFlags.Person | PawnFlags.Downed));
            foreach (PawnId id in new[] { Ada, bram, cato })
                frame.AddPawnAspect(new PawnAspect(id, CombatAspectNames.HpMaxKey, Pool));
            frame.AddPawnAspect(new PawnAspect(bram, CombatAspectNames.HpKey, 45_000));
            frame.AddPawnAspect(new PawnAspect(cato, CombatAspectNames.HpKey, -10_000));

            var roster = new RosterModel();
            roster.Refresh(frame, PawnId.None);

            Assert.That(roster.Cards.Count, Is.EqualTo(3));
            Assert.That(roster.Cards[0].Health, Is.EqualTo(1000));
            Assert.That(roster.Cards[1].Health, Is.EqualTo(450));
            Assert.That(roster.Cards[1].HealthInk, Is.EqualTo(StatInks.Ink(StatInks.Health, 450, StatPalette.World)));
            Assert.That(roster.Cards[2].Health, Is.EqualTo(0));
            Assert.That(roster.Cards[2].Downed, Is.True);
        }
    }
}
