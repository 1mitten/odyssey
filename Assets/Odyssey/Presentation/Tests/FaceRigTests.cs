#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The one thing that writes a face onto bones (design 59 §6). The rule under test is that a
    /// pose is written <b>absolutely from rest</b>, in the root's authored units, whatever the
    /// bones' own axes and scales happen to be — so the synthetic rig here is built deliberately
    /// awkward: a head turned on its side, brows under a scaled parent, eyes whose "up" is their
    /// local Z. It needs no licensed art and runs on every machine. The last test checks the same
    /// rule on every colonist body the catalogue can resolve.
    /// </summary>
    public class FaceRigTests
    {
        GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        /// <summary>root (scaled 1.4) / head (rotated, scaled) / Eyebrows, Eyes (rotated so up is local Z).</summary>
        Transform Rig(out Transform head, out Transform brows, out Transform eyes)
        {
            _root = new GameObject("root");
            _root.transform.localScale = Vector3.one * 1.4f;
            _root.transform.rotation = Quaternion.Euler(0f, 37f, 0f);

            head = new GameObject("Head").transform;
            head.SetParent(_root.transform, false);
            head.localPosition = new Vector3(0f, 1.57f, 0f);
            head.localRotation = Quaternion.Euler(0f, 0f, 90f);
            head.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            brows = new GameObject("Eyebrows").transform;
            brows.SetParent(head, false);
            brows.localPosition = new Vector3(13f, 0f, 11.8f);
            brows.localRotation = Quaternion.Euler(12f, 0f, 0f);

            eyes = new GameObject("Eyes").transform;
            eyes.SetParent(head, false);
            eyes.localPosition = new Vector3(9.5f, 0f, 11.8f);
            eyes.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            eyes.localScale = new Vector3(1f, 2f, 3f);
            return _root.transform;
        }

        [Test]
        public void ARaisedBrowGoesUpTheHeadByTheLiftAtTheFiguresScale()
        {
            Transform root = Rig(out Transform head, out Transform brows, out _);
            FaceRig rig = FaceRig.Bind(root, head)!;
            Vector3 before = brows.position;

            rig.Apply(FacePose.Of(FaceExpression.Raised));

            Vector3 moved = brows.position - before;
            float expected = FacePose.Of(FaceExpression.Raised).BrowLift * 1.4f;
            Assert.That(Vector3.Dot(moved, root.up), Is.EqualTo(expected).Within(1e-5f), "up the root, at its scale");
            Assert.That((moved - root.up * Vector3.Dot(moved, root.up)).magnitude, Is.LessThan(1e-5f), "and nowhere else");
        }

        [Test]
        public void RestPutsEveryBoneBackExactly()
        {
            Transform root = Rig(out Transform head, out Transform brows, out Transform eyes);
            Vector3 p = brows.localPosition, s = eyes.localScale;
            Quaternion r = brows.localRotation;
            FaceRig rig = FaceRig.Bind(root, head)!;

            rig.Apply(FacePose.Of(FaceExpression.Sceptical));
            rig.Apply(FacePose.Of(FaceExpression.Alarmed));
            rig.Apply(FacePose.Rest);

            Assert.That(brows.localPosition, Is.EqualTo(p));
            Assert.That(brows.localRotation, Is.EqualTo(r));
            Assert.That(eyes.localScale, Is.EqualTo(s));
        }

        [Test]
        public void APoseWrittenTwiceIsThePoseWrittenOnce()
        {
            Transform root = Rig(out Transform head, out Transform brows, out Transform eyes);
            FaceRig rig = FaceRig.Bind(root, head)!;
            FacePose stern = FacePose.Of(FaceExpression.Sceptical);
            rig.Apply(stern);
            Vector3 p = brows.localPosition, s = eyes.localScale;
            Quaternion r = brows.localRotation;
            for (int i = 0; i < 100; i++) rig.Apply(stern);
            Assert.That(brows.localPosition, Is.EqualTo(p), "absolute, not added: no wind-up");
            Assert.That(Quaternion.Angle(brows.localRotation, r), Is.LessThan(1e-3f));
            Assert.That(eyes.localScale, Is.EqualTo(s));
        }

        [Test]
        public void ABlinkShutsTheEyesAlongTheAxisThatPointsUpTheHead()
        {
            Transform root = Rig(out Transform head, out _, out Transform eyes);
            FaceRig rig = FaceRig.Bind(root, head)!;
            Vector3 rest = eyes.localScale;

            rig.Apply(new FacePose(0f, 0f, 0.08f, 1f));

            // Eyes rotated -90 about X under a head rolled 90: find the axis that points up the root.
            int up = 0;
            float most = -1f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = i == 0 ? eyes.right : i == 1 ? eyes.up : eyes.forward;
                float d = Mathf.Abs(Vector3.Dot(axis, root.up));
                if (d > most) { most = d; up = i; }
            }
            for (int i = 0; i < 3; i++)
                Assert.That(eyes.localScale[i], Is.EqualTo(i == up ? rest[i] * 0.08f : rest[i]).Within(1e-6f),
                    $"axis {i}");
        }

        [Test]
        public void ANodDipsTheFaceWhereverTheHeadIsTurned()
        {
            Transform root = Rig(out Transform head, out _, out _);
            // Which way the face points, in the head's own frame, read at rest.
            Vector3 local = head.InverseTransformDirection(root.forward);
            FaceRig rig = FaceRig.Bind(root, head)!;

            // Turned 50 degrees to the side, as the gaze might have it.
            head.rotation = Quaternion.AngleAxis(50f, root.up) * head.rotation;
            Vector3 face = head.TransformDirection(local);
            Vector3 position = head.position;

            rig.Nod(head, 10f, 0f);

            Vector3 now = head.TransformDirection(local);
            Assert.That(Vector3.Angle(face, now), Is.EqualTo(10f).Within(0.01f), "ten degrees");
            Assert.That(now.y, Is.LessThan(face.y - 0.1f), "down, chin first");
            Assert.That(Vector3.Dot(new Vector3(now.x, 0f, now.z).normalized, new Vector3(face.x, 0f, face.z).normalized),
                Is.GreaterThan(0.9999f), "and still turned the same way: a nod, not a tilt of the ear");
            Assert.That(head.position, Is.EqualTo(position), "about the head's own pivot");

            rig.Nod(head, 0f, 0f);
            Assert.That(Vector3.Angle(head.TransformDirection(local), now), Is.LessThan(1e-3f), "no nod is no change");
        }

        [Test]
        public void TheFacePassFor64FiguresCostsMicroseconds()
        {
            // What ApplyFaces does per live figure, sixty-four of them (the figure ceiling): step,
            // write the two bones, nod the head. Design 59 §9 carries the number.
            const int figures = 64, frames = 2000;
            var roots = new GameObject[figures];
            var heads = new Transform[figures];
            var rigs = new FaceRig[figures];
            var faces = new FaceMotion[figures];
            try
            {
                for (int i = 0; i < figures; i++)
                {
                    Transform root = Rig(out heads[i], out _, out _);
                    roots[i] = root.gameObject;
                    _root = null;
                    rigs[i] = FaceRig.Bind(root, heads[i])!;
                    faces[i] = FaceMotion.Start(i);
                    faces[i].Role = (TalkRole)(i % 3);
                    faces[i].Expression = (FaceExpression)(i % 6);
                }
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (int f = 0; f < frames; f++)
                    for (int i = 0; i < figures; i++)
                    {
                        faces[i].Step(1f / 60f);
                        rigs[i].Apply(faces[i].Pose);
                        rigs[i].Nod(heads[i], faces[i].NodPitch, faces[i].NodRoll);
                    }
                double perFrame = clock.Elapsed.TotalMilliseconds / frames;
                TestContext.WriteLine($"face pass, 64 figures: {perFrame * 1000.0:F1} us a frame");
                Assert.That(perFrame, Is.LessThan(1.0), "a ceiling against a mistake, not a budget");
            }
            finally
            {
                foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AHeadWithNeitherBoneHasNoFace()
        {
            _root = new GameObject("root");
            var head = new GameObject("Head").transform;
            head.SetParent(_root.transform, false);
            new GameObject("Hair").transform.SetParent(head, false);
            Assert.That(FaceRig.Bind(_root.transform, head), Is.Null);
        }

        [Test]
        public void EveryColonistBodyTheCatalogueResolvesHasBothBones()
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            if (catalogue == null) Assert.Ignore("no catalogue");
            int bodies = 0, faces = 0;
            foreach (ModuleEntry row in catalogue!.FindFamily(ModuleIds.ColonistBase))
            {
                if (row.prefab == null) continue;
                GameObject instance = Object.Instantiate(row.prefab);
                try
                {
                    var animator = instance.GetComponent<Animator>();
                    Transform? head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                    if (head == null) continue;
                    bodies++;
                    FaceRig? rig = FaceRig.Bind(instance.transform, head);
                    if (rig != null && rig.HasBrows && rig.HasEyes) faces++;
                    else TestContext.WriteLine($"{row.prefab.name}: no face");
                }
                finally { Object.DestroyImmediate(instance); }
            }
            // The art is licensed and absent on the runner: ask whether any body resolved, not
            // whether there is a catalogue (CLAUDE.md, the runner's rule).
            if (bodies == 0) Assert.Ignore("no colonist art resolved on this machine");
            TestContext.WriteLine($"{faces} of {bodies} colonist bodies have both facial bones");
            Assert.That(faces, Is.EqualTo(bodies));
        }
    }
}
