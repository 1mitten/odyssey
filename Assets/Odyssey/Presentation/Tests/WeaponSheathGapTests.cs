#nullable enable
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEditor;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// A sheathed weapon sits against the hip (design 33 §9c; owner, playtest 2026-09-23: <i>"Baseball
    /// bat wasn't close enough to hips/waist when not drawn. Same goes for machete."</i>).
    ///
    /// <para>Measured on the drawn meshes by <see cref="SheathGauge"/>, not read off the fit's own
    /// numbers: every sampled point of the weapon's surface against the posed skin, the arms apart.
    /// Four bodies — masculine, feminine, and two builds unlike either — with each of the four
    /// weapons, at two instants of the idle. The first hip fit (design 33 §8b) put the nearest point
    /// 0.9–7.4 cm off these four bodies (up to 10.9 cm across the cast) and leaned every weapon
    /// 25.7° from plumb; this fails on those numbers, seen to on 2026-09-23.</para>
    ///
    /// <para>Needs the colonist art and the weapons, and ignores itself where they did not resolve.</para>
    /// </summary>
    public class WeaponSheathGapTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>The bodies: masculine, feminine, a military build with pouches, and a dress.</summary>
        static readonly string[] Bodies =
        {
            "SM_Gen_Chr_Street_Male_01",
            "SM_Gen_Chr_Street_Female_01",
            "Character_MilitaryMale_01",
            "Character_70sFemale_01",
        };

        static readonly (int Def, string Name)[] Weapons =
        {
            (ItemHandle.Bat, "bat"),
            (ItemHandle.Crowbar, "crowbar"),
            (ItemHandle.Machete, "machete"),
            (ItemHandle.ArcBlade, "arcblade"),
        };

        /// <summary>The nearest point of the weapon to the body: about one to three centimetres.</summary>
        public const float NearestGap = 0.008f, FurthestGap = 0.03f;

        /// <summary>
        /// The furthest for a weapon longer than the leg, which is lifted clear of the floor and so
        /// hangs its guard at the waist, beside the swinging elbow: measured 3.8–4.2 cm on the
        /// military build over its idle (design 33 §9c).
        /// </summary>
        public const float FurthestLifted = 0.045f;

        /// <summary>Roughly vertical: the long axis within this of straight down, in degrees.</summary>
        public const float MostLean = 15f;

        /// <summary>
        /// The handle at the belt: the top of the weapon above the pelvis bone, and no higher above
        /// it than this fraction of the figure's height.
        /// </summary>
        public const float HighestTop = 0.2f;

        /// <summary>
        /// A weapon hung higher than the belt allows is excused only if its lowest point is within
        /// this fraction of the figure's height of the floor: lifted just clear of it, as the arc
        /// blade — longer than the leg — has to be.
        /// </summary>
        public const float LiftedBottom = 0.04f;

        [Test]
        public void EverySheathedWeaponSitsAgainstTheHip()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            Assume.That(catalogue, Is.Not.Null, "the module catalogue asset is committed");
            foreach (var weapon in Weapons)
                if (catalogue!.Find(ModuleIds.Item(weapon.Def)!)?.prefab == null) Assert.Ignore($"no {weapon.Name} art here");

            var parent = new GameObject("figures");
            PawnFigureDirector? director = null;
            try
            {
                director = new PawnFigureDirector(catalogue, parent.transform, 0);
                if (!director.CanDrawColonists) Assert.Ignore("no colonist art here");

                List<ModuleEntry> rows = catalogue!.FindFamily(ModuleIds.ColonistBase);
                var table = new StringBuilder();
                var failures = new List<string>();
                int measured = 0;
                for (int b = 0; b < Bodies.Length; b++)
                {
                    int look = rows.FindIndex(row => row.prefabName == Bodies[b] && row.prefab != null);
                    if (look < 0)
                    {
                        failures.Add($"{Bodies[b]}: not a resolvable colonist row");
                        continue;
                    }
                    var id = new PawnId(b + 1);
                    director.Appearances.Override(id.Value, new ColonistAppearance(look,
                        Rgb24.FromHex(0xE0B088), Rgb24.FromHex(0x3B2A1E), Rgb24.FromHex(0x4A4F55), Rgb24.FromHex(0x2A2E33)));

                    foreach (var weapon in Weapons)
                    {
                        WorldSnapshot frame = Frame(100 + b, id, weapon.Def);
                        // Two instants of the idle, a second apart: it breathes, and the sheath does not follow the thigh.
                        foreach (int frames in new[] { 10, 60 })
                        {
                            for (int i = 0; i < frames; i++)
                            {
                                director.Sync(frame, 0, new SliceSettings(), 0f, 1, 1f / 60f);
                                director.Evaluate(1f / 60f);
                            }
                            string label = $"{Bodies[b],-28} {weapon.Name,-8} +{frames,2}f";
                            if (!director.TryMeasureSheath(id, out SheathGap gap))
                            {
                                failures.Add($"{label}: no weapon at the hip");
                                continue;
                            }
                            measured++;
                            table.AppendLine($"{label} {gap}");
                            if (gap.Inside > 0)
                                failures.Add($"{label}: {gap.Inside} points inside the body, {gap.Depth * 100f:F1} cm deep");
                            bool longerThanTheLeg = gap.BottomAboveFloor < LiftedBottom * gap.Height;
                            float furthest = longerThanTheLeg ? FurthestLifted : FurthestGap;
                            if (gap.Gap < NearestGap || gap.Gap > furthest)
                                failures.Add($"{label}: nearest point {gap.Gap * 100f:F1} cm off the body, not {NearestGap * 100f:F1}–{furthest * 100f:F1} cm");
                            if (gap.Lean > MostLean)
                                failures.Add($"{label}: leaning {gap.Lean:F1} deg from straight down");
                            if (gap.BottomAboveFloor < 0f)
                                failures.Add($"{label}: {-gap.BottomAboveFloor * 100f:F1} cm through the floor");
                            // At the belt — or, for a weapon longer than the leg, no higher than it
                            // must be to clear the floor.
                            if (gap.TopAbovePelvis < 0f || (gap.TopAbovePelvis > HighestTop * gap.Height && !longerThanTheLeg))
                                failures.Add($"{label}: its top {gap.TopAbovePelvis * 100f:F1} cm above the pelvis, not at the belt");
                        }
                    }
                }

                TestContext.WriteLine(table.ToString());
                Debug.Log("[SheathGap]\n" + table);
                Assert.That(measured, Is.EqualTo(Bodies.Length * Weapons.Length * 2), string.Join("\n", failures));
                Assert.That(failures, Is.Empty, "\n" + string.Join("\n", failures) + "\n\n" + table);
            }
            finally
            {
                director?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        static WorldSnapshot Frame(int tick, PawnId id, int weapon)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(tick, new GridSize(12, 12, 4), 0);
            snapshot.AddPawn(new PawnView(id, new CellRef(3, 3, 0), 800, 800, 600, JobHandle.Wait, flags: PawnFlags.Person));
            snapshot.AddPawnAspect(new PawnAspect(id, CombatAspectNames.WeaponKey, weapon));
            return snapshot;
        }
    }
}
