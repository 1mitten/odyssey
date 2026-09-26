#nullable enable

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The owner's first look at the Meadow look pass, and the three fixes it asked for
    /// (<c>docs/design/38-meadow-overhaul.md</c> §17c): leaves fade to a faint ghost over a
    /// colonist, no ink on terrain, and mixed stands of leaf colour like the reference.
    ///
    /// <para>What the tier can prove is here; what only a picture can show — does the fade read as
    /// a ghost, are the terrace lines gone, do the stands look like #13 — is the look harness
    /// (<c>FrameTimeTests.TheLookAtThePlayCamera</c>) and the owner's Play.</para>
    /// </summary>
    public class LookFixesTests
    {
        static string Shader(string file) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Odyssey/Presentation/Shaders", file));

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
                if (made != null) Object.DestroyImmediate(made);
            _made.Clear();
        }

        // ------------------------------------------------------------------ leaf colours

        /// <summary>
        /// Mostly greens, a quarter or so gold and orange, the odd red — over thousands of trees on a
        /// board-sized area, as the owner chose ("like #13: mixed stands").
        /// </summary>
        [Test]
        public void MostTreesAreGreenWithPatchesOfAutumnAndTheOddRed()
        {
            var random = new System.Random(7);
            int green = 0, autumn = 0, red = 0, total = 20000;
            for (int i = 0; i < total; i++)
            {
                float x = (float)random.NextDouble() * 600f, z = (float)random.NextDouble() * 600f;
                switch (LeafVariety.At(x, z))
                {
                    case LeafVariety.Family.Green:
                    case LeafVariety.Family.Lime: green++; break;
                    case LeafVariety.Family.Gold:
                    case LeafVariety.Family.Orange: autumn++; break;
                    default: red++; break;
                }
            }
            float g = green / (float)total, a = autumn / (float)total, r = red / (float)total;
            Assert.That(g, Is.InRange(0.60f, 0.82f), $"greens {g:P0}: the meadow should read green first");
            Assert.That(a, Is.InRange(0.15f, 0.38f), $"gold and orange {a:P0}: patches, not a scatter or a blanket");
            Assert.That(r, Is.InRange(0.01f, 0.07f), $"red {r:P1}: the odd one");
        }

        /// <summary>
        /// Grouped in stands: an autumn tree's near neighbour is autumn far more often than a tree
        /// picked at random is — the reference's colours come in patches, not confetti.
        /// </summary>
        [Test]
        public void AutumnComesInStandsNotConfetti()
        {
            var random = new System.Random(11);
            int autumnTrees = 0, autumnNeighbours = 0, all = 0, allAutumn = 0;
            for (int i = 0; i < 20000; i++)
            {
                float x = (float)random.NextDouble() * 600f, z = (float)random.NextDouble() * 600f;
                bool here = IsAutumn(LeafVariety.At(x, z));
                all++;
                if (here) allAutumn++;
                if (!here) continue;
                autumnTrees++;
                if (IsAutumn(LeafVariety.At(x + 5f, z + 2.5f))) autumnNeighbours++;
            }
            float baseline = allAutumn / (float)all;
            float together = autumnNeighbours / (float)autumnTrees;
            Assert.That(together, Is.GreaterThan(baseline * 1.5f),
                $"an autumn tree's neighbour is autumn {together:P0} of the time against {baseline:P0} overall, so there are no stands");
        }

        static bool IsAutumn(LeafVariety.Family f) => f == LeafVariety.Family.Gold || f == LeafVariety.Family.Orange;

        [Test]
        public void ATreeIsAlwaysDealtTheSameColour()
        {
            for (int i = 0; i < 50; i++)
            {
                float x = i * 13.7f, z = i * 7.3f;
                Assert.That(LeafVariety.At(x, z), Is.EqualTo(LeafVariety.At(x, z)));
            }
        }

        /// <summary>
        /// The shader deals the colour; <see cref="LeafVariety"/> only mirrors it so it can be
        /// counted. Two copies of one rule stay one rule only if something checks them against each
        /// other (P1), so this reads the shader's constants back out of its source.
        /// </summary>
        [Test]
        public void TheShaderDealsColourByTheRuleTheMirrorCounts()
        {
            string source = Shader("OdysseyFoliage.shader");
            string[] expected =
            {
                $"xz * {LeafVariety.StandScale.ToString(System.Globalization.CultureInfo.InvariantCulture)} + {LeafVariety.StandOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"xz * {LeafVariety.TreeScale.ToString(System.Globalization.CultureInfo.InvariantCulture)} + {LeafVariety.TreeOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"xz * {LeafVariety.PickScale.ToString(System.Globalization.CultureInfo.InvariantCulture)} + {LeafVariety.PickOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"s * {LeafVariety.StandWeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"h2 > {LeafVariety.RedAbove.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"t > {LeafVariety.AutumnAbove.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"h > {LeafVariety.LimeAbove.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            };
            foreach (string constant in expected)
                Assert.That(source, Does.Contain(constant),
                    $"Odyssey/Foliage no longer deals the stand colours by `{constant}`, so LeafVariety counts a rule the game does not draw");
        }

        // ------------------------------------------------------------------ the leaf fade

        /// <summary>
        /// A crown in a colonist's line of sight is drawn by our foliage shader at the fade the
        /// owner chose, cut-out kept — not by the translucent stand-in, whose whole-pane leaf cards
        /// stacked into a solid crown.
        /// </summary>
        [Test]
        public void AFadedCrownIsOurShaderAtAFaintGhost()
        {
            if (UnityEngine.Shader.Find("Odyssey/Foliage") == null) Assert.Ignore("Odyssey/Foliage is not in this project");
            var art = new Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")) { name = "SomeTreeArt" };
            _made.Add(art);
            var cache = new MaterialCache();
            try
            {
                Material? solid = cache.GetTree(art, Color.white, 1f, 1f);
                Material? faded = cache.GetTree(art, Color.white, ChunkRenderer.DefaultSightLeafFade, 1f);
                Assert.That(solid, Is.Not.Null);
                Assert.That(faded, Is.Not.Null);
                Assert.That(faded, Is.Not.SameAs(solid), "the faded crown shares the solid crown's material");
                Assert.That(faded!.shader.name, Is.EqualTo("Odyssey/Foliage"), "the faded crown left our shader, so its cut-out is lost");
                Assert.That(faded.GetFloat("_Fade"), Is.EqualTo(0.15f).Within(0.005f));
                Assert.That(solid!.GetFloat("_Fade"), Is.EqualTo(1f));
                Assert.That(faded.GetFloat("_StandVariety"), Is.EqualTo(1f), "a faded tree must keep its colour");

                // A ghost, not a dither: blended over its own depth pass, drawn with the
                // transparents. The solid crown must not pay for the ghost's depth pass.
                Assert.That(faded.GetFloat("_Ghost"), Is.EqualTo(1f));
                Assert.That(faded.GetShaderPassEnabled(MaterialCache.GhostPass), Is.True,
                    "the ghost has no depth pass, so its forty leaf cards stack back into a solid crown");
                Assert.That(faded.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
                Assert.That(solid.GetShaderPassEnabled(MaterialCache.GhostPass), Is.False,
                    "every solid crown draws the ghost's depth pass for nothing");
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public void TheFadeIsAFaintGhostByDefault() =>
            Assert.That(ChunkRenderer.DefaultSightLeafFade, Is.InRange(0.1f, 0.2f), "the owner chose about 15%");

        // ------------------------------------------------------------------ no ink on terrain

        /// <summary>
        /// The mark and its reader: the ground's DepthNormals pass writes the terrain mark into the
        /// spare channel, the outline reads it and leaves the pixel alone.
        /// </summary>
        [Test]
        public void TheGroundMarksItselfAndTheInkReadsTheMark()
        {
            string ground = Shader("OdysseyMeadowGround.shader");
            Assert.That(ground, Does.Contain("#define ODYSSEY_TERRAIN_MARK 1.0"));
            Assert.That(ground, Does.Contain("NormalizeNormalPerPixel(input.normalWS), ODYSSEY_TERRAIN_MARK)"),
                "the ground's DepthNormals pass no longer writes the terrain mark");

            string outline = Shader("OdysseyOutline.shader");
            Assert.That(outline, Does.Contain("_CameraNormalsTexture"));
            Assert.That(outline, Does.Contain("if (terrain > 0.5) return scene;"),
                "the ink no longer leaves marked terrain alone");
        }

        /// <summary>
        /// Everything that keeps its ink must overwrite the mark with 0 in the prepass, or it stands
        /// on the ground's mark and loses its line. A colonist was never in the prepass.
        /// </summary>
        [TestCase("OdysseyCharacter.shader")]
        [TestCase("OdysseyTree.shader")]
        [TestCase("OdysseyFoliage.shader")]
        public void WhatIsNotTerrainWritesNoMark(string file)
        {
            string source = Shader(file);
            Assert.That(source, Does.Contain("\"LightMode\" = \"DepthNormals\""),
                $"{file} has no DepthNormals pass, so whatever it draws stands on the ground's terrain mark and loses its outline");
            Assert.That(source, Does.Not.Contain("ODYSSEY_TERRAIN_MARK"), $"{file} marks itself as terrain");
        }

        /// <summary>
        /// Every natural terrain draws through the ground shader when the look is on, or it keeps
        /// its ink. Asked of the art, since the look only resolves where the packs are.
        /// </summary>
        [Test]
        public void EveryNaturalTerrainIsDrawnByTheGroundShader()
        {
            if (!MeadowLook.GroundActive) Assert.Ignore("the Meadow look did not resolve on this machine");
#if UNITY_EDITOR
            var catalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
            if (catalogue == null) Assert.Ignore("no catalogue");
            using var library = new ModuleLibrary(catalogue);
            string[] terrains = { "Grass", "BareEarth", "PackedGravel", "Subsoil", "Marsh", "Sand" };
            foreach (string terrain in terrains)
            {
                ResolvedModule module = library[library.Resolve(ModuleIds.Terrain(terrain), ModuleShape.GroundBlock)];
                Assert.That(module.Parts[0].Material.shader.name, Is.EqualTo("Odyssey/MeadowGround"),
                    $"{terrain} is drawn by {module.Parts[0].Material.shader.name}, which writes no terrain mark, so its steps keep their ink");
            }

            // Stone stays on the pack's shader, deliberately: through ours an outcrop drew blue, and
            // an outcrop keeps its outline (design 38 §17c).
            ResolvedModule rock = library[library.Resolve(ModuleIds.Terrain("Rock"), ModuleShape.RockBlock)];
            Assert.That(rock.Parts[0].Material.shader.name, Is.Not.EqualTo("Odyssey/MeadowGround"));
#endif
        }
    }
}
