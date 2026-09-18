# Plan: Colonist Head Turning, Dynamic Work Gaze, and Ambient Motion

**Governing Design:** `docs/design/23-head-turning-and-gaze.md`  
**Research References:** `docs/research/e-06-head-look-kinematics.md`, `docs/research/a-17-ambient-gaze-and-motion.md`  
**Worktree:** `D:\code\odyssey-motion` on branch `claude/colonist-head-turn`

---

## 1. Scope and Design Goals

Enable colonists to feel alive and motionful through procedural head and neck movement:
1. **Work Target Focus:** Colonists turn and tilt their heads directly towards what they are working on (`WorkCentre` — tree notch, rock face, building site, crops), with dipped pitch when mining downward and raised pitch when mining overhead.
2. **Ambient Glances:** Colonists walking or idling periodically glance around ($\pm 15^\circ..\pm 35^\circ$) for 1–2 seconds, breaking up rigid forward staring.
3. **Contextual Social Awareness:** Colonists passing within 2–3 cells of each other briefly look towards one another ("greeting glance").
4. **Vertical Postures:** Colonists look up when ascending ladders/shafts, look down when descending, and rest their heads naturally when sleeping.
5. **Zero Allocation & Clean Seam:** Purely presentation-side in `Odyssey.Presentation.World`; 0 B GC allocations per frame; zero impact on simulation determinism or state hashes.

---

## 2. Unit Breakdown

### Unit 1: Kinematic Foundation & Bone Binding (`HeadLookKinematics`)
- **Files Touched:**
  - `Assets/Odyssey/Presentation/World/HeadLookKinematics.cs` (New)
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.Figure.cs`
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.Tools.cs`
  - `Assets/Odyssey/Presentation/Tests/HeadLookKinematicsTests.cs` (New)
- **Work:**
  1. In `PawnFigureDirector.Figure.cs`:
     - Add `public Transform? Head;`, `public Transform? Neck;`, `public Transform? Chest;`.
     - Define `public LookGazeState Gaze;` (struct containing `CurrentAngles`, `AngleVelocity`, `TargetWorldPosition`, `GazeWeight`, `StateTimer`, `DwellDuration`, `ActivePriority`, `SocialCooldown`, `HasTarget`).
     - Define `public enum GazePriority : byte { None, PathForward, AmbientWander, SocialPassing, WorkFocus, LadderTraversal, SleepLock }`.
  2. In `PawnFigureDirector.Tools.cs` (`BindWorkBones`):
     - Resolve `animator.GetBoneTransform(HumanBodyBones.Head)`, `HumanBodyBones.Neck`, and `HumanBodyBones.Chest` (fallback to `Spine`).
  3. In `HeadLookKinematics.cs`:
     - Implement pure static methods:
       - `SolveAngles(Vector3 targetWorld, Transform chest, Transform head, out float yaw, out float pitch)`
       - `ComputeRearWeight(float absYaw)` (cubic smoothstep between $60^\circ \to 95^\circ$)
       - `UpdateDampedAngles(ref LookGazeState state, Transform? chest, Transform? head, float dt)` using `Mathf.SmoothDampAngle`
       - `ApplyAdditiveRotation(Transform? neck, Transform? head, Vector2 angles)` applying 30% Neck and 70% Head.
  4. In `HeadLookKinematicsTests.cs`:
     - Test coordinate transformations (target directly in front, to the left, above, below).
     - Test clamping: yaw clamped to $\pm 60^\circ$, pitch clamped to $[-40^\circ, +35^\circ]$.
     - Test rear falloff: target at $120^\circ$ fades gaze weight to zero.
     - Test 30/70 distribution math.

### Unit 2: Work Target Gaze & Vertical Actions
- **Files Touched:**
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.cs`
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.Poses.cs`
  - `Assets/Odyssey/Presentation/Tests/HeadLookKinematicsTests.cs`
