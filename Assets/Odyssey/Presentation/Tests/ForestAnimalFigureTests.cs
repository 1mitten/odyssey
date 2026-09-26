#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
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
    /// The forest animals' art (design 66, FA1): every form's rows are in the catalogue in the
    /// shape the figure deals from, and — where SIMPLE Forest Animals is installed — each kind is
    /// drawn at life size against a colonist, the stag as a stag and the doe as a doe, and lowers
    /// its head to eat.
    ///
    /// <para><b>Runner safety (design 66 §7).</b> The pack is licensed and absent on the runner.
    /// The catalogue test needs no art and runs everywhere; every drawn test asks
    /// <see cref="PawnFigureDirector.CanDrawKind"/> for the kind it draws and ignores itself when
    /// that kind's art did not resolve — never whether a catalogue exists, and never
    /// <c>Enabled</c>, which the CC0 hog, rat and frog make true on every machine.</para>
    /// </summary>
    public class ForestAnimalFigureTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";
        const string ForestFolder = "Assets/Synty/SimpleForestAnimal";

        /// <summary>The colonists' draw factor (design 66 §5), which the rows are scaled against.</summary>
        const float DrawFactor = 1.42f;

        /// <summary>Kind, form, the prefab its rows start with, how many colourways, and the life shoulder in metres.</summary>
        static readonly (int Kind, int Form, string Prefab, int Coats, float Life)[] Forms =
        {
            (10, 0, "Rabbit", 3, 0.25f),
            (11, 0, "Doe", 2, 1.00f),
            (11, 1, "Stag", 3, 1.20f),
            (12, 0, "Fox", 3, 0.40f),
            (13, 0, "Raccoon", 2, 0.30f),
            (14, 0, "Skunk", 3, 0.25f),
            (15, 0, "Boar", 3, 0.90f),
            (16, 0, "Moose_Female", 2, 1.80f),
            (16, 1, "Moose_Male", 3, 1.90f),
            (17, 0, "Wolf", 3, 0.80f),
            (18, 0, "Bear", 3, 1.10f),
        };

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        /// <summary>
        /// Structure, not art, so it runs on the runner too: each kind's family is its forms'
        /// colourways in order, variant 0 on the bare id, every row named under the pack's own
        /// folder with the rig's Idle, Walk, Run and Eat, its form marked, and a positive scale
        /// and speeds, the run faster than the walk.
        /// </summary>
        [Test]
        public void EveryFormHasItsColourwayRowsInTheCatalogue()
        {
            ModuleCatalogue catalogue = Catalogue();
            var byKind = new Dictionary<int, int>();
            foreach (var form in Forms) byKind[form.Kind] = (byKind.TryGetValue(form.Kind, out int n) ? n : 0) + form.Coats;

            foreach (var pair in byKind)
            {
                List<ModuleEntry> family = catalogue.FindFamily(ModuleIds.Animal(pair.Key));
                Assert.That(family, Has.Count.EqualTo(pair.Value), $"kind {pair.Key} ({ModuleIds.AnimalNames[pair.Key]})");
                Assert.That(catalogue.Find(ModuleIds.Animal(pair.Key, 0)), Is.Not.Null, "variant 0 is the bare id");
            }

            foreach (var form in Forms)
            {
                int first = 0;
                foreach (var earlier in Forms)
                {
                    if (earlier.Kind != form.Kind) continue;
                    if (earlier.Form == form.Form) break;
                    first += earlier.Coats;
                }
                for (int coat = 0; coat < form.Coats; coat++)
                {
                    ModuleEntry? row = catalogue.Find(ModuleIds.Animal(form.Kind, first + coat));
                    string what = $"{form.Prefab} colourway {coat + 1}";
                    Assert.That(row, Is.Not.Null, what);
                    Assert.That(row!.meshName, Is.EqualTo($"SM_{form.Prefab}_0{coat + 1}"), what);
                    Assert.That(row.prefabName, Is.Not.Empty, $"{what} names its rig's model");
                    Assert.That(row.prefabUnder, Is.EqualTo(ForestFolder), $"{what} is asked for by the pack's folder");
                    Assert.That(row.animalForm, Is.EqualTo(form.Form), what);
                    Assert.That(row.eatClipName, Does.EndWith("_Eat"), what);
                    Assert.That(row.scale.x, Is.GreaterThan(0f), what);
                    Assert.That(row.locomotion, Has.Count.EqualTo(3), what);
                    Assert.That(row.locomotion[1].metresPerSecond, Is.GreaterThan(0f), $"{what} walks");
                    Assert.That(row.locomotion[2].metresPerSecond, Is.GreaterThan(row.locomotion[1].metresPerSecond),
                        $"{what} runs faster than it walks");
                }
            }
        }

        /// <summary>
        /// <b>Life size against a colonist</b> (owner, 2026-09-26; design 66 §5): each form's drawn
        /// shoulder — the top of the back over the fore legs, measured off the live figure's posed
        /// mesh — is within five per cent of its life shoulder times the colonists' draw factor.
        /// </summary>
        [Test]
        public void EachFormIsDrawnAtItsShoulder()
        {
            var parent = new GameObject("forest figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                int drawable = 0;
                foreach (var form in Forms) if (director.CanDrawKind(form.Kind)) drawable++;
                Assume.That(drawable, Is.GreaterThan(0), "SIMPLE Forest Animals is not installed here");

                var views = new List<PawnView>();
                var aspects = new List<PawnAspect>();
                for (int i = 0; i < Forms.Length; i++)
                {
                    if (!director.CanDrawKind(Forms[i].Kind)) continue;
                    int id = 100 + i;
                    views.Add(Standing(id, Forms[i].Kind, 2 + 4 * i, 4));
                    aspects.Add(new PawnAspect(new PawnId(id), AnimalAspects.Form, Forms[i].Form));
                }
                director.Sync(Frame(views, aspects), 0, new SliceSettings(), 0f, 1, 0.016f);

                for (int i = 0; i < Forms.Length; i++)
                {
                    if (!director.CanDrawKind(Forms[i].Kind)) continue;
                    GameObject? figure = FigureWearing(parent.transform, Forms[i].Prefab + "_");
                    Assert.That(figure, Is.Not.Null, $"a figure wearing {Forms[i].Prefab}");
                    AnimalMeasure.Size size = AnimalMeasure.Measure(figure!);
                    float target = Forms[i].Life * DrawFactor;
                    Assert.That(size.Shoulder, Is.EqualTo(target).Within(target * 0.05f),
                        $"{Forms[i].Prefab}: drawn shoulder {size.Shoulder:0.000} m against {target:0.000} m " +
                        $"(sole {size.Sole:0.000}, crown {size.Crown:0.000}, length {size.Length:0.000})");
                }
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// <b>The form the simulation publishes is the form drawn</b> (design 66 §4): a deer at
        /// form 1 is a stag and at form 0 a doe, a moose a bull and a cow, and a one-form species
        /// with no aspect at all is drawn as itself.
        /// </summary>
        [Test]
        public void TheStagIsAStagAndTheDoeADoe()
        {
            var parent = new GameObject("forest forms");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                Assume.That(director.CanDrawKind(PawnKindIndex.HedgerowDeer), Is.True, "SIMPLE Forest Animals is not installed here");

                var views = new List<PawnView>
                {
                    Standing(1, PawnKindIndex.HedgerowDeer, 2, 2), Standing(2, PawnKindIndex.HedgerowDeer, 6, 2),
                    Standing(3, PawnKindIndex.MireMoose, 10, 2), Standing(4, PawnKindIndex.MireMoose, 14, 2),
                    Standing(5, PawnKindIndex.AshFox, 18, 2),
                };
                var aspects = new List<PawnAspect>
                {
                    new PawnAspect(new PawnId(1), AnimalAspects.Form, 0), new PawnAspect(new PawnId(2), AnimalAspects.Form, 1),
                    new PawnAspect(new PawnId(3), AnimalAspects.Form, 0), new PawnAspect(new PawnId(4), AnimalAspects.Form, 1),
                };
                director.Sync(Frame(views, aspects), 0, new SliceSettings(), 0f, 1, 0.016f);

                Assert.That(FigureWearing(parent.transform, "Doe_"), Is.Not.Null, "form 0 deer is a doe");
                Assert.That(FigureWearing(parent.transform, "Stag_"), Is.Not.Null, "form 1 deer is a stag");
                Assert.That(FigureWearing(parent.transform, "Moose_Female_"), Is.Not.Null, "form 0 moose is a cow");
                Assert.That(FigureWearing(parent.transform, "Moose_Male_"), Is.Not.Null, "form 1 moose is a bull");
                Assert.That(FigureWearing(parent.transform, "Fox_"), Is.Not.Null, "a fox with no form aspect is a fox");

                // The pack's prefab carries every mesh of its rig; the figure keeps only its own.
                GameObject fox = FigureWearing(parent.transform, "Fox_")!;
                int skins = fox.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true).Length;
                Assert.That(skins, Is.EqualTo(1), "the fox figure draws the fox alone, not the raccoon, skunk and wolf beside it");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>
        /// <b>An eating animal lowers its head</b> (design 66 §6): the Eat loop is an input on the
        /// figure's mixer, and at full weight a doe's head is lower than standing.
        /// </summary>
        [Test]
        public void ADoeLowersItsHeadToEat()
        {
            var parent = new GameObject("forest eat");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(Catalogue(), parent.transform, 0);
                Assume.That(director.CanDrawKind(PawnKindIndex.HedgerowDeer), Is.True, "SIMPLE Forest Animals is not installed here");

                var views = new List<PawnView> { Standing(1, PawnKindIndex.HedgerowDeer, 4, 4) };
                var aspects = new List<PawnAspect> { new PawnAspect(new PawnId(1), AnimalAspects.Form, 0) };

                // Nothing runs the graphs in an edit-mode test: the harness evaluates them, as
                // AnimalProbe.ShootMoving does, after each Sync.
                director.ForceEat = 0f;
                director.Sync(Frame(views, aspects), 0, new SliceSettings(), 0f, 1, 0.5f);
                director.Evaluate(0.5f);
                GameObject doe = FigureWearing(parent.transform, "Doe_")!;
                Transform head = AnimalMeasure.Find(doe.transform, "Head")!;
                Assume.That(head, Is.Not.Null, "the deer rig has a head joint");
                float standing = head.position.y;

                // Through a whole cycle of the loop (the deer's is 3.75 s), since a grazing animal
                // lifts its head between mouthfuls: the claim is that it goes down, not that it
                // stays down at any one instant.
                director.ForceEat = 1f;
                float lowest = float.MaxValue, highest = float.MinValue;
                for (int i = 0; i < 20; i++)
                {
                    director.Sync(Frame(views, aspects), 0, new SliceSettings(), 0f, 1, 0.25f);
                    director.Evaluate(0.25f);
                    lowest = Mathf.Min(lowest, head.position.y);
                    highest = Mathf.Max(highest, head.position.y);
                }

                Assert.That(lowest, Is.LessThan(standing - 0.2f),
                    $"the head stood at {standing:0.000} m and, eating, ranged {lowest:0.000}..{highest:0.000} m");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>The live animal figure whose one kept mesh is one of this form's colourways (<c>SM_Doe_…</c>).</summary>
        static GameObject? FigureWearing(Transform parent, string prefabPrefix)
        {
            foreach (Transform child in parent)
            {
                if (!child.gameObject.activeSelf || !child.name.StartsWith("Animal figure")) continue;
                foreach (SkinnedMeshRenderer skin in child.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
                    if (skin.gameObject.name.StartsWith("SM_" + prefabPrefix)) return child.gameObject;
            }
            return null;
        }

        static WorldSnapshot Frame(List<PawnView> pawns, List<PawnAspect> aspects)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(0, new GridSize(80, 20, 4), 0);
            foreach (PawnView pawn in pawns) snapshot.AddPawn(pawn);
            foreach (PawnAspect aspect in aspects) snapshot.AddPawnAspect(aspect);
            return snapshot;
        }

        static PawnView Standing(int id, int kind, int x, int z) =>
            new PawnView(new PawnId(id), new CellRef(x, z, 0), 800, 800, 600, JobHandle.Wait, kind: kind);
    }
}
