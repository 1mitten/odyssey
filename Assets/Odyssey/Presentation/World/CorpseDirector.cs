#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The dead, drawn (design 33 §1, §3): every <see cref="CorpseView"/> on a drawn layer, lying
    /// where it fell in the death pose — the Sword Combat pack's <c>Death_*</c> clip played out and
    /// its <c>_Pose</c> held where the pack is present, a computed fall where it is not — with the
    /// face and colours its seed deals, so the colonist who fell is the colonist who lies there.
    /// <b>Lane B's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A corpse is a baked copy, not a figure.</b> A figure is lent out
    /// (<see cref="PawnFigureDirector.BorrowForCorpse"/>), laid down, baked to static meshes and
    /// handed straight back to the pool, so the dead never count against the 64-figure ceiling and
    /// cost a static mesh each rather than a graph. A death seen happening plays its fall first; a
    /// corpse that was already lying there when the frame arrived — a load, or a slice scrolled
    /// down to it — is baked lying at once.</para>
    ///
    /// <para><b>A corpse that cannot be drawn as a body</b> — no art for its face on this machine —
    /// is a grey capsule lying along the way it fell, which is what the runner draws.</para>
    ///
    /// <para><b>Per-frame cost scales with the corpses on the board</b> (<c>docs/process.md</c> §3),
    /// which a colony counts on its fingers: one visibility test each, and a pose and an evaluate
    /// for the few still falling.</para>
    /// </summary>
    public sealed class CorpseDirector : IDisposable
    {
        /// <summary>
        /// How recent a death must be, in ticks, to be seen falling rather than found lying: two
        /// seconds at normal speed. Anything older was already on the ground before this frame.
        /// </summary>
        public const int FreshTicks = 120;

        /// <summary>How far round a corpse the cursor's brackets stand, in metres.</summary>
        public const float CursorMargin = 0.15f;

        readonly WorldRenderModel _model;
        readonly PawnFigureDirector? _figures;
        readonly Transform _root;
        readonly int _layer;

        sealed class Body
        {
            public int Id;
            public CellRef Cell;
            public GameObject? Object;
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public PawnFigureDirector.CorpseLoan? Loan;
            public float Seconds;
            public Vector3 Floor;
            public float Yaw;
            public Bounds Box;
            public int Stamp;
            public bool Marker;
            public bool Visible = true;
        }

        readonly Dictionary<int, Body> _bodies = new Dictionary<int, Body>();
        readonly List<int> _gone = new List<int>();
        int _stamp;
        bool _seenAFrame;
        Material? _markerMaterial;

        public CorpseDirector(WorldRenderModel model, ModuleCatalogue? catalogue, PawnFigureDirector? figures,
            Transform parent, int layer)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _figures = figures;
            _layer = layer;
            var root = new GameObject("Corpses") { layer = layer };
            root.transform.SetParent(parent, false);
            _root = root.transform;
        }

        /// <summary>How many corpses are drawn, falling or lying.</summary>
        public int Count => _bodies.Count;

        /// <summary>How many are still falling, each on a lent figure.</summary>
        public int Falling
        {
            get
            {
                int n = 0;
                foreach (Body body in _bodies.Values) if (body.Loan != null) n++;
                return n;
            }
        }

        /// <summary>How many are drawn as the stand-in capsule because their face has no art here.</summary>
        public int Markers
        {
            get
            {
                int n = 0;
                foreach (Body body in _bodies.Values) if (body.Marker) n++;
                return n;
            }
        }

        /// <summary>
        /// Once a frame, after the doors. Starts a body for every new corpse, lets the falling ones
        /// fall, hides the ones off the drawn layers and destroys the ones the frame no longer
        /// carries. <paramref name="deltaTime"/> is the frame's time; a paused world holds a fall
        /// where it is.
        /// </summary>
        public void Sync(WorldSnapshot snapshot, int activeLayer, SliceSettings slice, float deltaTime = 0f)
        {
            _stamp++;
            float dt = snapshot.Running ? deltaTime : 0f;
            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));
            int highest = slice.HighestVisibleLayer(activeLayer, snapshot.Size.SizeY);

            ReadOnlySpan<CorpseView> corpses = snapshot.Corpses;
            for (int i = 0; i < corpses.Length; i++)
            {
                CorpseView corpse = corpses[i];
                if (!_bodies.TryGetValue(corpse.Id, out Body? body))
                {
                    bool fresh = _seenAFrame && snapshot.Running && snapshot.Tick - corpse.Tick <= FreshTicks;
                    body = Start(corpse, fresh);
                    _bodies[corpse.Id] = body;
                }
                body.Stamp = _stamp;

                if (body.Loan != null)
                {
                    body.Seconds += dt;
                    _figures!.PoseCorpse(body.Loan, body.Seconds, body.Floor, body.Yaw);
                    if (body.Seconds >= body.Loan.DyingSeconds) Finish(body);
                }

                bool visible = corpse.Cell.Y >= lowest && corpse.Cell.Y <= highest;
                if (visible != body.Visible || body.Object != null && body.Object.activeSelf != visible)
                {
                    body.Visible = visible;
                    if (body.Object != null) body.Object.SetActive(visible);
                }
            }

            _gone.Clear();
            foreach (KeyValuePair<int, Body> pair in _bodies)
                if (pair.Value.Stamp != _stamp) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++)
            {
                Release(_bodies[_gone[i]]);
                _bodies.Remove(_gone[i]);
            }

            _seenAFrame = true;
        }

        Body Start(in CorpseView corpse, bool fresh)
        {
            var body = new Body
            {
                Id = corpse.Id,
                Cell = corpse.Cell,
                Floor = GroundRelief.Lift(CellMetrics.FloorCentre(corpse.Cell)),
                Yaw = CombatPose.YawOfFacing(corpse.Facing),
            };

            PawnFigureDirector.CorpseLoan? loan =
                _figures != null && _figures.CanDrawCorpse(corpse) ? _figures.BorrowForCorpse(corpse) : null;
            if (loan == null)
            {
                MakeMarker(body, (corpse.Flags & PawnFlags.Person) == 0);
                return body;
            }

            body.Loan = loan;
            if (fresh)
            {
                _figures!.PoseCorpse(loan, 0f, body.Floor, body.Yaw);
                // A fall has no box of its own yet; the cell's footprint, flat, until it is baked.
                body.Box = new Bounds(body.Floor + Vector3.up * 0.5f,
                    new Vector3(CellMetrics.SizeXZ, 1f, CellMetrics.SizeXZ));
                return body;
            }

            _figures!.PoseCorpse(loan, float.MaxValue, body.Floor, body.Yaw);
            Finish(body);
            return body;
        }

        /// <summary>Bake the body where it lies and hand its figure back.</summary>
        void Finish(Body body)
        {
            PawnFigureDirector.CorpseLoan loan = body.Loan!;
            body.Object = _figures!.BakeCorpse(loan, _root, _layer, body.Meshes);
            _figures.ReturnCorpse(loan);
            body.Loan = null;
            body.Object.SetActive(body.Visible);
            body.Box = BoundsOf(body.Object);
        }

        void MakeMarker(Body body, bool animal)
        {
            float length = animal ? 1.0f : 2.0f;
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "Corpse marker";
            marker.layer = _layer;
            Collider? collider = marker.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            marker.transform.SetParent(_root, false);

            Vector3 heading = Quaternion.Euler(0f, body.Yaw, 0f) * Vector3.forward;
            // A capsule is two units tall about its middle: laid along the heading, on the floor.
            marker.transform.SetPositionAndRotation(
                body.Floor + Vector3.up * (animal ? 0.15f : 0.2f),
                Quaternion.LookRotation(Vector3.up, heading));
            marker.transform.localScale = new Vector3(animal ? 0.35f : 0.5f, length * 0.5f, 0.35f);

            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MarkerMaterial();

            body.Object = marker;
            body.Marker = true;
            body.Box = BoundsOf(marker);
        }

        Material MarkerMaterial()
        {
            if (_markerMaterial != null) return _markerMaterial;
            _markerMaterial = new Material(_model.Library.FallbackMaterial) { name = "Odyssey/CorpseMarker" };
            _markerMaterial.SetColor("_BaseColor", new Color(0.42f, 0.40f, 0.38f));
            return _markerMaterial;
        }

        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            bool any = false;
            var box = new Bounds(root.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!any) { box = renderers[i].bounds; any = true; }
                else box.Encapsulate(renderers[i].bounds);
            }
            return box;
        }

        /// <summary>
        /// Where a corpse is drawn, as the box its renderers fill, or false for a corpse that is not
        /// drawn (unknown, or off the drawn layers). What the cursor brackets and a click hits.
        /// </summary>
        public bool TryGetBox(int corpseId, out Bounds box)
        {
            if (corpseId > 0 && _bodies.TryGetValue(corpseId, out Body? body) && body.Visible)
            {
                box = body.Box;
                return true;
            }
            box = default;
            return false;
        }

        /// <summary>
        /// The cursor round the selected corpse (design 33 §5f): the corpse's own box with the
        /// margin, or false when nothing is selected or the corpse is not drawn.
        /// </summary>
        public bool TryBracket(SelectionDirector? selection, out Matrix4x4 place, out Vector3 size)
        {
            if (selection != null && selection.HasCorpse && TryGetBox(selection.Corpse, out Bounds box))
            {
                place = Matrix4x4.Translate(box.center);
                size = box.size + Vector3.one * (CursorMargin * 2f);
                return true;
            }
            place = default;
            size = default;
            return false;
        }

        /// <summary>
        /// The nearest drawn corpse the ray passes through, at or above <paramref name="lowestLayer"/>,
        /// or 0. The click half of "clickable as Corpse of X": the hit-test beside the colonists'.
        /// </summary>
        public int CorpseUnderRay(Ray ray, int lowestLayer)
        {
            int best = 0;
            float nearest = float.MaxValue;
            foreach (Body body in _bodies.Values)
            {
                if (!body.Visible || body.Cell.Y < lowestLayer) continue;
                if (!body.Box.IntersectRay(ray, out float distance) || distance >= nearest) continue;
                nearest = distance;
                best = body.Id;
            }
            return best;
        }

        void Release(Body body)
        {
            if (body.Loan != null && _figures != null)
            {
                _figures.ReturnCorpse(body.Loan);
                body.Loan = null;
            }
            if (body.Object != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(body.Object);
                else UnityEngine.Object.DestroyImmediate(body.Object);
                body.Object = null;
            }
            for (int i = 0; i < body.Meshes.Count; i++)
            {
                if (body.Meshes[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(body.Meshes[i]);
                else UnityEngine.Object.DestroyImmediate(body.Meshes[i]);
            }
            body.Meshes.Clear();
        }

        public void Dispose()
        {
            foreach (Body body in _bodies.Values) Release(body);
            _bodies.Clear();
            if (_markerMaterial != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_markerMaterial);
                else UnityEngine.Object.DestroyImmediate(_markerMaterial);
                _markerMaterial = null;
            }
            if (_root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root.gameObject);
                else UnityEngine.Object.DestroyImmediate(_root.gameObject);
            }
        }
    }
}
