#nullable enable
using System;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Resolves what a pick landed on: a colonist, an item, or nothing but the cell.
    ///
    /// This is the InputRouter's one live case so far, and it stays a small, dull class on
    /// purpose. The camera rig owns picking and raises <c>SelectionChanged</c> with the ray;
    /// this component is the listener that says what the pick *meant* — the ray passed through
    /// a colonist's bracket, an item sits in the cell, or it is bare ground — and holds the
    /// answer for everyone else. The selection cursor (bootstrap) and the inspect pane (HUD)
    /// both read it, so the thing bracketed on the board is exactly the thing the pane describes.
    ///
    /// It exists because of a specific piece of feedback: a colonist hauling and a colonist
    /// wandering because they had broken down looked identical, so there was no way to tell a
    /// working simulation from a stuck one by looking at it. The readout that answered it grew
    /// into the HUD's inspect pane; the display moved there, the resolution stayed here.
    ///
    /// It reads the published snapshot and nothing else, like everything else in presentation.
    /// </summary>
    [RequireComponent(typeof(OdysseyBootstrap))]
    public sealed class SelectionReadout : MonoBehaviour
    {
        /// <summary>
        /// The colonist the last click landed on, or <see cref="PawnId.None"/>.
        ///
        /// Public because the cursor needs it: a selected colonist gets a bracket around the
        /// figure rather than around the cell they happen to be standing in, and the thing that
        /// draws that has no business repeating the pick.
        /// </summary>
        public PawnId SelectedPawn => _selected;

        /// <summary>The item in the clicked cell, when no colonist claimed the click.</summary>
        public ThingId SelectedThing => _selectedThing;

        /// <summary>The item def of <see cref="SelectedThing"/>, which is what its art is looked up by.</summary>
        public int SelectedThingDef => _selectedThingDef;

        /// <summary>
        /// Raised after every pick is resolved (and after <see cref="SelectPawn"/>), so the
        /// inspect pane can answer the click in the same frame. Carries no payload on purpose:
        /// listeners read the three properties above, which by then are already the answer.
        /// </summary>
        public event Action? SelectionResolved;

        ThingId _selectedThing = ThingId.None;
        int _selectedThingDef = -1;
        PawnId _selected = PawnId.None;

        OdysseyBootstrap? _bootstrap;
        Odyssey.Presentation.CameraRig.SliceCameraRig? _rig;

        void Awake()
        {
            _bootstrap = GetComponent<OdysseyBootstrap>();
            _rig = _bootstrap != null ? _bootstrap.cameraRig : null;
            if (_rig != null) _rig.SelectionChanged += OnSelectionChanged;
        }

        void OnDestroy()
        {
            if (_rig != null) _rig.SelectionChanged -= OnSelectionChanged;
        }

        /// <summary>
        /// Select a colonist outright, without a pick — the roster bar's click path. Resolves
        /// the same way a world click does, so the cursor, the pane and the roster all agree on
        /// one selection from one method.
        /// </summary>
        public void SelectPawn(PawnId id)
        {
            _selected = id;
            _selectedThing = ThingId.None;
            _selectedThingDef = -1;
            SelectionResolved?.Invoke();
        }

        /// <summary>
        /// Resolve what a pick landed on, inside the pick.
        ///
        /// Deliberately no input handling here: the camera rig owns picking through the Input
        /// System, and reading the mouse again from this component meant the legacy Input class,
        /// which does nothing under the new backend. This used to poll the rig's selection from
        /// its own Update instead, and that had its own fault — Unity does not order two
        /// components' Updates, so on the click frame this could run first, resolve against last
        /// frame's cell, and the cursor would draw one tier and then snap to the right one. A
        /// handler on the rig's event runs during the pick itself; the answer exists before any
        /// LateUpdate draws.
        /// </summary>
        void OnSelectionChanged(CellRef? picked, Ray ray)
        {
            var world = _bootstrap?.World;
            if (world == null) return;

            // A colonist is picked by the ray passing through the same box the cursor draws round
            // them, so what is bracketed is exactly what can be clicked and nothing wider. This
            // replaced a catchment radius of two cells — a five-by-five-cell square, twelve metres
            // across — that made it hard to click off a colonist or on to anything near one. The
            // radius had been compensating for a tilted ray sailing over a low baked box; the
            // figures are full height now and a proper hit-test needs no compensation.
            //
            // The picker cannot return a cell above the active layer, and the hit-test only looks
            // at pawns on drawn layers, so a colonist upstairs is still never selected through
            // the floor they are standing on.
            _selected = picked.HasValue
                ? PawnUnderRay(world.Views.Current, ray, picked.Value.Y)
                : PawnId.None;

            // A colonist wins over an item, because a colonist standing on a crate is what you
            // meant to click, and the crate will still be there when they walk off it.
            _selectedThing = ThingId.None;
            _selectedThingDef = -1;
            if (picked.HasValue && !_selected.IsValid)
                ThingAt(world.Views.Current, picked.Value, out _selectedThing, out _selectedThingDef);

            SelectionResolved?.Invoke();
        }

        static void ThingAt(WorldSnapshot snapshot, CellRef cell, out ThingId id, out int def)
        {
            var things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
            {
                if (things[i].Cell != cell) continue;
                id = things[i].Id;
                def = things[i].DefIndex;
                return;
            }
            id = ThingId.None;
            def = -1;
        }

        /// <summary>
        /// The nearest colonist whose cursor box the ray passes through, on or below the picked
        /// layer. Placed with the same tween the figure is drawn with, so a walking colonist is
        /// clickable where they appear, not where their cell says they are.
        /// </summary>
        PawnId PawnUnderRay(WorldSnapshot snapshot, Ray ray, int activeLayer)
        {
            Vector3 box = _bootstrap != null ? _bootstrap.colonistCursor : new Vector3(1.15f, 2.7f, 1.15f);
            PawnId best = PawnId.None;
            float nearest = float.MaxValue;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (pawn.Cell.Y > activeLayer) continue;

                Vector3 feet = Odyssey.Presentation.Rendering.PawnPose.Of(pawn, 0f, 0, out _);
                var bounds = new Bounds(feet + Vector3.up * (box.y * 0.5f), box);
                if (!bounds.IntersectRay(ray, out float distance) || distance >= nearest) continue;

                nearest = distance;
                best = pawn.Id;
            }
            return best;
        }
    }
}
