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
            Assert.That(hog.strideMetres, Is.GreaterThan(0f), "the hog walks on a computed gait");
            Assert.That(hog.locomotion, Has.Count.EqualTo(1), "over its one idle clip");
            Assert.That(hog.locomotion[0].clip, Is.Not.Null);
            Assert.That(hog.locomotion[0].clip!.isLooping, Is.True, "which loops, or the hog would freeze");

            Assert.That(rat!.prefab, Is.Not.Null);
            Assert.That(rat.strideMetres, Is.Zero, "the rat walks on its own clips");
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
            Assert.That(ModuleIds.Animal(99), Is.Empty, "nor does a kind past the table");
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

                WorldSnapshot frame = Frame(Standing(1, 1, 2, 2), Standing(2, 2, 4, 2), Standing(3, 9, 6, 2));
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 0.016f);

                Assert.That(director.HasFigureFor(1), Is.True, "the hog is drawn as a figure");
                Assert.That(director.HasFigureFor(2), Is.True, "and so is the rat");
                Assert.That(director.HasFigureFor(3), Is.False, "a kind with no row is not drawn here at all");

                int animals = 0;
                foreach (Transform child in parent.transform)
                    if (child.name.StartsWith("Animal figure")) animals++;
                Assert.That(animals, Is.EqualTo(2));
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TheHogsLegsSwingWhenItWalksAndRestWhenItStands()
        {
            ModuleEntry hog = Catalogue().Find(ModuleIds.Animal(1))!;
            Assume.That(hog.prefab, Is.Not.Null);

            GameObject walking = Object.Instantiate(hog.prefab!);
            GameObject standing = Object.Instantiate(hog.prefab!);
            try
            {
                QuadrupedGait? gait = QuadrupedGait.Bind(walking.transform, hog.strideMetres);
                Assert.That(gait, Is.Not.Null, "the rig has the four legs the gait looks for");

                Transform leg = walking.transform.Find("Armature/root/Body/FrontLeg.L/FrontUpLeg.L")
                    ?? FindDeep(walking.transform, "FrontUpLeg.L")!;
                Assume.That(leg, Is.Not.Null);
                Quaternion before = leg.rotation;

                // A tenth of a second at one metre a second is a tenth of a stride: the left fore
                // leg is a quarter-cycle ahead, so it is near the top of its swing.
                gait!.Advance(1f, 0.1f);
                Assert.That(gait.Phase, Is.EqualTo(0.1f).Within(1e-4f));
                Assert.That(gait.Weight, Is.GreaterThan(0f), "the walk is fading in");
                gait.Apply(walking.transform.right, walking.transform.up);
                Assert.That(Quaternion.Angle(before, leg.rotation), Is.GreaterThan(1f),
                    "the leg swung");

                // The control: a hog standing still keeps the clip's pose exactly.
                QuadrupedGait still = QuadrupedGait.Bind(standing.transform, hog.strideMetres)!;
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
                Object.DestroyImmediate(walking);
                Object.DestroyImmediate(standing);
            }
        }

        [Test]
        public void ARigWithoutTheLegsGetsNoGait()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(QuadrupedGait.Bind(cube.transform, 1f), Is.Null,
                    "a rig without the four legs walks on its clips, as a colonist with no arms swings no axe");
                Assert.That(QuadrupedGait.Bind(cube.transform, 0f), Is.Null, "and no stride means no computed walk");
            }
            finally { Object.DestroyImmediate(cube); }
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
