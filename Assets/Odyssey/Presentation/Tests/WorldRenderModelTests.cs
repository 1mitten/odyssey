#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What module the render mirror picks for what stands in a cell.
    ///
    /// The natural generator numbers its edifices after the city's, and the mirror used to
    /// switch on the city's codes alone with the wall as its default. Nothing failed: every tree
    /// on the wooded board simply drew as a grey wall box, which is exactly the kind of fault
    /// that only a picture or a test like this one catches.
    /// </summary>
    public class WorldRenderModelTests
    {
        [Test]
        public void ATreeResolvesToItsOwnModuleAndNotToTheWall()
        {
            var world = new RenderTestWorld(6, 6, 3)
                .Solid(2, 2, 0).Edifice(2, 2, 1, NaturalContent.EdificeTreeBirch, blocking: false)
                .Solid(3, 2, 0).Edifice(3, 2, 1, NaturalContent.EdificeTreeMeadow, blocking: false)
                .Solid(4, 2, 0).Edifice(4, 2, 1, CoreContent.EdificeWall)
                .Publish();

            int conifer = world.Model.EdificeModule(world.Index(2, 2, 1));
            int broadleaf = world.Model.EdificeModule(world.Index(3, 2, 1));
            int wall = world.Model.EdificeModule(world.Index(4, 2, 1));

            Assert.That(conifer, Is.EqualTo(world.Library.Resolve(NaturalContent.ModuleTreeConifer, ModuleShape.Pillar)));
            Assert.That(broadleaf, Is.EqualTo(world.Library.Resolve(NaturalContent.ModuleTreeBroadleaf, ModuleShape.Pillar)));
            Assert.That(conifer, Is.Not.EqualTo(wall), "a conifer is not a wall");
            Assert.That(broadleaf, Is.Not.EqualTo(wall), "a broadleaf is not a wall");
            Assert.That(conifer, Is.Not.EqualTo(broadleaf), "the two kinds of tree are told apart");
        }

        [Test]
        public void ATreeIsABodyStandingInTheCellNotAPanelOnItsFaces()
        {
            // The mesher emits a wall as up to four face panels and everything else as one body.
            // A tree drawn as panels would be the grey-box fault by another route.
            var world = new RenderTestWorld(6, 6, 3)
                .Solid(2, 2, 0).Edifice(2, 2, 1, NaturalContent.EdificeTreeBirch, blocking: false)
                .Publish();

            int module = world.Model.EdificeModule(world.Index(2, 2, 1));
            Assert.That(module, Is.Not.Zero, "a tree has a module");
            Assert.That(world.Library[module].Shape, Is.EqualTo(ModuleShape.Pillar));
        }

        /// <summary>
        /// The floor of the open landscape is the lowest column top on the board, not the lowest
        /// cell in it. A terrace two steps down is still ground somebody can stand on and has to
        /// be drawn; the rock under it is not.
        /// </summary>
        [Test]
        public void TheLandscapeFloorIsTheLowestColumnTop()
        {
            // Three terraces, two layers apart at the extremes, over a common base.
            var world = new RenderTestWorld(4, 1, 6)
                .Solid(0, 0, 0).Solid(0, 0, 1)
                .Solid(1, 0, 0).Solid(1, 0, 1).Solid(1, 0, 2)
                .Solid(2, 0, 0).Solid(2, 0, 1).Solid(2, 0, 2).Solid(2, 0, 3)
                .Solid(3, 0, 0).Solid(3, 0, 1).Solid(3, 0, 2)
                .Publish();

            Assert.That(world.Model.LowestOutdoorLayer, Is.EqualTo(1),
                "the lowest terrace top, not the bedrock under all four columns");
            Assert.That(world.Model.HighestOccupiedLayer, Is.EqualTo(4),
                "and the high-water mark is still the top of the geometry plus a standing colonist");
        }

        /// <summary>
        /// A column with a roof over it is not open landscape, so its ground has no claim on the
        /// drawn band. The slab is what the column's top is, and the mark reads it.
        /// </summary>
        [Test]
        public void ARoofedColumnDoesNotPullTheLandscapeFloorDown()
        {
            var world = new RenderTestWorld(2, 1, 6)
                .Solid(0, 0, 0).Solid(0, 0, 1).Solid(0, 0, 2)      // open ground at L2
                .Solid(1, 0, 0).Slab(1, 0, 3)                      // ground at L0, roofed at L3
                .Publish();

            Assert.That(world.Model.LowestOutdoorLayer, Is.EqualTo(2),
                "the buried floor of a roofed column counted as landscape");
        }

        /// <summary>
        /// A column of pure air contributes nothing rather than contributing zero, or one empty
        /// corner of a map would drag the drawn band down to the bedrock everywhere.
        /// </summary>
        [Test]
        public void AnEmptyColumnDoesNotCount()
        {
            var world = new RenderTestWorld(2, 1, 6)
                .Solid(0, 0, 0).Solid(0, 0, 1).Solid(0, 0, 2)
                .Publish();

            Assert.That(world.Model.LowestOutdoorLayer, Is.EqualTo(2));
        }

        /// <summary>
        /// Two floors of different materials are not drawn with the same mesh.
        ///
        /// <para>They were. Every slab id in the catalogue — the template default, the concrete
        /// one and the deck — resolved to the one wooden deck prefab, and a material showed only
        /// as a tint multiplied over it. Stone's tint is a near-white grey, so a stone floor was
        /// the wood deck 12% darker and the owner read it as wood (2026-09-17). A multiply cannot
        /// separate them: it only darkens, and brown times grey is browner.</para>
        ///
        /// <para>The catalogue rather than the render model, because that is where the fault was.
        /// The model's part of it is <see cref="WithoutTheLicensedPacksAFloorDrawsWhatItAlwaysDrew"/>:
        /// the mesh is only swapped when there is art to swap to.</para>
        /// </summary>
        [Test]
        public void StoneAndWoodFloorsAreNotTheSameMesh()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            Assert.That(catalogue, Is.Not.Null, "the module catalogue is committed and should load");

            List<ModuleEntry> slabs = catalogue!.FindFamily(ModuleIds.Slab);
            string PrefabFor(string id)
            {
                ModuleEntry? row = slabs.FirstOrDefault(e => e.moduleId == id);
                Assert.That(row, Is.Not.Null, $"{id} has no row; rebuild the catalogue");
                return row!.prefabName;
            }

            Assert.That(PrefabFor(ModuleIds.SlabOf("stone")),
                Is.Not.EqualTo(PrefabFor(ModuleIds.SlabOf("wood"))),
                "a stone floor and a wooden one drew the same planks");
        }

        /// <summary>
        /// <b>Every floor slab puts its walking surface on the cell's floor plane.</b>
        ///
        /// <para>A slab is drawn at the cell's lower boundary (<c>ChunkMesher.EmitFloor</c> →
        /// <c>CellMetrics.FloorCentre</c>), so in module-local terms the plane a colonist stands on
        /// is y = 0 and every slab's <em>top</em> face belongs there. Its thickness hangs below,
        /// where nobody walks.</para>
        ///
        /// <para><b>This is the sibling of the test above and was written the same way — from a
        /// screenshot.</b> That one pinned that two materials are two meshes; this one pins that
        /// the two meshes land at one height. Taking the first without the second is exactly what
        /// happened: the slab rows asked for neither <c>baseAtY</c> nor <c>topAtY</c>, so each
        /// prefab landed on whatever pivot convention its artist used, and the street tile's top
        /// came out 25 mm above the plank deck's. The owner reported a grey tile sitting at the
        /// wrong height in an otherwise wooden deck (2026-09-18, `docs/design/15-building.md`).</para>
        ///
        /// <para>Asked of the resolved module rather than of the row, because the row says
        /// <c>topAtY</c> and the question is whether that produced a level floor —
        /// <c>ResolvedModule.Bounds</c> is measured after placement, so this walks the real
        /// arithmetic. It also covers the street surfaces, which are walked on for the same reason
        /// and were 33 mm out for the same one.</para>
        ///
        /// <para><b>Two assertions and not one, because the first fix broke the second.</b>
        /// Levelling the slabs on to the plane <em>exactly</em> made them coplanar with the terrain
        /// block below — <c>FloorCentre</c> for cell y is that block's top face — and the owner's
        /// board z-fought within the hour. "All the slabs agree" is necessary and is not enough:
        /// they must agree at a height that clears the ground. A version of this test that checked
        /// only the first would have passed the flicker through.</para>
        /// </summary>
        [Test]
        public void EveryFloorSlabPutsItsWalkingSurfaceOnTheCellFloor()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            Assert.That(catalogue, Is.Not.Null, "the module catalogue is committed and should load");

            var library = new ModuleLibrary(catalogue!);
            int checked_ = 0;

            foreach (ModuleEntry row in catalogue!.Entries)
            {
                if (row.shape != ModuleShape.FloorSlab) continue;

                ResolvedModule resolved = library[library.Resolve(row.moduleId, ModuleShape.FloorSlab)];

                // A clone without the licensed packs draws a primitive, whose height is the
                // fallback's business and not this rule's.
                if (!resolved.UsesArt || resolved.IsEmpty) continue;

                Assert.That(resolved.Bounds.max.y, Is.EqualTo(CellMetrics.SlabLift).Within(0.001f),
                    $"{row.moduleId} ({row.prefabName}) draws its top face {resolved.Bounds.max.y:F3} m " +
                    "above the cell floor, so it will not sit level with the slab in the next cell");
                checked_++;
            }

            // And the clearance is not zero. CellMetrics.SlabLift carries the argument; this is
            // here so that "level" can never again be achieved by levelling everything on to the
            // one plane the ground below already occupies.
            Assert.That(CellMetrics.SlabLift, Is.GreaterThan(0f),
                "a slab flush with the cell floor plane is coplanar with the terrain block beneath " +
                "it, and paving is laid on ground by definition");

            // Or the loop above passes by never running, which is how a rule quietly stops being
            // one: five buildable slabs and five street surfaces are in the committed catalogue.
            // **Only where there is art to check.** The guard above is right that a loop which
            // never runs is a rule that has quietly stopped being one — but asking for ten checked
            // rows unconditionally asks for the licensed packs, and the project's standing rule is
            // that a clone without them still builds and runs headless. It does not: this test was
            // the one red on the self-hosted runner from the moment it was written, because the
            // runner's checkout has no `Assets/Synty` and so every row is a primitive.
            //
            // The two cases are told apart by the library rather than by the environment: with no
            // packs, no *slab* in the catalogue has art, so there is nothing here to be wrong. With
            // packs, ten rows must be checkable, and fewer means the rule has lost its reach.
            //
            // Slabs, not the whole catalogue (2026-09-23). This asked "does any row at all have
            // art" and was right until the animals unit committed two rows of the project's own
            // art (design 29 §8a), which resolve on the runner exactly because they are not the
            // licensed packs. From then on the guard was true there, no slab had art, and the
            // rule asked for ten and got none — the third time a "is the art here" question has
            // turned the runner red by asking about the wrong thing (CLAUDE.md, the tiers).
            bool anySlabArt = false;
            foreach (ModuleEntry row in catalogue.Entries)
            {
                if (row.shape != ModuleShape.FloorSlab) continue;
                if (library[library.Resolve(row.moduleId, row.shape)].UsesArt) { anySlabArt = true; break; }
            }

            if (anySlabArt)
                Assert.That(checked_, Is.GreaterThanOrEqualTo(10),
                    "the catalogue should hold at least the five slab ids and the five street tiles");
            else
                Assert.Pass("no licensed pack is present, so every module is a primitive and this " +
                            "rule has nothing to measure. The clearance assertion above still ran.");
        }

        /// <summary>
        /// A clone without the licensed packs draws exactly what it drew before.
        ///
        /// <para>An unknown module id does not resolve to nothing — it resolves to a built-in
        /// primitive — so a per-material lookup that trusted the resolver would swap the group's
        /// slab for a bare block on every machine that has no art. The material's mesh is taken
        /// only when it is real art; otherwise the template's slab stands, for both.</para>
        /// </summary>
        [Test]
        public void WithoutTheLicensedPacksAFloorDrawsWhatItAlwaysDrew()
        {
            var world = new RenderTestWorld(6, 6, 3)
                .Solid(2, 2, 0).Slab(2, 2, 1, NaturalContent.StuffWood)
                .Solid(3, 2, 0).Slab(3, 2, 1, NaturalContent.StuffStone)
                .Publish();

            int slab = world.Library.Resolve(ModuleIds.Slab, ModuleShape.FloorSlab);
            Assert.That(world.Model.FloorModule(world.Index(2, 2, 1)), Is.EqualTo(slab));
            Assert.That(world.Model.FloorModule(world.Index(3, 2, 1)), Is.EqualTo(slab));
        }
    }
}
