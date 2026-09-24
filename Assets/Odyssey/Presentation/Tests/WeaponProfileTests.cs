#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The committed weapon profiles (<c>WeaponProfiles.g.cs</c>) are the weapon meshes' own
    /// (design 33 §9c). The sheath fit reads the table because a player cannot read the meshes;
    /// the editor can, so this measures each mesh again and fails when the table has drifted from
    /// the art — rebake it with <c>scripts/unity.sh exec Odyssey.EditorTools.WeaponProfileBake.Run</c>.
    /// </summary>
    public class WeaponProfileTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        static readonly string[] Rows =
        {
            ModuleIds.ItemBat, ModuleIds.ItemCrowbar, ModuleIds.ItemMachete, ModuleIds.ItemArcBlade,
        };

        [Test]
        public void EveryWeaponMeshHasItsMeasuredProfile()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            int meshes = 0;
            foreach (string id in Rows)
            {
                GameObject? prefab = catalogue!.Find(id)?.prefab;
                if (prefab == null) continue;
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true))
                {
                    Mesh? mesh = filter.sharedMesh;
                    if (mesh == null) continue;
                    meshes++;
                    Assert.That(WeaponProfiles.TryGet(mesh.name, out WeaponProfile? committed), Is.True,
                        $"{id}: no profile for {mesh.name}; rebake the table");
                    WeaponProfile? measured = WeaponProfile.Measure(mesh.vertices, mesh.triangles);
                    Assert.That(measured, Is.Not.Null, mesh.name);
                    Assert.That(measured!.Difference(committed!), Is.LessThan(1e-4f),
                        $"{mesh.name}: the committed profile no longer matches the mesh; rebake the table");
                }
            }
            if (meshes == 0) Assert.Ignore("no weapon art here");
        }

        /// <summary>
        /// A profile is tighter than the bounds where the weapon is: the bat's handle is thinner
        /// than its barrel, which is the whole reason the table exists.
        /// </summary>
        [Test]
        public void ABatsHandleIsThinnerThanItsBarrel()
        {
            if (!WeaponProfiles.TryGet("SM_Wep_Bat_01", out WeaponProfile? bat)) Assert.Ignore("no bat in the table");
            float Width(int slice) => bat!.Extents[slice * 4 + 1] - bat.Extents[slice * 4];
            float thinnest = float.MaxValue, thickest = 0f;
            for (int s = 0; s < WeaponProfile.Slices; s++)
            {
                thinnest = Mathf.Min(thinnest, Width(s));
                thickest = Mathf.Max(thickest, Width(s));
            }
            Assert.That(thinnest, Is.LessThan(0.7f * thickest));
        }
    }
}
