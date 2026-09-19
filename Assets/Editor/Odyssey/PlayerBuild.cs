#nullable enable
// Builds a standalone player, headless.
//
// Editor menu:  Odyssey > Build > Windows 64 player
// Headless:     scripts/unity.sh build
//               scripts/unity.sh exec Odyssey.EditorTools.PlayerBuild.Windows64
//
// **Why this exists at all, given two green test tiers.** The tiers compile the Editor and Test
// assemblies and run against them in the editor's own domain. A player build is the only thing
// that compiles the *player* assembly set — no UnityEditor, stripping on, IL2CPP or Mono as
// configured — and the only thing that proves the scene, the catalogue and the shaders survive
// being packaged. A `using UnityEditor` that slipped into Presentation passes every test in this
// repository and fails here, which is exactly the class of fault this is for.
//
// It writes to Build/, which is gitignored: a player carries the licensed Synty content baked in,
// so the one thing this output must never be is committed.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public static class PlayerBuild
    {
        /// <summary>Where a player goes. Gitignored — see the note at the top of this file.</summary>
        public const string OutputFolder = "Build/Win64";

        [MenuItem("Odyssey/Build/Windows 64 player")]
        public static void Windows64() => Run(BuildTarget.StandaloneWindows64, "Odyssey.exe");

        static void Run(BuildTarget target, string executable)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            // A build with no scenes succeeds and produces a player that opens on nothing, which
            // is a worse outcome than a failure because it looks like it worked. The project
            // generates its one scene (PlayScene.Build), so an empty list here means the build
            // settings were never populated rather than that somebody meant it.
            if (scenes.Length == 0)
            {
                Fail("no scenes are enabled in the build settings; run PlayScene.Build first");
                return;
            }

            // **A scene listed in the build settings that is not on disk.** This is not a
            // hypothetical: the settings still carried the Unity template's SampleScene.unity
            // until 2026-09-19, months after the project's one scene was generated as Play.unity,
            // and nothing had ever noticed because nothing had ever built a player. Unity's own
            // message for it is "is an incorrect path for a scene file", which sends you looking
            // at the path format rather than at the fact that the file is not there.
            string[] missing = scenes.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0)
            {
                Fail("the build settings list scene(s) that do not exist: "
                     + string.Join(", ", missing)
                     + ". Run PlayScene.Build, which registers the scene it generates.");
                return;
            }

            // **A shader this game finds at runtime, not in the build.** The editor has every
            // shader and every variant always, so nothing short of a player build can notice —
            // and the symptom is not an error, it is an empty world with the characters still in
            // it. Refused rather than fixed silently: the list is a committed project setting, so
            // a build that quietly edited it would leave the next person a diff they did not make.
            string[] stripped = ShaderInclusion.Missing();
            if (stripped.Length > 0)
            {
                Fail("these shaders are found at runtime but are not in the always-included list, "
                     + "so the player would draw nothing that uses them: "
                     + string.Join(", ", stripped)
                     + ". Run Odyssey.EditorTools.ShaderInclusion.Apply and commit the change.");
                return;
            }

            string root = Path.GetDirectoryName(Application.dataPath)!;
            string output = Path.Combine(root, OutputFolder, executable);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);

            Debug.Log($"PlayerBuild: {scenes.Length} scene(s) -> {output}");
            foreach (string scene in scenes) Debug.Log($"PlayerBuild:   {scene}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                // Development, deliberately. This is a check that the player assemblies compile
                // and the scene packages, run from a terminal by somebody who wants the answer in
                // under a few minutes — not a shipping artefact. A release build additionally
                // strips and (on IL2CPP) transpiles, which is a different and much longer
                // question worth asking on its own.
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"{summary.result} after {summary.totalTime}, {summary.totalErrors} error(s)");
                return;
            }

            Debug.Log(
                $"PlayerBuild: succeeded in {summary.totalTime}, " +
                $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalWarnings} warning(s)");

            // Exit code 0 explicitly, so the shell wrapper can tell a finished build from a batch
            // run that simply stopped. `unity.sh` already knows that a Unity batch run can end
            // without exiting; this is the other half of that bargain.
            EditorApplication.Exit(0);
        }

        static void Fail(string why)
        {
            Debug.LogError("PlayerBuild: " + why);
            EditorApplication.Exit(1);
        }
    }
}
