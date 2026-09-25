#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The jump's two clips in the committed catalogue (design 44 §7): the take-off and the landing,
    /// each naming the masculine and the feminine clip and the file both live in; every clip that
    /// resolved is Base Locomotion's in-place, Humanoid walking jump, and as long as
    /// <see cref="JumpArc"/> assumes; where none did — the runner — the figure holds its gait.
    ///
    /// <para><b>Written without a Unity run</b> (the session that wrote it had none). The rows are
    /// in the committed asset with empty clip references, so the runner passes; the owner's
    /// catalogue rebuild fills the references, and this is what says whether it did.</para>
    /// </summary>
    public class JumpClipRowTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";
        const string BaseLocomotionPolygon = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon";

        /// <summary>A clip's length may differ from the arc's constant by this much: one frame at sixty.</summary>
        const float OneFrame = 1f / 60f;

        static ModuleCatalogue Catalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            return catalogue!;
        }

        [Test]
        public void TheJumpRowsResolveToInPlaceWalkingJumpsOrTheFigureHoldsItsGait()
        {
            ModuleCatalogue catalogue = Catalogue();
            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                int resolved = 0;
                foreach (string id in ModuleIds.JumpRows)
                {
                    ModuleEntry? row = catalogue.Find(id);
                    Assert.That(row, Is.Not.Null, $"{id} is in the committed catalogue");
                    bool land = id == ModuleIds.JumpLand;
                    var variants = new List<string>();
                    foreach (CombatClipEntry entry in row!.combat)
                    {
                        variants.Add(entry.variant);
                        Assert.That(entry.clipName, Does.StartWith(land ? "A_Land_Walking_" : "A_Jump_Walking_"));
                        Assert.That(entry.fileName, Does.StartWith("A_Jump_Walking_").And.Not.Contain("RootMotion"),
                            "both clips are in the in-place take-off's file");
                        if (entry.clip == null)
                        {
                            Assert.That(System.IO.Directory.Exists(BaseLocomotionPolygon), Is.False,
                                $"the pack is here and {entry.clipName} did not resolve: rebuild the catalogue");
                            continue;
                        }
                        resolved++;
                        string path = AssetDatabase.GetAssetPath(entry.clip);
                        Assert.That(path, Does.StartWith(BaseLocomotionPolygon), entry.clipName);
                        Assert.That(System.IO.Path.GetFileNameWithoutExtension(path), Is.EqualTo(entry.fileName));
                        Assert.That(entry.clip.name, Is.EqualTo(entry.clipName), "the file's other clip was taken");
                        Assert.That(entry.clip.humanMotion, Is.True, $"{entry.clipName} drives a Humanoid rig");
                        Assert.That(entry.clip.length,
                            Is.EqualTo(land ? JumpArc.LandSeconds : JumpArc.TakeOffSeconds).Within(OneFrame),
                            $"{entry.clipName} is not the length JumpArc shares the flight out by");
                    }
                    Assert.That(variants, Is.EquivalentTo(new[] { CombatVariant.Masc, CombatVariant.Femn }), id);
                }
                Assert.That(director.HasJumpClips, Is.EqualTo(resolved == 4),
                    "the director's question and the rows disagree about whether the jump is animated");
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }
    }
}
