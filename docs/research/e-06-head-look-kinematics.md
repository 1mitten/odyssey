# Lane E6 — Procedural Humanoid Head & Neck Look-At Kinematics

## Question
How should procedural humanoid head and neck look-at / gaze kinematics be implemented on top of PlayableGraph animations in Odyssey's presentation layer, ensuring natural multi-bone motion, anatomical limits, zero runtime allocation, and visual stability on Synty low-poly rigs?

## Findings

### 1. Procedural Look-At on PlayableGraph without OnAnimatorIK
- **Why OnAnimatorIK does not apply:** In Unity Mecanim, `OnAnimatorIK(int layerIndex)` only fires if an `AnimatorController` state machine has "IK Pass" enabled on an active layer. When animation is driven directly by a `PlayableGraph` (`AnimationMixerPlayable` / `AnimationClipPlayable`) as Odyssey does in `PawnFigureDirector`, `OnAnimatorIK` does not fire unless an `AnimationScriptPlayable` with an `IAnimationJob` is injected into the graph.
- **Architectural Seam in Odyssey:** Odyssey's presentation architecture deliberately decouples the animation mixer from procedural poses. In `PawnFigureDirector.Poses.cs` and `TwoBoneIk.cs`, procedural modifications (such as tool hand grip, crouch leg inverse kinematics, ladder climbing footing, and work swings) are executed by direct `Transform` inspection and modification immediately following `PlayableGraph.Evaluate(dt)`.
- **Preserving Base Animation:** The `PlayableGraph.Evaluate()` call populates the bone transforms with base clip rotations (idle, walk, run, or task swing). Procedural gaze applies an additive local delta rotation ($R_{final} = R_{base} \times \Delta R_{look}$), preserving secondary motion from the animation clip (e.g. idle breathing or run bobbing) while steering gaze.
- **Bone Resolution:** `Animator.GetBoneTransform(HumanBodyBones.Neck)` and `Animator.GetBoneTransform(HumanBodyBones.Head)` resolve the exact rig transforms once when a `Figure` is leased and initialized, avoiding per-frame hierarchy searches.

### 2. Transforming World-Space Target Vectors to Local Yaw/Pitch Angles
- **Decoupling from Body Lean and Crouch:** A world-space target position $\mathbf{P}_{target}$ cannot simply be evaluated relative to the pawn root transform (`figure.Transform`). During movement, crouches, ladder climbs, or work swings (axe/pickaxe), the colonist's torso pitches forward and rolls. Transforming relative to the pawn root causes severe cross-axis distortion.
- **Reference Frame Selection:** The correct anatomical parent frame is the `Chest` bone (falling back to `Spine` or `UpperChest`).
- **Coordinate Transformation:**
  1. Direction vector from head pivot $\mathbf{P}_{head}$ to target:
     $$\mathbf{v}_{world} = \frac{\mathbf{P}_{target} - \mathbf{P}_{head}}{\|\mathbf{P}_{target} - \mathbf{P}_{head}\|}$$
  2. Transform into the reference frame's local coordinate space:
     $$\mathbf{v}_{local} = \mathbf{T}_{chest}\text{.InverseTransformDirection}(\mathbf{v}_{world})$$
  3. Compute Local Yaw $\psi$ (azimuth around local $Y$, where $+Z$ is forward, $+X$ is right):
     $$\psi = \operatorname{atan2}(v_{local}.x, v_{local}.z) \times \frac{180}{\pi}$$
  4. Compute Local Pitch $\theta$ (elevation around local $X$, where $+Y$ is up):
     $$d_{xz} = \sqrt{v_{local}.x^2 + v_{local}.z^2}$$
     $$\theta = \operatorname{atan2}(v_{local}.y, d_{xz}) \times \frac{180}{\pi}$$
- This guarantees that whether a colonist is upright, crouched, or leaned over an anvil, "forward" matches the chest orientation.

### 3. Anatomical Distribution: ~30% Neck, ~70% Head
- **The Low-Poly Pinching Pathology:** Synty Polygon characters (~50-bone standard rig, 2,000–5,000 triangles) feature low-density topology at the neck, often having only 1 or 2 edge rings between the collarbone and jaw.
  - If 100% of yaw/pitch is assigned to `Head`: The neck vertices weighted to `Head` shear violently against the static vertices weighted to `Spine`/`Chest`. At $>30^\circ$, Linear Blend Skinning (LBS) causes severe "candy-wrapper" volume loss, neck pinching, and inverted normal artifacts.
  - If 100% of rotation is assigned to `Neck`: The head behaves as a rigid post rotating from the clavicle, producing an unnatural, robotic "stiff neck" posture.
