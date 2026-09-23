#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// The baked far form has a head to hang hair on, and it is where a head should be
    /// (<c>docs/design/29-modular-colonists.md</c>, MC6).
    ///
    /// <para><b>Why this is measured rather than reasoned about.</b> The head transform is read off
    /// a rig that is instantiated, posed, measured and destroyed inside one method, then pushed
    /// through the same normalisation every part gets — a pivot convention and a 1.4 scale. Getting
    /// either wrong puts a colonist's hair at their feet or a metre above them, and nothing else in
    /// the project would notice: the far form only appears past the figure cap, so it is exactly
    /// the kind of fault that ships.</para>
    ///
    /// <para><b>It needs the licensed packs and says so.</b> A catalogue row whose prefab did not
    /// resolve bakes nothing, so on a machine without <c>Assets/Synty</c> — which is the CI runner —
    /// there is no head to find and no claim to make. The right question is whether the art
    /// <i>resolved</i>, never whether there is a catalogue: the catalogue is committed and its
    /// prefab references point into the gitignored folder, so it loads perfectly with every
    /// reference null on exactly the machine that can draw nobody.</para>
    /// </summary>
    public class FarColonistHeadTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static ModuleCatalogue? Catalogue() =>
            AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);

        [Test]
        public void EveryColonistInThePoolHasAHeadWhereAHeadShouldBe()
        {
            ModuleCatalogue? catalogue = Catalogue();
            if (catalogue == null) Assert.Ignore("no catalogue on this machine");

            var library = new ModuleLibrary(catalogue);
            try
            {
                List<ModuleEntry> rows = catalogue!.FindFamily(ModuleIds.ColonistBase);
                int checkedBodies = 0;

                for (int i = 0; i < rows.Count; i++)
                {
                    if (!rows[i].colonistPool) continue;

                    ResolvedModule module = library[library.Resolve(ModuleIds.Colonist(i), ModuleShape.Pillar)];
                    if (!module.UsesArt) continue;

                    checkedBodies++;
                    Assert.That(module.HasHead, Is.True,
                        $"{rows[i].prefabName} baked without a head bone, so it can wear no hair");

                    Vector3 head = module.Head.GetColumn(3);
                    Bounds b = module.Bounds;

                    // A head is near the top of a standing body and near its middle in plan. The
                    // bands are wide on purpose: this is catching a head at the feet or a scale
                    // applied twice, not auditing anatomy.
                    Assert.That(head.y, Is.GreaterThan(b.min.y + b.size.y * 0.6f),
                        $"{rows[i].prefabName}: head at y {head.y:F3} is too low in a body " +
                        $"spanning {b.min.y:F3} to {b.max.y:F3}");
                    Assert.That(head.y, Is.LessThan(b.max.y + 0.15f),
                        $"{rows[i].prefabName}: head at y {head.y:F3} is above a body topping " +
                        $"out at {b.max.y:F3}");
                    Assert.That(Mathf.Abs(head.x - b.center.x), Is.LessThan(0.35f),
                        $"{rows[i].prefabName}: head off to one side");
                    Assert.That(Mathf.Abs(head.z - b.center.z), Is.LessThan(0.35f),
                        $"{rows[i].prefabName}: head fore or aft of the body");
                }

                if (checkedBodies == 0)
                    Assert.Ignore("no colonist row resolved its art — the licensed packs are absent");
            }
            finally { library.Dispose(); }
        }

        [Test]
        public void TheBakedBodyIsNotWearingThePacksOwnHair()
        {
            // PolygonGeneric ships hair, hats and hoods as *active* skinned children, and the bake
            // takes every active renderer. Without ColonistAttachments.BareTheHead in the bake, a
            // colonist past the figure cap wore the pack's hair while the same colonist in front of
            // the camera wore ours -- two drawers, two answers, which is the fault ColonistLook's
            // header exists to warn about.
            ModuleCatalogue? catalogue = Catalogue();
            if (catalogue == null) Assert.Ignore("no catalogue on this machine");

            List<ModuleEntry> rows = catalogue!.FindFamily(ModuleIds.ColonistBase);
            int generic = -1;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].colonistPool && rows[i].prefabName.StartsWith("SM_Gen_Chr_"))
                {
                    generic = i;
                    break;
                }

            if (generic < 0) Assert.Ignore("no PolygonGeneric body in the pool");

            var library = new ModuleLibrary(catalogue);
            try
            {
                ResolvedModule module = library[library.Resolve(ModuleIds.Colonist(generic), ModuleShape.Pillar)];
                if (!module.UsesArt) Assert.Ignore("the licensed packs are absent");

                foreach (ModulePart part in module.Parts)
                    Assert.That(part.Mesh.name, Does.Not.Contain("_Attach_"),
                        $"{rows[generic].prefabName} baked '{part.Mesh.name}' in: the head was " +
                        "not bared before the bake");
            }
            finally { library.Dispose(); }
        }
    }
}
