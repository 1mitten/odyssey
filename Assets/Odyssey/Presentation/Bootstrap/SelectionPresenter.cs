#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The geometry half of a world pick: which colonist, if any, the pick ray passes through.
    ///
    /// The camera rig raises <see cref="SliceCameraRig.Picked"/> with the cell and the ray; this
    /// component hit-tests the ray against the colonists it can see and hands the answer to the
    /// <see cref="SelectionDirector"/>, which decides what the click means and holds it for
    /// everyone else. Only the ray maths lives here, because only the ray maths needs Unity.
    ///
    /// It exists because of a specific piece of feedback: a colonist hauling and a colonist
    /// wandering because they had broken down looked identical, so there was no way to tell a
    /// working simulation from a stuck one by looking at it. The readout that answered it grew
    /// into the HUD's inspect pane; the resolution stayed here and the decision moved to the
    /// director.
    ///
    /// The same feet-placing maths answers the two gestures that select many at once: a drag box
    /// (containment of each colonist's screen point in the released rect) and a double click
    /// (everything of the clicked kind visible on screen), per <c>09-ui-and-input.md</c> M2.
    /// </summary>
    public sealed class SelectionPresenter : MonoBehaviour
    {
        /// <summary>Two presses closer than this, on the same colonist, are a double click — one
        /// threshold shared with the roster card's double click, owned by <see cref="DoubleClick"/>.</summary>
        const float DoubleClickSeconds = DoubleClick.Seconds;

        OdysseyBootstrap? _bootstrap;
        SliceCameraRig? _rig;
        float _lastClickAt;
        PawnId _lastClickPawn;

        void Awake()
        {
            _bootstrap = GetComponent<OdysseyBootstrap>();
            _rig = _bootstrap != null ? _bootstrap.cameraRig : null;
            if (_rig != null)
            {
                _rig.Picked += OnPicked;
                _rig.BoxSelected += OnBoxSelected;
            }
        }

        void OnDestroy()
        {
            if (_rig != null)
            {
                _rig.Picked -= OnPicked;
                _rig.BoxSelected -= OnBoxSelected;
            }
        }

        void OnPicked(CellRef? picked, Ray ray)
        {
            var world = _bootstrap?.World;
            var directors = _bootstrap?.Directors;
            if (world == null || directors == null) return;

            // A colonist is picked by the ray passing through the same box the cursor draws round
            // them, so what is bracketed is exactly what can be clicked and nothing wider. This
            // replaced a catchment radius of two cells — a five-by-five-cell square, twelve metres
            // across — that made it hard to click off a colonist or on to anything near one.
            //
            // The picker returns the nearest cell on any solid-drawn layer, and the hit-test
            // looks only at pawns at or above it, so a colonist upstairs is clickable where they
            // are drawn and a colonist below the floor the ray stopped at is not.
            PawnId under = picked.HasValue
                ? PawnUnderRay(world.Views.Current, ray, picked.Value.Y)
                : PawnId.None;

            bool shift = Keyboard.current?.shiftKey.isPressed == true;

            // A double click on a colonist asks for everything of the same kind on screen —
            // today the one kind is colonists, so the kind is everyone. It replaces unless shift
            // is held, like every other selection gesture.
            if (under.IsValid && !shift && under == _lastClickPawn
                && Time.unscaledTime - _lastClickAt <= DoubleClickSeconds)
            {
                var camera = Camera();
                var onScreen = new List<PawnId>();
                if (camera != null && PawnsOnScreen(camera, world.Views.Current, onScreen))
                {
                    directors.Selection.PickMany(onScreen, additive: false, SelectionChange.Similar);
                    _lastClickPawn = PawnId.None;
                    return;
                }
            }
            _lastClickAt = Time.unscaledTime;
            _lastClickPawn = under;

            // The pane's question is asked and answered BEFORE the selection changes, because the
            // selection's own handlers read the frame the moment it does: republishing with the
            // answer already in it is what stops the pane painting one refresh of "Ground" — or
            // the previous tile's facts — before the real ones arrive (owner, 2026-09-17: the
            // readout visibly skipped to something else before settling). Republishing between
            // ticks is safe for exactly the reason a question is safe: it changes no state the
            // simulation owns, publishes over the same settled world, and moves no counter.
            // A corpse under the pointer, where no living pawn is (design 33 §5f): clickable as
            // "Corpse of X". The corpse's own box, the one its cursor draws. The choice is lane C's
            // (HudDirectors.ChooseCorpse); until it answers yes the click falls through to the
            // ground beneath, as it always did.
            if (!under.IsValid && picked.HasValue && !shift && _bootstrap?.Corpses != null)
            {
                SelectableBand(out int lowest, out _);
                int corpse = _bootstrap.Corpses.CorpseUnderRay(ray, Mathf.Max(lowest, picked.Value.Y));
                if (corpse > 0 && directors.ChooseCorpse(corpse, world.Views.Current)) return;
            }

            if (picked.HasValue)
                world.Intents.Submit(new Intent(IntentKind.QueryCell, picked.Value));
            else
                world.Intents.Submit(new Intent(IntentKind.QueryCell, default, -1));
            world.RepublishViews();

            directors.Selection.Pick(picked, under, world.Views.Current, additive: shift);
        }

        /// <summary>
        /// A right-click with no tool armed (design 33 §2f), handed on by
        /// <see cref="DesignatePresenter"/>. <see cref="OrderModel.RightClick"/> decides; this only
        /// supplies the two things a Unity-free model cannot find for itself — who is under the
        /// pointer, by the same hit-test a left click uses, and whether Ctrl is held — and carries
        /// the answer to the world. A selection with no colonist in it is not asked at all, so
        /// a right-click that is not an order costs nothing.
        ///
        /// <para><b>A colonist, not a drafted one</b> (design 33 §5j): a right-click on a weapon
        /// sends the primary colonist for it drafted or not, so the gate is
        /// <see cref="OrderModel.HearsRightClick"/>. Behind <c>AnyDrafted</c>, as C1 left it, an
        /// undrafted colonist could not be sent for a weapon at all — the model said yes and the
        /// click never reached it (integration, 2026-09-23). Everything else still needs a draft
        /// inside the model, so the wider gate sends nothing new.</para>
        ///
        /// <para><b>A thing with several answers asks instead of acting</b> (design 33 §7a): when
        /// the model answers with menu rows — a weapon's <i>Equip</i> and <i>Cancel</i> — the HUD
        /// raises them at the pointer and nothing is sent until a row is chosen. The model fills
        /// one list or the other, never both.</para>
        /// </summary>
        public void Order(CellRef? cell, Ray ray)
        {
            var world = _bootstrap?.World;
            var directors = _bootstrap?.Directors;
            if (world == null || directors == null) return;

            WorldSnapshot snapshot = world.Views.Current;
            IReadOnlyList<PawnId> selection = directors.Selection.Pawns;
            if (!OrderModel.HearsRightClick(selection, snapshot)) return;

            PawnId under = cell.HasValue ? PawnUnderRay(snapshot, ray, cell.Value.Y) : PawnId.None;
            bool ctrl = Keyboard.current?.ctrlKey.isPressed == true;

            // What stands in the clicked cell, off the render mirror, for the building half of the
            // order (C6, design 33 §13i): the model cannot see the grid, and which edifices are
            // targets is the snapshot's, so this is a fact handed over like the pawn and Ctrl.
            Odyssey.Presentation.World.WorldRenderModel? mirror = _bootstrap?.Model;
            int edifice = cell.HasValue && mirror != null && snapshot.Size.Contains(cell.Value.X, cell.Value.Z, cell.Value.Y)
                ? mirror.EdificeDef(snapshot.Size.Index(cell.Value))
                : EdificeHandle.None;

            _orders.Clear();
            OrderModel.RightClick(selection, snapshot, cell, under, ctrl, _orders, _menu, edifice);
            if (_menu.Count > 0)
            {
                if (_shell == null) _shell = GetComponent<Ui.HudShell>();
                Mouse? mouse = Mouse.current;
                if (_shell != null && mouse != null) _shell.OpenContextMenu(_menu, mouse.position.ReadValue());
            }
            for (int i = 0; i < _orders.Count; i++) world.Intents.Submit(_orders[i]);
            _orders.Clear();
            _menu.Clear();
        }

        // Scratch for Order: filled and emptied inside one call, never state. The shell copies
        // the rows it is handed, so the list can be emptied as soon as the menu is up.
        readonly List<Intent> _orders = new List<Intent>();
        readonly List<ContextMenuRow> _menu = new List<ContextMenuRow>();
        Ui.HudShell? _shell;

        void OnBoxSelected(Rect screenRect, bool additive)
        {
            var world = _bootstrap?.World;
            var directors = _bootstrap?.Directors;
            var camera = Camera();
            if (world == null || directors == null || camera == null) return;

            // Containment, not enclosure: a colonist whose bracket the box touches is selected,
            // because the box is drawn on screen and so is the colonist — the test that matches
            // what the player saw is whether their point is inside the rect.
            var snapshot = world.Views.Current;
            SelectableBand(out int lowest, out int highest);
            var boxed = new List<PawnId>();
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (!Visible(pawn, lowest, highest)) continue;
                Vector3 point = camera.WorldToScreenPoint(ScreenPointOf(pawn));
                if (point.z <= 0f) continue;
                if (screenRect.Contains(new Vector2(point.x, point.y))) boxed.Add(pawn.Id);
            }

            directors.Selection.PickMany(boxed, additive, SelectionChange.Boxed);
        }

        /// <summary>
        /// Is this pawn on a layer the player can actually see and click?
        ///
        /// <para><b>Both ends, and both are bugs this exists for.</b> The lower one came first: a
        /// screen-rect containment test has no depth constraint, so a colonist eight layers down
        /// projects to a screen point exactly like one at your feet, and a box dragged across the
        /// surface selected miners underground that were never drawn. The command grid would then
        /// act on colonists the player had never seen, let alone chosen.</para>
        ///
        /// <para>The upper one used to be the active layer, because ADR 0006 said nothing above
        /// the slice may be a pointer target. The owner took that up on 2026-09-16 along with the
        /// cell picker: above the surface every layer is drawn <i>solid</i>, and a figure working a
        /// storey up that can be seen and not clicked is the same complaint as an outcrop that can
        /// be seen and not mined. The bound is now the band the picker walks, which is the band the
        /// renderer draws at full opacity — so "selectable" and "drawn solid" cannot drift
        /// apart.</para>
        /// </summary>
        bool Visible(PawnView pawn, int lowest, int highest) =>
            pawn.Cell.Y >= lowest && pawn.Cell.Y <= highest;

        /// <summary>The layers a colonist may be selected on, matching <c>SlicePicker</c>'s band.</summary>
        void SelectableBand(out int lowest, out int highest)
        {
            lowest = _rig != null ? Mathf.Max(0, _rig.LowestSelectableLayer) : 0;
            highest = _rig != null ? _rig.HighestSelectableLayer : int.MaxValue;
        }

        /// <summary>
        /// Every colonist whose screen point is inside the viewport, on a layer that can be seen
        /// and clicked — the population a double click means by "on screen".
        /// </summary>
        bool PawnsOnScreen(UnityEngine.Camera camera, WorldSnapshot snapshot, List<PawnId> into)
        {
            SelectableBand(out int lowest, out int highest);
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (!Visible(pawn, lowest, highest)) continue;
                Vector3 point = camera.WorldToScreenPoint(ScreenPointOf(pawn));
                if (point.z <= 0f) continue;
                if (point.x >= 0f && point.y >= 0f && point.x <= camera.pixelWidth && point.y <= camera.pixelHeight)
                    into.Add(pawn.Id);
            }
            return into.Count > 0;
        }

        UnityEngine.Camera? Camera() => _rig != null ? _rig.GetComponent<UnityEngine.Camera>() : null;

        /// <summary>
        /// The point that stands for a colonist on screen: the middle of the chest rather than
        /// the feet, so a box drawn over the visible half of a figure still catches them when
        /// the feet are below the frame's edge.
        /// </summary>
        Vector3 ScreenPointOf(PawnView pawn)
        {
            if (_bootstrap != null && _bootstrap.Figures != null
                && _bootstrap.Figures.TryGetFeet(pawn.Id, out Vector3 feet))
                return feet + Vector3.up * (_bootstrap.colonistCursor.y * 0.5f);
            int movePerTick = _bootstrap != null ? _bootstrap.MovePerTick : 0;
            float tickAlpha = _bootstrap != null ? _bootstrap.TickAlpha : 0f;
            return Odyssey.Presentation.Rendering.PawnPose.Of(
                       pawn, tickAlpha, movePerTick, out _, _bootstrap?.Model)
                + Vector3.up * (_bootstrap != null ? _bootstrap.colonistCursor.y * 0.5f : 1.35f);
        }

        /// <summary>
        /// The nearest colonist whose cursor box the ray passes through, at or above the layer of
        /// the cell the ray ended on. Placed with the same tween the figure is drawn with, so a
        /// walking colonist is clickable where they appear, not where their cell says they are.
        /// </summary>
        PawnId PawnUnderRay(WorldSnapshot snapshot, Ray ray, int pickedLayer)
        {
            SelectableBand(out int lowest, out int highest);

            // The pick ray descends, so everything it met before the terrain it stopped at is at
            // or above that cell's layer — which is how a colonist standing on an outcrop stays
            // clickable while a miner eight layers below, whose box the same ray clips long after
            // it has already buried itself in the ground, does not. It is the cheap stand-in for
            // comparing ray distances, and it is exact for the only camera this game has.
            lowest = Mathf.Max(lowest, pickedLayer);
            Vector3 box = _bootstrap != null ? _bootstrap.colonistCursor : new Vector3(1.15f, 2.7f, 1.15f);
            float tickAlpha = _bootstrap != null ? _bootstrap.TickAlpha : 0f;
            int movePerTick = _bootstrap != null ? _bootstrap.MovePerTick : 0;
            PawnId best = PawnId.None;
            float nearest = float.MaxValue;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];

                if (!Visible(pawn, lowest, highest)) continue;

                // The same tween the figure is drawn with, which is what the summary above claims
                // and what this line did not do: it passed 0 and 0, placing the box at the tick
                // boundary while the figure had already been carried on into the part-tick. A
                // walking colonist was therefore clickable slightly behind where they appeared,
                // by up to the distance they cover in one tick, and a click aimed at the figure
                // fell through to the cell underneath them instead.
                // The figure's own position when it has one, and the pose only as the fallback for
                // a pawn drawn by the instanced pass. A working figure is stepped off its cell to
                // reach the wood, so the pose and the screen disagree by most of a stride exactly
                // while a colonist is chopping — which is when the player is trying to click them.
                Bounds bounds;
                if (pawn.IsAnimal && _bootstrap?.Figures != null
                    && _bootstrap.Figures.TryGetAnimalBox(pawn.Id, out Matrix4x4 place, out Vector3 animal))
                {
                    // An animal is clicked through its own drawn box, the one the cursor draws
                    // (owner, 2026-09-22). Axis-aligned at the longer of its two footprint sides,
                    // which is a square a turned hog still fits inside.
                    float side = Mathf.Max(animal.x, animal.z);
                    bounds = new Bounds(place.GetPosition(), new Vector3(side, animal.y, side));
                }
                else
                {
                    if (_bootstrap?.Figures == null || !_bootstrap.Figures.TryGetFeet(pawn.Id, out Vector3 feet))
                        feet = Odyssey.Presentation.Rendering.PawnPose.Of(
                            pawn, tickAlpha, movePerTick, out _, _bootstrap?.Model);
                    bounds = new Bounds(feet + Vector3.up * (box.y * 0.5f), box);
                }
                if (!bounds.IntersectRay(ray, out float distance) || distance >= nearest) continue;

                nearest = distance;
                best = pawn.Id;
            }
            return best;
        }
    }
}
