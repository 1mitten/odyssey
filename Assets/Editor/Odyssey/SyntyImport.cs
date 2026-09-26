#nullable enable
// Phase 0 ground tooling: imports the owner's Synty .unitypackage files headless.
// Not gameplay code. The packages themselves are licensed and never committed;
// this script only automates what the Package Manager import dialog would do.
//
// Run headless (no -quit: the method exits the editor itself):
//   Unity -batchmode -nographics -projectPath . \
//     -executeMethod Odyssey.EditorTools.SyntyImport.ImportAll \
//     -odysseyPackages "path\a.unitypackage;path\b.unitypackage" \
//     -logFile Logs/synty-import.log
//
// Package paths come from -odysseyPackages (semicolon-separated) or, failing
// that, the ODYSSEY_PACKAGES environment variable. Every pack must land under
// Assets/Synty (the 2022.3-era Synty packages do this on their own); the run
// fails if that folder is missing or empty afterwards, so a pack that installs
// elsewhere is caught rather than silently committed.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public static class SyntyImport
    {
        const string SyntyRoot = "Assets/Synty";

        public static void ImportAll()
        {
            int exitCode = 0;
            try
            {
                string[] packages = ReadPackagePaths();
                if (packages.Length == 0)
                    throw new ArgumentException(
                        "No packages given. Pass -odysseyPackages \"a.unitypackage;b.unitypackage\" or set ODYSSEY_PACKAGES.");

                foreach (string package in packages)
                {
                    if (!File.Exists(package))
                        throw new FileNotFoundException("Package not found", package);
                    Debug.Log($"[SyntyImport] importing {package}");
                    ImportPackageNow(package);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (!AssetDatabase.IsValidFolder(SyntyRoot))
                    throw new InvalidOperationException(
                        $"{SyntyRoot} does not exist after import; the pack(s) installed somewhere else.");
                int assetCount = AssetDatabase.FindAssets("", new[] { SyntyRoot }).Length;
                Debug.Log($"[SyntyImport] done: {assetCount} assets under {SyntyRoot} " +
                          $"({string.Join(", ", AssetDatabase.GetSubFolders(SyntyRoot).Select(Path.GetFileName))})");
                if (assetCount == 0) exitCode = 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SyntyImport] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
        }

        static void ImportPackageNow(string package)
        {
            // AssetDatabase.ImportPackage(path, interactive: false) only queues the import when
            // called from -executeMethod, so the editor can exit before anything is extracted
            // (observed on 6000.x: the run "succeeds" with an empty project). The internal
            // ImportPackageImmediately — what the -importPackage command line flag calls — is
            // synchronous. Fall back to five separate -importPackage runs if it ever disappears.
            MethodInfo? immediate = typeof(AssetDatabase).GetMethod("ImportPackageImmediately",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (immediate == null)
                throw new MissingMethodException(
                    "AssetDatabase.ImportPackageImmediately no longer exists; import each package " +
                    "with 'Unity -batchmode -importPackage <path> -quit' instead.");
            immediate.Invoke(null, new object[] { package });
        }

        /// <summary>
        /// Runs URP's Built-in → URP converter set over the packs under Assets/Synty, headless. The 2022.3-era Synty
        /// packs are Shader Graph native, but a handful of materials (Standard, legacy particle
        /// shaders) still target the built-in pipeline and render magenta under URP without this.
        /// Run via: scripts/unity.sh exec Odyssey.EditorTools.SyntyImport.UpgradeBuiltInMaterials
        /// </summary>
        public static void UpgradeBuiltInMaterials()
        {
            try
            {
                // Not Converters.RunInBatchMode: on URP 17.3.0 it throws MissingMethodException
                // while enumerating the container (Base2DMaterialUpgrader has no default
                // constructor) before converting anything, with or without a converter filter.
                // The underlying material-upgrader API is public and batchmode-aware, so use it.
                var upgraders = UnityEditor.Rendering.MaterialUpgrader.FetchAllUpgradersForPipeline(
                    typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset));
                // The packs only. UpgradeProjectFolder walks all of Assets/, and
                // Resources/OdysseyKeepAlive/Standard.mat is committed precisely to keep the built-in
                // Standard shader in a player build: the project-wide pass rewrote it to URP Lit on a
                // fresh machine's first open (2026-09-26, docs/lessons.md).
                var guids = AssetDatabase.FindAssets("t:Material", new[] { SyntyRoot });
                foreach (var guid in guids)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (material == null) continue;
                    var before = material.shader;
                    UnityEditor.Rendering.MaterialUpgrader.Upgrade(material, upgraders,
                        UnityEditor.Rendering.MaterialUpgrader.UpgradeFlags.None);
                    if (material.shader != before) EditorUtility.SetDirty(material);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[SyntyImport] Built-in → URP converter run finished over {guids.Length} materials under {SyntyRoot}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SyntyImport] converter failed: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static string[] ReadPackagePaths()
        {
            string? joined = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "-odysseyPackages", StringComparison.OrdinalIgnoreCase))
                    joined = args[i + 1];
            joined ??= Environment.GetEnvironmentVariable("ODYSSEY_PACKAGES");
            return string.IsNullOrWhiteSpace(joined)
                ? Array.Empty<string>()
                : joined.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }
    }
}
