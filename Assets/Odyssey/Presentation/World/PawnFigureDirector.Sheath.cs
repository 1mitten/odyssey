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
    /// <para><b>The sheath is measured, not authored.</b> At bind, in the idle, the stow point is
    /// found on the drawn mesh: at the height of the left hip joint, the outermost vertex on the
    /// figure's left that belongs to the thigh or the pelvis rather than to the hanging arm
    /// (<see cref="HipReach"/>). It is kept in the pelvis's own space — the parent of the left thigh,
    /// because <c>HumanBodyBones.Hips</c> on the Synty avatar is <c>Root</c>, on the floor — so it
    /// rides the pelvis and not the swinging leg. Each weapon is then fitted to it once, off its own
    /// mesh: the haft hanging down and back, the flat of the blade against the thigh, the point a
    /// quarter of the way up from the butt at the hip, and the whole weapon stood off the body by
    /// its own half-thickness.</para>
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

        /// <summary>How far the blade leans back from straight down, in degrees. INVENTED.</summary>
        public const float SheathTiltDegrees = 25f;

        /// <summary>How far the tip splays out from the leg, in degrees. INVENTED.</summary>
        public const float SheathSplayDegrees = 6f;

        /// <summary>The point of a weapon that hangs at the belt, as a fraction of its length from the butt. INVENTED.</summary>
        public const float SheathHangFraction = 0.25f;

        /// <summary>The gap between the body and a sheathed weapon, as a fraction of the figure's height. INVENTED.</summary>
        public const float SheathClearance = 0.005f;

        /// <summary>How far forward of the hip joint the sheath hangs, as a fraction of the figure's height. INVENTED.</summary>
        public const float SheathForward = 0.02f;

        /// <summary>
        /// The band of heights round the hip joint that the mesh is searched in for the hip's
        /// surface, as a fraction of the figure's height. INVENTED.
        /// </summary>
        public const float HipBand = 0.03f;

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
        /// height of the hip joint, a touch forward of it, with the blade leaning back and splayed
        /// out. The surface is the drawn mesh's; with nothing bakeable the hip joint's own offset,
        /// doubled, stands in.
        /// </summary>
        void MeasureHipStow(Figure figure)
        {
            Transform root = figure.Transform;
            Transform pelvis = figure.Pelvis!;
            Transform thigh = figure.LeftUpperLeg!;
            float height = figure.StandingHeight > 0.01f ? figure.StandingHeight : FigureBuild.FallbackHeight;

            Vector3 up = root.up;
            Vector3 forward = root.forward;
            Vector3 outward = -root.right;
            // The avatar's left thigh says which side is left; a rig mirrored from ours is believed.
            if (Vector3.Dot(thigh.position - root.position, outward) < 0f) outward = -outward;

            float hipY = thigh.position.y;
            float reach = HipReach(figure, root.position, outward, hipY, HipBand * height);
            if (reach <= 0f) reach = 2f * Mathf.Max(0.01f, Vector3.Dot(thigh.position - root.position, outward));

            float along = Vector3.Dot(thigh.position - root.position, forward) + SheathForward * height;
            Vector3 point = new Vector3(root.position.x, hipY, root.position.z) + outward * reach + forward * along;

            float tilt = SheathTiltDegrees * Mathf.Deg2Rad;
            Vector3 down = (-up * Mathf.Cos(tilt) - forward * Mathf.Sin(tilt)).normalized;
            down = (down + outward * Mathf.Tan(SheathSplayDegrees * Mathf.Deg2Rad)).normalized;

            figure.StowPoint = pelvis.InverseTransformPoint(point);
            figure.StowDown = pelvis.InverseTransformDirection(down);
            figure.StowOut = pelvis.InverseTransformDirection(outward);
            figure.StowForward = pelvis.InverseTransformDirection(forward);
            figure.HasStow = true;
        }

        /// <summary>
        /// How far out on <paramref name="outward"/> the drawn body reaches at the height
        /// <paramref name="y"/> (± <paramref name="band"/>), from <paramref name="centre"/>, counting
        /// only vertices nearer the left thigh or the pelvis than the left arm — at the hip, the
        /// hanging hand is further out than the hip is, and a sheath hung outside it hangs in the air.
        /// Nought when nothing bakeable is there.
        ///
        /// <para>Baked, not bounded, for <see cref="FigureBuild"/>'s reasons; which bone a vertex
        /// "belongs to" is the nearest bone segment rather than its skin weights, which a mesh not
        /// marked readable does not hand out in a player.</para>
        /// </summary>
        static float HipReach(Figure figure, Vector3 centre, Vector3 outward, float y, float band)
        {
            Transform? thigh = figure.LeftUpperLeg, knee = figure.LeftLowerLeg, pelvis = figure.Pelvis;
            if (thigh == null || knee == null || pelvis == null) return 0f;
            Vector3 spine = figure.Spine != null ? figure.Spine.position : pelvis.position + figure.Transform.up * 0.2f;

            Vector3 shoulder = figure.LeftUpperArm != null ? figure.LeftUpperArm.position : spine;
            Vector3 elbow = figure.LeftLowerArm != null ? figure.LeftLowerArm.position : shoulder;
            Vector3 wrist = figure.LeftHand != null ? figure.LeftHand.position : elbow;
            Vector3 fingers = wrist + (wrist - elbow) * 0.6f;

            float best = 0f;
            Mesh? baked = null;
            SkinnedMeshRenderer[] skins = figure.Skins;
            for (int s = 0; s < skins.Length; s++)
            {
                SkinnedMeshRenderer skin = skins[s];
                if (skin == null || !skin.enabled || skin.sharedMesh == null) continue;
                baked ??= new Mesh { name = "Odyssey/HipProbe" };
                skin.BakeMesh(baked, useScale: true);
                Vector3[] vertices = baked.vertices;
                Transform at = skin.transform;
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 p = at.TransformPoint(vertices[v]);
                    if (Mathf.Abs(p.y - y) > band) continue;
                    float body = Mathf.Min(SegmentDistanceSq(p, thigh.position, knee.position),
                        SegmentDistanceSq(p, pelvis.position, spine));
                    float arm = Mathf.Min(SegmentDistanceSq(p, shoulder, elbow),
                        Mathf.Min(SegmentDistanceSq(p, elbow, wrist), SegmentDistanceSq(p, wrist, fingers)));
                    if (arm < body) continue;
                    float reach = Vector3.Dot(p - centre, outward);
                    if (reach > best) best = reach;
                }
            }
            if (baked != null) UnityEngine.Object.DestroyImmediate(baked);
            return best;
        }

        static float SegmentDistanceSq(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq) : 0f;
            return (a + ab * t - p).sqrMagnitude;
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
            FitWeapon(figure, prop);
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
        /// butt at the stow point, stood off the body by the weapon's own half-thickness.
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

            Vector3 butt = bounds.center - haft * half;
            Vector3 hang = butt + haft * (2f * half * SheathHangFraction);
            Vector3 target = point + outward * (standOff + SheathClearance * height);
            prop.position += target - prop.TransformPoint(hang);
        }

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
