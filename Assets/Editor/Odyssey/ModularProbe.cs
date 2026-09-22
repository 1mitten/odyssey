#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Three contact sheets for the modular colonist work
    /// (<c>docs/design/29-modular-colonists.md</c>, MC8).
    ///
    /// <list type="bullet">
    /// <item><c>pool-bodies.png</c> — every body a colonist can actually be dealt, bald and
    /// clean-shaven. This is the sheet that answers "which of these is wearing a hat", which is a
    /// question for the owner's eye and not for a name test.</item>
    /// <item><c>beards.png</c> — one body wearing each beard in turn.</item>
    /// <item><c>hairs.png</c> — one body wearing each hair piece in turn.</item>
    /// </list>
    ///
    /// <para><b>The last two exist to halve a search rather than to look pretty.</b> The first
    /// whole-cast sheet drew a dark bar across every bearded face, and the placement had already
    /// been measured correct — so the next move was one picture per slot instead of more
    /// reasoning (<c>docs/bug-patterns.md</c>, the runbook for a thing that looks wrong).</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.ModularProbe.Shoot</c>. Needs a graphics
    /// device, like every other portrait.</para>
    /// </summary>
    public static class ModularProbe
    {
        [MenuItem("Odyssey/Presentation/Shoot the modular sheets")]
        public static void ShootFromMenu() => Photograph(false);

        public static void Shoot() => Photograph(Application.isBatchMode);

        const int Columns = 8;

        /// <summary>Fixed colours, so the only thing varying across a sheet is the thing under test.</summary>
        static readonly Rgb24 Skin = Rgb24.FromHex(0xE0B088);
        static readonly Rgb24 Hair = Rgb24.FromHex(0x3B2A1E);
        static readonly Rgb24 Cloth = Rgb24.FromHex(0x4A4F55);
        static readonly Rgb24 Cloth2 = Rgb24.FromHex(0x2A2E33);

        static void Photograph(bool exitWhenDone)
        {
            int exitCode = 0;
            PortraitStudio? studio = null;
            ColonistMaterials? materials = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null)
                {
                    Debug.LogError("[Modular] no catalogue.");
                    if (exitWhenDone) EditorApplication.Exit(1);
                    return;
                }

                ColonistMaterials.AdoptInkFrom();
                materials = new ColonistMaterials();
                studio = new PortraitStudio(catalogue, materials);

                var report = new StringBuilder();
                report.AppendLine("=== modular colonist sheets ===");

                List<ModuleEntry> bodies = catalogue.FindFamily(ModuleIds.ColonistBase);
                List<ModuleEntry> hairs = catalogue.FindFamily(ModuleIds.HairBase);
                List<ModuleEntry> beards = catalogue.FindFamily(ModuleIds.BeardBase);

                // ---- 1. the pool, bare.
                var pool = new List<int>();
                for (int i = 0; i < bodies.Count; i++)
                    if (bodies[i].colonistPool)
                        pool.Add(i);

                var poolShots = new List<(string Name, Texture2D? Shot)>();
                foreach (int look in pool)
                    poolShots.Add((bodies[look].prefabName,
                        studio.For(new ColonistAppearance(look, Skin, Hair, Cloth, Cloth2,
                            ColonistAppearance.NoPiece, ColonistAppearance.NoPiece))));

                Write("pool-bodies.png", poolShots, report);

                // ---- 2 and 3. one body, every piece.
                int model = ModelBody(bodies);
                report.AppendLine($"model body for the piece sheets: {bodies[model].prefabName} (look {model})");

                var beardShots = new List<(string, Texture2D?)>();
                for (int i = 0; i < beards.Count; i++)
                    beardShots.Add((beards[i].prefabName,
                        studio.For(new ColonistAppearance(model, Skin, Hair, Cloth, Cloth2,
                            ColonistAppearance.NoPiece, i))));
                Write("beards.png", beardShots, report);

                var hairShots = new List<(string, Texture2D?)>();
                for (int i = 0; i < hairs.Count; i++)
                    hairShots.Add((hairs[i].prefabName,
                        studio.For(new ColonistAppearance(model, Skin, Hair, Cloth, Cloth2,
                            i, ColonistAppearance.NoPiece))));
                Write("hairs.png", hairShots, report);

                SetupAgainstColony(catalogue, materials, report);

                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[Modular] " + e);
                exitCode = 1;
            }
            finally
            {
                studio?.Dispose();
                materials?.Dispose();
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// The setup screen against the colony, in one process
        /// (owner, 2026-09-22: <i>"the portraits on the character selection screen do not match up
        /// with what generates in game"</i>).
        ///
        /// <para>The two differ in exactly one way that is hard to see by reading: the setup page
        /// runs <b>before</b> <c>OdysseyBootstrap.BuildSession</c>, so the studio answers from
        /// whatever book its own fallback built, and the session then replaces the book and clears
        /// the cache. This reproduces both states in order and compares the appearance a candidate
        /// is dealt on each side — which is the whole claim, and is either true or it is not.</para>
        /// </summary>
        static void SetupAgainstColony(ModuleCatalogue catalogue, ColonistMaterials materials,
            StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- the setup screen against the colony");

            // 1. What the setup page sees: a studio with no book assigned, exactly as it is before
            //    a session exists. Its fallback answers.
            using var setup = new PortraitStudio(catalogue, materials);

            // 2. What the colony gives: the book BuildSession assigns, from the world's seed.
            const uint WorldSeed = 20260922u;
            using var colony = new PortraitStudio(catalogue, materials)
            {
                Appearances = AppearanceBooks.For(WorldSeed, catalogue),
            };

            int disagreed = 0;
            for (int slot = 0; slot < 3; slot++)
            {
                // The candidate's own seed and the id that slot will occupy -- the same two
                // numbers HudShell.Start hands the studio.
                uint candidate = (uint)(1_000_003 * (slot + 1) + 17);
                PawnId willBe = Odyssey.Sim.Pawns.ColonistDraw.IdForSlot(slot);

                // Ask through the public call first: PortraitStudio only builds its fallback book
                // inside For(rollSeed, id), not in the property, so reading the book before any
                // portrait has been asked for gives null. The setup page asks exactly this way.
                setup.For(candidate, willBe);

                ColonistAppearance onCard = setup.Appearances!.For(willBe.Value, candidate);
                ColonistAppearance inGame = colony.Appearances!.For(willBe.Value, candidate);

                bool same = onCard.Equals(inGame);
                if (!same) disagreed++;
                report.AppendLine($"   slot {slot} (pawn {willBe.Value}, seed {candidate}): " +
                                  (same ? "agree" : "DISAGREE"));
                report.AppendLine($"     card  {onCard}");
                report.AppendLine($"     game  {inGame}");
            }

            report.AppendLine(disagreed == 0
                ? "   the setup screen and the colony agree on all three"
                : $"   {disagreed} of 3 DISAGREE -- reproduced");
        }

        /// <summary>
        /// A bare-headed body to hang the pieces on, so a sheet of hair is a sheet of hair and not
        /// a sheet of one hat.
        /// </summary>
        static int ModelBody(List<ModuleEntry> bodies)
        {
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i].prefabName == "Character_MilitaryMale_01")
                    return i;

            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i].colonistPool)
                    return i;

            return 0;
        }

        static void Write(string file, List<(string Name, Texture2D? Shot)> shots, StringBuilder report)
        {
            int side = PortraitStudio.Size;
            int columns = Columns;
            int lines = (shots.Count + columns - 1) / columns;
            if (lines < 1) lines = 1;

            var sheet = new Texture2D(columns * side, lines * side, TextureFormat.RGBA32, false);
            var clear = new Color32[columns * side * lines * side];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(24, 26, 30, 255);
            sheet.SetPixels32(clear);

            report.AppendLine();
            report.AppendLine($"-- {file}: {shots.Count} frames, {columns} across");

            for (int i = 0; i < shots.Count; i++)
            {
                report.AppendLine($"  {i,3}  {shots[i].Name}");
                Texture2D? shot = shots[i].Shot;
                if (shot == null) continue;

                int col = i % columns, line = i / columns;
                // Rows run down the sheet, and a Texture2D's y runs up it.
                sheet.SetPixels32(col * side, (lines - 1 - line) * side, side, side, shot.GetPixels32());
            }

            sheet.Apply();
            Directory.CreateDirectory(ShotFolder.Path);
            string path = Path.GetFullPath(Path.Combine(ShotFolder.Path, file));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            report.AppendLine($"  wrote {path}");
        }
    }
}
