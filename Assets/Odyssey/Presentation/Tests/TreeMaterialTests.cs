#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The tree material cache: one material per <em>species</em> per shade, and never one per
    /// colour.
    ///
    /// <para>It used to be one per colour, and that is what made a coloured wood cost draw calls: a
    /// material is a bucket, so a colour on the material was a colour per bucket, and a mixed wood
    /// cost 384 draw calls on the played board. The four colours are per-instance data now. What is
    /// left on the material is the pair of things one genuinely has to carry — which atlas cells to
    /// repaint, and the shading the slice and the surround apply to a whole draw.</para>
    /// </summary>
    public class TreeMaterialTests
    {
        /// <summary>
        /// A stand-in for a pack tree material: the cache needs an albedo to carry across, and a
        /// clone without <c>Assets/Synty</c> has none. A 1 x 1 texture is enough — nothing here
        /// samples it, and building the test on real pack art would make it depend on the licensed
        /// content, which the working agreement forbids.
        /// </summary>
        static Material Source(out Texture2D atlas)
        {
            atlas = new Texture2D(1, 1);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", atlas);
            return material;
        }

        /// <summary>
        /// The claim the whole performance argument rests on: the palette can be any size at all
        /// and the material count does not move.
        /// </summary>
        [Test]
        public void MaterialsDoNotGrowWithThePalette()
        {
            var materials = new TreeMaterials();
            Material source = Source(out Texture2D atlas);
            try
            {
                if (!materials.Available) Assert.Ignore("Odyssey/Tree is not in this build");

                // Every theme in the table, asked for ten thousand times over. If this ever counts
                // more than the two species, a colour has found its way back onto the material and
                // the wood is back on the draw-call bill.
                for (int i = 0; i < 10_000; i++)
                {
                    TreeSpecies species = TreePalette.At(i % TreePalette.Count).Species;
                    materials.For(source, species, 1f);
                }

                TestContext.WriteLine(
                    $"{TreePalette.Count} themes drew {materials.MaterialCount} materials");
                Assert.That(materials.MaterialCount, Is.EqualTo(2));
            }
            finally
            {
                materials.Dispose();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void TheSameSpeciesAtTheSameShadeIsTheSameMaterial()
        {
            var materials = new TreeMaterials();
            Material source = Source(out Texture2D atlas);
            try
            {
                if (!materials.Available) Assert.Ignore("Odyssey/Tree is not in this build");

                Material? first = materials.For(source, TreeSpecies.Broadleaf, 1f);
                Assert.That(first, Is.Not.Null);
                Assert.That(materials.For(source, TreeSpecies.Broadleaf, 1f), Is.SameAs(first));

                // The two trees repaint different cells of the atlas, so they are two materials
                // however identical everything else about them is.
                Assert.That(materials.For(source, TreeSpecies.Conifer, 1f), Is.Not.SameAs(first));

                // A different depth shade is a different material, because the shade multiplies the
                // repainted albedo and there is nowhere else for it to live. The quantiser is what
                // stops a float differing in its last bit from minting a second one.
                Assert.That(materials.For(source, TreeSpecies.Broadleaf, 0.5f), Is.Not.SameAs(first));
                Assert.That(materials.For(source, TreeSpecies.Broadleaf, 1f - 1e-4f), Is.SameAs(first));

                // And a different surround haze likewise: it desaturates all four colours at once.
                Assert.That(materials.For(source, TreeSpecies.Broadleaf, 1f, muteStep: 2),
                    Is.Not.SameAs(first));
            }
            finally
            {
                materials.Dispose();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(atlas);
            }
        }

        /// <summary>
        /// Null, not a clone that happens to change nothing. The caller then draws exactly what it
        /// drew before this feature existed — which is what keeps a clone with no packs, or a
        /// machine where the shader failed to compile, showing a wood rather than a magenta one.
        /// </summary>
        [Test]
        public void ArtWithNoAtlasKeepsItsOwnMaterial()
        {
            var materials = new TreeMaterials();
            var bare = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                Assert.That(materials.For(null, TreeSpecies.Broadleaf, 1f), Is.Null);
                Assert.That(materials.For(bare, TreeSpecies.Broadleaf, 1f), Is.Null, "no albedo to repaint");
                Assert.That(materials.MaterialCount, Is.Zero);
            }
            finally
            {
                materials.Dispose();
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void TheRecolourCanBeSwitchedOffWholesale()
        {
            // The diagnostic lever TreeCheck photographs the "before" with. Off must mean the pack
            // material, not a clone of it, or the control column of the contact sheet is comparing
            // the feature against itself.
            var materials = new TreeMaterials();
            Material source = Source(out Texture2D atlas);
            try
            {
                TreeMaterials.Enabled = false;
                Assert.That(materials.For(source, TreeSpecies.Broadleaf, 1f), Is.Null);
                Assert.That(materials.MaterialCount, Is.Zero);
            }
            finally
            {
                TreeMaterials.Enabled = true;
                materials.Dispose();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(atlas);
            }
        }
    }
}
