# Plan: Doors and Building Enclosure

## Goal
Implement functional, buildable, auto-sliding doors that allow pawn passage, provide smooth visual opening and closing animations, integrate into the Build menu under Structure, and establish strict room enclosure (sealing a building when all perimeter walls/doors and overhead roofs are intact).

---

## Technical Decisions (from Interview)

1. **Visuals & Animation**:
   - Use the Synty wall doorway frame (`SM_Bld_Base_Wall_Door_01`) placed at the cell floor.
   - Use the sci-fi sliding door leaf (`SM_Prop_Door_01`–`05`), sliding open into the doorway pocket/frame when a pawn passes.
2. **Traversal & Door Open/Close Mechanics**:
   - Automatic sliding door: opens automatically when a pawn approaches or steps into the cell.
   - Traversal charges `MoveCost.DoorOpening` (already defined in `NavGrid`) if closed.
   - Auto-closes after a short timeout (e.g. 30 ticks / 0.5s) once the cell and approach are clear.
   - Locking and "hold open" toggles deferred to a later polish unit.
3. **Room Enclosure & Sealing**:
   - Strict enclosure check: a space is enclosed ("Indoors") if it is horizontally bounded on that layer by walls, closed doors, or solid rock, **and** 100% roofed (every interior cell has solid rock or a built floor slab on the layer immediately above).
   - Displayed on cell inspect (`InspectModel`) as "Environment: Indoors" vs "Environment: Outdoors".
   - Presentation: Outdoor weather/rain blocked overhead, and outdoor ambient audio ducked/attenuated when camera or pawn is inside.
4. **Placement & Orientation**:
   - Free placement on any standable cell (with a floor below).
   - The door automatically detects adjacent walls (`FirstOpenDirection` / wall connectivity) to align its frame and open along the walkway/corridor.
   - Cost: 5 Wood / Stone (matching wall costs), built using `BuildingHandle.Door` (`ui.arch.tool.door`).

---

## Technical Architecture

### 1. Simulation: Building Defs & Construction (`Odyssey.Sim`, `Odyssey.Sim.Contracts`)
- **Catalogue & Handles**:
  - Add `BuildingHandle.Door = 6` to `Assets/Odyssey/Sim.Contracts/Catalogue.cs`.
  - Update `BuildingHandle.Count = 7`.
- **Defs & Content**:
  - Add `Building_Door` to `Assets/Odyssey/Defs/Core/World/Buildings.xml` (edifice: `CoreContent.EdificeDoor` (2), `blocking: false`, `costCount: 5`, `workToBuild: 135`, `minSkill: 0`, `iconKey: ui.arch.tool.door`).
  - Update `ConstructionContent.cs` in-code tables to mirror `Buildings.xml`.
- **Placement & Construction Grid**:
  - In `ConstructionGrid.Allows(cell, BuildingHandle.Door)`: permit on any cell that is inside bounds, has a floor underneath, nothing standing in it, and no water.
  - In `ConstructionGrid.RaiseEdifice`:
    - When `CoreContent.EdificeDoor` is raised: call `_grid.Nav.SetDoor(cell, isDoor: true, open: false)`.
    - In `RemoveEdifice` / deconstruction: call `_grid.Nav.SetDoor(cell, isDoor: false, open: false)`.
  - In `DeconstructJob`: refunds 50% of the material, same as walls.

### 2. Simulation: Traversal & Auto-Open Lifecycle (`Odyssey.Sim`)
- **Door State Tracking**:
  - Track active door ticks in `DoorSystem` (or within `PawnContext` / `NavGraph`):
    - `NavFlags.Door` indicates an edifice door.
    - `NavFlags.DoorOpen` indicates current open state.
    - An integer or byte array `DoorOpenTicks[cell]` counts remaining open ticks.
- **Pawn Traversal**:
  - In `MovementSystem.Advance`: when a pawn moves toward or enters a cell with `NavFlags.Door`, if `NavFlags.DoorOpen` is false:
    - Step cost already adds `MoveCost.DoorOpening` (via `NavGrid.StepCost`).
    - Open the door: `Nav.SetDoorOpen(cell, true)`, reset `DoorOpenTicks[cell] = OpenDurationTicks`.
  - In `DoorSystem.Tick`: decrement `DoorOpenTicks` for open doors. If ticks reach 0 and no pawn occupies `cell`, close door: `Nav.SetDoorOpen(cell, false)`.

