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
    /// The bandit gang's rows and how they are drawn past the figure cap
    /// (<c>docs/design/42-bandits.md</c>): six bodies with their vests, the welding helmet, and a
    /// far form in red and black rather than the pack's camo — the owner's report was that the
    /// hostiles "look like colonists", and a far bandit in the pack's paint would look like
    /// nobody in particular.
    ///
    /// <para><b>It needs the licensed packs and says so</b>, asking whether the gang's own rows
    /// resolved (<see cref="FarColonistHeadTests"/>' rule): the runner has animal art of the
    /// project's own, so "is any art here" is the wrong question.</para>
    /// </summary>
    public class FarBanditTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static readonly int ClothColourId = Shader.PropertyToID("_ClothColour");
        static readonly int Cloth2ColourId = Shader.PropertyToID("_Cloth2Colour");
        static readonly int ClothRectId = Shader.PropertyToID("_ClothRect0");
        static readonly int Cloth2RectId = Shader.PropertyToID("_Cloth2Rect0");
        static readonly int SkinRectId = Shader.PropertyToID("_SkinRect0");

        static readonly Vector4 Empty = new Vector4(1f, 1f, 0f, 0f);
        static readonly Vector4 WholeAtlas = new Vector4(0f, 0f, 1f, 1f);

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null) Assert.Ignore("no catalogue on this machine");
            return catalogue!;
        }

        static List<int> ResolvedGang(ModuleCatalogue catalogue, ColonistCastPools pools)
        {
            List<ModuleEntry> rows = catalogue.FindFamily(ModuleIds.ColonistBase);
            var gang = new List<int>();
            foreach (int i in pools.BanditMale) if (rows[i].prefab != null) gang.Add(i);
            foreach (int i in pools.BanditFemale) if (rows[i].prefab != null) gang.Add(i);
            if (gang.Count == 0) Assert.Ignore("no bandit body resolved its art — the licensed packs are absent");
            return gang;
        }

        [Test]
        public void TheGangIsSixBodiesThreeVestsEachAndOneHelmet()
        {
            ModuleCatalogue catalogue = Catalogue();
            ColonistCastPools pools = AppearanceBooks.PoolsFrom(catalogue);
            Assert.That(pools.BanditMale, Has.Length.EqualTo(3), "the male gang");
            Assert.That(pools.BanditFemale, Has.Length.EqualTo(3), "the female gang");

            List<ModuleEntry> rows = catalogue.FindFamily(ModuleIds.ColonistBase);
            var vests = new HashSet<string>();
            foreach (int i in ResolvedGang(catalogue, pools))
            {
                ModuleEntry row = rows[i];
                Assert.That(row.colonistPool || row.uniform, Is.False, $"{row.moduleId} is a colonist's too");
                Assert.That(row.overlayName, Does.Contain("_Armor_0"), row.moduleId);
                vests.Add(row.overlayName);

                // The body keeps its skin; everything else on it is the black slot, the whole atlas.
                Assert.That(row.appearance.skin, Is.Not.Empty, $"{row.moduleId} lost its skin");
                Assert.That(row.appearance.cloth, Is.Empty, $"{row.moduleId}: the body carries no red");
                Assert.That(row.appearance.cloth2, Has.Length.EqualTo(1));
                Assert.That(row.appearance.cloth2[0], Is.EqualTo(Rect.MinMaxRect(0f, 0f, 1f, 1f)));

                // The vest's own rectangles: its box red, the rest black.
                Assert.That(row.overlayAppearance.cloth, Has.Length.EqualTo(1), $"{row.moduleId} vest has no red");
                Assert.That(row.overlayAppearance.cloth[0].width, Is.GreaterThan(0.1f), "the vest box is the camo, not a swatch");

                // The rig carries the vest it names.
                GameObject instance = Object.Instantiate(row.prefab!);
                try
                {
                    ColonistAttachments.BareTheHead(instance);
                    SkinnedMeshRenderer? vest = ColonistAttachments.ShowOverlay(instance, row.overlayName);
                    Assert.That(vest, Is.Not.Null, $"{row.prefabName} has no {row.overlayName}");
                    Assert.That(vest!.gameObject.activeInHierarchy, Is.True);
                }
                finally { Object.DestroyImmediate(instance); }
            }
            Assert.That(vests, Has.Count.EqualTo(6), "three cuts for each sex");

            Assert.That(pools.Headgear, Has.Length.EqualTo(1), "the welding helmet");
            ModuleEntry helmet = catalogue.FindFamily(ModuleIds.HeadgearBase)[pools.Headgear[0]];
            Assert.That(helmet.prefabName, Is.EqualTo("SM_Chr_Attach_Helmet_03"));
            Assert.That(helmet.appearance.Any, Is.False, "the helmet is worn in the pack's own paint");
        }

        [Test]
        public void AColonistIsNeverDealtTheGangsBody()
        {
            ModuleCatalogue catalogue = Catalogue();
            ColonistAppearanceBook book = AppearanceBooks.For(20260924u, catalogue);
            for (int pawn = 1; pawn <= 300; pawn++)
            {
                ColonistAppearance colonist = book.For(pawn, (uint)(pawn * 7919));
                Assert.That(book.Pools.IsBanditBody(colonist.Look), Is.False, $"pawn {pawn}");
                Assert.That(colonist.HidesHair, Is.False);
            }
        }

        [Test]
        public void AFarBanditWearsTheRedVestAndIsBlackWhereItIsNotSkin()
        {
            ModuleCatalogue catalogue = Catalogue();
            ColonistCastPools pools = AppearanceBooks.PoolsFrom(catalogue);
            List<int> gang = ResolvedGang(catalogue, pools);

            var library = new ModuleLibrary(catalogue);
            using var materials = new ColonistMaterials();
            try
            {
                if (!materials.Available) Assert.Fail("Odyssey/Character did not resolve");
                var size = new GridSize(4, 4, 2);
                var model = new WorldRenderModel(size, new ChunkGrid(size), library);
                // The renderer's own book is built from the catalogue, so it has the gang in it.
                using var renderer = new ChunkRenderer(model) { Recolours = materials };

                foreach (int body in gang)
                {
                    ResolvedModule module = library[library.Resolve(ModuleIds.Colonist(body), ModuleShape.Pillar)];
                    Assume.That(module.UsesArt, Is.True);

                    Material?[]? painted = renderer.FarMaterialsFor(body);
                    Assert.That(painted, Is.Not.Null, $"bandit body {body} is drawn past the cap in the pack's camo");

                    int vestParts = 0, bodyParts = 0;
                    ModulePart[] parts = module.Parts;
                    for (int p = 0; p < parts.Length; p++)
                    {
                        Material? m = painted![p];
                        if (m == null) continue;
                        // BanditTrousers is #1E1E22: near-black, not black, so up to 0x22 / 255.
                        Color black = m.GetColor(Cloth2ColourId);
                        Assert.That(Mathf.Max(black.r, Mathf.Max(black.g, black.b)), Is.LessThan(0.2f), $"{black} is not black");
                        Assert.That(m.GetVector(Cloth2RectId), Is.EqualTo(WholeAtlas));

                        if (library.IsOverlayMaterial(parts[p].Material))
                        {
                            vestParts++;
                            Color red = m.GetColor(ClothColourId);
                            Assert.That(red.r, Is.GreaterThan(2f * red.g), $"vest {red} is not red");
                            Assert.That(m.GetVector(ClothRectId), Is.Not.EqualTo(Empty));
                        }
                        else
                        {
                            bodyParts++;
                            // The red belongs to the vest: on the male rig the vest's camo and the
                            // trousers share a box, so a red slot here would paint the trousers.
                            Assert.That(m.GetVector(ClothRectId), Is.EqualTo(Empty), "the body carries the vest's red");
                            Assert.That(m.GetVector(SkinRectId), Is.Not.EqualTo(Empty), "the far skin would go black");
                        }
                    }
                    Assert.That(vestParts, Is.GreaterThan(0), $"body {body}: the vest was merged into the body or not baked");
                    Assert.That(bodyParts, Is.GreaterThan(0), $"body {body}: no body part");
                }
            }
            finally { library.Dispose(); }
        }
    }
}
