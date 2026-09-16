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
            Assert.That(director.Cell, Is.EqualTo(cell));
            Assert.That(changes, Is.EqualTo(new[] { SelectionChange.Picked }));
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
