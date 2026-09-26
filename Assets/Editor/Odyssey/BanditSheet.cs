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
    /// The contact sheets the bandit's look is chosen from (<c>docs/design/39-bandits.md</c>).
    ///
    /// <list type="bullet">
    /// <item><c>bandit-heads.png</c> — every full-head piece in Battle Royale on the two bandit
    /// bodies, male above and female below, in the pack's own paint. None of the seven is named
    /// "welder", so the owner picks from the picture rather than from a file name.</item>
    /// <item><c>bandit-vests.png</c> — the three armour vests on both bodies, the vest painted red
    /// and the body's clothing black, and two diagnostic frames per body painting the body's two
    /// clothing slots green (<c>cloth</c>) and blue (<c>cloth2</c>) so the picture says which slot
    /// is the trousers.</item>
    /// </list>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.BanditSheet.Shoot</c>. Needs a graphics
    /// device. The log carries every frame's caption and the cluster table for each body and
    /// vest.</para>
    /// </summary>
    public static class BanditSheet
    {
        [MenuItem("Odyssey/Presentation/Shoot the bandit sheets")]
        public static void ShootFromMenu() => Run(false);

        public static void Shoot() => Run(Application.isBatchMode);

        internal static readonly string[] Bodies = { "Character_ToplessMale_01", "Character_SportsBraFemale_01" };

        static readonly string[] Candidates =
        {
            "Character_SportyMale_02", "Character_SportyFemale_02", "Character_ToplessMale_01",
        };

        static readonly string[] AllBattleRoyaleBodies =
        {
            "Character_BusinessMale_01", "Character_MercenaryMale_01", "Character_MilitaryMale_01",
            "Character_RedneckMale_01", "Character_SportyMale_01", "Character_SportyMale_02",
            "Character_ToplessMale_01", "Character_GhillieSuit_01",
            "Character_70sFemale_01", "Character_GothFemale_01", "Character_MercenaryFemale_01",
            "Character_MilitaryFemale_01", "Character_SportyFemale_01", "Character_SportyFemale_02",
            "Character_SportsBraFemale_01",
        };

        internal static readonly string[] HeadPieces =
        {
            "SM_Chr_Attach_Helmet_01", "SM_Chr_Attach_Helmet_02", "SM_Chr_Attach_Helmet_03",
            "SM_Chr_Attach_Facemask_01", "SM_Chr_Attach_Facemask_02", "SM_Chr_Attach_Facemask_03",
            "SM_Chr_Attach_GasMask_01",
        };

        static readonly Rgb24 Skin = Rgb24.FromHex(0xC89274);
        static readonly Rgb24 Hair = Rgb24.FromHex(0x3B2A1E);
        static readonly Rgb24 Red = Rgb24.FromHex(0x9E1B1B);
        static readonly Rgb24 Black = Rgb24.FromHex(0x1E1E22);
        static readonly Rgb24 Green = Rgb24.FromHex(0x20C040);
        static readonly Rgb24 Blue = Rgb24.FromHex(0x2050E0);

        const int HeadSide = 192;
        const int BodyWidth = 192, BodyHeight = 384;

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? root = null;
            ColonistMaterials? materials = null;
            try
            {
                root = new GameObject("BanditSheet");
                PlayScene.BuildSheetLighting(root.transform);
                ColonistMaterials.AdoptInkFrom();
                materials = new ColonistMaterials();

                var camObject = new GameObject("camera");
                camObject.transform.SetParent(root.transform, false);
                var camera = camObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);

                var report = new StringBuilder();
                report.AppendLine("=== bandit sheets ===");

                // ---- the heads.
                var heads = new List<(string, Texture2D?)>();
                foreach (string body in Bodies)
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(body);
                    if (prefab == null) { report.AppendLine($"  body {body}: NOT FOUND"); continue; }

                    report.AppendLine($"-- {body} clusters:");
                    report.Append(CharacterSwatches.DescribeBody(prefab, 8));

                    foreach (string piece in HeadPieces)
                    {
                        GameObject? headPrefab = ScatterSheet.FindPrefab(piece);
                        if (headPrefab == null) { report.AppendLine($"  piece {piece}: NOT FOUND"); heads.Add((piece, null)); continue; }
                        heads.Add(($"{body} + {piece}",
                            Photograph(camera, root.transform, prefab, headPrefab, vest: -1,
                                materials, Black, Black, headShot: true)));
                    }
                }
                Write("bandit-heads.png", heads, HeadPieces.Length, HeadSide, HeadSide, report);

                // ---- the vests, and the two diagnostic frames.
                var vests = new List<(string, Texture2D?)>();
                foreach (string body in Bodies)
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(body);
                    if (prefab == null) continue;

                    vests.Add(($"{body} cloth=green cloth2=blue",
                        Photograph(camera, root.transform, prefab, null, -1, materials, Green, Blue, false)));

                    for (int v = 1; v <= 3; v++)
                        vests.Add(($"{body} vest {v}",
                            Photograph(camera, root.transform, prefab, null, v, materials, Black, Black, false,
                                report)));
                }
                Write("bandit-vests.png", vests, 4, BodyWidth, BodyHeight, report);

                // ---- every Battle Royale body under vest 2, for choosing who wears trousers.
                var all = new List<(string, Texture2D?)>();
                foreach (string body in AllBattleRoyaleBodies)
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(body);
                    if (prefab == null) { all.Add((body, null)); continue; }
                    all.Add((body, Photograph(camera, root.transform, prefab, null, 2, materials,
                        Black, Black, false)));
                }
                Write("bandit-bodies.png", all, 8, BodyWidth, BodyHeight, report);

                // ---- the candidates, dressed: welding helmet, each vest filled red over its whole
                //      camo box, and the topless male's camo trousers filled black the same way.
                GameObject? welder = ScatterSheet.FindPrefab("SM_Chr_Attach_Helmet_03");
                var dressed = new List<(string, Texture2D?)>();
                foreach (string body in Candidates)
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(body);
                    if (prefab == null) continue;
                    for (int v = 1; v <= 3; v++)
                        dressed.Add(($"{body} welder vest {v} (filled)",
                            Photograph(camera, root.transform, prefab, welder, v, materials,
                                Black, Black, false, report, fill: true)));
                }
                Write("bandit-dressed.png", dressed, 6, BodyWidth, BodyHeight, report);

                // ---- the game's own path: twelve bandits dealt by the book from the committed
                //      catalogue and photographed by PortraitStudio, each beside the same person
                //      in the uniform — the pane's masked portrait and who is under it.
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue != null)
                {
                    using var studio = new PortraitStudio(catalogue, materials)
                    {
                        Appearances = AppearanceBooks.For(20260924u, catalogue),
                    };
                    var people = new List<(string, Texture2D?)>();
                    for (int pawn = 1; pawn <= 12; pawn++)
                    {
                        uint roll = (uint)(pawn * 2654435761u % 900000 + 1000);
                        ColonistAppearance bandit = studio.Appearances!.For(pawn, roll, PawnOutfit.Bandit);
                        ColonistAppearance person = studio.Appearances!.For(pawn, roll, PawnOutfit.Issued);
                        people.Add(($"pawn {pawn} bandit: {bandit}", Copy(studio.For(bandit))));
                        people.Add(($"pawn {pawn} person: {person}", Copy(studio.For(person))));
                    }
                    Write("bandit-portraits.png", people, 8, PortraitStudio.Size, PortraitStudio.Size, report);
                }

                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[BanditSheet] " + e);
                exitCode = 1;
            }
            finally
            {
                materials?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// Where the camo is (<c>docs/design/42-bandits.md</c> §4a). The trousers and every vest are
        /// textured camo rather than one swatch cell, so they cannot be found by clustering; this
        /// prints the UV box of each body's leg-worn vertices outside the swatch palette, and of each
        /// vest's vertices outside the near-black corner, and counts every <i>other</i> vertex of the
        /// same mesh that falls inside that box — which is the whole question of whether a flat fill
        /// over the box repaints only the garment.
        /// </summary>
        public static void Measure()
        {
            var report = new StringBuilder("=== bandit camo boxes ===\n");
            try
            {
                foreach (string name in Bodies)
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(name);
                    if (prefab == null) { report.AppendLine($"{name}: NOT FOUND"); continue; }
                    GameObject instance = UnityEngine.Object.Instantiate(prefab);
                    try
                    {
                        SkinnedMeshRenderer? body = null;
                        foreach (var s in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            if (s.gameObject.activeSelf && s.sharedMesh != null &&
                                (body == null || s.sharedMesh.vertexCount > body.sharedMesh.vertexCount))
                                body = s;
                        if (body == null) continue;
                        report.AppendLine($"-- {name} body {body.sharedMesh.vertexCount}v");
                        BoxReport(report, "legs outside palette", body,
                            (uv, bone) => uv.x > 0.3f && IsLeg(bone));
                        BoxReport(report, "anything outside palette", body, (uv, bone) => uv.x > 0.3f);

                        bool female = name.Contains("Female");
                        for (int v = 1; v <= 3; v++)
                        {
                            SkinnedMeshRenderer? armour = Armour(instance, female, v);
                            if (armour == null) continue;
                            report.AppendLine($"-- {armour.gameObject.name} {armour.sharedMesh.vertexCount}v");
                            BoxReport(report, "vest outside dark corner", armour,
                                (uv, bone) => !(uv.x < 0.05f && uv.y < 0.05f));
                            Rect vest = Box(armour, (uv, bone) => !(uv.x < 0.05f && uv.y < 0.05f), out _);
                            report.AppendLine($"     body vertices inside this vest box: {Inside(body, vest)}");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            }
            catch (Exception e) { report.AppendLine("FAILED " + e); }
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static Rect Pad(Rect r) => Rect.MinMaxRect(r.xMin - 0.0012f, r.yMin - 0.0012f, r.xMax + 0.0012f, r.yMax + 0.0012f);

        /// <summary>
        /// A copy of a studio portrait. The studio owns its textures and <see cref="Write"/>
        /// destroys what it is handed, so the sheet takes its own.
        /// </summary>
        static Texture2D? Copy(Texture2D? shot)
        {
            if (shot == null) return null;
            var copy = new Texture2D(shot.width, shot.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(shot.GetPixels32());
            copy.Apply();
            return copy;
        }

        static AppearanceCells Copy(AppearanceCells c) => new AppearanceCells
        {
            skin = c.skin, hair = c.hair, cloth = c.cloth, cloth2 = c.cloth2,
            skinVerts = c.skinVerts, hairVerts = c.hairVerts, clothVerts = c.clothVerts,
            cloth2Verts = c.cloth2Verts, totalVerts = c.totalVerts, quality = c.quality,
        };

        static bool IsLeg(string bone) =>
            bone.StartsWith("UpperLeg", StringComparison.Ordinal) || bone.StartsWith("LowerLeg", StringComparison.Ordinal) ||
            bone.StartsWith("Hips", StringComparison.Ordinal) || bone.StartsWith("Ankle", StringComparison.Ordinal);

        static Rect Box(SkinnedMeshRenderer skin, Func<Vector2, string, bool> wanted, out int count)
        {
            Vector2[] uv = skin.sharedMesh.uv;
            BoneWeight[] w = skin.sharedMesh.boneWeights;
            Transform[] bones = skin.bones;
            float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
            count = 0;
            for (int i = 0; i < uv.Length; i++)
            {
                string bone = i < w.Length && (uint)w[i].boneIndex0 < (uint)bones.Length && bones[w[i].boneIndex0] != null
                    ? bones[w[i].boneIndex0].name : string.Empty;
                if (!wanted(uv[i], bone)) continue;
                count++;
                x0 = Mathf.Min(x0, uv[i].x); y0 = Mathf.Min(y0, uv[i].y);
                x1 = Mathf.Max(x1, uv[i].x); y1 = Mathf.Max(y1, uv[i].y);
            }
            return count == 0 ? Rect.zero : Rect.MinMaxRect(x0, y0, x1, y1);
        }

        static void BoxReport(StringBuilder report, string label, SkinnedMeshRenderer skin,
            Func<Vector2, string, bool> wanted)
        {
            Rect r = Box(skin, wanted, out int count);
            report.AppendLine($"   {label}: {count}v box ({r.xMin:F4},{r.yMin:F4})-({r.xMax:F4},{r.yMax:F4}); " +
                              $"other vertices of this mesh inside it: {Inside(skin, r) - count}");
        }

        static int Inside(SkinnedMeshRenderer skin, Rect r)
        {
            if (r.width <= 0f && r.height <= 0f) return 0;
            int n = 0;
            foreach (Vector2 t in skin.sharedMesh.uv)
                if (t.x >= r.xMin && t.x <= r.xMax && t.y >= r.yMin && t.y <= r.yMax) n++;
            return n;
        }

        /// <summary>The armour overlay a Battle Royale rig carries, by number, or null.</summary>
        internal static SkinnedMeshRenderer? Armour(GameObject instance, bool female, int vest)
        {
            string wanted = $"_{(female ? "Female" : "Male")}_Armor_0{vest}";
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.gameObject.name.EndsWith(wanted, StringComparison.OrdinalIgnoreCase))
                    return skin;
            return null;
        }

        static Texture2D? Photograph(Camera camera, Transform parent, GameObject bodyPrefab,
            GameObject? headPrefab, int vest, ColonistMaterials materials, Rgb24 cloth, Rgb24 cloth2,
            bool headShot, StringBuilder? report = null, bool fill = false)
        {
            GameObject instance = UnityEngine.Object.Instantiate(bodyPrefab, parent);
            try
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 165f, 0f);
                var animator = instance.GetComponent<Animator>();
                if (animator != null) animator.enabled = false;
                ColonistAttachments.BareTheHead(instance);

                bool female = bodyPrefab.name.Contains("Female");
                var look = new ColonistAppearance(0, Skin, Hair, cloth, cloth2);

                AppearanceCells bodyCells = CharacterSwatches.Classify(bodyPrefab, out _);
                foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!skin.gameObject.activeSelf) continue;
                    AppearanceCells cells = bodyCells;
                    if (fill)
                    {
                        // The body's camo (anything painted from outside the swatch palette),
                        // filled flat black in the second cloth slot beside what it already had.
                        Rect camo = Box(skin, (uv, bone) => uv.x > 0.3f, out int n);
                        if (n > 0)
                        {
                            cells = Copy(bodyCells);
                            cells.cloth2 = cells.cloth2.Length > 0 ? new[] { Pad(camo), cells.cloth2[0] } : new[] { Pad(camo) };
                            if (cells.quality == AppearanceQuality.None) cells.quality = AppearanceQuality.ClothOnly;
                        }
                    }
                    Material? painted = materials.For(skin.sharedMaterial, cells.Any ? cells : null, look);
                    if (painted != null) skin.sharedMaterial = painted;
                }

                if (vest > 0)
                {
                    SkinnedMeshRenderer? armour = Armour(instance, female, vest);
                    if (armour == null) report?.AppendLine($"  {bodyPrefab.name} vest {vest}: NO ARMOUR CHILD");
                    else
                    {
                        armour.gameObject.SetActive(true);
                        AppearanceCells vestCells = CharacterSwatches.ClassifyOverlay(armour, out string note);
                        if (fill)
                        {
                            // The whole vest bar its near-black corner (straps, buckles), flat red.
                            Rect box = Box(armour, (uv, bone) => !(uv.x < 0.05f && uv.y < 0.05f), out _);
                            vestCells = new AppearanceCells
                            {
                                cloth = new[] { Pad(box) },
                                quality = AppearanceQuality.ClothOnly,
                            };
                            note = $"filled box ({box.xMin:F3},{box.yMin:F3})-({box.xMax:F3},{box.yMax:F3})";
                        }
                        report?.AppendLine($"  {armour.gameObject.name}: {note}");
                        // The vest red and anything second on it black, so the picture shows the
                        // garment the way the bandit would wear it.
                        var vestLook = new ColonistAppearance(0, Skin, Hair, Red, Black);
                        Material? painted = materials.For(armour.sharedMaterial, vestCells.Any ? vestCells : null, vestLook);
                        if (painted != null) armour.sharedMaterial = painted;
                    }
                }

                Transform? head = animator != null && animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

                if (headPrefab != null && head != null)
                {
                    ColonistAttachments.MakeSlot(head, "Head", instance.layer, out MeshFilter f, out MeshRenderer r);
                    var filter = headPrefab.GetComponentInChildren<MeshFilter>(true);
                    var renderer = headPrefab.GetComponentInChildren<MeshRenderer>(true);
                    f.sharedMesh = filter != null ? filter.sharedMesh : null;
                    r.sharedMaterial = renderer != null ? renderer.sharedMaterial : null;
                    r.enabled = f.sharedMesh != null;
                }

                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;
                Bounds body = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) body.Encapsulate(renderers[i].bounds);
                float stature = body.size.y;

                int width, height;
                Vector3 anchor;
                if (headShot && head != null)
                {
                    width = height = HeadSide;
                    anchor = head.position + Vector3.up * (stature * 0.05f);
                    camera.orthographicSize = stature * 0.16f;
                }
                else
                {
                    width = BodyWidth;
                    height = BodyHeight;
                    anchor = body.center;
                    camera.orthographicSize = stature * 0.56f;
                }

                camera.transform.position = new Vector3(anchor.x, anchor.y, body.center.z - 4f);
                camera.transform.rotation = Quaternion.identity;

                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 4,
                };
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var shot = new Texture2D(width, height, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);
                return shot;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static void Write(string file, List<(string Name, Texture2D? Shot)> shots, int columns,
            int width, int height, StringBuilder report)
        {
            int lines = Math.Max(1, (shots.Count + columns - 1) / columns);
            var sheet = new Texture2D(columns * width, lines * height, TextureFormat.RGBA32, false);
            var clear = new Color32[columns * width * lines * height];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(24, 26, 30, 255);
            sheet.SetPixels32(clear);

            report.AppendLine();
            report.AppendLine($"-- {file}: {shots.Count} frames, {columns} across");
            for (int i = 0; i < shots.Count; i++)
            {
                report.AppendLine($"  {i,3} (col {i % columns}, row {i / columns})  {shots[i].Name}");
                Texture2D? shot = shots[i].Shot;
                if (shot == null) continue;
                int col = i % columns, line = i / columns;
                sheet.SetPixels32(col * width, (lines - 1 - line) * height, width, height, shot.GetPixels32());
                UnityEngine.Object.DestroyImmediate(shot);
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
