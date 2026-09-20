#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The three directors and the rules between them, with no scene and no camera: what a click
    /// means, when a selection is dropped, how the slice clamps, and what a roster choice does in
    /// what order.
    /// </summary>
    public class SelectionDirectorTests
    {
        static WorldSnapshot FrameWith(PawnId pawn, CellRef at, ThingId thing = default, CellRef thingAt = default)
        {
            var snapshot = Frame.Write();
            if (pawn.IsValid) snapshot.AddPawn(new PawnView(pawn, at, 500, 500, 50, JobHandle.Haul));
            if (thing.IsValid) snapshot.AddThing(new ThingView(thing, thingAt, ItemHandle.Meal, 0));
            return snapshot;
        }

        [Test]
        public void AColonistWinsOverTheItemTheyStandOn()
        {
            var cell = new CellRef(3, 3, 1);
            var snapshot = FrameWith(new PawnId(1), cell, new ThingId(9), cell);
            var director = new SelectionDirector();
            var changes = new List<SelectionChange>();
            director.Changed += changes.Add;

            director.Pick(cell, new PawnId(1), snapshot);

            Assert.That(director.Pawn, Is.EqualTo(new PawnId(1)));
            Assert.That(director.HasThing, Is.False, "the crate is still there when they walk off it");
            Assert.That(director.Cell, Is.Null,
                "a colonist is the whole selection: the cell under their feet is not part of it");
            Assert.That(changes, Is.EqualTo(new[] { SelectionChange.Picked }));
        }

        /// <summary>
        /// The line above used to read <c>Is.EqualTo(cell)</c>, so the dual state was deliberate
        /// and this suite documented it. It came back on 2026-09-16 as a playtest report: clicking
        /// a colonist chopping a tree put a highlight on a cell near them and selected the colonist
        /// anyway. Both halves are this — the cursor preferred the pawn and drew the right bracket
        /// most of the time, but the cell went on being part of the selection underneath, so every
        /// other reader saw it, and any frame the pawn lookup missed fell through to the cell tier
        /// and bracketed the tree instead.
        /// </summary>
        [Test]
        public void APickOnAColonistDoesNotAlsoSelectTheCellTheyStandIn()
        {
            var cell = new CellRef(3, 3, 1);
            var snapshot = FrameWith(new PawnId(1), cell, ThingId.None, default);
            var director = new SelectionDirector();

            director.Pick(cell, new PawnId(1), snapshot);

            Assert.That(director.HasPawn, Is.True);
            Assert.That(director.Cell, Is.Null,
                "the cell is still part of the selection, so a cursor can still draw it instead " +
                "of the colonist and every other reader still sees a cell that was not picked");
        }

        [Test]
        public void APickWithNoColonistResolvesTheThingInTheCell()
        {
            var cell = new CellRef(4, 4, 1);
            var snapshot = FrameWith(PawnId.None, default, new ThingId(9), cell);
            var director = new SelectionDirector();

            director.Pick(cell, PawnId.None, snapshot);

            Assert.That(director.HasPawn, Is.False);
            Assert.That(director.Thing, Is.EqualTo(new ThingId(9)));
            Assert.That(director.ThingDef, Is.EqualTo(ItemHandle.Meal));
        }

        /// <summary>
        /// The playtest report behind the second look in <c>ThingAt</c> (owner, 2026-09-17): a
        /// pile resting on bare ground sits in the air cell above the solid block the picker
        /// resolves to, so matching the picked cell alone made every such pile unselectable —
        /// the click fell through to the cell pane and read as "wood cannot be selected".
        /// </summary>
        [Test]
        public void APickOnGroundUnderAPileSelectsThePileRestingOnIt()
        {
            // The picker hands back the solid ground block; the pile is one cell up.
            var ground = new CellRef(5, 5, 0);
            var pileAt = new CellRef(5, 5, 1);
            var snapshot = FrameWith(PawnId.None, default, new ThingId(4), pileAt);
            var director = new SelectionDirector();

            director.Pick(ground, PawnId.None, snapshot);

            Assert.That(director.HasThing, Is.True,
                "a thing resting on the block the player clicked is the thing the player clicked");
            Assert.That(director.Thing, Is.EqualTo(new ThingId(4)));
            Assert.That(director.ThingDef, Is.EqualTo(ItemHandle.Meal));
        }

        [Test]
        public void APickAtTheTopOfTheWorldDoesNotLookForAThingAboveIt()
        {
            // A thing in the topmost cell is found in its own right; the guard being tested is
            // the layer above it, which does not exist.
            var top = new CellRef(5, 5, 3);
            var snapshot = Frame.Write(layers: 4);
            snapshot.AddThing(new ThingView(new ThingId(4), top, ItemHandle.Meal, 0));
            var director = new SelectionDirector();

            director.Pick(top, PawnId.None, snapshot);

            Assert.That(director.Thing, Is.EqualTo(new ThingId(4)));
        }

        /// <summary>
        /// <b>A second click on the same cell looks past what is lying in it.</b>
        ///
        /// <para>A thing wins the first click and should — a pile of wood is what the player
        /// pointed at. But a stockpile is wall-to-wall things, and there was no way at all to
        /// reach the tile under one (owner, 2026-09-19: "it seems to be difficult to click on a
        /// tile with wood in — always the item takes precedence when clicking on a tile, then
        /// clicking on again would then go to the tile info").</para>
        /// </summary>
        [Test]
        public void ASecondClickOnAPileShowsTheTileUnderIt()
        {
            var ground = new CellRef(5, 5, 0);
            var pileAt = new CellRef(5, 5, 1);
            var snapshot = FrameWith(PawnId.None, default, new ThingId(4), pileAt);
            var director = new SelectionDirector();

            director.Pick(ground, PawnId.None, snapshot);
            Assume.That(director.HasThing, Is.True, "the first click is the pile");

            director.Pick(ground, PawnId.None, snapshot);
            Assert.That(director.HasThing, Is.False, "the second click is the tile under it");
            Assert.That(director.Cell, Is.EqualTo(ground), "and the cell is still held");

            director.Pick(ground, PawnId.None, snapshot);
            Assert.That(director.Thing, Is.EqualTo(new ThingId(4)),
                "a third click comes back round to the pile: two rungs and a loop");
        }

        [Test]
        public void ClickingAwayAndBackStartsAtThePileAgain()
        {
            // The cycle is read off the selection rather than kept in a counter, so going
            // somewhere else resets it with nothing to remember to reset.
            var ground = new CellRef(5, 5, 0);
            var pileAt = new CellRef(5, 5, 1);
            var snapshot = FrameWith(PawnId.None, default, new ThingId(4), pileAt);
            var director = new SelectionDirector();

            director.Pick(ground, PawnId.None, snapshot);
            director.Pick(ground, PawnId.None, snapshot);
            Assume.That(director.HasThing, Is.False);

            director.Pick(new CellRef(1, 1, 0), PawnId.None, snapshot);
            director.Pick(ground, PawnId.None, snapshot);

            Assert.That(director.Thing, Is.EqualTo(new ThingId(4)),
                "coming back to a pile shows the pile, however the cell was left");
        }

        [Test]
        public void AColonistIsNeverCycledPastIntoTheCellSheStandsIn()
        {
            // The cycle belongs to the thing tier alone. A colonist clicked twice is the same
            // colonist, because a pick that lands on one selects them and the cell under their
            // feet is not part of the selection at all.
            var at = new CellRef(5, 5, 0);
            var pawn = new PawnId(2);
            var snapshot = FrameWith(pawn, at, ThingId.None, default);
            var director = new SelectionDirector();

            director.Pick(at, pawn, snapshot);
            director.Pick(at, pawn, snapshot);

            Assert.That(director.HasPawn, Is.True);
            Assert.That(director.Cell, Is.Null, "a colonist pick clears the cell tier, twice over");
        }

        [Test]
        public void APickOnBareGroundKeepsTheCellAndNothingElse()
        {
            var director = new SelectionDirector();
            director.Pick(new CellRef(1, 1, 0), PawnId.None, Frame.Write());
            Assert.That(director.Cell, Is.Not.Null);
            Assert.That(director.IsEmpty, Is.False, "ground is a subject, not a miss");
            Assert.That(director.HasPawn, Is.False);
            Assert.That(director.HasThing, Is.False);
        }

        [Test]
        public void ALayerChangeClearsTheSelectionWithItsReason()
        {
            var director = new SelectionDirector();
            director.Choose(new PawnId(2));
            SelectionChange? reason = null;
            director.Changed += r => reason = r;

            director.OnLayerChanged();

            Assert.That(director.IsEmpty, Is.True);
            Assert.That(reason, Is.EqualTo(SelectionChange.LayerChanged));
        }

        [Test]
        public void ADeadSubjectSurvivesOneFrameThenDrops()
        {
            var director = new SelectionDirector();
            director.Choose(new PawnId(5));
            var empty = Frame.Write();
            SelectionChange? reason = null;
            director.Changed += r => reason = r;

            director.Refresh(empty);
            Assert.That(director.HasPawn, Is.True, "the frame a colonist dies still shows them");
            director.Refresh(empty);
            Assert.That(director.HasPawn, Is.False);
            Assert.That(reason, Is.EqualTo(SelectionChange.Died));
        }

        [Test]
        public void ClearingAnEmptySelectionAnnouncesNothing()
        {
            var director = new SelectionDirector();
            int raised = 0;
            director.Changed += _ => raised++;
            director.Clear();
            Assert.That(raised, Is.Zero);
        }

        // ------------------------------------------------------ multi-select (M2)

        static WorldSnapshot FrameWithPawns(params (PawnId id, int layer)[] placed)
        {
            var snapshot = Frame.Write();
            foreach (var (id, layer) in placed)
                snapshot.AddPawn(new PawnView(id, new CellRef(1, 1, layer), 500, 500, 50, JobHandle.Haul));
            return snapshot;
        }

        [Test]
        public void ABoxSelectsTheSetAndNamesThePrimary()
        {
            var director = new SelectionDirector();
            var boxed = new[] { new PawnId(1), new PawnId(3), new PawnId(2) };
            SelectionChange? reason = null;
            director.Changed += r => reason = r;

            director.PickMany(boxed, additive: false, SelectionChange.Boxed);

            Assert.That(director.Pawns, Is.EqualTo(boxed));
            Assert.That(director.Pawn, Is.EqualTo(new PawnId(1)), "the first in is the primary");
            Assert.That(director.HasMultiple, Is.True);
            Assert.That(reason, Is.EqualTo(SelectionChange.Boxed));
        }

        [Test]
        public void AShiftBoxJoinsTheSelectionWithoutDuplicatingIt()
        {
            var director = new SelectionDirector();
            director.PickMany(new[] { new PawnId(1) }, additive: false, SelectionChange.Boxed);

            director.PickMany(new[] { new PawnId(2), new PawnId(1) }, additive: true, SelectionChange.Boxed);

            Assert.That(director.Pawns, Is.EqualTo(new[] { new PawnId(1), new PawnId(2) }));
        }

        [Test]
        public void AShiftPickTogglesAColonistInTheSet()
        {
            var director = new SelectionDirector();
            var snapshot = FrameWithPawns((new PawnId(1), 0), (new PawnId(2), 0));
            director.PickMany(new[] { new PawnId(1), new PawnId(2) }, additive: false, SelectionChange.Boxed);
            SelectionChange? reason = null;
            director.Changed += r => reason = r;

            var cell = new CellRef(1, 1, 0);
            director.Pick(cell, new PawnId(1), snapshot, additive: true);

            Assert.That(director.Pawns, Is.EqualTo(new[] { new PawnId(2) }), "out, not replaced");
            Assert.That(reason, Is.EqualTo(SelectionChange.Toggled));

            director.Pick(cell, new PawnId(1), snapshot, additive: true);
            Assert.That(director.Pawn, Is.EqualTo(new PawnId(2)), "a toggle back in does not steal primary");
        }

        [Test]
        public void OneDeathPrunesOnlyTheDeadHandle()
        {
            var director = new SelectionDirector();
            director.PickMany(new[] { new PawnId(1), new PawnId(2) }, additive: false, SelectionChange.Boxed);
            var oneAlive = FrameWithPawns((new PawnId(2), 0));

            director.Refresh(oneAlive);
            Assert.That(director.Pawns.Count, Is.EqualTo(2), "the frame a colonist dies still shows them");
            director.Refresh(oneAlive);
            Assert.That(director.Pawns, Is.EqualTo(new[] { new PawnId(2) }));
            Assert.That(director.HasPawn, Is.True, "one death does not take the living");
        }

        [Test]
        public void APlainPickReplacesTheWholeSetWithOneColonist()
        {
            var director = new SelectionDirector();
            var snapshot = FrameWithPawns((new PawnId(1), 0), (new PawnId(2), 0));
            director.PickMany(new[] { new PawnId(1), new PawnId(2) }, additive: false, SelectionChange.Boxed);

            director.Pick(new CellRef(1, 1, 0), new PawnId(2), snapshot);

            Assert.That(director.Pawns, Is.EqualTo(new[] { new PawnId(2) }));
        }

        [Test]
        public void SelectSimilarReplacesUnlessShiftJoins()
        {
            var director = new SelectionDirector();
            director.Choose(new PawnId(1));

            director.PickMany(new[] { new PawnId(1), new PawnId(2), new PawnId(3) }, additive: false, SelectionChange.Similar);
            Assert.That(director.Pawns.Count, Is.EqualTo(3));

            director.PickMany(new[] { new PawnId(4) }, additive: true, SelectionChange.Similar);
            Assert.That(director.Pawns.Count, Is.EqualTo(4));
        }
    }

    public class SliceDirectorTests
    {
        [Test]
        public void TheLayerClampsAndAnnouncesOnlyRealChanges()
        {
            var slice = new SliceDirector();
            slice.Bind(layerCount: 4, startLayer: 9);
            Assert.That(slice.ActiveLayer, Is.EqualTo(3), "a start above the top clamps to the top");

            var seen = new List<int>();
            slice.LayerChanged += seen.Add;
            Assert.That(slice.SetLayer(3), Is.False, "already there");
            Assert.That(slice.Step(-1), Is.True);
            Assert.That(slice.Step(-10), Is.True);
            Assert.That(slice.Step(-1), Is.False, "the floor is the floor");
            Assert.That(seen, Is.EqualTo(new[] { 2, 0 }));
        }
    }

    public class CameraDirectorTests
    {
        [Test]
        public void AJumpIsHeldUntilTheRigArrivesOrThePlayerPans()
        {
            var camera = new CameraDirector();
            camera.JumpTo(new CellRef(5, 6, 1));
            Assert.That(camera.JumpTarget, Is.EqualTo(new CellRef(5, 6, 1)));
            camera.Arrived();
            Assert.That(camera.JumpTarget, Is.Null);

            camera.JumpTo(new CellRef(7, 7, 1));
            camera.Cancel();
            Assert.That(camera.JumpTarget, Is.Null, "a pan is the player winning");
        }
    }

    public class HudDirectorsTests
    {
        [Test]
        public void ChoosingAColonistMovesTheSliceSelectsThemAndSendsTheCamera()
        {
            var directors = new HudDirectors(layerCount: 4, startLayer: 1);
            var snapshot = Frame.Write();
            var at = new CellRef(6, 2, 3);
            snapshot.AddPawn(new PawnView(new PawnId(7), at, 500, 500, 50, JobHandle.Haul));
            // Somebody is already selected, so the layer change has a selection to clear.
            directors.Selection.Choose(new PawnId(1));
            var order = new List<string>();
            directors.Slice.LayerChanged += _ => order.Add("layer");
            directors.Selection.Changed += r => order.Add(r.ToString());

            Assert.That(directors.ChooseColonist(new PawnId(7), snapshot), Is.True);

            Assert.That(directors.Slice.ActiveLayer, Is.EqualTo(3));
            Assert.That(directors.Selection.Pawn, Is.EqualTo(new PawnId(7)));
            Assert.That(directors.Camera.JumpTarget, Is.EqualTo(at));
            // The layer change clears the old selection before the choice lands, which is the
            // order that leaves the colonist selected rather than cleared by their own jump. The
            // directors' own handler runs before the test's, so the clear is announced first.
            Assert.That(order, Is.EqualTo(new[] { "LayerChanged", "layer", "Chosen" }));
        }

        [Test]
        public void ChoosingAColonistNotInTheFrameDoesNothing()
        {
            var directors = new HudDirectors(4, 1);
            Assert.That(directors.ChooseColonist(new PawnId(99), Frame.Write()), Is.False);
            Assert.That(directors.Selection.IsEmpty, Is.True);
            Assert.That(directors.Camera.JumpTarget, Is.Null);
        }
    }
}

