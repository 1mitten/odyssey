#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Odyssey.Presentation.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Which of the Sword Combat pack's attack clips are swings and which are thrusts, measured and
    /// photographed (design 33 §22).
    ///
    /// <para><b>Written for the owner's report</b> (2026-09-25): <i>"crowbar and baseball bat
    /// [should not] have the stabbing motion — only swinging type moves."</i> The heavy row
    /// alternated <c>HeavyCombo01A</c> with <c>HeavyStab01</c>, and a clip's name is the only thing
    /// that said which was which. The replacement, <c>HeavyCombo01B</c> and <c>C</c>, had never
    /// been looked at, so a name is not enough to put them in a blunt weapon's hand.</para>
    ///
    /// <para><b>What it measures.</b> Each clip plays on a real colonist body and the right hand
    /// is tracked in the body's own frame over the blow itself — the Hit cut, the fifth of a second
    /// after the wind-up. Two numbers read it: the share of the hand's travel that runs <i>along
    /// the arm</i> (a thrust extends the arm, so the hand moves along the shoulder-to-hand line; a
    /// swing rotates it, so the hand moves across that line), and how far that line <i>turns</i>.
    /// <c>HeavyStab01</c> is the negative control and <c>LightCombo01A</c>, a swing every blade
    /// already uses, the positive one: if the probe cannot tell those two apart it says nothing
    /// about the others. It could not on its first metric (the hand's forward share before the
    /// impact, which scored the stab as a swing), and that metric is gone. Measured 2026-09-25:
    /// the stab 70 % along the arm and 49° of turn, the light swing 36 % and 145°,
    /// <c>HeavyCombo01A/B/C</c> 18–23 % and 102–126° (design 33 §22).</para>
    ///
    /// <para><c>scripts/unity.sh shot Odyssey.EditorTools.HeavySwingSheet.Shoot</c> — it needs a
    /// graphics device for the picture, written to
    /// <c>docs/reference/screenshots/2026-09-25-heavy-swings.png</c>: two rows per clip, the
    /// figure's right side over a view from above, seven frames from the start through the impact
    /// (the fourth) to the end, the hand's path so far drawn as a trail of dots.</para>
    /// </summary>
    public static class HeavySwingSheet
    {
        /// <summary>The clips, the controls first.</summary>
        public static readonly string[] Clips =
        {
            "A_Attack_HeavyStab01_Sword",      // negative control: the thrust being removed
            "A_Attack_LightCombo01A_Sword",    // positive control: a swing the blades already use
            "A_Attack_HeavyCombo01A_Sword",
            "A_Attack_HeavyCombo01B_Sword",
            "A_Attack_HeavyCombo01C_Sword",
            "A_Attack_HeavyFlourish01_Sword",
        };

        const string Body = "SM_Gen_Chr_Street_Male_01";
        const int Frames = 7, TileWidth = 220, TileHeight = 240;
        const float Window = 0.2f;

        [MenuItem("Odyssey/Presentation/Shoot heavy swings")]
        public static void ShootFromMenu() => Run(false);

        public static void Shoot() => Run(Application.isBatchMode);

        static void Run(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? root = null;
            Texture2D? sheet = null;
            Material? dotMaterial = null;
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null) throw new InvalidOperationException("no catalogue");
                ModuleEntry? row = null;
                foreach (ModuleEntry r in catalogue.FindFamily(ModuleIds.ColonistBase))
                    if (r.prefabName == Body && r.prefab != null) row = r;
                if (row == null) throw new InvalidOperationException($"no {Body} here; the packs are gitignored");

                root = new GameObject("HeavySwingSheet");
                PlayScene.BuildSheetLighting(root.transform);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.orthographic = true;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 40f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.6f, 0.66f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

                dotMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                dotMaterial.SetColor("_BaseColor", new Color(1f, 0.25f, 0.1f));

                sheet = new Texture2D(TileWidth * Frames, 2 * TileHeight * Clips.Length, TextureFormat.RGB24, false);
                var report = new StringBuilder();
                report.AppendLine($"[HeavySwing] body {Body}; the hand over the {Window:F2} s after the wind-up (the Hit cut)");

                for (int c = 0; c < Clips.Length; c++)
                {
                    AnimationClip? clip = PlayScene.FindSwordCombatClip(Clips[c], out string? path);
                    if (clip == null || path == null)
                    {
                        report.AppendLine($"  {Clips[c],-34} NOT FOUND");
                        continue;
                    }
                    float impact = PlayScene.MeasureImpact(path, clip.name, clip);
                    int y = (Clips.Length - 1 - c) * 2 * TileHeight;
                    report.AppendLine("  " + Shoot(row, clip, impact, camera, dotMaterial, root.transform, sheet, y));
                }

                sheet.Apply();
                string folder = Path.GetFullPath("docs/reference/screenshots");
                Directory.CreateDirectory(folder);
                const string name = "2026-09-25-heavy-swings.png";
                File.WriteAllBytes(Path.Combine(folder, name), sheet.EncodeToPNG());
                report.AppendLine($"[HeavySwing] wrote {name}: rows {string.Join(", ", Clips)}; two rows each, the right side over the top view; the impact is the fourth frame");
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[HeavySwing] " + e);
                exitCode = 1;
            }
            finally
            {
                if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
                if (dotMaterial != null) UnityEngine.Object.DestroyImmediate(dotMaterial);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>One clip: its measurement, and a row of the sheet.</summary>
        static string Shoot(ModuleEntry row, AnimationClip clip, float impact, Camera camera, Material dot,
            Transform parent, Texture2D sheet, int y)
        {
            GameObject body = UnityEngine.Object.Instantiate(row.prefab!, parent);
            var dots = new List<GameObject>();
            PlayableGraph graph = default;
            try
            {
                body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                body.transform.localScale = row.scale.sqrMagnitude < 1e-6f ? Vector3.one : row.scale;
                var animator = body.GetComponent<Animator>();
                if (animator == null) animator = body.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                graph = PlayableGraph.Create("Odyssey heavy swing sheet");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph, "Swing", animator).SetSourcePlayable(playable);
                graph.Play();

                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (hand == null || shoulder == null) return $"{clip.name,-34} NOT HUMANOID";

                // The hand's path, sampled at 60 per second, in the body's frame (x right, y up, z forward).
                float length = clip.length;
                int samples = Mathf.Max(2, Mathf.CeilToInt(length * 60f) + 1);
                var hands = new Vector3[samples];
                var arms = new Vector3[samples];
                for (int i = 0; i < samples; i++)
                {
                    Pose(graph, playable, length * i / (samples - 1));
                    hands[i] = body.transform.InverseTransformPoint(hand.position);
                    arms[i] = body.transform.InverseTransformDirection(hand.position - shoulder.position).normalized;
                }

                // The blow is the Hit cut: from the end of the wind-up to a fifth of a second after it.
                int from = Mathf.Clamp(Mathf.RoundToInt(impact / length * (samples - 1)), 0, samples - 2);
                int to = Mathf.Min(samples - 1, from + Mathf.RoundToInt(Window * 60f));
                float along = 0f, path = 0f;
                for (int i = from + 1; i <= to; i++)
                {
                    Vector3 step = hands[i] - hands[i - 1];
                    along += Mathf.Abs(Vector3.Dot(step, arms[i]));
                    path += step.magnitude;
                }
                float alongShare = path > 1e-5f ? along / path : 0f;
                float arc = Vector3.Angle(arms[from], arms[to]);

                // The row: seven frames, the fourth the impact, with the hand's path so far as dots.
                float[] times =
                {
                    0f, impact / 3f, impact * 2f / 3f, impact,
                    impact + (length - impact) / 3f, impact + (length - impact) * 2f / 3f, length,
                };
                int placed = 0;
                for (int f = 0; f < Frames; f++)
                {
                    int upTo = Mathf.RoundToInt(times[f] / length * (samples - 1));
                    for (int i = placed; i <= upTo; i += 2, placed = i)
                    {
                        var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
                        d.GetComponent<Renderer>().sharedMaterial = dot;
                        d.transform.SetParent(parent, false);
                        d.transform.position = body.transform.TransformPoint(hands[i]);
                        d.transform.localScale = Vector3.one * 0.035f;
                        dots.Add(d);
                    }
                    Pose(graph, playable, times[f]);
                    // From the figure's right side (an overhead chop is an arc, a thrust a line
                    // going forward) and from above (a flat swing is an arc, a thrust a line).
                    Tile(camera, new Vector3(0f, 1.0f, 0.4f), Vector3.left, 1.3f, sheet, f * TileWidth, y + TileHeight);
                    Tile(camera, new Vector3(0f, 1.0f, 0.4f), new Vector3(0f, -1f, 0.001f), 1.3f, sheet, f * TileWidth, y);
                }

                return $"{clip.name,-34} impact {impact:F3} s of {length:F3}; over the hit: along the arm {alongShare:P0}, " +
                       $"arm turns {arc:F0} deg, hand travels {path:F2} m";
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                foreach (GameObject d in dots) UnityEngine.Object.DestroyImmediate(d);
                UnityEngine.Object.DestroyImmediate(body);
            }
        }

        static void Pose(PlayableGraph graph, AnimationClipPlayable playable, float seconds)
        {
            playable.SetTime(seconds);
            playable.SetTime(seconds);   // twice, so the previous time is this one and nothing is blended
            graph.Evaluate(0f);
        }

        /// <summary>One orthographic view along <paramref name="view"/>, into the sheet at (x, y).</summary>
        static void Tile(Camera camera, Vector3 focus, Vector3 view, float halfHeight, Texture2D sheet, int x, int y)
        {
            camera.orthographicSize = halfHeight;
            camera.aspect = TileWidth / (float)TileHeight;
            camera.transform.SetPositionAndRotation(focus - view.normalized * 15f, Quaternion.LookRotation(view, Vector3.up));
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
