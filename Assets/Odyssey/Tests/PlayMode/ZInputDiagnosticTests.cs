#nullable enable
using System.Collections;
using System.Text;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>Temporary: which side of the seam drops the wheel?</summary>
    public class ZInputDiagnosticTests
    {
        [UnityTest]
        public IEnumerator Trace()
        {
            var log = new StringBuilder();
            using var mouse = new MouseHarness();

            GameObject root = RigWorld.Build(out OdysseyBootstrap _, out SliceCameraRig rig);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);

                log.Append($"current==harness:{ReferenceEquals(Mouse.current, mouse.Device)} ");
                log.Append($"settled d={rig.distance:F3} target={rig.TargetDistance:F3} ");
                log.Append($"zoomSpeed={rig.zoomSpeed} enabled={rig.isActiveAndEnabled}; ");

                InputSystem.QueueStateEvent(mouse.Device, new UnityEngine.InputSystem.LowLevel.MouseState
                {
                    position = new Vector2(320f, 240f),
                    scroll = new Vector2(0f, 1f),
                });
                InputSystem.Update();
                log.Append($"[delivered scroll={mouse.Device.scroll.ReadValue().y:F2} " +
                           $"current={(Mouse.current == null ? "null" : Mouse.current.scroll.ReadValue().y.ToString("F2"))}] ");

                for (int frame = 0; frame < 5; frame++)
                {
                    yield return null;
                    log.Append($"f{frame}: scroll={mouse.Device.scroll.ReadValue().y:F2} " +
                               $"target={rig.TargetDistance:F3} d={rig.distance:F3}; ");
                }

                Assert.Fail(log.ToString());
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
