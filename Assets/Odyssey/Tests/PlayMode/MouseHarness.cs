#nullable enable
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A mouse that works in a batch PlayMode run (OQ-40).
    ///
    /// <para><b>Two things stop mouse input dead in a headless test, and both are silent.</b></para>
    ///
    /// <para>First, <c>Mouse.current</c> is <b>null</b>: the player has no window and therefore no
    /// pointer device. Queueing state at it does nothing, and <c>SliceCameraRig.ReadMouse</c>
    /// returns immediately when the device is null, so no assertion downstream can ever be
    /// reached. An earlier attempt at this row queued events into that void, wrote three tests
    /// that passed without any input arriving, and deleted them rather than keep them.</para>
    ///
    /// <para>Second — and this is the one that took measuring rather than reading — a device added
    /// by hand is created <b>disabled</b>. The input system's <c>backgroundBehavior</c> defaults to
    /// <c>ResetAndDisableNonBackgroundDevices</c>, a batch player is never focused, so the mouse is
    /// disabled the moment focus is evaluated and every queued event is dropped. The trace that
    /// found it read: <c>focused=False batch=True device=Mouse enabled=False added=True
    /// bg=ResetAndDisableNonBackgroundDevices</c>, with <c>scroll=0.00</c> on every frame after
    /// every queued event.</para>
    ///
    /// <para>It also explains why the first attempt at a fix looked like it worked: between adding
    /// the device and focus being applied there is a window in which events do land, so whichever
    /// test ran first sometimes passed and the same test failed when it ran second. A harness that
    /// works four times in five is worse than one that never works, because it teaches you to
    /// trust it.</para>
    ///
    /// <para>Third, and only visible once the first two were fixed: <b>the player loop never runs
    /// an input update in a batch run</b>. With the device present and enabled a queued event
    /// still did nothing — <c>scroll=0.00 pos=0</c> on every frame after every event — and one
    /// manual <c>InputSystem.Update()</c> delivered it instantly (<c>scroll=1.00 pos=320</c>).
    /// Events queue and are never processed, because nothing in a windowless player asks for
    /// them. So the harness pumps the update itself.</para>
    ///
    /// <para>That has a consequence worth knowing: since nothing else updates input, a delta
    /// control is never cleared either. A scroll delivered once would stay applied and zoom the
    /// camera on every subsequent frame. Each gesture below therefore ends by delivering the
    /// neutral state, so a notch is one notch rather than a stuck wheel.</para>
    ///
    /// <para>So this sets <c>IgnoreFocus</c> for the run, enables the device, drives the updates,
    /// and <b>asserts</b> both that the device is enabled and that the state arrived — every
    /// silent failure above becomes a loud one. The settings are put back afterwards, because
    /// they are global and the rest of the PlayMode suite shares them.</para>
    /// </summary>
    public sealed class MouseHarness : IDisposable
    {
        readonly InputSettings.BackgroundBehavior _behaviourBefore;
        readonly bool _deviceWasAlreadyThere;

        public MouseHarness()
        {
            _behaviourBefore = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            Mouse? existing = Mouse.current;
            _deviceWasAlreadyThere = existing != null;
            Device = existing ?? InputSystem.AddDevice<Mouse>();

            if (!Device.enabled) InputSystem.EnableDevice(Device);

            Assert.That(Device.enabled, Is.True,
                "the mouse device is disabled, so every event queued at it will be dropped in " +
                "silence. A batch player is never focused; see MouseHarness for the whole story.");
        }

        public Mouse Device { get; }

        /// <summary>
        /// Turn the wheel over a point: one notch, delivered for exactly one frame.
        /// </summary>
        public IEnumerator Scroll(float notches, Vector2 at)
        {
            Deliver(new MouseState { position = at, scroll = new Vector2(0f, notches) });
            Assert.That(Device.scroll.ReadValue().y, Is.EqualTo(notches).Within(0.001f),
                "the wheel state did not reach the device, so nothing downstream can be tested");
            yield return null;

            // Clear the wheel, or it stays turned: nothing else updates input here.
            Deliver(new MouseState { position = at });
            yield return null;
        }

        /// <summary>Move the pointer without pressing anything.</summary>
        public IEnumerator MoveTo(Vector2 at)
        {
            Deliver(new MouseState { position = at });
            Assert.That(Device.position.ReadValue().x, Is.EqualTo(at.x).Within(0.5f),
                "the pointer position did not reach the device");
            yield return null;
        }

        /// <summary>Press and release the left button at a point, a frame apart.</summary>
        public IEnumerator Click(Vector2 at)
        {
            Deliver(new MouseState { position = at }.WithButton(MouseButton.Left));
            yield return null;
            Deliver(new MouseState { position = at });
            yield return null;
        }

        /// <summary>
        /// Queue one state and process it. The <c>Update</c> is the whole trick: without it the
        /// event sits in the buffer for the rest of the run, because a batch player's loop never
        /// asks the input system to process anything.
        /// </summary>
        void Deliver(MouseState state)
        {
            InputSystem.QueueStateEvent(Device, state);
            InputSystem.Update();
        }

        public void Dispose()
        {
            if (!_deviceWasAlreadyThere && Device.added) InputSystem.RemoveDevice(Device);
            InputSystem.settings.backgroundBehavior = _behaviourBefore;
        }
    }
}
