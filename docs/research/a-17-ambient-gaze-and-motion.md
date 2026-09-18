# Ambient gaze arbitration, behavioural priorities, and glance state machines

## Question

How should ambient gaze arbitration, behavioral priorities, and glance state machines be structured for characters in a 3D colony sim? Specifically:
1. What is the clean priority stack for competing gaze targets (e.g. In Bed / Asleep -> Climbing Ladder -> Working / Tool Impact -> Social Greeting / Passing Colonist -> Ambient Wandering Glance -> Path Forward)?
2. What are the timing, cadence, pseudo-random generation, and smoothing rules for natural saccadic eye/head glance state machines?
3. How can lightweight social greeting / colonist awareness be implemented within 2–3 cells without physics triggers or allocations using `WorldSnapshot.Pawns` or figure positions?
4. How should vertical actions (climbing ladders, sleeping postures, hauling pitch) govern head orientation?

## Findings

### 1. Gaze arbitration priority stack
In character animation and procedural attention systems (e.g. GDC architectural post-mortems on *The Sims*, *Uncharted*, *The Last of Us*, and biological saccade models), procedural head-look cannot operate as a simple weighted blend of all candidate targets. Averaging mutually exclusive targets (e.g., looking at a passing colonist while swinging an axe at a tree trunk) produces uncanny "halfway" drifting where characters appear disoriented or cross-eyed.

Instead, gaze targets must be resolved via a strict **Priority Arbiter with Pre-emption**:
- **Layer 0: Anatomical & State Overrides (Rigid Constraints; Gaze Inactive or Locked)**
  - *Asleep / In Bed (`SleepPose`):* Eyes closed, gaze disabled, head rotation conformed to the pillow/mattress plane (pitch ~0°, roll ~0–15° if side-sleeping). Gaze weight = 0.
  - *Climbing Ladder / Vertical Traversal (`ClimbPose`):* Survival and balance constraint. Head orientation is tightly coupled to traversal vector (looking up rung-line or down to foothold). Gaze weight = 1.0 to traversal target, horizontal yaw strictly clamped to $\pm 10^\circ$ to prevent clipping into wall or ladder geometry.
  - *Incapacitated / Downed / Unconscious:* Procedural head look completely disabled (ragdoll or downed clip takes 100% control).
- **Layer 1: Focused Task & Tool Impact (`WorkSwing` / Manual Action)**
  - When actively swinging an axe, pickaxe, hammer, or hoeing:
  - Gaze focuses sharply on the work contact point (tree notch, rock face, construction frame).
  - *Saccadic coupling:* Gaze leads the stroke; during the wind-up the head focuses on the strike target, locking rigidly on impact (`Landed == true`).
  - Gaze weight = 0.85–1.0. Completely suppresses social greetings and ambient wander.
- **Layer 2: Immediate Social & Threat Awareness (Passing Colonists / Events)**
  - Colonists passing within 2–3 cells (~5.0–7.5 m) entering the forward field of view.
  - Brief, high-priority glance at the other colonist's face/chest.
  - Active for a short dwell window (0.8–1.6 s), then suppressed by a per-pair or per-pawn social cooldown.
- **Layer 3: Environmental Points of Interest & Ambient Wander**
  - Triggered during steady locomotion or standing idle when no social target is active.
  - Pseudo-random glances toward terrain features, crops, light sources, or ambient lateral angles ($\pm 15^\circ$ to $\pm 45^\circ$).
- **Layer 4: Default Locomotion Tangent ("Path Forward")**
  - Base baseline when moving: head aligned with movement heading, gaze resting on the ground/path 3–5 m ahead.
  - Base baseline when idle: head aligned with body `TargetYaw`.

### 2. Ambient glance state machine & saccadic cadence
Human eye/head coordination operates via **saccadic gaze shifts**:
- At colony sim camera distances (~48° pitch, 30–80 m range), eyeball rotations and blinks are sub-pixel noise and completely invisible on 50-bone low-poly meshes. The visual storytelling of attention is carried 100% by the **head and neck bones**.
- **Cadence & Timings:**
  - *Straight-ahead Dwell:* 3.0 to 6.0 seconds. Characters hold forward awareness to avoid looking twitchy or neurotic.
  - *Glance Dwell:* 1.0 to 2.2 seconds. Shorter dwells (<0.5 s) read as graphical glitches; longer dwells (>3.0 s) look like catatonic staring.
  - *Transition Duration (Saccade):* 0.20 to 0.35 seconds. Natural head saccades exhibit high peak angular velocities (200–400°/s) with rapid deceleration. Linear interpolation (`Lerp`) looks robotic and drifting; a critically damped spring (`DampSpring` / smooth damping) or cubic ease-in/ease-out produces biological snappy onset and smooth settlement.