- **Work:**
  1. In `PawnFigureDirector.Poses.cs`:
     - Implement `void ApplyGazePose()` called at the end of `Sync()` and `Evaluate()`.
     - Wire Tier 6 (`SleepLock`): When `figure.SleepWeight > 0.001f`, force `GazeWeight = 0f` and clear targets.
     - Wire Tier 5 (`LadderTraversal`): When `figure.ClimbPhase >= 0f` and `figure.ClimbFace != Vector3.zero`:
       - If ascending (`pawn.NextCell.Y > pawn.Cell.Y`), target upward pitch $+30^\circ$.
       - If descending (`pawn.NextCell.Y < pawn.Cell.Y`), target downward pitch $-35^\circ$.
       - Constrain yaw to $\pm 10^\circ$ relative to climb face.
     - Wire Tier 4 (`WorkFocus`): When `pawn.Working`:
       - Target = `figure.WorkCentre`.
       - If dipped (mining down), target includes downward dip pitch.
       - If raised (mining overhead), target includes raised pitch.
  2. Diagnostic properties in `PawnFigureDirector.cs`:
     - `public float MeasuredGazeYaw { get; private set; }`
     - `public float MeasuredGazePitch { get; private set; }`
     - `public GazePriority ActiveGazePriority { get; private set; }`
  3. Tests:
     - Verify working woodcutter looks at tree centre.
     - Verify miner cutting floor looks down into the cut.
     - Verify climber looking up when ascending.
     - Verify sleeper gaze suppressed.

### Unit 3: Ambient Wandering Glances & Social Greeting
- **Files Touched:**
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.cs`
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.Poses.cs`
  - `Assets/Odyssey/Presentation/Tests/HeadLookKinematicsTests.cs`
- **Work:**
  1. Implement Tier 3 (`SocialPassing`):
     - In `PawnFigureDirector.Sync()`, evaluate pairwise proximity across drawn figures:
       - Proximity check: horizontal distance $< 6.0$ m (~2.4 cells), layer height difference $|\Delta y| \le 1.5$ m.
       - Vision cone: other colonist is within forward cone ($\mathbf{fwd} \cdot \mathbf{dir} > 0.42$).
       - If conditions met and `figure.Gaze.SocialCooldown <= 0`:
         - Set social glance target towards fellow colonist's head.
         - Dwell for 1.2–1.8 s.
         - Set social cooldown for 20 s to prevent immediate re-triggering.
  2. Implement Tier 2 (`AmbientWander`):
     - For moving or idle colonists without higher priority:
       - Forward dwell state for 3.0–6.0 seconds.
       - Glance state for 1.0–2.0 seconds: pseudo-random lateral angle ($\pm 15^\circ..\pm 35^\circ$), pitch ($-8^\circ..+15^\circ$), seeded by `pawn.Id` and glance index.
       - Smooth transition back to forward.
  3. Hauling & Eating postures:
     - When hauling: bias baseline pitch down by $-12^\circ$ (looking towards footing).
     - When eating: bias baseline pitch down by $-25^\circ$ (looking towards meal).
  4. Tests:
     - Verify passing colonists trigger social glance.
     - Verify cooldown suppresses rapid repeat glances.
     - Verify ambient timer cycles between forward dwell and glance.

### Unit 4: Integration, Regression Verification & Content Gate Check
- **Files Touched:**
  - `Assets/Odyssey/Presentation/Tests/`
- **Work:**
  1. Run `scripts/test-fast.sh` (all Sim + Hud tests green).
  2. Run EditMode test suite in Unity to verify Presentation assembly compiles and all tests pass.
  3. Run `build_wiki.py --check` and `emit_labels.py --check`.
  4. Update `docs/journal.md` and `CLAUDE.md` / `AGENTS.md` status.
  5. Prepare hand-over table and playtest instructions.

---

## 3. Risk Analysis & Cheapest Verification Experiments

| Risk | Impact | Mitigation / Cheapest Experiment |
|---|---|---|
| Synty neck pinching at extreme angles | Low-poly mesh tearing or candy-wrapper collapse | 30/70 neck/head split + $\pm 60^\circ$ clamp. Tested by measuring neck angle $\le 18^\circ$ at max yaw. |
| Robotic synchronized glances across pawns | Uncanny uniform head bobbing | Deterministic hash phasing per `pawn.Id` decorrelates dwell timers and glance bearings across the colony. |
| Social proximity search overhead | Frame time degradation at high pawn counts | Flat iteration over active figures ($N \le 64$, typical 5–15 on screen); layer pre-cull and squared distance check costs $<0.005$ ms, zero heap allocations. |
| Jitter when targets pass behind colonist | Head flipping between left and right extremes | Cubic smoothstep falloff over $60^\circ \to 95^\circ$ smoothly returns head to forward neutral. |
