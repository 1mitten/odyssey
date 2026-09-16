#nullable enable
using System.Text;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// Says what is under the cursor and what the selected colonist is doing.
    ///
    /// This exists because of a specific piece of feedback: the scene rendered a world, but a
    /// colonist hauling and a colonist wandering because they had broken down looked identical,
    /// so there was no way to tell a working simulation from a stuck one by looking at it. A
    /// simulation you cannot read is a simulation you cannot trust.
    ///
    /// Deliberately minimal, and deliberately not a HUD. The interface proper — panels, the
    /// inspect pane, the alert stack — belongs to the UI line of work and will replace this. What
    /// is here is the smallest thing that makes the simulation legible while that is built.
    ///
    /// It reads the published snapshot and nothing else, like everything else in presentation.
    /// Strings are rebuilt only when the selection or the tick changes, not every frame.
    /// </summary>
    [RequireComponent(typeof(OdysseyBootstrap))]
    public sealed class SelectionReadout : MonoBehaviour
    {
        /// <summary>Job names, indexed by the job handle the snapshot carries. See PawnContent.JobIndex.</summary>
        static readonly string[] JobNames = { "hauling", "eating", "sleeping", "wandering", "waiting" };

        /// <summary>
        /// How far from the clicked cell a colonist may stand and still be the one selected.
        ///
        /// Two, not one, and the reason is geometric rather than a matter of taste. The picker
        /// answers with the floor cell the ray crosses, but a colonist is drawn as a body and a
        /// beacon standing well clear of that floor. Under a tilted camera the player aims at the
        /// beacon, so the ray passes over the pawn and meets the ground a cell or two beyond them.
        /// A radius of one leaves the most natural click — straight at the bright marker — landing
        /// on empty grass, which reads as the click being ignored rather than as a near miss.
        /// </summary>
        [Tooltip("Cells within this distance of the click count as picking that colonist.")]
        public int pickRadius = 2;

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

        ThingId _selectedThing = ThingId.None;
        int _selectedThingDef = -1;

        OdysseyBootstrap? _bootstrap;
        Odyssey.Presentation.CameraRig.SliceCameraRig? _rig;
        readonly StringBuilder _text = new StringBuilder(256);
        PawnId _selected = PawnId.None;
        int _builtForTick = -1;
        string _cached = string.Empty;
        GUIStyle? _style;

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
        void OnSelectionChanged(CellRef? picked)
        {
            var world = _bootstrap?.World;
            if (world == null) return;

            // The picker cannot return a cell above the active layer, so a colonist upstairs can
            // never be selected through the floor they are standing on.
            _selected = picked.HasValue
                ? NearestPawn(world.Views.Current, picked.Value, pickRadius)
                : PawnId.None;

            // A colonist wins over an item, because a colonist standing on a crate is what you
            // meant to click, and the crate will still be there when they walk off it.
            _selectedThing = ThingId.None;
            _selectedThingDef = -1;
            if (picked.HasValue && !_selected.IsValid)
                ThingAt(world.Views.Current, picked.Value, out _selectedThing, out _selectedThingDef);

            _builtForTick = -1;
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

        static PawnId NearestPawn(WorldSnapshot snapshot, CellRef cell, int radius)
        {
            PawnId best = PawnId.None;
            int bestDistance = int.MaxValue;
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                var pawn = pawns[i];
                if (pawn.Cell.Y != cell.Y) continue;
                int dx = Mathf.Abs(pawn.Cell.X - cell.X);
                int dz = Mathf.Abs(pawn.Cell.Z - cell.Z);
                int distance = Mathf.Max(dx, dz);
                if (distance > radius || distance >= bestDistance) continue;
                bestDistance = distance;
                best = pawn.Id;
            }
            return best;
        }

        void OnGUI()
        {
            var world = _bootstrap?.World;
            if (world == null) return;

            var snapshot = world.Views.Current;
            if (snapshot.Tick != _builtForTick)
            {
                Rebuild(snapshot);
                _builtForTick = snapshot.Tick;
            }
            if (_cached.Length == 0) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white },
                padding = new RectOffset(10, 10, 8, 8),
            };

            var size = _style.CalcSize(new GUIContent(_cached));
            var box = new Rect(12f, Screen.height - size.y - 12f, size.x + 8f, size.y);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(box, _cached, _style);
        }

        void Rebuild(WorldSnapshot snapshot)
        {
            _text.Clear();

            if (_selected.IsValid && snapshot.TryGetPawn(_selected, out PawnView pawn))
            {
                _text.Append("Colonist ").Append(_selected.Value);
                _text.Append("  ").Append(JobName(pawn.JobDef));
                _text.Append("\nat ").Append(pawn.Cell.X).Append(", ").Append(pawn.Cell.Z)
                     .Append("  layer ").Append(pawn.Cell.Y);
                _text.Append("\nfood ").Append(Percent(pawn.Food));
                _text.Append("   rest ").Append(Percent(pawn.Rest));
                _text.Append("   mood ").Append(pawn.Mood);
            }
            else
            {
                // Nothing selected: say what the colony as a whole is doing, which is the next
                // most useful thing and answers "is anything happening at all".
                _selected = PawnId.None;
                _text.Append(snapshot.PawnCount).Append(" colonists");
                if (snapshot.PawnCount > 0)
                {
                    _text.Append("  (");
                    AppendJobCounts(snapshot);
                    _text.Append(')');
                }
                _text.Append("\nclick a colonist to inspect");
            }

            _cached = _text.ToString();
        }

        void AppendJobCounts(WorldSnapshot snapshot)
        {
            var pawns = snapshot.Pawns;
            bool first = true;
            for (int job = 0; job < JobNames.Length; job++)
            {
                int count = 0;
                for (int i = 0; i < pawns.Length; i++) if (pawns[i].JobDef == job) count++;
                if (count == 0) continue;
                if (!first) _text.Append(", ");
                _text.Append(count).Append(' ').Append(JobNames[job]);
                first = false;
            }
            if (first) _text.Append("idle");
        }

        static string JobName(int jobDef) =>
            jobDef >= 0 && jobDef < JobNames.Length ? JobNames[jobDef] : "idle";

        static string Percent(int need) => (need / 10).ToString() + "%";
    }
}
