#nullable enable
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A stockpile drag, from the presenter's own drag handler to a published zone — everything
    /// after the pointer. Owner, 2026-09-23: <i>"I can't seem to create stockpiles anymore"</i>, the
    /// drag painting nothing, while a Fell drag in the same session reached the simulation. A batch
    /// run cannot press a mouse (CLAUDE.md, known gaps), so this raises the gesture the rig would
    /// raise and asserts the rest: if it paints here and not in Play, the fault is on the input side.
    /// </summary>
    public class StockpileDragTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";

        [UnityTest]
        public IEnumerator AStockpileDragReachesTheGameAndIsPublished()
        {
            GameObject root = Build();
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 0; i < 3; i++) yield return null;

                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var presenter = root.GetComponentInChildren<DesignatePresenter>();
                Assert.That(boot.World, Is.Not.Null, "no session");
                Assert.That(presenter, Is.Not.Null, "no designate presenter");

                // A colonist stands on walkable ground: the box goes beside them.
                WorldSnapshot frame = boot.World!.Views.Current;
                Assume.That(frame.PawnCount, Is.GreaterThan(0));
                CellRef at = frame.Pawns[0].Cell;
                var anchor = new CellRef(at.X + 1, at.Z + 1, at.Y);
                var head = new CellRef(at.X + 3, at.Z + 3, at.Y);

                presenter!.Director.Tool = DesignateTool.Stockpile;
                MethodInfo drag = typeof(DesignatePresenter).GetMethod("OnToolDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                drag.Invoke(presenter, new object[] { anchor, head });

                // Until the tick has taken the orders and the renderer has had its frames, not a
                // fixed count: a warm second world runs frames faster than ticks.
                var model = boot.Model!;
                int stride = boot.World.Size.LayerStride;
                float until = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < until &&
                       (boot.World.Views.Current.Stores.Length == 0 ||
                        !model.IsStoredAbove(boot.World.Views.Current.Stores[0].CellIndex - stride)))
                    yield return null;
                for (int i = 0; i < 5; i++) yield return null;

                int stores = boot.World.Views.Current.Stores.Length;
                Debug.Log($"[StockpileDrag] box {anchor} to {head} from a colonist at {at}: " +
                          $"{stores} stockpile cells published");
                Assert.That(stores, Is.GreaterThan(0),
                    "a stockpile drag beside a colonist published no zone cells, so the fault is " +
                    "after the pointer: the director, the presenter's submit or the simulation");

                // **And it is drawn** (owner, 2026-09-23: "There is no visual to the stockpile").
                // On natural ground a store's cell is the air above the ground, and the wash is on
                // the ground's top face, which the chunk one layer down meshes (IsStoredAbove).
                // Since chunks keep their own versions, that chunk re-meshes only if it was marked,
                // so it must be at least as fresh as the store's own chunk, which the mark bumped.
                int storeCell = boot.World.Views.Current.Stores[0].CellIndex;
                CellRef store = boot.World.Size.FromIndex(storeCell);
                CellRef ground = boot.World.Size.FromIndex(storeCell - stride);
                Assert.That(model.IsStoredAbove(storeCell - stride), Is.True,
                    "the render mirror did not hear about the store");
                int storeVersion = model.ChunkVersion(model.Chunks.ChunkIndexOfCell(store));
                int groundVersion = model.ChunkVersion(model.Chunks.ChunkIndexOfCell(ground));
                Debug.Log($"[StockpileDrag] store {store} chunk version {storeVersion}, ground {ground} chunk version {groundVersion}");
                Assert.That(groundVersion, Is.GreaterThanOrEqualTo(storeVersion),
                    "the chunk that meshes the ground under the stockpile is older than the stockpile, " +
                    "so it was never re-meshed and the wash cannot be on screen");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The default gesture (owner, 2026-09-17): click to anchor, move, click to place. It goes
        /// through the presenter's click handler rather than its drag handler, so it is a second
        /// path to the same order and is asserted on its own.
        /// </summary>
        [UnityTest]
        public IEnumerator AStockpileClickMoveClickReachesTheGameAndIsPublished()
        {
            GameObject root = Build();
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 0; i < 3; i++) yield return null;

                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var presenter = root.GetComponentInChildren<DesignatePresenter>();
                WorldSnapshot frame = boot.World!.Views.Current;
                Assume.That(frame.PawnCount, Is.GreaterThan(0));
                CellRef at = frame.Pawns[0].Cell;
                var anchor = new CellRef(at.X + 1, at.Z + 1, at.Y);
                var head = new CellRef(at.X + 3, at.Z + 3, at.Y);

                presenter!.Director.Tool = DesignateTool.Stockpile;
                MethodInfo click = typeof(DesignatePresenter).GetMethod("OnToolClick",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                click.Invoke(presenter, new object[] { anchor });
                yield return null;
                click.Invoke(presenter, new object[] { head });

                float until = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < until && boot.World.Views.Current.Stores.Length == 0)
                    yield return null;

                int stores = boot.World.Views.Current.Stores.Length;
                Debug.Log($"[StockpileDrag] click {anchor} then {head}: {stores} stockpile cells published");
                Assert.That(stores, Is.GreaterThan(0),
                    "two stockpile clicks beside a colonist published no zone cells");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Owner, 2026-09-23, after the wash came back: <i>"it's not rendering the stockpile graphic
        /// immediately - big delay and in another case didn't appear"</i>, and <i>"having more than
        /// one stockpile - seemed to not draw the other one"</i>. Two stockpiles are painted and the
        /// renderer's own chunk batches — what is actually submitted — are read every frame until
        /// both the wash (the ground chunk) and the outline (the store's chunk) are in them.
        ///
        /// <para>Counted from the frame the zone is <i>published</i>, not from the drag. The order
        /// lands on the next tick, and ticks are paced by real time: on the CI runner a frame is
        /// about a millisecond, so the next tick can be sixteen frames off and the same renderer
        /// read 3 frames on one run and 11 on the next purely on where the drag fell in the tick
        /// (2026-09-24, PR #180). The owner's report is about the renderer, so that is what is
        /// timed; the frames from the drag are logged beside it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TwoStockpilesAreBothDrawnPromptly()
        {
            GameObject root = Build();
            try
            {
                yield return new WaitForSecondsRealtime(0.3f);
                for (int i = 0; i < 30; i++) yield return null;

                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var presenter = root.GetComponentInChildren<DesignatePresenter>();
                var model = boot.Model!;
                var renderer = boot.Renderer!;
                var batches = (ChunkBatch?[])typeof(ChunkRenderer)
                    .GetField("_batches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
                WorldSnapshot frame = boot.World!.Views.Current;
                CellRef at = frame.Pawns[0].Cell;

                var boxes = new[]
                {
                    (new CellRef(at.X + 1, at.Z + 1, at.Y), new CellRef(at.X + 3, at.Z + 3, at.Y)),
                    (new CellRef(at.X - 6, at.Z - 2, at.Y), new CellRef(at.X - 4, at.Z, at.Y)),
                };

                presenter!.Director.Tool = DesignateTool.Stockpile;
                MethodInfo drag = typeof(DesignatePresenter).GetMethod("OnToolDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                long tick0 = boot.World.Views.Current.Tick;
                foreach (var (a, h) in boxes)
                {
                    drag.Invoke(presenter, new object[] { a, h });
                    presenter.Director.Tool = DesignateTool.Stockpile;
                }

                var seen = new int[boxes.Length];
                for (int i = 0; i < seen.Length; i++) seen[i] = -1;
                int frames = 0, published = -1;
                float until = Time.realtimeSinceStartup + 6f;
                while (Time.realtimeSinceStartup < until && System.Array.IndexOf(seen, -1) >= 0)
                {
                    yield return null;
                    frames++;
                    if (published < 0 && boot.World.Views.Current.Tick > tick0) published = frames;
                    for (int b = 0; b < boxes.Length; b++)
                    {
                        if (seen[b] >= 0) continue;
                        CellRef a = boxes[b].Item1;
                        int ground = model.Chunks.ChunkIndexOfCell(new CellRef(a.X, a.Z, a.Y - 1));
                        int store = model.Chunks.ChunkIndexOfCell(a);
                        if (Has(batches[ground], model, TintCode.IsStored) && Has(batches[store], model, TintCode.IsStoreEdge))
                            seen[b] = frames;
                    }
                }

                long ticks = boot.World.Views.Current.Tick - tick0;
                Debug.Log($"[StockpileDrag] drawn after frames [{string.Join(", ", seen)}] from the drag, " +
                          $"published on frame {published}, {ticks} ticks, " +
                          $"{boot.World.Views.Current.Stores.Length} store cells published, " +
                          $"tick {boot.World.Views.Current.Tick}");
                Assert.That(published, Is.GreaterThan(0), "no tick ran after the drag, so nothing was published");
                for (int b = 0; b < boxes.Length; b++)
                {
                    Assert.That(seen[b], Is.Not.EqualTo(-1),
                        $"stockpile {b + 1} of {boxes.Length} was never in the drawn batches, in six seconds");
                    Assert.That(seen[b] - published, Is.InRange(0, 10),
                        $"stockpile {b + 1} of {boxes.Length} was not in the drawn batches within ten frames " +
                        $"of being published (frame {seen[b]}, published on {published})");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static bool Has(ChunkBatch? batch, Odyssey.Presentation.World.WorldRenderModel model, System.Func<int, bool> tint)
        {
            if (batch == null) return false;
            foreach (InstanceBucket bucket in batch.Body)
                if (bucket.Count > 0 && tint(bucket.Tint) && !TintCode.IsTree(bucket.Tint)) return true;
            foreach (InstanceBucket bucket in batch.Roof)
                if (bucket.Count > 0 && tint(bucket.Tint) && !TintCode.IsTree(bucket.Tint)) return true;
            return false;
        }

        /// <summary>
        /// The same question on the board the owner plays: the wooded meadow at 120 x 120 x 16,
        /// where the meshing budget (11 chunks a frame, taken in walk order) has real competition.
        /// Logs how much re-meshing an idle colony asks for each frame, then how long two
        /// stockpiles take to reach the drawn batches.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoStockpilesAreDrawnPromptlyOnTheGameBoard()
        {
            GameObject root = Build(wooded: true);
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
                for (int i = 0; i < 60; i++) yield return null;

                var boot = root.GetComponentInChildren<OdysseyBootstrap>();
                var presenter = root.GetComponentInChildren<DesignatePresenter>();
                var model = boot.Model!;
                var renderer = boot.Renderer!;
                var batches = (ChunkBatch?[])typeof(ChunkRenderer)
                    .GetField("_batches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;

                // Idle pressure first: how much does a colony doing nothing in particular ask of
                // the budget, frame by frame?
                int idleMeshed = 0, idleDeferredMax = 0, idleFramesDeferring = 0;
                for (int i = 0; i < 120; i++)
                {
                    yield return null;
                    idleMeshed += renderer.ChunksMeshedThisFrame;
                    if (renderer.ChunksMeshDeferred > 0) idleFramesDeferring++;
                    idleDeferredMax = System.Math.Max(idleDeferredMax, renderer.ChunksMeshDeferred);
                }

                WorldSnapshot frame = boot.World!.Views.Current;
                CellRef at = frame.Pawns[0].Cell;
                presenter!.Director.Tool = DesignateTool.Stockpile;
                MethodInfo drag = typeof(DesignatePresenter).GetMethod("OnToolDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                drag.Invoke(presenter, new object[] { new CellRef(at.X + 1, at.Z + 1, at.Y), new CellRef(at.X + 3, at.Z + 3, at.Y) });
                presenter.Director.Tool = DesignateTool.Stockpile;
                drag.Invoke(presenter, new object[] { new CellRef(at.X - 6, at.Z - 2, at.Y), new CellRef(at.X - 4, at.Z, at.Y) });

                int frames = 0, meshed = 0, deferredMax = 0;
                int[] seen = { -1, -1 };
                int[] firstCell = { -1, -1 };
                float until = Time.realtimeSinceStartup + 10f;
                int stride = boot.World.Size.LayerStride;
                while (Time.realtimeSinceStartup < until && (seen[0] < 0 || seen[1] < 0))
                {
                    yield return null;
                    frames++;
                    meshed += renderer.ChunksMeshedThisFrame;
                    deferredMax = System.Math.Max(deferredMax, renderer.ChunksMeshDeferred);

                    // One cell per zone, off what was actually published: a tree in the box
                    // refuses its cell, so the anchor is not to be trusted.
                    var stores = boot.World.Views.Current.Stores;
                    for (int s = 0; s < stores.Length; s++)
                    {
                        int zone = stores[s].Zone;
                        if (zone < 2 && firstCell[zone] < 0) firstCell[zone] = stores[s].CellIndex;
                    }
                    for (int z = 0; z < 2; z++)
                    {
                        if (seen[z] >= 0 || firstCell[z] < 0) continue;
                        CellRef cell = boot.World.Size.FromIndex(firstCell[z]);
                        CellRef below = boot.World.Size.FromIndex(firstCell[z] - stride);
                        if (Has(batches[model.Chunks.ChunkIndexOfCell(below)], model, TintCode.IsStored)
                            && Has(batches[model.Chunks.ChunkIndexOfCell(cell)], model, TintCode.IsStoreEdge))
                            seen[z] = frames;
                    }
                }

                Debug.Log($"[StockpileDrag] game board: idle {idleMeshed} chunks meshed over 120 frames, " +
                          $"{idleFramesDeferring} frames deferring (max {idleDeferredMax}); after the drags " +
                          $"drawn at frames [{seen[0]}, {seen[1]}], {meshed} meshed over {frames} frames, " +
                          $"max deferred {deferredMax}, cells {firstCell[0]} / {firstCell[1]}, " +
                          $"{boot.World.Views.Current.Stores.Length} store cells");
                for (int z = 0; z < 2; z++)
                    Assert.That(seen[z], Is.InRange(0, 10),
                        $"stockpile {z + 1} was not in the drawn batches within ten frames (-1: never, in ten seconds)");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject Build(bool wooded = false)
        {
            var root = new GameObject("StockpileDrag");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);
            var boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.buildOnPlay = true;
            boot.sizeX = wooded ? 120 : 60;
            boot.sizeZ = wooded ? 120 : 60;
            boot.layers = wooded ? 16 : 8;
            boot.seed = 1;
            boot.barrenMap = !wooded;
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