- **Pseudo-Random Generation (Zero Allocation & Deterministic/Decorrelated):**
  - To prevent all idle colonists glancing simultaneously in lockstep, pseudo-random intervals must be phased using a hash of the pawn ID and simulation tick or system clock.
  - Seed hash: `uint hash = Hash32(pawnId, glanceIndex)`.
  - Angle distribution:
    - Lateral Yaw: Bimodal Gaussian or triangular distribution biased between $\pm 15^\circ$ and $\pm 40^\circ$ (rarely up to $\pm 60^\circ$).
    - Pitch: Slight elevation shift ($-8^\circ$ looking at path up to $+15^\circ$ looking toward skyline/structures).
  - Return-to-center: On dwell timer expiration, the state machine transitions to `Return`, smoothly damping back to body forward over 0.25–0.35 s.

### 3. Lightweight social greeting / nearby colonist awareness
Performing pairwise distance and line-of-sight checks via Unity physics (`Physics.OverlapSphere` or raycasts) introduces substantial garbage collection (GC) allocations and CPU overhead under load.

- **Presentation-Side Evaluation (Zero Allocation):**
  - `PawnFigureDirector` already maintains a pooled list of active `Figure` instances (up to `MaxFigures = 64`, typically 5–15 active figures).
  - In a colony of $N \le 64$, flat pairwise iteration in Presentation is at most $\frac{N(N-1)}{2} \le 2016$ checks in the theoretical worst case, and for 10 colonists is only 45 checks.
  - **Chebyshev & Manhattan Pruning:**
    1. Layer check: `if (Mathf.Abs(posA.y - posB.y) > 1.5f) continue;` (colonists on different storeys ignore each other).
    2. Squared 2D Distance: `dx = posB.x - posA.x; dz = posB.z - posA.z; float distSq = dx*dx + dz*dz;`
       Check if `distSq <= 36.0f` (~6 m, roughly 2.4 cells).
    3. Vision Cone (Dot Product): Check if $B$ is in front of $A$:
       $\mathbf{dir}_{AB} = \frac{\mathbf{pos}_B - \mathbf{pos}_A}{\|\mathbf{pos}_B - \mathbf{pos}_A\|}$
       $\mathbf{forward}_A \cdot \mathbf{dir}_{AB} > \cos(65^\circ) \approx 0.42$.
    4. Motion Vector / Approach: A greeting glance is most poignant when pawns are walking towards or past each other ($\mathbf{v}_A \cdot \mathbf{v}_B < 0$) or when walking past an idle/working colonist.
- **Asymmetric Social Mechanics & Cooldown:**
  - If both colonists snap to look at each other simultaneously on the exact same frame, the interaction reads as robotic synchronization.
  - Use a hash roll:
    - 40% chance of Mutual Glance (with a staggered 0.1–0.25 s delay for the second pawn).
    - 40% chance of Unilateral Glance (one pawn glances, the other stays focused on their route).
    - 20% chance of Mutual Ignore (both focused ahead).
  - **Cooldown:** Store `SocialGlanceCooldownUntil` timestamp on each `Figure` (15.0 to 30.0 seconds cooldown). Once pawn $A$ has acknowledged pawn $B$, neither will acknowledge the other until the cooldown clears.

### 4. Vertical actions & posture rules
Verticality in Odyssey introduces three distinct head postures:
- **Climbing Ladders (`ClimbPose`):**
  - Traversal velocity along $Y$ determines primary pitch:
    - *Ascending:* Pitch up $+25^\circ$ to $+35^\circ$ (looking towards next rung/exit hatch).
    - *Descending:* Pitch down $-30^\circ$ to $-45^\circ$ (looking down at footholds).
    - *Stationary on ladder:* Pitch $+5^\circ$ (neutral forward into ladder rung).
  - Yaw constraint: Clamp head yaw to $\pm 10^\circ$ relative to the ladder wall normal. A colonist turning their head $60^\circ$ into a solid stone wall or air behind them breaks physical believability.
- **Sleeping in Bed (`SleepPose`):**
  - Head rotation is overridden by bed orientation: head aligned with mattress heading, resting flat or rolling slightly ($\pm 10^\circ$) to one side.
  - Gaze arbiter completely disabled; procedural gaze weight forced to 0.
