#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The swatch rectangles committed to <c>ModuleCatalogue.asset</c>.
    ///
    /// <para>These read the asset and nothing else, so they are the first tests in the project
    /// that can catch colonist-catalogue drift <b>on a machine with no Synty content at all</b> —
    /// the rectangles are numbers, and numbers are committed. Everything else about a colonist row
    /// is a reference into a gitignored folder that no test can look at, which is how the
    /// catalogue and its builder drifted apart once already.</para>
    /// </summary>
    public class AppearanceCatalogueTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>
        /// The measured floor, with a little room.
        ///
        /// Classification found 53 of the 61 bodies Full; 5 with no skin showing (two aliens, a
        /// helmeted cop, a masked cyborg ninja and a gowned medic — all correct), one android with
        /// neither skin nor hair, and two whose slots overlapped and dropped the smaller. Forty
        /// eight is that number less a margin: a reimported pack, a repainted atlas or a new body
        /// that pushes it lower is something to look at, not to discover in a screenshot.
        /// </summary>
        const int FullyClassifiedFloor = 48;

        static ModuleCatalogue Load()
        {
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, "no catalogue at " + CataloguePath);
            return catalogue!;
        }

        [Test]
        public void EveryColonistRowCarriesUsableAppearanceData()
        {
            foreach (ModuleEntry row in Load().FindFamily(ModuleIds.ColonistBase))
            {
                AppearanceCells cells = row.appearance;
                Assert.That(cells, Is.Not.Null, row.prefabName);
                Assert.That(Enum.IsDefined(typeof(AppearanceQuality), cells.quality), Is.True,
                    row.prefabName + " has an unrecognised quality");

                foreach (Rect r in All(cells))
                {
                    Assert.That(r.width, Is.GreaterThan(0f), row.prefabName + ": empty rect");
                    Assert.That(r.height, Is.GreaterThan(0f), row.prefabName + ": empty rect");
                    Assert.That(r.xMin, Is.GreaterThanOrEqualTo(-0.01f), row.prefabName);
                    Assert.That(r.yMin, Is.GreaterThanOrEqualTo(-0.01f), row.prefabName);
                    Assert.That(r.xMax, Is.LessThanOrEqualTo(1.01f), row.prefabName);
                    Assert.That(r.yMax, Is.LessThanOrEqualTo(1.01f), row.prefabName);
                }
            }
        }

        [Test]
        public void NoSlotsOverlapOnAnyBody()
        {
            // The shader lays the four slots over one another in a fixed order and does not test
            // for overlap, so a fragment inside two of them would take whichever came last. That
            // is only safe because they are disjoint, which is asserted here rather than hoped
            // for. A body the classifier could not separate is marked Shared and drops the
            // smaller slot, so even those arrive disjoint.
            // Across slots, not within one. Two skin rectangles that touch paint the same colour
            // twice and are the same picture; two rectangles belonging to *different* slots are
            // the ambiguity the shader cannot resolve. The classifier merges the first case and
            // drops a slot in the second.
            foreach (ModuleEntry row in Load().FindFamily(ModuleIds.ColonistBase))
            {
                AppearanceCells cells = row.appearance;
                var slots = new[] { cells.skin, cells.hair, cells.cloth, cells.cloth2 };
                for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                    foreach (Rect a in slots[i])
                    foreach (Rect b in slots[j])
                        Assert.That(a.Overlaps(b), Is.False,
                            row.prefabName + ": " + a + " overlaps " + b);
            }
        }

        [Test]
        public void EveryBodyHasSomethingToRecolourAndMostAreFullyClassified()
        {
            List<ModuleEntry> rows = Load().FindFamily(ModuleIds.ColonistBase);
            int full = 0, classified = 0;
            foreach (ModuleEntry row in rows)
            {
                if (row.appearance.quality == AppearanceQuality.Full) full++;
                if (row.appearance.Any) classified++;
            }

            // The stronger claim, and the one that matters: nobody is left out of the feature.
            // Three bodies failed this before the classifier learned to take the largest active
            // mesh rather than the first, which on a PolygonGeneric body is sometimes the hair.
            Assert.That(classified, Is.EqualTo(rows.Count), "some body has nothing to recolour");
            Assert.That(full, Is.GreaterThanOrEqualTo(FullyClassifiedFloor),
                "only " + full + " bodies classified fully");
        }

        static List<Rect> All(AppearanceCells cells)
        {
            var rects = new List<Rect>();
            rects.AddRange(cells.skin);
            rects.AddRange(cells.hair);
            rects.AddRange(cells.cloth);
            rects.AddRange(cells.cloth2);
            return rects;
        }
    }

    /// <summary>One material per look, and never one per colonist.</summary>
    public class ColonistMaterialsTests
    {
        static AppearanceCells Cells() => new AppearanceCells
        {
            skin = new[] { Rect.MinMaxRect(0.01f, 0.18f, 0.02f, 0.19f) },
            cloth = new[] { Rect.MinMaxRect(0.10f, 0.20f, 0.11f, 0.21f) },
            quality = AppearanceQuality.NoHair,
            totalVerts = 100,
        };

        static ColonistAppearance Look(uint skin, uint hair, uint cloth) =>
            new ColonistAppearance(0, Rgb24.FromHex(skin), Rgb24.FromHex(hair),
                                   Rgb24.FromHex(cloth), Rgb24.FromHex(cloth).Scaled(65));

        [Test]
        public void TheSameLookIsTheSameMaterialTwice()
        {
            var materials = new ColonistMaterials();
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                if (!materials.Available) Assert.Ignore("Odyssey/Character is not in this build");

                Material? first = materials.For(source, Cells(), Look(0xC89870, 0x3B2A1E, 0x4A4F55));
                Material? second = materials.For(source, Cells(), Look(0xC89870, 0x3B2A1E, 0x4A4F55));

                Assert.That(first, Is.Not.Null);
                // Same colours but a *different* cells object: two bodies sharing one pack
                // material paint different rectangles of it, so they are different materials.
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(materials.MaterialCount, Is.EqualTo(2));
            }
            finally
            {
                materials.Dispose();
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void MaterialsGrowWithLooksAndNotWithColonists()
        {
            // Fifty colonists sharing three looks must cost three materials. If this ever counts
            // fifty, something has begun keying on the pawn and the cache has become a leak.
            var materials = new ColonistMaterials();
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AppearanceCells cells = Cells();
            try
            {
                if (!materials.Available) Assert.Ignore("Odyssey/Character is not in this build");

                var looks = new[]
                {
                    Look(0xF0C8A0, 0x1A1512, 0x4A4F55),
                    Look(0xA87850, 0x5C4028, 0x7A4A3C),
                    Look(0x4A301E, 0x9A9A96, 0x3F5F5A),
                };

                for (int i = 0; i < 50; i++) materials.For(source, cells, looks[i % looks.Length]);
                Assert.That(materials.MaterialCount, Is.EqualTo(looks.Length));
            }
            finally
            {
                materials.Dispose();
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void AnUnclassifiedBodyKeepsItsOwnArt()
        {
            // Null, not a clone that happens to change nothing. The caller then assigns the art
            // material it already had, so a body the classifier could not read looks exactly the
            // way the artist painted it.
            var materials = new ColonistMaterials();
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                ColonistAppearance look = Look(0xC89870, 0x3B2A1E, 0x4A4F55);
                Assert.That(materials.For(source, new AppearanceCells(), look), Is.Null);
                Assert.That(materials.For(source, null, look), Is.Null);
                Assert.That(materials.For(null, Cells(), look), Is.Null);
                Assert.That(materials.MaterialCount, Is.Zero);
            }
            finally
            {
                materials.Dispose();
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }

    /// <summary>
    /// There is one ink line in this game, and it has to stay one.
    ///
    /// <para>The world is inked by <c>OutlineFeature</c>, a screen-space pass over the camera
    /// depth texture. Characters cannot use it: skinned meshes are absent from that texture, which
    /// was measured rather than assumed -- a cube stood behind a colonist keeps its outline
    /// running straight across the colonist, while occluding it perfectly in colour. So characters
    /// ink themselves with a hull pass in <c>Odyssey/Character</c>.</para>
    ///
    /// <para>Two mechanisms drawing what is meant to be the same line is exactly the sort of thing
    /// that drifts, and drifts silently -- somebody retunes the feature and half the picture
    /// follows. The bootstrap copies the feature's values across at startup; this pins the
    /// fallback defaults to the feature's own, so the two agree even before it runs.</para>
    /// </summary>
    public class InkConsistencyTests
    {
        [Test]
        public void TheCharacterInkDefaultsMatchTheOutlineFeature()
        {
            var feature = ScriptableObject.CreateInstance<OutlineFeature>();
            try
            {
                Assert.That(ColonistMaterials.InkWidth, Is.EqualTo(feature.thickness).Within(0.001f),
                    "character ink width has drifted from the outline feature");

                Color ink = ColonistMaterials.InkColour;
                Assert.That(ink.r, Is.EqualTo(feature.outlineColour.r).Within(0.002f), "ink red");
                Assert.That(ink.g, Is.EqualTo(feature.outlineColour.g).Within(0.002f), "ink green");
                Assert.That(ink.b, Is.EqualTo(feature.outlineColour.b).Within(0.002f), "ink blue");
            }
            finally { UnityEngine.Object.DestroyImmediate(feature); }
        }

        [Test]
        public void ColonistsAreDrawnAfterTheInkIsPainted()
        {
            // The outline is a fullscreen pass that paints over the finished image, and colonists
            // are absent from the depth texture it reads -- so it does not know a colonist stands
            // in front of a tree and paints the tree's line over them. Drawing characters after
            // the pass is what puts them on top of that ink rather than under it, and it is the
            // same mechanism, and the same queue, that keeps grass from being inked.
            Assert.That(ColonistMaterials.CharacterQueue,
                Is.GreaterThan(MaterialCache.DefaultFoliageQueue),
                "colonists must be drawn after the ink, or other objects paint their lines over them");
        }

        [Test]
        public void TheCharacterShaderCarriesAnInkPass()
        {
            Shader shader = Shader.Find("Odyssey/Character");
            if (shader == null) Assert.Ignore("Odyssey/Character is not in this build");

            UnityEditor.ShaderData data = UnityEditor.ShaderUtil.GetShaderData(shader);
            UnityEditor.ShaderData.Subshader sub = data.GetSubshader(0);

            var names = new List<string>();
            for (int i = 0; i < sub.PassCount; i++) names.Add(sub.GetPass(i).Name);

            // The ink hull, the lit pass, and a shadow pass that repeats the same alpha clip --
            // without which a hair card casts the shadow of a solid rectangle.
            Assert.That(names, Does.Contain("CharacterInk"));
            Assert.That(names, Does.Contain("CharacterForward"));
            Assert.That(names, Does.Contain("ShadowCaster"));
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
