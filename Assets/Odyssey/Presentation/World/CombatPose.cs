#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// What a figure is doing in a fight, as the director draws it (design 33 §1, §3). A one-shot
    /// role — a swing, a reaction, getting up — or one of the two held states, stunned and down.
    ///
    /// <para>The first nine are the Sword Combat pack's rows (<see cref="ModuleIds.CombatRows"/>,
    /// in that order after <see cref="None"/>); <see cref="Punch"/> and <see cref="Bite"/> are the
    /// two the pack does not have at all and are always computed.</para>
    /// </summary>
    public enum CombatRole : byte
    {
        None = 0,
        SwingLight = 1,
        SwingHeavy = 2,
        HitReact = 3,
        Stagger = 4,
        Dodge = 5,
        Stun = 6,
        Downed = 7,
        Death = 8,
        DeathPose = 9,
        Punch = 10,
        Bite = 11,
    }

    /// <summary>
    /// Which side of a figure a blow came from, in the figure's own frame. The same values as
    /// <see cref="HitSide"/>, which the Unity-free model answers in and which this casts to.
    /// </summary>
    public enum BlowSide : byte
    {
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// The variant names a combat row's clips carry (<see cref="CombatClipEntry.variant"/>). The
    /// catalogue build writes them and the figure asks for them, so they are spelled once.
    /// </summary>
    public static class CombatVariant
    {
        public const string Front = "F";
        public const string Back = "B";
        public const string Left = "L";
        public const string Right = "R";

        public const string Begin = "Begin";
        public const string Loop = "Loop";
        public const string End = "End";

        /// <summary>The body's sex, for the draw and the sheathe (design 33 §8b): the pack authors each twice.</summary>
        public const string Masc = "Masc";
        public const string Femn = "Femn";

        /// <summary>Combo steps, played in turn, A, B, C, by a light swing and a heavy one alike.</summary>
        public static readonly string[] Combo = { "A", "B", "C" };
    }

    /// <summary>
    /// One computed combat pose, as a handful of numbers laid over the clip underneath — the same
    /// bargain <see cref="WorkSwing"/> makes for the axe.
    ///
    /// <para><b>Signs</b>, which are the thing to get right and are not one convention (see
    /// <see cref="WorkSwing"/>): <see cref="Shoulder"/> and <see cref="Elbow"/> are how far the
    /// working arm is raised forward and bent, positive up and in; <see cref="Spine"/> is the
    /// fold, positive forward into the blow and negative leaning back; <see cref="Twist"/> turns
    /// the chest, positive to the figure's right; <see cref="Lunge"/> and <see cref="Side"/> move
    /// the whole figure, in metres, forward and to its right; <see cref="Head"/> nods the head on top of
    /// the chest, positive forward — the flinch's snap (design 33 §9a).</para>
    /// </summary>
    public readonly struct CombatShape
    {
        public readonly float Shoulder;
        public readonly float Elbow;
        public readonly float OffShoulder;
        public readonly float Spine;
        public readonly float Twist;
        public readonly float Lunge;
        public readonly float Side;
        public readonly float Head;

        public CombatShape(float shoulder, float elbow, float offShoulder, float spine, float twist,
            float lunge, float side, float head = 0f)
        {
            Shoulder = shoulder;
            Elbow = elbow;
            OffShoulder = offShoulder;
            Spine = spine;
            Twist = twist;
            Lunge = lunge;
            Side = side;
            Head = head;
        }

        public static readonly CombatShape Rest = default;

        public CombatShape Scaled(float weight) => new CombatShape(
            Shoulder * weight, Elbow * weight, OffShoulder * weight, Spine * weight, Twist * weight,
            Lunge * weight, Side * weight, Head * weight);

        /// <summary>Two shapes laid on together: a flinch over a computed swing.</summary>
        public CombatShape Plus(in CombatShape other) => new CombatShape(
            Shoulder + other.Shoulder, Elbow + other.Elbow, OffShoulder + other.OffShoulder,
            Spine + other.Spine, Twist + other.Twist, Lunge + other.Lunge, Side + other.Side,
            Head + other.Head);

        /// <summary>A computed reaction from the Unity-free model: the body and the head, never the arms.</summary>
        public static CombatShape Of(in ReactionPose pose) =>
            new CombatShape(0f, 0f, 0f, pose.Spine, pose.Twist, pose.Lunge, pose.Side, pose.Head);

        public static CombatShape Lerp(in CombatShape a, in CombatShape b, float t) => new CombatShape(
            Mathf.LerpUnclamped(a.Shoulder, b.Shoulder, t),
            Mathf.LerpUnclamped(a.Elbow, b.Elbow, t),
            Mathf.LerpUnclamped(a.OffShoulder, b.OffShoulder, t),
            Mathf.LerpUnclamped(a.Spine, b.Spine, t),
            Mathf.LerpUnclamped(a.Twist, b.Twist, t),
            Mathf.LerpUnclamped(a.Lunge, b.Lunge, t),
            Mathf.LerpUnclamped(a.Side, b.Side, t),
            Mathf.LerpUnclamped(a.Head, b.Head, t));

        /// <summary>True when nothing in it would move a bone or the body.</summary>
        public bool IsRest =>
            Mathf.Abs(Shoulder) < 1e-3f && Mathf.Abs(Elbow) < 1e-3f && Mathf.Abs(OffShoulder) < 1e-3f
            && Mathf.Abs(Spine) < 1e-3f && Mathf.Abs(Twist) < 1e-3f && Mathf.Abs(Head) < 1e-3f
            && Mathf.Abs(Lunge) < 1e-4f && Mathf.Abs(Side) < 1e-4f;
    }

    /// <summary>
    /// The fight's computed poses (design 33 §1): the fallback for every role the Sword Combat
    /// pack's clips play when the pack is present — a swing for each <c>AttackStyle</c> timed so
    /// its impact lands on the attack's <c>windupTicks</c>, a hit react, a stagger, a dodge, a
    /// stun — and the punch and the bite the pack does not have at all. The downed lie and the
    /// death pose are the sleeper's lie and <see cref="LieFlat"/>, because a body on the ground is
    /// a body on the ground. Pure functions of a phase, like <see cref="WorkSwing"/>,
    /// <see cref="CarryPose"/> and <see cref="SleepPose"/>, so EditMode tests can hold them
    /// without the pack.
    ///
    /// <para><b>One clock for the swing, and it is the simulation's.</b> A swing is timed in
    /// ticks since its wind-up began, read off the frame's tick plus the part-tick the frame is
    /// drawn at, so its impact lands on the tick the simulation resolves the blow at whatever the
    /// game speed, and it stops dead under a pause. A clip is scaled so its <i>authored</i> impact
    /// frame — the end of its WindUp sub-clip, measured by the catalogue build — falls on that
    /// tick (<see cref="ClipTime"/>); a computed swing is shaped so its blow falls there
    /// (<see cref="Lands"/>). The reactions have no tick to meet and run on the frame's own
    /// seconds, stopped by a pause like every other ease in the director.</para>
    /// </summary>
    public static class CombatPose
    {
        /// <summary>
        /// Ticks a second at normal speed, for the one case a swing has no measured impact to
        /// scale by: a clip with no WindUp sub-clip plays at its authored rate.
        /// </summary>
        public const float NominalTicksPerSecond = 60f;

        /// <summary>
        /// How far past the impact a computed swing runs before it is over, as a fraction of its
        /// wind-up. The arm comes back through the follow-through in a little less time than it
        /// took to raise, which is what makes a blow read as a blow rather than as a wave.
        /// </summary>
        public const float RecoverFraction = 0.8f;

        /// <summary>How long a computed hit react runs, in seconds: the flinch (design 33 §9a).</summary>
        public const float ReactSeconds = CombatReactions.FlinchSeconds;

        /// <summary>How long a computed stagger runs: longer and bigger than a react.</summary>
        public const float StaggerSeconds = CombatReactions.StaggerSeconds;

        /// <summary>How long a computed dodge runs.</summary>
        public const float DodgeSeconds = 0.6f;

        /// <summary>
        /// A blow at or above this, in thousandths of a hit point, staggers rather than flinches
        /// (owner, 2026-09-23: 12 or more). A stun and a critical always stagger. The model's number.
        /// </summary>
        public const int StaggerDamageMilli = CombatReactions.StaggerDamageMilli;

        /// <summary>
        /// How far a body lying on its back is raised off the floor it lies on, in metres at the
        /// figure's drawn scale: the depth from the spine to the back of the ribs. Without it the
        /// figure's root is on the floor and its back is through it.
        /// </summary>
        public const float BackDepth = 0.18f;

        /// <summary>The Sword Combat row a role draws from, or null for a role the pack has no clip for.</summary>
        public static string? RowOf(CombatRole role) => role switch
        {
            CombatRole.SwingLight => ModuleIds.CombatSwingLight,
            CombatRole.SwingHeavy => ModuleIds.CombatSwingHeavy,
            CombatRole.HitReact => ModuleIds.CombatHitReact,
            CombatRole.Stagger => ModuleIds.CombatStagger,
            CombatRole.Dodge => ModuleIds.CombatDodge,
            CombatRole.Stun => ModuleIds.CombatStun,
            CombatRole.Downed => ModuleIds.CombatDowned,
            CombatRole.Death => ModuleIds.CombatDeath,
            CombatRole.DeathPose => ModuleIds.CombatDeathPose,
            _ => null,
        };

        /// <summary>
        /// Whether a role has a computed pose to stand in when its clip is absent. Every role does;
        /// the question exists so a test can say so of each row rather than assume it.
        /// </summary>
        public static bool HasFallback(CombatRole role) => role != CombatRole.None;

        /// <summary>
        /// The swing a style is drawn with (design 33 §5j): a weapon's <c>AttackDef.style</c> picks
        /// the family, bare hands punch and an animal bites. Only the light and the heavy swing
        /// have the pack's clips.
        /// </summary>
        public static CombatRole SwingRole(AttackStyle style) => style switch
        {
            AttackStyle.Light => CombatRole.SwingLight,
            AttackStyle.Heavy => CombatRole.SwingHeavy,
            AttackStyle.Bite => CombatRole.Bite,
            _ => CombatRole.Punch,
        };

        /// <summary>
        /// Which family a swing is (design 33 §5j): the event's weapon's style where the weapon is
        /// an item with an attack, else the attacker's own — a person fights with fists, an animal
        /// bites. <paramref name="weaponStyles"/> is indexed by item def and holds null for an item
        /// that is not a weapon.
        /// </summary>
        /// <param name="natural">The attacker's species' own attack, when it fights with one and
        /// holds nothing (design 62 §5: the butcher's cleaver swings heavy); null falls back to fists
        /// for a person and a bite for an animal, which is every species' answer before it.</param>
        public static AttackStyle StyleFor(int weapon, IReadOnlyList<AttackStyle?>? weaponStyles, bool isPerson,
            AttackStyle? natural = null)
        {
            if (weapon >= 0 && weaponStyles != null && weapon < weaponStyles.Count
                && weaponStyles[weapon] is AttackStyle style)
                return style;
            if (natural is AttackStyle own) return own;
            return isPerson ? AttackStyle.Fists : AttackStyle.Bite;
        }

        /// <summary>
        /// Every kind's natural attack style, or null for a kind whose species has none — indexed by
        /// kind, as a pawn view carries it. Built once, off the content.
        /// </summary>
        public static AttackStyle?[] NaturalStylesOf(PawnContent? content)
        {
            if (content == null) return Array.Empty<AttackStyle?>();
            var styles = new AttackStyle?[content.Kinds.Length];
            for (int k = 0; k < styles.Length; k++) styles[k] = content.SpeciesOf(k).naturalAttack?.style;
            return styles;
        }

        /// <summary>Every item def's attack style, or null for an item that is not a weapon. Built once.</summary>
        public static AttackStyle?[] StylesOf(IReadOnlyList<ItemDef>? items)
        {
            if (items == null) return Array.Empty<AttackStyle?>();
            var styles = new AttackStyle?[items.Count];
            for (int i = 0; i < items.Count; i++) styles[i] = items[i]?.weapon?.style;
            return styles;
        }

        /// <summary>
        /// Which side of a figure facing <paramref name="forward"/> a blow from the direction
        /// <paramref name="toAttacker"/> arrives on. Horizontal only; a blow from straight above or
        /// with no direction at all is taken as from the front, which is where a figure that turned
        /// to fight is looking.
        /// </summary>
        public static BlowSide SideOf(Vector3 forward, Vector3 toAttacker) =>
            (BlowSide)CombatReactions.SideOf(forward.x, forward.z, toAttacker.x, toAttacker.z);

        /// <summary>The directional variant a hit react, a stagger or a death is drawn from.</summary>
        public static string VariantOf(BlowSide side) => side switch
        {
            BlowSide.Back => CombatVariant.Back,
            BlowSide.Left => CombatVariant.Left,
            BlowSide.Right => CombatVariant.Right,
            _ => CombatVariant.Front,
        };

        /// <summary>
        /// Which way a figure dodges a blow from <paramref name="side"/>: away from it. A blow from
        /// the front steps back and one from behind steps forward; one from the right steps left.
        /// <b>Never right</b>: the pack's <c>Dodge_R</c> imports Generic and cannot drive our rigs
        /// (<c>synty-sword-combat.md</c>), so a blow from the left is dodged backwards.
        /// </summary>
        public static string DodgeVariant(BlowSide side) => side switch
        {
            BlowSide.Back => CombatVariant.Front,
            BlowSide.Right => CombatVariant.Left,
            _ => CombatVariant.Back,
        };

        /// <summary>
        /// How far through a swing a figure is: 0 as the wind-up starts, 1 on the tick the blow
        /// lands, past 1 in the follow-through. A swing with no wind-up has already landed.
        /// </summary>
        public static float Phase(float elapsedTicks, int windupTicks) =>
            windupTicks > 0 ? elapsedTicks / windupTicks : 1f + Mathf.Max(0f, elapsedTicks) / NominalTicksPerSecond;

        /// <summary>Whether the blow landed between two phases: the one frame the chips, the sound and the jolt belong to.</summary>
        public static bool Lands(float phaseBefore, float phaseAfter) => phaseBefore < 1f && phaseAfter >= 1f;

        /// <summary>
        /// Where in a clip a swing is, in the clip's own seconds, given the ticks since its wind-up
        /// began. <b>The measured impact lands on the wind-up tick</b>: the clip is played at
        /// <c>impactSeconds / windupTicks</c> seconds a tick, before the impact and after it, so the
        /// follow-through keeps the pace the blow was struck at. A clip with no measured impact, or a
        /// swing with no wind-up, plays at its authored rate at normal speed.
        /// </summary>
        public static float ClipTime(float elapsedTicks, int windupTicks, float impactSeconds)
        {
            if (elapsedTicks <= 0f) return 0f;
            if (windupTicks > 0 && impactSeconds > 0f) return elapsedTicks * (impactSeconds / windupTicks);
            return elapsedTicks / NominalTicksPerSecond;
        }

        /// <summary>
        /// The rate a clip plays at against normal-speed game time, for a diagnostic: 1 is the
        /// authored rate. Above 1 the blow is quicker than the animator drew it.
        /// </summary>
        public static float ClipRate(int windupTicks, float impactSeconds) =>
            windupTicks > 0 && impactSeconds > 0f
                ? impactSeconds * NominalTicksPerSecond / windupTicks
                : 1f;

        /// <summary>Whether a computed swing is over: past its impact by <see cref="RecoverFraction"/> of its wind-up.</summary>
        public static bool SwingFinished(float phase) => phase >= 1f + RecoverFraction;

        // The computed swings as three keys each: the cock at the top of the wind-up, the blow at
        // the impact, and rest. Between rest and the cock the pose eases out; from the cock to the
        // blow it is the fast half; after the blow it eases back to rest over the recovery.
        static readonly CombatShape PunchCock = new CombatShape(-25f, 110f, 45f, 0f, -22f, -0.05f, 0f);
        static readonly CombatShape PunchBlow = new CombatShape(85f, 5f, 40f, 8f, 22f, 0.18f, 0f);
        static readonly CombatShape LightCock = new CombatShape(150f, 60f, 20f, -6f, -25f, 0f, 0f);
        static readonly CombatShape LightBlow = new CombatShape(45f, 10f, 15f, 12f, 25f, 0.12f, 0f);
        static readonly CombatShape HeavyCock = new CombatShape(165f, 50f, 150f, -15f, -30f, -0.05f, 0f);
        static readonly CombatShape HeavyBlow = new CombatShape(20f, 5f, 20f, 28f, 18f, 0.22f, 0f);
        static readonly CombatShape BiteCock = new CombatShape(0f, 0f, 0f, -8f, 0f, -0.12f, 0f);
        static readonly CombatShape BiteBlow = new CombatShape(0f, 0f, 0f, 14f, 0f, 0.35f, 0f);

        /// <summary>Where in the wind-up the arm reaches the top of its cock and starts down.</summary>
        const float CockAt = 0.7f;

        /// <summary>
        /// A computed swing at a phase (see <see cref="Phase"/>). The blow is at phase 1 exactly,
        /// which is the wind-up tick; the cock is reached at <see cref="CockAt"/>; the pose is back
        /// at rest at <c>1 + </c><see cref="RecoverFraction"/>.
        /// </summary>
        public static CombatShape Swing(CombatRole role, float phase)
        {
            Keys(role, out CombatShape cock, out CombatShape blow);
            if (phase <= 0f || SwingFinished(phase)) return CombatShape.Rest;

            if (phase < CockAt)
            {
                float t = phase / CockAt;
                return CombatShape.Lerp(CombatShape.Rest, cock, 1f - (1f - t) * (1f - t));
            }
            if (phase < 1f)
            {
                // The strike itself: slow off the top and fastest into the target.
                float t = (phase - CockAt) / (1f - CockAt);
                return CombatShape.Lerp(cock, blow, t * t);
            }

            float back = (phase - 1f) / RecoverFraction;
            return CombatShape.Lerp(blow, CombatShape.Rest, back * back * (3f - 2f * back));
        }

        static void Keys(CombatRole role, out CombatShape cock, out CombatShape blow)
        {
            switch (role)
            {
                case CombatRole.SwingLight: cock = LightCock; blow = LightBlow; return;
                case CombatRole.SwingHeavy: cock = HeavyCock; blow = HeavyBlow; return;
                case CombatRole.Bite: cock = BiteCock; blow = BiteBlow; return;
                default: cock = PunchCock; blow = PunchBlow; return;
            }
        }

        /// <summary>How long a computed reaction runs, in seconds; nought for a role that is not one.</summary>
        public static float ReactionSeconds(CombatRole role) => role switch
        {
            CombatRole.HitReact => ReactSeconds,
            CombatRole.Stagger => StaggerSeconds,
            CombatRole.Dodge => DodgeSeconds,
            _ => 0f,
        };

        /// <summary>
        /// A computed reaction <paramref name="seconds"/> in: thrown away from the side the blow
        /// came from, quickly, and recovered slowly. A dodge instead steps out of the way in the
        /// direction <see cref="DodgeVariant"/> picks and comes back.
        /// </summary>
        public static CombatShape Reaction(CombatRole role, BlowSide side, float seconds)
        {
            // The flinch and the stagger are the Unity-free model's (design 33 §9a).
            if (role == CombatRole.HitReact || role == CombatRole.Stagger)
                return CombatShape.Of(CombatReactions.Pose(
                    role == CombatRole.Stagger ? HitReaction.Stagger : HitReaction.Flinch, (HitSide)side, seconds));

            float span = ReactionSeconds(role);
            if (span <= 0f || seconds <= 0f || seconds >= span) return CombatShape.Rest;

            // Up fast, down slow: a jolt, not a sway. Peaks at a sixth of the way through.
            float t = seconds / span;
            const float peak = 1f / 6f;
            float amount = t < peak ? t / peak : 1f - (t - peak) / (1f - peak);
            amount = amount * amount * (3f - 2f * amount);

            if (role == CombatRole.Dodge)
            {
                string way = DodgeVariant(side);
                float step = 0.5f * amount;
                return way == CombatVariant.Front ? new CombatShape(0f, 0f, 0f, 10f * amount, 0f, step, 0f)
                    : way == CombatVariant.Left ? new CombatShape(0f, 0f, 0f, 0f, -12f * amount, 0f, -step)
                    : new CombatShape(0f, 0f, 0f, -10f * amount, 0f, -step, 0f);
            }

            return CombatShape.Rest;
        }

        /// <summary>
        /// The reaction a figure is drawing, as a computed shape at its instant: the whole of it
        /// where it has the clip layer and nothing draws it, or laid over a swing that holds the
        /// layer (design 33 §9a). Rest when nothing is running.
        /// </summary>
        public static CombatShape Reaction(in ReactionTrack track) =>
            track.Live ? CombatShape.Of(track.Pose()) : CombatShape.Rest;

        /// <summary>The pack's row a reaction plays from: the hit react or the stagger.</summary>
        public static CombatRole RoleOf(HitReaction reaction) =>
            reaction == HitReaction.Stagger ? CombatRole.Stagger
            : reaction == HitReaction.Flinch ? CombatRole.HitReact
            : CombatRole.None;

        /// <summary>
        /// The stunned sway, <paramref name="seconds"/> into the stun: a slow, unsteady circle of
        /// the chest and the arms hanging a little forward. Held for as long as the flag is.
        /// </summary>
        public static CombatShape Stunned(float seconds)
        {
            float a = seconds * 2f * Mathf.PI * 0.7f;
            return new CombatShape(12f, 20f, 12f, 8f + 5f * Mathf.Sin(a), 7f * Mathf.Sin(a * 0.5f), 0f,
                0.05f * Mathf.Sin(a * 0.5f));
        }

        /// <summary>
        /// A body laid flat where it fell, for the computed corpse and for a figure the pack cannot
        /// pose: its feet at one end of the cell, its head at the other, towards
        /// <paramref name="headingYaw"/> — the way it fell — face up. An animal lies on its side
        /// along the same line. <paramref name="floor"/> is the drawn floor under the middle of the
        /// cell and <paramref name="length"/> the body's drawn length (its standing height).
        /// </summary>
        public static void LieFlat(Vector3 floor, float headingYaw, float length, bool animal,
            out Vector3 root, out Quaternion rotation)
        {
            Vector3 heading = Quaternion.Euler(0f, headingYaw, 0f) * Vector3.forward;
            if (animal)
            {
                // Rolled on to its flank about its own length: the root stays under the middle.
                rotation = Quaternion.Euler(0f, headingYaw, 0f) * Quaternion.Euler(0f, 0f, 90f);
                root = floor + Vector3.up * Mathf.Min(0.25f, length * 0.2f);
                return;
            }

            // Head along the heading, face to the sky: the figure's up is the heading and its
            // forward is the world's up, so the back is on the floor.
            rotation = Quaternion.LookRotation(Vector3.up, heading);
            root = floor - heading * (length * 0.5f) + Vector3.up * BackDepth;
        }

        /// <summary>The yaw a corpse's eight-way facing names: 0 is +Z, counting clockwise from above.</summary>
        public static float YawOfFacing(byte facing) => (facing & 7) * 45f;
    }
}
