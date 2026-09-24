#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Levels of detail drawn through instancing, where no <see cref="LODGroup"/> ever runs
    /// (<c>docs/design/36-meadow-overhaul.md</c> §3, M2).
    ///
    /// <para>The world is drawn with <c>RenderMeshInstanced</c>, so a prefab's LOD group does
    /// nothing at draw time and the library used to keep its finest level only. These pin the two
    /// halves that replace it: a prefab resolves into every level, each placed exactly as the
    /// finest so one matrix draws them all — or into its finest level alone when that is not true —
    /// and a level is chosen from the screen height a module fills, by the rule the pack authored
    /// its numbers against.</para>
    ///
    /// <para>The prefab here is built in code with the shape the Meadow art has (e-09 §1): a finest
    /// level of two parts on two materials, a coarser level of one, and a card on a material of its
    /// own, every part on the prefab's origin.</para>
    /// </summary>
    public class InstancedLodTests
    {
        const float GroupSize = 4f;
        static readonly float[] Heights = { 0.5f, 0.2f, 0.05f };

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
                if (made != null) Object.DestroyImmediate(made);
            _made.Clear();
        }

        Material Material(string name)
        {
            var material = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = name };
            _made.Add(material);
            return material;
        }

        GameObject Part(GameObject root, string name, PrimitiveType shape, Material material, Vector3 at)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = at;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>Trunk and leaves, then a trunk alone, then a card; the card may be moved off
        /// the origin to break the one-matrix rule.</summary>
        GameObject Tree(out Material card, Vector3 cardAt = default, bool withGroup = true)
        {
            var root = new GameObject("lod-tree");
            _made.Add(root);
            Material bark = Material("bark"), leaves = Material("leaves");
            card = Material("card");

            GameObject trunk = Part(root, "LOD0 trunk", PrimitiveType.Cube, bark, Vector3.zero);
            GameObject crown = Part(root, "LOD0 crown", PrimitiveType.Sphere, leaves, Vector3.zero);
            GameObject coarse = Part(root, "LOD1 trunk", PrimitiveType.Cube, bark, Vector3.zero);
            GameObject flat = Part(root, "LOD2 card", PrimitiveType.Quad, card, cardAt);
            if (!withGroup)
            {
                Object.DestroyImmediate(coarse);
                Object.DestroyImmediate(flat);
                return root;
            }

            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(Heights[0], new Renderer[] { trunk.GetComponent<Renderer>(), crown.GetComponent<Renderer>() }),
                new LOD(Heights[1], new Renderer[] { coarse.GetComponent<Renderer>() }),
                new LOD(Heights[2], new Renderer[] { flat.GetComponent<Renderer>() }),
            });
            group.size = GroupSize;
            return root;
        }

        ResolvedModule Resolve(GameObject prefab, out ModuleLibrary library)
        {
            var catalogue = ScriptableObject.CreateInstance<ModuleCatalogue>();
            _made.Add(catalogue);
            catalogue.SetEntries(new List<ModuleEntry>
            {
                new ModuleEntry { moduleId = "test.lod.tree", shape = ModuleShape.Pillar, prefab = prefab },
            });
            library = new ModuleLibrary(catalogue);
            return library[library.Resolve("test.lod.tree", ModuleShape.Pillar)];
        }

        [Test]
        public void ALodGroupResolvesIntoEveryLevelPlacedAsTheFinest()
        {
            GameObject prefab = Tree(out Material card);
            ResolvedModule module = Resolve(prefab, out ModuleLibrary library);
            using (library)
            {
                Assert.That(module.UsesArt, Is.True, "the prefab fell back to a primitive");
                Assert.That(module.DrawsByLevel, Is.True, "a one-matrix LOD group was not drawn by level");
                Assert.That(module.Lods.Length, Is.EqualTo(3));
                Assert.That(module.Lods[0].Parts, Is.SameAs(module.Parts),
                    "the finest level is not the module's parts, so every existing reader would see another thing");
                Assert.That(module.Lods[0].Parts.Length, Is.EqualTo(2), "trunk and crown are two materials, two parts");
                Assert.That(module.Lods[1].Parts.Length, Is.EqualTo(1));
                Assert.That(module.Lods[2].Parts[0].Material, Is.SameAs(card),
                    "the card level lost its own material, so a tree would turn the trunk's colour at a distance");

                for (int level = 0; level < Heights.Length; level++)
                    Assert.That(module.Lods[level].ScreenHeight, Is.EqualTo(Heights[level]).Within(1e-6f));

                // The whole point: one matrix draws every part of every level.
                Matrix4x4 local = module.Parts[0].Local;
                foreach (ModuleLod level in module.Lods)
                    foreach (ModulePart part in level.Parts)
                        for (int c = 0; c < 16; c++)
                            Assert.That(part.Local[c], Is.EqualTo(local[c]).Within(1e-4f),
                                "a coarser level was placed differently from the finest, so the swap would be seen");

                Assert.That(module.LodSize, Is.EqualTo(GroupSize).Within(1e-4f),
                    "the group's size did not carry through the placement");
            }
        }

        [Test]
        public void ALevelOffTheOriginKeepsTheFinestLevelOnly()
        {
            GameObject prefab = Tree(out _, cardAt: new Vector3(1f, 0f, 0f));
            ResolvedModule module = Resolve(prefab, out ModuleLibrary library);
            using (library)
            {
                Assert.That(module.DrawsByLevel, Is.False,
                    "a level one matrix cannot place was drawn by level anyway");
                Assert.That(module.Lods.Length, Is.EqualTo(1));
                Assert.That(module.Parts.Length, Is.EqualTo(2),
                    "falling back should mean the finest level, exactly as before levels existed");
            }
        }

        [Test]
        public void APrefabWithoutAGroupHasOneLevel()
        {
            GameObject prefab = Tree(out _, withGroup: false);
            ResolvedModule module = Resolve(prefab, out ModuleLibrary library);
            using (library)
            {
                Assert.That(module.DrawsByLevel, Is.False);
                Assert.That(module.Lods.Length, Is.EqualTo(1));
                Assert.That(module.Lods[0].Parts, Is.SameAs(module.Parts));
            }
        }

        /// <summary>
        /// The pick, by the arithmetic the pack authored against: a 4 m group at a 40° field of view
        /// fills 4 / (2 d tan 20°) of the screen — 0.5 at about 11 m, 0.2 at about 27.5 m.
        /// </summary>
        [Test]
        public void TheLevelFollowsTheScreenHeightItFills()
        {
            GameObject prefab = Tree(out _);
            ResolvedModule module = Resolve(prefab, out ModuleLibrary library);
            using (library)
            {
                Assert.That(ChunkRenderer.LevelFor(module, 5f, 40f, 1f), Is.EqualTo(0), "near");
                Assert.That(ChunkRenderer.LevelFor(module, 20f, 40f, 1f), Is.EqualTo(1), "middle");
                Assert.That(ChunkRenderer.LevelFor(module, 100f, 40f, 1f), Is.EqualTo(2), "far");
                Assert.That(ChunkRenderer.LevelFor(module, 100000f, 40f, 1f), Is.EqualTo(2),
                    "past the pack's last number the last level stays: nothing here culls by size");
                Assert.That(ChunkRenderer.LevelFor(module, 20f, 40f, 2f), Is.EqualTo(0),
                    "a bias of two should keep the finest level twice as far out");
                Assert.That(ChunkRenderer.LevelFor(module, 0f, 40f, 1f), Is.EqualTo(0),
                    "no distance is no viewer, and no viewer draws the finest");
            }
        }

        /// <summary>Off unless a unit turns it on: the pack's numbers put every tuft on screen at its
        /// crudest card from this camera (design 36 §3).</summary>
        [Test]
        public void LevelsAreOffUntilAUnitTurnsThemOn()
        {
            var world = new RenderTestWorld(4, 4, 2).Publish();
            using var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false };
            Assert.That(renderer.UseLods, Is.False);
            Assert.That(renderer.LodBias, Is.EqualTo(1f));
        }
    }
}
