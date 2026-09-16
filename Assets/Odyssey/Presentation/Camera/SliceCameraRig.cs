#nullable enable
using System;
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
    /// Selection goes through <see cref="SlicePicker"/>, which cannot return a cell above the
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
        public Color selectionColour = new Color(0.25f, 0.85f, 0.95f, 0.35f);

        WorldRenderModel? _model;
        ChunkRenderer? _renderer;
        Vector3 _focus;
        float _targetYaw;
        float _targetDistance;
        bool _orbiting;
        Vector2 _lastPointer;

        public int ActiveLayer { get; private set; }

        /// <summary>The cell the player last clicked, or none. Never above the active layer.</summary>
        public CellRef? Selection { get; private set; }

        /// <summary>Raised when the player changes layer, so the world can be told through an intent.</summary>
        public event Action<int>? ActiveLayerChanged;

        /// <summary>Raised when the player asks for a speed: 0 paused, 1 normal, 2 fast, 3 very fast.</summary>
        public event Action<int>? GameSpeedRequested;

        /// <summary>
        /// Raised the instant the selection changes — a pick that hit, a pick that missed, or a
        /// layer change clearing it.
        ///
        /// An event and not a value to poll, because polling had a race in it. The readout used to
        /// read <see cref="Selection"/> in its own <c>Update</c>, and Unity does not order two
        /// components' Updates: on the click frame it could run first, see last frame's cell, and
        /// resolve the wrong colonist or item — so the cursor drew one tier, then snapped to the
        /// right one a frame later. A handler runs inside the pick, so by the time anything draws
        /// the answer is already known.
        /// </summary>
        public event Action<CellRef?, Ray>? SelectionChanged;

        /// <param name="ray">The pick ray, so a listener can hit-test things that stand *in* a
        /// cell rather than settle for the cell. Meaningless when <paramref name="cell"/> is null.</param>
        void SetSelection(CellRef? cell, Ray ray)
        {
            bool same = cell.HasValue == Selection.HasValue
                        && (!cell.HasValue || cell.Value == Selection!.Value);
            Selection = cell;
            // Re-clicking the same cell still announces: the thing standing in it may have moved.
            if (!same || cell.HasValue) SelectionChanged?.Invoke(cell, ray);
        }

        public void Bind(WorldRenderModel model, ChunkRenderer renderer, int startLayer)
        {
            _model = model;
            _renderer = renderer;
            ActiveLayer = Mathf.Clamp(startLayer, 0, model.Size.SizeY - 1);
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
            if (_model == null) return;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

            ReadKeyboard(dt);
            ReadMouse(dt);
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

            if (keys.pageUpKey.wasPressedThisFrame || keys.rKey.wasPressedThisFrame) ChangeLayer(1);
            if (keys.pageDownKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame) ChangeLayer(-1);

            if (keys.spaceKey.wasPressedThisFrame) GameSpeedRequested?.Invoke(0);
            if (keys.digit1Key.wasPressedThisFrame) GameSpeedRequested?.Invoke(1);
            if (keys.digit2Key.wasPressedThisFrame) GameSpeedRequested?.Invoke(2);
            if (keys.digit3Key.wasPressedThisFrame) GameSpeedRequested?.Invoke(3);

            if (keys.vKey.wasPressedThisFrame)
                slice.above = (AboveMode)(((int)slice.above + 1) % 6);
            if (keys.bKey.wasPressedThisFrame)
                slice.below = (BelowMode)(((int)slice.below + 1) % 3);
            if (keys.homeKey.wasPressedThisFrame) Frame();
        }

        void ReadMouse(float dt)
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _targetDistance = Mathf.Clamp(
                    _targetDistance - Mathf.Sign(scroll) * zoomSpeed * DistanceScale,
                    minDistance, maxDistance);

            Vector2 pointer = mouse.position.ReadValue();
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

            if (mouse.leftButton.wasPressedThisFrame && !_orbiting) PickAt(pointer);
        }

        float DistanceScale => Mathf.Clamp(_targetDistance / 40f, 0.35f, 3f);

        void Pan(Vector2 amount)
        {
            Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = flat * Vector3.forward;
            Vector3 right = flat * Vector3.right;
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

        void ChangeLayer(int delta)
        {
            if (_model == null) return;
            int next = Mathf.Clamp(ActiveLayer + delta, 0, _model.Size.SizeY - 1);
            if (next == ActiveLayer) return;
            ActiveLayer = next;
            SetSelection(null, default);
            ActiveLayerChanged?.Invoke(ActiveLayer);
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

        // -------------------------------------------------------- selection

        void PickAt(Vector2 screenPosition)
        {
            if (_model == null) return;
            var camera = GetComponent<UnityEngine.Camera>();
            Ray ray = camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            if (SlicePicker.Pick(ray, _model, ActiveLayer, out CellRef cell)) SetSelection(cell, ray);
            else SetSelection(null, ray);
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
            if (_renderer == null || Selection == null || SuppressCellCursor) return;
            _renderer.DrawCellHighlight(Selection.Value, selectionColour);
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

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
