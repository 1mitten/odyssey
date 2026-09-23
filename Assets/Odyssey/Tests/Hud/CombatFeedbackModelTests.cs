#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// What the interface says about a fight (design 33 §1): the four answers lane B draws — a bar
    /// over the hurt and the drafted, the floating words and their ink, the hostile marker — and
    /// the two it may also ask, how long a word floats and what colour a bar is.
    /// </summary>
    public class CombatFeedbackModelTests
    {
        static readonly PawnId Whole = new PawnId(1), Hurt = new PawnId(2), Drafted = new PawnId(3),
            Hog = new PawnId(4), Raider = new PawnId(5), Down = new PawnId(6), WholeHog = new PawnId(7);

        const int Pool = 100_000;

        static WorldSnapshot Board()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Whole, new CellRef(1, 1, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Hurt, new CellRef(2, 1, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Drafted, new CellRef(3, 1, 1), 800, 800, 700,
                flags: PawnFlags.Person | PawnFlags.Drafted));
            frame.AddPawn(new PawnView(Hog, new CellRef(4, 1, 1), 800, 800, 700, kind: 1, flags: PawnFlags.None));
            frame.AddPawn(new PawnView(Raider, new CellRef(5, 1, 1), 800, 800, 700, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawn(new PawnView(Down, new CellRef(6, 1, 1), 800, 800, 700,
                flags: PawnFlags.Person | PawnFlags.Downed));
            frame.AddPawn(new PawnView(WholeHog, new CellRef(7, 1, 1), 800, 800, 700, kind: 1, flags: PawnFlags.None));

            // As the simulation publishes them (design 33 §5d): the pool for every person always,
            // hit points only while hurt, downed or drafted; an animal's pool only beside its hp.
            foreach (PawnId person in new[] { Whole, Hurt, Drafted, Raider, Down })
                frame.AddPawnAspect(new PawnAspect(person, CombatAspectNames.HpMaxKey, Pool));
            frame.AddPawnAspect(new PawnAspect(Hurt, CombatAspectNames.HpKey, 42_300));
            frame.AddPawnAspect(new PawnAspect(Drafted, CombatAspectNames.HpKey, Pool));
            frame.AddPawnAspect(new PawnAspect(Hog, CombatAspectNames.HpKey, 30_000));
            frame.AddPawnAspect(new PawnAspect(Hog, CombatAspectNames.HpMaxKey, 60_000));
            frame.AddPawnAspect(new PawnAspect(Raider, CombatAspectNames.HpKey, 80_000));
            frame.AddPawnAspect(new PawnAspect(Down, CombatAspectNames.HpKey, -12_000));
            return frame;
        }

        static bool Bar(WorldSnapshot frame, PawnId id, out int hp, out int max)
        {
            frame.TryGetPawn(id, out PawnView view);
            return CombatFeedbackModel.HealthBar(frame, view, out hp, out max);
        }

        /// <summary>
        /// The owner's rule is a bar over the hurt and the drafted and nobody else (design 33 §1);
        /// the simulation says which by publishing hit points, so the whole colonist beside the
        /// hurt one is the control.
        /// </summary>
        [Test]
        public void ABarStandsOverTheHurtAndTheDraftedAndNobodyElse()
        {
            WorldSnapshot frame = Board();

            Assert.That(Bar(frame, Whole, out _, out _), Is.False, "a whole colonist wore a bar");
            Assert.That(Bar(frame, WholeHog, out _, out _), Is.False, "a whole hog wore a bar");

            Assert.That(Bar(frame, Hurt, out int hp, out int max), Is.True);
            Assert.That((hp, max), Is.EqualTo((42_300, Pool)));

            Assert.That(Bar(frame, Drafted, out hp, out max), Is.True, "a drafted colonist wears a full bar");
            Assert.That((hp, max), Is.EqualTo((Pool, Pool)));

            Assert.That(Bar(frame, Hog, out hp, out max), Is.True);
            Assert.That((hp, max), Is.EqualTo((30_000, 60_000)));

            Assert.That(Bar(frame, Raider, out hp, out max), Is.True);
            Assert.That((hp, max), Is.EqualTo((80_000, Pool)));
        }

        /// <summary>A downed pawn is below nought until it dies at −50 %; its bar is empty, never negative.</summary>
        [Test]
        public void ADownedPawnsBarIsEmptyRatherThanNegative()
        {
            Assert.That(Bar(Board(), Down, out int hp, out int max), Is.True);
            Assert.That(hp, Is.Zero);
            Assert.That(max, Is.EqualTo(Pool));
        }

        [Test]
        public void ABarWithNoPoolIsNotDrawn()
        {
            // hp without a pool beside it is a frame the simulation never publishes; drawing it
            // would divide by nought.
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Hurt, new CellRef(2, 1, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawnAspect(new PawnAspect(Hurt, CombatAspectNames.HpKey, 50_000));
            Assert.That(Bar(frame, Hurt, out _, out _), Is.False);
        }

        static CombatEventView Event(CombatEventKind kind, int amount = 0) =>
            new CombatEventView(1, 100, kind, new PawnId(1), new PawnId(2), new CellRef(2, 1, 1), amount);

        [Test]
        public void TheWordsAreTheRegistrysAndTheDamageIsInWholePoints()
        {
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Miss)),
                Is.EqualTo(Registry.Label("ui.combat.miss")));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Dodge)),
                Is.EqualTo(Registry.Label("ui.combat.dodge")));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Stun, 60)),
                Is.EqualTo(Registry.Label("ui.combat.stunned")));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Downed)),
                Is.EqualTo(Registry.Label("ui.status.downed")));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Died)),
                Is.EqualTo(Registry.Label("ui.combat.dead")));

            // Thousandths in, whole points out, rounded to the nearest; a blow that landed is
            // never "-0".
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Hit, 7_400)), Is.EqualTo("-7"));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Hit, 7_500)), Is.EqualTo("-8"));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Hit, 12_000)), Is.EqualTo("-12"));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Hit, 200)), Is.EqualTo("-1"));
            Assert.That(CombatFeedbackModel.FloatingText(Event(CombatEventKind.Hit, 250_000)), Is.EqualTo("-250"));
        }

        [Test]
        public void ASwingAndAGettingUpFloatNothing()
        {
            foreach (CombatEventKind kind in new[] { CombatEventKind.None, CombatEventKind.Swing, CombatEventKind.Recovered })
            {
                Assert.That(CombatFeedbackModel.FloatingText(Event(kind, 30)), Is.Empty, kind.ToString());
                Assert.That(CombatFeedbackModel.FloatingColour(Event(kind)).A, Is.Zero, kind.ToString());
                Assert.That(CombatFeedbackModel.FloatingSeconds(Event(kind)), Is.Zero, kind.ToString());
            }
        }

        /// <summary>
        /// Every word that floats has an ink you can see and a life longer than a frame; damage is
        /// the bad red, a miss the dim white, and the two words that end a fight outlast a number.
        /// </summary>
        [Test]
        public void EveryFloatingWordHasAnInkAndALifetime()
        {
            foreach (CombatEventKind kind in new[] { CombatEventKind.Hit, CombatEventKind.Miss, CombatEventKind.Dodge,
                         CombatEventKind.Stun, CombatEventKind.Downed, CombatEventKind.Died })
            {
                CombatEventView moment = Event(kind, 5_000);
                Assert.That(CombatFeedbackModel.FloatingColour(moment).A, Is.GreaterThan(0.5f), kind.ToString());
                Assert.That(CombatFeedbackModel.FloatingSeconds(moment), Is.GreaterThan(0.25f), kind.ToString());
            }

            Assert.That(CombatFeedbackModel.FloatingColour(Event(CombatEventKind.Hit, 5_000)), Is.EqualTo(HudTheme.Bad));
            Assert.That(CombatFeedbackModel.FloatingColour(Event(CombatEventKind.Miss)),
                Is.Not.EqualTo(CombatFeedbackModel.FloatingColour(Event(CombatEventKind.Hit, 5_000))));
            Assert.That(CombatFeedbackModel.FloatingSeconds(Event(CombatEventKind.Died)),
                Is.GreaterThan(CombatFeedbackModel.FloatingSeconds(Event(CombatEventKind.Hit, 5_000))));
        }

        [Test]
        public void TheMarkerIsOnAHostileOnly()
        {
            WorldSnapshot frame = Board();
            foreach (PawnView pawn in frame.Pawns)
                Assert.That(CombatFeedbackModel.HostileMarker(pawn), Is.EqualTo(pawn.Id == Raider), $"pawn {pawn.Id.Value}");
        }

        [Test]
        public void TheBarIsGreenWhenWellAndRedWhenLow()
        {
            Assert.That(CombatFeedbackModel.HealthBarColour(Pool, Pool), Is.EqualTo(HudTheme.Good));
            Assert.That(CombatFeedbackModel.HealthBarColour(50_000, Pool), Is.EqualTo(HudTheme.Warn));
            Assert.That(CombatFeedbackModel.HealthBarColour(20_000, Pool), Is.EqualTo(HudTheme.Bad));
            Assert.That(CombatFeedbackModel.HealthBarColour(0, Pool), Is.EqualTo(HudTheme.Bad));
        }
    }
}
