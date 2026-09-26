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

        /// <summary>
        /// Every door on the board, kept chunk by chunk (design 62 §2c, DM1). It was one list
        /// rebuilt by a scan of every cell whenever <see cref="WorldRenderModel.Version"/> moved —
        /// which any edit anywhere does, so one mined cell on a 32-layer Huge board read 1.8
        /// million cells. Now the board-wide version is only the cheap "has anything changed?",
        /// and the chunks whose own <see cref="WorldRenderModel.ChunkVersion"/> moved are the only
        /// ones read again.
        /// </summary>
        readonly Odyssey.Hud.ChunkedCellList _doors;
        readonly Func<int, int> _chunkVersionOf;
        readonly Action<int, List<int>> _scanChunk;
        IReadOnlyList<int> _doorCells => _doors.Cells;
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

            // Both delegates made once, here: EnsureDoorList runs on a version move and must not
            // allocate on each one.
            _doors = new Odyssey.Hud.ChunkedCellList(model.Chunks.Count);
            _chunkVersionOf = model.ChunkVersion;
            _scanChunk = ScanChunkForDoors;

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

        /// <summary>
        /// The door cell whose leaf the selection highlight wants this frame, or -1 (design 44 §3).
        /// Set before <see cref="Sync"/>; the leaf is caught where it is drawn, open or shut.
        /// </summary>
        public int CaptureCell { get; set; } = -1;

        Matrix4x4 _captured;
        bool _hasCaptured;

        /// <summary>The captured leaf's module and placement, when it was drawn this frame.</summary>
        public bool TryGetCapturedLeaf(out ResolvedModule? module, out Matrix4x4 placement)
        {
            placement = _captured;
            module = _hasCaptured && _leafModule != 0 ? _library[_leafModule] : null;
            return module != null && !module.IsEmpty;
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
            _hasCaptured = false;

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
                if (slice.LowersWallsOn(activeLayer, cell.Y)) continue;

                Matrix4x4 root = GroundRelief.Drape(faceFloor) *
                                 Matrix4x4.Rotate(Quaternion.Euler(0f, Directions.Yaw[dir], 0f));
                Vector3 slide = new Vector3(SlideDistance * state.OpenFactor, 0f, 0f);
                Matrix4x4 placement = root * Matrix4x4.Translate(slide);

                AppendPlacement(placement);
                if (cellIndex == CaptureCell) { _captured = placement; _hasCaptured = true; }
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
            _doors.Update(_chunkVersionOf, _scanChunk);
        }

        /// <summary>How many chunks the last door-list update read again. For a test that says an
        /// edit rescans the chunks it touched rather than the board.</summary>
        public int DoorChunksRescanned => _doors.ChunksScannedLastUpdate;

        void ScanChunkForDoors(int chunk, List<int> into)
        {
            _model.ChunkBounds(chunk, out int x0, out int z0, out int y, out int x1, out int z1);
            for (int z = z0; z < z1; z++)
            {
                int row = _model.Index(0, z, y);
                for (int x = x0; x < x1; x++)
                    if (_model.EdificeDef(row + x) == CoreContent.EdificeDoor) into.Add(row + x);
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
