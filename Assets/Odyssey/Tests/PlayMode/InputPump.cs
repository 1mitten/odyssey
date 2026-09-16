#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Delivers mouse state at the top of a frame, before any game component reads it.
    ///
    /// <para><b>Why a component rather than a couple of lines in a test.</b> Three separate
    /// timing facts have to line up at once, and each of them fails silently on its own:</para>
    ///
    /// <list type="number">
    /// <item>A batch player never processes input by itself, so something must call
    /// <c>InputSystem.Update()</c>.</item>
    /// <item>A queued event does not survive to the next frame's update: queueing in one frame and
    /// updating in the next delivered nothing, measured. Queue and update have to be one act.</item>
    /// <item>A test coroutine resumes <i>after</i> every <c>Update</c> has run, so anything it
    /// delivers is already too late for that frame — and a delta control such as the wheel is
    /// spent by the time the coroutine can read it, which is why a test cannot tell "delivered and
    /// consumed" from "never delivered" by looking at the device itself.</item>
    /// </list>
    ///
    /// <para>Posting state here satisfies all three: the pump runs first in the frame
    /// (<c>DefaultExecutionOrder</c> -10000), queues and updates in one go, and records what the
    /// device read immediately afterwards. The rig then reads the same value later in the same
    /// frame through its own ordinary code path, and the recording is what lets the harness fail
    /// loudly when nothing arrived.</para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class InputPump : MonoBehaviour
    {
        readonly Queue<MouseState> _pending = new Queue<MouseState>();

        /// <summary>The device posted state is delivered to.</summary>
        public Mouse? Device { get; set; }

        /// <summary>What the wheel read immediately after the last delivery, and on which frame.</summary>
        public float LastScrollY { get; private set; }

        public int LastScrollFrame { get; private set; } = -1;

        /// <summary>Deliver this state at the top of the next frame.</summary>
        public void Post(MouseState state) => _pending.Enqueue(state);

        void Update()
        {
            if (Device == null || _pending.Count == 0) return;

            // One state per frame: a wheel notch is a notch, not a stuck wheel.
            MouseState state = _pending.Dequeue();
            InputSystem.QueueStateEvent(Device, state);
            InputSystem.Update();

            LastScrollY = Device.scroll.ReadValue().y;
            LastScrollFrame = Time.frameCount;
        }
    }
}