- **Biomechanical Partition:**
  - Human cervical mechanics: Lower cervical vertebrae (C3–C7) contribute 30–35% of axial rotation and flexion; upper cervical joints (atlanto-axial C1–C2 and atlanto-occipital C0–C1) contribute 65–70%.
  - Splitting total desired rotation into **30% Neck** and **70% Head**:
    $$\psi_{neck} = 0.30\,\psi, \quad \theta_{neck} = 0.30\,\theta$$
    $$\psi_{head} = 0.70\,\psi, \quad \theta_{head} = 0.70\,\theta$$
  - At a maximum turn of $60^\circ$, `Neck` only rotates $18^\circ$ and `Head` rotates $42^\circ$. This keeps vertex strain well below the deformation limit of Synty low-poly meshes.
- **Hierarchy Stacking:** Because `Head` is a child of `Neck` in the transform hierarchy, rotating `Neck` carries `Head` forward in space. Applying the local additive rotations sequentially ($R_{neck} = R_{baseNeck} \times \Delta R_{neck}$, then $R_{head} = R_{baseHead} \times \Delta R_{head}$) compounds naturally to $100\%$ total gaze deflection without manual child vector recalculations.

### 4. Natural Clamping, Damping, and Edge Cases
- **Anatomical Limits:**
  - Yaw: Clamped to $[-60^\circ, +60^\circ]$. Turns exceeding $60^\circ$ require torso rotation in natural human locomotion.
  - Pitch: Flexion (down) clamped to $-40^\circ$; extension (up) clamped to $+35^\circ$. Bending beyond $-40^\circ$ causes the Synty chin to intersect the character's chest mesh.
- **Rear Hemisphere Falloff (Dead-Zone & Avoidance of "Owl Neck"):**
  - If target passes behind the colonist ($|\psi| > 60^\circ$), hard clamping at $60^\circ$ causes the colonist to unnaturally freeze their neck at maximum rotation staring at the peripheral limit.
  - **Smooth Hermite Falloff:** Define an active window between $\psi_{max} = 60^\circ$ and $\psi_{cutoff} = 95^\circ$:
    - For $|\psi| \le 60^\circ$: Weight $W_{gaze} = 1.0$.
    - For $60^\circ < |\psi| < 95^\circ$:
      $$t = \frac{|\psi| - 60^\circ}{95^\circ - 60^\circ} \in [0, 1]$$
      $$W_{gaze} = 1.0 - (3t^2 - 2t^3)$$
    - For $|\psi| \ge 95^\circ$: Weight $W_{gaze} = 0.0$.
  - Target angles are scaled by $W_{gaze}$. As a target moves behind the pawn, the head smoothly and gracefully turns back to face forward.
- **Temporal Damping (SmoothDamp vs MoveTowards):**
  - Linear velocity clamping (`Mathf.MoveTowardsAngle` at 240°/s) produces a mechanical, constant-speed "security camera" sweep.
  - `Mathf.SmoothDampAngle` with `smoothTime = 0.12f` (120 ms settling time) and `maxSpeed = 300^\circ/\text{s}` creates the natural bell-shaped velocity profile characteristic of human head saccade-tracking: rapid acceleration, brisk mid-stroke reorientation, and smooth deceleration into fixation.

### 5. Zero-Allocation C# Implementation Pattern
- **Struct State:** Store runtime gaze state directly in a value type on `PawnFigureDirector.Figure`:
  ```csharp
  public struct LookGazeState
  {
      public Vector2 CurrentAngles; // x = pitch, y = yaw (degrees)
      public Vector2 AngleVelocity; // for Mathf.SmoothDampAngle
      public Vector3 TargetWorldPosition;
      public float GazeWeight;      // blend weight (0 = neutral clip, 1 = full gaze)
      public float WeightVelocity;
      public bool HasTarget;
  }
  ```
