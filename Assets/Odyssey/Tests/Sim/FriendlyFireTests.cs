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
            Assert.That(attacked.renewsOnRepeat, Is.True, "a second blow renews the day (§14e)");

            ThoughtDef died = content.Thoughts[ThoughtIndex.ColonistDied];
            Assert.That(died.defName, Is.EqualTo("Thought_ColonistDied"));
            Assert.That(died.moodOffset, Is.EqualTo(-60));
            Assert.That(died.durationTicks, Is.EqualTo(3 * Calendar.TicksPerDay));
            Assert.That(died.stackLimit, Is.EqualTo(3));
            Assert.That(died.renewsOnRepeat, Is.False);

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
        /// The controls on the attack: a bandit's blow — landed or missed — is not friendly fire,
        /// and a colonist hitting a bandit gives nobody anything. Colonist on colonist only (§12b).
        /// </summary>
        [Test]
        public void ABanditsBlowIsNotFriendlyFire()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn victim = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 10, 10));
            int tick = colony.World.CurrentTick;

            Strike(colony, bandit, victim, 1_000);
            colony.Pawns.Combat!.ApplySwing(bandit, victim, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Miss), tick);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "a bandit's swing was remembered");

            Strike(colony, by, bandit, 1_000);
            Assert.That(Copies(bandit, ThoughtIndex.AttackedByColonist), Is.EqualTo(0));
            Assert.That(Copies(by, ThoughtIndex.AttackedByColonist), Is.EqualTo(0));

            Strike(colony, by, victim, 1_000);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "the control: a colonist's blow that lands");
        }

        /// <summary>
        /// A colonist's swing at a colonist is an attack whatever came of it (design 33 §14f; the
        /// owner: a missed swing counts) — a miss, a dodge and a hit alike give the memory, once. A
        /// swing arriving at a colonist already past the death line gives nothing: the dead feel
        /// nothing (§12b).
        /// </summary>
        [Test]
        public void AColonistsSwingThatMissesIsRememberedToo()
        {
            var colony = Board(colonists: 4);
            colony.World.Tick(5);
            Pawn by = colony.Pawns.Pawns.All[0], missed = colony.Pawns.Pawns.All[1], dodged = colony.Pawns.Pawns.All[2];
            Pawn dead = colony.Pawns.Pawns.All[3];
            int tick = colony.World.CurrentTick;

            colony.Pawns.Combat!.ApplySwing(by, missed, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Miss), tick);
            colony.Pawns.Combat!.ApplySwing(by, dodged, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Dodge), tick);
            Assert.That(missed.HpMilli, Is.EqualTo(missed.HpMaxMilli), "the control: the miss took nothing");
            Assert.That(Copies(missed, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "a missed swing was not remembered");
            Assert.That(Copies(dodged, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "a dodged swing was not remembered");
            Assert.That(ExpiryOf(missed, ThoughtIndex.AttackedByColonist), Is.EqualTo(tick + Calendar.TicksPerDay));
            Assert.That(Copies(by, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "the one who swung was not swung at");

            colony.Pawns.Combat!.ApplySwing(by, missed, Fists(colony.Pawns), Blow(1_000), tick);
            Assert.That(Copies(missed, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "a miss and then a hit are one memory");

            Strike(colony, by, dead, Fatal(dead));
            dead.Memories.Clear();
            colony.Pawns.Combat!.ApplySwing(by, dead, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Miss), tick);
            Assert.That(Copies(dead, ThoughtIndex.AttackedByColonist), Is.EqualTo(0), "the dead remembered a swing");
        }

        /// <summary>
        /// The hook the memory is heard on (design 33 §14f): <c>SwingResolved</c>, once for every swing
        /// that reaches a pawn — a hit, a miss or a dodge — before <c>DamageApplied</c> for a hit; and
        /// never for a blow at a building, which raises no hooks (§13g).
        /// </summary>
        [Test]
        public void EverySwingAtAPawnIsHeardOnceAndNoneAtABuilding()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            var hooks = new HookCounter();
            colony.Pawns.CombatHooks.Add(hooks);
            int tick = colony.World.CurrentTick;

            colony.Pawns.Combat!.ApplySwing(a, b, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Miss), tick);
            colony.Pawns.Combat!.ApplySwing(a, b, Fists(colony.Pawns), new SwingOutcome(CombatEventKind.Dodge), tick);
            colony.Pawns.Combat!.ApplySwing(a, b, Fists(colony.Pawns), Blow(1_000), tick);
            Assert.That(hooks.Heard, Is.EqualTo(new[]
                { "swing:" + b.Id.Value, "swing:" + b.Id.Value, "swing:" + b.Id.Value, "damage:" + b.Id.Value }));

            int cell = Near(colony, 6, 0);
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall,
                StuffHandle.Wood, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            Assert.That(BuildingTargets.TryFind(colony.Pawns, cell, out BuildingTarget wall), Is.True);
            colony.Pawns.Combat!.StrikeBuilding(a, wall, cell, Fists(colony.Pawns), Blow(1_000), tick);
            Assert.That(hooks.SwingCount, Is.EqualTo(3), "a blow at a wall was heard as a swing at a pawn");
        }

        /// <summary>
        /// A second blow in the day renews the memory (design 33 §14e; the owner left it to us and we
        /// recommended it): still one copy, still -80, but the day runs from the latest blow. The
        /// thought's <c>renewsOnRepeat</c> does it, not a change to how every thought is added.
        /// </summary>
        [Test]
        public void ASecondBlowRenewsTheDay()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn victim = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            int first = colony.World.CurrentTick;
            Strike(colony, by, victim, 1_000);
            Assert.That(ExpiryOf(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(first + Calendar.TicksPerDay),
                "the control: the first blow's day");
            colony.World.Tick(100);
            int second = colony.World.CurrentTick;
            Assert.That(second, Is.GreaterThan(first));

            Strike(colony, by, victim, 1_000);
            Assert.That(Copies(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "a second blow stacked");
            Assert.That(ExpiryOf(victim, ThoughtIndex.AttackedByColonist), Is.EqualTo(second + Calendar.TicksPerDay),
                "the second blow did not renew the day");
            Assert.That(OffsetOf(victim, ThoughtIndex.AttackedByColonist, second), Is.EqualTo(-80));
        }

        /// <summary>
        /// Renewal is the friendly-fire memory's alone (design 33 §14e): every other thought is added
        /// exactly as before — a meal at its limit of two is dropped, and the two it had keep their
        /// days. That is what keeps the goldens, which eat, where they were.
        /// </summary>
        [Test]
        public void NoOtherThoughtRenews()
        {
            PawnContent content = ContentPack.Pawns();
            for (int i = 0; i < content.Thoughts.Length; i++)
                // And being held (design 60 §6): renewed while she is in the cell, so it lasts as
                // long as the cell does. No golden holds a prisoner, so none eats it.
                Assert.That(content.Thoughts[i].renewsOnRepeat,
                    Is.EqualTo(i == ThoughtIndex.AttackedByColonist || i == ThoughtIndex.Imprisoned),
                    content.Thoughts[i].defName);

            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.Memories.Clear();
            pawn.AddMemory(ThoughtIndex.AteMeal, 100);
            pawn.AddMemory(ThoughtIndex.AteMeal, 200);
            pawn.AddMemory(ThoughtIndex.AteMeal, 300);
            int meal = content.Thoughts[ThoughtIndex.AteMeal].durationTicks;
            Assert.That(pawn.Memories.Select(m => m.ExpiryTick).ToArray(), Is.EqualTo(new[] { 100 + meal, 200 + meal }),
                "a meal past its limit renewed or stacked");
        }

        /// <summary>
        /// A colonist's death is felt by every other colonist on the board, standing or downed, for
        /// three days — and by no bandit, no animal, and not by the dead.
        /// </summary>
        [Test]
        public void EveryOtherColonistFeelsAColonistsDeath()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn dies = colony.Pawns.Pawns.All[0], stands = colony.Pawns.Pawns.All[1], lies = colony.Pawns.Pawns.All[2];
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 10, 10));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -10, -10));

            Strike(colony, bandit, lies, lies.HpMilli);
            Assert.That(lies.Downed, Is.True);
            Assert.That(Copies(stands, ThoughtIndex.ColonistDied), Is.EqualTo(0), "the control: a fall is not a death");

            int tick = colony.World.CurrentTick;
            Strike(colony, bandit, dies, Fatal(dies));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(dies.Id), Is.Null, "she did not die");

            foreach (Pawn survivor in new[] { stands, lies })
            {
                Assert.That(Copies(survivor, ThoughtIndex.ColonistDied), Is.EqualTo(1), $"pawn {survivor.Id.Value} felt nothing");
                Assert.That(ExpiryOf(survivor, ThoughtIndex.ColonistDied), Is.EqualTo(tick + 3 * Calendar.TicksPerDay));
            }
            Assert.That(OffsetOf(stands, ThoughtIndex.ColonistDied, tick), Is.EqualTo(-60));
            Assert.That(Copies(bandit, ThoughtIndex.ColonistDied), Is.EqualTo(0), "a bandit mourned");
            Assert.That(Copies(hog, ThoughtIndex.ColonistDied), Is.EqualTo(0), "an animal mourned");
            Assert.That(Copies(dies, ThoughtIndex.ColonistDied), Is.EqualTo(0), "the dead mourned herself");
        }

        [Test]
        public void ABanditsDeathIsFeltByNobody()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn a = colony.Pawns.Pawns.All[0], b = colony.Pawns.Pawns.All[1];
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 10, 10));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, -10, -10));

            Strike(colony, a, bandit, Fatal(bandit));
            Strike(colony, a, hog, Fatal(hog));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(bandit.Id), Is.Null, "the control: the bandit died");
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
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 10, 10));
            int tick = colony.World.CurrentTick;

            for (int i = 0; i < 4; i++)
            {
                Strike(colony, bandit, all[i], Fatal(all[i]));
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
            // The one who started it remembers the blows she takes back (design 33 §14a, C5 (b): the
            // owner, yes) — any swing, landed or not (§14f). Her answer was decided when its wind-up
            // began (§9g); it reaches her when the wind-up ends.
            for (int t = 0; t < 200 && Copies(a, ThoughtIndex.AttackedByColonist) == 0; t++)
            {
                a.BreakTicksLeft = 0;
                b.BreakTicksLeft = 0;
                colony.World.Tick();
            }
            Assert.That(Copies(a, ThoughtIndex.AttackedByColonist), Is.EqualTo(1), "the one who started it remembers nothing");
        }
    }
}
