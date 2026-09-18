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
                .Solid(2, 2, 0).Edifice(2, 2, 1, NaturalContent.EdificeTreeConifer, blocking: false)
                .Solid(3, 2, 0).Edifice(3, 2, 1, NaturalContent.EdificeTreeBroadleaf, blocking: false)
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
                .Solid(2, 2, 0).Edifice(2, 2, 1, NaturalContent.EdificeTreeConifer, blocking: false)
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

                Assert.That(resolved.Bounds.max.y, Is.EqualTo(0f).Within(0.001f),
                    $"{row.moduleId} ({row.prefabName}) draws its top face {resolved.Bounds.max.y:F3} m " +
                    "off the cell floor, so it will not sit level with the slab in the next cell");
                checked_++;
            }

            // Or the loop above passes by never running, which is how a rule quietly stops being
            // one: five buildable slabs and five street surfaces are in the committed catalogue.
            Assert.That(checked_, Is.GreaterThanOrEqualTo(10),
                "the catalogue should hold at least the five slab ids and the five street tiles");
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
