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

        public int Writes { get; private set; }

        public bool? Read(string key) => _values.TryGetValue(key, out bool value) ? value : null;

        public void Write(string key, bool value)
        {
            _values[key] = value;
            Writes++;
        }

        public void Preset(string key, bool value) => _values[key] = value;
    }

    public class SettingsDirectorTests
    {
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

        [Test]
        public void EveryOptionStartsOnAndReportsWhetherChangingItCostsARedraw()
        {
            var settings = new SettingsDirector();
            foreach (GraphicsOption option in SettingsDirector.All)
                Assert.That(settings.IsOn(option), Is.True, $"{option} should default to drawn");

            // Two are read as the frame is submitted; two are baked into the instance matrices
            // when a chunk is meshed, and the panel has to know which it is holding.
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.Shadows), Is.False);
            Assert.That(SettingsDirector.NeedsRedraw(GraphicsOption.Surround), Is.False);
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
    }
}
