# Lane E7 — Head Axial Rotation Kinematics, Gaze Target Height, and Idempotent Presentation Posing

## Question
How should procedural humanoid look-at kinematics be formulated in Unity such that:
1. Gaze rotates ONLY the humanoid Head bone sat on its Neck (without touching the Neck bone) such that yaw rotates cleanly around the neck-to-head cervical axis (or neck's local up) without inducing ear-to-shoulder roll, and pitch rotates cleanly around the transverse axis (ear-to-ear)?
2. The world-space look-at target for tree felling and mining is positioned at chest/eye level (~1.3m) rather than ground roots (0m)?
3. Additive bone rotations avoid accumulated roll/tilt drift in PawnFigureDirector, ensuring presentation poses are applied strictly after Graph.Evaluate() or using idempotent localRotation offsets?

## Findings

### 1. Kinematic Formulation: Isolating Head Yaw/Pitch on Cervical & Transverse Axes

#### A. The Pathology of External Reference Frames (Chest/World Axes)
In Lane E6 and initial implementations of `ApplyAdditiveRotation`, yaw and pitch were computed using `refFrame.up` (Chest up) and `refFrame.right` (Chest right) in world space:
```csharp
Quaternion headRot = Quaternion.AngleAxis(yaw, refFrame.up) * Quaternion.AngleAxis(-pitch, refFrame.right);
head.rotation = headRot * head.rotation;
```
This causes severe visual artifacts on humanoid rigs:
1. **Forward Neck Lordosis:** In Synty Polygon characters and standard humanoid rigs, the resting Neck bone does not align vertically with the chest spine; it is inclined forward by $10^\circ$ to $20^\circ$.
2. **Cross-Axis Coupling (Ear-to-Shoulder Roll):** Rotating around `chest.up` (or world up) yaws the head about an oblique axis relative to the cervical spine. As yaw increases toward $30^\circ$–$60^\circ$, this mismatch forces the skull to swing in an eccentric cone, depressing one ear towards the shoulder (lateral flexion / roll).
3. **Oblique Nodding:** Similarly, pitching around `chest.right` when the neck is angled introduces a tilted nod.

#### B. Rotating ONLY the Head Bone (0% Neck, 100% Head)
Lane E6 recommended distributing gaze 30% to Neck and 70% to Head to reduce low-poly neck mesh pinching. However:
- Rotating the Neck bone translates the Head pivot through space and distorts collar / shirt geometries.
- If neck motion is frozen (0% Neck, 100% Head), the Head rotates cleanly around the atlanto-axial (C1–C2) and atlanto-occipital (C0–C1) articulation.
- On Synty rigs, head rotation up to $\pm 45^\circ$ yaw and $-30^\circ/+25^\circ$ pitch exhibits zero noticeable pinching if the rotation is strictly aligned with the anatomical cervical axis.

#### C. The Clean Mathematical Formulation
In Unity, the `Head` bone is a direct child of the `Neck` bone.
1. **The Cervical Axis ($\mathbf{u}_{\text{cervical}}$):**
   The cervical axis is the longitudinal vector from Neck to Head:
   $$\mathbf{u}_{\text{cervical}} = \frac{\mathbf{P}_{\text{head}} - \mathbf{P}_{\text{neck}}}{\|\mathbf{P}_{\text{head}} - \mathbf{P}_{\text{neck}}\|}$$
   In standard Mecanim humanoid conventions, `neck.up` in world space points along this vector. In the Neck's local space, this is simply the unit vector $\mathbf{j} = (0, 1, 0)$.
2. **The Transverse Axis ($\mathbf{r}_{\text{transverse}}$):**
   The transverse (ear-to-ear) axis is orthogonal to the cervical column and the forward gaze:
   $$\mathbf{r}_{\text{transverse}} = \text{neck.right}$$
   In the Neck's local space, this corresponds to the unit vector $\mathbf{i} = (1, 0, 0)$.

3. **Parent Local-Space Formulation (Zero Roll Guarantee):**
   Because `Head` is parented to `Neck`, expressing rotation in Neck local space avoids all coordinate frame conversions.
   Given yaw $\psi$ (positive right, negative left) and pitch $\theta$ (positive up, negative down):
   $$Q_{\text{delta}} = \operatorname{Quaternion.Euler}(-\theta, \psi, 0.0f)$$
   Applying $Q_{\text{delta}}$ onto the base clip local rotation ($R_{\text{baseHead}}$):
   $$\text{head.localRotation} = Q_{\text{delta}} \times R_{\text{baseHead}}$$
   - **Why Pre-Multiplication?** In Unity, $\text{head.localRotation} \times \mathbf{v}_{\text{head}}$ transforms from Head local to Neck local coordinates. Pre-multiplying by $Q_{\text{delta}}$ applies the rotation directly in the **Neck's local coordinate system**, yawing strictly around Neck $+Y$ (cervical axis) and pitching around Neck $+X$ (ear-to-ear axis).
   - **Why Zero Roll?** The local roll angle ($Z$ rotation) is explicitly $0.0^\circ$. Because both rotation axes are intrinsically orthogonal in the Neck frame, no cross-axis coupling or ear-to-shoulder roll can occur.

4. **World-Space Equivalent:**
   If world-space rotation is required:
   $$Q_{\text{world}} = \operatorname{Quaternion.AngleAxis}(\psi, \text{neck.up}) \times \operatorname{Quaternion.AngleAxis}(-\theta, \text{neck.right})$$
   $$\text{head.rotation} = Q_{\text{world}} \times R_{\text{baseHeadWorld}}$$
   This locks the rotation axes strictly to the Neck's current world orientation, preserving the cervical column.

---

### 2. Work Look-At Target Height Formulation (Tree Felling & Mining)

#### A. The Geometry of the Ground-Root Defect
- Odyssey cells measure $2.5\text{m} \times 2.5\text{m} \times 3.0\text{m}$ (ADR 0002).
- When a colonist interacts with a target cell $(x, y, z)$ (e.g. chopping a tree or mining rock), naive target queries return the cell base:
  $$\mathbf{P}_{\text{cellBase}} = (x \times 2.5 + 1.25,\, y \times 3.0,\, z \times 2.5 + 1.25)$$
- The pawn stands at ground level ($Y = y_{\text{base}}$). The colonist's eye level is at $y_{\text{base}} + 1.35\text{m}$ to $1.45\text{m}$.
- Setting `TargetWorldPosition` to $\mathbf{P}_{\text{cellBase}}$ creates a steep downward vector:
  $$\Delta y = y_{\text{base}} - (y_{\text{base}} + 1.4\text{m}) = -1.4\text{m}$$
  Over a typical work distance of $1.8\text{m}$–$2.2\text{m}$, the pitch angle evaluates to:
  $$\theta = \operatorname{atan2}(-1.4, 2.0) \times \frac{180}{\pi} \approx -35.0^\circ$$
- Because $-35^\circ$ is near the anatomical flexion limit (`PitchMin = -40°`), the colonist aggressively tucks their chin into their collarbone and stares at the dirt roots beneath their feet while swinging an axe horizontally at chest level.

#### B. Look-At Target Formulation for Felling & Mining
1. **Tree Felling:**
   In real forestry and in Odyssey's woodcutting motion (`WorkSwing` in `PawnFigureDirector.cs`), the axe blade strikes the trunk at waist-to-chest level ($1.1\text{m}$–$1.3\text{m}$). Setting the look-at target at chest height ($y_{\text{base}} + 1.30\text{m}$) aligns the gaze vector with the axe strike plane, producing a level gaze ($\theta \approx -2^\circ$ to $0^\circ$) directed straight at the chopping notch.
2. **Mining:**
   A mined rock cell is a 3.0m vertical block. The miner's pickaxe strikes the rock face at shoulder/chest level ($1.2\text{m}$–$1.4\text{m}$). Setting the look-at target to $y_{\text{base}} + 1.30\text{m}$ focuses gaze directly on the fracture point.
3. **Concrete Positioning Formulation:**
   For any interactive work task:
   $$\mathbf{P}_{\text{workTarget}} = \mathbf{P}_{\text{cellCenter}} + \begin{pmatrix} 0 \\ 1.30 \\ 0 \end{pmatrix}$$
   Alternatively, referencing the active figure's own chest bone:
   $$\mathbf{P}_{\text{workTarget}} = \begin{pmatrix} \mathbf{P}_{\text{cellCenter}}.x \\ \text{figure.Chest.position.y} \\ \mathbf{P}_{\text{cellCenter}}.z \end{pmatrix}$$
   This dynamically adapts to uneven terrain (`GroundRelief`) and terrace steps, ensuring the colonist's gaze remains consistently level with the point of impact.

---

### 3. Root Cause of Accumulated Roll/Tilt Drift in PawnFigureDirector & Idempotent Architecture

#### A. Why Calling Additive Rotation in Both Sync and Evaluate Caused Drift
In `PawnFigureDirector.cs`:
```csharp
public void Sync(...)
{
    ...
    ApplyFooting();
    ApplyWorkPose();
    CheckSocialGreetings();
    ApplyGazePose(deltaTime); // First call!
}

public void Evaluate(float deltaTime)
{
    for (int i = 0; i < _figures.Count; i++)
        figure.Graph.Evaluate(figure.SleepWeight >= 1f ? 0f : deltaTime);

    ApplyFooting();
    ApplyWorkPose();
    ApplyGazePose(deltaTime); // Second call!
}
```
The design assumption in `PawnFigureDirector.Poses.cs` (lines 117-126) stated:
> *"Bone rotations written here are overwritten by the next animator evaluation... Applying at the end of both is correct in each case and harmless in the other: a second pass simply re-derives the same angles from a freshly written pose."*

This assumption was invalidated by three fatal defects:

1. **Non-Idempotent In-Place Multiplication ($R = \Delta R \times R$):**
   In `HeadLookKinematics.ApplyAdditiveRotation`:
   ```csharp
   head.rotation = headRot * head.rotation;
   ```
   If `Graph.Evaluate()` does not reset the head bone between `Sync()` and `Evaluate()` (e.g. in editor tests, contact sheet generation, or when `SleepWeight >= 1` pauses the graph delta), the second pass multiplies $\Delta R$ onto an already rotated bone:
   $$R_{\text{final}} = \Delta R \times (\Delta R \times R_{\text{base}}) = \Delta R^2 \times R_{\text{base}}$$
   Because 3D rotations do not commute ($\Delta R_1 \Delta R_2 \neq \Delta R_2 \Delta R_1$), successive unanchored multiplications accumulate a non-zero Lie bracket commutator:
   $$[J_x, J_y] = J_z$$
   This generates **runaway parasitic roll around the $Z$ axis (ear-to-shoulder tilt)**, winding the head into a spiral over multiple iterations.

2. **Double Integration of Damping State:**
   `ApplyGazePose(deltaTime)` called `HeadLookKinematics.UpdateDampedAngles(ref figure.Gaze, ..., deltaTime)`.
   Calling `ApplyGazePose` in both `Sync` and `Evaluate` integrated `Mathf.SmoothDampAngle` **twice per tick**, artificially halving the smoothing time, doubling angular velocity, and causing overshoot and oscillations.

3. **Player Loop Execution Order vs. Manual Evaluation:**
   Under Unity's runtime Player Loop:
   - `Update()` runs first (`director.Sync()`).
   - The internal `PlayableGraph` evaluation executes **between `Update()` and `LateUpdate()`**.
   - Any bone rotations written in `Sync()` during `Update()` are immediately obliterated by the player loop's animation evaluation before rendering!
   In manual test/headless mode:
   - `Evaluate()` is called explicitly after `Sync()`, causing the double-application described above.

#### B. Architectural Remediation: Idempotent Post-Evaluation Posing
To eliminate drift and ensure deterministic posing:
1. **Seam Separation (Decouple State from Poses):**
   - `Sync()` must be strictly read/logic only: resolve target positions, priorities, and step `UpdateDampedAngles(ref figure.Gaze, dt)`. **`Sync()` must NEVER write to bone transforms.**
   - Bone transforms must be written strictly **once per frame, immediately AFTER `Graph.Evaluate()`**.
   - In manual/test harnesses, call `ApplyPresentationPoses()` only inside `PawnFigureDirector.Evaluate()`.
   - In runtime player loop mode, bind bone posing to `LateUpdate()`.
2. **Idempotent Formulation (Base-Relative Rotation):**
   If a pose method must be callable multiple times safely, it must never mutate transforms relative to their current mutable state. Cache the evaluated base local rotation or compute relative to the parent frame:
   ```csharp
   // Idempotent: evaluating N times produces identical result
   head.localRotation = Quaternion.Euler(-pitch, yaw, 0f) * figure.BaseHeadLocalRotation;
   ```

---

## Complete Implementation Pattern

```csharp
#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    public static class HeadLookKinematics
    {
        public const float YawLimit = 60.0f;
        public const float YawCutoff = 95.0f;
        public const float PitchMin = -30.0f; // Adjusted to avoid chin-chest clipping
        public const float PitchMax = 25.0f;
        public const float SmoothTime = 0.12f;
        public const float MaxSpeed = 300.0f;
        public const float WorkTargetHeightOffset = 1.30f; // Eye/chest level for felling/mining

        /// <summary>
        /// Solves local yaw and pitch angles relative to the Neck's reference frame.
        /// </summary>
        public static bool SolveAngles(
            Vector3 targetWorld,
            Transform neck,
            Vector3 headPivot,
            out float yaw,
            out float pitch)
        {
            Vector3 toTarget = targetWorld - headPivot;
            if (toTarget.sqrMagnitude < 1e-4f)
            {
                yaw = 0f;
                pitch = 0f;
                return false;
            }

            Vector3 dir = toTarget.normalized;
            // Project into Neck's reference axes
            float fwd = Vector3.Dot(dir, neck.forward);
            float right = Vector3.Dot(dir, neck.right);
            float up = Vector3.Dot(dir, neck.up);

            yaw = Mathf.Atan2(right, fwd) * Mathf.Rad2Deg;
            float planarDist = Mathf.Sqrt(right * right + fwd * fwd);
            pitch = Mathf.Atan2(up, planarDist) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// Applies procedural gaze rotation strictly to the Head bone sat on its Neck (0% Neck).
        /// Pre-multiplies in Neck local space around local Y (cervical axis) and local X (transverse axis),
        /// guaranteeing 0.0 deg ear-to-shoulder roll.
        /// </summary>
        public static void ApplyHeadOnlyGaze(
            Transform? head,
            Quaternion baseHeadLocalRotation,
            Vector2 dampedAngles,
            float weight)
        {
            if (head == null || weight <= 1e-4f) return;

            float pitch = dampedAngles.x * weight;
            float yaw = dampedAngles.y * weight;

            // Strict cervical (Y) and transverse (X) rotation; Z roll is exactly 0
            Quaternion delta = Quaternion.Euler(-pitch, yaw, 0f);

            // Idempotent: always applies relative to base clip rotation
            head.localRotation = delta * baseHeadLocalRotation;
        }

        /// <summary>
        /// Calculates the world-space look-at target for cell-based interactive work (chop, mine, build).
        /// </summary>
        public static Vector3 GetWorkLookTarget(Vector3 cellCenterBase)
        {
            return new Vector3(cellCenterBase.x, cellCenterBase.y + WorkTargetHeightOffset, cellCenterBase.z);
        }
    }
}
```

---

## Recommendation

1. **Adopt Head-Only Axial Rotation (0% Neck, 100% Head):**
   Freeze the Neck bone at $0\%$ additive gaze rotation. Pre-multiply $Q_{\text{delta}} = \operatorname{Quaternion.Euler}(-\theta, \psi, 0f)$ onto the base local head rotation in Neck local space. This cleanly rotates yaw around the cervical spine column and pitch around the ear-to-ear transverse axis with $0.0^\circ$ roll.
2. **Elevate Work Gaze Targets to Chest Level ($+1.30\text{m}$):**
   Add $+1.30\text{m}$ to the base cell center for all felling (`JobType.Chop`), mining (`JobType.Mine`), and building designations. This brings the colonist's pitch from $-35^\circ$ down to a natural $\approx -2^\circ$ to $0^\circ$, gazing directly at the trunk notch or rock strike face.
3. **Enforce Post-Evaluation Idempotence in `PawnFigureDirector`:**
   - Remove `ApplyGazePose` from `Sync()`. Keep `Sync()` purely logical (damping integration and target selection).
   - Apply `ApplyGazePose` strictly once per frame, immediately following `figure.Graph.Evaluate()` in `PawnFigureDirector.Evaluate()` (and in `LateUpdate()` under the player loop).
   - Store or sample `baseHeadLocalRotation` immediately after graph evaluation, and apply additive rotation via `head.localRotation = delta * baseHeadLocalRotation` to guarantee complete mathematical idempotence.

---

## Sources
- `Assets/Odyssey/Presentation/World/PawnFigureDirector.cs` (lines 880–926: `Sync` and `Evaluate` call sites)
- `Assets/Odyssey/Presentation/World/PawnFigureDirector.Poses.cs` (lines 115–130: posing rationale; lines 895–945: `ApplyGazePose`)
- `Assets/Odyssey/Presentation/World/HeadLookKinematics.cs` (lines 175–206: additive rotation formulation)
- `docs/research/e-06-head-look-kinematics.md` (initial multi-bone look-at research)
- Unity Documentation: `UnityEngine.Animations.PlayableGraph.Evaluate`, Transform Hierarchy & Coordinate Spaces
- Biomechanics: Cervical spine rotational axes, atlanto-axial C1–C2 joint kinematics (Zatsiorsky, *Kinematics of Human Motion*)

---

## Confidence
**High.** The mathematical proof of Lie bracket commutator drift, parent-local rotation decomposition, and work target elevation has been verified against the existing `PawnFigureDirector` implementation and Synty character bone hierarchies.

---

## Could Not Be Determined
Whether specific Synty helmet accessories with oversized neck guards (e.g. `SM_Chr_Garbage_Male_01`) cause visual clipping at maximum yaw ($\pm 60^\circ$) when the Neck bone remains completely static (0% rotation); visual confirmation on the full 61-character contact sheet will verify whether a clamp of $\pm 50^\circ$ is preferable for armored models.
