#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The quality presets and the grass ladders (<c>docs/design/38-meadow-overhaul.md</c> §9, M6;
    /// <c>27-graphics-settings.md</c>).
    ///
    /// <para>The rule under test is that a preset is <b>read off the levers, never remembered</b>:
    /// choosing one sets every lever it owns through that lever's own setter, and moving any one of
    /// them by hand is Custom because that is what the levers then say.</para>
    /// </summary>
    public class QualityPresetTests
    {
        /// <summary>A machine that has never been told, as the presenter leaves it: the pipeline
        /// asset's 250 m shadow distance seeded, which snaps to the top rung.</summary>
        static SettingsDirector Fresh()
        {
            var settings = new SettingsDirector();
            settings.SeedValue(GraphicsLadder.ShadowDistance, 250);
            return settings;
        }

        [Test]
        public void AFreshMachineIsOnHighBecauseHighIsWhatTheGameShipsWith()
        {
            Assert.That(Fresh().Preset, Is.EqualTo(QualityPreset.High),
                "choosing High on a new install would change something, so High is not what ships");
        }

        [Test]
        public void ChoosingAPresetPutsEveryLeverItOwnsWhereItSays()
        {
            foreach (QualityPreset preset in SettingsDirector.Presets)
            {
                if (preset == QualityPreset.Custom) continue;
                SettingsDirector settings = Fresh();
                settings.ApplyPreset(preset);

                foreach (GraphicsOption option in SettingsDirector.PresetOptions)
                    Assert.That(settings.IsOn(option), Is.EqualTo(SettingsDirector.PresetOn(preset, option)),
                        $"{preset} left {option} where it was");
                foreach (GraphicsLadder ladder in SettingsDirector.PresetLadders)
                    Assert.That(settings.Value(ladder), Is.EqualTo(SettingsDirector.PresetRung(preset, ladder)),
                        $"{preset} left {ladder} where it was");
                Assert.That(settings.Preset, Is.EqualTo(preset), $"{preset} applied does not read as {preset}");
            }
        }

        [Test]
        public void EveryPresetRungIsARungItsLadderOffers()
        {
            foreach (QualityPreset preset in SettingsDirector.Presets)
                foreach (GraphicsLadder ladder in SettingsDirector.PresetLadders)
                    Assert.That(SettingsDirector.RungsOf(ladder), Does.Contain(SettingsDirector.PresetRung(preset, ladder)),
                        $"{preset} asks {ladder} for a rung the panel cannot show");
        }

        /// <summary>Four presets that could not be told apart would be one preset with four names,
        /// and the row would light whichever came first.</summary>
        [Test]
        public void NoTwoPresetsAreTheSame()
        {
            var seen = new HashSet<string>();
            foreach (QualityPreset preset in SettingsDirector.Presets)
            {
                if (preset == QualityPreset.Custom) continue;
                var signature = new System.Text.StringBuilder();
                foreach (GraphicsOption option in SettingsDirector.PresetOptions)
                    signature.Append(SettingsDirector.PresetOn(preset, option) ? '1' : '0');
                foreach (GraphicsLadder ladder in SettingsDirector.PresetLadders)
                    signature.Append(',').Append(SettingsDirector.PresetRung(preset, ladder));
                Assert.That(seen.Add(signature.ToString()), Is.True, $"{preset} is another preset under a second name");
            }
        }

        [Test]
        public void MovingAnyOwnedLeverByHandIsCustomAndMovingItBackIsNot()
        {
            SettingsDirector settings = Fresh();
            settings.ApplyPreset(QualityPreset.Medium);

            settings.Toggle(GraphicsOption.Shadows);
            Assert.That(settings.Preset, Is.EqualTo(QualityPreset.Custom));
            settings.Toggle(GraphicsOption.Shadows);
            Assert.That(settings.Preset, Is.EqualTo(QualityPreset.Medium), "the levers are Medium's again");

            settings.SetValue(GraphicsLadder.VegetationDensity, 300);
            Assert.That(settings.Preset, Is.EqualTo(QualityPreset.Custom));
        }

        /// <summary>The screen's own business — VSync, the cap, the window — is not a preset's, and
        /// moving it must not knock the row off the preset the player chose.</summary>
        [Test]
        public void ALeverNoPresetOwnsLeavesThePresetAlone()
        {
            SettingsDirector settings = Fresh();
            settings.ApplyPreset(QualityPreset.Low);
            settings.SetValue(GraphicsLadder.VSync, 0);
            settings.SetValue(GraphicsLadder.FrameCap, 144);
            settings.Toggle(GraphicsOption.SeeThrough);
            Assert.That(settings.Preset, Is.EqualTo(QualityPreset.Low));
        }

        [Test]
        public void ChoosingCustomDoesNothing()
        {
            SettingsDirector settings = Fresh();
            int raised = 0;
            settings.OptionChanged += _ => raised++;
            settings.LadderChanged += _ => raised++;
            settings.ApplyPreset(QualityPreset.Custom);
            Assert.That(raised, Is.Zero);
        }

        /// <summary>A preset is written down lever by lever, so a restart comes back on it — the preset
        /// itself is never stored, and does not need to be.</summary>
        [Test]
        public void APresetSurvivesARestartBecauseItsLeversDo()
        {
            var store = new FakeSettingsStore();
            SettingsDirector settings = Fresh();
            settings.UseStore(store);
            settings.ApplyPreset(QualityPreset.Low);

            SettingsDirector restarted = Fresh();
            restarted.UseStore(store);
            Assert.That(restarted.Preset, Is.EqualTo(QualityPreset.Low));
        }

        [Test]
        public void TheGrassLaddersSnapAndSayWhatTheyAre()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Value(GraphicsLadder.VegetationDensity), Is.EqualTo(SettingsDirector.ShippedVegetation));
            Assert.That(settings.Value(GraphicsLadder.GrassDistance), Is.EqualTo(SettingsDirector.Unlimited),
                "grass has always been drawn everywhere the camera looks");

            settings.SetValue(GraphicsLadder.VegetationDensity, 70);
            Assert.That(settings.Value(GraphicsLadder.VegetationDensity), Is.EqualTo(60), "70 snaps to the nearest rung");
            settings.SetValue(GraphicsLadder.GrassDistance, 400);
            Assert.That(settings.Value(GraphicsLadder.GrassDistance), Is.EqualTo(250));

            foreach (GraphicsLadder ladder in SettingsDirector.DetailLadders)
                foreach (int rung in SettingsDirector.RungsOf(ladder))
                {
                    Assert.That(SettingsDirector.RungLabel(ladder, rung), Is.Not.Empty, $"{ladder} {rung} has no word");
                    Assert.That(SettingsDirector.RungTooltip(ladder, rung), Is.Not.Empty, $"{ladder} {rung} says nothing on hover");
                }
            Assert.That(SettingsDirector.RungLabel(GraphicsLadder.VegetationDensity, 0), Is.EqualTo("Off"),
                "the bottom rung is what the grass toggle's off used to be");
        }

        /// <summary>
        /// The grass toggle's answer is carried over once: a player who had the grass off still has
        /// it off, a player who had it on is untouched, and a density stored since outranks both.
        /// </summary>
        [Test]
        public void TheOldGrassToggleIsCarriedOverOnce()
        {
            var off = new FakeSettingsStore();
            off.Preset(SettingsDirector.LegacyGrassKey, false);
            SettingsDirector settings = Fresh();
            settings.UseStore(off);
            Assert.That(settings.Value(GraphicsLadder.VegetationDensity), Is.Zero, "grass that was off came back on");

            var on = new FakeSettingsStore();
            on.Preset(SettingsDirector.LegacyGrassKey, true);
            settings = Fresh();
            settings.UseStore(on);
            Assert.That(settings.Value(GraphicsLadder.VegetationDensity), Is.EqualTo(SettingsDirector.ShippedVegetation),
                "a stored 'on' read as a density would snap to bare ground");

            var since = new FakeSettingsStore();
            since.Preset(SettingsDirector.LegacyGrassKey, false);
            since.Preset(SettingsDirector.VegetationKey, 150);
            settings = Fresh();
            settings.UseStore(since);
            Assert.That(settings.Value(GraphicsLadder.VegetationDensity), Is.EqualTo(150));
        }

        [Test]
        public void TheStoreIsOnlyThereOnceAttached()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.HasStore, Is.False,
                "a harness with no presenter must keep its own fields, so the root asks this first");
            settings.UseStore(new FakeSettingsStore());
            Assert.That(settings.HasStore, Is.True);
        }
    }
}
