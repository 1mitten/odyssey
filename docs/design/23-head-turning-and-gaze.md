# 23. Head Turning, Gaze Kinematics, and Ambient Motion

*Settled by interview with the owner on 2026-09-18 (Phase 1). Sibling to `12-work-poses-and-tools.md`
and `13-gestures.md`. Extends `PawnFigureDirector`'s procedural transform pose pipeline.*

---

## 1. What was asked and agreed

The request was to make colonists visually expressive and motionful through head and neck movement:
- **Work focus:** Looking towards what they are working on (the tree when chopping, the rock when mining, the frame when building, the ground when sowing/harvesting).
- **Ambient movement:** While walking or standing, glancing in various directions occasionally rather than staring rigidly straight ahead.
- **Contextual social awareness:** Glancing towards fellow colonists when crossing paths in corridors or outdoors.
- **Vertical and secondary actions:** Looking up/down ladders and vertical shafts during climbs, resting heads naturally during sleep, and pitching the head during hauling and eating.

The agreed parameters from the Phase 1 interview and subsequent round 2 clarification:
1. **Work target:** Continuous dynamic gaze directed at the work object (tree trunk at chest/eye level ~1.3m height, rock face at impact level) rather than the cell floor roots (Option A).
2. **Ambient glances:** Casual left-and-right glances: occasionally turn the head smoothly left or right (20°–35°) for a couple of seconds, then return to forward (Option A).
3. **Bone distribution:** Rotate **only the Head bone** sat on its neck (0% Neck, 100% Head). The Neck remains steady while the Head turns purely left/right (yaw) around the neck's longitudinal cervical axis and pitches up/down on the transverse condyle axis, with mathematically 0.0° ear-to-shoulder roll (Option A).
4. **Motion limits & speed:** Natural human limits (yaw $\pm 60^\circ$, pitch down $-40^\circ$, pitch up $+35^\circ$) with critically damped smooth damping (~$240^\circ$/s to $300^\circ$/s) (Option A).
5. **Additional actions:** Ladder climbing look up/down, sleep head posture, hauling downward pitch, and eating downward pitch (Option A, all included).

---

## 2. Architecture & Seams

### The Presentation Boundary
Gaze, head orientation, and ambient glances are **100% presentation-side** in `Odyssey.Presentation.World`.
- `Odyssey.Sim` and `Odyssey.Sim.Contracts` have no concept of gaze angles, eye contact, or head direction.
- Nothing simulated reads head rotation, and nothing touches saves, tick loops, or state hashes.
- The governing rule holds: *"Nothing in presentation is in a cell, a save or the hash."*

### The Pose Evaluation Seam
In `PawnFigureDirector`, figure posing executes immediately after `PlayableGraph.Evaluate()`:
1. `ApplyFooting()` adjusts hips and leg IK so feet plant on relief.
2. `ApplyWorkPose()` executes tool swings, climbing poses, sleep poses, and gestures.
3. `ApplyGazePose()` calculates and applies additive local head rotations on top of the base animation clip.

**The Idempotent Pose Rule:**
Procedural bone rotations written to humanoid transforms are overwritten whenever the PlayableGraph evaluates.
- `UpdateFigureGaze(figure, ...)` runs in `Sync()` once per frame to advance timers, update targets, and integrate smooth damping (`Mathf.SmoothDampAngle`).
- Bone transforms are **never modified in `Sync()`**. Modifying transforms in both `Sync()` and `Evaluate()` causes non-idempotent in-place rotation compounding (where a transform is multiplied onto an already-rotated bone), creating non-zero Lie bracket commutator drift that manifests as unnatural ear-to-shoulder tilt and roll.
- `ApplyGazePose()` runs strictly after `figure.Graph.Evaluate()` in `Evaluate()` (and editor harness calls), applying the head orientation relative to the evaluated base clip rotation.

---

## 3. Kinematics: Head-Only Cervical Axial Look-At

### Reference Frame Decoupling
To prevent body roll, slope lean, or crouch pitch from corrupting head yaw into axial roll:
1. Compute the world direction from the head pivot $\mathbf{P}_{head}$ to target $\mathbf{P}_{target}$:
   $$\mathbf{v}_{world} = \frac{\mathbf{P}_{target} - \mathbf{P}_{head}}{\|\mathbf{P}_{target} - \mathbf{P}_{head}\|}$$
2. Transform $\mathbf{v}_{world}$ into the coordinate frame of the `Neck` bone (or `Chest`/`Spine` if neck is absent):
   $$\mathbf{v}_{local} = \mathbf{T}_{neck}^{-1} \cdot \mathbf{v}_{world}$$
3. Compute local azimuth (yaw $\psi$) and elevation (pitch $\theta$):
   $$\psi = \operatorname{atan2}(v_{local}.x, v_{local}.z) \times \frac{180}{\pi}$$
   $$\theta = \operatorname{atan2}(v_{local}.y, \sqrt{v_{local}.x^2 + v_{local}.z^2}) \times \frac{180}{\pi}$$

