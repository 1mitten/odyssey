#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The wake into a world (<c>docs/design/56-wake-up.md</c>) under the real bootstrap: the menu
    /// fades, the world is built behind the black, the player wakes blurred, warm and muffled with
    /// the colony held — and afterwards <b>nothing is left behind</b>: no volume, no filter on the
    /// listener, no muted listener, no camera offset, no held clock.
    ///
    /// <para>The curves and the rules are the fast tier's (<c>Tests/Hud/WakeTransitionTests</c>).
    /// What only Unity can say is that the engine pieces the shell hangs off them come and go, and
    /// that a load resumes at the speed it was saved at through the hold.</para>
    /// </summary>
    public class WakeUpTests
    {
        /// <summary>The standard shape, squeezed: a fifth of a second to close, 0.6 s of dream.</summary>
        static readonly WakeTiming Quick = new(
            closeSeconds: 0.2, darkFrames: 2, coverFrames: 3, wakeSeconds: 0.6,
            liftSeconds: 0.1, rushSeconds: 0.1, maxStepSeconds: 0.05);

        /// <summary>Long enough that a skip is obviously what ended it.</summary>
        static readonly WakeTiming Long = new(
            closeSeconds: 0.2, darkFrames: 2, coverFrames: 3, wakeSeconds: 30,
            liftSeconds: 0.1, rushSeconds: 0.1, maxStepSeconds: 0.05);

        static GameObject Rig(out OdysseyBootstrap boot, out SliceCameraRig rig, out HudShell shell, bool world)
        {
            GameObject root = RigWorld.BuildWithHud(out boot, out rig, out shell, buildOnPlay: world);
            // The harness camera has no ears; the game's does.
            if (rig.GetComponent<AudioListener>() == null) rig.gameObject.AddComponent<AudioListener>();
            return root;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>
        /// How long a loop waits for the passage, in <b>real seconds</b>, never in frames. The
        /// passage is timed in real seconds, and a batch run with nothing but the menu on screen
        /// and no frame cap draws a frame in well under a millisecond: 600 frames were over before
        /// a 0.2 s close had run, and all four of these tests failed on the runner with the passage
        /// still in <see cref="WakePhase.Closing"/> (2026-09-26).
        /// </summary>
        const float PassageSeconds = 15f;

        static float Deadline() => Time.realtimeSinceStartup + PassageSeconds;

        static bool AnythingLeftBehind(SliceCameraRig rig, out string what)
        {
            var left = new List<string>();
            if (GameObject.Find("WakeVolume") != null) left.Add("the dream's volume");
            if (rig.GetComponent<AudioLowPassFilter>() != null) left.Add("the low-pass filter");
            if (rig.GetComponent<AudioReverbFilter>() != null) left.Add("the reverb");
            if (!Mathf.Approximately(AudioListener.volume, 1f)) left.Add("a muted listener (" + AudioListener.volume + ")");
            if (rig.Settling) left.Add("the camera's settle");
            what = string.Join(", ", left);
            return left.Count > 0;
        }

        [UnityTest]
        public IEnumerator ANewColonyIsWokenIntoHeldAndNothingIsLeftBehind()
        {
            GameObject root = Rig(out OdysseyBootstrap boot, out SliceCameraRig rig, out HudShell shell, world: false);
            try
            {
                yield return Frames(8);
                shell.WakeTiming = Quick;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Frames(8);
                Assume.That(boot.Preferences.WakeUp, Is.True, "the wake-up is switched off on this machine");

                Assert.That(shell.Menu.Start(), Is.True);
                Assert.That(boot.HasSession, Is.False, "the world was built on the press, over the menu");

                var phases = new List<WakePhase>();
                bool sawDream = false, sawMuffle = false, sawSettle = false, tickMoved = false;
                int? heldTick = null;
                float deadline = Deadline();
                while (Time.realtimeSinceStartup < deadline && (shell.Wake.Active || phases.Count == 0))
                {
                    yield return null;
                    WakePhase phase = shell.Wake.Phase;
                    if (phases.Count == 0 || phases[^1] != phase) phases.Add(phase);
                    if (phase == WakePhase.Waking)
                    {
                        sawDream |= GameObject.Find("WakeVolume") != null;
                        sawMuffle |= rig.GetComponent<AudioLowPassFilter>() != null && AudioListener.volume < 1f;
                        sawSettle |= rig.Settling;
                        Assert.That(boot.ClockHeld, Is.True, "the colony ran while the player's eyes were closed");
                        heldTick ??= boot.World!.CurrentTick;
                        tickMoved |= boot.World!.CurrentTick != heldTick;
                    }
                }

                Assert.That(phases, Is.SupersetOf(new[]
                {
                    WakePhase.Closing, WakePhase.Dark, WakePhase.Covered, WakePhase.Waking, WakePhase.Done,
                }), "phases seen: " + string.Join(", ", phases));
                Assert.That(phases.IndexOf(WakePhase.Dark), Is.LessThan(phases.IndexOf(WakePhase.Covered)));
                Assert.That(phases.IndexOf(WakePhase.Covered), Is.LessThan(phases.IndexOf(WakePhase.Waking)));
                Assert.That(sawDream, Is.True, "no grade was laid over the dream");
                Assert.That(sawMuffle, Is.True, "the sound was never muffled");
                Assert.That(sawSettle, Is.True, "the camera never settled");
                Assert.That(tickMoved, Is.False, "the clock moved while it was held");

                yield return Frames(2); // Destroy lands at the end of a frame
                Assert.That(AnythingLeftBehind(rig, out string left), Is.False, "left behind: " + left);
                Assert.That(boot.ClockHeld, Is.False);
                Assert.That(boot.Keys.GameKeysLive, Is.True, "the keys were never given back");

                int tick = boot.World!.CurrentTick;
                yield return Frames(30);
                Assert.That(boot.World.CurrentTick, Is.GreaterThan(tick), "the colony never started");
                Assert.That(boot.World.GameSpeed, Is.EqualTo(1), "a new colony should wake at normal speed");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator ALoadWakesAtTheSpeedItWasSavedAt()
        {
            GameObject root = Rig(out OdysseyBootstrap boot, out SliceCameraRig rig, out HudShell shell, world: true);
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "odyssey-wake-load-" + System.Guid.NewGuid());
            try
            {
                yield return Frames(8);
                Assume.That(boot.Preferences.WakeUp, Is.True, "the wake-up is switched off on this machine");
                boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 3));
                yield return Frames(10);
                Assume.That(boot.World.GameSpeed, Is.EqualTo(3), "the speed never reached the world");

                System.IO.Directory.CreateDirectory(folder);
                string path = System.IO.Path.Combine(folder, "wake" + SaveCatalogue.Extension);
                boot.SaveSession(path);
                boot.TeardownSession();
                yield return Frames(8);

                shell.WakeTiming = Quick;
                Assert.That(shell.EnterWorld(() => boot.LoadSession(path)), Is.True);
                Assert.That(shell.EnterWorld(() => boot.LoadSession(path)), Is.False, "a second press started a second load");

                bool coveredAfterBuild = false, heldInDream = false;
                float deadline = Deadline();
                while (Time.realtimeSinceStartup < deadline && shell.Wake.Active)
                {
                    yield return null;
                    if (boot.HasSession && shell.Wake.Phase == WakePhase.Covered)
                        coveredAfterBuild |= shell.CurtainUp;
                    if (shell.Wake.Phase == WakePhase.Waking) heldInDream |= boot.ClockHeld;
                }
                Assert.That(shell.Wake.Phase, Is.EqualTo(WakePhase.Done));
                Assert.That(coveredAfterBuild, Is.True, "the loaded world was not behind the veil after its build");
                Assert.That(heldInDream, Is.True, "a loaded colony ran while the player's eyes were closed");

                yield return Frames(30);
                Assert.That(boot.World!.GameSpeed, Is.EqualTo(3),
                    "the hold overwrote the saved speed — it must be a gate on the clock, never a speed");
                Assert.That(AnythingLeftBehind(rig, out string left), Is.False, "left behind: " + left);
            }
            finally
            {
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, recursive: true);
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator ASkipWakesAtOnce()
        {
            GameObject root = Rig(out OdysseyBootstrap boot, out SliceCameraRig rig, out HudShell shell, world: false);
            try
            {
                yield return Frames(8);
                Assume.That(boot.Preferences.WakeUp, Is.True, "the wake-up is switched off on this machine");
                shell.WakeTiming = Long;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Frames(8);
                shell.Menu.Start();

                float deadline = Deadline();
                while (Time.realtimeSinceStartup < deadline && shell.Wake.Phase != WakePhase.Waking) yield return null;
                Assert.That(shell.Wake.Phase, Is.EqualTo(WakePhase.Waking));
                yield return Frames(5);
                Assert.That(boot.ClockHeld, Is.True);

                shell.SkipWake();
                yield return null;
                Assert.That(boot.ClockHeld, Is.False, "the colony was still held after the player woke");

                float until = Time.realtimeSinceStartup + 1f;
                while (shell.Wake.Active && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(shell.Wake.Phase, Is.EqualTo(WakePhase.Done), "a skip did not end a thirty-second dream");
                yield return Frames(2);
                Assert.That(AnythingLeftBehind(rig, out string left), Is.False, "left behind: " + left);
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator WithTheWakeUpOffThereIsOnlyAFade()
        {
            GameObject root = Rig(out OdysseyBootstrap boot, out SliceCameraRig rig, out HudShell shell, world: false);
            bool was = boot.Preferences.WakeUp;
            try
            {
                yield return Frames(8);
                // Written through the preferences, which may be the machine's own store: put back
                // in the finally, whatever happens.
                boot.Preferences.SetWakeUp(false);
                shell.WakeTiming = Quick;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Frames(8);
                shell.Menu.Start();

                bool anyDream = false, anyHold = false;
                float deadline = Deadline();
                while (Time.realtimeSinceStartup < deadline && shell.Wake.Active)
                {
                    yield return null;
                    anyDream |= GameObject.Find("WakeVolume") != null || rig.GetComponent<AudioLowPassFilter>() != null
                                || rig.Settling || shell.WakeBlurNow != null;
                    anyHold |= boot.ClockHeld;
                }
                Assert.That(boot.HasSession, Is.True, "the plain fade built no world");
                Assert.That(shell.Wake.Phase, Is.EqualTo(WakePhase.Done));
                Assert.That(anyDream, Is.False, "the dream ran with the wake-up switched off");
                Assert.That(anyHold, Is.False, "the clock was held with the wake-up switched off");
            }
            finally
            {
                boot.Preferences.SetWakeUp(was);
                Object.Destroy(root);
            }
        }
    }
}
