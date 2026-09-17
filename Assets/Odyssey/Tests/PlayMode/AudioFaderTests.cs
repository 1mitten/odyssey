#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.Ui;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The volume faders in B17: one per bus, and the three parts of each one agree about where
    /// the value is.
    ///
    /// <para>They were seven clickable rungs until the owner asked for a drag (2026-09-17). The
    /// arithmetic of the track — what a held dB means as a position, and what a position means as
    /// a dB — is <c>SettingsDirector</c>'s and is proved in the fast tier. What can only be asked
    /// with Unity running is whether the shell wired the three parts to it: a fill, a handle and a
    /// readout that must never disagree, because a handle sitting somewhere other than the number
    /// beside it is the whole failure mode of a control like this.</para>
    ///
    /// <para><b>What this does not test is the drag itself.</b> Synthesising a captured pointer
    /// gesture through UI Toolkit is not something this suite can do today, so whether the grab
    /// feels right — whether 96 pixels is enough travel, whether the handle is easy to catch —
    /// is a question for somebody looking at it, and is written down as such.</para>
    /// </summary>
    public class AudioFaderTests
    {
        [UnityTest]
        public IEnumerator EveryBusHasAFaderAndItFollowsItsValue()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out _, out _);
            try
            {
                yield return RigWorld.WarmUp();

                var doc = root.GetComponentInChildren<UIDocument>();
                Assert.That(boot.Directors, Is.Not.Null, "the shell never got its directors");
                SettingsDirector settings = boot.Directors!.Settings;

                foreach (SettingsBus bus in SettingsDirector.Buses)
                {
                    string key = SettingsDirector.VolumeKey(bus);
                    VisualElement? fader = doc.rootVisualElement.Q(key);
                    Assert.That(fader, Is.Not.Null, $"{bus} has no fader");

                    VisualElement? handle = fader!.Q(className: "fader__handle");
                    VisualElement? fill = fader.Q(className: "fader__fill");
                    Assert.That(handle, Is.Not.Null, $"{bus}'s fader has no handle");
                    Assert.That(fill, Is.Not.Null, $"{bus}'s fader has no fill");

                    // Nothing inside the control may take the pointer: the whole fader is the
                    // drag target, so a press anywhere on it starts a drag rather than landing on
                    // a mark and doing nothing.
                    Assert.That(handle!.pickingMode, Is.EqualTo(PickingMode.Ignore));
                    Assert.That(fill!.pickingMode, Is.EqualTo(PickingMode.Ignore));
                }

                // A value the old ladder could not have held, to make the point that the rungs are
                // marks now rather than the set of legal answers.
                const int Db = -37;
                settings.SetBusDb(SettingsBus.Music, Db);
                yield return null;

                string musicKey = SettingsDirector.VolumeKey(SettingsBus.Music);
                VisualElement fader2 = doc.rootVisualElement.Q(musicKey);
                Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(Db),
                    "the director rounded a value the fader is allowed to hold");

                float want = SettingsDirector.VolumeFraction(Db) * 100f;
                Assert.That(fader2.Q(className: "fader__handle").style.left.value.value, Is.EqualTo(want).Within(0.01f),
                    "the handle is not where the value says it is");
                Assert.That(fader2.Q(className: "fader__fill").style.width.value.value, Is.EqualTo(want).Within(0.01f),
                    "the fill and the handle disagree about the value");

                var readout = doc.rootVisualElement.Q<Label>(className: "fader__value");
                Assert.That(readout, Is.Not.Null, "no fader has a readout");

                // The ends, where a fader is most often left: dragged right off the bottom is
                // Mute and says so in a word, because silence is not a number.
                settings.SetBusDb(SettingsBus.Music, SettingsDirector.VolumeDbAt(0f));
                yield return null;
                Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(SettingsDirector.SilenceDb));
                Assert.That(fader2.Q(className: "fader__handle").style.left.value.value, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
