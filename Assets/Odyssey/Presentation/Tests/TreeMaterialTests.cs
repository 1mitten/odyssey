#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The tree material cache: one material per colour a wood actually wears, and never one per
    /// tree. It is the same claim <c>ColonistCatalogueTests</c> makes about faces, and it fails the
    /// same way — silently, as a leak that grows with the board rather than with the palette.
    /// </summary>
    public class TreeMaterialTests
    {
        /// <summary>
        /// A stand-in for a pack tree material: the cache needs an albedo to carry across, and a
        /// clone without <c>Assets/Synty</c> has none. A 1 x 1 texture is enough — nothing here
        /// samples it, and building the test on real pack art would make it depend on the
        /// licensed content, which the working agreement forbids.
        /// </summary>
        static Material Source(out Texture2D atlas)
        {
            atlas = new Texture2D(1, 1);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", atlas);
            return material;
        }

        [Test]
        public void MaterialsGrowWithThemesAndNotWithTrees()
        {
            var materials = new TreeMaterials();
            Material source = Source(out Texture2D atlas);
            try
            {
                if (!materials.Available) Assert.Ignore("Odyssey/Tree is not in this build");

                // Ten thousand trees over the whole palette. If this ever counts ten thousand,
                // something has begun keying on the cell and the cache has become a leak.
                for (int i = 0; i < 10_000; i++)
                    materials.For(source, i % TreePalette.Count, 1f);

                Assert.That(materials.MaterialCount, Is.EqualTo(TreePalette.Count));
            }
            finally
            {
                materials.Dispose();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void TheSameThemeAtTheSameShadeIsTheSameMaterial()
        {
            var materials = new TreeMaterials();
            Material source = Source(out Texture2D atlas);
            try
            {
                if (!materials.Available) Assert.Ignore("Odyssey/Tree is not in this build");

                Material? first = materials.For(source, 0, 1f);
                Assert.That(first, Is.Not.Null);
                Assert.That(materials.For(source, 0, 1f), Is.SameAs(first));

                // A different depth shade is a different material, because the shade multiplies
                // the repainted albedo and there is nowhere else for it to live. The quantiser is
                // what stops a float differing in its last bit from minting a second one.
                Assert.That(materials.For(source, 0, 0.5f), Is.Not.SameAs(first));
                Assert.That(materials.For(source, 0, 1f - 1e-4f), Is.SameAs(first));

                // And a different surround haze likewise: it desaturates all four colours at once.
                Assert.That(materials.For(source, 0, 1f, muteStep: 2), Is.Not.SameAs(first));
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
                Assert.That(materials.For(null, 0, 1f), Is.Null);
                Assert.That(materials.For(bare, 0, 1f), Is.Null, "no albedo to repaint");
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
                Assert.That(materials.For(source, 0, 1f), Is.Null);
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
