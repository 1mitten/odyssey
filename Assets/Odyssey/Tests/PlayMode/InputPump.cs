#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Processes queued input at the start of every frame, before any game component reads it.
    ///
    /// <para>This exists because of an ordering problem that is invisible until it is measured. A
    /// batch player never processes input by itself, so a test has to call
    /// <c>InputSystem.Update()</c> — but a test coroutine resumes <b>after</b> the frame's
    /// <c>Update</c> calls, so anything it delivers arrives too late for that frame, and a delta
    /// control such as the wheel is reset by the following update before the rig ever sees it.
    /// The trace read <c>[delivered scroll=1.00] f0: scroll=0.00</c> — delivered, then gone,
    /// without one line of game code having run in between.</para>
    ///
    /// <para>A component at the front of the execution order closes the gap: queue in one frame,
    /// processed at the top of the next, read by the rig in that same frame, reset at the top of
    /// the one after. That is the same shape as a real player, where the input system updates
    /// early in the loop, so the game's own code path is what the test exercises.</para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class InputPump : MonoBehaviour
    {
        /// <summary>
        /// What the wheel read immediately after the last update, and the frame it read it on.
        ///
        /// <para>A test cannot observe this for itself. A delta control accumulates during an
        /// update and is spent by the end of the frame, so a coroutine — which resumes after every
        /// <c>Update</c> has run — always reads zero and cannot tell "delivered and consumed" from
        /// "never delivered". Recording it here, at the one moment the game sees it, is what makes
        /// the harness able to fail loudly instead of quietly.</para>
        /// </summary>
        public float LastScrollY { get; private set; }

        public int LastScrollFrame { get; private set; } = -1;

        void Update()
        {
            InputSystem.Update();

            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                LastScrollY = scroll;
                LastScrollFrame = Time.frameCount;
            }
        }
    }
}
