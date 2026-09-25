#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <see cref="PawnFigureDirector"/>: the weapon <b>sheathed at the left hip</b> or <b>drawn in
    /// the right hand</b> (design 33 §8b, owner 2026-09-23).
    ///
    /// <para><b>Who decides what.</b> Whether the weapon should be out is the simulation's, published
    /// as <see cref="PawnFlags.Drawn"/>; how long it stays out after the last reason is
    /// <see cref="WeaponSheath"/>'s; this file owns only where the prop hangs and the animation
    /// between the two. Nothing here is saved or hashed.</para>
    ///
    /// <para><b>The sheath is measured, not authored</b> (design 33 §9c). At bind, in the idle, the
    /// drawn skin is baked and mapped as a relief of the figure's left side (<see cref="SheathSurface"/>):
    /// how far out the body reaches, and how far in the hanging arm comes, at every height and depth
    /// round the hip. It is kept in the pelvis's own space — the parent of the left thigh, because
    /// <c>HumanBodyBones.Hips</c> on the Synty avatar is <c>Root</c>, on the floor — so it rides the
    /// pelvis and not the swinging leg. The relief is the envelope of the whole idle, because every
    /// pawn plays its idle from a phase of its own. Each weapon is then fitted once, off its measured
    /// profile (<see cref="WeaponProfile"/>, since a player cannot read the meshes): the haft hanging
    /// nearly straight down, splayed as far as the leg is, the flat of the blade against the thigh,
    /// the point a quarter of the way up from the butt at the hip joint's height, and the whole weapon
    /// slid out until its nearest point is <see cref="SheathClearance"/> off the relief — and back,
    /// if that clears the hanging hand.</para>
    ///
    /// <para><b>The pack's clips, where they are.</b> <c>A_Draw_Sword</c> and <c>A_Sheathe_Sword</c>
    /// (masculine and feminine) play on an upper-body layer over the walk, and the prop changes
    /// bone on the frame the hand is on the hilt — measured once per clip by sampling it on the
    /// first figure that can play it and taking the frame the palm comes nearest the stow point
    /// (<see cref="SheathMoment"/>), not the clip's start. Without the pack the weapon snaps.</para>
    ///
    /// <para><b>The hand is one hand.</b> While a tool is in it or a load in the arms, the weapon
    /// is at the hip whatever the rule says and no draw plays; the tool's own ease and this share
    /// one threshold, so the two are never in the fist together.</para>
    ///
    /// <para><b>Per-frame cost</b> scales with the live figures (capped at 64): one clock step, and
    /// a re-parent only on the frame the weapon changes place. The mesh bake and the clip sampling
    /// happen once per figure and once per clip, when a figure is built.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>The draw and the sheathe's input on the figure's layer mixer: over the gaits (0) and the fight (1).</summary>
        internal const int SheathLayerInput = 2;

        /// <summary>
        /// How far the weapon leans back from straight down, in degrees: near enough vertical that
        /// it reads as hung at the hip (owner, 2026-09-23), with the butt a touch forward. Was 25,
        /// which put a bat's head behind the knee (design 33 §9c).
        /// </summary>
        public const float SheathTiltDegrees = 8f;

        /// <summary>The point of a weapon that hangs at the hip joint's height, as a fraction of its length from the butt.</summary>
        public const float SheathHangFraction = 0.25f;

        /// <summary>
        /// The gap kept between the body and a sheathed weapon, as a fraction of the figure's height
        /// (about a centimetre on a 2.4 m colonist). Kept between the weapon's measured profile and
        /// the relief of the whole idle, so the drawn gap at the nearest point comes out at one to
        /// three centimetres whatever instant of the idle is drawn (design 33 §9c, measured).
        /// </summary>
        public const float SheathClearance = 0.004f;

        /// <summary>
        /// How far back, and how far forward, of the side of the hip a weapon may move to pass the
        /// hanging hand rather than go through it, as fractions of the figure's height. Behind is
        /// where a scabbard hangs; much in front, and a weapon hangs over the knee.
        /// </summary>
        public const float SheathBehind = 0.06f, SheathAhead = 0.02f;

        /// <summary>
        /// The least a sheathed weapon's lowest point clears the floor by, as a fraction of the
        /// figure's height: the arc blade is longer than the leg and is hung higher to keep it.
        /// </summary>
        public const float SheathFloorClearance = 0.02f;

        /// <summary>
        /// The most a weapon is splayed out from plumb to follow a leg that stands wider at the
        /// knee than at the hip, in degrees: with the lean back, still within the owner's
        /// "roughly vertical".
        /// </summary>
        public const float SheathMostSplay = 10f;

        /// <summary>How many instants of the idle the hip's relief is the envelope of.</summary>
        public const int SheathIdleSamples = 6;

        /// <summary>
        /// Where in the draw the hand takes the hilt, and in the sheathe lets it go, as a fraction of
        /// the clip, when the sampling could not find the hand at the hip. INVENTED.
        /// </summary>
        public const float DrawGraspFallback = 0.35f;
        public const float SheatheReleaseFallback = 0.65f;

        /// <summary>
        /// How near the palm must come to the stow point for the sampled moment to be believed, as
        /// a fraction of the figure's height; further, and the fallback fraction is used. INVENTED.
        /// </summary>
        public const float SheathReachTolerance = 0.2f;

        /// <summary>The sheath rows' usable clips by row id, read out of the catalogue once.</summary>
        Dictionary<string, List<CombatClipEntry>>? _sheathRows;

        /// <summary>The measured hand-on-hilt moment of each draw and sheathe clip, in seconds.</summary>
        readonly Dictionary<CombatClipEntry, float> _sheathMoments = new Dictionary<CombatClipEntry, float>();

        Dictionary<string, List<CombatClipEntry>> SheathRows
        {
            get
            {
                if (_sheathRows != null) return _sheathRows;
                _sheathRows = new Dictionary<string, List<CombatClipEntry>>(StringComparer.Ordinal);
                if (_catalogue == null) return _sheathRows;
                foreach (string id in ModuleIds.SheathRows)
                {
                    ModuleEntry? row = _catalogue.Find(id);
                    if (row == null) continue;
                    var usable = new List<CombatClipEntry>();
                    foreach (CombatClipEntry entry in row.combat)
                        if (entry.clip != null) usable.Add(entry);
                    if (usable.Count > 0) _sheathRows[id] = usable;
                }
                return _sheathRows;
            }
        }

        /// <summary>True when the draw and the sheathe both resolved to clips: the weapon is animated, not snapped.</summary>
        public bool HasSheathClips =>
            SheathRows.ContainsKey(ModuleIds.CombatDraw) && SheathRows.ContainsKey(ModuleIds.CombatSheathe);

        /// <summary>The draw or the sheathe for a body, or null: the body's own sex first, else the row's first clip.</summary>
        public CombatClipEntry? SheathClipFor(bool draw, bool feminine)
        {
            string id = draw ? ModuleIds.CombatDraw : ModuleIds.CombatSheathe;
            if (!SheathRows.TryGetValue(id, out List<CombatClipEntry>? clips)) return null;
            string variant = feminine ? CombatVariant.Femn : CombatVariant.Masc;
            for (int i = 0; i < clips.Count; i++)
                if (string.Equals(clips[i].variant, variant, StringComparison.Ordinal)) return clips[i];
            return clips[0];
        }

        /// <summary>
        /// The seconds into a draw or sheathe clip at which the prop changes bone, as measured, or
        /// the fallback fraction of its length when it has not been (or could not be).
        /// </summary>
        public float SheathMoment(CombatClipEntry clip, bool draw)
        {
            if (_sheathMoments.TryGetValue(clip, out float seconds)) return seconds;
            float length = clip.clip != null ? clip.clip.length : 0f;
            return length * (draw ? DrawGraspFallback : SheatheReleaseFallback);
        }

        /// <summary>The upper body — everything but the root, the legs and the feet — for the sheath layer.</summary>
        static AvatarMask? s_upperBody;

        static AvatarMask UpperBody
        {
            get
            {
                if (s_upperBody != null) return s_upperBody;
                var mask = new AvatarMask { name = "Odyssey/UpperBody" };
                for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
                {
                    bool legs = part == AvatarMaskBodyPart.Root
                                || part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg
                                || part == AvatarMaskBodyPart.LeftFootIK || part == AvatarMaskBodyPart.RightFootIK;
                    mask.SetHumanoidBodyPartActive(part, !legs);
                }
                s_upperBody = mask;
                return mask;
            }
        }

        // ---- Diagnostics, for the tests and the contact sheet ------------------------------------

        /// <summary>Whether a pawn's figure has its weapon at the hip (true) or in the hand (false); false with no weapon.</summary>
        public bool TryGetWeaponPlace(PawnId pawn, out bool atHip, out Transform? parent)
        {
            if (_byPawn.TryGetValue(pawn.Value, out Figure? figure) && figure.Weapon != null)
            {
                atHip = figure.WeaponAtHip;
                parent = figure.Weapon.transform.parent;
                return true;
            }
            atHip = false;
            parent = null;
            return false;
        }

        /// <summary>The bone a pawn's figure hangs its sheath from, or null.</summary>
        public Transform? PelvisOf(PawnId pawn) =>
            _byPawn.TryGetValue(pawn.Value, out Figure? figure) ? figure.Pelvis : null;

        /// <summary>A pawn's figure's right hand, or null.</summary>
        public Transform? RightHandOf(PawnId pawn) =>
            _byPawn.TryGetValue(pawn.Value, out Figure? figure) ? figure.RightHand : null;

        /// <summary>The draw or the sheathe a pawn's figure is playing, or none.</summary>
        public SheathChange SheathActionOf(PawnId pawn) =>
            _byPawn.TryGetValue(pawn.Value, out Figure? figure) ? figure.SheathAction : SheathChange.None;

        /// <summary>The draw or sheathe clip a pawn's figure is playing, or null.</summary>
        public CombatClipEntry? SheathClipOf(PawnId pawn) =>
            _byPawn.TryGetValue(pawn.Value, out Figure? figure) ? figure.SheathClip : null;

        /// <summary>
        /// How a pawn's sheathed weapon sits against its body as drawn this frame (design 33 §9c):
        /// false when the figure has no weapon at the hip. Bakes the skin; a diagnostic, never a
        /// per-frame call.
        /// </summary>
        public bool TryMeasureSheath(PawnId pawn, out SheathGap gap)
        {
            gap = default;
            if (!_byPawn.TryGetValue(pawn.Value, out Figure? figure) || figure.Weapon == null || !figure.WeaponAtHip
                || !TryGaugeBones(figure, out SheathGauge.Bones bones))
                return false;
            float height = figure.StandingHeight > 0.01f ? figure.StandingHeight : FigureBuild.FallbackHeight;
            Transform root = figure.Transform;
            Vector3 outward = -root.right;
            if (Vector3.Dot(figure.LeftUpperLeg!.position - root.position, outward) < 0f) outward = -outward;
            gap = SheathGauge.Measure(figure.Skins, bones, figure.Weapon.transform, outward, root.up, root.forward,
                figure.LeftUpperLeg.position.y, figure.Pelvis!.position.y, root.position.y, height);
            return true;
        }

        /// <summary>The bones a body's skin is split into arms and the rest by, as the figure stands now.</summary>
        static bool TryGaugeBones(Figure figure, out SheathGauge.Bones bones)
        {
            bones = default;
            if (figure.Pelvis == null || figure.LeftUpperLeg == null || figure.LeftLowerLeg == null) return false;
            Vector3 Of(Transform? bone, Vector3 fallback) => bone != null ? bone.position : fallback;
            Vector3 pelvis = figure.Pelvis.position;
            Vector3 spine = Of(figure.Spine, pelvis + figure.Transform.up * 0.2f);
            bones = new SheathGauge.Bones
            {
                LeftThigh = figure.LeftUpperLeg.position,
                LeftKnee = figure.LeftLowerLeg.position,
                LeftFoot = Of(figure.LeftFoot, figure.LeftLowerLeg.position),
                RightThigh = Of(figure.RightUpperLeg, figure.LeftUpperLeg.position),
                RightKnee = Of(figure.RightLowerLeg, figure.LeftLowerLeg.position),
                RightFoot = Of(figure.RightFoot, Of(figure.LeftFoot, figure.LeftLowerLeg.position)),
                Pelvis = pelvis,
                Spine = spine,
                Chest = Of(figure.Chest, spine),
                LeftShoulder = Of(figure.LeftUpperArm, spine),
                LeftElbow = Of(figure.LeftLowerArm, Of(figure.LeftUpperArm, spine)),
                LeftWrist = Of(figure.LeftHand, Of(figure.LeftLowerArm, Of(figure.LeftUpperArm, spine))),
                RightShoulder = Of(figure.RightUpperArm, spine),
                RightElbow = Of(figure.RightLowerArm, Of(figure.RightUpperArm, spine)),
                RightWrist = Of(figure.RightHand, Of(figure.RightLowerArm, Of(figure.RightUpperArm, spine))),
                LeftTips = Tips(figure.Animator, true),
                RightTips = Tips(figure.Animator, false),
                Height = figure.StandingHeight > 0.01f ? figure.StandingHeight : FigureBuild.FallbackHeight,
            };
            return true;
        }

        static readonly HumanBodyBones[] LeftFingers =
        {
            HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };

        static readonly HumanBodyBones[] RightFingers =
        {
            HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        /// <summary>
        /// Each mapped finger's tip as it hangs now: its last bone carried on by that bone's own
        /// length. Null when the rig maps no fingers.
        /// </summary>
        static Vector3[]? Tips(Animator? animator, bool left)
        {
            if (animator == null || !animator.isHuman) return null;
            HumanBodyBones[] fingers = left ? LeftFingers : RightFingers;
            var tips = new List<Vector3>(fingers.Length / 2);
            for (int i = 0; i + 1 < fingers.Length; i += 2)
            {
                Transform? middle = animator.GetBoneTransform(fingers[i]), last = animator.GetBoneTransform(fingers[i + 1]);
                if (middle == null || last == null) continue;
                tips.Add(last.position + (last.position - middle.position));
            }
            return tips.Count > 0 ? tips.ToArray() : null;
        }

        // ---- Bind: the stow point, and the clips' moments ------------------------------------------

        /// <summary>
        /// Measure where this figure's sheath hangs, in the idle, off its drawn mesh; then, once per
        /// clip, where in the draw and the sheathe its hand is on the hilt. Called at build, after
        /// the body is measured. Leaves the figure in the idle.
        /// </summary>
        void BindSheath(Figure figure, bool feminine)
        {
            if (figure.LeftUpperLeg == null) return;
            figure.Pelvis = figure.LeftUpperLeg.parent;
            if (figure.Pelvis == null) return;

            figure.Graph.Evaluate(0f);
            MeasureHipStow(figure);
            if (!figure.HasStow || !figure.Fight.HasSheathLayer) return;

            for (int draw = 0; draw < 2; draw++)
            {
                CombatClipEntry? clip = SheathClipFor(draw == 0, feminine);
                if (clip != null && !_sheathMoments.ContainsKey(clip))
                    _sheathMoments[clip] = MeasureSheathMoment(figure, clip, draw == 0);
            }
            figure.Graph.Evaluate(0f);
        }

        /// <summary>
        /// The stow frame, in the pelvis's space: the hip's surface on the figure's left at the
        /// height and depth of the hip joint, with the blade leaning back by
        /// <see cref="SheathTiltDegrees"/>; and the relief every weapon is fitted against. The
        /// surface is the drawn mesh's; with nothing bakeable the hip joint's own offset, doubled,
        /// stands in.
        /// </summary>
        void MeasureHipStow(Figure figure)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Transform root = figure.Transform;
            Transform pelvis = figure.Pelvis!;
            Transform thigh = figure.LeftUpperLeg!;
            float height = figure.StandingHeight > 0.01f ? figure.StandingHeight : FigureBuild.FallbackHeight;

            Vector3 up = root.up;
            Vector3 forward = root.forward;
            Vector3 outward = -root.right;
            // The avatar's left thigh says which side is left; a rig mirrored from ours is believed.
            if (Vector3.Dot(thigh.position - root.position, outward) < 0f) outward = -outward;

            // The frame the relief is kept in: out, up and forward about the root, in metres.
            Vector3 origin = root.position;
            float hipY = Vector3.Dot(thigh.position - origin, up);
            float thighZ = Vector3.Dot(thigh.position - origin, forward);
            float joint = Vector3.Dot(thigh.position - origin, outward);

            // The whole idle, not its first frame: every pawn starts its clips at a phase of its
            // own (Desynchronise), and the idle shifts its weight from leg to leg, so a relief of
            // one instant is a hip another colonist's thigh swings through. Each sample is carried
            // into the pelvis as it stands now, which is what the weapon rides.
            //
            // One body, one relief: every figure wearing a look shares its mesh, its scale and its
            // idle, and the relief is kept in the pelvis's space, so it is measured once a look.
            _sheathReliefs.TryGetValue(figure.Look, out SheathSurface? surface);
            var body = new List<Vector3>();
            var arms = new List<Vector3>();
            Matrix4x4 bindPelvis = pelvis.localToWorldMatrix;
            AnimationClipPlayable idle = figure.Clips.Length > 0 ? figure.Clips[0] : default;
            double idleWas = idle.IsValid() ? idle.GetTime() : 0d;
            double idleLength = idle.IsValid() && idle.GetAnimationClip() != null ? idle.GetAnimationClip().length : 0d;
            int samples = surface != null ? 0 : idleLength > 1e-3 ? SheathIdleSamples : 1;
            for (int s = 0; s < samples; s++)
            {
                if (samples > 1)
                {
                    idle.SetTime(idleWas + idleLength * s / samples);
                    figure.Graph.Evaluate(0f);
                }
                if (!TryGaugeBones(figure, out SheathGauge.Bones bones)) break;
                int bodyFrom = body.Count, armsFrom = arms.Count;
                SheathGauge.BakeTriangles(figure.Skins, bones, body, arms, root.position, outward, up,
                    hipY - (SheathSurface.BelowHip + 0.05f) * height, hipY + (SheathSurface.AboveHip + 0.05f) * height);
                Matrix4x4 toBind = bindPelvis * pelvis.worldToLocalMatrix;
                for (int i = bodyFrom; i < body.Count; i++) body[i] = toBind.MultiplyPoint3x4(body[i]);
                for (int i = armsFrom; i < arms.Count; i++) arms[i] = toBind.MultiplyPoint3x4(arms[i]);
            }
            if (samples > 1)
            {
                idle.SetTime(idleWas);
                figure.Graph.Evaluate(0f);
            }
            if (surface == null && body.Count > 0)
            {
                surface = SheathSurface.Map(body, arms, origin, outward, forward, hipY, thighZ, height, bindPelvis);
                if (surface != null) _sheathReliefs[figure.Look] = surface;
            }
            if (surface != null) _sheathSurfaces[figure] = surface;
            else _sheathSurfaces.Remove(figure);

            // The hip's own surface beside the joint, for the grasp's sampling to aim at; with
            // nothing bakeable, the joint's offset doubled.
            float reach = surface != null ? surface.BodyAt(hipY, thighZ) : float.NegativeInfinity;
            if (float.IsNegativeInfinity(reach)) reach = 2f * Mathf.Max(0.01f, joint);
            Vector3 point = origin + outward * reach + up * hipY + forward * thighZ;

            float tilt = SheathTiltDegrees * Mathf.Deg2Rad;
            Vector3 down = (-up * Mathf.Cos(tilt) - forward * Mathf.Sin(tilt)).normalized;

            figure.StowPoint = pelvis.InverseTransformPoint(point);
            figure.StowDown = pelvis.InverseTransformDirection(down);
            figure.StowOut = pelvis.InverseTransformDirection(outward);
            figure.StowForward = pelvis.InverseTransformDirection(forward);
            figure.HasStow = true;
            MeasuredSheathBindMs = Math.Max(MeasuredSheathBindMs, watch.Elapsed.TotalMilliseconds);
        }

        /// <summary>The longest any figure's hip measurement has taken, in milliseconds: it is paid once, when a figure is built.</summary>
        public double MeasuredSheathBindMs { get; private set; }

        /// <summary>Each figure's measured hip relief (design 33 §9c), kept beside the figure rather than in it.</summary>
        readonly Dictionary<Figure, SheathSurface> _sheathSurfaces = new Dictionary<Figure, SheathSurface>();

        /// <summary>The relief measured for each look, shared by every figure wearing it.</summary>
        readonly Dictionary<int, SheathSurface> _sheathReliefs = new Dictionary<int, SheathSurface>();

        /// <summary>What each figure's last hip fit chose, for <see cref="DescribeSheathFit"/>.</summary>
        readonly Dictionary<Figure, SheathSurface.Placement> _sheathPlacements = new Dictionary<Figure, SheathSurface.Placement>();

        /// <summary>A world point (or, with <paramref name="direction"/>, a direction) in a pawn's figure's frame about its left hip joint, in centimetres. For the contact sheet.</summary>
        public string DescribeInFrame(PawnId pawn, Vector3 world, bool direction = false)
        {
            if (!_byPawn.TryGetValue(pawn.Value, out Figure? figure) || figure.LeftUpperLeg == null) return "?";
            Transform root = figure.Transform;
            Vector3 outward = -root.right;
            if (Vector3.Dot(figure.LeftUpperLeg.position - root.position, outward) < 0f) outward = -outward;
            Vector3 d = direction ? world : world - figure.LeftUpperLeg.position;
            return $"({Vector3.Dot(d, outward) * 100f:F1} out, {Vector3.Dot(d, root.up) * 100f:F1} up, {Vector3.Dot(d, root.forward) * 100f:F1} fwd)";
        }

        /// <summary>
        /// What the hip fit chose for a pawn's weapon, in the figure's frame about the hip joint:
        /// the splay, how far out and along it slid, what decided it, whether the hand was cleared,
        /// and the relief down the weapon's line. For the contact sheet.
        /// </summary>
        public string DescribeSheathFit(PawnId pawn)
        {
            if (!_byPawn.TryGetValue(pawn.Value, out Figure? figure)) return "no figure";
            if (!_sheathSurfaces.TryGetValue(figure, out SheathSurface? surface)) return "no relief";
            if (!_sheathPlacements.TryGetValue(figure, out SheathSurface.Placement p)) return "not fitted to the relief";
            Vector3 b = p.Binding;
            _sheathSplays.TryGetValue(figure, out float splay);
            return $"side depth {surface.SideDepth() * 100f:F1} cm, splay {splay:F0} deg, slid out {p.Outward * 100f:F1} along {p.Along * 100f:F1} cm, " +
                   $"decided by the point {b.x * 100f:F1} out {(b.y - surface.HipY) * 100f:F1} up {(b.z - surface.ThighZ) * 100f:F1} fwd of the hip joint, " +
                   $"hand {(p.ArmMiss > 0f ? $"overlapped by {p.ArmMiss * 100f:F1} cm" : "cleared")}; " +
                   $"the body out at that depth, every 10 cm down from the hip:{Column(surface, b.z)}";
        }

        static string Column(SheathSurface surface, float z)
        {
            var text = new System.Text.StringBuilder();
            for (int cm = 10; cm >= -110; cm -= 10)
            {
                float x = surface.BodyAt(surface.HipY + cm * 0.01f, z);
                text.Append(' ').Append(float.IsNegativeInfinity(x) ? "-" : (x * 100f).ToString("F0"));
            }
            return text.ToString();
        }

        /// <summary>
        /// Sample a draw or sheathe clip on this figure and return the seconds at which the palm is
        /// nearest the stow point — the hand on the hilt. Too far at its nearest, and the fallback
        /// fraction stands. The layer is left empty.
        /// </summary>
        float MeasureSheathMoment(Figure figure, CombatClipEntry clip, bool draw)
        {
            CombatState fight = figure.Fight;
            float length = clip.clip != null ? clip.clip.length : 0f;
            float fallback = length * (draw ? DrawGraspFallback : SheatheReleaseFallback);
            if (length <= 1e-3f || figure.RightHand == null) return fallback;

            PutSheathClip(figure, clip);
            fight.Layer.SetInputWeight(1, 0f);
            fight.Layer.SetInputWeight(SheathLayerInput, 1f);

            int samples = Mathf.Max(8, Mathf.CeilToInt(length * 60f));
            float bestSq = float.MaxValue, bestTime = fallback;
            for (int i = 0; i <= samples; i++)
            {
                float time = length * i / samples;
                fight.SheathSlot.SetTime(time);
                figure.Graph.Evaluate(0f);
                Vector3 hilt = figure.Pelvis!.TransformPoint(figure.StowPoint);
                float d = (HandGrip.Palm(figure.RightGrip) - hilt).sqrMagnitude;
                if (d < bestSq)
                {
                    bestSq = d;
                    bestTime = time;
                }
            }

            fight.Layer.SetInputWeight(SheathLayerInput, 0f);
            fight.Layer.SetInputWeight(1, fight.Weight);
            float height = figure.StandingHeight > 0.01f ? figure.StandingHeight : FigureBuild.FallbackHeight;
            return Mathf.Sqrt(bestSq) <= SheathReachTolerance * height ? bestTime : fallback;
        }

        void PutSheathClip(Figure figure, CombatClipEntry clip)
        {
            CombatState fight = figure.Fight;
            if (ReferenceEquals(fight.SheathSlotClip, clip) && fight.SheathSlot.IsValid()) return;
            if (fight.SheathSlot.IsValid())
            {
                figure.Graph.Disconnect(fight.Layer, SheathLayerInput);
                fight.SheathSlot.Destroy();
            }
            AnimationClipPlayable playable = AnimationClipPlayable.Create(figure.Graph, clip.clip);
            // Timed by hand, like every action clip (PawnFigureDirector.Combat.cs).
            playable.SetSpeed(0d);
            playable.SetApplyFootIK(false);
            figure.Graph.Connect(playable, 0, fight.Layer, SheathLayerInput);
            fight.SheathSlot = playable;
            fight.SheathSlotClip = clip;
        }

        // ---- The weapon's two places ---------------------------------------------------------------

        /// <summary>
        /// Fit a newly made weapon both ways: into the fist (<see cref="FitWeapon"/>, as it always
        /// was) and on the hip, and keep each as a local pose. The prop is left where it was fitted
        /// last; <see cref="PlaceWeapon"/> puts it where it belongs.
        /// </summary>
        void FitWeaponBothWays(Figure figure, Transform prop)
        {
            // A gun is held by its grip, not its butt (design 47 §4a).
            if (figure.IsGun) FitPistol(figure, prop);
            else FitWeapon(figure, prop);
            figure.WeaponHandPosition = prop.localPosition;
            figure.WeaponHandRotation = prop.localRotation;
            figure.WeaponAtHip = false;
            if (figure.Pelvis == null || !figure.HasStow) return;

            prop.SetParent(figure.Pelvis, false);
            FitWeaponAtHip(figure, prop);
            figure.WeaponHipPosition = prop.localPosition;
            figure.WeaponHipRotation = prop.localRotation;
            figure.WeaponAtHip = true;
        }

        /// <summary>
        /// Hang a weapon (already a child of the pelvis) in the sheath, measured off its mesh as the
        /// fist's fit is: the haft along the stow's down, the blade's width along the figure's front
        /// so the flat lies against the thigh, the point <see cref="SheathHangFraction"/> up from the
        /// butt at the stow point; then slid out of the idle's relief until its nearest point is
        /// <see cref="SheathClearance"/> clear (<see cref="SheathSurface.Fit"/>). The weapon's own
        /// points are its measured profile, because its mesh is not readable in a player.
        /// </summary>
        void FitWeaponAtHip(Figure figure, Transform prop)
        {
            prop.localPosition = Vector3.zero;
            prop.localRotation = Quaternion.identity;
            if (!LocalBounds(prop, out Bounds bounds)) return;

            Vector3 extents = bounds.extents;
            Vector3 haft = extents.x >= extents.y && extents.x >= extents.z ? Vector3.right
                : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            float half = Vector3.Dot(extents, haft);
            if (half <= 1e-4f) return;
            if (Vector3.Dot(bounds.center, haft) < 0f) haft = -haft;
            Vector3 bit = BitAxis(bounds, haft);

            Transform pelvis = figure.Pelvis!;
            Vector3 point = pelvis.TransformPoint(figure.StowPoint);
            Vector3 down = pelvis.TransformDirection(figure.StowDown).normalized;
            Vector3 outward = pelvis.TransformDirection(figure.StowOut).normalized;
            Vector3 forward = pelvis.TransformDirection(figure.StowForward).normalized;

            prop.rotation = Quaternion.FromToRotation(prop.TransformDirection(haft), down) * prop.rotation;
            Vector3 haftWorld = prop.TransformDirection(haft);
            Vector3 want = Vector3.ProjectOnPlane(forward, haftWorld);
            Vector3 have = Vector3.ProjectOnPlane(prop.TransformDirection(bit), haftWorld);
            if (want.sqrMagnitude > 1e-6f && have.sqrMagnitude > 1e-6f)
                prop.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(have, want, haftWorld), haftWorld) * prop.rotation;

            Vector3 flat = Vector3.Cross(haft, bit);
            float halfThick = Mathf.Abs(Vector3.Dot(extents, new Vector3(Mathf.Abs(flat.x), Mathf.Abs(flat.y), Mathf.Abs(flat.z))));
            float standOff = prop.TransformVector(flat.normalized * halfThick).magnitude;
            float height = figure.StandingHeight > 0.01f ? figure.StandingHeight : FigureBuild.FallbackHeight;

            // The hang point on the hip's surface beside the joint, then slid out of the body —
            // and back or forward past the hanging hand — by the relief the idle left.
            Vector3 butt = bounds.center - haft * half;
            Vector3 hang = butt + haft * (2f * half * SheathHangFraction);
            prop.position += point - prop.TransformPoint(hang);

            if (_sheathSurfaces.TryGetValue(figure, out SheathSurface? surface))
            {
                Matrix4x4 toFrame = surface.PelvisToFrame * pelvis.worldToLocalMatrix;
                List<Vector3> points = FramePoints(prop, surface, toFrame);

                // Splayed out as far as the leg is: a wide stance puts the knee further out than
                // the hip, and a weapon hung plumb from the hip then stands off it by the whole
                // difference (seven centimetres on the widest). The splay that brings the belt end
                // nearest the hip, searched a degree at a time.
                float splay = BestSplay(surface, points, toFrame.MultiplyPoint3x4(point), SheathClearance * height);
                if (splay > 0f)
                {
                    Vector3 axis = forward;
                    Vector3 turned = Quaternion.AngleAxis(splay, axis) * down;
                    if (Vector3.Dot(turned, outward) < 0f) axis = -axis;
                    prop.RotateAround(point, axis, splay);
                    points = FramePoints(prop, surface, toFrame);
                }
                _sheathSplays[figure] = splay;

                // Clear of the floor: a weapon longer than the leg is hung higher, not dragged.
                float lowest = float.PositiveInfinity;
                for (int i = 0; i < points.Count; i++) lowest = Mathf.Min(lowest, points[i].y);
                float lift = Mathf.Max(0f, SheathFloorClearance * height - lowest);
                for (int i = 0; i < points.Count; i++) points[i] += new Vector3(0f, lift, 0f);

                // The weapon's own line — a crowbar's shaft, not the middle of its claw — at the
                // depth the side of the hip stands out furthest, then out until it clears.
                float start = surface.SideDepth() - surface.SpineDepth(points);
                if (surface.Fit(points, SheathClearance * height, start, SheathBehind * height, SheathAhead * height,
                        out SheathSurface.Placement placement))
                {
                    Vector3 local = surface.FrameToPelvis.MultiplyVector(new Vector3(placement.Outward, lift, placement.Along));
                    prop.position += pelvis.TransformVector(local);
                    _sheathPlacements[figure] = placement;
                    return;
                }
            }
            _sheathPlacements.Remove(figure);

            // Nothing bakeable: stood off the stow point by its own half-thickness.
            prop.position += outward * (standOff + SheathClearance * height);
        }

        static List<Vector3> FramePoints(Transform prop, SheathSurface surface, Matrix4x4 toFrame)
        {
            List<Vector3> points = SheathGauge.HullPoints(prop, surface.Cell);
            for (int i = 0; i < points.Count; i++) points[i] = toFrame.MultiplyPoint3x4(points[i]);
            return points;
        }

        /// <summary>
        /// The splay, in whole degrees up to <see cref="SheathMostSplay"/>, that lets the belt end
        /// of a weapon (<paramref name="hang"/>, in the frame) come nearest the hip once the whole
        /// weapon is slid clear of the body; the least of equals.
        /// </summary>
        static float BestSplay(SheathSurface surface, List<Vector3> points, Vector3 hang, float clearance)
        {
            float start = surface.SideDepth() - surface.SpineDepth(points);
            float best = 0f, bestSlide = float.PositiveInfinity;
            var turned = new List<Vector3>(points.Count);
            for (int degrees = 0; degrees <= (int)SheathMostSplay; degrees++)
            {
                float s = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(s), sin = Mathf.Sin(s);
                turned.Clear();
                for (int i = 0; i < points.Count; i++)
                {
                    float dx = points[i].x - hang.x, dy = points[i].y - hang.y;
                    // Below the belt moves out.
                    turned.Add(new Vector3(hang.x + dx * cos - dy * sin, hang.y + dx * sin + dy * cos, points[i].z));
                }
                float slide = surface.Slide(turned, clearance, start);
                if (float.IsNegativeInfinity(slide)) continue;
                if (slide < bestSlide - 0.002f)
                {
                    bestSlide = slide;
                    best = degrees;
                }
            }
            return best;
        }

        /// <summary>What each figure's last hip fit splayed its weapon by, for <see cref="DescribeSheathFit"/>.</summary>
        readonly Dictionary<Figure, float> _sheathSplays = new Dictionary<Figure, float>();

        /// <summary>Put the weapon at the hip or in the hand, from the cached fits. A no-op when it is already there.</summary>
        static void PlaceWeapon(Figure figure, bool atHip)
        {
            GameObject? weapon = figure.Weapon;
            if (weapon == null) return;
            Transform? parent = atHip ? figure.Pelvis : figure.RightHand;
            if (atHip && !figure.HasStow) parent = null;
            if (parent == null) return;
            Transform prop = weapon.transform;
            if (figure.WeaponAtHip == atHip && prop.parent == parent) return;

            prop.SetParent(parent, false);
            prop.localPosition = atHip ? figure.WeaponHipPosition : figure.WeaponHandPosition;
            prop.localRotation = atHip ? figure.WeaponHipRotation : figure.WeaponHandRotation;
            figure.WeaponAtHip = atHip;
        }

        /// <summary>Forget the draw state: a figure lent to a new pawn takes its weapon as it finds it.</summary>
        static void ForgetSheath(Figure figure)
        {
            figure.Sheath = default;
            figure.SheathAction = SheathChange.None;
            figure.SheathClip = null;
            figure.SheathSeconds = 0f;
            if (figure.Fight.HasSheathLayer) figure.Fight.Layer.SetInputWeight(SheathLayerInput, 0f);
        }

        /// <summary>
        /// Once a frame per figure holding a weapon, from <see cref="ShowWeapon"/>: step the clock,
        /// start a draw or a sheathe on its edge, advance the one playing and move the prop at the
        /// hand-on-hilt moment, and drive the layer.
        /// </summary>
        void PoseSheath(Figure figure, in PawnView pawn, bool handBusy, bool feminine, float deltaTime)
        {
            SheathChange change = WeaponSheath.Step(ref figure.Sheath, in pawn, _frame != null ? _frame.Tick : 0);
            CombatState fight = figure.Fight;

            // The hand is taken — a tool or a load. The weapon waits at the hip and nothing plays;
            // when the hand is free again it goes where the clock says, without a flourish.
            if (handBusy || figure.Pelvis == null || !figure.HasStow)
            {
                StopSheath(figure);
                PlaceWeapon(figure, atHip: figure.Pelvis != null && figure.HasStow);
                return;
            }

            if (change != SheathChange.None)
            {
                bool draw = change == SheathChange.Draw;
                // Already where it is going — a sheathe called before a draw reached the hilt —
                // and there is nothing to animate: it simply stays.
                bool already = draw ? !figure.WeaponAtHip : figure.WeaponAtHip;
                CombatClipEntry? clip = fight.HasSheathLayer && !already ? SheathClipFor(draw, feminine) : null;
                if (clip != null && clip.clip != null)
                {
                    figure.SheathAction = change;
                    figure.SheathClip = clip;
                    figure.SheathSeconds = 0f;
                    PutSheathClip(figure, clip);
                }
                else StopSheath(figure);   // no clip: snap, below
            }

            // A blow, a reaction or a held state takes the whole body: the draw or sheathe is done at once.
            if (figure.SheathAction != SheathChange.None && fight.Action != CombatRole.None) StopSheath(figure);
            // So does a shot mid-draw (design 47 §4b): the gun is never seen firing from the hip.
            if (figure.SheathAction == SheathChange.Draw && figure.FiredThisFrame) StopSheath(figure);

            CombatClipEntry? playing = figure.SheathClip;
            AnimationClip? animation = playing != null ? playing.clip : null;
            if (figure.SheathAction == SheathChange.None || playing == null || animation == null)
            {
                PlaceWeapon(figure, atHip: !figure.Sheath.Out);
                return;
            }

            bool drawing = figure.SheathAction == SheathChange.Draw;
            float length = animation.length;
            figure.SheathSeconds += deltaTime;
            float t = figure.SheathSeconds;
            if (t >= length)
            {
                StopSheath(figure);
                PlaceWeapon(figure, atHip: !figure.Sheath.Out);
                return;
            }

            // In the hand from the grasp on, while drawing; at the hip from the release on, while sheathing.
            bool past = t >= SheathMoment(playing, drawing);
            PlaceWeapon(figure, atHip: drawing ? !past : past);

            fight.SheathSlot.SetTime(t);
            float ease = CombatEaseSeconds > 1e-4f ? Mathf.Min(1f, Mathf.Min(t, length - t) / CombatEaseSeconds) : 1f;
            fight.Layer.SetInputWeight(SheathLayerInput, Mathf.Clamp01(ease) * (1f - fight.Weight));
        }

        static void StopSheath(Figure figure)
        {
            figure.SheathAction = SheathChange.None;
            figure.SheathClip = null;
            figure.SheathSeconds = 0f;
            if (figure.Fight.HasSheathLayer) figure.Fight.Layer.SetInputWeight(SheathLayerInput, 0f);
        }
    }
}
