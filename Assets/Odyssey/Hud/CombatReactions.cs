#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>What a struck body does, strongest last (design 33 §9a).</summary>
    public enum HitReaction : byte
    {
        None = 0,

        /// <summary>Every landed blow: the head and chest snap away from it and come back.</summary>
        Flinch = 1,

        /// <summary>A heavy blow or a critical: rocked back half a step and recovered, on the same tile.</summary>
        Stagger = 2,

        /// <summary>Knocked back a tile and off its feet (<see cref="CombatEventKind.KnockedBack"/>).</summary>
        KnockDown = 3,
    }

    /// <summary>
    /// Which side of a body a blow came from, in the body's own frame. The same order as
    /// presentation's <c>BlowSide</c>, which casts to it.
    /// </summary>
    public enum HitSide : byte
    {
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>What the fight's clip layer shows this frame, when a swing and a reaction are both live.</summary>
    public enum ActionShown : byte
    {
        Nothing = 0,
        Swing = 1,
        Reaction = 2,
    }

    /// <summary>
    /// A computed reaction at one instant, in the figure's own frame (the signs of presentation's
    /// <c>CombatShape</c>): <see cref="Spine"/> folds forward when positive, <see cref="Twist"/>
    /// turns the chest to the figure's right, <see cref="Head"/> nods forward, and
    /// <see cref="Lunge"/> and <see cref="Side"/> move the whole body forward and to its right,
    /// in metres. Degrees for the rest.
    /// </summary>
    public readonly struct ReactionPose
    {
        public readonly float Spine;
        public readonly float Twist;
        public readonly float Head;
        public readonly float Lunge;
        public readonly float Side;

        public ReactionPose(float spine, float twist, float head, float lunge, float side)
        {
            Spine = spine;
            Twist = twist;
            Head = head;
            Lunge = lunge;
            Side = side;
        }

        public bool IsRest =>
            Math.Abs(Spine) < 1e-3f && Math.Abs(Twist) < 1e-3f && Math.Abs(Head) < 1e-3f
            && Math.Abs(Lunge) < 1e-4f && Math.Abs(Side) < 1e-4f;
    }

    /// <summary>
    /// The reactions to a blow, as the figures draw them (design 33 §9a): which reaction an event
    /// asks for, which side it came from, how long it runs and what shape it takes, and — the
    /// thing the owner's playtest turned on — <b>who wins the clip layer when the struck body is
    /// in the middle of its own swing</b>. Unity-free, so the fast tier holds every rule;
    /// <c>PawnFigureDirector.Combat.cs</c> only asks.
    ///
    /// <para><b>Why the arbitration exists.</b> Lane B's figure refused a hit react while its own
    /// swing was showing and let the next swing cut a react off, so in a fight — where both
    /// bodies are swinging on equal cooldowns — 30–70 % of landed blows drew nothing and most of
    /// the rest were cut within a third of a second (§9a). A struck body now reacts <em>every</em>
    /// time: the reaction takes the layer except in a window round the body's own impact, which
    /// the simulation will land whatever is drawn, and inside that window a computed flinch is
    /// laid over the swing instead.</para>
    ///
    /// <para>Every number here is INVENTED and wants a contact sheet.</para>
    /// </summary>
    public static class CombatReactions
    {
        /// <summary>A blow of this much, in thousandths of a hit point, or more staggers (owner, 2026-09-23).</summary>
        public const int StaggerDamageMilli = 12_000;

        /// <summary>How long a computed flinch runs, in seconds of game time.</summary>
        public const float FlinchSeconds = 0.4f;

        /// <summary>How long a computed stagger runs: rocked back and recovered.</summary>
        public const float StaggerSeconds = 0.9f;

        /// <summary>
        /// How far before its own impact, as a fraction of its wind-up, a swing takes the clip layer
        /// back from a reaction: enough of the wind-up to read as a blow coming.
        /// </summary>
        public const float SwingLead = 0.35f;

        /// <summary>How far past its impact, as a fraction of its wind-up, the swing keeps the layer so the blow is seen to connect.</summary>
        public const float SwingFollow = 0.25f;

        /// <summary>Where in a flinch it is furthest from rest, as a fraction of its span: a snap, not a sway.</summary>
        public const float FlinchPeak = 0.15f;

        /// <summary>Where in a stagger it is furthest back.</summary>
        public const float StaggerPeak = 0.3f;

        /// <summary>A flinch's fold of the chest and nod of the head, in degrees, and its shove in metres.</summary>
        public const float FlinchLean = 14f, FlinchHead = 20f, FlinchShove = 0.06f;

        /// <summary>A stagger's lean and nod, in degrees, and how far it rocks back: half a step. Drawn only; the pawn stays on its tile.</summary>
        public const float StaggerLean = 26f, StaggerHead = 14f, StaggerShove = 0.35f;

        /// <summary>
        /// The reaction an event asks of its target. A landed hit always flinches, and staggers at
        /// <see cref="StaggerDamageMilli"/> or more; a critical and a stun stagger; a knock-back
        /// knocks down. Nothing else moves the struck body: a miss and a dodge are the attacker's
        /// and the dodger's, and going down or dying is drawn off the pawn's flags and the corpse.
        /// </summary>
        public static HitReaction For(CombatEventKind kind, int amount) => kind switch
        {
            CombatEventKind.Hit => amount >= StaggerDamageMilli ? HitReaction.Stagger : HitReaction.Flinch,
            CombatEventKind.Critical => HitReaction.Stagger,
            CombatEventKind.Stun => HitReaction.Stagger,
            CombatEventKind.KnockedBack => HitReaction.KnockDown,
            _ => HitReaction.None,
        };

        /// <summary>
        /// Whether a reaction ends the struck body's own swing. Only a stun's: a stunned pawn's
        /// wound-up blow does not land (design 33 §5j), so the swing is let go. A critical's
        /// stagger does not — the simulation still lands the victim's swing on its tick, and the
        /// swing is drawn landing.
        /// </summary>
        public static bool CancelsSwing(CombatEventKind kind) => kind == CombatEventKind.Stun;

        /// <summary>Knocked down or downed: the body is on the ground and the held clips draw it.</summary>
        public static bool Floored(PawnFlags flags) => (flags & (PawnFlags.Downed | PawnFlags.KnockedDown)) != 0;

        /// <summary>How long a reaction runs when it is computed (no clip, or an animal).</summary>
        public static float ComputedSeconds(HitReaction reaction) => reaction switch
        {
            HitReaction.Flinch => FlinchSeconds,
            HitReaction.Stagger => StaggerSeconds,
            _ => 0f,
        };

        /// <summary>
        /// Which side of a body facing (<paramref name="forwardX"/>, <paramref name="forwardZ"/>) a
        /// blow from the direction (<paramref name="toX"/>, <paramref name="toZ"/>) — body to
        /// attacker — arrives on. Quarters at 45°. No direction at all is the front, which is where
        /// a body that turned to fight is looking.
        /// </summary>
        public static HitSide SideOf(float forwardX, float forwardZ, float toX, float toZ)
        {
            float f = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            float t = (float)Math.Sqrt(toX * toX + toZ * toZ);
            if (f < 1e-4f || t < 1e-4f) return HitSide.Front;

            float ahead = (forwardX * toX + forwardZ * toZ) / (f * t);
            if (ahead >= 0.7071f) return HitSide.Front;
            if (ahead <= -0.7071f) return HitSide.Back;
            // The up component of forward × toAttacker in Unity's left-handed frame: positive when
            // the attacker is on the body's right — (0,0,1) × (1,0,0) = (0,1,0).
            float up = forwardZ * toX - forwardX * toZ;
            return up > 0f ? HitSide.Right : HitSide.Left;
        }

        /// <summary>
        /// Who has the clip layer: a swing, a reaction, or neither. A reaction that ends the swing
        /// always has it. With both live, the swing has it from <see cref="SwingLead"/> of its
        /// wind-up before the impact to <see cref="SwingFollow"/> after, and the reaction the rest
        /// of the time — so a blow taken during a wind-up interrupts it, the swing comes back in
        /// time to be seen landing, and a blow taken in a follow-through cuts it short.
        /// <paramref name="swingPhase"/> is 0 at the start of the wind-up and 1 on the impact tick.
        /// </summary>
        public static ActionShown Arbitrate(bool swinging, float swingPhase, bool reacting, bool reactionCancelsSwing)
        {
            if (reacting && (reactionCancelsSwing || !swinging)) return ActionShown.Reaction;
            if (!swinging) return ActionShown.Nothing;
            if (!reacting) return ActionShown.Swing;
            return swingPhase >= 1f - SwingLead && swingPhase <= 1f + SwingFollow
                ? ActionShown.Swing
                : ActionShown.Reaction;
        }

        /// <summary>
        /// The computed reaction <paramref name="seconds"/> in: thrown away from the side the blow
        /// came from, fast out and slower back, and rest once it is over. For an animal, for a
        /// checkout without the pack, and laid over a swing that has the clip layer (see
        /// <see cref="Arbitrate"/>).
        /// </summary>
        public static ReactionPose Pose(HitReaction reaction, HitSide side, float seconds)
        {
            float span = ComputedSeconds(reaction);
            if (span <= 0f || seconds <= 0f || seconds >= span) return default;

            bool stagger = reaction == HitReaction.Stagger;
            float amount = Amount(seconds / span, stagger ? StaggerPeak : FlinchPeak);
            float lean = (stagger ? StaggerLean : FlinchLean) * amount;
            float head = (stagger ? StaggerHead : FlinchHead) * amount;
            float shove = (stagger ? StaggerShove : FlinchShove) * amount;

            return side switch
            {
                // Struck from behind: folded forward and pushed on, the head thrown forward.
                HitSide.Back => new ReactionPose(lean, 0f, head, shove, 0f),
                // From the left: the chest turned away to the right and pushed right; and the mirror.
                HitSide.Left => new ReactionPose(-lean * 0.4f, lean * 0.6f, -head * 0.5f, 0f, shove),
                HitSide.Right => new ReactionPose(-lean * 0.4f, -lean * 0.6f, -head * 0.5f, 0f, -shove),
                // From the front: rocked back, the head snapped back.
                _ => new ReactionPose(-lean, 0f, -head, -shove, 0f),
            };
        }

        /// <summary>0 to 1 and back over a span: up to <paramref name="peak"/> quickly, then down, smoothed.</summary>
        static float Amount(float t, float peak)
        {
            float a = t < peak ? t / peak : 1f - (t - peak) / (1f - peak);
            a = Math.Max(0f, Math.Min(1f, a));
            return a * a * (3f - 2f * a);
        }
    }

    /// <summary>
    /// One figure's reaction to the last blow it took (design 33 §9a): what it is, from which side,
    /// how far in and how long it runs. Presentation state, never saved; one per live figure.
    ///
    /// <para><b>Strongest wins.</b> A reaction as strong or stronger than the one running replaces
    /// it — the stagger a critical asks for on the same tick as its hit's flinch, a second blow
    /// from another side — and a weaker one is let go while the stronger runs.</para>
    /// </summary>
    public struct ReactionTrack
    {
        public HitReaction Kind;
        public HitSide Side;

        /// <summary>Seconds of game time since it began: stopped by a pause, like every ease in the director.</summary>
        public float Seconds;

        /// <summary>How long it runs: the clip's length where the pack draws it, else <see cref="CombatReactions.ComputedSeconds"/>.</summary>
        public float Span;

        /// <summary>Whether it ended the body's own swing (<see cref="CombatReactions.CancelsSwing"/>).</summary>
        public bool CancelsSwing;

        /// <summary>
        /// Whether the body's own swing has taken the clip layer back from it. Once it has, the
        /// reaction does not return to the layer when the swing's window closes — the swing's
        /// follow-through plays on — and is seen only as the computed flinch laid over it. One
        /// hand-over per blow, not a flicker between two clips.
        /// </summary>
        public bool Yielded;

        public bool Live => Kind != HitReaction.None && Seconds < Span;

        /// <summary>Whether <paramref name="incoming"/> would replace what is running.</summary>
        public bool Accepts(HitReaction incoming) =>
            incoming != HitReaction.None && (!Live || incoming >= Kind);

        /// <summary>Start a reaction, if it is at least as strong as the one running. True if it started.</summary>
        public bool Take(HitReaction incoming, HitSide side, float span, bool cancelsSwing)
        {
            if (!Accepts(incoming)) return false;
            Kind = incoming;
            Side = side;
            Seconds = 0f;
            Span = span;
            CancelsSwing = cancelsSwing;
            Yielded = false;
            return true;
        }

        /// <summary>Advance by a frame; nought under a pause. Forgets itself once it has run out.</summary>
        public void Step(float deltaSeconds)
        {
            if (Kind == HitReaction.None) return;
            if (deltaSeconds > 0f) Seconds += deltaSeconds;
            if (Seconds >= Span) Clear();
        }

        /// <summary>The computed shape at this instant (<see cref="CombatReactions.Pose"/>).</summary>
        public ReactionPose Pose() => CombatReactions.Pose(Kind, Side, Seconds);

        /// <summary>
        /// Who has the clip layer this frame (<see cref="CombatReactions.Arbitrate"/>), remembering a
        /// hand-over to the swing (<see cref="Yielded"/>). <paramref name="swingPhase"/> is 0 at the
        /// start of the body's own wind-up and 1 on its impact tick.
        /// </summary>
        public ActionShown Show(bool swinging, float swingPhase)
        {
            bool live = Live;
            ActionShown shown = CombatReactions.Arbitrate(swinging, swingPhase, live && !Yielded,
                CancelsSwing);
            if (live && swinging && shown == ActionShown.Swing) Yielded = true;
            return shown;
        }

        /// <summary>
        /// The computed flinch to lay over whatever the layer shows this frame, or rest: over the
        /// swing that holds it, and over the idle or the walk once a reaction that yielded to that
        /// swing outlives it — so a blow is seen for its whole flinch whatever took the layer.
        /// Nothing when the reaction itself is showing, or over.
        /// </summary>
        public ReactionPose Overlay(ActionShown shown) =>
            shown != ActionShown.Reaction && Live ? Pose() : default;

        public void Clear()
        {
            Kind = HitReaction.None;
            Seconds = 0f;
            Span = 0f;
            CancelsSwing = false;
            Yielded = false;
        }
    }

    /// <summary>
    /// The drawn slide of a body knocked back a tile (design 33 §9a, §9b): from where the blow found
    /// it to where the simulation put it, along the line of the blow, over
    /// <see cref="Seconds"/>. Horizontally it is thrown and slows; downwards, where it lands a layer
    /// lower, it falls — slow off the edge and quickening — so it goes over the lip before it
    /// drops. The pawn is on the landing tile from the tick of the blow; this is only the drawing.
    /// </summary>
    public static class KnockbackSlide
    {
        /// <summary>How long the slide takes, in seconds of game time.</summary>
        public const float Seconds = 0.25f;

        /// <summary>How far along the ground, 0 at the blow and 1 on landing: thrown, then slowing.</summary>
        public static float Along(float seconds)
        {
            float t = Clamp01(seconds / Seconds);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>How far down a drop, 0 at the blow and 1 on landing: falling, so quickening.</summary>
        public static float Down(float seconds)
        {
            float t = Clamp01(seconds / Seconds);
            return t * t;
        }

        public static bool Finished(float seconds) => seconds >= Seconds;

        /// <summary>
        /// Where the body is drawn, as an offset from the landing position, given the offset of the
        /// from-position from it (<paramref name="fromX"/>…, from minus landing). All of it at the
        /// blow, none of it on landing. The vertical part falls when the from-position is higher
        /// and follows the ground otherwise.
        /// </summary>
        public static void Offset(float fromX, float fromY, float fromZ, float seconds,
            out float x, out float y, out float z)
        {
            float along = 1f - Along(seconds);
            x = fromX * along;
            z = fromZ * along;
            y = fromY * (fromY > 0f ? 1f - Down(seconds) : along);
        }

        // ---- the fling (design 62 §8) ------------------------------------------------------

        /// <summary>
        /// How long a butcher's fling takes to draw, in seconds of game time: two cells, or a drop
        /// of several layers, at the one-tile slide's quarter second would read as a teleport.
        /// INVENTED; the first play says whether it reads as thrown.
        /// </summary>
        public const float FlingSeconds = 0.45f;

        /// <summary>How high a fling lifts the body at its midpoint, in metres: a throw, not a shove. INVENTED.</summary>
        public const float FlingArcMetres = 0.6f;

        /// <summary>Is the slide of this length a fling — further than the one-tile knockback can go?</summary>
        public static bool IsFling(float horizontal, float vertical, float cell, float layer) =>
            horizontal > cell * 1.5f || (vertical < 0f ? -vertical : vertical) > layer * 1.5f;

        /// <summary>
        /// The slide over <paramref name="duration"/> with a lift of <paramref name="arc"/> metres at
        /// its middle — <see cref="Seconds"/> and nought give exactly <see cref="Offset(float, float, float, float, out float, out float, out float)"/>.
        /// </summary>
        public static void Offset(float fromX, float fromY, float fromZ, float seconds, float duration, float arc,
            out float x, out float y, out float z)
        {
            float scaled = duration > 0f ? seconds * Seconds / duration : Seconds;
            Offset(fromX, fromY, fromZ, scaled, out x, out y, out z);
            float t = Clamp01(scaled / Seconds);
            y += arc * 4f * t * (1f - t);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
