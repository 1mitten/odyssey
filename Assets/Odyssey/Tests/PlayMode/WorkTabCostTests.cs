#nullable enable
using System;
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// <b>What the Work tab costs while it is open, measured rather than argued.</b>
    ///
    /// <para>Owner, 2026-09-20, before merging: <i>"can we ensure it's performant"</i>. The fast
    /// tier asserts the element count is <i>bounded</i> and that a refresh reuses its rows, which
    /// are the two things that make the cost a constant — but neither of them is a millisecond.
    /// This is the millisecond, taken the way <c>HudStressTests</c> takes its own: the real panel,
    /// over a real session, with the panel open and refreshing on its own cadence.</para>
    ///
    /// <para><b>Held against the same budget and by the same reasoning.</b> ADR 0003 sets 3.5 ms on
    /// a 2022 mid-range laptop for the whole HUD; this machine is far quicker, so the headroom
    /// factor of 3 applies here exactly as it does there, and the Work tab is given a quarter of
    /// that because it is one panel and not the interface.</para>
    ///
    /// <para><b>Read the baseline before reading the verdict.</b> The test logs the shipped HUD
    /// with the panel shut and quotes the open cost over it, so a failure caused by another Unity
    /// batch run on the same machine shows up as a baseline that has moved rather than as a
    /// regression in this panel. That has already happened once (docs/lessons.md).</para>
    /// </summary>
    public class WorkTabCostTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        const int WarmupFrames = 120;
        const int TimedFrames = 180;

        /// <summary>ADR 0003's whole-HUD figure, and this panel is a quarter of it.</summary>
        const float LaptopBudgetMs = 3.5f;
        const float HeadroomFactor = 3f;
        const float PanelShareOfHud = 0.25f;
        const float BudgetMs = LaptopBudgetMs / HeadroomFactor * PanelShareOfHud;

        [UnityTest]
        public IEnumerator TheWorkTabCostsLittleEnoughToLeaveOpen()
        {
            GameObject root = Build();
            try
            {
                yield return WarmUp();

                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(boot, Is.Not.Null);
                Assert.That(doc?.rootVisualElement, Is.Not.Null, "the HUD has no panel");

                HudDirectors? directors = boot!.Directors;
                if (directors == null)
                {
                    Assert.Ignore("no session, so there is no Work tab to open");
                    yield break;
                }

                // Shut: the shipped HUD doing everything else it does.
                directors.Work.SetOpen(false);
                yield return Settle();
                float closed = 0f;
                yield return Sample(TimedFrames, ms => closed = ms);

                // Open: the same HUD with the grid up and refreshing on the mid bucket.
                directors.Work.SetOpen(true);
                yield return Settle();
                float open = 0f;
                long bytes = 0;
                int collections = 0;
                yield return SampleWithHeap(TimedFrames, (ms, b, c) =>
                {
                    open = ms;
                    bytes = b;
                    collections = c;
                });

                VisualElement? panel = doc!.rootVisualElement.Q("work");
                Assert.That(panel, Is.Not.Null, "the Work panel is not in the tree");
                Assert.That(panel!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                    "the panel did not actually open, so nothing here was measured");

                int elements = Count(panel);
                float cost = open - closed;
                double perFrame = (double)bytes / TimedFrames;

                Debug.Log($"[Work] open costs {cost:0.000} ms over a {closed:0.000} ms baseline, " +
                          $"against a {BudgetMs:0.000} ms budget ({LaptopBudgetMs} ms laptop / " +
                          $"{HeadroomFactor} headroom / {PanelShareOfHud:0.##} of the HUD). " +
                          $"{elements} elements, {perFrame:0.0} B/frame over {collections} gen-0 " +
                          $"collections. {Screen.width}x{Screen.height}, {SystemInfo.processorType}.");

                // The bound the fast tier asserts, checked against the tree that was actually
                // built: eleven columns and twelve rows, never the catalogue and never the colony.
                Assert.That(elements, Is.LessThan(2_000),
                    $"the open panel built {elements} elements. A page is 11 columns by 12 rows " +
                    "however large the colony is, so this number does not grow — if it has, the " +
                    "paging has stopped bounding what is built.");

                Assert.That(cost, Is.LessThan(BudgetMs),
                    $"the Work tab costs {cost:0.000} ms a frame while open, against a " +
                    $"{BudgetMs:0.000} ms budget. Read the {closed:0.000} ms baseline first: if it " +
                    "has moved too, another Unity run is on this machine and this is contention " +
                    "rather than a regression (docs/lessons.md).");

                Assert.That(collections, Is.Zero,
                    $"the collector ran {collections} times with the panel open and steady. Its " +
                    "rows come from a pool and its cells are rebuilt only when the roster changes, " +
                    "so a collection means something started allocating every refresh.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static int Count(VisualElement element)
        {
            int total = 1;
            for (int i = 0; i < element.childCount; i++) total += Count(element[i]);
            return total;
        }

        static IEnumerator Sample(int frames, Action<float> report)
        {
            float total = 0f;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                total += Time.unscaledDeltaTime * 1000f;
            }
            report(total / frames);
        }

        static IEnumerator SampleWithHeap(int frames, Action<float, long, int> report)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;

            long before = GC.GetTotalMemory(false);
            int collectionsBefore = GC.CollectionCount(0);
            float total = 0f;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                total += Time.unscaledDeltaTime * 1000f;
            }
            report(total / frames,
                GC.GetTotalMemory(false) - before,
                GC.CollectionCount(0) - collectionsBefore);
        }

        static IEnumerator Settle()
        {
            for (int i = 0; i < WarmupFrames; i++) yield return null;
        }

        static IEnumerator WarmUp()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>The stack <c>HudStressTests</c> builds, and for the same reason.</summary>
        static GameObject Build()
        {
            var root = new GameObject("WorkTabCost");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.transform.rotation = Quaternion.Euler(72f, 35f, 0f);

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);
            var boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.buildOnPlay = true;
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.layers = 8;
            boot.seed = 1;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.AddComponent<SelectionPresenter>();

            var doc = bootObject.AddComponent<UIDocument>();
#if UNITY_EDITOR
            doc.panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            if (doc.panelSettings == null)
            {
                doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                doc.panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            }

            var shell = bootObject.AddComponent<HudShell>();
#if UNITY_EDITOR
            shell.hudStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
#endif
            bootObject.SetActive(true);
            return root;
        }
    }
}
