#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Report the whole cast, row by row: can a live animated figure be built for this face, and
    /// can a portrait be photographed of it.
    ///
    /// <para>Written for the report that "some colonists are not animated and their profile
    /// picture is black". Both halves of that are per-face facts, and both are silent — a face
    /// with no gait clip is dropped by <c>PawnFigureDirector.LooksFrom</c> and the pawn falls
    /// through to the baked instanced form, which stands still; a portrait that renders nothing
    /// leaves the avatar box its tile colour. Neither logs anything today.</para>
    ///
    /// <para><c>scripts/unity.sh exec Odyssey.EditorTools.CastProbe.Run</c> for the catalogue half
    /// (no graphics device needed), <c>scripts/unity.sh shot Odyssey.EditorTools.CastProbe.Shoot</c>
    /// for the portraits.</para>
    /// </summary>
    public static class CastProbe
    {
        [MenuItem("Odyssey/Presentation/Probe the cast")]
        public static void RunFromMenu() => Report(false);

        public static void Run() => Report(Application.isBatchMode);

        static void Report(bool exitWhenDone)
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
            if (catalogue == null)
            {
                Debug.LogError("[Cast] no catalogue at " + PlayScene.CataloguePath);
                if (exitWhenDone) EditorApplication.Exit(1);
                return;
            }

            List<ModuleEntry> rows = catalogue.FindFamily(ModuleIds.ColonistBase);
            var report = new StringBuilder();
            report.AppendLine($"=== cast: {rows.Count} colonist rows ===");
            report.AppendLine("idx  prefab                                        gaits  skins human pose qual");

            int noPrefab = 0, noGaits = 0, notHuman = 0, noSkins = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                ModuleEntry row = rows[i];
                string name = string.IsNullOrEmpty(row.prefabName) ? "(none)" : row.prefabName;

                if (row.prefab == null)
                {
                    noPrefab++;
                    report.AppendLine($"{i,3}  {name,-44}  NO PREFAB");
                    continue;
                }

                int live = 0;
                for (int g = 0; g < row.locomotion.Count; g++)
                    if (row.locomotion[g].clip != null) live++;
                if (live == 0) noGaits++;

                var skins = row.prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) noSkins++;

                var animator = row.prefab.GetComponent<Animator>();
                bool human = animator != null && animator.avatar != null && animator.avatar.isHuman;
                if (!human) notHuman++;

                report.AppendLine(
                    $"{i,3}  {name,-44}  {live,3}/{row.locomotion.Count,-3}  {skins.Length,3}   " +
                    $"{(human ? "yes" : "NO ")}  {(row.poseClip != null ? "yes" : "NO ")}  " +
                    $"{row.appearance.quality}");
            }

            report.AppendLine();
            report.AppendLine($"no prefab {noPrefab}, no usable gait {noGaits}, " +
                              $"no skinned mesh {noSkins}, not humanoid {notHuman}");
            Debug.Log(report.ToString());

            if (exitWhenDone) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Photograph every face through the real <see cref="PortraitStudio"/> and say which ones
        /// came back empty. Writes <c>Logs/cast-portraits.png</c> as the contact sheet.
        /// </summary>
        [MenuItem("Odyssey/Presentation/Shoot the whole cast")]
        public static void ShootFromMenu() => Photograph(false);

        public static void Shoot() => Photograph(Application.isBatchMode);

        const int Columns = 10;

        static void Photograph(bool exitWhenDone)
        {
            int exitCode = 0;
            PortraitStudio? studio = null;
            ColonistMaterials? materials = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                List<ModuleEntry> rows = catalogue == null
                    ? new List<ModuleEntry>()
                    : catalogue.FindFamily(ModuleIds.ColonistBase);

                if (rows.Count == 0)
                {
                    Debug.LogError("[Cast] no colonist rows; nothing to photograph.");
                    if (exitWhenDone) EditorApplication.Exit(1);
                    return;
                }

                ColonistMaterials.AdoptInkFrom();
                materials = new ColonistMaterials();
                studio = new PortraitStudio(catalogue, materials);

                var report = new StringBuilder();
                report.AppendLine($"=== portraits: {rows.Count} faces ===");
                report.AppendLine("idx  prefab                                        result  opaque%  meanLuma");

                int side = PortraitStudio.Size;
                int columns = Columns;
                int lines = (rows.Count + columns - 1) / columns;
                var sheet = new Texture2D(columns * side, lines * side, TextureFormat.RGBA32, false);
                var clear = new Color32[columns * side * lines * side];
                for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(24, 26, 30, 255);
                sheet.SetPixels32(clear);

                int empty = 0, missing = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    // Look i, with fixed colours, so the only thing varying down the sheet is
                    // which body it is.
                    var appearance = new ColonistAppearance(
                        i,
                        Rgb24.FromHex(0xE0B088), Rgb24.FromHex(0x3B2A1E),
                        Rgb24.FromHex(0x4A4F55), Rgb24.FromHex(0x2A2E33));

                    Texture2D? shot = studio.For(appearance);
                    string name = string.IsNullOrEmpty(rows[i].prefabName) ? "(none)" : rows[i].prefabName;

                    if (shot == null)
                    {
                        missing++;
                        report.AppendLine($"{i,3}  {name,-44}  NULL");
                        continue;
                    }

                    Color32[] pixels = shot.GetPixels32();
                    long opaque = 0;
                    double luma = 0;
                    for (int p = 0; p < pixels.Length; p++)
                    {
                        if (pixels[p].a > 8)
                        {
                            opaque++;
                            luma += pixels[p].r * 0.299 + pixels[p].g * 0.587 + pixels[p].b * 0.114;
                        }
                    }

                    double opaquePercent = 100.0 * opaque / pixels.Length;
                    double meanLuma = opaque == 0 ? 0 : luma / opaque;
                    if (opaquePercent < 2.0) empty++;

                    report.AppendLine(
                        $"{i,3}  {name,-44}  {(opaquePercent < 2.0 ? "EMPTY " : "ok    ")}  " +
                        $"{opaquePercent,7:F1}  {meanLuma,8:F1}");

                    int col = i % columns, line = i / columns;
                    // Rows run down the sheet, and a Texture2D's y runs up it.
                    sheet.SetPixels32(col * side, (lines - 1 - line) * side, side, side, pixels);
                }

                sheet.Apply();
                Directory.CreateDirectory(ShotFolder.Path);
                string path = Path.GetFullPath(Path.Combine(ShotFolder.Path, "cast-portraits.png"));
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);

                report.AppendLine();
                report.AppendLine($"null {missing}, empty {empty}, wrote {path}");
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[Cast] " + e);
                exitCode = 1;
            }
            finally
            {
                studio?.Dispose();
                materials?.Dispose();
            }

            if (exitWhenDone) EditorApplication.Exit(exitCode);
        }
    }
}
