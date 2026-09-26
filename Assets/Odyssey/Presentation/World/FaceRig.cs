#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The two facial bones of one rig, and the one way they are written (design 59 §6).
    ///
    /// <para>Every colonist body carries one <c>Eyes</c> and one <c>Eyebrows</c> bone under the
    /// head, each moving both of its pair (<c>docs/research/e-15</c>). Everything a pose needs is
    /// read <b>once, at rest</b>: where the brows sit, which way is up the head and along the face in
    /// the brows' parent space, and which of the eyes' own axes points up. A pose is then written
    /// <b>absolutely</b> from that rest — never added to or multiplied onto whatever the bone holds
    /// — because nothing else rewrites these bones each frame, and a relative write would wind
    /// itself up (§10).</para>
    ///
    /// <para>Lifts are metres of the body as authored, measured in <c>root</c>'s own space, so the
    /// figure's 1.4 scale applies to a lift exactly as it applies to the face under it.</para>
    /// </summary>
    public sealed class FaceRig
    {
        readonly Transform? _brows;
        readonly Transform? _eyes;
        readonly Vector3 _browRestPosition;
        readonly Quaternion _browRestRotation;
        readonly Vector3 _browUpPerMetre;
        readonly Vector3 _browRollAxis;
        readonly Vector3 _eyesRestScale;
        readonly int _eyesUpAxis;
        readonly Vector3 _headForward;

        FaceRig(Transform root, Transform head, Transform? brows, Transform? eyes)
        {
            _brows = brows;
            _eyes = eyes;
            _headForward = head.InverseTransformDirection(root.forward).normalized;

            if (brows != null)
            {
                Transform space = brows.parent != null ? brows.parent : head;
                _browRestPosition = brows.localPosition;
                _browRestRotation = brows.localRotation;
                _browUpPerMetre = space.InverseTransformVector(root.TransformVector(Vector3.up));
                _browRollAxis = space.InverseTransformDirection(root.forward).normalized;
            }

            if (eyes != null)
            {
                _eyesRestScale = eyes.localScale;
                _eyesUpAxis = MostNearly(eyes, root.up);
            }
        }

        /// <summary>
        /// The face of the rig under <paramref name="head"/>, or null for a rig with neither bone —
        /// an animal, or a body that paints its face on. Call at rest, before any clip has posed the
        /// rig: the rest read here is what every later pose is written from.
        /// </summary>
        public static FaceRig? Bind(Transform root, Transform head)
        {
            Transform? brows = Find(head, "Eyebrows");
            Transform? eyes = Find(head, "Eyes");
            return brows == null && eyes == null ? null : new FaceRig(root, head, brows, eyes);
        }

        public bool HasBrows => _brows != null;

        public bool HasEyes => _eyes != null;

        /// <summary>How far up the head the brows sit now, in authored metres — read off the bone, not the pose asked for.</summary>
        public float MeasuredBrowLift =>
            _brows == null || _browUpPerMetre.sqrMagnitude < 1e-12f ? 0f
            : Vector3.Dot(_brows.localPosition - _browRestPosition, _browUpPerMetre) / _browUpPerMetre.sqrMagnitude;

        /// <summary>How open the eyes are now against their rest, along the axis up the head — read off the bone.</summary>
        public float MeasuredEyeOpen =>
            _eyes == null || _eyesRestScale[_eyesUpAxis] == 0f ? 1f
            : _eyes.localScale[_eyesUpAxis] / _eyesRestScale[_eyesUpAxis];

        /// <summary>Write <paramref name="pose"/>. <see cref="FacePose.Rest"/> puts both bones back exactly as bound.</summary>
        public void Apply(in FacePose pose)
        {
            if (_brows != null)
            {
                _brows.localPosition = _browRestPosition + _browUpPerMetre * pose.BrowLift;
                _brows.localRotation = pose.BrowRoll == 0f
                    ? _browRestRotation
                    : Quaternion.AngleAxis(pose.BrowRoll, _browRollAxis) * _browRestRotation;
            }

            if (_eyes != null)
            {
                Vector3 scale = _eyesRestScale * pose.EyeSize;
                scale[_eyesUpAxis] *= pose.EyeOpen;
                _eyes.localScale = scale;
            }
        }

        /// <summary>
        /// Dip and sway <paramref name="head"/> by a talker's nod, on top of whatever the gaze has
        /// just turned it to. About the face's own axes as they point now, so a colonist looking
        /// sideways at her partner nods at her rather than tilting an ear.
        /// </summary>
        public void Nod(Transform head, float pitch, float roll)
        {
            if (pitch == 0f && roll == 0f) return;
            Vector3 face = head.TransformDirection(_headForward);
            Vector3 across = Vector3.Cross(Vector3.up, face);
            if (across.sqrMagnitude < 1e-6f) return;
            head.rotation = Quaternion.AngleAxis(pitch, across.normalized) * Quaternion.AngleAxis(roll, face) * head.rotation;
        }

        static int MostNearly(Transform bone, Vector3 direction)
        {
            int best = 1;
            float most = -1f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = i == 0 ? bone.right : i == 1 ? bone.up : bone.forward;
                float d = Mathf.Abs(Vector3.Dot(axis, direction));
                if (d > most) { most = d; best = i; }
            }
            return best;
        }

        static Transform? Find(Transform under, string name)
        {
            for (int i = 0; i < under.childCount; i++)
            {
                Transform child = under.GetChild(i);
                if (child.name == name) return child;
            }
            for (int i = 0; i < under.childCount; i++)
            {
                Transform? deeper = Find(under.GetChild(i), name);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