namespace Odyssey.Tests.Hud
{
    public class OverlayDirectorTests
    {
        [Test]
        public void TheDeveloperOverlayIsOffUntilToggledAndAnnouncesEachChange()
        {
            var overlays = new OverlayDirector();
            int raised = 0;
            overlays.Changed += () => raised++;

            Assert.That(overlays.DeveloperVisible, Is.False, "it sits on the picture, so it starts off");
            overlays.ToggleDeveloper();
            Assert.That(overlays.DeveloperVisible, Is.True);
            overlays.SetDeveloper(true);
            Assert.That(raised, Is.EqualTo(1), "setting what is already set says nothing");
            overlays.ToggleDeveloper();
            Assert.That(overlays.DeveloperVisible, Is.False);
            Assert.That(raised, Is.EqualTo(2));
        }
    }

    public class DebugDirectorTests
    {
        [Test]
        public void TheDebugMenuIsClosedUntilToggledAndAnnouncesEachChange()
        {
            var debug = new DebugDirector();
            int raised = 0;
            debug.Changed += () => raised++;

            Assert.That(debug.Open, Is.False);
            debug.Toggle();
            Assert.That(debug.Open, Is.True);
            Assert.That(raised, Is.EqualTo(1));
            debug.SetOpen(true);
            Assert.That(raised, Is.EqualTo(1), "setting what is already set says nothing");
            debug.Toggle();
            Assert.That(debug.Open, Is.False);
            Assert.That(raised, Is.EqualTo(2));
        }
    }
}

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A fake preference store, so persistence can be proved without a machine to store anything
    /// on. The real one writes to <c>PlayerPrefs</c> and lives in the Presentation assembly for
    /// exactly this reason.
    /// </summary>
    sealed class FakeSettingsStore : ISettingsStore
    {
        readonly System.Collections.Generic.Dictionary<string, bool> _values = new();
        readonly System.Collections.Generic.Dictionary<string, int> _numbers = new();
        readonly System.Collections.Generic.Dictionary<string, string> _words = new();

        public int Writes { get; private set; }

        public bool? Read(string key) => _values.TryGetValue(key, out bool value) ? value : null;

        public void Write(string key, bool value)
        {
            _values[key] = value;
            Writes++;
        }

        public int? ReadInt(string key) => _numbers.TryGetValue(key, out int value) ? value : null;

        public void WriteInt(string key, int value)
        {
            _numbers[key] = value;
            Writes++;
        }

        public string? ReadString(string key) => _words.TryGetValue(key, out string? value) ? value : null;

        public void WriteString(string key, string value)
        {
            _words[key] = value;
            Writes++;
        }

        public void Preset(string key, bool value) => _values[key] = value;

        public void Preset(string key, int value) => _numbers[key] = value;

        public void Preset(string key, string value) => _words[key] = value;
    }

    public class SettingsDirectorTests
    {
        /// <summary>
        /// Every ladder's default is one of its own rungs.
        ///
        /// <para>Written as a loop over the enum rather than six assertions, so a ladder added
        /// without a default cannot pass. A default off the ladder would open the panel with
        /// nothing lit and the first click would look like a jump.</para>
        /// </summary>
        [Test]
        public void EveryLadderRestsOnARungItOffers()
        {
            var settings = new SettingsDirector();
            foreach (GraphicsLadder ladder in SettingsDirector.AllLadders)
            {
                int[] rungs = SettingsDirector.RungsOf(ladder);
                Assert.That(rungs, Is.Not.Empty, $"{ladder} offers nothing");
                Assert.That(rungs, Contains.Item(SettingsDirector.DefaultOf(ladder)),
                    $"{ladder}'s default is not one of its rungs");
                Assert.That(settings.Value(ladder), Is.EqualTo(SettingsDirector.DefaultOf(ladder)),
                    $"{ladder} did not start where DefaultOf says");
                Assert.That(SettingsDirector.KeyOf(ladder), Does.StartWith("ui.settings."),
                    $"{ladder} is not named in the registry namespace");

                foreach (int rung in rungs)
                    Assert.That(SettingsDirector.RungLabel(ladder, rung), Is.Not.Empty,
                        $"{ladder}'s {rung} rung has nothing written on it");
            }
        }

        /// <summary>A value from an older build, whose ladder had different rungs, snaps to the
        /// nearest one this build offers rather than leaving the panel showing a number none of
        /// its own buttons can reproduce.</summary>
        [Test]
        public void AStrayValueSnapsToTheNearestRung()
        {
            var settings = new SettingsDirector();

            settings.SetValue(GraphicsLadder.FrameCap, 55);
            Assert.That(settings.Value(GraphicsLadder.FrameCap), Is.EqualTo(60));

            settings.SetValue(GraphicsLadder.RenderScale, 200);
            Assert.That(settings.Value(GraphicsLadder.RenderScale), Is.EqualTo(100));

            settings.SetValue(GraphicsLadder.AntiAliasing, 3);
            Assert.That(settings.Value(GraphicsLadder.AntiAliasing), Is.EqualTo(2).Or.EqualTo(4));
        }

        /// <summary>A ladder writes through when it moves, says nothing when it does not, and a
        /// fresh director reads the machine back.</summary>
        [Test]
        public void ALadderIsKeptOnTheMachineAndReadBack()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            settings.UseStore(store);

            var moved = new System.Collections.Generic.List<GraphicsLadder>();
            settings.LadderChanged += moved.Add;

            settings.SetValue(GraphicsLadder.VSync, 0);
            Assert.That(store.ReadInt(SettingsDirector.KeyOf(GraphicsLadder.VSync)), Is.EqualTo(0));
            Assert.That(moved, Is.EqualTo(new[] { GraphicsLadder.VSync }));

            settings.SetValue(GraphicsLadder.VSync, 0);
            Assert.That(moved, Has.Count.EqualTo(1), "a rung that did not move announced itself");

            var restarted = new SettingsDirector();
            restarted.UseStore(store);
            Assert.That(restarted.Value(GraphicsLadder.VSync), Is.EqualTo(0));
        }

        /// <summary>A seed describes the machine without writing it back, and a stored preference
        /// is laid over it — the bargain every other seed here makes.</summary>
        [Test]
        public void TheStoreBeatsTheSeedAndTheSeedBeatsNothing()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();

            settings.SeedValue(GraphicsLadder.ShadowDistance, 120);
            Assert.That(settings.Value(GraphicsLadder.ShadowDistance), Is.EqualTo(120));
            Assert.That(store.Writes, Is.Zero, "seeding wrote to the machine");

            store.Preset(SettingsDirector.KeyOf(GraphicsLadder.ShadowDistance), 30);
            settings.UseStore(store);
            Assert.That(settings.Value(GraphicsLadder.ShadowDistance), Is.EqualTo(30));

            Assert.That(settings.Value(GraphicsLadder.RenderScale), Is.EqualTo(100),
                "a ladder the machine has never been told should keep its default");
        }

        /// <summary>
        /// The cap is dead behind VSync, and the panel is the thing that has to know.
        ///
        /// <para>Unity ignores <c>Application.targetFrameRate</c> whenever <c>vSyncCount</c> is
        /// above zero. The rule lives on the director so this tier can hold it.</para>
        /// </summary>
        [Test]
        public void TheFrameCapIsOnlyLiveWithVSyncOff()
        {
            var settings = new SettingsDirector();

            settings.SetValue(GraphicsLadder.VSync, 1);
            Assert.That(settings.FrameCapIsLive, Is.False);

            settings.SetValue(GraphicsLadder.VSync, 2);
            Assert.That(settings.FrameCapIsLive, Is.False, "half rate is still VSync pacing the frame");

            settings.SetValue(GraphicsLadder.VSync, 0);
            Assert.That(settings.FrameCapIsLive, Is.True);
        }

        /// <summary>Only the levers that resize a render target say they cost a hitch.</summary>
        [Test]
        public void OnlyTheBufferLeversAdmitToAHitch()
        {
            Assert.That(SettingsDirector.CostsAHitch(GraphicsLadder.RenderScale), Is.True);
            Assert.That(SettingsDirector.CostsAHitch(GraphicsLadder.AntiAliasing), Is.True);
            Assert.That(SettingsDirector.CostsAHitch(GraphicsLadder.VSync), Is.False);
            Assert.That(SettingsDirector.CostsAHitch(GraphicsLadder.FrameCap), Is.False);
        }

        /// <summary>The screen's sizes arrive de-duplicated by area and largest first, because
        /// <c>Screen.resolutions</c> reports one entry per refresh rate.</summary>
        [Test]
        public void TheResolutionLadderDropsTheRepeatsOneRefreshRateAtATime()
        {
            var settings = new SettingsDirector();
            settings.SeedResolutions(new[]
            {
                new SettingsDirector.Mode(1920, 1080),
                new SettingsDirector.Mode(1920, 1080),
                new SettingsDirector.Mode(1280, 720),
                new SettingsDirector.Mode(2560, 1440),
                new SettingsDirector.Mode(0, 0),
            });

            Assert.That(settings.Resolutions.Count, Is.EqualTo(3));
            Assert.That(settings.Resolutions[0], Is.EqualTo(new SettingsDirector.Mode(2560, 1440)));
            Assert.That(settings.Resolutions[2], Is.EqualTo(new SettingsDirector.Mode(1280, 720)));
        }

        /// <summary>
        /// A stored size this machine no longer offers resolves to the nearest by pixel count.
        ///
        /// <para>Unplugging a second monitor must not leave the row blank and the button dead:
        /// the commonest way a preference like this goes wrong is the one where nothing happens
        /// and nothing says why.</para>
        /// </summary>
        [Test]
        public void AResolutionThisScreenNoLongerOffersFallsToTheNearest()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            settings.SeedResolutions(new[]
            {
                new SettingsDirector.Mode(2560, 1440),
                new SettingsDirector.Mode(1920, 1080),
                new SettingsDirector.Mode(1280, 720),
            });
            store.Preset(SettingsDirector.ResolutionKey, "3840x2160");
            settings.UseStore(store);

            Assert.That(settings.Resolution, Is.EqualTo(new SettingsDirector.Mode(2560, 1440)));
            Assert.That(store.ReadString(SettingsDirector.ResolutionKey), Is.EqualTo("2560x1440"),
                "the fallback should be written back, not re-resolved every launch");
        }

        /// <summary>An unreadable preference is no answer, which is not the same as a wrong one:
        /// the game keeps the window it opened at rather than throwing.</summary>
        [Test]
        public void AnUnreadableResolutionLeavesTheWindowAlone()
        {
            Assert.That(SettingsDirector.ParseMode(null), Is.Null);
            Assert.That(SettingsDirector.ParseMode(""), Is.Null);
            Assert.That(SettingsDirector.ParseMode("1920"), Is.Null);
            Assert.That(SettingsDirector.ParseMode("1920x"), Is.Null);
            Assert.That(SettingsDirector.ParseMode("widexhigh"), Is.Null);
            Assert.That(SettingsDirector.ParseMode("1920x1080"),
                Is.EqualTo(new SettingsDirector.Mode(1920, 1080)));

            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            store.Preset(SettingsDirector.ResolutionKey, "nonsense");
            settings.SeedResolution(new SettingsDirector.Mode(1600, 900));
            Assert.DoesNotThrow(() => settings.UseStore(store));
            Assert.That(settings.Resolution, Is.EqualTo(new SettingsDirector.Mode(1600, 900)));
        }

        /// <summary>Nothing seeded means nothing offered, and asking for a size must still not
        /// throw — a Linux session can report a very short list, or none.</summary>
        [Test]
        public void AnEmptyResolutionListIsSurvivable()
        {
            var settings = new SettingsDirector();
            Assert.That(settings.Resolutions, Is.Empty);
            Assert.DoesNotThrow(() => settings.SetResolution(new SettingsDirector.Mode(1920, 1080)));
            Assert.That(settings.Resolution, Is.EqualTo(new SettingsDirector.Mode(1920, 1080)));
        }

        [Test]
        public void ThePanelIsShutUntilAskedForAndAnnouncesEachChange()
        {
            var settings = new SettingsDirector();
            int raised = 0;
            settings.Changed += () => raised++;

            Assert.That(settings.Open, Is.False, "nothing opens a panel the player did not ask for");
            settings.Toggle();
            Assert.That(settings.Open, Is.True);
            settings.SetOpen(true);
            Assert.That(raised, Is.EqualTo(1), "setting what is already set says nothing");
            settings.Toggle();
            Assert.That(settings.Open, Is.False);
            Assert.That(raised, Is.EqualTo(2));
        }

        [Test]
        public void EscapeCancelsTheToolBeforeItTouchesThePanel()
        {
            var settings = new SettingsDirector();

            // The order is the whole rule: a player who armed a tool and pressed Escape wants the
            // tool put down, not a settings panel in the middle of the screen.
            Assert.That(settings.Escape(toolArmed: true), Is.EqualTo(EscapeAction.DisarmTool));
            settings.SetOpen(true);
            Assert.That(settings.Escape(toolArmed: true), Is.EqualTo(EscapeAction.DisarmTool),
                "an armed tool outranks an open panel, however the panel got there");

            Assert.That(settings.Escape(toolArmed: false), Is.EqualTo(EscapeAction.ClosePanel));
            settings.SetOpen(false);
            Assert.That(settings.Escape(toolArmed: false), Is.EqualTo(EscapeAction.OpenPanel));
        }

        /// <summary>
        /// Every option starts as <see cref="SettingsDirector.DefaultOn"/> says, and the panel
        /// knows which ones cost a remesh to change.
        ///
        /// <para><b>This used to assert that every option starts on</b>, which was true while every
        /// option added a piece of the world. <c>CutAwayCeiling</c> is the first that <em>takes one
        /// away</em> — it is what hid the floor a player had just built one layer up — so it starts
        /// off, and the rule is now "as the default says" with the default itself spelled out
        /// below rather than a blanket true.</para>
        /// </summary>
        [Test]
        public void EveryOptionStartsOnAndReportsWhetherChangingItCostsARedraw()
        {
            var settings = new SettingsDirector();
            foreach (GraphicsOption option in SettingsDirector.All)
                Assert.That(settings.IsOn(option), Is.EqualTo(SettingsDirector.DefaultOn(option)),
                    $"{option} does not start as its own default says");

            // Spelled out rather than derived, so that flipping a default has to be written here.
            Assert.That(SettingsDirector.DefaultOn(GraphicsOption.Shadows), Is.True);
            Assert.That(SettingsDirector.DefaultOn(GraphicsOption.SeeThrough), Is.True);
            Assert.That(SettingsDirector.DefaultOn(GraphicsOption.CutAwayCeiling), Is.False,
                "the cut-away hides the floor overhead, so it is the one option that starts off");

            // Three are read as the frame is submitted; two are baked into the instance matrices
            // when a chunk is meshed, and the panel has to know which it is holding.
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.Shadows), Is.False);
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.Surround), Is.False);
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.SeeThrough), Is.False);
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.GrassTufts), Is.True);
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.GroundRelief), Is.True);
        }

        [Test]
        public void AnOptionAnnouncesOnlyRealChanges()
        {
            var settings = new SettingsDirector();
            var changed = new System.Collections.Generic.List<GraphicsOption>();
            settings.OptionChanged += changed.Add;

            settings.Set(GraphicsOption.Shadows, true);
            Assert.That(changed, Is.Empty, "it was already on");

            settings.Toggle(GraphicsOption.Shadows);
            Assert.That(settings.IsOn(GraphicsOption.Shadows), Is.False);
            Assert.That(changed, Is.EqualTo(new[] { GraphicsOption.Shadows }));
        }

        [Test]
        public void TheSceneSetsTheStartingStateAndAStoredPreferenceBeatsIt()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();

            // The scene was built with no grass. Seeding says so without raising anything, so a
            // panel cannot change the board merely by existing.
            var changed = new System.Collections.Generic.List<GraphicsOption>();
            settings.OptionChanged += changed.Add;
            settings.Seed(GraphicsOption.GrassTufts, false);
            Assert.That(settings.IsOn(GraphicsOption.GrassTufts), Is.False);
            Assert.That(changed, Is.Empty, "seeding is a record of what is, not a request");

            // This machine was told once to keep the shadows off. That outranks the scene.
            store.Preset(SettingsDirector.KeyOf(GraphicsOption.Shadows), false);
            settings.UseStore(store);

            Assert.That(settings.IsOn(GraphicsOption.Shadows), Is.False);
            Assert.That(changed, Is.EqualTo(new[] { GraphicsOption.Shadows }),
                "only the stored value that differed had to be applied to the board");
            Assert.That(settings.IsOn(GraphicsOption.GrassTufts), Is.False,
                "an option the store has never heard of keeps what the scene gave it");
        }

        [Test]
        public void AChoiceIsWrittenDownAsSoonAsItIsMade()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            settings.UseStore(store);
            Assert.That(store.Writes, Is.Zero, "attaching a store that knows nothing writes nothing");

            settings.Toggle(GraphicsOption.GroundRelief);
            Assert.That(store.Read(SettingsDirector.KeyOf(GraphicsOption.GroundRelief)), Is.False);
            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public void TheCameraSpeedSnapsToItsLadderAndIsKeptOnTheMachine()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            settings.UseStore(store);
            var raised = new System.Collections.Generic.List<int>();
            settings.CameraSpeedChanged += raised.Add;

            Assert.That(settings.CameraSpeed, Is.EqualTo(100), "the rig's tuned speed is the default");

            settings.SetCameraSpeed(90);
            Assert.That(settings.CameraSpeed, Is.EqualTo(100), "90 snaps to the rung it sits between");
            Assert.That(raised, Is.Empty, "a snap that lands where it started says nothing");

            settings.SetCameraSpeed(160);
            Assert.That(settings.CameraSpeed, Is.EqualTo(150));
            Assert.That(raised, Is.EqualTo(new[] { 150 }));
            Assert.That(store.ReadInt(SettingsDirector.CamSpeedKey), Is.EqualTo(150));

            // A stored preference beats the tuning, the same as every other lever here.
            store.Preset(SettingsDirector.CamSpeedKey, 60);
            var restarted = new SettingsDirector();
            restarted.UseStore(store);
            Assert.That(restarted.CameraSpeed, Is.EqualTo(60));
        }

        [Test]
        public void TheDeveloperOverlayRowDescribesTheScreenAndThenKeepsItsOwnCounsel()
        {
            var settings = new SettingsDirector();
            var store = new FakeSettingsStore();
            settings.UseStore(store);
            int raised = 0;
            settings.DeveloperOverlayChanged += () => raised++;

            // Seeded from whatever armed the readout — the key, or last session's preference —
            // so the row never switches the overlay on by existing.
            settings.SeedDeveloperOverlay(true);
            Assert.That(settings.DeveloperOverlay, Is.True);
            Assert.That(raised, Is.Zero, "seeding is a record of what is, not a request");

            settings.SetDeveloperOverlay(true);
            Assert.That(raised, Is.Zero);
            settings.SetDeveloperOverlay(false);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(store.Read(SettingsDirector.DeveloperKey), Is.False,
                "the key never wrote anything down; the row does");
        }

        [Test]
        public void AVolumeTakesAnyWholeDecibelInItsSpanAndSaysSoOnlyWhenItMoves()
        {
            var settings = new SettingsDirector();
            var changed = new System.Collections.Generic.List<SettingsBus>();
            settings.BusDbChanged += changed.Add;

            foreach (SettingsBus bus in SettingsDirector.Buses)
                Assert.That(settings.BusDb(bus), Is.Zero,
                    "a fader starts at unity — nothing attenuated, nothing boosted");

            // A fader holds a continuum: any whole dB in the span is taken as it stands. The
            // section shipped as seven-rung ladders and the owner asked for sliders on
            // 2026-09-17, so the snapping went with the rungs.
            settings.SetBusDb(SettingsBus.Music, -33);
            Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(-33),
                "-33 is a place a thumb can rest, so -33 it stays");
            Assert.That(changed, Is.EqualTo(new[] { SettingsBus.Music }),
                "one real move, announced once");

            settings.SetBusDb(SettingsBus.Music, -33);
            Assert.That(changed.Count, Is.EqualTo(1), "a drag that lands where it started says nothing");

            settings.SetBusDb(SettingsBus.Music, 5);
            Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(5),
                "a boost is taken as it stands, to the ceiling and no further");
            settings.SetBusDb(SettingsBus.Music, -200);
            Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(SettingsDirector.SilenceDb),
                "the fader has a floor, and it is silence");
            settings.SetBusDb(SettingsBus.Music, 30);
            Assert.That(settings.BusDb(SettingsBus.Music), Is.EqualTo(SettingsDirector.BoostDb),
                "and a ceiling, and it is the boost cap");
            Assert.That(changed, Is.EqualTo(new[]
                    { SettingsBus.Music, SettingsBus.Music, SettingsBus.Music, SettingsBus.Music }),
                "each real move is announced; the faders the store left alone say nothing");

            // Seeding is the presenter laying the audio store's values in: recorded, never
            // raised, never written.
            var seeded = new SettingsDirector();
            var seededRaised = new System.Collections.Generic.List<SettingsBus>();
            seeded.BusDbChanged += seededRaised.Add;
            seeded.SeedBusDb(SettingsBus.Alerts, -47);
            Assert.That(seeded.BusDb(SettingsBus.Alerts), Is.EqualTo(-47));
            seeded.SeedBusDb(SettingsBus.Alerts, -999);
            Assert.That(seeded.BusDb(SettingsBus.Alerts), Is.EqualTo(SettingsDirector.SilenceDb),
                "a value from an older build cannot seed what the fader cannot show");
            Assert.That(seededRaised, Is.Empty);
        }

        [Test]
        public void UnitySitsAtTheCentreOfTheFadersTrack()
        {
            // The owner asked on 2026-09-17 for the default in the middle of the control:
            // lowering to silence on one side of it, boosting to the ceiling on the other.
            // Each half is linear in dB over its own span, which is the only way one track
            // can say both.
            Assert.That(SettingsDirector.TrackOf(SettingsDirector.UnityDb), Is.EqualTo(0f),
                "unity is the centre of the track");
            Assert.That(SettingsDirector.TrackOf(SettingsDirector.SilenceDb), Is.EqualTo(-1f),
                "silence is the left end");
            Assert.That(SettingsDirector.TrackOf(SettingsDirector.BoostDb), Is.EqualTo(1f),
                "the boost ceiling is the right end");
            Assert.That(SettingsDirector.TrackOf(SettingsDirector.SilenceDb / 2),
                Is.EqualTo(-0.5f).Within(0.0001f),
                "the left half spends itself on 80 dB of attenuation");
            Assert.That(SettingsDirector.TrackOf(SettingsDirector.BoostDb / 2),
                Is.EqualTo(0.5f).Within(0.0001f),
                "the right half on the boost");

            // And back again, to the whole dB the thumb rests at.
            Assert.That(SettingsDirector.DbOf(0f), Is.EqualTo(SettingsDirector.UnityDb));
            Assert.That(SettingsDirector.DbOf(-1f), Is.EqualTo(SettingsDirector.SilenceDb));
            Assert.That(SettingsDirector.DbOf(1f), Is.EqualTo(SettingsDirector.BoostDb));
            Assert.That(SettingsDirector.DbOf(0.25f), Is.EqualTo(3), "a quarter right is +3 dB");
            Assert.That(SettingsDirector.DbOf(-0.5f), Is.EqualTo(-40), "half left is -40 dB");

            // Every whole dB in the span round-trips to itself, or the readout would say a
            // figure the thumb is not standing at.
            for (int db = SettingsDirector.SilenceDb; db <= SettingsDirector.BoostDb; db++)
                Assert.That(SettingsDirector.DbOf(SettingsDirector.TrackOf(db)), Is.EqualTo(db),
                    $"whole dB {db} does not survive the seat");
        }

        [Test]
        public void TheExitRowAsksBeforeItLeavesAndThePanelClosingStandsItDown()
        {
            var settings = new SettingsDirector();
            int armed = 0, asked = 0;
            settings.ExitChanged += () => armed++;
            settings.ExitRequested += () => asked++;

            settings.RequestExit();
            Assert.That(settings.ExitArmed, Is.True, "the first click asks to be sure");
            Assert.That(asked, Is.Zero);
            settings.RequestExit();
            Assert.That(asked, Is.EqualTo(1), "the second click is the promise kept");
            Assert.That(settings.ExitArmed, Is.False, "a fired exit is not still armed");

            // Nothing is saved, so an armed row must not outlive the panel it lives in.
            settings.SetOpen(true);
            settings.RequestExit();
            Assert.That(settings.ExitArmed, Is.True);
            settings.SetOpen(false);
            Assert.That(settings.ExitArmed, Is.False,
                "closing the panel stands the row down, Escape included");
            Assert.That(asked, Is.EqualTo(1), "standing down is not leaving");
        }

        [Test]
        public void ThePanelNamesItsOwnTabsFromTheRegistry()
        {
            // The tab chips used to be a ternary in the shell; four tabs made it a switch,
            // and a switch belongs beside the enum it switches on, where the naming test can
            // reach it.
            Assert.That(SettingsDirector.TabKey(SettingsTab.Interface), Is.EqualTo(SettingsDirector.InterfaceKey));
            Assert.That(SettingsDirector.TabKey(SettingsTab.Graphics), Is.EqualTo(SettingsDirector.GraphicsKey));
            Assert.That(SettingsDirector.TabKey(SettingsTab.Audio), Is.EqualTo(SettingsDirector.AudioKey));
            Assert.That(SettingsDirector.TabKey(SettingsTab.Keys), Is.EqualTo(HotkeyDirector.KeysKey));
        }
    }
}