### Head-Only Axial Rotation Sat on Neck
Rotating the Neck bone on low-poly humanoid characters twists the collar mesh and causes eccentric conical head precession when the neck rests with natural forward lordosis (~15° tilt).
Instead, the Neck remains stationary (0%), and the Head bone alone rotates (100%) sat directly on top of the neck:
- **Yaw (swivel left/right):** Rotates purely around the neck's longitudinal cervical axis (`neck.up` in world space, or `Vector3.up` in neck-local coordinates).
- **Pitch (nod up/down):** Rotates purely around the transverse condyle axis (`neck.right` in world space, or `Vector3.right` in neck-local coordinates).
- **Roll:** Exactly $0.0^\circ$. By pre-multiplying in the Neck's local coordinate frame:
  $$Q_{delta} = \operatorname{Quaternion.Euler}(-\theta \cdot W, \psi \cdot W, 0)$$
  $$R_{headLocal} = Q_{delta} \times R_{baseHeadLocal}$$
  The head turns cleanly left and right sat on its neck without any ear-to-shoulder tilt.

### Work Target Elevation (Tree Felling & Rock Mining)
Cell coordinates in Odyssey are grounded at the floor plane ($Y = 0$). Colonist eye height sits at $Y \approx 1.35$–$1.45$ m.
- Aiming directly at `FloorCentre(cell)` produces a downward gaze of $\approx -35^\circ$, forcing the head to its maximum chin-chest depression limit and staring at the roots in the dirt.
- For tree felling, mining, and building, the look-at target is elevated to chest/eye level:
  $$\mathbf{P}_{workTarget} = \mathbf{P}_{workCellFloor} + \begin{pmatrix} 0 \\ 1.30\,\text{m} \\ 0 \end{pmatrix}$$
- This levels the colonist's gaze directly at the tree trunk notch or rock face being worked.

### Rear Cutoff & Dead Zone
When a target passes behind the colonist ($|\psi| > 60^\circ$), clamping at $60^\circ$ would cause unnatural peripheral fixation. A cubic smoothstep window over $60^\circ \to 95^\circ$ smoothly transitions gaze weight to zero:
$$t = \operatorname{clamp01}\left(\frac{|\psi| - 60^\circ}{95^\circ - 60^\circ}\right)$$
$$W_{rear} = 1.0 - (3t^2 - 2t^3)$$

---

## 4. Gaze Priority Arbiter

When multiple stimuli compete for a colonist's attention, averaging produces disoriented, cross-eyed poses. An explicit 6-tier priority arbiter resolves the active target with strict pre-emption:

| Tier | Priority | Trigger Condition | Target & Angles | Dwell / Behavior |
|---|---|---|---|---|
| **6** | `SleepLock` | `pawn.Asleep` / `SleepWeight > 0.001` | Gaze disabled ($W=0$); head conformed to bed/mattress plane | Entire sleep duration |
| **5** | `LadderTraversal` | `figure.ClimbPhase >= 0` | Climb up: Pitch $+30^\circ$; Climb down: Pitch $-35^\circ$; Yaw $\pm 10^\circ$ max | Entire climb duration |
| **4** | `WorkFocus` | `pawn.Working` | Target = `figure.WorkCentre` (tree notch, rock face, building site) | Entire work duration |
| **3** | `SocialPassing` | Colonist within 6 m (~2.4 cells), $|\Delta y| \le 1.5$ m, in forward cone ($\cos > 0.42$) | Target = Fellow colonist's chest/head | 1.0–1.8 s dwell, 20 s cooldown |
| **2** | `AmbientWander` | Walking or idle; no social/work target | Lateral glance $\pm 15^\circ..\pm 35^\circ$, pitch $-8^\circ..+15^\circ$ | 3–6 s forward dwell, 1–2 s glance |
| **1** | `PathForward` | Default baseline | Forward movement heading or body `TargetYaw` | Continuous |

### Special Postures
- **Hauling:** When hauling a commodity or item, the baseline pitch is offset by $-12^\circ$ (looking towards the path 3 m ahead), conveying load weight.
- **Eating:** When eating meals, pitch is offset by $-25^\circ$ down towards food.

---

## 5. Performance & Zero Allocations

1. **Figure State Footprint:** Added directly to `PawnFigureDirector.Figure`:
   - `Transform? Head`, `Transform? Neck`, `Transform? Chest` (bound once at figure build time).
   - `LookGazeState Gaze` (44 bytes value type holding current angles, velocities, timers, and active priority).
2. **Pairwise Social Check:** Evaluated in presentation over the active figures list (`_figures`). For $N \le 64$ figures (typically 5–15 on screen), flat iteration does at most 45–100 distance checks per frame, costing $<0.005$ ms with zero GC allocations.
3. **Deterministic Seed Hashing:** Pseudo-random intervals are generated using integer hashes of `(pawnId, glanceIndex)` to ensure decorrelated, non-lockstep head turning without runtime heap allocations.
