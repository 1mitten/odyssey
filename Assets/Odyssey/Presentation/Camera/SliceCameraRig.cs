#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.CameraRig
{
    /// <summary>
    /// The three-quarter orbit camera and the slice control, per
    /// <c>06-rendering-and-camera.md</c> section 3.
    ///
    /// The vertical control changes the **active layer**, not the camera height: moving up a floor
    /// in a layered colony sim is a change of what you are working on, not a change of vantage.
    /// The camera follows the layer so the focus stays at eye level for whatever is being built.
    ///
    /// The rig decides nothing about layer or selection. It reads the keys and the mouse, hands
    /// a layer request to the <see cref="SliceDirector"/> and a pick to whoever listens to
    /// <see cref="Picked"/>, glides to wherever the <see cref="CameraDirector"/> has been asked to
    /// go, and draws the cell cursor for the <see cref="SelectionDirector"/>. Those three are
    /// Unity-free and tested without it; this class is the one that needs a camera.
    ///
    /// Picking goes through <see cref="SlicePicker"/>, which cannot return a cell above the
    /// active layer. That is the non-negotiable part of this class.
    ///
    /// Input goes through the Input System package, because the project is configured for it
    /// (<c>activeInputHandler: 1</c>) and the legacy <c>Input</c> class throws outright under that
    /// setting.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class SliceCameraRig : MonoBehaviour
    {
        [Header("Slice")]
        public SliceSettings slice = new SliceSettings();

        [Header("Camera")]
        [Range(20f, 80f)] public float pitch = 48f;
        public float yaw = 45f;
        public float distance = 48f;
        public float minDistance = 10f;
        public float maxDistance = 160f;
        public float panSpeed = 26f;
        public float zoomSpeed = 6f;
        public float smoothing = 12f;

        [Header("Selection")]
        public Color selectionColour = Color.white;

        WorldRenderModel? _model;
        ChunkRenderer? _renderer;
        HudDirectors? _directors;
        Vector3 _focus;
        Vector3? _glideTarget;
        CellRef? _glidingTo;
        float _targetYaw;
        float _targetDistance;
        bool _orbiting;
        Vector2 _lastPointer;

        /// <summary>The slice director's layer. The rig realises it; it does not own it.</summary>
        public int ActiveLayer => _directors?.Slice.ActiveLayer ?? 0;

        /// <summary>Where the camera is looking, in world metres. For tests and readouts.</summary>
        public Vector3 Focus => _focus;

        /// <summary>Where a glide is taking the focus, or null when the camera is where it was asked to be.</summary>
        public Vector3? GlideTarget => _glideTarget;

        /// <summary>Raised when the player asks for a speed: 0 paused, 1 normal, 2 fast, 3 very fast.</summary>
        public event Action<int>? GameSpeedRequested;

        /// <summary>
        /// Set by the interface: returns true when a screen position sits over a HUD region.
        /// The rig consults it before picking, because the rig reads the mouse directly and a
        /// UI Toolkit panel does not stop it — without this gate a click on a panel also picks
        /// the world behind it, which is input case 2 of design 09 §6 and the classic bug of
        /// the genre. Orbit and pan are deliberately not gated: a drag begun on the world
        /// completes against the world even if the pointer crosses a panel on the way.
        /// </summary>
        public Func<Vector2, bool>? PointerOverInterface { get; set; }

        /// <summary>
        /// Ask for a game speed from anywhere the keyboard cannot reach — the HUD's speed
        /// buttons, later a menu. Raises the same event the keys do, so the composition root's
        /// paused-clock handling stays in one place.
        /// </summary>
        public void RequestGameSpeed(int speed) => GameSpeedRequested?.Invoke(speed);

        /// <summary>Move the slice: the keys' path, and anything else that has a layer in mind.</summary>
        public void SetLayer(int layer) => _directors?.Slice.SetLayer(layer);

        /// <summary>
        /// Raised the instant a world pick lands, with the cell (or none) and the ray, so the
        /// presenter can hit-test the colonists that stand *in* a cell rather than settle for the
        /// cell. An event and not a value to poll, because polling had a race in it: two
        /// components' Updates are unordered, and on the click frame the listener could see last
        /// frame's pick. A handler runs inside the pick, so the answer exists before anything draws.
        /// </summary>
        public event Action<CellRef?, Ray>? Picked;

        public void Bind(WorldRenderModel model, ChunkRenderer renderer, HudDirectors directors)
        {
            _model = model;
            _renderer = renderer;
            _directors = directors;
            _targetYaw = yaw;
            _targetDistance = distance;
            _focus = new Vector3(
                model.Size.SizeX * CellMetrics.SizeXZ * 0.5f,
                ActiveLayer * CellMetrics.SizeY,
                model.Size.SizeZ * CellMetrics.SizeXZ * 0.5f);
            ApplyTransform(instant: true);
        }

        void Update()
        {
            if (_model == null || _directors == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

            ReadKeyboard(dt);
            ReadMouse(dt);
            TakeJumpRequest();
            ApplyTransform(instant: false);
            DrawSelection();
        }

        // ------------------------------------------------------------- input

        void ReadKeyboard(float dt)
        {
            Keyboard? keys = Keyboard.current;
            if (keys == null) return;

            var move = Vector2.zero;
            if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move.y += 1f;
            if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move.y -= 1f;
            if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) move.x += 1f;
            if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) move.x -= 1f;
            if (move.sqrMagnitude > 0f) Pan(move.normalized * (panSpeed * dt * DistanceScale));

            if (keys.qKey.wasPressedThisFrame) _targetYaw -= 90f;
            if (keys.eKey.wasPressedThisFrame) _targetYaw += 90f;

            if (keys.pageUpKey.wasPressedThisFrame || keys.rKey.wasPressedThisFrame) _directors!.Slice.Step(1);
            if (keys.pageDownKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame) _directors!.Slice.Step(-1);

            if (keys.spaceKey.wasPressedThisFrame) RequestGameSpeed(0);
            if (keys.digit1Key.wasPressedThisFrame) RequestGameSpeed(1);
            if (keys.digit2Key.wasPressedThisFrame) RequestGameSpeed(2);
            if (keys.digit3Key.wasPressedThisFrame) RequestGameSpeed(3);

            if (keys.vKey.wasPressedThisFrame) CycleAboveMode();
            if (keys.bKey.wasPressedThisFrame)
                slice.below = (BelowMode)(((int)slice.below + 1) % 3);
            if (keys.homeKey.wasPressedThisFrame) Frame();

            // The developer overlay sits on the picture, so it is off until asked for.
            if (keys.backquoteKey.wasPressedThisFrame) _directors!.Overlays.ToggleDeveloper();
        }

        /// <summary>
        /// Step through the seven states the V key offers: the depth-following default, then each
        /// of ADR 0006's six modes, then back.
        ///
        /// <para><b>An explicit choice wins over the default.</b> With <c>followDepth</c> on the
        /// mode is derived from where the slice sits, so setting the field would have done nothing
        /// at all and the key would simply have looked broken. The first press therefore pins
        /// whatever is currently on screen — which is why it copies the resolved mode across before
        /// switching the default off — and the picture does not jump on the press that only means
        /// "let me drive".</para>
        /// </summary>
        void CycleAboveMode()
        {
            if (slice.followDepth)
            {
                slice.above = slice.AboveAt(ActiveLayer);
                slice.followDepth = false;
                return;
            }

            int next = (int)slice.above + 1;
            if (next >= 6) slice.followDepth = true;
            else slice.above = (AboveMode)next;
        }

        void ReadMouse(float dt)
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pointer = mouse.position.ReadValue();
            bool overInterface = PointerOverInterface != null && PointerOverInterface(pointer);

            // Case 8 of design 09 section 6: scroll over a panel scrolls the panel, scroll over
            // the world zooms the camera. Until this guard the wheel did both at once — a scroll
            // down the ledger hauled the camera in behind it.
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !overInterface)
                _targetDistance = Mathf.Clamp(
                    _targetDistance - Mathf.Sign(scroll) * zoomSpeed * DistanceScale,
                    minDistance, maxDistance);
            Vector2 delta = pointer - _lastPointer;
            _lastPointer = pointer;

            if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                _orbiting = mouse.rightButton.isPressed;

            if (mouse.rightButton.isPressed)
            {
                _targetYaw += delta.x * 0.25f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.15f, 20f, 80f);
            }
            else if (mouse.middleButton.isPressed)
            {
                Pan(new Vector2(-delta.x, -delta.y) * (0.02f * DistanceScale));
            }
            else
            {
                _orbiting = false;
            }

            if (mouse.leftButton.wasPressedThisFrame && !_orbiting && !overInterface)
                PickAt(pointer);
        }

        float DistanceScale => Mathf.Clamp(_targetDistance / 40f, 0.35f, 3f);

        void Pan(Vector2 amount)
        {
            Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = flat * Vector3.forward;
            Vector3 right = flat * Vector3.right;
            // A pan is the player taking the camera back: whatever a jump was heading for, it stops.
            _directors?.Camera.Cancel();
            _glideTarget = null;
            _glidingTo = null;
            _focus += forward * amount.y + right * amount.x;
            ClampFocus();
        }

        void ClampFocus()
        {
            if (_model == null) return;
            var size = _model.Size;
            _focus.x = Mathf.Clamp(_focus.x, 0f, size.SizeX * CellMetrics.SizeXZ);
            _focus.z = Mathf.Clamp(_focus.z, 0f, size.SizeZ * CellMetrics.SizeXZ);
        }

        /// <summary>Pull back to see the whole map at the current layer.</summary>
        public void Frame()
        {
            if (_model == null) return;
            var size = _model.Size;
            _focus = new Vector3(
                size.SizeX * CellMetrics.SizeXZ * 0.5f,
                ActiveLayer * CellMetrics.SizeY,
                size.SizeZ * CellMetrics.SizeXZ * 0.5f);
            _targetDistance = Mathf.Clamp(
                Mathf.Max(size.SizeX, size.SizeZ) * CellMetrics.SizeXZ * 0.9f, minDistance, maxDistance);
        }

        /// <summary>
        /// Open looking at one place rather than at the whole map.
        ///
        /// Framing the entire map is the wrong opening shot for a colony sim: on a 60-cell map the
        /// camera sits 135 units back, and a colonist is 0.9 m wide, so the people you are meant to
        /// be watching are a few pixels of tan against brown ground. Reported as "I can't even see
        /// one", and it was not a bug in the simulation — they were all there, drawn, and far too
        /// small to notice.
        /// </summary>
        public void FocusOn(CellRef cell, float distance = 32f)
        {
            _focus = CellMetrics.FloorCentre(cell);
            _targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <summary>
        /// Realise the camera director's jump: a glide to the cell at the current zoom, on the
        /// smoothing the rest of the camera uses, because a cut would lose the player their
        /// bearings where a glide keeps them. The director is told when the rig has landed.
        /// </summary>
        void TakeJumpRequest()
        {
            CellRef? wanted = _directors!.Camera.JumpTarget;
            if (!wanted.HasValue)
            {
                _glideTarget = null;
                _glidingTo = null;
                return;
            }
            if (_glidingTo.HasValue && _glidingTo.Value == wanted.Value) return;

            Vector3 target = CellMetrics.FloorCentre(wanted.Value);
            target.y = _focus.y;
            _glideTarget = target;
            _glidingTo = wanted;
        }

        // -------------------------------------------------------- selection

        void PickAt(Vector2 screenPosition)
        {
            if (_model == null) return;
            var camera = GetComponent<UnityEngine.Camera>();
            Ray ray = camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            if (SlicePicker.Pick(ray, _model, ActiveLayer, out CellRef cell)) Picked?.Invoke(cell, ray);
            else Picked?.Invoke(null, ray);
        }

        /// <summary>
        /// The cell cursor — drawn only when nothing more specific has claimed the selection.
        ///
        /// A colonist's cursor is drawn by the composition root instead, because it has to hug the
        /// figure and glide with it between cells, and that needs the published snapshot. The rig
        /// has a model and a renderer and deliberately no access to the simulation at all.
        /// </summary>
        void DrawSelection()
        {
            if (_renderer == null || SuppressCellCursor) return;
            CellRef? cell = _directors!.Selection.Cell;
            if (cell == null) return;
            _renderer.DrawCellHighlight(cell.Value, selectionColour);
        }

        /// <summary>
        /// Set each frame by whoever is drawing a better cursor, and cleared when they stop.
        ///
        /// A flag rather than an ordering rule, because the rig draws in its own Update and the
        /// composition root in LateUpdate: whichever way round they ran, one of them would have to
        /// know about the other. This way neither does.
        /// </summary>
        public bool SuppressCellCursor { get; set; }

        // -------------------------------------------------------- transform

        void ApplyTransform(bool instant)
        {
            float k = instant ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            yaw = Mathf.LerpAngle(yaw, _targetYaw, k);
            distance = Mathf.Lerp(distance, _targetDistance, k);

            float targetY = ActiveLayer * CellMetrics.SizeY;
            _focus.y = instant ? targetY : Mathf.Lerp(_focus.y, targetY, k);

            if (_glideTarget.HasValue)
            {
                Vector3 target = _glideTarget.Value;
                _focus.x = instant ? target.x : Mathf.Lerp(_focus.x, target.x, k);
                _focus.z = instant ? target.z : Mathf.Lerp(_focus.z, target.z, k);
                if (instant || (Mathf.Abs(_focus.x - target.x) < 0.02f && Mathf.Abs(_focus.z - target.z) < 0.02f))
                {
                    _focus.x = target.x;
                    _focus.z = target.z;
                    _glideTarget = null;
                    _glidingTo = null;
                    _directors?.Camera.Arrived();
                }
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
