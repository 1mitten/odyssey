#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The weapon drawn in the right hand and lying on the ground (design 33 §1, C3; added at the
    /// C2/C3 integration, 2026-09-23, because no lane owned it). The hand test asks whether the
    /// art <em>resolved</em> — a colonist to hold it and the weapon's own row — and ignores itself
    /// on the runner, which has no <c>Assets/Synty</c>; the lie-flat arithmetic runs everywhere.
    /// </summary>
    public class WeaponPropTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        static WorldSnapshot Frame(int tick, PawnView pawn, int weapon)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(12, 12, 4), 0);
            snapshot.AddPawn(pawn);
            if (weapon >= 0) snapshot.AddPawnAspect(new PawnAspect(pawn.Id, CombatAspectNames.WeaponKey, weapon));
            return snapshot;
        }

        static PawnView Colonist(int job = JobHandle.Wait, bool working = false, PawnFlags flags = PawnFlags.Person) =>
            new PawnView(new PawnId(1), new CellRef(3, 3, 0), 800, 800, 600, job,
                working: working, workCell: working ? new CellRef(4, 3, 0) : default, flags: flags);

        static void Step(PawnFigureDirector director, WorldSnapshot frame, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 1f / 60f);
                director.Evaluate(1f / 60f);
            }
        }

        /// <summary>
        /// A weapon modelled haft-up lies on its broadest face: whichever axis is thinnest ends up
        /// vertical, and a piece already lying flat is not turned.
        /// </summary>
        [Test]
        public void LyingFlatTurnsTheThinnestAxisUp()
        {
            var sizes = new[]
            {
                new Vector3(0.04f, 0.9f, 0.12f), // a machete, x thinnest
                new Vector3(0.12f, 0.9f, 0.04f), // the same, z thinnest
                new Vector3(0.8f, 0.05f, 0.2f),  // already lying
            };
            foreach (Vector3 size in sizes)
            {
                Quaternion lie = ModuleLibrary.LieFlat(size);
                Vector3 turned = lie * size; // a quarter turn, so the turned box is the turned size
                float thinnest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                Assert.That(Mathf.Abs(turned.y), Is.EqualTo(thinnest).Within(1e-4f), $"{size}: not lying on its broadest face");
            }
            Assert.That(ModuleLibrary.LieFlat(new Vector3(0.8f, 0.05f, 0.2f)), Is.EqualTo(Quaternion.identity));
        }

        /// <summary>
        /// A colonist the simulation says is holding a machete is drawn wearing it — at the hip at
        /// peace, in the right hand once the simulation says drawn (design 33 §8b) — and still at
        /// the hip, and still showing, while she works with a tool; a colonist holding nothing
        /// holds nothing — the control. Until 2026-09-23 the weapon was in the hand whenever the
        /// hand was free and hidden while she worked.
        /// </summary>
        [Test]
        public void AColonistHoldingAWeaponWearsItAtTheHipAndDrawsItWhenTheSimulationSays()
        {
            ModuleCatalogue catalogue = Catalogue();
            if (catalogue.Find(ModuleIds.ItemMachete)?.prefab == null)
                Assert.Ignore("no machete art here: nothing to hold");

            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                if (!director.CanDrawColonists) Assert.Ignore("no colonist art here: nobody to hold it");
                var id = new PawnId(1);

                Step(director, Frame(100, Colonist(), weapon: -1), 10);
                Assert.That(director.WeaponOf(id), Is.Null, "the control: bare hands held a prop");

                Step(director, Frame(110, Colonist(), ItemIndex.Machete), 10);
                Transform? prop = director.WeaponOf(id);
                Assert.That(prop, Is.Not.Null, "the machete in the simulation's hand was not drawn");
                Assert.That(prop!.gameObject.activeInHierarchy, Is.True, "the machete was made and hidden");
                Assert.That(director.PelvisOf(id), Is.Not.Null, "a colonist rig with no left thigh");
                Assert.That(director.TryGetWeaponPlace(id, out bool atHip, out Transform? on), Is.True);
                Assert.That(atHip, Is.True, "at peace, and the machete was out");
                Assert.That(on, Is.SameAs(director.PelvisOf(id)), "sheathed, but not on the pelvis");
                Assert.That(director.ArmedFigures, Is.EqualTo(1));

                // Drafted: out. Long enough for the pack's draw (about a second) or at once without it.
                Step(director, Frame(120, Colonist(flags: PawnFlags.Person | PawnFlags.Drafted | PawnFlags.Drawn),
                    ItemIndex.Machete), 120);
                Assert.That(director.TryGetWeaponPlace(id, out atHip, out on), Is.True);
                Assert.That(atHip, Is.False, "drafted, and the machete stayed at the hip");
                Assert.That(on, Is.SameAs(director.RightHandOf(id)), "drawn, but not in the right hand");
                Assert.That(on!.name, Does.Contain("Hand"));

                // Released, and at work: at the hip at once, showing, with the axe in the hand.
                Step(director, Frame(130, Colonist(JobHandle.Fell, working: true), ItemIndex.Machete), 60);
                Assert.That(prop.gameObject.activeSelf, Is.True, "the machete was hidden while she worked");
                Assert.That(director.TryGetWeaponPlace(id, out atHip, out _), Is.True);
                Assert.That(atHip, Is.True, "the machete stayed in the hand beside the axe");

                Step(director, Frame(300, Colonist(), weapon: -1), 2);
                Assert.That(director.WeaponOf(id), Is.Null, "the machete stayed on a colonist the simulation emptied");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