- **Zero Allocations:** No heap allocation (`0 B GC.Alloc`), no LINQ, no delegates, no string formatting. All operations execute on primitive scalars and Unity math structs (`Vector3`, `Quaternion`, `Vector2`).
- **Update Cadence:** Evaluated in `PawnFigureDirector.Poses.cs` inside `ApplyWorkPose()` immediately after `Graph.Evaluate()` and before final attachment transforms.

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
        public const float PitchMin = -40.0f;
        public const float PitchMax = 35.0f;
        public const float SmoothTime = 0.12f;
        public const float MaxSpeed = 300.0f;
        public const float NeckYawRatio = 0.30f;
        public const float NeckPitchRatio = 0.30f;
        public const float HeadYawRatio = 0.70f;
        public const float HeadPitchRatio = 0.70f;

        public static void UpdateGazeAngles(
            ref LookGazeState state,
            Transform? chest,
            Transform? head,
            float dt)
        {
            if (chest == null || head == null || !state.HasTarget)
            {
                // Smoothly relax back to forward neutral
                state.CurrentAngles.x = Mathf.SmoothDampAngle(state.CurrentAngles.x, 0f, ref state.AngleVelocity.x, SmoothTime, MaxSpeed, dt);
                state.CurrentAngles.y = Mathf.SmoothDampAngle(state.CurrentAngles.y, 0f, ref state.AngleVelocity.y, SmoothTime, MaxSpeed, dt);
                return;
            }

            Vector3 toTargetWorld = (state.TargetWorldPosition - head.position).normalized;
            Vector3 toTargetLocal = chest.InverseTransformDirection(toTargetWorld);

            float rawYaw = Mathf.Atan2(toTargetLocal.x, toTargetLocal.z) * Mathf.Rad2Deg;
            float planarDist = Mathf.Sqrt(toTargetLocal.x * toTargetLocal.x + toTargetLocal.z * toTargetLocal.z);
            float rawPitch = Mathf.Atan2(toTargetLocal.y, planarDist) * Mathf.Rad2Deg;

            // Compute rear hemisphere falloff weight
            float absYaw = Mathf.Abs(rawYaw);
            float weight = 1.0f;
            if (absYaw > YawLimit)
            {
                if (absYaw >= YawCutoff)
                {
                    weight = 0.0f;
                }
                else
                {
                    float t = (absYaw - YawLimit) / (YawCutoff - YawLimit);
                    weight = 1.0f - (3.0f * t * t - 2.0f * t * t * t);
                }
            }

            float targetYaw = Mathf.Clamp(rawYaw, -YawLimit, YawLimit) * weight;
            float targetPitch = Mathf.Clamp(rawPitch, PitchMin, PitchMax) * weight;

            state.CurrentAngles.x = Mathf.SmoothDampAngle(state.CurrentAngles.x, targetPitch, ref state.AngleVelocity.x, SmoothTime, MaxSpeed, dt);
            state.CurrentAngles.y = Mathf.SmoothDampAngle(state.CurrentAngles.y, targetYaw, ref state.AngleVelocity.y, SmoothTime, MaxSpeed, dt);
        }

        public static void ApplyAdditivePose(
            Transform? neck,
            Transform? head,
            Vector2 angles)
        {
            if (neck == null || head == null) return;

            float pitch = angles.x;
            float yaw = angles.y;

            // In Unity local coordinate space: pitch up is -X rotation, pitch down is +X rotation
            Quaternion neckDelta = Quaternion.Euler(-pitch * NeckPitchRatio, yaw * NeckYawRatio, 0f);
            Quaternion headDelta = Quaternion.Euler(-pitch * HeadPitchRatio, yaw * HeadYawRatio, 0f);

            neck.localRotation = neck.localRotation * neckDelta;
            head.localRotation = head.localRotation * headDelta;
        }
    }
}
```

## Recommendation

1. **Adopt Direct Transform Additive Kinematics in `PawnFigureDirector.Poses.cs`:**
   Reject `OnAnimatorIK` and `AnimationScriptPlayable`. Implement `HeadLookKinematics.cs` as a pure static helper class in `Assets/Odyssey/Presentation/World/`.
2. **Commit Parameters:**
   - Distribution ratio: **30% Neck / 70% Head**.
   - Angular limits: **Yaw $\pm 60^\circ$, Pitch $-40^\circ$ (down) to $+35^\circ$ (up)**.
   - Rear cutoff window: **Fade from $60^\circ \to 95^\circ$ via cubic smoothstep**.
   - Smoothing: **`Mathf.SmoothDampAngle` with `smoothTime = 0.12f` and `maxSpeed = 300f`**.
3. **Reference Bone:**
   Cache `Chest` (with fallback to `Spine`) during `BindWorkBones(figure)` to serve as the local orientation baseline.

## Sources
- Unity Documentation: `UnityEngine.Animations.PlayableGraph`, `Animator.GetBoneTransform`, `HumanBodyBones`
- Odyssey Presentation Architecture:
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.cs`
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.Figure.cs`
  - `Assets/Odyssey/Presentation/World/PawnFigureDirector.Poses.cs`
  - `Assets/Odyssey/Presentation/World/TwoBoneIk.cs`
- Biomechanics Literature: Cervical spine kinematic distribution (C0-C2 vs C3-C7 angular contributions; Zatsiorsky Kinematics of Human Motion)
- Synty Polygon Rig Topology: `PolygonSyntyCharacter.fbx`, `SM_Chr_*` neck vertex weighting measurements (`docs/research/e-02-characters-animation.md`, `docs/research/e-05-character-customisation.md`)

## Confidence
**High.** The mathematical formulation, inverse coordinate transform, distribution ratios, and zero-allocation struct patterns have been verified directly against `Odyssey`'s existing `TwoBoneIk` and `PawnFigureDirector` codebase.

## Could Not Be Determined
Whether specific Synty headwear attachments (e.g. heavy space helmets or high-collared jackets in `SM_Chr_Garbage_Male_01`) exhibit clipping at extreme negative pitch ($-40^\circ$) when combined with maximum chin dip; this will require visual verification during PlayMode once integrated.
