#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Friendly fire, C5 (design 33 §1, §12): a colonist hurt by a colonist remembers it for a day,
    /// every colonist feels a colonist's death for three, and a colonist Ctrl-attacked by another
    /// fights back. The two memories are <see cref="FriendlyFireListener"/>'s, heard on the fight's
    /// hooks; the order and the answering blow were C2's and are joined here end to end.
    /// </summary>
    public class FriendlyFireTests
    {
        static int Copies(Pawn pawn, int thought) => pawn.Memories.Count(m => m.ThoughtIndex == thought);

        static int ExpiryOf(Pawn pawn, int thought) => pawn.Memories.First(m => m.ThoughtIndex == thought).ExpiryTick;

        /// <summary>The mood one thought alone gives her at <paramref name="tick"/>: every other memory set aside.</summary>
        static int OffsetOf(Pawn pawn, int thought, int tick)
        {
            pawn.Memories.RemoveAll(m => m.ThoughtIndex != thought);
            return pawn.MemoryMoodOffset(tick);
        }

        /// <summary>One blow from where she stands to past the death line.</summary>
        static int Fatal(Pawn pawn) => pawn.HpMilli - pawn.DeathAtMilli;

        [Test]
        public void TheTwoThoughtsAreTheOwnersNumbersOnOurScale()
        {
            // The owner's -8 and -6 are points on a mood of a hundred; ours is thousandths of a
            // thousand (design 33 §12b), the scale a night on the ground's -40 is on.
            PawnContent content = ContentPack.Pawns();

            ThoughtDef attacked = content.Thoughts[ThoughtIndex.AttackedByColonist];
            Assert.That(attacked.defName, Is.EqualTo("Thought_AttackedByColonist"));
            Assert.That(attacked.moodOffset, Is.EqualTo(-80));
            Assert.That(attacked.durationTicks, Is.EqualTo(Calendar.TicksPerDay));
            Assert.That(attacked.stackLimit, Is.EqualTo(1));

            ThoughtDef died = content.Thoughts[ThoughtIndex.ColonistDied];
            Assert.That(died.defName, Is.EqualTo("Thought_ColonistDied"));
            Assert.That(died.moodOffset, Is.EqualTo(-60));
            Assert.That(died.durationTicks, Is.EqualTo(3 * Calendar.TicksPerDay));
            Assert.That(died.stackLimit, Is.EqualTo(3));

            Assert.That(content.Thoughts, Has.Length.EqualTo(ThoughtIndex.Count));
        }

        [Test]
        public void AColonistHurtByAColonistRemembersItForADay()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn victim = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "the control: nothing before the blow");

            int tick = colony.World.CurrentTick;
            Strike(colony, by, victim, 1_000);

            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(1));
            Assert.That(ExpiryOf(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(tick + Calendar.TicksPerDay));
            Assert.That(Copies(by, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "the one who struck was not struck");

            Assert.That(OffsetOf(victim, ThoughtIndex.AttackedByColonist, tick), Is.EqualTo(-80));
            Assert.That(victim.MemoryMoodOffset(tick + Calendar.TicksPerDay - 1), Is.EqualTo(-80));
            Assert.That(victim.MemoryMoodOffset(tick + Calendar.TicksPerDay), Is.EqualTo(0), "it outlived its day");
        }

        /// <summary>
        /// The controls on the attack: a marauder's blow is not friendly fire, a swing that missed
        /// is not remembered, and a colonist hitting a marauder gives nobody anything.
        /// </summary>
        [Test]
        public void AMaraudersBlowAndAMissAreNotFriendlyFire()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn victim = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 10, 10));
            int tick = colony.World.CurrentTick;

            Strike(colony, marauder, victim, 1_000);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "a marauder's blow was remembered");

            colony.Pawns.Combat!.ApplySwing(by, victim, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Miss), tick);
            colony.Pawns.Combat!.ApplySwing(by, victim, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Dodge), tick);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "a swing that never landed was remembered");

            Strike(colony, by, marauder, 1_000);
            Assert.That(Copies(marauder, ThoughtIndex.AttackedByColonist), Is.EqualTo(0));
            Assert.That(Copies(by, ThoughtIndex.AttackedByColonist), Is.EqualTo(0));

            Strike(colony, by, victim, 1_000);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "the control: a colonist's blow that lands");
        }

        /// <summary>
        /// Stack limit one, as every thought is added (<c>Pawn.AddMemory</c> drops a copy past the
        /// limit): a second blow later in the day neither adds a copy nor moves the day on (§12b).
        /// </summary>
        [Test]
        public void ASecondBlowNeitherStacksNorRenews()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn victim = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            int first = colony.World.CurrentTick;
            Strike(colony, by, victim, 1_000);
            colony.World.Tick(100);
            Assert.That(colony.World.CurrentTick, Is.GreaterThan(first));

            Strike(colony, by, victim, 1_000);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(1));
            Assert.That(ExpiryOf(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(first + Calendar.TicksPerDay),
                "the second blow renewed the day");
        }

        /// <summary>
        /// A colonist's death is felt by every other colonist on the board, standing or downed, for
        /// three days — and by no marauder, no animal, and not by the dead.
        /// </summary>
        [Test]
        public void EveryOtherColonistFeelsAColonistsDeath()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn dies = colony.Pawns.Pawns.All[0], stands = colony.Pawns.Pawns.All[1], lies = colony.Pawns.Pawns.All[2];
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 10, 10));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -10, -10));

            Strike(colony, marauder, lies, lies.HpMilli);
            Assert.That(lies.Downed, Is.True);
            Assert.That(Copies(stands, ThoughtIndex.ColonistDied), Is.EqualTo(0), "the control: a fall is not a death");

            int tick = colony.World.CurrentTick;
            Strike(colony, marauder, dies, Fatal(dies));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(dies.Id), Is.Null, "she did not die");

            foreach (Pawn survivor in new[] { stands, lies })
            {
                Assert.That(Copies(survivor, ThoughtIndex.ColonistDied), Is.EqualTo(1), $"pawn {survivor.Id.Value} felt nothing");
                Assert.That(ExpiryOf(survivor, ThoughtIndex.ColonistDied), Is.EqualTo(tick + 3 * Calendar.TicksPerDay));
            }
            Assert.That(OffsetOf(stands, ThoughtIndex.ColonistDied, tick), Is.EqualTo(-60));
            Assert.That(Copies(marauder, ThoughtIndex.ColonistDied), Is.EqualTo(0), "a marauder mourned");
            Assert.That(Copies(hog, ThoughtIndex.ColonistDied), Is.EqualTo(0), "an animal mourned");
            Assert.That(Copies(dies, ThoughtIndex.ColonistDied), Is.EqualTo(0), "the dead mourned herself");
        }

        [Test]
        public void AMaraudersDeathIsFeltByNobody()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 10, 10));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -10, -10));

            Strike(colony, a, marauder, Fatal(marauder));
            Strike(colony, a, hog, Fatal(hog));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(marauder.Id), Is.Null, "the control: the marauder died");
            Assert.That(colony.Pawns.Pawns.Get(hog.Id), Is.Null, "the control: the hog died");

            Assert.That(Copies(a, ThoughtIndex.ColonistDied), Is.EqualTo(0));
            Assert.That(Copies(b, ThoughtIndex.ColonistDied), Is.EqualTo(0));
        }

        /// <summary>
        /// Four deaths, three memories: the fourth is dropped while the three last, and the three
        /// diminish at the thought's 750 per mille (-60, -45, -33).
        /// </summary>
        [Test]
        public void DeathsStackToThree()
        {
            var colony = Board(colonists: 6);
            colony.World.Tick(5);
            Pawn[] all = colony.Pawns.Pawns.All.ToArray();
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 10, 10));
            int tick = colony.World.CurrentTick;

            for (int i = 0; i < 4; i++)
            {
                Strike(colony, marauder, all[i], Fatal(all[i]));
                colony.World.Tick();
                Assert.That(colony.Pawns.Pawns.Get(all[i].Id), Is.Null, $"death {i + 1} did not happen");
                if (i == 1)
                    Assert.That(Copies(all[5], ThoughtIndex.ColonistDied), Is.EqualTo(2), "the control: two deaths are two memories");
            }

            foreach (Pawn survivor in new[] { all[4], all[5] })
            {
                Assert.That(Copies(survivor, ThoughtIndex.ColonistDied), Is.EqualTo(3));
                Assert.That(OffsetOf(survivor, ThoughtIndex.ColonistDied, tick + 10), Is.EqualTo(-60 - 45 - 33));
            }
        }

        /// <summary>The order <c>CombatListeners</c> promises: the weapon drop first, then friendly fire, once.</summary>
        [Test]
        public void TheListenerIsRegisteredOnceAfterTheWeaponDrop()
        {
            var colony = Board();
            var listeners = colony.Pawns.CombatHooks.Listeners;
            Assert.That(listeners[0], Is.InstanceOf<WeaponDropListener>());
            Assert.That(listeners.Count(l => l is FriendlyFireListener), Is.EqualTo(1));
            Assert.That(listeners.ToList().FindIndex(l => l is FriendlyFireListener), Is.GreaterThan(0));
        }

        /// <summary>
        /// C5 end to end: a drafted colonist ordered on to another — the intent Ctrl + right-click
        /// sends (<c>CombatOrders.Route</c>) — closes and strikes, and the one she struck, undrafted
        /// and about her business, turns on her and swings back, carrying the memory of it.
        /// </summary>
        [Test]
        public void ACtrlAttackOnAnUndraftedColonistIsFoughtBackAndRemembered()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Stand(colony, a, Near(colony, 0, 0));
            Stand(colony, b, Near(colony, 4, 0));
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            Assert.That(Draft(colony, a), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, a, b), Is.EqualTo(IntentRejection.None), "the order refused a colonist");
            Assert.That(b.Drafted, Is.False);
            Assert.That(rules.TicksOf(b).Count, Is.EqualTo(0), "the control: she was not fighting");

            for (int t = 0; t < 3_000 && rules.TicksOf(b).Count == 0; t++)
            {
                // A broken colonist neither takes orders nor fights (design 33 §6A.6); on a bare
                // board with nothing to lift her mood either might break.
                a.BreakTicksLeft = 0;
                b.BreakTicksLeft = 0;
                colony.World.Tick();
            }

            Assert.That(rules.Swings.Any(s => s.Attacker == a.Id.Value && s.Target == b.Id.Value), Is.True, "she never struck");
            Assert.That(rules.TicksOf(b).Count, Is.GreaterThan(0), "the one attacked never struck back");
            Assert.That(rules.Swings.Where(s => s.Attacker == b.Id.Value).All(s => s.Target == a.Id.Value), Is.True);
            Assert.That(Copies(b, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "she does not remember it");
        }
    }
}
