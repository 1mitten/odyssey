#nullable enable
using System;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
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
    /// Picking goes through <see cref="SlicePicker"/>, which returns the nearest cell on any
    /// layer the slice draws solid — the whole stack above the surface, the active layer alone
    /// when a layer above it is x-rayed. What is non-negotiable is the second half: a ghosted
    /// layer is a depth cue and is never a pointer target.
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

        /// <summary>
        /// What the settings panel's camera-speed rung multiplies every camera
        /// <em>translation</em> by: keyboard pan, drag pan and wheel zoom. 1 is the tuned
        /// speed — the fields above, exactly as authored — and the rungs the panel offers
        /// scale on top of it, beside <see cref="DistanceScale"/> and shift's
        /// <see cref="fastMultiplier"/> rather than replacing either. Orbit is left alone for
        /// the same reason shift leaves it alone: it is a direct mouse-delta mapping, and a
        /// scaled mouse delta is not a slower orbit, only a numb one.
        /// </summary>
        [HideInInspector] public float speedScale = 1f;

        /// <summary>
        /// What holding shift multiplies every camera <em>translation</em> by.
        ///
        /// <para>The board is 300 m across and the camera pans at a speed chosen for looking at
        /// one colony, so crossing it takes a while. Shift is the usual answer and costs nothing:
        /// it scales on top of <see cref="DistanceScale"/> rather than replacing it, so a fast pan
        /// zoomed out is still faster than a fast pan zoomed in, which is what makes both feel
        /// like the same control.</para>
        ///
        /// <para><b>Translation only — pan, zoom and the slice.</b> Orbiting is deliberately left
        /// alone: it is already a direct mouse-delta mapping, and three times a mouse delta is not
        /// a fast orbit but an uncontrollable one.</para>
        ///
        /// <para>Below one it becomes a precision modifier instead, which is a legitimate thing to
        /// want and costs nothing to allow; the field is clamped only against zero and absurdity.</para>
        /// </summary>
        [Tooltip("Hold shift to multiply pan, zoom and slice stepping by this. Below 1 makes shift a precision modifier.")]
        public float fastMultiplier = 3f;

        /// <summary>
        /// How many layers one slice step covers with shift held.
        ///
        /// A storey at a time is right for reading a building and slow for getting from the
        /// surface to the bottom of a sixteen-layer map, which is the same complaint the pan speed
        /// answers and deserves the same key.
        /// </summary>
        [Tooltip("Layers per slice step with shift held.")]
        public int fastLayerStep = 4;

        /// <summary>
        /// Degrees a second Q and E turn the camera while held.
        ///
        /// <para><b>Free rotation, not a 90-degree snap</b> (owner, 2026-09-16: "change Q and E to
        /// snap rotate — to allow a free rotation completely for now as I think that would make it
        /// easier"). Q and E used to add or subtract ninety degrees on the frame they were
        /// pressed, which is the genre's convention and assumes a board that reads the same from
        /// four sides. This one does not: it is layered, the slice is cut at an angle, and a wall
        /// or an outcrop hides different things at fifty degrees than at ninety. Held keys let the
        /// player stop wherever the view is actually clearest.</para>
        ///
        /// <para>Ninety a second is one quarter turn a second at full speed, which is about as
        /// fast as the eye can follow a rotating board; shift multiplies it like every other
        /// camera speed, so a full turn takes a third of a second when you already know where you
        /// are going.</para>
        /// </summary>
        [Tooltip("Degrees a second Q and E turn the camera while held. Shift multiplies it.")]
        public float rotateSpeed = 90f;

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

        /// <summary>
        /// Where the zoom is heading, as against <see cref="distance"/>, which is where it has
        /// smoothed to so far. A test that asks "did the wheel do anything" wants this: the
        /// smoothed value approaches its target exponentially and never quite arrives, so it
        /// cannot distinguish a small input from none without waiting an arbitrary number of
        /// frames.
        /// </summary>
        public float TargetDistance => _targetDistance;

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

        /// <summary>
        /// Raised when a left drag that became a box is released, with the screen rect and whether
        /// shift was held. A press that never crossed the drag threshold raises <see cref="Picked"/>
        /// instead, so the box and the click cannot both fire off one press.
        /// </summary>
        public event Action<Rect, bool>? BoxSelected;

        /// <summary>
        /// The screen rect of the box being dragged, or none. Polled by the HUD each frame to draw
        /// the marquee, because a marquee is a picture, not a decision.
        /// </summary>
        ///
        /// <para><b>None while a tool is armed.</b> The release already decides that a press belongs
        /// to the tool rather than to selection, so the selection marquee was drawing a box over a
        /// gesture it would never receive — two rectangles on one drag, one of them a lie about
        /// what was going to happen. Reported by the owner as the build tool conflicting with
        /// drag-and-drop (2026-09-17).</para>
        ///
        /// <para>Asked here rather than in the HUD because this is where the same question is
        /// already asked on release: one answer, one place, and the picture cannot promise
        /// something the release will not do.</para>
        public Rect? DragBox =>
            _boxActive && _dragStart.HasValue && !(WorldToolArmed != null && WorldToolArmed())
                ? RectFromTo(_dragStart.Value, _draggedTo)
                : (Rect?)null;

        /// <summary>
        /// Set by the interface: true while a designate tool is armed, and then the world gesture
        /// belongs to designation rather than to selection.
        ///
        /// <para><b>Somebody has to own a press, and until now nobody did.</b> `BoxSelected` and
        /// `Picked` are events, so designation could simply subscribe — and then a drag with the
        /// mine tool armed would mark the rock <i>and</i> box-select every colonist under it. Two
        /// consumers of one gesture with no arbiter is the bug, not the merge.</para>
        ///
        /// <para>This is deliberately the same shape as <see cref="PointerOverInterface"/>: a
        /// predicate the rig consults, owned by whoever knows the answer, rather than the rig
        /// growing an opinion about tools. It is the cheap half of design 09 §6's
        /// <c>InputRouter</c> — one claim, checked once, at the moment the gesture completes. The
        /// capture stack and the eight enumerated cases are still that router's to build; this
        /// settles only the one case that is in the way today.</para>
        /// </summary>
        public Func<bool>? WorldToolArmed { get; set; }

        /// <summary>
        /// A completed world gesture that belongs to a tool: the cell the drag started on and the
        /// cell it ended on, both on the active layer.
        ///
        /// <para>Cells rather than a screen rect, because only the rig can turn a screen point
        /// into a cell — it holds the render mirror and the picker. A click with no travel raises
        /// this too, with both cells the same, which is what makes "click to mark one" and "drag
        /// to mark many" one gesture rather than two.</para>
        /// </summary>
        public event Action<CellRef, CellRef>? ToolDrag;

        /// <summary>
        /// The box as it is being drawn, every frame the button is held with a tool armed.
        ///
        /// <para><b>Without this a tool gives no feedback at all until the button comes up.</b>
        /// <see cref="ToolDrag"/> fires on release and nothing else ever did, so a player dragging
        /// a wall across a meadow saw the board exactly as it was before they pressed — reported by
        /// the owner as "nothing appears" even though the order was placed correctly on release
        /// (2026-09-17). <c>DesignateDirector.TryPreview</c> was written for this and had never
        /// been called by anything.</para>
        ///
        /// <para>Raised with the anchor and the current head, the same pair <see cref="ToolDrag"/>
        /// carries, so the preview and the order that follows it cannot disagree about the box.
        /// Raised before the threshold is crossed as well: a press that has not travelled yet is a
        /// one-cell box, which is exactly what a click places.</para>
        /// </summary>
        public event Action<CellRef, CellRef>? ToolDragging;

        /// <summary>The drag ended without placing anything — the tool was not armed, or it was a pick.</summary>
        public event Action? ToolDragCancelled;

        /// <summary>
        /// A right-click that never became an orbit. Whoever is holding a tool puts it down.
        ///
        /// <para><b>It means one thing and must go on meaning one thing.</b> Escape unwinds — tool,
        /// then top panel, then the menu — and that order lives in exactly one place
        /// (<c>SettingsDirector.Escape</c>) because two components reading one key once disarmed a
        /// tool and opened a panel on the same keystroke. Right-click is not a second Escape: it
        /// disarms and does nothing else, and with nothing armed it is deliberately inert, because
        /// that is the gesture the forced-order context menu is reserved for
        /// (<c>docs/design/15-building.md</c> §8).</para>
        /// </summary>
        public event Action? WorldRightClicked;

        /// <summary>Whether the right button is being swept or merely pressed. See <see cref="WorldRightClicked"/>.</summary>
        PressGesture _rightPress;

        /// <summary>A press must travel this many pixels before it counts as a box and not a click.</summary>
        const float DragThresholdPixels = 6f;

        Vector2? _dragStart;
        Vector2 _draggedTo;
        bool _boxActive;

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
            if (keys == null || _directors == null) return;

            // Actions, not keys: which key each action is on is the player's to change in the
            // settings panel, and this is the only thing the rebind needs to hold true — the
            // same line reads W and whatever W was changed to. While a slot in that panel is
            // waiting for a key, every press belongs to the rebind, so the pollers sit the
            // frame out rather than arming a tool with the key the player is offering it.
            HotkeyDirector hotkeys = _directors.Hotkeys;
            if (hotkeys.Listening != null) return;

            var move = Vector2.zero;
            if (keys.IsPressed(hotkeys, HotkeyAction.CameraForward)) move.y += 1f;
            if (keys.IsPressed(hotkeys, HotkeyAction.CameraBack)) move.y -= 1f;
            if (keys.IsPressed(hotkeys, HotkeyAction.CameraRight)) move.x += 1f;
            if (keys.IsPressed(hotkeys, HotkeyAction.CameraLeft)) move.x -= 1f;
            if (move.sqrMagnitude > 0f)
                Pan(move.normalized * (panSpeed * speedScale * dt * DistanceScale * Boost));

            // Held, not tapped: see rotateSpeed. The target is driven rather than the yaw itself,
            // so the same smoothing that carries a mouse orbit carries this, and the two cannot
            // fight each other over who owns the angle.
            float turn = 0f;
            if (keys.IsPressed(hotkeys, HotkeyAction.CameraTurnLeft)) turn -= 1f;
            if (keys.IsPressed(hotkeys, HotkeyAction.CameraTurnRight)) turn += 1f;
            if (turn != 0f) _targetYaw += turn * rotateSpeed * dt * Boost;

            // Shift covers several storeys at once, for the same reason it covers more ground: a
            // layer at a time is right for reading a building and slow for getting from the
            // surface to the floor of a sixteen-layer map.
            int layers = Fast ? Mathf.Max(1, fastLayerStep) : 1;
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.SliceUp)) _directors.Slice.Step(layers);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.SliceDown)) _directors.Slice.Step(-layers);

            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Pause)) RequestGameSpeed(0);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Speed1)) RequestGameSpeed(1);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Speed2)) RequestGameSpeed(2);
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.Speed3)) RequestGameSpeed(3);

            // The above-slice cycle and its shift-modified below-slice half are one question —
            // what do I see above me, and what do I see below me — so they share a key. B was
            // the below-mode cycle until 2026-09-16, when the command bar wanted it for Build;
            // a view-debug key gives way to a player-facing command, and pairing the two
            // cycles is tidier than the letter it replaced.
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.CycleAbove))
            {
                if (Fast) slice.below = (BelowMode)(((int)slice.below + 1) % 3);
                else CycleAboveMode();
            }
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.FrameMap)) Frame();

            // The developer overlay sits on the picture, so it is off until asked for.
            if (keys.WasPressedThisFrame(hotkeys, HotkeyAction.DeveloperOverlay))
                _directors.Overlays.ToggleDeveloper();
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
                    _targetDistance - Mathf.Sign(scroll) * zoomSpeed * speedScale * DistanceScale * Boost,
                    minDistance, maxDistance);
            Vector2 delta = pointer - _lastPointer;
            _lastPointer = pointer;

            // The right button is an orbit if it travels and a click if it does not — the same
            // split the left button has always made between a box and a pick, applied to the other
            // button. The arithmetic is <see cref="PressGesture"/>'s rather than this file's,
            // because nothing decided inside a MonoBehaviour can be tested: the PlayMode harness
            // still cannot deliver a synthetic mouse. A press that began over a panel is abandoned
            // rather than tracked, per input case 2 of design 09 §6.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (overInterface) _rightPress.Abandon();
                else _rightPress.Press(pointer.x, pointer.y);
            }

            if (_rightPress.Down && mouse.rightButton.isPressed) _rightPress.MoveTo(pointer.x, pointer.y);
            if (mouse.rightButton.wasReleasedThisFrame && _rightPress.Release())
                WorldRightClicked?.Invoke();

            if (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                _orbiting = mouse.rightButton.isPressed;

            if (mouse.rightButton.isPressed)
            {
                _targetYaw += delta.x * 0.25f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.15f, 20f, 80f);
            }
            else if (mouse.middleButton.isPressed)
            {
                Pan(new Vector2(-delta.x, -delta.y) * (0.02f * speedScale * DistanceScale * Boost));
            }
            else
            {
                _orbiting = false;
            }

            // A left press on the world is a click until it travels: past the threshold it
            // becomes a box, and on release the box is completed against the world even if the
            // pointer ends over a panel (input case 1 of design 09 §6) — a drag begun on the
            // world belongs to the world. The press itself was gated on the interface, so a
            // drag begun on a panel never starts (case 2).
            if (mouse.leftButton.wasPressedThisFrame && !_orbiting && !overInterface)
            {
                _dragStart = pointer;
                _draggedTo = pointer;
                _boxActive = false;
            }
            if (_dragStart.HasValue && mouse.leftButton.isPressed)
            {
                _draggedTo = pointer;
                if (!_boxActive && (pointer - _dragStart.Value).sqrMagnitude
                    >= DragThresholdPixels * DragThresholdPixels)
                    _boxActive = true;

                // Show the box while it is being drawn. Asked every frame rather than latched at
                // the press, so arming a tool part way through a drag lights the preview up
                // immediately — which is the same question the release below asks, and it must be
                // asked the same way or the preview and the order disagree.
                if (WorldToolArmed != null && WorldToolArmed()
                    && CellAt(_dragStart.Value, out CellRef dragAnchor)
                    && CellAt(_draggedTo, out CellRef dragHead))
                    ToolDragging?.Invoke(dragAnchor, dragHead);
                else
                    ToolDragCancelled?.Invoke();
            }
            else if (_dragStart.HasValue)
            {
                bool shift = Keyboard.current?.shiftKey.isPressed == true;
                Vector2 start = _dragStart.Value;
                bool wasBox = _boxActive;
                _dragStart = null;
                _boxActive = false;
                // Who owns this press. Asked once, on release, so arming a tool mid-drag cannot
                // turn a half-drawn selection box into an order.
                if (WorldToolArmed != null && WorldToolArmed())
                {
                    // **A press that travelled is a drag; one that did not is a click.** They are
                    // two different gestures and the owner asked for both (2026-09-17): hold and
                    // drag finishes on release, and a plain click anchors a run that the next
                    // click finishes. The rig cannot tell them apart any later than this, because
                    // `_boxActive` is the only record that the pointer ever moved.
                    if (!CellAt(start, out CellRef anchor) || !CellAt(_draggedTo, out CellRef head))
                        ToolDragCancelled?.Invoke();
                    else if (wasBox) ToolDrag?.Invoke(anchor, head);
                    else ToolClick?.Invoke(head);
                }
                else if (wasBox)
                {
                    ToolDragCancelled?.Invoke();
                    BoxSelected?.Invoke(RectFromTo(start, _draggedTo), shift);
                }
                else
                {
                    ToolDragCancelled?.Invoke();
                    PickAt(_draggedTo);
                }
            }
            else if (WorldToolArmed != null && WorldToolArmed() && !_orbiting && !overInterface
                     && CellAt(pointer, out CellRef hovered))
            {
                ToolHover?.Invoke(hovered);
            }
            else
            {
                ToolHoverLost?.Invoke();
            }
        }

        /// <summary>
        /// The cell under the pointer while a build tool is armed and no button is down.
        ///
        /// <para><b>There was no such thing until 2026-09-17, and its absence was the whole of the
        /// owner's report</b> that they could not tell where a wall or floor would land: with a tool
        /// armed and the button up, nothing was drawn anywhere. A player found out where a thing
        /// went by placing it. See `19-build-cursor.md`.</para>
        ///
        /// <para>Gated exactly as a press is — not orbiting, not over the interface — so that
        /// swinging the camera round with a tool still in hand does not trail a ghost across the
        /// board, and so a pointer over a panel shows nothing in the world beneath it. The last
        /// branch is deliberate: whenever any of that stops being true the cursor is told to go
        /// away, rather than being left behind wherever it last was.</para>
        /// </summary>
        public event Action<CellRef>? ToolHover;

        /// <summary>Nothing is under the pointer, or nothing should be: put the cursor away.</summary>
        public event Action? ToolHoverLost;

        /// <summary>
        /// A left press on the world that never travelled, with a tool armed.
        ///
        /// <para>Distinct from <see cref="ToolDrag"/> because the two gestures now mean different
        /// things: a drag is a run drawn with the button held and finished by letting go, and a
        /// click anchors a run that a second click finishes (owner, 2026-09-17). Both produce the
        /// same box in the end, which is why the difference stops here and the director owns the
        /// rest.</para>
        /// </summary>
        public event Action<CellRef>? ToolClick;

        /// <summary>
        /// The one place a screen position becomes a ray. Shared so that a tool drag and a
        /// selection click cannot resolve the same pixel to different cells.
        /// </summary>
        Ray RayAt(Vector2 screenPosition) =>
            GetComponent<UnityEngine.Camera>()
                .ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));

        /// <summary>
        /// The highest layer a click may land on, and the lowest. The picker's band, published so
        /// that the colonist hit-test and the drag box cull against the same bounds the world does
        /// — "selectable" and "drawn solid" must not be allowed to drift apart.
        /// </summary>
        public int HighestSelectableLayer =>
            _model == null ? ActiveLayer : slice.HighestSelectableLayer(ActiveLayer, _model.Size.SizeY);

        /// <inheritdoc cref="HighestSelectableLayer"/>
        public int LowestSelectableLayer =>
            _model == null ? ActiveLayer : slice.LowestSelectableLayer(ActiveLayer, _model.LowestOutdoorLayer);

        /// <summary>The cell under a screen point on a drawn layer, or false when the ray misses.</summary>
        bool CellAt(Vector2 screenPosition, out CellRef cell)
        {
            cell = default;
            if (_model == null) return false;
            return SlicePicker.Pick(RayAt(screenPosition), _model, ActiveLayer, slice, out cell);
        }

        static Rect RectFromTo(Vector2 a, Vector2 b) => Rect.MinMaxRect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        float DistanceScale => Mathf.Clamp(_targetDistance / 40f, 0.35f, 3f);

        /// <summary>Is a shift key down? Read live, so nothing has to be threaded between the input passes.</summary>
        static bool Fast
        {
            get
            {
                Keyboard? keys = Keyboard.current;
                return keys != null && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            }
        }

        /// <summary>
        /// What to multiply a camera translation by this frame. One unless shift is held.
        ///
        /// Clamped against zero and absurdity and nothing else: a value below one is a precision
        /// modifier rather than a mistake. See <see cref="fastMultiplier"/>.
        /// </summary>
        float Boost => Fast ? Mathf.Clamp(fastMultiplier, 0.05f, 20f) : 1f;

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
        /// Put the camera back exactly where it was, live values and smoothing targets together
        /// (U38).
        ///
        /// <para><b>Why this is not four assignments at the call site.</b> <c>yaw</c> and
        /// <c>distance</c> are *lerped* toward <see cref="_targetYaw"/> and
        /// <see cref="_targetDistance"/> on every frame, and those targets are private. Writing the
        /// public fields alone looks right for exactly one frame and then swings back to wherever
        /// the rig was already heading — which is worse than not restoring at all, because it reads
        /// as the camera being shoved rather than as the restore not working.</para>
        ///
        /// <para>It is also why the focus is taken as a world position rather than as a cell:
        /// <see cref="FocusOn"/> snaps to a cell centre, and half a cell is 1.25 m of drift every
        /// time a colony is saved and loaded. A restored view has to be the view that was saved, or
        /// it is a slow leak that nobody can reproduce.</para>
        ///
        /// <para>The focus is clamped to the board the way ordinary panning is, so a pose saved
        /// against a larger map cannot put the camera outside this one.</para>
        /// </summary>
        public void RestorePose(Vector3 focus, float yawDegrees, float pitchDegrees, float distanceMetres)
        {
            _focus = focus;
            ClampFocus();

            yaw = _targetYaw = Mathf.Repeat(yawDegrees, 360f);
            pitch = Mathf.Clamp(pitchDegrees, 20f, 80f);
            distance = _targetDistance = Mathf.Clamp(distanceMetres, minDistance, maxDistance);
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
            Ray ray = RayAt(screenPosition);
            if (SlicePicker.Pick(ray, _model, ActiveLayer, slice, out CellRef cell)) Picked?.Invoke(cell, ray);
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

            // Both ways of turning the camera add to the target rather than setting it, so it
            // wanders away from zero for as long as the session lasts — a mouse orbit always did,
            // and a held Q or E does it ninety degrees a second. Wrapped here, at the one place
            // that reads it, because LerpAngle takes the short way round regardless and the only
            // thing an unbounded angle costs is float precision, eventually.
            _targetYaw = Mathf.Repeat(_targetYaw, 360f);
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
