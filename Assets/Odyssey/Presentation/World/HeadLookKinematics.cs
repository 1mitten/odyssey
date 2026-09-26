#nullable enable
using System;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Priorities for competing gaze targets, resolved via strict pre-emption.
    /// Higher values pre-empt lower values.
    /// </summary>
    public enum GazePriority : byte
    {
        None = 0,
        PathForward = 1,
        AmbientWander = 2,
        SocialPassing = 3,
        /// <summary>Talking: each looks at the other's head (design 59 §5). Work, a ladder and sleep all pre-empt it.</summary>
        Conversation = 4,
        WorkFocus = 5,
        LadderTraversal = 6,
        SleepLock = 7,
    }

    /// <summary>
    /// Runtime procedural gaze state stored per live figure. 0 B GC.Alloc.
    /// </summary>
    public struct LookGazeState
    {
        /// <summary>Current smoothed pitch (x) and yaw (y) in degrees.</summary>
        public Vector2 CurrentAngles;

        /// <summary>Velocity for Mathf.SmoothDampAngle.</summary>
        public Vector2 AngleVelocity;

        /// <summary>The world-space position being looked at when HasTarget is true.</summary>
        public Vector3 TargetWorldPosition;

        /// <summary>Whether a valid 3D target point is currently set.</summary>
        public bool HasTarget;

        /// <summary>Active priority owning this gaze.</summary>
        public GazePriority ActivePriority;

        /// <summary>Time in seconds spent in the current ambient or glance state.</summary>
        public float StateTimer;

        /// <summary>Duration in seconds for the current dwell or glance.</summary>
        public float StateDuration;

        /// <summary>Whether currently in an ambient lateral glance (true) or looking forward (false).</summary>
        public bool IsGlancing;

        /// <summary>Target glance angles (pitch x, yaw y) when IsGlancing is true.</summary>
        public Vector2 AmbientAngles;

        /// <summary>Cooldown timer in seconds before another social greeting glance can trigger.</summary>
        public float SocialCooldown;

        /// <summary>Pitch offset for carrying/hauling or eating.</summary>
        public float PosturePitchOffset;

        /// <summary>Overall gaze weight (0 = neutral clip, 1 = full gaze).</summary>
        public float GazeWeight;
    }

    /// <summary>
    /// Pure procedural humanoid head and neck look-at kinematics, damping, and multi-bone distribution.
    /// </summary>
    public static class HeadLookKinematics
    {
        public const float YawLimit = 60.0f;
        public const float YawCutoff = 95.0f;
        public const float PitchMin = -40.0f;
        public const float PitchMax = 35.0f;

        public const float SmoothTime = 0.12f;
        public const float MaxSpeed = 300.0f;

        public const float NeckYawRatio = 0.0f;
        public const float NeckPitchRatio = 0.0f;
        public const float HeadYawRatio = 1.0f;
        public const float HeadPitchRatio = 1.0f;

        /// <summary>
        /// Solves local yaw and pitch angles (in degrees) to look at targetWorld,
        /// relative to the reference frame's forward, right, and up axes.
        /// Positive yaw is right, negative is left. Positive pitch is up, negative is down.
        /// </summary>
        public static bool SolveAngles(
            Vector3 targetWorld,
            Transform referenceFrame,
            Vector3 headPivot,
            out float yaw,
            out float pitch)
        {
            Vector3 toTarget = targetWorld - headPivot;
            float sqrMag = toTarget.sqrMagnitude;
            if (sqrMag < 1e-4f)
            {
                yaw = 0f;
                pitch = 0f;
                return false;
            }

            Vector3 dir = toTarget.normalized;
            float fwd = Vector3.Dot(dir, referenceFrame.forward);
            float right = Vector3.Dot(dir, referenceFrame.right);
            float up = Vector3.Dot(dir, referenceFrame.up);

            yaw = Mathf.Atan2(right, fwd) * Mathf.Rad2Deg;
            float planarDist = Mathf.Sqrt(right * right + fwd * fwd);
            pitch = Mathf.Atan2(up, planarDist) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// Computes the rear hemisphere falloff weight via cubic smoothstep.
        /// Returns 1.0 within YawLimit (60 deg), fades to 0.0 at YawCutoff (95 deg).
        /// </summary>
        public static float ComputeRearWeight(float absYaw)
        {
            if (absYaw <= YawLimit) return 1.0f;
            if (absYaw >= YawCutoff) return 0.0f;
            float t = (absYaw - YawLimit) / (YawCutoff - YawLimit);
            return 1.0f - (3.0f * t * t - 2.0f * t * t * t);
        }

        /// <summary>
        /// Updates smoothed gaze angles towards the target angles or neutral forward using SmoothDampAngle.
        /// </summary>
        public static void UpdateDampedAngles(
            ref LookGazeState state,
            Transform? referenceFrame,
            Vector3 headPivot,
            float dt)
        {
            float targetPitch = 0f;
            float targetYaw = 0f;

            if (state.ActivePriority == GazePriority.SleepLock || dt <= 1e-5f)
            {
                if (state.ActivePriority == GazePriority.SleepLock)
                {
                    state.CurrentAngles = Vector2.zero;
                    state.AngleVelocity = Vector2.zero;
                    state.GazeWeight = 0f;
                    return;
                }
            }

            if (state.HasTarget && referenceFrame != null)
            {
                if (SolveAngles(state.TargetWorldPosition, referenceFrame, headPivot, out float rawYaw, out float rawPitch))
                {
                    float rearWeight = ComputeRearWeight(Mathf.Abs(rawYaw));
                    targetYaw = Mathf.Clamp(rawYaw, -YawLimit, YawLimit) * rearWeight;
                    targetPitch = Mathf.Clamp(rawPitch + state.PosturePitchOffset, PitchMin, PitchMax) * rearWeight;
                }
            }
            else if (state.IsGlancing)
            {
                targetYaw = Mathf.Clamp(state.AmbientAngles.y, -YawLimit, YawLimit);
                targetPitch = Mathf.Clamp(state.AmbientAngles.x + state.PosturePitchOffset, PitchMin, PitchMax);
            }
            else
            {
                targetPitch = Mathf.Clamp(state.PosturePitchOffset, PitchMin, PitchMax);
                targetYaw = 0f;
            }

            state.CurrentAngles.x = Mathf.SmoothDampAngle(
                state.CurrentAngles.x, targetPitch, ref state.AngleVelocity.x, SmoothTime, MaxSpeed, dt);
            state.CurrentAngles.y = Mathf.SmoothDampAngle(
                state.CurrentAngles.y, targetYaw, ref state.AngleVelocity.y, SmoothTime, MaxSpeed, dt);
        }

        /// <summary>
        /// Applies the gaze orientation to the head bone sat on the neck.
        /// Turns the Head bone (0% Neck, 100% Head) around the upright cervical axis (yawAxis)
        /// and nods around the transverse condyle axis (pitchAxis) with zero ear-to-shoulder roll.
        /// Uses absolute look rotation so repeated calls across Sync and Evaluate never accumulate.
        /// </summary>
        public static void ApplyAdditiveRotation(
            Transform? neck,
            Transform? head,
            Vector3 yawAxis,
            Vector3 pitchAxis,
            Vector2 angles,
            float weight = 1f)
        {
            if (head == null || weight <= 1e-4f) return;

            float pitch = angles.x * weight;
            float yaw = angles.y * weight;

            Vector3 fwd = Vector3.Cross(pitchAxis, yawAxis).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;

            Quaternion yawRot = Quaternion.AngleAxis(yaw, yawAxis);
            Vector3 turnedPitchAxis = yawRot * pitchAxis;
            Quaternion pitchRot = Quaternion.AngleAxis(-pitch, turnedPitchAxis);

            Vector3 worldDir = pitchRot * yawRot * fwd;
            Quaternion targetRot = Quaternion.LookRotation(worldDir, yawAxis);

            if (weight >= 0.999f)
            {
                head.rotation = targetRot;
            }
            else
            {
                Quaternion neutralRot = Quaternion.LookRotation(fwd, yawAxis);
                head.rotation = Quaternion.Slerp(neutralRot, targetRot, weight);
            }
        }
    }
}
