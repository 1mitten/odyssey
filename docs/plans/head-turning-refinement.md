# Plan: Head Turning Refinement — Pure Axial Swivel & Felling Target Elevation

**Governing Design:** `docs/design/23-head-turning-and-gaze.md`  
**Research Reference:** `docs/research/e-07-head-axial-rotation-and-felling-gaze.md`  
**Worktree:** `D:\code\odyssey-motion` on branch `claude/colonist-head-turn`

---

## 1. Context & Motivation

PlayMode testing revealed two distinct visual issues:
1. **Unnatural Head Tilting (Ear-to-Shoulder Roll):** Rotating the `Neck` bone combined with `Head` rotation around torso/chest axes induced severe ear-to-shoulder roll and cervical twisting during movement and chopping. Furthermore, calling `ApplyGazePose` in both `Sync()` and `Evaluate()` caused non-idempotent in-place rotation accumulation.
2. **Chopping Colonists Not Looking at the Tree:** `WorkCentre` was evaluated at the cell floor plane ($Y = 0$). Colonists have eye height at $Y \approx 1.4$ m, so aiming at $Y = 0$ forced the head down to its chin-chest depression limit (staring at the dirt roots) rather than looking at the tree trunk at chest/eye level (~1.3 m).

In the round 2 interview, the owner confirmed:
- Rotate **only the Head bone** sat on its neck (0% Neck, 100% Head).
- Pure left/right yaw swivel around the neck's longitudinal cervical axis, with mathematically 0.0° ear-to-shoulder roll, and subtle up/down pitch only when targeting high/low objects.
- Look at the tree trunk at chest/eye level (~1.3 m height) while chopping.
- Casual left-and-right glances while walking or idling (20°–35° for a couple of seconds, then return forward).

---

## 2. Unit Breakdown

### Unit 1: Pure Head-Only Cervical Axial Kinematics (`HeadLookKinematics.cs`)
- **Objectives:**
  1. Eliminate all rotation on the `Neck` bone (`NeckYawRatio = 0f`, `NeckPitchRatio = 0f`, `HeadYawRatio = 1.0f`, `HeadPitchRatio = 1.0f`).
  2. Implement pure parent-local rotation pre-multiplication on the `Head` bone:
     - In `ApplyAdditiveRotation`:
       $$\Delta R = \operatorname{Quaternion.Euler}(-\text{pitch} \times \text{weight}, \text{yaw} \times \text{weight}, 0f)$$
       $$\text{head.localRotation} = \Delta R \times \text{baseHeadLocalRotation}$$
     - Or in world space relative to the `Neck` transform:
       $$\Delta R_{world} = \operatorname{Quaternion.AngleAxis}(\text{yaw} \times \text{weight}, \text{neck.up}) \times \operatorname{Quaternion.AngleAxis}(-\text{pitch} \times \text{weight}, \text{neck.right})$$
       $$\text{head.rotation} = \Delta R_{world} \times \text{head.rotation}$$
     - Because yaw is applied around `neck.up` (cervical spine axis) and pitch around `neck.right` (transverse ear-to-ear condyle line), roll is identically $0.0^\circ$.
  3. Ensure `SolveAngles` derives target yaw and pitch relative to the `Neck` transform's own orientation (or `Chest`/`Spine` fallback).

### Unit 2: Work Target Elevation & Tree Felling Focus (`PawnFigureDirector.cs`)
- **Objectives:**
  1. In `UpdateFigureGaze`, when `pawn.Working`:
     - Elevate `TargetWorldPosition` by $+1.30$ m above cell floor base:
       $$\mathbf{P}_{target} = \text{figure.WorkCentre} + \operatorname{Vector3.up} \times 1.30f$$
     - This aligns the gaze directly with the tree trunk notch and foliage, or the rock mining face at eye/chest level.
  2. Ensure `figure.Gaze.HasTarget = true` and `figure.Gaze.GazeWeight = figure.WorkWeight`.
  3. Keep the head steady facing the tree trunk during chopping strokes.

### Unit 3: Idempotent Presentation Seam & Drift Elimination (`PawnFigureDirector.Poses.cs`)
- **Objectives:**
  1. Remove `ApplyGazePose` from `Sync()`. Modifying humanoid bone transforms before `Graph.Evaluate()` causes double-application and in-place compounding.
  2. Keep `UpdateFigureGaze(figure, in pawn, deltaTime, running)` in `Sync()` (advancing state timers, selecting glance angles, and integrating smooth damping once per frame).
  3. Execute `ApplyGazePose(deltaTime)` strictly once per frame in `Evaluate()` immediately following `figure.Graph.Evaluate()`.
  4. Ensure base-relative evaluation so running test harnesses or editor frames remains 100% idempotent.

### Unit 4: Test Suite Updates & Authoritative Verification
- **Objectives:**
  1. Update `HeadLookKinematicsTests.cs`:
     - Verify 0% Neck / 100% Head rotation: Neck local rotation remains `Quaternion.identity`, Head receives 100% yaw and pitch.
     - Verify exact 0.0° roll around the line of sight under all yaw and pitch angles.
     - Verify felling gaze target with $+1.30$ m height elevation results in near-level pitch ($[-5^\circ, +2^\circ]$) when standing beside a tree cell.
  2. Run fast test tier: `scripts/test-fast.sh` (Sim + Hud).
  3. Run authoritative Unity EditMode test suite: `scripts/unity.sh test editmode`.
  4. Run authoritative Unity PlayMode test suite: `scripts/unity.sh test playmode`.
  5. Verify content gates: `build_wiki.py --check` and `emit_labels.py --check`.
  6. Update `docs/journal.md`.
