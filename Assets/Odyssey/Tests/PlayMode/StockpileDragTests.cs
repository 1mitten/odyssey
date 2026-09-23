#nullable enable
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
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

                for (int i = 0; i < 10; i++) yield return null;

                int stores = boot.World.Views.Current.Stores.Length;
                Debug.Log($"[StockpileDrag] box {anchor} to {head} from a colonist at {at}: " +
                          $"{stores} stockpile cells published");
                Assert.That(stores, Is.GreaterThan(0),
                    "a stockpile drag beside a colonist published no zone cells, so the fault is " +
                    "after the pointer: the director, the presenter's submit or the simulation");
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

                for (int i = 0; i < 10; i++) yield return null;

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

        static GameObject Build()
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
