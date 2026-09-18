# 23 — Flush Selection Cursor: Sitting Flush on Terrain, Floors, Water, and Banks

**Status: Designed 2026-09-18 following owner interview.**

## 1. Problem Statement

When selecting empty ground, floor slabs, or terrain in the world, the white selection cursor (bracket) rarely fits flush on top of the surface. On sloped terrain or angled surfaces, parts of the cursor stubs sink into the terrain and become obscured, while the opposite edges hover high in the air.

### The Root Causes in `ChunkRenderer.DrawFloorBracket` & `DrawCellHighlight`:

1. **Unsheared / Unrotated Stubs (`Quaternion.identity`):**
   Natural terrain and built floors are rendered with `GroundRelief.Drape(CellMetrics.FloorCentre(x, z, y))`, which applies a shear matrix `(m10 = slopeX, m12 = slopeZ)` to tilt the cell onto the tangent plane of the rolling ground relief. However, `DrawFloorBracket` places its 8 corner stubs with `Quaternion.identity` (pure horizontal orientation) and an initial vertical clearance of only 5 mm (`centre.y = FloorCentre + 0.04m`, stub half-thickness `0.035m`, leaving `0.04 - 0.035 = 0.005m`).
   On the wooded meadow board (`BoardAmplitude = 2.0m`), ground slopes reach ~8°. Over a stub length of 0.45m–0.55m, an 8° slope rises/drops by ~70 mm (7 cm). Because the stub is rigid and horizontal, it penetrates 6.5 cm into the ground on one side and hovers 7.5 cm above the ground on the other.

2. **Mismatched Corner Sampling vs. Tangent Plane:**
   `DrawFloorBracket` called `GroundRelief.Lift` independently at each of the 4 cell corners. Because the ground relief is a sum of sinusoids with curvature, the 4 independent heights disagree with the cell's actual planar mesh (which is a flat tangent plane sheared at cell center).

3. **Surface Elevation Differences Across Surface Kinds:**
   - **Floor slabs & bare ground:** Nominal walking surface is at cell floor level (`y = 0`).
   - **Water:** Water surface is rendered at `lift = CellMetrics.SizeY * ChunkMesher.WaterSurface` (0.72 × 3.0 m = 2.16 m above the cell floor).
   - **Banks:** Bank risers are earth ramps climbing 3.0 m across a 2.5 m cell (`slope = 1.2`), draped along the ground relief and rotated by `Directions.Yaw[bank.Rotation]`.

---

## 2. Mathematical Model for Flush Geometry

### Decision 1: Draped Corner Stubs in Cell-Local Space

Rather than computing world-space positions with `GroundRelief.Lift` and placing flat cubes with `Quaternion.identity`, the cursor geometry is defined in **cell-local coordinates** and transformed by the surface's placement matrix:

$$\mathbf{M}_{\text{placement}} = \text{GroundRelief.Drape}(\mathbf{C}_{\text{surface}})$$

Where $\mathbf{C}_{\text{surface}} = \text{CellMetrics.FloorCentre}(x, z, y) + \begin{pmatrix} 0 \\ \text{rise} \\ 0 \end{pmatrix}$.

In cell-local coordinates, the cell spans $x \in [-1.25, 1.25]$, $z \in [-1.25, 1.25]$, and the walking/water surface is at $y = 0$.

For each of the 4 corners $(s_x, s_z)$ where $s_x \in \{-1, +1\}, s_z \in \{-1, +1\}$:
- Length of stub: $L = \max(\text{SizeXZ} \times \text{BracketStub}, \text{BracketThickness}) \approx 0.45\text{ m}$.
- Thickness: $T = \text{BracketThickness} = 0.07\text{ m}$.
- Bias above surface: $B = 0.008\text{ m}$ (8 mm clearance to eliminate z-fighting with the ground mesh).
- Local vertical center: $y_{\text{center}} = \frac{T}{2} + B = 0.043\text{ m}$.

