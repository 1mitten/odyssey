#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// How big a quadruped is drawn, and how fast its clips cover ground, read off the posed mesh
    /// and its bones (design 66 §5, §6) — never off a bone's name alone or the pack's own scale,
    /// which is two to four times life (e-15). One owner for the probe that sets the catalogue's
    /// scales and speeds and the test that holds them.
    ///
    /// <para>Named for the SIMPLE rigs' joints (<c>…_l_FrontLeg_Hip…</c>,
    /// <c>…_l_FrontLeg_Ankle…</c>, <c>…_Head…</c>), found by substring so one routine serves all
    /// five rigs.</para>
    /// </summary>
    public static class AnimalMeasure
    {
        /// <summary>What a posed animal measures, in metres, in its root's frame.</summary>
        public readonly struct Size
        {
            public readonly float Sole, Crown, Withers, Length;
            public Size(float sole, float crown, float withers, float length)
            {
                Sole = sole; Crown = crown; Withers = withers; Length = length;
            }

            /// <summary>Withers above the sole: the shoulder height the scales are set against.</summary>
            public float Shoulder => Withers - Sole;

            /// <summary>Crown above the sole: ears, antlers, a skunk's raised tail.</summary>
            public float Height => Crown - Sole;
        }

        /// <summary>
        /// Bake every <b>active</b> skinned mesh as it is posed now and measure it. The withers are
        /// the highest point of the body in a slab just behind the fore legs' hip joints, along
        /// the axis from the pelvis to the head — the top of the back over the shoulders, which is
        /// what "shoulder height" means for a four-legged animal and what a neck, a head or a
        /// raised tail must not be counted as.
        /// </summary>
        public static Size Measure(GameObject instance)
        {
            Transform root = instance.transform;
            var points = new List<Vector3>();
            var body = new List<bool>();
            var baked = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false))
                {
                    if (!skin.enabled || skin.sharedMesh == null) continue;
                    skin.BakeMesh(baked, useScale: true);
                    // BakeMesh with useScale has already applied the renderer's own local scale,
                    // so it comes out of the matrix or it counts twice. On the colonists that
                    // scale is one and FigureBuild need not care; the SIMPLE wolf's mesh node
                    // carries x0.786 (e-15) and would be measured a fifth too small.
                    Vector3 own = skin.transform.localScale;
                    Matrix4x4 toWorld = skin.transform.localToWorldMatrix *
                        Matrix4x4.Scale(new Vector3(1f / own.x, 1f / own.y, 1f / own.z));
                    Vector3[] vertices = baked.vertices;
                    BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    Transform[] bones = skin.bones;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        points.Add(toWorld.MultiplyPoint3x4(vertices[v]));
                        int bone = v < weights.Length ? weights[v].boneIndex0 : -1;
                        body.Add(bone < 0 || bone >= bones.Length || bones[bone] == null || IsBody(bones[bone].name));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
            if (points.Count == 0) return default;

            Vector3 forward = Forward(root);
            float sole = float.MaxValue, crown = float.MinValue, back = float.MaxValue, front = float.MinValue;
            foreach (Vector3 p in points)
            {
                if (p.y < sole) sole = p.y;
                if (p.y > crown) crown = p.y;
                float along = Vector3.Dot(p - root.position, forward);
                if (along < back) back = along;
                if (along > front) front = along;
            }
            float length = front - back;

            Transform? leftHip = Find(root, "l_FrontLeg_Hip"), rightHip = Find(root, "r_FrontLeg_Hip");
            float hipAlong = leftHip != null && rightHip != null
                ? Vector3.Dot((leftHip.position + rightHip.position) * 0.5f - root.position, forward)
                : back + length * 0.7f;

            // Over the fore legs and just behind them, and only the trunk: a neck rising from the
            // shoulders, a stag's antlers and a skunk's tail curled over its back all stand in this
            // slab on one rig or another, and none of them is the withers. A vertex belongs to the
            // bone that moves it most.
            float withers = sole;
            for (int i = 0; i < points.Count; i++)
            {
                if (!body[i]) continue;
                Vector3 p = points[i];
                float along = Vector3.Dot(p - root.position, forward);
                if (along < hipAlong - length * 0.15f || along > hipAlong + length * 0.05f) continue;
                if (p.y > withers) withers = p.y;
            }
            return new Size(sole, crown, withers, length);
        }

        static readonly string[] NotTrunk = { "Head", "Neck", "Jaw", "Ear", "Eye", "Tail", "Antler", "Horn" };

        /// <summary>Is a bone part of the trunk and legs, rather than the head, neck or tail?</summary>
        static bool IsBody(string bone)
        {
            foreach (string part in NotTrunk)
                if (bone.IndexOf(part, System.StringComparison.Ordinal) >= 0) return false;
            return true;
        }

        /// <summary>What <see cref="StanceSpeed"/> saw, for the probe's report when it reads zero.</summary>
        public static string StanceTrace(GameObject instance, AnimationClip clip)
        {
            Transform root = instance.transform;
            Transform? foot = Find(root, "l_FrontLeg_Ankle") ?? Find(root, "l_FrontLeg_Ball");
            if (foot == null)
            {
                var names = new System.Text.StringBuilder("no fore foot; transforms:");
                foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                    if (names.Length < 1500) names.Append(' ').Append(t.name);
                return names.ToString();
            }
            Vector3 forward = Forward(root);
            float aMin = float.MaxValue, aMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i <= 24; i++)
            {
                clip.SampleAnimation(instance, clip.length * i / 24);
                Vector3 local = foot.position - root.position;
                float a = Vector3.Dot(local, forward);
                aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
                yMin = Mathf.Min(yMin, local.y); yMax = Mathf.Max(yMax, local.y);
            }
            return $"{foot.name} along {aMin:0.000}..{aMax:0.000} up {yMin:0.000}..{yMax:0.000} fwd {forward} len {clip.length:0.00}s";
        }

        /// <summary>
        /// The ground speed at which a clip's feet do not slide, at the instance's current scale:
        /// a fore foot's speed backward along the body while it is on the ground (the lowest fifth
        /// of its travel), the median over the cycle. The SIMPLE clips have no root motion, so
        /// this is the only honest number (design 66 §6). Zero when the rig has no fore foot.
        /// </summary>
        public static float StanceSpeed(GameObject instance, AnimationClip clip, int samples = 96)
        {
            Transform root = instance.transform;
            Transform? foot = Find(root, "l_FrontLeg_Ankle") ?? Find(root, "l_FrontLeg_Ball");
            if (foot == null || clip.length <= 0f) return 0f;

            Vector3 forward = Forward(root);
            var along = new float[samples + 1];
            var up = new float[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                clip.SampleAnimation(instance, clip.length * i / samples);
                Vector3 local = foot.position - root.position;
                along[i] = Vector3.Dot(local, forward);
                up[i] = local.y;
            }

            float low = float.MaxValue, high = float.MinValue;
            foreach (float y in up) { if (y < low) low = y; if (y > high) high = y; }
            float ground = low + (high - low) * 0.2f;

            float dt = clip.length / samples;
            var speeds = new List<float>();
            for (int i = 0; i < samples; i++)
            {
                if (up[i] > ground || up[i + 1] > ground) continue;
                float v = -(along[i + 1] - along[i]) / dt;
                if (v > 0f) speeds.Add(v);
            }
            if (speeds.Count == 0) return 0f;
            speeds.Sort();
            return speeds[speeds.Count / 2];
        }

        /// <summary>
        /// Which way the animal faces: from its pelvis towards its head, flattened. The SIMPLE
        /// rigs do not all face the same axis in their files, and the head is the one thing that
        /// cannot be at the back.
        /// </summary>
        public static Vector3 Forward(Transform root)
        {
            Transform? head = Find(root, "Head");
            Transform? pelvis = Find(root, "ROOTSHJnt");
            if (head == null || pelvis == null) return root.forward;
            Vector3 d = head.position - pelvis.position;
            d.y = 0f;
            return d.sqrMagnitude > 1e-8f ? d.normalized : root.forward;
        }

        /// <summary>The first transform under <paramref name="root"/> whose name contains <paramref name="part"/>, depth first.</summary>
        public static Transform? Find(Transform root, string part)
        {
            if (root.name.IndexOf(part, System.StringComparison.Ordinal) >= 0) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform? found = Find(root.GetChild(i), part);
                if (found != null) return found;
            }
            return null;
        }
    }
}
