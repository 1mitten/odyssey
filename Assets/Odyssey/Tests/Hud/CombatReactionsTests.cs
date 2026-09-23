#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The reactions to a blow (design 33 §9a): which reaction each event asks for, the side it
    /// came from, the computed shapes, the knock-back's slide, and — the owner's report, "there
    /// needs to be visual reactions to hits" — a duel, stepped frame by frame the way the figure
    /// director steps it, in which <b>every landed blow is seen</b>. The duel's negative control
    /// is lane B's rule, which drew nothing for most of them.
    /// </summary>
    public class CombatReactionsTests
    {
        // ---- Which reaction ---------------------------------------------------------------------

        [Test]
        public void EveryLandedHitFlinchesAndAHeavyOneStaggers()
        {
            Assert.That(CombatReactions.For(CombatEventKind.Hit, 1), Is.EqualTo(HitReaction.Flinch), "the lightest blow flinches");
            Assert.That(CombatReactions.For(CombatEventKind.Hit, 4_000), Is.EqualTo(HitReaction.Flinch), "a punch");
            Assert.That(CombatReactions.For(CombatEventKind.Hit, 11_999), Is.EqualTo(HitReaction.Flinch));
            Assert.That(CombatReactions.For(CombatEventKind.Hit, 12_000), Is.EqualTo(HitReaction.Stagger), "12 points staggers");
            Assert.That(CombatReactions.For(CombatEventKind.Hit, 30_000), Is.EqualTo(HitReaction.Stagger));
            Assert.That(CombatReactions.For(CombatEventKind.Critical, 0), Is.EqualTo(HitReaction.Stagger), "any critical staggers");
            Assert.That(CombatReactions.For(CombatEventKind.Stun, 60), Is.EqualTo(HitReaction.Stagger), "a stun opens with a stagger");
            Assert.That(CombatReactions.For(CombatEventKind.KnockedBack, 1234), Is.EqualTo(HitReaction.KnockDown));

            foreach (CombatEventKind kind in new[] { CombatEventKind.None, CombatEventKind.Swing, CombatEventKind.Miss,
                         CombatEventKind.Dodge, CombatEventKind.Downed, CombatEventKind.Died, CombatEventKind.Recovered })
                Assert.That(CombatReactions.For(kind, 20_000), Is.EqualTo(HitReaction.None), $"{kind} moves nobody struck");

            Assert.That(CombatReactions.CancelsSwing(CombatEventKind.Stun), Is.True, "a stunned swing does not land");
            Assert.That(CombatReactions.CancelsSwing(CombatEventKind.Critical), Is.False, "a critical's victim still lands hers");
            Assert.That(CombatReactions.CancelsSwing(CombatEventKind.Hit), Is.False);
        }

        [Test]
        public void KnockedDownAndDownedAreBothOnTheGround()
        {
            Assert.That(CombatReactions.Floored(PawnFlags.Person | PawnFlags.KnockedDown), Is.True);
            Assert.That(CombatReactions.Floored(PawnFlags.Person | PawnFlags.Downed), Is.True);
            Assert.That(CombatReactions.Floored(PawnFlags.Person | PawnFlags.Stunned | PawnFlags.Drafted), Is.False);
        }

        [Test]
        public void ABlowIsReadFromTheSideItCameFrom()
        {
            // Facing +Z (north); the attacker's direction from the body.
            Assert.That(CombatReactions.SideOf(0, 1, 0, 1), Is.EqualTo(HitSide.Front));
            Assert.That(CombatReactions.SideOf(0, 1, 0, -1), Is.EqualTo(HitSide.Back));
            Assert.That(CombatReactions.SideOf(0, 1, 1, 0), Is.EqualTo(HitSide.Right), "+X is the right of a body facing +Z");
            Assert.That(CombatReactions.SideOf(0, 1, -1, 0), Is.EqualTo(HitSide.Left));
            Assert.That(CombatReactions.SideOf(0, 1, 1, 1.1f), Is.EqualTo(HitSide.Front), "just inside the front quarter");
            Assert.That(CombatReactions.SideOf(0, 1, 1, 0.9f), Is.EqualTo(HitSide.Right), "just outside it");
            Assert.That(CombatReactions.SideOf(0, 1, 0, 0), Is.EqualTo(HitSide.Front), "no direction is the front");
            Assert.That(CombatReactions.SideOf(0, 0, 1, 0), Is.EqualTo(HitSide.Front), "no facing is the front");
            // Turned: facing +X, an attacker to the south is on the right.
            Assert.That(CombatReactions.SideOf(1, 0, 0, -1), Is.EqualTo(HitSide.Right));
        }

        // ---- The shapes ----------------------------------------------------------------------------

        [Test]
        public void EveryComputedReactionSnapsAwayFromTheBlowAndSettles()
        {
            foreach (HitReaction reaction in new[] { HitReaction.Flinch, HitReaction.Stagger })
            {
                float span = CombatReactions.ComputedSeconds(reaction);
                float peak = span * (reaction == HitReaction.Stagger ? CombatReactions.StaggerPeak : CombatReactions.FlinchPeak);

                ReactionPose front = CombatReactions.Pose(reaction, HitSide.Front, peak);
                Assert.That(front.Spine, Is.LessThan(0f), $"{reaction} from the front leans back");
                Assert.That(front.Head, Is.LessThan(0f), $"{reaction} from the front snaps the head back");
                Assert.That(front.Lunge, Is.LessThan(0f), $"{reaction} from the front is pushed back");

                ReactionPose back = CombatReactions.Pose(reaction, HitSide.Back, peak);
                Assert.That(back.Spine, Is.GreaterThan(0f), $"{reaction} from behind folds forward");
                Assert.That(back.Lunge, Is.GreaterThan(0f), $"{reaction} from behind is pushed on");

                ReactionPose left = CombatReactions.Pose(reaction, HitSide.Left, peak);
                ReactionPose right = CombatReactions.Pose(reaction, HitSide.Right, peak);
                Assert.That(left.Side, Is.GreaterThan(0f), $"{reaction} from the left is pushed right");
                Assert.That(right.Side, Is.LessThan(0f), $"{reaction} from the right is pushed left");
                Assert.That(left.Twist, Is.EqualTo(-right.Twist).Within(1e-5f), "the two sides mirror");

                foreach (HitSide side in new[] { HitSide.Front, HitSide.Back, HitSide.Left, HitSide.Right })
                {
                    Assert.That(CombatReactions.Pose(reaction, side, 0f).IsRest, Is.True, $"{reaction} {side} starts at rest");
                    Assert.That(CombatReactions.Pose(reaction, side, span).IsRest, Is.True, $"{reaction} {side} settles");
                    Assert.That(CombatReactions.Pose(reaction, side, span * 0.5f).IsRest, Is.False, $"{reaction} {side} is still moving half way");
                }
            }

            ReactionPose flinch = CombatReactions.Pose(HitReaction.Flinch, HitSide.Front, CombatReactions.FlinchSeconds * CombatReactions.FlinchPeak);
            ReactionPose stagger = CombatReactions.Pose(HitReaction.Stagger, HitSide.Front, CombatReactions.StaggerSeconds * CombatReactions.StaggerPeak);
            Assert.That(-stagger.Lunge, Is.GreaterThan(-flinch.Lunge * 3f), "a stagger rocks back much further than a flinch");
            Assert.That(-stagger.Lunge, Is.EqualTo(CombatReactions.StaggerShove).Within(0.01f), "half a step at its furthest");
            Assert.That(CombatReactions.Pose(HitReaction.KnockDown, HitSide.Front, 0.1f).IsRest, Is.True,
                "a knock-down is the held clips' and the slide's, not a computed jolt");
        }

        // ---- The track -----------------------------------------------------------------------------

        [Test]
        public void TheStrongestReactionWinsAndAWeakerOneWaits()
        {
            var track = new ReactionTrack();
            Assert.That(track.Live, Is.False);

            Assert.That(track.Take(HitReaction.Flinch, HitSide.Front, 0.4f, false), Is.True);
            track.Step(0.1f);
            Assert.That(track.Take(HitReaction.Stagger, HitSide.Left, 0.9f, false), Is.True, "the critical on the same tick upgrades it");
            Assert.That(track.Kind, Is.EqualTo(HitReaction.Stagger));
            Assert.That(track.Side, Is.EqualTo(HitSide.Left));
            Assert.That(track.Seconds, Is.Zero, "and starts it afresh");

            track.Step(0.2f);
            Assert.That(track.Take(HitReaction.Flinch, HitSide.Back, 0.4f, false), Is.False, "a flinch waits out a stagger");
            Assert.That(track.Kind, Is.EqualTo(HitReaction.Stagger));
            Assert.That(track.Take(HitReaction.Stagger, HitSide.Back, 0.9f, false), Is.True, "another stagger restarts it from its side");
            Assert.That(track.Side, Is.EqualTo(HitSide.Back));

            track.Step(0f);
            track.Step(0f);
            Assert.That(track.Seconds, Is.Zero, "a pause holds it");

            track.Step(0.95f);
            Assert.That(track.Live, Is.False, "run out");
            Assert.That(track.Take(HitReaction.Flinch, HitSide.Front, 0.4f, false), Is.True, "and anything starts once it has");
            Assert.That(track.Take(HitReaction.None, HitSide.Front, 0.4f, false), Is.False, "nothing is not a reaction");
        }

        // ---- Who has the layer ---------------------------------------------------------------------

        [Test]
        public void AReactionInterruptsAWindUpAndTheSwingComesBackToLand()
        {
            Assert.That(CombatReactions.Arbitrate(false, 0f, false, false), Is.EqualTo(ActionShown.Nothing));
            Assert.That(CombatReactions.Arbitrate(true, 0.5f, false, false), Is.EqualTo(ActionShown.Swing));
            Assert.That(CombatReactions.Arbitrate(false, 0f, true, false), Is.EqualTo(ActionShown.Reaction));

            Assert.That(CombatReactions.Arbitrate(true, 0.3f, true, false), Is.EqualTo(ActionShown.Reaction),
                "struck early in her wind-up, she reacts");
            Assert.That(CombatReactions.Arbitrate(true, 1f - CombatReactions.SwingLead, true, false), Is.EqualTo(ActionShown.Swing),
                "and her swing comes back in time to be seen landing");
            Assert.That(CombatReactions.Arbitrate(true, 1f, true, false), Is.EqualTo(ActionShown.Swing), "on the impact tick");
            Assert.That(CombatReactions.Arbitrate(true, 1f + CombatReactions.SwingFollow, true, false), Is.EqualTo(ActionShown.Swing));
            Assert.That(CombatReactions.Arbitrate(true, 1.5f, true, false), Is.EqualTo(ActionShown.Reaction),
                "a blow taken in the follow-through cuts it short");
            Assert.That(CombatReactions.Arbitrate(true, 1f, true, true), Is.EqualTo(ActionShown.Reaction),
                "a stun ends the swing outright");
        }

        /// <summary>
        /// A reaction hands the layer to the body's own swing once, and does not take it back when
        /// the swing's window closes: the follow-through plays on and the flinch is laid over it.
        /// One hand-over per blow, not a flicker between two clips.
        /// </summary>
        [Test]
        public void AReactionHandsTheLayerToTheSwingOnceAndIsLaidOverIt()
        {
            var track = new ReactionTrack();
            track.Take(HitReaction.Flinch, HitSide.Front, 0.87f, false);
            track.Step(0.05f);

            Assert.That(track.Show(true, 0.4f), Is.EqualTo(ActionShown.Reaction), "early in her wind-up, the reaction");
            Assert.That(track.Overlay(ActionShown.Reaction).IsRest, Is.True, "and nothing laid over it");

            Assert.That(track.Show(true, 0.7f), Is.EqualTo(ActionShown.Swing), "her swing comes back to land");
            Assert.That(track.Yielded, Is.True);
            Assert.That(track.Overlay(ActionShown.Swing).IsRest, Is.False, "with the flinch laid over it");

            Assert.That(track.Show(true, 1.6f), Is.EqualTo(ActionShown.Swing), "and keeps it through the follow-through");
            Assert.That(track.Show(false, 0f), Is.EqualTo(ActionShown.Nothing), "and the reaction does not come back after");

            track.Take(HitReaction.Flinch, HitSide.Left, 0.87f, false);
            Assert.That(track.Yielded, Is.False, "a new blow is a new reaction");
            Assert.That(track.Show(true, 1.6f), Is.EqualTo(ActionShown.Reaction), "a blow in the follow-through cuts it short");

            track.Step(1f);
            Assert.That(track.Overlay(ActionShown.Swing).IsRest, Is.True, "nothing over a swing once it has run out");
        }

        // ---- The knock-back ------------------------------------------------------------------------

        [Test]
        public void AKnockBackSlidesFromWhereTheBlowFoundItAndDropsOverTheLip()
        {
            // Knocked 2.5 m west and a terrace step (3 m) down: the from-position is east and above.
            const float fromX = 2.5f, fromY = 3f, fromZ = 0f;

            KnockbackSlide.Offset(fromX, fromY, fromZ, 0f, out float x0, out float y0, out float z0);
            Assert.That(x0, Is.EqualTo(fromX).Within(1e-5f), "drawn where the blow found it");
            Assert.That(y0, Is.EqualTo(fromY).Within(1e-5f));
            Assert.That(z0, Is.Zero);

            KnockbackSlide.Offset(fromX, fromY, fromZ, KnockbackSlide.Seconds, out float x1, out float y1, out float z1);
            Assert.That(x1, Is.Zero.Within(1e-5f), "on the landing tile when it is over");
            Assert.That(y1, Is.Zero.Within(1e-5f));
            Assert.That(KnockbackSlide.Finished(KnockbackSlide.Seconds), Is.True);
            Assert.That(KnockbackSlide.Finished(KnockbackSlide.Seconds * 0.9f), Is.False);

            float previous = fromX;
            for (int i = 1; i <= 10; i++)
            {
                float s = KnockbackSlide.Seconds * i / 10f;
                KnockbackSlide.Offset(fromX, fromY, fromZ, s, out float x, out float y, out _);
                Assert.That(x, Is.LessThan(previous), "always moving away from the blow");
                previous = x;
                if (i < 10)
                    Assert.That(y / fromY, Is.GreaterThan(x / fromX),
                        $"at {s:F3} s it is further over than it is down: over the lip, then the drop");
            }

            // On the level the height follows the ground, not a fall.
            KnockbackSlide.Offset(2.5f, 0.2f, 0f, KnockbackSlide.Seconds * 0.5f, out float lx, out float ly, out _);
            KnockbackSlide.Offset(2.5f, -0.2f, 0f, KnockbackSlide.Seconds * 0.5f, out _, out float uy, out _);
            Assert.That(ly / 0.2f, Is.LessThan(1f).And.GreaterThan(0f));
            Assert.That(uy / -0.2f, Is.EqualTo(lx / 2.5f).Within(1e-5f), "a slide on to higher ground rises with the slide");
        }

        // ---- The duel: the owner's report ----------------------------------------------------------

        /// <summary>
        /// Two fighters swinging at each other, stepped a frame at a time at 60 frames a second, the
        /// way <c>PawnFigureDirector</c> steps them: the reaction's clock in the frame's seconds, the
        /// swing's in simulation ticks. Every weapon pairing in the content's numbers, every phase
        /// offset between the two cooldowns, and speed one and three. Each landed blow must be seen
        /// for at least <see cref="SeenSeconds"/> of the first <see cref="CombatReactions.FlinchSeconds"/>
        /// — as the reaction on the clip layer, or as the flinch laid over a swing that holds it.
        /// </summary>
        [Test]
        public void InADuelEveryLandedBlowIsSeen([Values(1, 3)] int speed, [Values(true, false)] bool pack)
        {
            Duel.Result result = Duel.Run(speed, pack, Rule.Arbitrated);
            Assert.That(result.Hits, Is.GreaterThan(500), "the control: blows were landed");
            Assert.That(result.Unseen, Is.Zero,
                $"{result.Unseen} of {result.Hits} blows at speed {speed} drew under {SeenSeconds} s of reaction; worst {result.Worst:F3} s");
        }

        /// <summary>
        /// The cause of "no visible reaction", measured (design 33 §9a): lane B's rule — no react
        /// while the struck body's own swing is showing, and its next swing cutting a react off —
        /// in the same duels. Most blows draw less than <see cref="SeenSeconds"/>. This is the
        /// negative control for the test above, kept so the number stays on record.
        /// </summary>
        [Test]
        public void LaneBsRuleLeftMostBlowsUnseen([Values(1, 3)] int speed)
        {
            Duel.Result result = Duel.Run(speed, pack: true, Rule.LaneB);
            Assert.That(result.Hits, Is.GreaterThan(500));
            Assert.That(result.Unseen, Is.GreaterThan(result.Hits / 2),
                $"lane B's rule left {result.Unseen} of {result.Hits} blows under {SeenSeconds} s at speed {speed}");
            TestContext.Out.WriteLine($"lane B, speed {speed}: {result.Unseen} of {result.Hits} blows under {SeenSeconds} s");
        }

        /// <summary>How much of a flinch's first 0.4 s must be drawn for a blow to count as seen.</summary>
        const float SeenSeconds = 0.3f;

        enum Rule { Arbitrated, LaneB }

        /// <summary>A duel on the content's numbers: see <see cref="InADuelEveryLandedBlowIsSeen"/>.</summary>
        static class Duel
        {
            public struct Result
            {
                public int Hits;
                public int Unseen;
                public float Worst;
            }

            /// <summary>A weapon's wind-up and cooldown in ticks (Items.xml, Combat.xml) and its drawn swing's length as multiples of the wind-up.</summary>
            readonly struct Arm
            {
                public readonly int Windup, Cooldown, Damage;
                public readonly float[] Spans;

                public Arm(int windup, int cooldown, int damage, float[] spans)
                {
                    Windup = windup;
                    Cooldown = cooldown;
                    Damage = damage;
                    Spans = spans;
                }
            }

            // The pack's clips: whole length over measured impact (synty-sword-combat.md, design 33 §6B).
            static readonly float[] Light = { 0.83f / 0.333f, 0.67f / 0.167f, 0.73f / 0.367f };
            static readonly float[] Heavy = { 2.07f / 0.967f, 1.40f / 0.867f };
            // A computed swing: over at 1 + RecoverFraction.
            static readonly float[] Computed = { 1.8f };

            static Arm Fists(bool pack) => new Arm(18, 120, 4_000, Computed);
            static Arm Bat(bool pack) => new Arm(30, 120, 7_000, pack ? Heavy : Computed);
            static Arm Crowbar(bool pack) => new Arm(36, 132, 8_000, pack ? Heavy : Computed);
            static Arm Machete(bool pack) => new Arm(22, 96, 8_000, pack ? Light : Computed);

            const int Ticks = 1_200;

            /// <summary>The pack's hit react, 1–26 at 30 fps.</summary>
            const float ReactClipSeconds = 0.87f;

            sealed class Fighter
            {
                public Arm Arm;
                public int NextSwing;
                public int Swings;
                public float SwingStart = -1e9f;
                public float SwingEnd = -1e9f;
                public bool SwingLanded = true;
                public ReactionTrack Track;
                public float LaneBReactUntil = -1f;
                public readonly List<(float at, float seen)> Blows = new List<(float, float)>();
                public int OpenBlow = -1;
            }

            public static Result Run(int speed, bool pack, Rule rule)
            {
                var pairs = new[]
                {
                    (Fists(pack), Machete(pack)),
                    (Bat(pack), Machete(pack)),
                    (Crowbar(pack), Machete(pack)),
                    (Machete(pack), Machete(pack)),
                    (Bat(pack), Bat(pack)),
                };

                var result = new Result { Worst = float.MaxValue };
                foreach ((Arm a, Arm b) in pairs)
                for (int offset = 0; offset < b.Cooldown; offset += 5)
                {
                    var one = new Fighter { Arm = a, NextSwing = 0 };
                    var two = new Fighter { Arm = b, NextSwing = offset };
                    Fight(one, two, speed, pack, rule);
                    foreach (Fighter f in new[] { one, two })
                    foreach ((float at, float seen) in f.Blows)
                    {
                        // A blow in the last 0.4 s of the run has not had its time to be seen.
                        if (at + CombatReactions.FlinchSeconds > (Ticks / speed - 1) / 60f) continue;
                        result.Hits++;
                        if (seen < SeenSeconds) result.Unseen++;
                        result.Worst = Math.Min(result.Worst, seen);
                    }
                }
                return result;
            }

            /// <summary>
            /// 1,200 ticks of it. Each frame, in the director's order: the figures are posed (the
            /// reaction's clock stepped, the layer arbitrated, what shows recorded against every blow
            /// still inside its first 0.4 s), then the frame's events are handed on.
            /// </summary>
            static void Fight(Fighter one, Fighter two, int speed, bool pack, Rule rule)
            {
                const float dt = 1f / 60f;
                float seconds = 0f;
                for (int tick = 0; tick < Ticks; tick += speed, seconds += dt)
                {
                    Pose(one, tick, seconds, dt, rule);
                    Pose(two, tick, seconds, dt, rule);

                    for (int t = tick - speed + 1; t <= tick; t++)
                    {
                        Swing(one, t);
                        Swing(two, t);
                        Land(one, two, t, seconds, pack, rule);
                        Land(two, one, t, seconds, pack, rule);
                    }
                }
            }

            static void Swing(Fighter f, int tick)
            {
                if (tick != f.NextSwing) return;
                f.SwingStart = tick;
                f.SwingEnd = tick + f.Arm.Windup * f.Arm.Spans[f.Swings % f.Arm.Spans.Length];
                f.SwingLanded = false;
                f.Swings++;
                f.NextSwing = tick + f.Arm.Cooldown;
                // Lane B: a strike begins a swing whatever is showing, cutting any react off.
                f.LaneBReactUntil = -1f;
            }

            static void Land(Fighter attacker, Fighter target, int tick, float seconds, bool pack, Rule rule)
            {
                if (attacker.SwingLanded || tick != (int)attacker.SwingStart + attacker.Arm.Windup) return;
                attacker.SwingLanded = true;
                target.Blows.Add((seconds, 0f));

                if (rule == Rule.LaneB)
                {
                    // React refused while the target's own swing is still being drawn.
                    if (tick >= target.SwingEnd) target.LaneBReactUntil = seconds + ReactClipSeconds;
                    return;
                }

                HitReaction reaction = CombatReactions.For(CombatEventKind.Hit, attacker.Arm.Damage);
                float span = pack ? ReactClipSeconds : CombatReactions.ComputedSeconds(reaction);
                target.Track.Take(reaction, HitSide.Front, span, cancelsSwing: false);
            }

            static void Pose(Fighter f, int tick, float seconds, float dt, Rule rule)
            {
                bool seen;
                if (rule == Rule.LaneB)
                {
                    seen = seconds < f.LaneBReactUntil;
                }
                else
                {
                    f.Track.Step(dt);
                    bool swinging = tick < f.SwingEnd;
                    float phase = (tick - f.SwingStart) / f.Arm.Windup;
                    ActionShown shown = f.Track.Show(swinging, phase);
                    seen = shown == ActionShown.Reaction || !f.Track.Overlay(shown).IsRest;
                }

                for (int i = 0; i < f.Blows.Count; i++)
                {
                    (float at, float had) = f.Blows[i];
                    if (seconds <= at || seconds > at + CombatReactions.FlinchSeconds) continue;
                    if (seen) f.Blows[i] = (at, had + dt);
                }
            }
        }
    }
}