### 3. Simulation: Strict Room Enclosure Solver (`Odyssey.Sim`)
- **Enclosure Detection (`EnclosureGrid` / `RoomSolver`)**:
  - Evaluates bounded areas per layer using a flood fill from dirty regions.
  - An interior cell flood is bounded by:
    - `EdificeWall`, `EdificeDoor`, or solid terrain (`TerrainRock`, etc.).
  - Enclosure fails (`Outdoors`) if:
    - Flood reaches the map edge.
    - Flood exceeds maximum room area (e.g. 2,500 cells).
    - Any interior cell lacks an overhead roof (i.e. on layer `y + 1`, `!CellGrid.HasFloor(cell + LayerStride)` and not solid rock).
  - Maintains a bitmask/array `IsEnclosed[cell]`.
  - Invalidated and lazily refreshed when walls, doors, or floor slabs are placed or removed.
- **Published View**:
  - Expose `bool IsIndoors(int cell)` via `WorldSnapshot` / `CellDetail` for presentation and HUD.

### 4. HUD & Interface (`Odyssey.Hud`)
- **Build Palette**:
  - In `PaletteTools.cs`, register `ui.arch.tool.door` under `Structure` category, mapped to `BuildingHandle.Door`.
  - `BuildPalette.cs` displays Door in Structure category.
- **Inspect Pane**:
  - In `InspectModel.SetCellRows`:
    - Add an environment row: `Row(n++, "environment", detail.IsIndoors ? "indoors" : "outdoors")`.

### 5. Presentation: Rendering, Sliding Animation & Audio (`Odyssey.Presentation`)
- **ChunkMesher Door Frame**:
  - `EmitDoor`: Emits `SM_Bld_Base_Wall_Door_01` draped to floor, rotated based on `FirstOpenDirection(x, z, y)` so the frame aligns with adjacent walls.
- **Door Leaf Rendering & Animation (`DoorDirector` or `ChunkRenderer`)**:
  - Render `SM_Prop_Door_01` leaf.
  - Track visual open factor $\alpha \in [0, 1]$ per active door.
  - When open, smoothly slide leaf laterally into the wall frame over ~0.2s.
  - Audio: Trigger `SoundIds.DoorOpen` / `SoundIds.DoorClose` on state transitions.
- **Atmosphere & Weather Gating**:
  - In `WorldRenderModel` / rain emitter: do not spawn or draw rain on cells where `IsIndoors` is true.
  - Audio: Duck outdoor ambient loop when camera focus is inside an enclosed room.

---

## Unit Breakdown

| Unit | Scope | Deliverables | Tests |
|---|---|---|---|
| **U1: Building Defs & Construction** | `Odyssey.Sim`, `Sim.Contracts` | `BuildingHandle.Door`, `Buildings.xml`, `ConstructionContent.cs`, `ConstructionGrid` placement & deconstruction, `NavGraph.SetDoor`. | `ConstructionTests.cs`, `ConstructionContentDefTests.cs` (assert door can be placed, delivered, built, and deconstructed; verify nav flags set). |
| **U2: Traversal & Door Open/Close Lifecycle** | `Odyssey.Sim` | `DoorSystem` / auto-open on approach, timeout auto-close, `MoveCost.DoorOpening` validation. | `DoorMovementTests.cs` (assert closed door charges open cost, opens for pawn, closes after timeout, stays open while occupied). |
| **U3: Strict Room Enclosure Solver** | `Odyssey.Sim` | `EnclosureSystem` per-layer flood fill with wall/door boundary and 100% overhead roof check; publish `IsIndoors` on `CellDetail`. | `RoomEnclosureTests.cs` (assert sealed room is indoors, missing roof tile is outdoors, missing wall/door is outdoors, closed door seals room). |
| **U4: HUD & Build Palette Integration** | `Odyssey.Hud` | Map `ui.arch.tool.door` in `PaletteTools.cs`, update `InspectModel.cs` for environment readout; run wiki & registry gates. | `RegistryTests.cs`, `InspectReadoutTests.cs`, `DesignateDirectorTests.cs`. |
| **U5: Presentation Door Frame & Sliding Leaf Animation** | `Odyssey.Presentation` | Align doorway frame to adjacent walls; render sliding door leaf with smooth open/close lerp; hook audio triggers. | `DoorPresentationTests.cs` (verify frame orientation, leaf offset calculation, audio events). |
| **U6: System Verification & Polish** | Full System | Fast tier (Sim + Hud), EditMode tests, wiki check, label check; verify hash determinism. | Fast tier green, EditMode green, wiki gates clean. |

---

## Verification Plan
1. **Fast Tier**: `bash scripts/test-fast.sh` (Sim + Hud unit tests, state hash determinism).
2. **EditMode Tests**: `bash scripts/unity.sh test editmode` (Presentation, mesher, and rendering tests).
3. **Content Gates**:
   - `python3 tools/wiki/build_wiki.py --check`
   - `python3 tools/wiki/emit_labels.py --check`
