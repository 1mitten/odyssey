#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// A colonist past the figure cap wears the issued uniform, not the pack's own paint
    /// (<c>docs/design/29-modular-colonists.md</c> §13a).
    ///
    /// <para><b>The owner's report, 2026-09-23:</b> past a certain number of colonists some were
    /// "in an orange suit" and textures "kept switching". The certain number is the 64-figure cap.
    /// The far form drew the jumpsuit in the pack's own paint, and PolygonGeneric paints that
    /// jumpsuit burnt orange (<c>#B06F24</c>). So every colonist beyond the nearest 64 changed into
    /// orange, and changed back as the camera brought them into the nearest 64.</para>
    ///
    /// <para><b>It needs the licensed packs and says so</b>, on the rule
    /// <see cref="FarColonistHeadTests"/> states: ask whether the art <i>resolved</i>, never whether
    /// there is a catalogue.</para>
    /// </summary>
    public class FarColonistUniformTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static readonly int ClothColourId = Shader.PropertyToID("_ClothColour");
        static readonly int SkinRectId = Shader.PropertyToID("_SkinRect0");
        static readonly int HairRectId = Shader.PropertyToID("_HairRect0");

        [Test]
        public void AFarColonistWearsTheIssuedUniformAndNotThePacksPaint()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null) Assert.Ignore("no catalogue on this machine");

            ColonistCastPools pools = AppearanceBooks.For(0u, catalogue).Pools;
            if (!pools.HasUniform) Assert.Ignore("this catalogue issues no uniform");

            var library = new ModuleLibrary(catalogue);
            using var materials = new ColonistMaterials();
            try
            {
                if (!materials.Available) Assert.Fail("Odyssey/Character did not resolve");

                var size = new GridSize(4, 4, 2);
                var model = new WorldRenderModel(size, new ChunkGrid(size), library);
                using var renderer = new ChunkRenderer(model) { Recolours = materials };

                int checkedBodies = 0;
                foreach (int uniform in new[] { pools.UniformMale, pools.UniformFemale })
                {
                    if (uniform == ColonistCastPools.NoUniform) continue;
                    ResolvedModule body = library[library.Resolve(ModuleIds.Colonist(uniform), ModuleShape.Pillar)];
                    if (!body.UsesArt) continue;
                    checkedBodies++;

                    Material?[]? painted = renderer.FarMaterialsFor(uniform);
                    Assert.That(painted, Is.Not.Null,
                        $"uniform body {uniform} is drawn past the cap in the pack's own paint");

                    int paintedParts = 0;
                    foreach (Material? m in painted!)
                    {
                        if (m == null) continue;
                        paintedParts++;

                        // Near white in either colour space, and nowhere near the pack's orange,
                        // whose blue channel is 0x24.
                        Color c = m.GetColor(ClothColourId);
                        Assert.That(Mathf.Min(c.r, Mathf.Min(c.g, c.b)), Is.GreaterThan(0.7f),
                            $"body {uniform} far cloth is {c}, not the issued white");

                        // Cloth only: the far form keeps the pack's skin and hair, as it always has,
                        // so the two slots must be switched off rather than painted a default.
                        Assert.That(m.GetVector(SkinRectId), Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                        Assert.That(m.GetVector(HairRectId), Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                        Assert.That(m.enableInstancing, Is.True, "the far form is drawn instanced");
                    }
                    Assert.That(paintedParts, Is.GreaterThan(0));

                    // Resolved once: asking again is the same materials, not new ones.
                    int before = materials.MaterialCount;
                    Assert.That(renderer.FarMaterialsFor(uniform), Is.SameAs(painted));
                    Assert.That(materials.MaterialCount, Is.EqualTo(before));
                }

                if (checkedBodies == 0)
                    Assert.Ignore("no uniform body resolved its art — the licensed packs are absent");

                // Negative control: a body out of the pool is still drawn in its own paint,
                // because its colours are rolled per colonist and the far form has one material a body.
                List<ModuleEntry> rows = catalogue!.FindFamily(ModuleIds.ColonistBase);
                for (int i = 0; i < rows.Count; i++)
                {
                    if (i == pools.UniformMale || i == pools.UniformFemale) continue;
                    // The gang's bodies own their colour too (design 42): FarBanditTests holds them.
                    if (pools.IsBanditBody(i)) continue;
                    Assert.That(renderer.FarMaterialsFor(i), Is.Null,
                        $"{rows[i].prefabName} claimed a far colour it does not own");
                }
            }
            finally { library.Dispose(); }
        }
    }
}
