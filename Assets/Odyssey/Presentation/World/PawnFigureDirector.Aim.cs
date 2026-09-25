#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <see cref="PawnFigureDirector"/>: the gun (design 47 §4a, §4b). How a pistol sits in the fist,
    /// the two-handed aim, the recoil, the slide, and low ready — computed, in the pose pass after the
    /// graph, because no pack has a gun clip (Battle Royale ships none; <c>e-10</c>).
    ///
    /// <para><b>The gun is the truth.</b> While aiming, the gun is put on the line from the shoulders
    /// to the target at <see cref="GunReachOfArm"/> of the rig's own arm length, barrel along the
    /// line, slide up; the right hand is solved to its grip and the left cups under it. Hands chase
    /// the gun, never the other way round, so the hands stay on it when it pitches or kicks
    /// (<c>d-22</c> finding 20). The prop is placed absolutely every frame (<c>Tools.cs</c>'s rule)
    /// and put back to its fitted place in the fist before anything else looks at it, because
    /// <see cref="PlaceWeapon"/> re-seats it only when it changes parent.</para>
    ///
    /// <para><b>Aiming</b> is the simulation's own statement: working on <c>Job_AttackRanged</c>, which
    /// publishes its target's cell as the work cell while it aims and while it stands engaged between
    /// shots. <b>Low ready</b> is the gun drawn and in the hand with nothing to shoot at. Both ease,
    /// so neither snaps; both stop when the world does.</para>
    ///
    /// <para><b>Per-frame cost scales with the live figures</b>, capped at 64: for each figure aiming,
    /// one pass over the figures to find the one it is aiming at and two arm solves.</para>
    /// </summary>
    public sealed partial class PawnFigureDirector
    {
        /// <summary>How far out the gun is held, as a fraction of the rig's own arm length (<c>d-22</c>). INVENTED.</summary>
        public const float GunReachOfArm = 0.85f;

        /// <summary>How long the aim takes to settle, at least — the aim is seen to settle before the shot. INVENTED.</summary>
        public const float AimEaseSeconds = 0.18f;

        /// <summary>How long low ready takes to come and go.</summary>
        public const float LowReadyEaseSeconds = 0.25f;

        /// <summary>How long the aim point takes to catch a walking target (a critically damped follow).</summary>
        public const float AimFollowSeconds = 0.12f;

        /// <summary>The most the spine turns to aim, either way, before the pawn itself must turn (<c>d-22</c>).</summary>
        public const float AimYawLimit = 60f;

        /// <summary>The most the spine pitches to aim, up or down; beyond it the head and the arms take the rest.</summary>
        public const float AimPitchLimit = 45f;

        /// <summary>The recoil's whole length: the spring is derived from this one number (<c>d-22</c> finding 13). INVENTED.</summary>
        public const float RecoilSeconds = 0.25f;

        /// <summary>The recoil's kick at the muzzle: pitch, and back towards the shooter.</summary>
        public const float RecoilPitchDegrees = 6f, RecoilBackMetres = 0.04f;

        /// <summary>How much of the kick the chest takes, which is what reads from far off.</summary>
        public const float RecoilChestShare = 0.25f;

        /// <summary>How far the slide runs back on a shot, and for how long.</summary>
        public const float SlideTravelMetres = 0.02f, SlideSeconds = 0.06f;

        /// <summary>Low ready: the upper arm forward and the forearm raised, degrees — the barrel forward and down.</summary>
        public const float LowReadyShoulder = 18f, LowReadyElbow = 42f;

        /// <summary>Where a chest is, as a fraction of a figure's height, when no figure stands in the target's cell.</summary>
        public const float ChestOfHeight = 0.72f;

        /// <summary>Figures aiming this frame. For tests.</summary>
        public int AimingFigures { get; private set; }

        /// <summary>The recoil's kick at <paramref name="seconds"/> after the shot: 1 at the shot, easing to nought with no overshoot.</summary>
        public static float RecoilKick(float seconds)
        {
            if (seconds < 0f || seconds >= RecoilSeconds) return 0f;
            // A critically damped spring released from 1 with no speed: (1 + wt) e^(-wt), with w
            // chosen so it is within 2 % of rest at RecoilSeconds.
            float w = 5.83f / RecoilSeconds;
            float wt = w * seconds;
            return (1f + wt) * Mathf.Exp(-wt);
        }

        /// <summary>The slide's run: back and home again inside <see cref="SlideSeconds"/>.</summary>
        public static float SlideRun(float seconds) =>
            seconds < 0f || seconds >= SlideSeconds ? 0f : Mathf.Sin(Mathf.PI * seconds / SlideSeconds);

        /// <summary>Is this item def a gun, by the style the content gives it?</summary>
        bool IsGunDef(int def) =>
            def >= 0 && def < WeaponStyles.Length && WeaponStyles[def] == AttackStyle.Pistol;

        /// <summary>
        /// Seat a pistol in the right fist (design 47 §4a). <c>FitWeapon</c>'s blade rule would seat the
        /// barrel along the forearm with the palm on the slide; a pistol wants the palm on the grip.
        /// Every Synty gun measured — Battle Royale's and Sci-Fi City's alike — pivots at the grip with
        /// the barrel along its local +Z and the grip down its local −Y, so those are read as the
        /// axes, and the palm goes to the middle of the grip read off the bounds: two fifths of the
        /// way down it, a tenth of the way back.
        /// </summary>
        void FitPistol(Figure figure, Transform prop)
        {
            Transform? hand = figure.RightHand;
            prop.localPosition = Vector3.zero;
            prop.localRotation = Quaternion.identity;
            if (hand == null || !LocalBounds(prop, out Bounds bounds)) return;

            figure.GunGrip = new Vector3(0f, bounds.min.y * 0.4f, bounds.min.z * 0.1f);

            Vector3 outOfTheFist = figure.RightLowerArm != null ? hand.position - figure.RightLowerArm.position : hand.forward;
            if (outOfTheFist.sqrMagnitude < 1e-6f) return;
            outOfTheFist.Normalize();

            // Barrel out of the fist, slide towards the figure's front: the grip then runs across
            // the palm, which is how a hand holds one.
            Vector3 top = Vector3.ProjectOnPlane(figure.Transform.forward, outOfTheFist);
            if (top.sqrMagnitude < 1e-6f) top = Vector3.ProjectOnPlane(figure.Transform.up, outOfTheFist);
            prop.rotation = Quaternion.LookRotation(outOfTheFist, top.normalized);
            prop.position += HandGrip.Palm(figure.RightGrip) - prop.TransformPoint(figure.GunGrip);

            // The slide, if the prop has one by that name (Battle Royale's does).
            figure.GunSlide = null;
            foreach (Transform child in prop.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child == prop || child.name.IndexOf("Slide", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                figure.GunSlide = child;
                figure.GunSlideRest = child.localPosition;
                break;
            }
        }

        /// <summary>
        /// The frame's state for the gun, once per figure from <see cref="Pose"/>: whether it is
        /// aiming and at what, low ready, the shot's clock. Eased on the figure's own clock, which
        /// stops when the world does.
        /// </summary>
        void PoseGun(Figure figure, in PawnView pawn, float deltaTime)
        {
            bool inHand = figure.IsGun && figure.Weapon != null && figure.Weapon.activeSelf && !figure.WeaponAtHip;
            bool aiming = inHand && pawn.Working && pawn.JobDef == JobHandle.AttackRanged && !pawn.IsDowned;
            bool ready = inHand && !aiming && pawn.IsWeaponDrawn && !pawn.IsDowned;

            if (figure.FireClock >= 0f) figure.FireClock += deltaTime;

            float aimStep = AimEaseSeconds > 1e-4f ? deltaTime / AimEaseSeconds : 1f;
            // A shot snaps the aim home: the gun is never seen firing from the hip (design 47 §4b).
            figure.AimWeight = figure.FiredThisFrame && aiming ? 1f : Mathf.MoveTowards(figure.AimWeight, aiming ? 1f : 0f, aimStep);
            float readyStep = LowReadyEaseSeconds > 1e-4f ? deltaTime / LowReadyEaseSeconds : 1f;
            figure.LowReadyWeight = Mathf.MoveTowards(figure.LowReadyWeight, ready ? 1f : 0f, readyStep);

            if (!aiming || pawn.WorkCell.Y < 0)
            {
                if (figure.AimWeight <= 0f) figure.HasAimPoint = false;
                return;
            }

            Vector3 want = AimTarget(figure, pawn.WorkCell);
            if (!figure.HasAimPoint)
            {
                figure.AimPoint = want;
                figure.AimVelocity = Vector3.zero;
                figure.HasAimPoint = true;
            }
            else if (deltaTime > 0f)
                figure.AimPoint = Vector3.SmoothDamp(figure.AimPoint, want, ref figure.AimVelocity, AimFollowSeconds,
                    Mathf.Infinity, deltaTime);
        }

        /// <summary>
        /// Where to aim at a target cell: the chest of the figure standing on it or stepping out of it,
        /// else the cell's floor plus a chest's height. Three-dimensional, so a shooter above pitches
        /// down and one below pitches up.
        /// </summary>
        Vector3 AimTarget(Figure self, CellRef cell)
        {
            if (_frame != null)
            {
                for (int i = 0; i < _figures.Count; i++)
                {
                    Figure other = _figures[i];
                    if (other == self || other.Pawn < 0 || other.Chest == null) continue;
                    if (!_frame.TryGetPawn(new PawnId(other.Pawn), out PawnView view)) continue;
                    if (view.Cell == cell || view.NextCell == cell) return other.Chest.position;
                }
            }
            return CellMetrics.FloorCentre(cell.X, cell.Z, cell.Y) + Vector3.up * (FigureBuild.FallbackHeight * ChestOfHeight);
        }

        /// <summary>
        /// Put a gun back where it sits in the fist, before any pass moves it this frame: the aim
        /// places it absolutely, and <see cref="PlaceWeapon"/> would not undo that on its own.
        /// </summary>
        static void ReseatGun(Figure figure)
        {
            if (!figure.IsGun || figure.Weapon == null || figure.WeaponAtHip) return;
            Transform prop = figure.Weapon.transform;
            prop.localPosition = figure.WeaponHandPosition;
            prop.localRotation = figure.WeaponHandRotation;
            if (figure.GunSlide != null) figure.GunSlide.localPosition = figure.GunSlideRest;
        }

        /// <summary>
        /// The aim (design 47 §4b): the chest turned and pitched to the target within its limits, the
        /// gun on the aim line at <see cref="GunReachOfArm"/> of the arm, the recoil on it, the right
        /// hand on its grip and the left cupping under, all eased by <see cref="Figure.AimWeight"/>
        /// from where the fist had it.
        /// </summary>
        void ApplyAimPose(Figure figure)
        {
            if (figure.Weapon == null || figure.RightUpperArm == null || figure.RightLowerArm == null
                || figure.RightHand == null || !figure.HasAimPoint)
                return;
            AimingFigures++;
            // This pass runs at the end of both Sync and Evaluate and must derive the same pose from
            // the same state each time, so the gun starts from its place in the fist here too.
            ReseatGun(figure);

            float w = Mathf.Clamp01(figure.AimWeight);
            float kick = RecoilKick(figure.FireClock);
            Transform body = figure.Transform;
            Vector3 up = body.up;
            Vector3 right = body.right;

            // The chest: yaw, then pitch, shared between the two spine bones, the recoil's share on top.
            Vector3 shoulders = ShoulderMid(figure);
            Vector3 toTarget = figure.AimPoint - shoulders;
            if (toTarget.sqrMagnitude < 1e-4f) return;
            Vector3 flat = Vector3.ProjectOnPlane(toTarget, up);
            float yaw = flat.sqrMagnitude > 1e-6f
                ? Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(body.forward, up), flat, up), -AimYawLimit, AimYawLimit)
                : 0f;
            // Positive about the figure's right pitches forward and down (Unity's convention, and
            // ApplyCombatPose's), so a target below — whose line turns down from the level — is a
            // positive pitch.
            Vector3 level = flat.sqrMagnitude > 1e-6f ? flat.normalized : body.forward;
            float pitch = Mathf.Clamp(Vector3.SignedAngle(level, toTarget, Vector3.Cross(up, level)),
                -AimPitchLimit, AimPitchLimit);
            float chestKick = RecoilPitchDegrees * RecoilChestShare * kick;
            Pitch(figure.Spine, up, yaw * 0.45f * w);
            Pitch(figure.Chest, up, yaw * 0.55f * w);
            Pitch(figure.Spine, right, pitch * 0.4f * w);
            Pitch(figure.Chest, right, (pitch * 0.6f - chestKick) * w);

            // The gun: on the line from the shoulders as they now are, out a fraction of the arm.
            shoulders = ShoulderMid(figure);
            Vector3 aim = (figure.AimPoint - shoulders).normalized;
            float arm = Vector3.Distance(figure.RightUpperArm.position, figure.RightLowerArm.position)
                        + Vector3.Distance(figure.RightLowerArm.position, figure.RightHand.position);
            Vector3 gripAt = shoulders + aim * (arm * GunReachOfArm) - up * 0.05f;

            Vector3 top = Vector3.ProjectOnPlane(up, aim);
            if (top.sqrMagnitude < 1e-6f) top = Vector3.ProjectOnPlane(body.forward, aim);
            Quaternion aimed = Quaternion.LookRotation(aim, top.normalized);
            // The kick: the muzzle up about the grip and the whole gun back towards her.
            Vector3 gunRight = aimed * Vector3.right;
            aimed = Quaternion.AngleAxis(-RecoilPitchDegrees * kick, gunRight) * aimed;
            gripAt -= aim * (RecoilBackMetres * kick);

            // Eased from where the fist had it.
            Transform prop = figure.Weapon.transform;
            Vector3 heldGrip = prop.TransformPoint(figure.GunGrip);
            Quaternion held = prop.rotation;
            Vector3 grip = Vector3.Lerp(heldGrip, gripAt, w);
            Quaternion rotation = Quaternion.Slerp(held, aimed, w);

            // The hands chase the gun: the right palm to the grip, the left cupping under it.
            Vector3 gripDown = rotation * Vector3.down;
            Vector3 gripAlong = rotation * Vector3.forward;
            var strong = new GripArm(figure.RightUpperArm, figure.RightLowerArm, figure.RightGrip,
                new Vector3(0.12f, -0.15f, 0f));
            Grasp.One(strong, Hold.Bar(grip + gripDown * 0.04f, grip - gripDown * 0.04f, 0.035f), 0.5f, right, up, w);
            if (figure.LeftUpperArm != null && figure.LeftLowerArm != null)
            {
                Vector3 cup = grip + gripDown * 0.05f - gripAlong * 0.01f - right * 0.035f;
                var support = new GripArm(figure.LeftUpperArm, figure.LeftLowerArm, figure.LeftGrip,
                    new Vector3(-0.12f, -0.15f, 0f));
                Grasp.One(support, Hold.Bar(cup - gripAlong * 0.04f, cup + gripAlong * 0.04f, 0.04f), 0.5f, right, up, w);
            }

            // Last, so no bone moved after it carries it off: the gun where the aim put it.
            prop.rotation = rotation;
            prop.position += grip - prop.TransformPoint(figure.GunGrip);

            if (figure.GunSlide != null)
                figure.GunSlide.localPosition = figure.GunSlideRest - Vector3.forward * (SlideTravelMetres * SlideRun(figure.FireClock));
        }

        /// <summary>Low ready: the forearm raised and forward, so the gun points ahead and down — armed, not shooting.</summary>
        void ApplyLowReady(Figure figure)
        {
            if (figure.RightUpperArm == null) return;
            float w = Mathf.Clamp01(figure.LowReadyWeight);
            Vector3 right = figure.Transform.right;
            Pitch(figure.RightUpperArm, right, -LowReadyShoulder * w);
            Pitch(figure.RightLowerArm, right, -LowReadyElbow * w);
        }

        /// <summary>Half way between the two shoulders, or the chest when a rig has none.</summary>
        static Vector3 ShoulderMid(Figure figure)
        {
            if (figure.RightUpperArm != null && figure.LeftUpperArm != null)
                return (figure.RightUpperArm.position + figure.LeftUpperArm.position) * 0.5f;
            if (figure.Chest != null) return figure.Chest.position;
            return figure.Transform.position + figure.Transform.up * 1.4f;
        }
    }
}
