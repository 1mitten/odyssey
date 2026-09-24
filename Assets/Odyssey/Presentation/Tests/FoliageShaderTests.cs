#nullable enable

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Meadow foliage drawn by <c>Odyssey/Foliage</c> (<c>docs/design/38-meadow-overhaul.md</c> §4,
    /// M3): the shader is there and every pass moves a vertex the same way, Meadow materials are
    /// routed to it with their own textures and nothing else is, the wind runs on the game tick, and
    /// the clearance field clears round what is stamped into it and nowhere else.
    ///
    /// <para>What these cannot say is whether the grass looks like grass; that is the playtest row.
    /// The tests that need a real Meadow material ask whether it resolved, because the CI runner has
    /// no <c>Assets/Synty</c> and a material that loads with a null shader would pass for the wrong
    /// reason (CLAUDE.md, "ask whether the art resolved").</para>
    /// </summary>
    public class FoliageShaderTests
    {
        const string ShaderPath = "Odyssey/Presentation/Shaders/OdysseyFoliage.shader";
        const string MeadowGrass =
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/Plants/Grass_Med_Mat_01.mat";

        [TearDown]
        public void TearDown()
        {
            MaterialCache.OwnFoliageShader = true;
            Shader.SetGlobalVector("_OdysseyWind", Vector4.zero);
        }

        [Test]
        public void TheShaderIsThere()
        {
            Shader? shader = Shader.Find("Odyssey/Foliage");
            Assert.That(shader, Is.Not.Null, "Odyssey/Foliage is missing, so Meadow foliage draws with the pack's shader");
            Assert.That(shader!.isSupported, Is.True, "Odyssey/Foliage failed to compile on this device");
        }

        /// <summary>
        /// Every pass bends a vertex through the one include, and nothing reads wall time. A depth or
        /// shadow pass that bent differently would put the outline or the shadow where the grass is
        /// not; a pass that read <c>_Time</c> would wave through a pause.
        /// </summary>
        [Test]
        public void EveryPassMovesAVertexTheSameWayAndNoneReadsWallTime()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, ShaderPath));
            string code = Regex.Replace(source, @"//[^\n]*", string.Empty);

            int passes = Regex.Matches(code, @"\bPass\s*\{").Count;
            int calls = Regex.Matches(code, @"\bFoliageDisplace\(input\.").Count;
            Assert.That(passes, Is.EqualTo(5), "the forward, shadow, ghost-depth, depth and depth-normals passes");
            Assert.That(calls, Is.EqualTo(passes), "a pass positions its vertices without FoliageDisplace");
            Assert.That(code, Does.Not.Contain("_Time"), "the wind must come from the tick, not from _Time");
            Assert.That(Regex.Matches(code, @"multi_compile_instancing").Count, Is.EqualTo(passes),
                "a pass without instancing draws every clump at the origin, silently");
        }

        /// <summary>A crop carries the foliage tint and is PolygonFarm art with no leaf slot: it keeps
        /// its own material. Keying the swap on the tint drew every carrot as grass once already.</summary>
        [Test]
        public void AFoliageTintedMaterialWithoutALeafSlotKeepsItsOwnShader()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var crop = new Material(lit) { name = "crop" };
            var cache = new MaterialCache();
            try
            {
                Material drawn = cache.Get(crop, Color.white, Color.black, ghost: false, alpha: 1f, foliage: true);
                Assert.That(drawn.shader, Is.SameAs(lit));
                Assert.That(FoliageLook.IsMeadowFoliage(crop), Is.False);
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(crop);
            }
        }

#if UNITY_EDITOR
        static Material? MeadowMaterial()
        {
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MeadowGrass);
            // Asked of the art: the file, its shader, and a filled leaf slot.
            if (material == null || material.shader == null || !FoliageLook.IsMeadowFoliage(material)) return null;
            return material;
        }

        [Test]
        public void AMeadowMaterialIsDrawnByOurShaderWithItsOwnTextures()
        {
            Material? meadow = MeadowMaterial();
            if (meadow == null) Assert.Ignore("the Meadow grass material did not resolve on this machine");

            var cache = new MaterialCache();
            try
            {
                Material drawn = cache.Get(meadow!, Color.white, Color.black, ghost: false, alpha: 1f, foliage: true);
                Assert.That(drawn.shader.name, Is.EqualTo("Odyssey/Foliage"));
                Assert.That(drawn.GetTexture("_LeafMap"), Is.SameAs(meadow!.GetTexture("_Leaf_Texture")),
                    "the clone does not carry the art's own leaf texture");
                Assert.That(drawn.enableInstancing, Is.True);
                Assert.That(drawn.renderQueue, Is.EqualTo(MaterialCache.FoliageQueue),
                    "grass stays in the late queue, out of the ink and the opaque prepass (design 38 §13)");
                Assert.That(drawn.GetVector("_LeafGrade"), Is.EqualTo(FoliageLook.LeafGrade));

                // And the switch the measurement arm uses really does reach the clones.
                Assert.That(cache.ForgetFoliage(), Is.EqualTo(1));
                MaterialCache.OwnFoliageShader = false;
                Material pack = cache.Get(meadow!, Color.white, Color.black, ghost: false, alpha: 1f, foliage: true);
                Assert.That(pack.shader, Is.SameAs(meadow!.shader), "with ours off the pack's shader should draw");
            }
            finally
            {
                cache.Dispose();
            }
        }
#endif

        /// <summary>The wind is a function of the tick and of nothing else, so a paused world — one
        /// whose tick does not move — holds still, and two machines agree at the same tick.</summary>
        [Test]
        public void TheWindIsAFunctionOfTheTick()
        {
            var a = new WindDirector();
            var b = new WindDirector();
            try
            {
                a.Apply(1234);
                b.Apply(1234);
                Assert.That(a.Phase, Is.EqualTo(b.Phase));
                Assert.That(a.Push, Is.EqualTo(b.Push));

                a.Apply(1234);
                float held = a.Phase;
                a.Apply(1234);
                Assert.That(a.Phase, Is.EqualTo(held), "the same tick moved the wind, so a pause would not hold");

                a.Apply(1234 + (long)a.TicksPerGust);
                Assert.That(a.Phase - held, Is.EqualTo(Mathf.PI * 2f).Within(1e-3f), "one gust is one turn");
            }
            finally
            {
                a.Dispose();
                b.Dispose();
            }
            Assert.That(Shader.GetGlobalVector("_OdysseyWind"), Is.EqualTo(Vector4.zero),
                "disposing left a wind blowing for the next thing this editor draws");
        }

        [Test]
        public void TheClearanceFieldClearsRoundAStampAndNowhereElse()
        {
            using var field = new GrassClearance();
            var focus = new Vector3(100f, 0f, 100f);
            field.Begin(focus);

            var item = new Vector3(101.3f, 0f, 98.7f);
            field.Stamp(item, 0.55f);
            Assert.That(field.Stamps, Is.EqualTo(1));
            Assert.That(field.At(item), Is.GreaterThan(0.9f), "the ground under the item is not cleared");
            Assert.That(field.At(item + new Vector3(2f, 0f, 0f)), Is.Zero, "the ring reaches further than asked");

            field.Stamp(focus + new Vector3(GrassClearance.WindowMetres, 0f, 0f), 0.55f);
            Assert.That(field.Stamps, Is.EqualTo(1), "a stamp outside the window was counted");

            field.Begin(focus);
            Assert.That(field.At(item), Is.Zero, "a new frame kept last frame's stamps");
        }
    }
}