For the stub along the X-axis:
$$\text{localOffset}_X = \begin{pmatrix} s_x \cdot (1.25 - \frac{L}{2}) \\ y_{\text{center}} \\ s_z \cdot (1.25 - \frac{T}{2}) \end{pmatrix}, \quad \text{scale}_X = \begin{pmatrix} L \\ T \\ T \end{pmatrix}$$

For the stub along the Z-axis:
$$\text{localOffset}_Z = \begin{pmatrix} s_x \cdot (1.25 - \frac{T}{2}) \\ y_{\text{center}} \\ s_z \cdot (1.25 - \frac{L}{2}) \end{pmatrix}, \quad \text{scale}_Z = \begin{pmatrix} T \\ T \\ L \end{pmatrix}$$

The final instance matrix for each stub is:
$$\mathbf{M}_{\text{instance}} = \mathbf{M}_{\text{placement}} \times \text{ScaledAt}(\text{localOffset}, \text{scale})$$

### Proof of Uniform Clearance:
Under the shear matrix $\mathbf{M}_{\text{placement}}$:
$$\text{world } y = C_y + \text{height} + \text{local } y + \text{slope}_X \cdot \text{local } x + \text{slope}_Z \cdot \text{local } z$$
The drawn terrain/floor mesh top surface has $\text{local } y = 0$.
The bottom face of the stub has $\text{local } y = y_{\text{center}} - \frac{T}{2} = B$.
Therefore:
$$\text{distance} = y_{\text{stub bottom}} - y_{\text{surface}} = B \equiv \text{constant}$$
Across all 8 stubs, across all corners, regardless of the terrain slope (0° to 20°), the bottom of the cursor stub is always exactly $B$ (8 mm) above the surface. No part of the bracket ever clips into the terrain.

---

## 3. Surface Height Resolution

When drawing the selection cursor in `OdysseyBootstrap.DrawSelectionCursor`:
1. **Pawn:** Existing figure bracket.
2. **Item:** Existing item bounds bracket.
3. **Bed:** Existing bed bounds bracket.
4. **Solid Wall / Edifice:** Existing 3D cell highlight (updated to use `GroundRelief.Drape`).
5. **Surface / Floor / Terrain:**
   - If `WaterLine.IsWater(_model, cell)`:
     $$\text{rise} = \text{CellMetrics.SizeY} \times \text{ChunkMesher.WaterSurface}$$
     $$\mathbf{M}_{\text{placement}} = \text{GroundRelief.Drape}(\text{FloorCentre} + \text{rise} \cdot \hat{\mathbf{y}})$$
   - Else if bank exists in cell (`BankLayout.At(_model, cell).Exists`):
     Bank surface transform:
     $$\mathbf{M}_{\text{placement}} = \text{GroundRelief.Drape}(\text{FloorCentre}) \times \text{Rotate}(\text{Yaw}) \times \text{BankSurfaceShear}(\text{bank})$$
   - Else (floor slab, paved street, bare earth, meadow grass):
     $$\mathbf{M}_{\text{placement}} = \text{GroundRelief.Drape}(\text{FloorCentre})$$

---

## 4. Test Strategy

1. **Unit tests in `Presentation/Tests/SelectionCursorTests.cs` (fast and deterministic):**
   - Verify that all 8 stubs generated by `DrawFloorBracket` lie on the tangent plane of `GroundRelief.Drape`.
   - Verify that at arbitrary slopes (e.g. 0 m amplitude, 2 m amplitude), every vertex on the bottom face of every stub has positive clearance above the terrain mesh top.
   - Verify that on water cells, the placement elevation matches `WaterSurface`.
   - Verify that on bank cells, the bracket aligns with the bank slope.
2. **Fast tier run:** `scripts/test-fast.sh` proves 0 regressions in Sim/Hud tests.
3. **Unity EditMode run:** `scripts/unity.sh test editmode` confirms complete compilation and passing tests.
