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

        static PawnView Colonist(int job = JobHandle.Wait, bool working = false) =>
            new PawnView(new PawnId(1), new CellRef(3, 3, 0), 800, 800, 600, job,
                working: working, workCell: working ? new CellRef(4, 3, 0) : default, flags: PawnFlags.Person);

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
        /// A colonist the simulation says is holding a machete is drawn holding one, in the right
        /// hand; it goes away while she works with a tool and comes back after; and
        /// a colonist holding nothing holds nothing — the control.
        /// </summary>
        [Test]
        public void AColonistHoldingAWeaponIsDrawnHoldingItAndPutsItAwayToWork()
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
                Assert.That(prop.parent, Is.Not.Null.And.Property("name").Contains("Hand"),
                    "the machete is not in a hand");
                Assert.That(director.ArmedFigures, Is.EqualTo(1));

                Step(director, Frame(120, Colonist(JobHandle.Fell, working: true), ItemIndex.Machete), 60);
                Assert.That(prop.gameObject.activeSelf, Is.False, "the machete stayed out while the axe was in the hand");

                Step(director, Frame(200, Colonist(), ItemIndex.Machete), 60);
                Assert.That(prop.gameObject.activeSelf, Is.True, "the machete did not come back after the work");

                Step(director, Frame(300, Colonist(), weapon: -1), 2);
                Assert.That(director.WeaponOf(id), Is.Null, "the machete stayed in a hand the simulation emptied");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
