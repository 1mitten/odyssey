#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.CameraRig
{
    /// <summary>
    /// Riding along with a colonist (design 56): the rig's half. What is decided — who, how far
    /// back, where the player has looked, when it ends — is <see cref="RideDirector"/>'s and is
    /// tested without a camera; where the camera stands for that is <see cref="RideCamera"/>'s,
    /// likewise. This half reads the mouse and the time keys, puts the colony view's drawing
    /// aside and gives it back, and stands the camera.
    ///
    /// <para><b>The orbit is frozen, not moved.</b> A ride writes the camera's transform and
    /// nothing else: the focus, yaw, pitch and distance the colony view keeps are left exactly as
    /// they were, so a save taken mid-ride records the colony view (<c>ViewStateSection</c> reads
    /// those fields), and leaving is one instant re-apply rather than a remembered pose.</para>
    ///
    /// <para><b>Stood in <c>LateUpdate</c>, by the composition root, before anything reads the
    /// camera</b> (<see cref="PlaceRide"/>). The rig's own <c>Update</c> runs before the figures
    /// are posed, so a camera placed there would chase last frame's figure; and the frustum, the
    /// viewer position and the sight lines are all taken at the top of <c>LateUpdate</c>, so a
    /// camera placed after the figures would be culled against the frame before. Placing it first
    /// in <c>LateUpdate</c> from the figure's last drawn feet costs one frame of lag on a target
    /// that is itself smoothed over several, and keeps every reader on the camera that renders.</para>
    /// </summary>
    public sealed partial class SliceCameraRig
    {
        /// <summary>Degrees of view turn per pixel of mouse travel while riding.</summary>
        public const float RideLookDegreesPerPixel = 0.12f;

        /// <summary>How quickly the view swings round behind her when she turns, per second, over the shoulder.</summary>
        const float RideShoulderYawRate = 4f;

        /// <summary>The same at the eyes, where the view is hers and must turn with her head.</summary>
        const float RideEyeYawRate = 12f;

        /// <summary>How quickly the camera rides a change of height — a terrace, a hop, a drop — per second.</summary>
        const float RideHeightRate = 10f;

        /// <summary>How quickly the eye height follows her head, per second: slow enough to take out the walk's bob.</summary>
        const float RideEyeLiftRate = 3f;

        /// <summary>How quickly the arm follows the wheel, per second, and eases back out once a wall has let it go.</summary>
        const float RideArmRate = 6f;

        /// <summary>Below this far above a cell's floor a point is under the slab of the cell above, if there is one.</summary>
        const float RideCeilingClearance = 0.2f;

        /// <summary>Whether the rig is riding: the director's answer, as the rig has realised it.</summary>
        public bool Riding => _riding;

        /// <summary>The way the ride is looking, before the player's own turn, in degrees.</summary>
        public float RideYaw => _rideYaw;

        /// <summary>
        /// How far the camera stood from her eyes on the last frame it was placed, in metres
        /// (<see cref="RidePose.FromEyes"/>). The composition root takes her head away below
        /// <see cref="HeadClearMetres"/>, because a camera inside it sees the ink hull as solid
        /// black (design 56 §4).
        /// </summary>
        public float RideFromEyes { get; private set; } = float.MaxValue;

        /// <summary>Nearer her eyes than this, her head is in the way of the view.</summary>
        public const float HeadClearMetres = 0.6f;

        /// <summary>
        /// What the view is looking at: the colony view's focus, or while riding the colonist's
        /// feet. For the things that follow the view — the grass clearance, the ambience, the rain,
        /// the birds and the butterflies — rather than <see cref="Focus"/>, which is the colony
        /// view's own and is what a save records.
        /// </summary>
        public Vector3 ViewFocus => _riding && _rideHasPose ? _rideFeet : _focus;

        /// <summary>
        /// How far the camera stands from what it looks at, in metres: the zoom, or while riding the
        /// arm, never under two metres so nothing that scales with the view shrinks to nothing.
        /// </summary>
        public float ViewDistance => _riding ? Mathf.Max(_rideShown, 2f) : _targetDistance;

        bool _riding;
        bool _rideHasPose;
        int _rideSerial = -1;
        Vector3 _rideFeet;
        float _rideYaw;
        float _rideEyeLift = RideCamera.DefaultEyeLift;
        float _rideArm;
        float _rideShown;
        Func<float, float, float, bool>? _rideBlocked;

        // The colony view's drawing, put aside for the ride and given back after it.
        bool _savedFollowDepth;
        AboveMode _savedAbove;
        BelowMode _savedBelow;
        bool _savedCeiling;
        float _savedFieldOfView;
        float _savedNearClip;
        CursorLockMode _savedLock;
        bool _savedCursorVisible;

        /// <summary>
        /// Start or stop riding when the director has, and say whether the rig is riding this frame.
        /// Called at the top of <c>Update</c>, so the frame a ride begins is the first frame that
        /// reads no colony-view input.
        /// </summary>
        bool SyncRide()
        {
            bool wanted = _directors != null && _directors.Ride.Riding;
            if (wanted && !_riding) EnterRide();
            else if (!wanted && _riding) LeaveRide();
            return _riding;
        }

        void EnterRide()
        {
            _riding = true;
            _rideHasPose = false;
            _rideSerial = -1;
            RideFromEyes = float.MaxValue;

            // Whatever the pointer was in the middle of is abandoned, and anything drawn for it is
            // told to go: a ride begun from a button leaves no half-drawn box or hover behind.
            _dragStart = null;
            _boxActive = false;
            _orbiting = false;
            _rightPress.Abandon();
            _glideTarget = null;
            _glidingTo = null;
            Latch(PointerOutcome.HoverLost, _pointerNow, _pointerNow);
            ResolvePointer();
            _hasPointerCell = false;

            // The building as it is (design 56 §4): the colony view cuts rooms open to be read from
            // above, and from beside her that is a house with no ceiling and a ghost for the rock
            // over a mine. Walls down is the composition root's to hold off, since it writes it
            // every frame.
            _savedFollowDepth = slice.followDepth;
            _savedAbove = slice.above;
            _savedBelow = slice.below;
            _savedCeiling = slice.suppressActiveCeiling;
            slice.followDepth = false;
            slice.above = AboveMode.Full;
            slice.below = BelowMode.Normal;
            slice.suppressActiveCeiling = false;

            UnityEngine.Camera camera = Camera;
            _savedFieldOfView = camera.fieldOfView;
            _savedNearClip = camera.nearClipPlane;
            camera.fieldOfView = RideCamera.FieldOfView;
            camera.nearClipPlane = RideCamera.NearClip;

            // The mouse looks round, and there is nothing on screen to point at.
            _savedLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LeaveRide()
        {
            RestoreFromRide();

            // No jump in the first colony-view frame: the pointer comes back where it is, not where
            // it was when the ride began.
            Mouse? mouse = Mouse.current;
            if (mouse != null) _lastPointer = mouse.position.ReadValue();
            ApplyTransform(instant: true);
        }

        /// <summary>Give back everything a ride put aside. Safe to call when no ride is running.</summary>
        void RestoreFromRide()
        {
            if (!_riding) return;
            _riding = false;
            _rideHasPose = false;
            RideFromEyes = float.MaxValue;

            slice.followDepth = _savedFollowDepth;
            slice.above = _savedAbove;
            slice.below = _savedBelow;
            slice.suppressActiveCeiling = _savedCeiling;

            UnityEngine.Camera camera = Camera;
            camera.fieldOfView = _savedFieldOfView;
            camera.nearClipPlane = _savedNearClip;

            Cursor.lockState = _savedLock;
            Cursor.visible = _savedCursorVisible;
        }

        /// <summary>A pointer locked by a ride must not outlive the rig, in the editor above all.</summary>
        void OnDisable() => RestoreFromRide();

        /// <summary>
        /// The keys a ride keeps — the four time keys — and the mouse: its travel turns the view and
        /// its wheel moves the arm a stop at a time. Escape is the settings presenter's, where every
        /// meaning of that key is decided in one order.
        /// </summary>
        void ReadRide()
        {
            RideDirector ride = _directors!.Ride;
            HotkeyDirector hotkeys = _directors.Hotkeys;

            // Not GameKeysLive: the ride is what holds the game's keys, and these are the ones it
            // hands back. The two reasons a key belongs elsewhere still apply.
            Keyboard? keys = Keyboard.current;
            if (keys != null && hotkeys.Listening == null && !hotkeys.Typing)
            {
                if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Pause)) RequestGameSpeed(0);
                if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Speed1)) RequestGameSpeed(1);
                if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Speed2)) RequestGameSpeed(2);
                if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Speed3)) RequestGameSpeed(3);
            }

            Mouse? mouse = Mouse.current;
            if (mouse == null) return;

            // Mouse up looks up, which is a smaller pitch: Unity's positive pitch looks down.
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0f)
                ride.Look(delta.x * RideLookDegreesPerPixel, -delta.y * RideLookDegreesPerPixel);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) ride.Wheel(scroll > 0f ? 1 : -1);

            _lastPointer = mouse.position.ReadValue();
        }

        /// <summary>
        /// Stand the camera for this frame. Called by the composition root at the top of
        /// <c>LateUpdate</c>, before the frustum, the viewer position or the sight lines are taken.
        ///
        /// <para><paramref name="known"/> is false while the colonist has no position to give —
        /// before her figure exists, or once she has gone — and then the camera holds where it is,
        /// which is the "holds on where she was" of design 56 §5.</para>
        /// </summary>
        /// <param name="feet">Where she is drawn standing.</param>
        /// <param name="facingYaw">Which way she is drawn facing, in degrees.</param>
        /// <param name="eyeLift">How far her eyes are above her feet, in metres.</param>
        /// <param name="realSeconds">The frame's unscaled time: a paused game still turns its camera.</param>
        public void PlaceRide(bool known, Vector3 feet, float facingYaw, float eyeLift, float realSeconds)
        {
            if (!_riding || _directors == null || _model == null) return;
            RideDirector ride = _directors.Ride;
            float dt = Mathf.Max(realSeconds, 0f);

            if (known && !ride.Lost)
            {
                if (!_rideHasPose || _rideSerial != ride.Serial)
                {
                    // The first frame of a ride is a cut, not a glide: a camera swooping down from
                    // eighty metres is the colony view animating, not the ride beginning.
                    _rideFeet = feet;
                    _rideYaw = facingYaw;
                    _rideEyeLift = eyeLift;
                    _rideArm = ride.Arm;
                    _rideShown = ride.Arm;
                    _rideSerial = ride.Serial;
                    _rideHasPose = true;
                }
                else
                {
                    float turn = ride.AtEyes ? RideEyeYawRate : RideShoulderYawRate;
                    _rideYaw = Mathf.LerpAngle(_rideYaw, facingYaw, 1f - Mathf.Exp(-turn * dt));

                    // Across the ground exactly, so she stays where she is in the frame; up and down
                    // eased, so a hop or a terrace step is a lift rather than a jolt.
                    _rideFeet.x = feet.x;
                    _rideFeet.z = feet.z;
                    _rideFeet.y = Mathf.Lerp(_rideFeet.y, feet.y, 1f - Mathf.Exp(-RideHeightRate * dt));
                    _rideEyeLift = Mathf.Lerp(_rideEyeLift, eyeLift, 1f - Mathf.Exp(-RideEyeLiftRate * dt));
                }
            }

            if (!_rideHasPose) return;

            _rideArm = Mathf.Lerp(_rideArm, ride.Arm, 1f - Mathf.Exp(-RideArmRate * dt));

            var frame = new RideFrame(_rideFeet.x, _rideFeet.y, _rideFeet.z, _rideEyeLift,
                _rideYaw + ride.LookYaw, ride.LookPitch, _rideArm);
            _rideBlocked ??= RideBlocks;
            RidePose pose = RideCamera.Solve(frame, _rideBlocked);

            // **In at once, out gently.** A wall arriving behind her must pull the camera in on the
            // frame it arrives or the lens is inside it; the same wall going must not fling the
            // camera back out, which reads as a jolt. So the arm shown takes any shortening now and
            // any lengthening at the arm's own rate.
            _rideShown = pose.Arm < _rideShown
                ? pose.Arm
                : Mathf.Lerp(_rideShown, pose.Arm, 1f - Mathf.Exp(-RideArmRate * dt));

            var rotation = Quaternion.Euler(pose.Pitch, pose.Yaw, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 pivot = new Vector3(pose.X, pose.Y, pose.Z) + forward * pose.Arm;
            Vector3 position = pivot - forward * _rideShown;
            transform.SetPositionAndRotation(position, rotation);

            // Measured on what is shown, so the head is taken away while the arm is still easing
            // out from against her, not only once it has arrived.
            RideFromEyes = pose.FromEyes - (pose.Arm - _rideShown);
        }

        /// <summary>
        /// Whether a point is inside something the camera must not enter: a solid cell — rock,
        /// wall, window, pillar — or the slab of the storey above, close under it.
        ///
        /// <para><b>A door is not solid here</b>, although the picker counts it: she walks through
        /// doors, and a camera that treated the doorway she stands in as rock would collapse into
        /// her head on every threshold. Trees and bushes are not solid either; the sight lines fade
        /// them instead (design 56 §4).</para>
        /// </summary>
        bool RideBlocks(float x, float y, float z)
        {
            WorldRenderModel? model = _model;
            if (model == null) return false;
            var size = model.Size;

            int cx = Mathf.FloorToInt(x / CellMetrics.SizeXZ);
            int cz = Mathf.FloorToInt(z / CellMetrics.SizeXZ);
            int cy = Mathf.FloorToInt(y / CellMetrics.SizeY);
            if (!size.Contains(cx, cz, cy)) return false;

            int index = size.Index(cx, cz, cy);
            if (model.OccludesFace(index)) return true;

            float above = y - cy * CellMetrics.SizeY;
            if (above > CellMetrics.SizeY - RideCeilingClearance && size.Contains(cx, cz, cy + 1))
            {
                int up = size.Index(cx, cz, cy + 1);
                if (model.Floor(up) != 0 || model.OccludesFace(up)) return true;
            }
            return false;
        }
    }
}
