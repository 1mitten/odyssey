#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Manages the visual rendering and sliding open/close animation of door leaves.
    ///
    /// <para><b>A director and not a subsystem</b> (<c>01-architecture.md</c> §3a): presentation
    /// coordinating presentation. The simulation controls passage and charges pathing cost
    /// (<see cref="Odyssey.Sim.Pawns.DoorSystem"/>); this director is the visual facade that smoothly
    /// slides the door leaf laterally into the wall frame pocket when pawns approach or pass through,
    /// and triggers sound events on transitions.</para>
    /// </summary>
    public sealed class DoorDirector : IDisposable
    {
        public const float OpenDuration = 0.2f;
        public const float CloseDuration = 0.2f;
        public const float SlideDistance = 1.15f;
        public const float ProximityRange = 1.6f;

        public struct DoorState
        {
            public float OpenFactor;
            public bool TargetOpen;
        }

        readonly WorldRenderModel _model;
        readonly ModuleLibrary _library;
        readonly MaterialCache _materials;
        readonly bool _ownsMaterials;
        readonly int _leafModule;
        readonly GameObject _root;

        readonly Dictionary<int, DoorState> _states = new Dictionary<int, DoorState>();
        readonly List<int> _doorCells = new List<int>();
        int _doorListVersion = -1;

        Matrix4x4[] _placements = new Matrix4x4[16];
        int _placementCount;
        Matrix4x4[] _scratchMatrices = new Matrix4x4[16];

        public bool SubmitToGpu { get; set; } = true;

        /// <summary>Optional override for testing door states without pawns.</summary>
        public Func<int, bool>? IsOpenOverride { get; set; }

        public DoorDirector(
            WorldRenderModel model, ModuleCatalogue? catalogue, Transform? parent, int layer,
            MaterialCache? materials = null)
        {
            _model = model;
            _library = model.Library;
            _leafModule = _library.Resolve(ModuleIds.DoorLeaf, ModuleShape.WallPanel);
            _ownsMaterials = materials == null;
            _materials = materials ?? new MaterialCache();

            _root = new GameObject("Door Director");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.layer = layer;
        }

        /// <summary>How many door leaves the last <see cref="Sync"/> placed: the drawn ones, which walls-down
        /// leaves out (design 42 §5).</summary>
        public int LeavesPlaced => _placementCount;

        public int ActiveDoorCount
        {
            get
            {
                EnsureDoorList();
                return _doorCells.Count;
            }
        }

        public float OpenFactor(int cellIndex) =>
            _states.TryGetValue(cellIndex, out DoorState s) ? s.OpenFactor : 0f;

        public bool IsOpen(int cellIndex) =>
            _states.TryGetValue(cellIndex, out DoorState s) && s.OpenFactor > 0.01f;

        public void SetDoorState(int cellIndex, float openFactor, bool targetOpen)
        {
            _states[cellIndex] = new DoorState
            {
                OpenFactor = Mathf.Clamp01(openFactor),
                TargetOpen = targetOpen,
            };
        }

        public void Sync(
            WorldSnapshot snapshot, int activeLayer, SliceSettings slice, float dt,
            AudioDirector? audio = null)
        {
            EnsureDoorList();
            _placementCount = 0;

            int lowest = Mathf.Max(0, slice.LowestDrawnLayer(activeLayer, _model.LowestOutdoorLayer));
            int highest = slice.HighestVisibleLayer(activeLayer, _model.Size.SizeY);

            for (int i = 0; i < _doorCells.Count; i++)
            {
                int cellIndex = _doorCells[i];
                CellRef cell = _model.Size.FromIndex(cellIndex);
                if (cell.Y < lowest || cell.Y > highest) continue;

                int dir = _model.DoorFacing(cell.X, cell.Z, cell.Y);

                bool targetOpen = IsOpenOverride != null
                    ? IsOpenOverride(cellIndex)
                    : IsApproachedOrOccupied(snapshot, cell, dir);

                if (!_states.TryGetValue(cellIndex, out DoorState state))
                {
                    state = new DoorState { OpenFactor = 0f, TargetOpen = false };
                }

                Vector3 faceFloor = CellMetrics.FaceCentre(cell.X, cell.Z, cell.Y, dir);

                if (targetOpen && !state.TargetOpen)
                {
                    state.TargetOpen = true;
                    if (state.OpenFactor <= 0.01f)
                    {
                        audio?.PlayOneShot(SoundIds.DoorOpen, faceFloor);
                    }
                }
                else if (!targetOpen && state.TargetOpen)
                {
                    state.TargetOpen = false;
                }

                if (state.TargetOpen)
                {
                    state.OpenFactor = Mathf.MoveTowards(state.OpenFactor, 1f, dt / OpenDuration);
                }
                else
                {
                    float prev = state.OpenFactor;
                    state.OpenFactor = Mathf.MoveTowards(state.OpenFactor, 0f, dt / CloseDuration);
                    if (prev > 0f && state.OpenFactor == 0f)
                    {
                        audio?.PlayOneShot(SoundIds.DoorClose, faceFloor);
                    }
                }

                _states[cellIndex] = state;

                // Walls down (design 42 §5): the leaf is not drawn where the frame is a pair of
                // jambs or the storey is hidden. The door still opens, closes and sounds above —
                // only the drawing stops — so raising the walls finds it where it would have been.
                if (slice.LowersWallsOn(activeLayer, cell.Y) || slice.HidesBuiltOn(activeLayer, cell.Y))
                    continue;

                Matrix4x4 root = GroundRelief.Drape(faceFloor) *
                                 Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f));
                Vector3 slide = new Vector3(SlideDistance * state.OpenFactor, 0f, 0f);
                Matrix4x4 placement = root * Matrix4x4.Translate(slide);

                AppendPlacement(placement);
            }

            SubmitPlacements();
        }

        public bool IsApproachedOrOccupied(WorldSnapshot snapshot, CellRef cell) =>
            IsApproachedOrOccupied(snapshot, cell, _model.DoorFacing(cell.X, cell.Z, cell.Y));

        public bool IsApproachedOrOccupied(WorldSnapshot snapshot, CellRef cell, int dir)
        {
            Vector3 doorFace = CellMetrics.FaceCentre(cell.X, cell.Z, cell.Y, dir);
            float rangeSq = ProximityRange * ProximityRange;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                CellRef pcell = pawns[i].Cell;
                CellRef nextCell = pawns[i].NextCell;

                // Direct occupancy or actively walking into this door cell
                if ((pcell.X == cell.X && pcell.Z == cell.Z && pcell.Y == cell.Y) ||
                    (nextCell.X == cell.X && nextCell.Z == cell.Z && nextCell.Y == cell.Y))
                {
                    return true;
                }

                // Proximity check on the same layer
                if (pcell.Y == cell.Y)
                {
                    Vector3 pfloor = CellMetrics.FloorCentre(pcell.X, pcell.Z, pcell.Y);
                    float dx = pfloor.x - doorFace.x;
                    float dz = pfloor.z - doorFace.z;
                    if (dx * dx + dz * dz <= rangeSq)
                        return true;
                }
            }

            return false;
        }

        void EnsureDoorList()
        {
            if (_doorListVersion == _model.Version) return;
            _doorListVersion = _model.Version;
            _doorCells.Clear();

            int count = _model.Size.CellCount;
            for (int i = 0; i < count; i++)
            {
                if (_model.EdificeDef(i) == CoreContent.EdificeDoor)
                {
                    _doorCells.Add(i);
                }
            }
        }

        void AppendPlacement(in Matrix4x4 placement)
        {
            if (_placementCount == _placements.Length)
            {
                Array.Resize(ref _placements, _placements.Length * 2);
            }
            _placements[_placementCount++] = placement;
        }

        void SubmitPlacements()
        {
            if (_placementCount == 0 || !SubmitToGpu) return;

            ResolvedModule module = _library[_leafModule];
            if (module.IsEmpty) return;

            var parts = module.Parts;
            if (_scratchMatrices.Length < _placementCount)
                _scratchMatrices = new Matrix4x4[_placementCount];

            for (int p = 0; p < parts.Length; p++)
            {
                ModulePart part = parts[p];
                for (int i = 0; i < _placementCount; i++)
                    _scratchMatrices[i] = _placements[i] * part.Local;

                Material material = _materials.Get(
                    part.Material, Color.white, Color.black, ghost: false, alpha: 1f);

                var rp = new RenderParams(material)
                {
                    layer = _root.layer,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                };

                int sent = 0;
                while (sent < _placementCount)
                {
                    int n = Mathf.Min(ChunkRenderer.MaxInstancesPerCall, _placementCount - sent);
                    Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, _scratchMatrices, n, sent);
                    sent += n;
                }
            }
        }

        public void Dispose()
        {
            if (_ownsMaterials) _materials.Dispose();
            if (_root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }
        }
    }
}
