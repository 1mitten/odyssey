#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The animal figures (design 29, AN4): the two rows resolve without the packs, a hog and a
    /// rat get figures through the colonists' director, and the hog's computed walk moves its
    /// legs when it walks and leaves them alone when it stands.
    ///
    /// <para><b>These run on the runner.</b> Every other figure test ignores itself where the
    /// Synty folder is absent; the animal art is the project's own and committed, so this is the
    /// first figure that CI can actually build.</para>
    /// </summary>
    public class AnimalFigureTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        static WorldSnapshot Frame(params PawnView[] pawns)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(10, 10, 4), 0);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            return snapshot;
        }

        static PawnView Standing(int id, int kind, int x, int z) =>
            new PawnView(new PawnId(id), new CellRef(x, z, 0), 800, 800, 600, JobHandle.Wait, kind: kind);

        [Test]
        public void TheAnimalRowsResolveFromTheProjectsOwnArt()
        {
            ModuleCatalogue catalogue = Catalogue();

            ModuleEntry? hog = catalogue.Find(ModuleIds.Animal(1));
            ModuleEntry? rat = catalogue.Find(ModuleIds.Animal(2));
            Assert.That(hog, Is.Not.Null, "the hog row is in the catalogue");
            Assert.That(rat, Is.Not.Null, "the rat row is in the catalogue");

            Assert.That(hog!.prefab, Is.Not.Null, "the hog's model resolved");
            Assert.That(AssetDatabase.GetAssetPath(hog.prefab), Does.StartWith("Assets/Art/Custom/"),
                "and it is the project's own, not a pack's");
            Assert.That(hog.quadrupedGait, Is.True, "the hog moves on a computed gait");
            Assert.That(hog.locomotion, Has.Count.EqualTo(1), "over its one idle clip");
            Assert.That(hog.locomotion[0].clip, Is.Not.Null);
            Assert.That(hog.locomotion[0].clip!.isLooping, Is.True, "which loops, or the hog would freeze");

            Assert.That(rat!.prefab, Is.Not.Null);
            Assert.That(rat.quadrupedGait, Is.False, "the rat walks on its own clips");
            Assert.That(rat.locomotion, Has.Count.EqualTo(3));
            foreach (LocomotionEntry gait in rat.locomotion)
            {
                Assert.That(gait.clip, Is.Not.Null, $"{gait.clipName} resolved");
                Assert.That(gait.clip!.isLooping, Is.True, $"{gait.clipName} loops");
            }
            Assert.That(rat.locomotion[1].metresPerSecond, Is.GreaterThan(0f), "the walk has a declared speed");
            Assert.That(rat.locomotion[2].metresPerSecond, Is.GreaterThan(rat.locomotion[1].metresPerSecond),
                "and the run is faster than the walk, or the blend has nothing to blend");

            Assert.That(ModuleIds.Animal(0), Is.Empty, "the colonist has no animal row");
            Assert.That(ModuleIds.Animal(3), Is.Empty, "nor the bandit, a person");
            Assert.That(ModuleIds.Animal(4), Is.Empty, "nor the gunman");
            Assert.That(ModuleIds.Animal(99), Is.Empty, "nor does a kind past the table");
        }

        /// <summary>
        /// <b>The frog hops on its own Jump clip</b> (design 30 §8): kind 5's row resolves from the
        /// project's art, asks for the hop pacing rather than the computed trot, and has the idle
        /// and the jump as its two gaits — both looping, or the frog would take one hop and freeze
        /// in the air.
        /// </summary>
        [Test]
        public void TheFrogRowResolvesAndHopsOnItsJumpClip()
        {
            ModuleEntry? frog = Catalogue().Find(ModuleIds.Animal(5));
            Assert.That(frog, Is.Not.Null, "the frog row is in the catalogue");
            Assert.That(frog!.prefab, Is.Not.Null, "the frog's model resolved");
            Assert.That(AssetDatabase.GetAssetPath(frog.prefab), Does.StartWith("Assets/Art/Custom/"));
            Assert.That(frog.hopGait, Is.True, "the frog is paced to its hop");
            Assert.That(frog.quadrupedGait, Is.False, "and not given the hog's trot");
            Assert.That(frog.locomotion, Has.Count.EqualTo(2));
            foreach (LocomotionEntry gait in frog.locomotion)
            {
                Assert.That(gait.clip, Is.Not.Null, $"{gait.clipName} resolved");
                Assert.That(gait.clip!.isLooping, Is.True, $"{gait.clipName} loops");
            }
            Assert.That(frog.locomotion[1].clipName, Does.Contain("Jump"), "the moving gait is the jump");
            Assert.That(frog.locomotion[1].metresPerSecond, Is.GreaterThan(0f), "with a declared speed");
        }

        /// <summary>
        /// <b>A hop is still on the ground and quick in the air.</b> The drawn fraction of a hop —
        /// the even position plus the lead — is nought through the crouch and one after landing,
        /// the lead is nought at both ends of the cycle so the loop joins with no step, and it is
        /// never more than half a hop either way.
        /// </summary>
        [Test]
        public void AHopHoldsStillOnTheGroundAndCarriesThroughTheAir()
        {
            Assert.That(PawnFigureDirector.HopLead(0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(PawnFigureDirector.HopLead(1f), Is.EqualTo(0f).Within(1e-5f));
            for (int i = 0; i <= 100; i++)
            {
                float phase = i / 100f;
                float drawn = phase + PawnFigureDirector.HopLead(phase);
                if (phase <= PawnFigureDirector.HopLiftOff)
                    Assert.That(drawn, Is.EqualTo(0f).Within(1e-5f), $"moving on the ground at {phase:F2}");
                else if (phase >= PawnFigureDirector.HopTouchDown)
                    Assert.That(drawn, Is.EqualTo(1f).Within(1e-5f), $"moving after landing at {phase:F2}");
                Assert.That(Mathf.Abs(PawnFigureDirector.HopLead(phase)), Is.LessThanOrEqualTo(0.5f + 1e-5f));
            }
        }

        [Test]
        public void AHogAndARatGetFiguresAndAnUnknownKindDoesNot()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                Assume.That(director.Enabled, Is.True, "the animal rows alone make the director able to draw");

                WorldSnapshot frame = Frame(Standing(1, 1, 2, 2), Standing(2, 2, 4, 2), Standing(3, 9, 6, 2),
                    Standing(4, 4, 8, 2));
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 0.016f);

                Assert.That(director.HasFigureFor(1), Is.True, "the hog is drawn as a figure");
                Assert.That(director.HasFigureFor(2), Is.True, "and so is the rat");
                Assert.That(director.HasFigureFor(4), Is.True, "and so is the frog");
                Assert.That(director.HasFigureFor(3), Is.False, "a kind with no row is not drawn here at all");

                int animals = 0;
                foreach (Transform child in parent.transform)
                    if (child.name.StartsWith("Animal figure")) animals++;
                Assert.That(animals, Is.EqualTo(3));
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TheHogsLegsTrotWhenItMovesAndRestWhenItStands()
        {
            ModuleEntry hog = Catalogue().Find(ModuleIds.Animal(1))!;
            Assume.That(hog.prefab, Is.Not.Null);

            GameObject moving = Object.Instantiate(hog.prefab!);
            GameObject standing = Object.Instantiate(hog.prefab!);
            try
            {
                QuadrupedGait? gait = QuadrupedGait.Bind(moving.transform);
                Assert.That(gait, Is.Not.Null, "the rig has the four legs the gait looks for");

                // The stride is the rig's, not a number anyone typed: the pig's legs are about a
                // quarter of a metre from shoulder joint to sole, so the cycle turns every few
                // tenths of a metre rather than every metre. That was the whole of "it looks odd".
                Assert.That(gait!.LegMetres, Is.InRange(0.15f, 0.35f), $"the measured leg is {gait.LegMetres:F3} m");
                Assert.That(gait.Stride, Is.InRange(0.2f, 0.7f), $"the derived stride is {gait.Stride:F3} m");

                Transform fore = FindDeep(moving.transform, "FrontUpLeg.L")!;
                Transform hind = FindDeep(moving.transform, "BackUpLeg.R")!;
                Transform otherFore = FindDeep(moving.transform, "FrontUpLeg.R")!;
                Transform foreLow = FindDeep(moving.transform, "FrontLowLeg.L")!;
                Transform otherLow = FindDeep(moving.transform, "FrontLowLeg.R")!;
                Quaternion foreBefore = fore.rotation, hindBefore = hind.rotation, otherBefore = otherFore.rotation;
                Quaternion foreLowBefore = foreLow.rotation, otherLowBefore = otherLow.rotation;
                Vector3 right = moving.transform.right;

                // A quarter of a stride at full weight: the right fore is a quarter into its
                // stance and the left fore, half a cycle away, is in the middle of its swing.
                gait.Advance(1f, gait.Stride);
                gait.Advance(1f, gait.Stride * 0.25f);
                Assert.That(gait.Phase, Is.EqualTo(0.25f).Within(1e-3f));
                gait.Apply(right, moving.transform.up);

                float foreSwing = PitchAbout(foreBefore, fore.rotation, right);
                float hindSwing = PitchAbout(hindBefore, hind.rotation, right);
                float otherSwing = PitchAbout(otherBefore, otherFore.rotation, right);
                Assert.That(Mathf.Abs(foreSwing), Is.GreaterThan(5f), "the left fore leg swung");
                // A trot: the left fore and the right hind are a diagonal pair and swing together;
                // the right fore is half a cycle away and swings the other way.
                Assert.That(Mathf.Sign(hindSwing), Is.EqualTo(Mathf.Sign(foreSwing)), "its diagonal partner swung the same way");
                Assert.That(hindSwing, Is.EqualTo(foreSwing).Within(4f), "and about as far: a pelvis rocks a little less than a scapula slides");
                Assert.That(Mathf.Sign(otherSwing), Is.EqualTo(-Mathf.Sign(foreSwing)), "the opposite fore leg swung the other way");
                // Stance and swing are different shapes (owner, 2026-09-22, fifth look): the
                // planted leg is straight and its carpus only follows the hip, while the swinging
                // leg's carpus is folded back to carry the foot low and flat.
                float plantedFold = PitchAbout(otherLowBefore, otherLow.rotation, right) - otherSwing;
                float swingingFold = PitchAbout(foreLowBefore, foreLow.rotation, right) - foreSwing;
                Assert.That(Mathf.Abs(plantedFold), Is.LessThan(1f), "the planted right fore is straight");
                Assert.That(Mathf.Abs(swingingFold), Is.GreaterThan(20f), "the swinging left fore has its carpus folded");

                // The control: a hog standing still keeps the clip's pose exactly.
                QuadrupedGait still = QuadrupedGait.Bind(standing.transform)!;
                Transform stillLeg = FindDeep(standing.transform, "FrontUpLeg.L")!;
                Quaternion rest = stillLeg.rotation;
                still.Advance(0f, 1f);
                Assert.That(still.Weight, Is.Zero);
                Assert.That(still.Phase, Is.Zero, "the cycle does not run while standing");
                still.Apply(standing.transform.right, standing.transform.up);
                Assert.That(Quaternion.Angle(rest, stillLeg.rotation), Is.LessThan(1e-3f),
                    "a standing hog's legs are the clip's alone");
            }
            finally
            {
                Object.DestroyImmediate(moving);
                Object.DestroyImmediate(standing);
            }
        }

        /// <summary>
        /// The cursor round an animal is the animal's own box, not the person-sized column
        /// (owner, 2026-09-22: it highlighted the whole tile). The hog is about 1.2 m long and
        /// 0.6 m tall; the rat a quarter of that; both boxes are far smaller than the colonist's.
        /// </summary>
        [Test]
        public void AnAnimalsCursorBoxIsItsOwnSizeAndAColonistsIsNot()
        {
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                Assume.That(director.Enabled, Is.True);
                WorldSnapshot frame = Frame(Standing(1, 1, 2, 2), Standing(2, 2, 4, 2));
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 0.016f);

                Assert.That(director.TryGetAnimalBox(new PawnId(1), out Matrix4x4 place, out Vector3 hog), Is.True);
                Assert.That(hog.z, Is.InRange(1.0f, 1.5f), $"the hog's box is its body length, {hog}");
                Assert.That(hog.y, Is.InRange(0.45f, 0.8f), $"and its standing height, {hog}");
                Assert.That(hog.x, Is.InRange(0.25f, 0.6f), $"and its width, {hog}");
                Assert.That(place.GetPosition().y, Is.InRange(0.1f, 0.5f), "centred on the body, not on the floor");

                Assert.That(director.TryGetAnimalBox(new PawnId(2), out _, out Vector3 rat), Is.True);
                Assert.That(rat.y, Is.LessThan(hog.y * 0.6f), "the rat's box is a rat's");
                Assert.That(rat.z, Is.LessThan(hog.z), "shorter than the hog's");
                Assert.That(rat.z, Is.GreaterThan(0.3f), "but the tail is in it");

                Assert.That(director.TryGetAnimalBox(new PawnId(99), out _, out _), Is.False, "no figure, no box");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ARigWithoutTheLegsGetsNoGait()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(QuadrupedGait.Bind(cube.transform), Is.Null,
                    "a rig without the four legs walks on its clips, as a colonist with no arms swings no axe");
            }
            finally { Object.DestroyImmediate(cube); }
        }

        /// <summary>
        /// The step is a stance and a swing, not a sine (owner, 2026-09-22, fifth look): a
        /// planted foot sweeps back in a straight line at the body's speed, and a swinging one
        /// lifts quickly, carries flat and plants sharply, with no fold at all while planted.
        /// </summary>
        [Test]
        public void TheStepIsAStanceAndASwingNotASine()
        {
            float duty = QuadrupedGait.Duty;
            Assert.That(QuadrupedGait.HipAt(0f), Is.EqualTo(1f).Within(1e-4f), "the foot plants fully forward");
            Assert.That(QuadrupedGait.HipAt(duty * 0.5f), Is.EqualTo(0f).Within(1e-4f), "sweeps back in a straight line");
            Assert.That(QuadrupedGait.HipAt(duty), Is.EqualTo(-1f).Within(1e-4f), "and lifts fully back");
            Assert.That(QuadrupedGait.HipAt(0.999f), Is.EqualTo(1f).Within(1e-2f), "and is at the front again as the cycle closes");
            for (float p = 0f; p < duty; p += 0.05f)
                Assert.That(QuadrupedGait.FlexAt(p), Is.Zero, $"no fold while planted, at {p:F2}");
            float swing = 1f - duty;
            Assert.That(QuadrupedGait.FlexAt(duty + swing * 0.34f), Is.EqualTo(1f).Within(1e-3f), "folded within a third of the swing: a quick lift");
            Assert.That(QuadrupedGait.FlexAt(duty + swing * 0.5f), Is.EqualTo(1f), "and held: a flat carry");
            Assert.That(QuadrupedGait.FlexAt(duty + swing * 0.74f), Is.EqualTo(1f), "until the last quarter");
            Assert.That(QuadrupedGait.FlexAt(0.999f), Is.LessThan(0.01f), "and straight as it lands: a sharp plant");
        }

        /// <summary>
        /// A foot leaves the ground in its swing and lands in front of where it stood (owner,
        /// 2026-09-22, fifth look). Measured where the sole is drawn — the lower segment's end,
        /// along its own bone — because the rig's <c>Foot</c> bones are IK targets outside the
        /// chain that sit on the ground whatever the leg does. This is the test that would have
        /// caught the first signs: with them, the hind sole went three centimetres <i>under</i>
        /// the ground at mid-swing and every foot planted behind where it had lifted.
        /// </summary>
        [Test]
        public void AFootLiftsInItsSwingAndPlantsInFront()
        {
            ModuleEntry hog = Catalogue().Find(ModuleIds.Animal(1))!;
            Assume.That(hog.prefab, Is.Not.Null);
            GameObject moving = Object.Instantiate(hog.prefab!);
            try
            {
                QuadrupedGait gait = QuadrupedGait.Bind(moving.transform)!;
                Vector3 forward = moving.transform.forward, up = moving.transform.up, right = moving.transform.right;
                string[] lows = { "BackLowLeg.L", "FrontLowLeg.L", "BackLowLeg.R", "FrontLowLeg.R" };
                string[] feet = { "BackFoot.L", "FrontFoot.L", "BackFoot.R", "FrontFoot.R" };
                var rest = new Vector3[4];
                var length = new float[4];
                for (int i = 0; i < 4; i++)
                {
                    Transform low = FindDeep(moving.transform, lows[i])!;
                    length[i] = Vector3.Distance(low.position, FindDeep(moving.transform, feet[i])!.position);
                    rest[i] = Sole(low, length[i]);
                }

                // Weight up, then to the plant of the left hind (phase 0): its sole is in front
                // of its rest and on the ground; half a cycle on, mid-swing, it is lifted.
                gait.Advance(1f, gait.Stride);
                gait.Apply(right, up);
                Vector3 planted = Sole(FindDeep(moving.transform, lows[0])!, length[0]);
                Assert.That(Vector3.Dot(planted - rest[0], forward), Is.GreaterThan(0.05f), "the hind foot plants in front of where it stands");
                Assert.That(Vector3.Dot(planted - rest[0], up), Is.InRange(-0.01f, 0.06f), "and on, or just above, the ground: a straight leg reaching forward stands its sole a few centimetres up, which the plant closes");
                Vector3 forePlanted = Sole(FindDeep(moving.transform, lows[1])!, length[1]);
                Assert.That(Vector3.Dot(forePlanted - rest[1], forward), Is.LessThan(-0.03f), "the left fore, half a cycle away, is at the back of its stance");

                float swingMid = QuadrupedGait.Duty + (1f - QuadrupedGait.Duty) * 0.5f;
                gait.Advance(1f, gait.Stride * swingMid);
                gait.Apply(right, up);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 sole = Sole(FindDeep(moving.transform, lows[i])!, length[i]);
                    float lift = Vector3.Dot(sole - rest[i], up);
                    bool swinging = i == 0 || i == 3;
                    if (swinging)
                        Assert.That(lift, Is.GreaterThan(0.015f), $"{lows[i]} is lifted mid-swing, not dragged: {lift * 100f:F1} cm");
                    else
                        Assert.That(lift, Is.InRange(-0.02f, 0.04f), $"{lows[i]} is planted: {lift * 100f:F1} cm");
                }
            }
            finally
            {
                Object.DestroyImmediate(moving);
            }
        }

        static Vector3 Sole(Transform lower, float length) => lower.position + lower.up * length;

        /// <summary>The signed pitch from one rotation to another about an axis, degrees.</summary>
        static float PitchAbout(Quaternion before, Quaternion after, Vector3 axis)
        {
            (after * Quaternion.Inverse(before)).ToAngleAxis(out float angle, out Vector3 a);
            if (angle > 180f) { angle = 360f - angle; a = -a; }
            return angle * Mathf.Sign(Vector3.Dot(a, axis));
        }

        static Transform? FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform? found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
