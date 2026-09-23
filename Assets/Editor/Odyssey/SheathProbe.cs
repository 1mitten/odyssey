#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Odyssey.Hud;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// The four weapons sheathed on real colonist bodies, measured and photographed (design 33 §9c).
    ///
    /// <para><b>Written for the owner's report</b> (playtest 2026-09-23): <i>"Baseball bat wasn't
    /// close enough to hips/waist when not drawn. Same goes for machete."</i> Every number in the
    /// first hip fit was invented and none had been seen. This builds the figures through the real
    /// <see cref="PawnFigureDirector"/>, at peace in the idle, and prints
    /// <see cref="SheathGauge"/>'s reading for every body in the colonist pool with every weapon;
    /// then photographs a few bodies front and side, one contact sheet per weapon, into
    /// <c>docs/reference/screenshots/2026-09-23-sheathed-&lt;weapon&gt;[-&lt;tag&gt;].png</c> — rows
    /// are the bodies in <see cref="Photographed"/>'s order, the front on the left and the figure's
    /// left side on the right. <c>ODYSSEY_SHEATH_TAG</c> names a set (<c>before</c>, say).</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.SheathProbe.Shoot</c> — it needs a
    /// graphics device for the pictures.</para>
    /// </summary>
    public static class SheathProbe
    {
        /// <summary>The bodies photographed: masculine, feminine, and two builds unlike either.</summary>
        public static readonly string[] Photographed =
        {
            "SM_Gen_Chr_Street_Male_01",
            "SM_Gen_Chr_Street_Female_01",
            "Character_MilitaryMale_01",
            "Character_70sFemale_01",
        };

        public static readonly (int Def, string Name)[] Weapons =
        {
            (ItemHandle.Bat, "bat"),
            (ItemHandle.Crowbar, "crowbar"),
            (ItemHandle.Machete, "machete"),
            (ItemHandle.ArcBlade, "arcblade"),
        };

        const int TileWidth = 360, TileHeight = 480;

        [MenuItem("Odyssey/Presentation/Shoot sheathed weapons")]
        public static void ShootFromMenu() => Run(false);

        public static void Shoot() => Run(Application.isBatchMode);

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? root = null;
            PawnFigureDirector? director = null;
            var sheets = new Texture2D?[Weapons.Length];
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null) throw new InvalidOperationException("no catalogue");

                root = new GameObject("SheathProbe");
                PlayScene.BuildSheetLighting(root.transform);
                var figures = new GameObject("figures");
                figures.transform.SetParent(root.transform, false);
                director = new PawnFigureDirector(catalogue, figures.transform, 0);
                if (!director.CanDrawColonists) throw new InvalidOperationException("no colonist art here");

                List<ModuleEntry> rows = catalogue.FindFamily(ModuleIds.ColonistBase);
                string tag = Environment.GetEnvironmentVariable("ODYSSEY_SHEATH_TAG") ?? "";
                var report = new StringBuilder();
                report.AppendLine($"[Sheath] tilt {PawnFigureDirector.SheathTiltDegrees} splay {PawnFigureDirector.SheathSplayDegrees} " +
                                  $"hang {PawnFigureDirector.SheathHangFraction} tag '{tag}'");

                for (int w = 0; w < Weapons.Length; w++)
                {
                    sheets[w] = new Texture2D(TileWidth * 2, TileHeight * Photographed.Length, TextureFormat.RGB24, false);
                }

                var cameraObject = new GameObject("SheathCamera");
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.6f, 0.66f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

                int pawnId = 0;
                for (int look = 0; look < rows.Count; look++)
                {
                    ModuleEntry row = rows[look];
                    int photo = Array.IndexOf(Photographed, row.prefabName);
                    if (row.prefab == null || (!row.colonistPool && photo < 0)) continue;
                    pawnId++;
                    var id = new PawnId(pawnId);
                    director.Appearances.Override(pawnId, new ColonistAppearance(look,
                        Rgb24.FromHex(0xE0B088), Rgb24.FromHex(0x3B2A1E), Rgb24.FromHex(0x4A4F55), Rgb24.FromHex(0x2A2E33)));

                    for (int w = 0; w < Weapons.Length; w++)
                    {
                        WorldSnapshot frame = Frame(100 + look, id, Weapons[w].Def);
                        for (int i = 0; i < 10; i++)
                        {
                            director.Sync(frame, 0, new SliceSettings(), 0f, 1, 1f / 60f);
                            director.Evaluate(1f / 60f);
                        }

                        if (!director.TryMeasureSheath(id, out SheathGap gap))
                        {
                            report.AppendLine($"  {row.prefabName,-34} {Weapons[w].Name,-8} NO WEAPON AT THE HIP");
                            continue;
                        }
                        report.AppendLine($"  {row.prefabName,-34} {Weapons[w].Name,-8} {gap}");

                        if (photo < 0) continue;
                        Transform prop = director.WeaponOf(id)!;
                        Transform body = FigureRoot(prop, figures.transform);
                        float height = gap.Height;
                        Vector3 focus = body.position + Vector3.up * (0.42f * height);
                        int y = (Photographed.Length - 1 - photo) * TileHeight;
                        Tile(camera, focus, -body.forward, 0.3f * height, sheets[w]!, 0, y);
                        Tile(camera, focus, body.right, 0.3f * height, sheets[w]!, TileWidth, y);
                    }
                }

                string folder = Path.GetFullPath("docs/reference/screenshots");
                Directory.CreateDirectory(folder);
                for (int w = 0; w < Weapons.Length; w++)
                {
                    sheets[w]!.Apply();
                    string name = $"2026-09-23-sheathed-{Weapons[w].Name}{(tag.Length > 0 ? "-" + tag : "")}.png";
                    File.WriteAllBytes(Path.Combine(folder, name), sheets[w]!.EncodeToPNG());
                    report.AppendLine($"[Sheath] wrote {name}: rows {string.Join(", ", Photographed)}; front | left side");
                }
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[Sheath] " + e);
                exitCode = 1;
            }
            finally
            {
                director?.Dispose();
                for (int i = 0; i < sheets.Length; i++)
                    if (sheets[i] != null) UnityEngine.Object.DestroyImmediate(sheets[i]);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// One colonist at peace holding one weapon. The snapshot's writing side is the
        /// simulation's and internal to it, so an editor tool reaches it by reflection rather than
        /// running a colony to put a weapon in one hand.
        /// </summary>
        static WorldSnapshot Frame(int tick, PawnId id, int weapon)
        {
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var snapshot = new WorldSnapshot();
            typeof(WorldSnapshot).GetMethod("BeginWrite", Any)!
                .Invoke(snapshot, new object[] { tick, new GridSize(12, 12, 4), 0, 1, 0u });
            typeof(WorldSnapshot).GetMethod("AddPawn", Any)!
                .Invoke(snapshot, new object[] { new PawnView(id, new CellRef(3, 3, 0), 800, 800, 600, JobHandle.Wait, flags: PawnFlags.Person) });
            typeof(WorldSnapshot).GetMethod("AddPawnAspect", Any)!
                .Invoke(snapshot, new object[] { new PawnAspect(id, CombatAspectNames.WeaponKey, weapon) });
            return snapshot;
        }

        static Transform FigureRoot(Transform part, Transform parent)
        {
            Transform at = part;
            while (at.parent != null && at.parent != parent) at = at.parent;
            return at;
        }

        /// <summary>One orthographic view along <paramref name="view"/>, into the sheet at (x, y).</summary>
        static void Tile(Camera camera, Vector3 focus, Vector3 view, float halfHeight, Texture2D sheet, int x, int y)
        {
            camera.orthographicSize = halfHeight;
            camera.aspect = TileWidth / (float)TileHeight;
            camera.transform.SetPositionAndRotation(focus - view.normalized * 10f, Quaternion.LookRotation(view, Vector3.up));
            var target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), x, y);
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
