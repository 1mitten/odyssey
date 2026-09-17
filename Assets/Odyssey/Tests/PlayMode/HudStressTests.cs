#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Experiment R1 of <c>docs/research/g-02-unity-ui-framework.md</c>, and flip condition F1 of
    /// ADR 0003, which has stood at "accepted, pending one measurement" since 2026-09-15.
    ///
    /// <b>What F1 asks.</b> The dense case — a 50 x 25 priority grid, a 50-card roster bar and a
    /// ten-thousand-row archive, all open at once — must hold the frame budget and allocate
    /// nothing per frame after warm-up, with any cost attributable to framework internals rather
    /// than to our code. None of those three panels exists in the game yet, so the case is built
    /// synthetically here. That is the point rather than a shortcut: F1 is a question about UI
    /// Toolkit, and real panels would answer it with their own logic mixed in.
    ///
    /// <b>Why the data comes from a pool.</b> A retained panel allocates nothing while nothing
    /// changes, so a HUD left idle measures nothing and would pass vacuously. A HUD whose labels
    /// are rebuilt from interpolated strings measures the opposite error: the garbage is ours,
    /// not the framework's, and F1 excludes that in as many words. So the text assigned each
    /// frame is drawn from strings built once during set-up. Whatever is allocated after that
    /// belongs to UI Toolkit, which is what F1 is asking about.
    ///
    /// <b>Why the cost is a difference.</b> There is no verified profiler marker for a panel's own
    /// main-thread time, and naming one on faith is exactly how <c>docs/lessons.md</c> records the
    /// renderer benchmark going wrong. So the same scene is timed twice, once with the dense layer
    /// attached and once without, under the real player loop both times. The difference is the
    /// HUD, measured the only way this project trusts a frame number.
    /// </summary>
    public class HudStressTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        // The dense case, as ADR 0003 F1 and design 09 section 4 enumerate it.
        const int GridColumns = 50;
        const int GridRows = 25;
        const int RosterCards = 50;
        const int ArchiveRows = 10000;

        const int WarmupFrames = 120;
        const int TimedFrames = 180;

        /// <summary>
        /// Frames between one label's rebinds under the bucket cadence: fifteen is four times a
        /// second, which is the order design 09's frequency buckets describe for a need bar.
        /// </summary>
        const int BucketStride = 15;

        /// <summary>
        /// ADR 0003 sets 3.5 ms on a 2022 mid-range laptop. This machine is not that machine, and
        /// <c>FrameTimeTests</c> met the same problem by going loose and saying so. The owner's
        /// ruling on 2026-09-16 was a headroom rule instead, so that the budget still bites: the
        /// dense HUD must come in under a third of the laptop figure here.
        ///
        /// The divisor is <b>ASSUMED</b>. HUD cost is main-thread work — layout, style resolution,
        /// mesh generation, draw submission — so the axis that matters is single-thread speed and
        /// not the GPU. This machine is a Ryzen 7 9800X3D at 4.7 GHz; a 2022 mid-range laptop part
        /// is roughly half its single-thread throughput and throttles under sustained load, so
        /// three covers that gap with margin. It stands until someone measures a real laptop, at
        /// which point this constant is replaced by the observation and this paragraph deleted.
        /// </summary>
        const float LaptopBudgetMs = 3.5f;
        const float HeadroomFactor = 3f;
        const float DevBudgetMs = LaptopBudgetMs / HeadroomFactor;

        /// <summary>Which of the three dense panels a run puts up.</summary>
        [Flags]
        enum Parts
        {
            None = 0,
            Grid = 1,
            Roster = 2,
            Archive = 4,
            All = Grid | Roster | Archive,
        }

        [UnityTest]
        public IEnumerator Adr0003_F1_TheDenseHudHoldsItsBudgetAndAllocatesNothing()
        {
            GameObject root = Build();
            try
            {
                yield return WarmUp();

                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(doc, Is.Not.Null, "the HUD document is on the bootstrap object");
                Assert.That(doc!.rootVisualElement, Is.Not.Null, "the panel has no root: no panel settings?");

                // Baseline first: the real HUD alone, same scene, same loop, no dense layer.
                yield return Settle();
                Reading baseline = default;
                yield return Sample(TimedFrames, r => baseline = r);
                Debug.Log($"[R1] baseline, the shipped HUD alone: {baseline.MeanMs:0.000} ms a frame, " +
                          $"{(double)baseline.Bytes / TimedFrames:0.0} B/frame over {baseline.Collections} " +
                          $"gen-0 collections. {Screen.width}x{Screen.height}, {SystemInfo.processorType}, " +
                          $"{SystemInfo.graphicsDeviceName}.");

                // Each panel on its own before all three together. F1 only fires when the cost is
                // the framework's rather than ours, and "one Label per grid cell" is a construction
                // we chose. Splitting the arms is what tells those two apart: if the grid is the
                // whole cost, the answer is the custom-painted grid that g-02 already names as R1's
                // second arm, not a different UI framework.
                Reading all = default;
                int allLabels = 0;
                foreach (Parts parts in new[] { Parts.Grid, Parts.Roster, Parts.Archive, Parts.All })
                {
                    var stress = new StressLayer(doc.rootVisualElement, parts,
                        GridColumns, GridRows, RosterCards, ArchiveRows);
                    try
                    {
                        yield return WarmUpStress(stress);

                        Reading arm = default;
                        yield return Sample(TimedFrames, r => arm = r, stress);
                        if (parts == Parts.All)
                        {
                            all = arm;
                            allLabels = stress.LabelsTouchedPerFrame;
                        }

                        Debug.Log($"[R1] {parts}: {arm.MeanMs - baseline.MeanMs:0.000} ms over baseline " +
                                  $"({arm.MeanMs:0.000} ms a frame), " +
                                  $"{(double)(arm.Bytes - baseline.Bytes) / TimedFrames:0.0} B/frame, " +
                                  $"{arm.Collections} gen-0 collections, " +
                                  $"{stress.LabelsTouchedPerFrame} labels retexted per frame from a prebuilt pool.");
                    }
                    finally
                    {
                        stress.Dispose();
                    }
                    yield return Settle();
                }

                // The arms above rebind every live label every frame, which is the pessimistic
                // bound and is not what this HUD does. Design 09 binds inside frequency buckets:
                // a need bar moves on the simulation's cadence, not the display's. So the same
                // dense case is run again at a bucket cadence, and that is the arm F1 is asserted
                // against, because F1 asks whether the framework carries *this design* rather than
                // whether it survives a binding strategy the design already rejects.
                //
                // Both numbers are reported. The 60 Hz figure is not thrown away: it is the
                // ceiling, and the per-label cost derived from it is what lets any future panel be
                // budgeted before it is built.
                Reading bucketed = default;
                var bucketedLayer = new StressLayer(doc.rootVisualElement, Parts.All,
                    GridColumns, GridRows, RosterCards, ArchiveRows);
                try
                {
                    yield return WarmUpStress(bucketedLayer, BucketStride);
                    yield return Sample(TimedFrames, r => bucketed = r, bucketedLayer, BucketStride);
                }
                finally
                {
                    bucketedLayer.Dispose();
                }

                float everyFrameMs = all.MeanMs - baseline.MeanMs;
                float hudMs = bucketed.MeanMs - baseline.MeanMs;
                long hudBytes = bucketed.Bytes - baseline.Bytes;
                double bytesPerFrame = (double)hudBytes / TimedFrames;
                double perLabelUs = everyFrameMs * 1000d / allLabels;

                Debug.Log($"[R1] All at a {BucketStride}-frame bucket cadence " +
                          $"({60f / BucketStride:0.#} Hz a label, about {allLabels / BucketStride} labels a frame): " +
                          $"{hudMs:0.000} ms over baseline, {(double)hudBytes / TimedFrames:0.0} B/frame, " +
                          $"{bucketed.Collections} gen-0 collections. Every label every frame was " +
                          $"{everyFrameMs:0.000} ms, so a label whose text changes costs about " +
                          $"{perLabelUs:0.0} us on this machine.");

                Debug.Log($"[R1] dense case: {GridColumns}x{GridRows} grid ({GridColumns * GridRows} cells), " +
                          $"{RosterCards} cards, {ArchiveRows} archive rows (ListView, virtualised) — " +
                          $"{hudMs:0.000} ms against a {DevBudgetMs:0.000} ms dev budget " +
                          $"({LaptopBudgetMs} ms laptop / {HeadroomFactor} headroom, ASSUMED), " +
                          $"{bytesPerFrame:0.0} B/frame over {all.Collections} gen-0 collections.");

                // F1, first half. "Allocates anything per frame after warm-up" is the ADR's own
                // wording and it is deliberately absolute, so this is asserted at zero rather than
                // at a tolerance. A non-zero result is the experiment doing its job, not the test
                // being too strict: it is F1 firing, and it goes to the owner rather than being
                // tuned away.
                //
                // The collection count is checked first because it is the stronger signal. If the
                // collector ran at all during a few seconds of steady state then something
                // allocated, whatever the byte difference reads, and the byte difference is an
                // understatement rather than a measurement.
                Assert.That(all.Collections, Is.Zero,
                    $"the collector ran {all.Collections} times during {TimedFrames} steady-state frames " +
                    "with the dense HUD up, so it is allocating however small the heap difference looks. " +
                    "Its text comes from a prebuilt pool, so the garbage is UI Toolkit's rather than ours. " +
                    "That is flip condition F1 of ADR 0003.");

                Assert.That(bytesPerFrame, Is.LessThanOrEqualTo(0d),
                    $"the dense HUD allocates {bytesPerFrame:0.0} bytes a frame after warm-up, and its text " +
                    "comes from a prebuilt pool, so the garbage is UI Toolkit's rather than ours. " +
                    "That is flip condition F1 of ADR 0003.");

                // F1, second half.
                Assert.That(hudMs, Is.LessThan(DevBudgetMs),
                    $"the dense HUD costs {hudMs:0.000} ms on this machine against a {DevBudgetMs:0.000} ms " +
                    $"budget ({LaptopBudgetMs} ms on the target laptop divided by {HeadroomFactor} headroom). " +
                    "Read the per-panel arms logged above before calling this a framework verdict. " +
                    "That is flip condition F1 of ADR 0003.");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        readonly struct Reading
        {
            public Reading(float meanMs, long bytes, int collections)
            {
                MeanMs = meanMs;
                Bytes = bytes;
                Collections = collections;
            }

            public float MeanMs { get; }

            /// <summary>Growth of the managed heap across the window, in bytes.</summary>
            public long Bytes { get; }

            /// <summary>Generation-zero collections during the window. See <c>Sample</c>.</summary>
            public int Collections { get; }
        }

        /// <summary>
        /// Times a window under the real player loop, driving the stress layer if there is one.
        ///
        /// <b>How allocation is read, and why not the obvious way.</b> The obvious call is
        /// <c>GC.GetTotalAllocatedBytes</c>, which is what OQ-19's queue row names. It does not
        /// exist in this project's runtime — the compiler rejects it outright — so that row needs
        /// amending before it is worked. What is used instead is heap growth from
        /// <c>GC.GetTotalMemory(false)</c>, which is exact only while no collection runs, since a
        /// collection hands memory back and makes the difference understate what was allocated.
        ///
        /// So the collection count is carried alongside it, and the two together are honest in
        /// both directions. No collection over the window means the byte figure is the allocation
        /// figure. A collection means allocation certainly happened, whatever the bytes say —
        /// a generation-zero collection during a few seconds of steady state is itself the
        /// finding, and the reading is reported as understated rather than believed.
        /// </summary>
        static IEnumerator Sample(int frames, Action<Reading> report, StressLayer? stress = null, int stride = 1)
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
                stress?.Drive(i, stride);
                yield return null;
                total += Time.unscaledDeltaTime * 1000f;
            }
            long after = GC.GetTotalMemory(false);
            int collectionsAfter = GC.CollectionCount(0);

            report(new Reading(total / frames, after - before, collectionsAfter - collectionsBefore));
        }

        static IEnumerator Settle()
        {
            for (int i = 0; i < WarmupFrames; i++) yield return null;
        }

        /// <summary>
        /// The dense layer needs its own warm-up: the first frames build the visual tree, resolve
        /// styles and fill the ListView's recycled pool, all of which allocate once and would be
        /// charged to the steady state if they were inside the timed window.
        /// </summary>
        static IEnumerator WarmUpStress(StressLayer stress, int stride = 1)
        {
            for (int i = 0; i < WarmupFrames; i++)
            {
                stress.Drive(i, stride);
                yield return null;
            }
        }

        static IEnumerator WarmUp()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>
        /// The same stack <c>HudSmokeTests</c> builds: the real panel asset and stylesheet over a
        /// small barren world, so what is timed is the interface rather than the terrain.
        /// </summary>
        static GameObject Build()
        {
            var root = new GameObject("HudStress");

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
            bootObject.SetActive(false);   // so the fields land before Start runs
            var boot = bootObject.AddComponent<OdysseyBootstrap>();
            // Explicitly, not by default: since U38 pressing Play lands on the start screen, and
            // what this rig is asserting is that a session exists.
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

        /// <summary>
        /// The dense case as a detachable overlay: a priority grid, a roster bar and a virtualised
        /// archive, driven from strings built once so that nothing it allocates belongs to us.
        /// </summary>
        sealed class StressLayer : IDisposable
        {
            const int PoolSize = 64;

            readonly VisualElement _root;
            readonly VisualElement _host;
            readonly List<Label> _live = new List<Label>();
            readonly string[] _pool = new string[PoolSize];
            readonly List<int> _archive;

            public int LabelsTouchedPerFrame => _live.Count;

            public StressLayer(VisualElement root, Parts parts, int columns, int rows, int cards, int archiveRows)
            {
                _root = root;

                // Built once, never inside the timed window.
                for (int i = 0; i < PoolSize; i++)
                {
                    _pool[i] = (i * 7 % 100).ToString("00") + " pct";
                }

                _archive = new List<int>(archiveRows);
                for (int i = 0; i < archiveRows; i++)
                {
                    _archive.Add(i);
                }

                _host = new VisualElement();
                _host.style.position = Position.Absolute;
                _host.style.left = 0;
                _host.style.top = 0;
                _host.style.right = 0;
                _host.style.bottom = 0;
                // Untouchable, so nothing about the existing interface changes while it is
                // attached and the baseline stays comparable.
                _host.pickingMode = PickingMode.Ignore;

                if ((parts & Parts.Grid) != 0) _host.Add(BuildGrid(columns, rows));
                if ((parts & Parts.Roster) != 0) _host.Add(BuildRoster(cards));
                if ((parts & Parts.Archive) != 0) _host.Add(BuildArchive());

                _root.Add(_host);
            }

            VisualElement BuildGrid(int columns, int rows)
            {
                var grid = new VisualElement();
                grid.style.flexDirection = FlexDirection.Column;
                for (int y = 0; y < rows; y++)
                {
                    var line = new VisualElement();
                    line.style.flexDirection = FlexDirection.Row;
                    for (int x = 0; x < columns; x++)
                    {
                        var cell = new Label(_pool[(x + y) % PoolSize]);
                        cell.style.width = 18;
                        cell.style.height = 16;
                        line.Add(cell);
                        // A real priority grid repaints on a cadence, not wholesale every frame.
                        // One column of it changing per frame is the honest steady state.
                        if (x == 0) _live.Add(cell);
                    }
                    grid.Add(line);
                }
                return grid;
            }

            VisualElement BuildRoster(int cards)
            {
                var bar = new VisualElement();
                bar.style.flexDirection = FlexDirection.Row;
                for (int i = 0; i < cards; i++)
                {
                    var card = new VisualElement();
                    card.style.width = 84;
                    card.style.flexDirection = FlexDirection.Column;
                    card.Add(new Label(_pool[i % PoolSize]));

                    // Needs move every frame on a real card, and that is where a roster bar earns
                    // its cost, so all three are driven.
                    for (int n = 0; n < 3; n++)
                    {
                        var need = new Label(_pool[(i + n) % PoolSize]);
                        card.Add(need);
                        _live.Add(need);
                    }

                    bar.Add(card);
                }
                return bar;
            }

            VisualElement BuildArchive()
            {
                // The claim under test is virtualisation: ten thousand rows must cost what is on
                // screen rather than what is in the list. A plain container of ten thousand labels
                // would measure the wrong thing and answer a question nobody asked.
                var list = new ListView
                {
                    itemsSource = _archive,
                    fixedItemHeight = 16,
                    virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                    makeItem = () => new Label(),
                    bindItem = (element, index) => ((Label)element).text = _pool[index % PoolSize],
                };
                list.style.height = 240;
                list.style.width = 320;
                return list;
            }

            /// <summary>
            /// One frame of fake data, retexted from the pool.
            ///
            /// <paramref name="stride"/> is the binding cadence. At one, every live label changes
            /// every frame, which is the pessimistic bound and not what the HUD does. Above one,
            /// each label changes every <paramref name="stride"/> frames on a rotating schedule,
            /// which is what design 09's frequency buckets describe: a need bar moves on the
            /// simulation's cadence, not on the display's.
            /// </summary>
            public void Drive(int frame, int stride)
            {
                for (int i = 0; i < _live.Count; i++)
                {
                    if (stride > 1 && i % stride != frame % stride) continue;
                    _live[i].text = _pool[(frame + i) % PoolSize];
                }
            }

            public void Dispose()
            {
                _root.Remove(_host);
            }
        }
    }
}
