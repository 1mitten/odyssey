#nullable enable
using System.Collections;
using System.Text;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>Temporary: what does the device actually report, frame by frame?</summary>
    public class ZInputDiagnosticTests
    {
        [UnityTest]
        public IEnumerator Trace()
        {
            var log = new StringBuilder();
            using var harness = new MouseHarness();
            Mouse mouse = harness.Device;
            log.Append($"focused={Application.isFocused} batch={Application.isBatchMode} ");
            log.Append($"device={mouse.name} enabled={mouse.enabled} added={mouse.added} ");
            log.Append($"bg={InputSystem.settings.backgroundBehavior} update={InputSystem.settings.updateMode}; ");

            GameObject root = RigWorld.Build(out OdysseyBootstrap _, out SliceCameraRig rig);
            try
            {
                yield return RigWorld.WarmUp();
                yield return RigWorld.SettleCamera(rig);
                log.Append($"settled d={rig.distance:F3}; ");

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = new Vector2(320f, 240f),
                        scroll = new Vector2(0f, 1f),
                    });
                    log.Append($"[queued#{attempt}] ");
                    if (attempt == 1)
                    {
                        InputSystem.Update();
                        log.Append($"[after manual Update: scroll={mouse.scroll.ReadValue().y:F2} " +
                                   $"pos={mouse.position.ReadValue().x:F0}] ");
                    }

                    for (int frame = 0; frame < 4; frame++)
                    {
                        yield return null;
                        log.Append($"f{frame}: on={mouse.enabled} scroll={mouse.scroll.ReadValue().y:F2} " +
                                   $"pos={mouse.position.ReadValue().x:F0} d={rig.distance:F3}; ");
                    }
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