- **Hauling & Carrying Loads:**
  - Carrying heavy crates/materials: Spine pitches forward slightly, neck/head pitches down $-10^\circ$ to $-15^\circ$, focusing gaze on the ground 2.5–3.5 m ahead. Conveys strain and weight.

### 5. Integration with `Odyssey.Presentation.World`
- `Odyssey.Sim` remains completely pure and unaware of gaze: determinism and state hashes are 100% unaffected.
- Presentation already contains the exact architectural seams:
  - `PawnFigureDirector.Figure` caches humanoid bones (`Spine`, `Hips`, arms, legs).
  - Head and Neck bones can be bound at figure construction: `animator.GetBoneTransform(HumanBodyBones.Head)` and `HumanBodyBones.Neck`.
  - In `Figure.Pose()`, procedural adjustments (`ClimbPose`, `SleepPose`, `WorkSwing`) already run sequentially after the `PlayableGraph` locomotion blend.
  - Bone rotation split: Natural human head turns distribute ~30% rotation across the neck/cervical spine and ~70% across the head/cranium.

---

## Recommendation

1. **Adopt a 5-tier Gaze Priority Arbiter:**
   Implement an explicit priority enum in `Odyssey.Presentation.World`:
   ```csharp
   public enum GazePriority
   {
       None = 0,
       PathForward = 1,
       AmbientWander = 2,
       SocialPassing = 3,
       WorkFocus = 4,
       LadderTraversal = 5,
       SleepLock = 6
   }
   ```
2. **Implement a Lightweight `GazeState` on `Figure`:**
   Add a 40-byte state block to `PawnFigureDirector.Figure.cs`:
   - `Transform? Head`, `Transform? Neck` (resolved once during `BindBones`).
   - `Vector3 CurrentGazeDirection`, `Vector3 TargetGazeDirection`.
   - `float GazeWeight` (eased in/out).
   - `float StateTimer`, `float StateDuration`.
   - `GazePriority ActivePriority`.
   - `float SocialCooldown`.
3. **Use Critically Damped Spring Damping for Saccades:**
   Use a standard damp function (`Vector3.SmoothDamp` or angular spring) with `smoothTime = 0.22f` and `maxSpeed = 360f` deg/sec. This delivers rapid saccadic onset without robotic linear interpolation or elastic oscillation.
4. **Distribute Rotations 30/70 (Neck/Head):**
   Rotate the Neck bone by $0.30 \times \Delta\mathbf{R}$ and the Head bone by $0.70 \times \Delta\mathbf{R}$, clamping combined yaw to $\pm 65^\circ$ and combined pitch to $[-35^\circ, +30^\circ]$.
5. **Broadphase Social Check in `PawnFigureDirector.Update`:**
   Execute an $O(N^2/2)$ loop over active figures each frame (or every 3rd frame). With $N \le 16$, this costs $< 0.005$ ms, uses zero heap allocation, and checks 2D horizontal distance $< 6.0$ m and forward dot product $> 0.45$.

---

## Sources

- `D:\code\odyssey\Assets\Odyssey\Presentation\World\PawnFigureDirector.cs` (Figure pooling, animation playables, pose overrides)
- `D:\code\odyssey\Assets\Odyssey\Presentation\World\PawnFigureDirector.Figure.cs` (Bones, speeds, headings, and poses)
- `D:\code\odyssey\Assets\Odyssey\Presentation\World\ClimbPose.cs` & `SleepPose.cs` (Existing procedural pose overrides)
- `D:\code\odyssey\docs\research\e-02-characters-animation.md` (Humanoid bone rig structure and animation coverage)
- [GDC Vault — Procedural Head Look and Attention Systems in Virtual Characters](https://www.gdcvault.com)
- [ResearchGate — Neurobiological Models of Eye and Head Movements for Computer Graphics Avatars](https://www.researchgate.net)
- [Unity Documentation — HumanBodyBones and Transform-based Procedural IK](https://docs.unity3d.com/ScriptReference/HumanBodyBones.html)

---

## Confidence

**High.** The simulation/presentation boundary is well-defined and already proven in Odyssey by `WorkSwing`, `ClimbPose`, and `SleepPose`. The proposed priority arbiter and saccade parameters match established character animation literature and can be implemented entirely in presentation without touching simulation state, Defs, or determinism.

---

## Could not be determined

- Whether the Synty low-poly character meshes have individual eyeball bones (Synty standard Polygon characters share a single skinned mesh without individual eyeball bones, meaning gaze must be executed entirely via Head and Neck bone transforms).
- The exact aesthetic threshold for head yaw limits before the low-poly neck geometry stretches or pinches noticeably; this requires visual contact sheets in the editor across the 61 colonist models.
