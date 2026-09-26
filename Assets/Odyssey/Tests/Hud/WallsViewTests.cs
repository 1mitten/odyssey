#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// When the walls are lowered (design 42 §3, §6): the player's choice, overruled by build mode
    /// and by nothing else.
    /// </summary>
    public class WallsViewTests
    {
        [Test]
        public void TheWallsAreLoweredOnlyWhenChosen()
        {
            Assert.That(WallsView.Lowered(chosen: true, paletteOpen: false, DesignateTool.None), Is.True);
            Assert.That(WallsView.Lowered(chosen: false, paletteOpen: false, DesignateTool.None), Is.False);
        }

        [Test]
        public void AnOpenPaletteStandsTheWallsUpWithNothingArmed()
        {
            Assert.That(WallsView.Lowered(chosen: true, paletteOpen: true, DesignateTool.None), Is.False,
                "the palette open is build mode even before a building is picked");
        }

        /// <summary>
        /// Every tool, one by one, so a tool added later is decided rather than defaulted: this
        /// fails on a new member until somebody says which side of the line it stands.
        /// </summary>
        [Test]
        public void OnlyBuildingAndDeconstructingStandTheWallsUp()
        {
            foreach (DesignateTool tool in Enum.GetValues(typeof(DesignateTool)))
            {
                bool expected = tool switch
                {
                    DesignateTool.Build => false,
                    DesignateTool.Deconstruct => false,
                    DesignateTool.None or DesignateTool.Mine or DesignateTool.Fell or DesignateTool.Harvest or DesignateTool.Cancel
                        or DesignateTool.GrowZone or DesignateTool.Stockpile or DesignateTool.RemoveConduit => true,
                    _ => throw new AssertionException(
                        $"{tool} is a new tool: decide whether it is build mode (design 42 §2) and add it here"),
                };
                Assert.That(WallsView.Lowered(chosen: true, paletteOpen: false, tool), Is.EqualTo(expected), tool.ToString());
            }
        }

        [Test]
        public void TheOptionStartsOnAndIsNamedInTheRegistry()
        {
            Assert.That(SettingsDirector.DefaultOn(GraphicsOption.WallsDown), Is.True, "owner: on by default");
            Assert.That(new SettingsDirector().IsOn(GraphicsOption.WallsDown), Is.True);
            Assert.That(SettingsDirector.KeyOf(GraphicsOption.WallsDown), Is.EqualTo(SettingsDirector.WallsDownKey));
            Assert.That(Registry.Label(SettingsDirector.WallsDownKey), Is.Not.Empty);
        }

        [Test]
        public void HTogglesTheWalls()
        {
            var hotkeys = new HotkeyDirector();
            Assert.That(hotkeys.Key(HotkeyAction.WallsDown, 0), Is.EqualTo(HudKey.H));
            Assert.That(Registry.Label(HotkeyDirector.KeyOf(HotkeyAction.WallsDown)), Is.Not.Empty);
        }
    }
}
