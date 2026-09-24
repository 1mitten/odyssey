#nullable enable
using NUnit.Framework;
using UnityEngine;
using Odyssey.Presentation.Rendering;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The campfire draws as a campfire, on the board and under the pointer alike.
    ///
    /// <para><b>Written because it shipped as a wooden block</b> (owner, 2026-09-23: *"the campfire
    /// still is a wooden block both on placement and build"*). <c>ModuleIds.Campfire</c> had no
    /// catalogue row, so <c>ModuleLibrary.Resolve</c> fell back to the <c>SolidBlock</c> primitive
    /// and tinted it with the stuff it was built from. Nothing was broken and no test failed:
    /// a fallback is a designed path, which is exactly why it needed one of these
    /// (docs/design/31-campfire-art-and-fire.md §3).</para>
    ///
    /// <para><b>The question is whether the art <i>resolved</i>, never whether a catalogue
    /// exists.</b> The catalogue is committed and its prefab references point into the gitignored
    /// <c>Assets/Synty</c>, so on a machine without the packs it loads perfectly with every
    /// reference null — which is the one machine that can draw nothing. A
    /// <c>catalogue == null</c> check is the wrong question and has turned the runner red twice
    /// (CLAUDE.md). So: skip where the art is absent, assert where it is present.</para>
    /// </summary>
    public class CampfireArtTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static ResolvedModule? Campfire(out ModuleLibrary? library)
        {
            library = null;
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null) return null;

            library = new ModuleLibrary(catalogue);
            return library[library.Resolve(ModuleIds.Campfire, ModuleShape.SolidBlock)];
        }

        /// <summary>
        /// It is the pack's prop and not the fallback block.
        ///
        /// <para>Both surfaces the owner named come from this one answer:
        /// <c>OdysseyBootstrap.GhostModuleFor</c> and <c>ChunkMesher</c> each ask
        /// <c>WorldRenderModel.ModuleForEdificeAt</c> and place what comes back with the same
        /// <c>Drape(FloorCentre)</c>. Fixing the row fixed the ghost and the built thing at once,
        /// and this test covers both by covering the thing they share.</para>
        /// </summary>
        [Test]
        public void TheCampfireResolvesToArtRatherThanTheFallbackBlock()
        {
            ResolvedModule? fire = Campfire(out ModuleLibrary? library);
            using (library)
            {
                if (fire == null) Assert.Ignore("no module catalogue in this checkout");
                if (!fire!.UsesArt)
                    Assert.Ignore("the Synty packs are not present, so the campfire has no art to " +
                                  "resolve — correct on the runner, and the reason this is a skip");

                Assert.That(fire.IsEmpty, Is.False, "the campfire resolved to nothing at all");
                Assert.That(fire.Parts.Length, Is.GreaterThan(0));
                for (int i = 0; i < fire.Parts.Length; i++)
                    Assert.That(fire.Parts[i].IsFallback, Is.False,
                        $"campfire part {i} is a stand-in primitive, which is the block the owner " +
                        "reported. The catalogue row in PlayScene.cs is what puts real art here.");
            }
        }

        /// <summary>
        /// It fits its cell.
        ///
        /// <para><b>Measured, because the screenshot lied.</b> The first picture of it made the
        /// ring look about a metre across and prompted a hunt for a scale that was not applied —
        /// a reference cube stood between the camera and the prop and foreshortened it. The
        /// numbers said 2.30 × 2.25 m all along. <c>SM_Prop_Campfire_01</c> is 3.28 m in the
        /// pack and the row scales it by 0.70 for exactly this reason; a campfire wider than its
        /// cell would push through whatever is built beside it.</para>
        ///
        /// <para>It also asserts the piece is <b>centred and standing on the floor</b>, which is
        /// what <c>centreXZ</c> and <c>baseAtY</c> are for: a pack's pivot convention is nobody's
        /// business but the row's, and a campfire floating or sunk is the commonest way that
        /// goes wrong.</para>
        /// </summary>
        [Test]
        public void TheCampfireSitsInsideItsOwnCellAndOnTheFloor()
        {
            ResolvedModule? fire = Campfire(out ModuleLibrary? library);
            using (library)
            {
                if (fire == null || !fire.UsesArt)
                    Assert.Ignore("no campfire art in this checkout");

                Bounds box = Footprint(fire!);

                Assert.That(box.size.x, Is.LessThanOrEqualTo(CellMetrics.SizeXZ + 0.001f),
                    $"the campfire is {box.size.x:F2} m across and a cell is {CellMetrics.SizeXZ:F2} m");
                Assert.That(box.size.z, Is.LessThanOrEqualTo(CellMetrics.SizeXZ + 0.001f),
                    $"the campfire is {box.size.z:F2} m deep and a cell is {CellMetrics.SizeXZ:F2} m");
                Assert.That(box.size.y, Is.LessThanOrEqualTo(CellMetrics.SizeY + 0.001f),
                    $"the campfire is {box.size.y:F2} m tall and a layer is {CellMetrics.SizeY:F2} m");

                Assert.That(box.min.y, Is.EqualTo(0f).Within(0.02f),
                    $"the campfire's base is at {box.min.y:F3} m rather than on the cell floor");
                Assert.That(box.center.x, Is.EqualTo(0f).Within(0.05f), "not centred in x");
                Assert.That(box.center.z, Is.EqualTo(0f).Within(0.05f), "not centred in z");
            }
        }

        /// <summary>The box the resolved parts occupy, placed at the cell origin.</summary>
        static Bounds Footprint(ResolvedModule module)
        {
            var box = new Bounds();
            bool has = false;
            for (int i = 0; i < module.Parts.Length; i++)
            {
                ModulePart part = module.Parts[i];
                Bounds b = part.Mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        (c & 1) == 0 ? b.min.x : b.max.x,
                        (c & 2) == 0 ? b.min.y : b.max.y,
                        (c & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 world = part.Local.MultiplyPoint3x4(corner);
                    if (!has) { box = new Bounds(world, Vector3.zero); has = true; }
                    else box.Encapsulate(world);
                }
            }
            return box;
        }
    }
}
