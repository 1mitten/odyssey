#nullable enable
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Sim.Contracts;
using UnityEngine;

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
    /// </summary>
    public sealed class SelectionPresenter : MonoBehaviour
    {
        OdysseyBootstrap? _bootstrap;
        SliceCameraRig? _rig;

        void Awake()
        {
            _bootstrap = GetComponent<OdysseyBootstrap>();
            _rig = _bootstrap != null ? _bootstrap.cameraRig : null;
            if (_rig != null) _rig.Picked += OnPicked;
        }

        void OnDestroy()
        {
            if (_rig != null) _rig.Picked -= OnPicked;
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
            // The picker cannot return a cell above the active layer, and the hit-test only looks
            // at pawns on drawn layers, so a colonist upstairs is still never selected through
            // the floor they are standing on.
            PawnId under = picked.HasValue
                ? PawnUnderRay(world.Views.Current, ray, picked.Value.Y)
                : PawnId.None;
            directors.Selection.Pick(picked, under, world.Views.Current);
        }

        /// <summary>
        /// The nearest colonist whose cursor box the ray passes through, on or below the picked
        /// layer. Placed with the same tween the figure is drawn with, so a walking colonist is
        /// clickable where they appear, not where their cell says they are.
        /// </summary>
        PawnId PawnUnderRay(WorldSnapshot snapshot, Ray ray, int activeLayer)
        {
            Vector3 box = _bootstrap != null ? _bootstrap.colonistCursor : new Vector3(1.15f, 2.7f, 1.15f);
            float tickAlpha = _bootstrap != null ? _bootstrap.TickAlpha : 0f;
            int movePerTick = _bootstrap != null ? _bootstrap.MovePerTick : 0;
            PawnId best = PawnId.None;
            float nearest = float.MaxValue;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (pawn.Cell.Y > activeLayer) continue;

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
                if (_bootstrap?.Figures == null || !_bootstrap.Figures.TryGetFeet(pawn.Id, out Vector3 feet))
                    feet = Odyssey.Presentation.Rendering.PawnPose.Of(pawn, tickAlpha, movePerTick, out _);
                var bounds = new Bounds(feet + Vector3.up * (box.y * 0.5f), box);
                if (!bounds.IntersectRay(ray, out float distance) || distance >= nearest) continue;

                nearest = distance;
                best = pawn.Id;
            }
            return best;
        }
    }
}
