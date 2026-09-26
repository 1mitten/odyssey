#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Can a colonist's face carry an expression at the sizes the game draws one?
    /// (<c>docs/research/e-15-facial-emotion-and-talking.md</c>).
    ///
    /// <para>The rig has two facial bones that move geometry — <c>Eyes</c> and <c>Eyebrows</c>, one
    /// of each, not a left and a right — and no mouth at all. These sheets pose those two bones
    /// into a handful of candidate expressions and photograph them where a face is actually seen:
    /// the portrait at its cached 128 px and at the 60 px the roster shows it, and the play camera
    /// (40° field, 48° pitch, figures drawn at 1.4) at its closest zoom and at twice that, cropped
    /// and enlarged with no filtering so the picture shows the real pixel budget.</para>
    ///
    /// <para>A second sheet puts every hair piece on a male and a female with the brows neutral and
    /// raised, because a fringe over the brows hides the one feature that moves.</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.FaceSheet.Shoot</c>. Needs a graphics
    /// device. The log carries every frame's caption and the bone census of each body.</para>
    /// </summary>
    public static class FaceSheet
    {
        [MenuItem("Odyssey/Presentation/Shoot the face sheets")]
        public static void ShootFromMenu() => Run(false);

        public static void Shoot() => Run(Application.isBatchMode);

        static readonly string[] Bodies =
        {
            "SM_Gen_Chr_Jumpsuit_Male_01", "SM_Gen_Chr_Jumpsuit_Female_01",
            "Character_SportyMale_01", "Character_SportyFemale_01",
        };

        /// <summary>
        /// A named pose: the six expressions from the table the game itself reads
        /// (<see cref="FacePose.Of"/>), and a blink at the depth the game closes one to.
        /// </summary>
        readonly struct Expression
        {
            public Expression(string name, FacePose pose) { Name = name; Pose = pose; }

            public readonly string Name;
            public readonly FacePose Pose;
        }

        static readonly Expression[] Expressions = BuildExpressions();

        static Expression[] BuildExpressions()
        {
            var all = (FaceExpression[])Enum.GetValues(typeof(FaceExpression));
            var list = new Expression[all.Length + 1];
            for (int i = 0; i < all.Length; i++) list[i] = new Expression(all[i].ToString(), FacePose.Of(all[i]));
            list[all.Length] = new Expression("blink", new FacePose(0f, 0f, FaceMotion.BlinkShutOpen, 1f));
            return list;
        }

        static readonly Rgb24 Skin = Rgb24.FromHex(0xC89274);
        static readonly Rgb24 Hair = Rgb24.FromHex(0x3B2A1E);
        static readonly Rgb24 Suit = Rgb24.FromHex(0xE8EDF6);
        static readonly Rgb24 Trim = Rgb24.FromHex(0xA8B2C2);

        const int Cell = 256;
        const float FigureScale = 1.4f;
        const float FieldOfView = 40f;
        const float Pitch = 48f;

        /// <summary>A play-camera framing: the frame it is a crop of, and how close the camera is.</summary>
        readonly struct PlayView
        {
            public PlayView(string file, int width, int height, float distance, int crop)
            {
                File = file; Width = width; Height = height; Distance = distance; Crop = crop;
            }

            public readonly string File;
            public readonly int Width, Height, Crop;
            public readonly float Distance;
        }

        static readonly PlayView[] PlayViews =
        {
            new PlayView("faces-play-1080p-10m.png", 1920, 1080, 10f, 64),
            new PlayView("faces-play-1080p-20m.png", 1920, 1080, 20f, 64),
            new PlayView("faces-play-2160p-10m.png", 3840, 2160, 10f, 128),
        };

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? root = null;
            ColonistMaterials? materials = null;
            try
            {
                root = new GameObject("FaceSheet");
                PlayScene.BuildSheetLighting(root.transform);
                ColonistMaterials.AdoptInkFrom();
                materials = new ColonistMaterials();
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var attachments = new ColonistAttachments(catalogue);

                var camObject = new GameObject("camera");
                camObject.transform.SetParent(root.transform, false);
                var camera = camObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.36f, 0.45f, 0.30f, 1f);

                var report = new StringBuilder("=== face sheets ===\n");

                var portrait = new List<(string, Texture2D?)>();
                var roster = new List<(string, Texture2D?)>();
                var play = new List<(string, Texture2D?)>[PlayViews.Length];
                for (int v = 0; v < play.Length; v++) play[v] = new List<(string, Texture2D?)>();

                foreach (string body in Bodies)
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(body);
                    if (prefab == null) { report.AppendLine($"  body {body}: NOT FOUND"); continue; }
                    Census(prefab, report);

                    foreach (Expression e in Expressions)
                    {
                        string caption = $"{body} / {e.Name}";
                        Texture2D? shot = Portrait(camera, root.transform, prefab, e, materials, null, attachments);
                        Texture2D? small = shot != null ? Resample(shot, 60) : null;
                        portrait.Add((caption, shot != null ? Enlarge(shot, 2) : null));
                        roster.Add((caption, small != null ? Enlarge(small, 4) : null));
                        if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
                        if (small != null) UnityEngine.Object.DestroyImmediate(small);

                        for (int v = 0; v < PlayViews.Length; v++)
                        {
                            Texture2D? crop = Play(camera, root.transform, prefab, e, materials, PlayViews[v]);
                            play[v].Add((caption, crop != null ? Enlarge(crop, Cell / PlayViews[v].Crop) : null));
                            if (crop != null) UnityEngine.Object.DestroyImmediate(crop);
                        }
                    }
                }

                int columns = Expressions.Length;
                Write("faces-portrait-128.png", portrait, columns, report);
                Write("faces-portrait-60.png", roster, columns, report);
                for (int v = 0; v < PlayViews.Length; v++) Write(PlayViews[v].File, play[v], columns, report);

                // ---- every hair piece, brows neutral and raised, on one male and one female.
                var hair = new List<(string, Texture2D?)>();
                foreach (string body in new[] { Bodies[0], Bodies[3] })
                {
                    GameObject? prefab = ScatterSheet.FindPrefab(body);
                    if (prefab == null) continue;
                    for (int h = 0; h < attachments.HairCount; h++)
                    {
                        if (!attachments.Hair(h).Usable) continue;
                        for (int x = 0; x < 2; x++)
                        {
                            Texture2D? shot = Portrait(camera, root.transform, prefab, Expressions[x], materials,
                                h, attachments);
                            hair.Add(($"{body} / hair {h} / {Expressions[x].Name}",
                                shot != null ? Enlarge(shot, 2) : null));
                            if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
                        }
                    }
                }
                Write("faces-hair.png", hair, 8, report);

                Debug.Log(report.ToString());
                if (!exitWhenDone) ShotFolder.Reveal("faces-*.png");
            }
            catch (Exception ex)
            {
                Debug.LogError("[FaceSheet] " + ex);
                exitCode = 1;
            }
            finally
            {
                materials?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>Which facial bones this body's skin actually weights, by vertex count.</summary>
        static void Census(GameObject prefab, StringBuilder report)
        {
            foreach (SkinnedMeshRenderer skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.gameObject.activeSelf || skin.sharedMesh == null) continue;
                Transform[] bones = skin.bones;
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                var counts = new Dictionary<string, int>();
                foreach (BoneWeight w in weights)
                {
                    if (w.weight0 < 0.5f || (uint)w.boneIndex0 >= (uint)bones.Length || bones[w.boneIndex0] == null) continue;
                    string name = bones[w.boneIndex0].name;
                    if (name != "Eyes" && name != "Eyebrows" && name != "Jaw") continue;
                    counts.TryGetValue(name, out int n);
                    counts[name] = n + 1;
                }
                var line = new StringBuilder($"  {prefab.name}/{skin.name} ({skin.sharedMesh.vertexCount}v, readable {skin.sharedMesh.isReadable}):");
                foreach (string b in new[] { "Eyes", "Eyebrows", "Jaw" })
                    line.Append($" {b} {(counts.TryGetValue(b, out int c) ? c : 0)}");
                report.AppendLine(line.ToString());
            }
        }

        static GameObject Dress(Transform parent, GameObject prefab, ColonistMaterials materials,
            int? hairIndex, ColonistAttachments? attachments, out Animator? animator)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            animator = instance.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            ColonistAttachments.BareTheHead(instance);

            var look = new ColonistAppearance(0, Skin, Hair, Suit, Trim);
            AppearanceCells cells = CharacterSwatches.Classify(prefab, out _);
            foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.gameObject.activeSelf) continue;
                Material? painted = materials.For(skin.sharedMaterial, cells.Any ? cells : null, look);
                if (painted != null) skin.sharedMaterial = painted;
            }

            Transform? head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (hairIndex.HasValue && attachments != null && head != null)
            {
                ColonistAttachments.MakeSlot(head, "Hair", instance.layer, out MeshFilter f, out MeshRenderer r);
                ColonistAttachments.Wear(f, r, attachments.Hair(hairIndex.Value), materials, look);
            }
            return instance;
        }

        static Transform? Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform? found = Find(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Pose the face through the game's own rig, so the picture is what the game draws.</summary>
        static void Pose(GameObject instance, Animator? animator, in Expression e)
        {
            Transform? head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null) return;
            FaceRig.Bind(instance.transform, head)?.Apply(e.Pose);
        }

        static Bounds BoundsOf(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>The portrait exactly as <see cref="PortraitStudio"/> frames it: 165°, orthographic, 128 px.</summary>
        static Texture2D? Portrait(Camera camera, Transform parent, GameObject prefab, in Expression e,
            ColonistMaterials materials, int? hairIndex, ColonistAttachments attachments)
        {
            GameObject instance = Dress(parent, prefab, materials, hairIndex, attachments, out Animator? animator);
            try
            {
                instance.transform.localRotation = Quaternion.Euler(0f, 165f, 0f);
                Pose(instance, animator, e);
                Transform? head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head == null) return null;
                Bounds body = BoundsOf(instance);
                float stature = body.size.y;
                Vector3 anchor = head.position + Vector3.up * (stature * 0.035f);
                camera.orthographic = true;
                camera.orthographicSize = stature * 0.135f;
                camera.transform.position = new Vector3(anchor.x, anchor.y, body.center.z - 3f);
                camera.transform.rotation = Quaternion.identity;
                return Render(camera, PortraitStudio.Size, PortraitStudio.Size, null);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        /// <summary>
        /// The play camera at <paramref name="view"/>'s distance, facing the colonist square on —
        /// the best case, since the game's colonists face every which way — and a crop of the
        /// frame round the eyes.
        /// </summary>
        static Texture2D? Play(Camera camera, Transform parent, GameObject prefab, in Expression e,
            ColonistMaterials materials, in PlayView view)
        {
            GameObject instance = Dress(parent, prefab, materials, null, null, out Animator? animator);
            try
            {
                instance.transform.localScale = Vector3.one * FigureScale;
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Transform? eyes = Find(instance.transform, "Eyes");
                if (eyes == null) return null;
                Vector3 face = eyes.position;
                Pose(instance, animator, e);

                camera.orthographic = false;
                camera.fieldOfView = FieldOfView;
                float p = Pitch * Mathf.Deg2Rad;
                camera.transform.position = face + new Vector3(0f, Mathf.Sin(p), -Mathf.Cos(p)) * view.Distance;
                camera.transform.LookAt(face);
                return Render(camera, view.Width, view.Height, (face, view.Crop));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static Texture2D? Render(Camera camera, int width, int height, (Vector3 Point, int Size)? crop)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4,
            };
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            frame.Apply();
            RenderTexture.active = previous;

            Texture2D result = frame;
            if (crop.HasValue)
            {
                Vector3 s = camera.WorldToScreenPoint(crop.Value.Point);
                int size = crop.Value.Size;
                int x = Mathf.Clamp(Mathf.RoundToInt(s.x) - size / 2, 0, width - size);
                int y = Mathf.Clamp(Mathf.RoundToInt(s.y) - size / 2, 0, height - size);
                result = new Texture2D(size, size, TextureFormat.RGBA32, false);
                result.SetPixels(frame.GetPixels(x, y, size, size));
                result.Apply();
                UnityEngine.Object.DestroyImmediate(frame);
            }
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            return result;
        }

        /// <summary>Down to the size the interface draws it at, with the filter the interface uses.</summary>
        static Texture2D Resample(Texture2D source, int size)
        {
            source.filterMode = FilterMode.Bilinear;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(size, size, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            small.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return small;
        }

        /// <summary>Nearest-neighbour enlargement, so every screen pixel stays one visible square.</summary>
        static Texture2D Enlarge(Texture2D source, int factor)
        {
            int w = source.width * factor, h = source.height * factor;
            Color32[] from = source.GetPixels32();
            var to = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    to[y * w + x] = from[(y / factor) * source.width + x / factor];
            var big = new Texture2D(w, h, TextureFormat.RGBA32, false);
            big.SetPixels32(to);
            big.Apply();
            return big;
        }

        static void Write(string file, List<(string Name, Texture2D? Shot)> shots, int columns, StringBuilder report)
        {
            int lines = Math.Max(1, (shots.Count + columns - 1) / columns);
            var sheet = new Texture2D(columns * Cell, lines * Cell, TextureFormat.RGBA32, false);
            var clear = new Color32[columns * Cell * lines * Cell];
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
                int w = Math.Min(Cell, shot.width), h = Math.Min(Cell, shot.height);
                int ox = (Cell - w) / 2, oy = (Cell - h) / 2;
                sheet.SetPixels32(col * Cell + ox, (lines - 1 - line) * Cell + oy, w, h, shot.GetPixels32());
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
