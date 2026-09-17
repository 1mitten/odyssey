#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The drag-a-box-and-give-an-order state machine, which is the half of designation that can
    /// be tested at all: the pointer half lives in the camera rig, and the PlayMode input harness
    /// cannot yet deliver a synthetic mouse to it.
    /// </summary>
    public class DesignateDirectorTests
    {
        static CellRef At(int x, int z, int y = 4) => new CellRef(x, z, y);

        [Test]
        public void WithNoToolArmedADragNeverStarts()
        {
            var director = new DesignateDirector();

            Assert.That(director.Begin(At(3, 3)), Is.False,
                "a click with no tool armed must fall through to selection");
            Assert.That(director.Dragging, Is.False);
            Assert.That(director.Commit(), Is.Empty);
        }

        [Test]
        public void AClickWithNoMovementIsAOneCellOrder()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };

            Assert.That(director.Begin(At(7, 9)), Is.True);
            IReadOnlyList<CellRef> cells = director.Commit();

            Assert.That(cells, Is.EqualTo(new[] { At(7, 9) }),
                "click-to-mark-one and drag-to-mark-many are the same code path");
            Assert.That(director.Dragging, Is.False, "committing ends the drag");
        }

        /// <summary>
        /// A box drawn from any corner is the same box. Players drag whichever way suits them and
        /// three of the four directions produce a negative width if nobody sorts the corners.
        /// </summary>
        [Test]
        public void ABoxIsTheSameWhicheverCornerItIsDrawnFrom()
        {
            var forward = Cover(At(2, 2), At(4, 5));
            var back = Cover(At(4, 5), At(2, 2));
            var mixedX = Cover(At(4, 2), At(2, 5));
            var mixedZ = Cover(At(2, 5), At(4, 2));

            Assert.That(forward.Count, Is.EqualTo(3 * 4), "three cells across, four deep");
            Assert.That(back, Is.EqualTo(forward));
            Assert.That(mixedX, Is.EqualTo(forward));
            Assert.That(mixedZ, Is.EqualTo(forward));
        }

        /// <summary>
        /// The order is part of the contract, not an accident. These become intents, intents are
        /// hashed into the state, and a set of orders arriving in a different order on two
        /// machines is a divergence.
        /// </summary>
        [Test]
        public void TheCellsComeBackInAFixedOrder()
        {
            var cells = Cover(At(5, 5), At(6, 6));

            Assert.That(cells, Is.EqualTo(new[] { At(5, 5), At(6, 5), At(5, 6), At(6, 6) }),
                "row by row from the low corner");
        }

        /// <summary>
        /// Every cell of the box is on the layer the drag started on. The picker cannot return a
        /// cell above the active layer, so a drag must not climb a storey half way across either.
        /// </summary>
        [Test]
        public void TheWholeBoxSitsOnTheLayerTheDragStartedOn()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Fell };
            director.Begin(At(1, 1, y: 4));
            director.DragTo(At(3, 3, y: 11));

            foreach (CellRef cell in director.Commit())
                Assert.That(cell.Y, Is.EqualTo(4), $"{cell} left the anchor's layer");
        }

        [Test]
        public void TheBoxCanBeThrownAway()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };
            director.Begin(At(1, 1));
            director.DragTo(At(9, 9));
            Assume.That(director.PreviewCount, Is.EqualTo(81));

            director.Abandon();

            Assert.That(director.Dragging, Is.False);
            Assert.That(director.TryPreview(out _, out _), Is.False);
            Assert.That(director.Commit(), Is.Empty, "an abandoned drag must not still be orderable");
        }

        /// <summary>
        /// Changing tool mid-drag abandons it rather than re-labelling it. Half a box of mining
        /// orders becoming felling orders on a key press is the kind of surprise that loses a
        /// player an afternoon's work.
        /// </summary>
        [Test]
        public void ChangingToolAbandonsTheDragAndAnnouncesItself()
        {
            var seen = new List<DesignateTool>();
            var director = new DesignateDirector();
            director.ToolChanged += seen.Add;

            director.Tool = DesignateTool.Mine;
            director.Begin(At(1, 1));
            director.DragTo(At(4, 4));
            director.Tool = DesignateTool.Fell;

            Assert.That(director.Dragging, Is.False, "the half-drawn box survived a tool change");
            Assert.That(seen, Is.EqualTo(new[] { DesignateTool.Mine, DesignateTool.Fell }));

            director.Tool = DesignateTool.Fell;
            Assert.That(seen.Count, Is.EqualTo(2), "setting the same tool announced a change");
        }

        [Test]
        public void DraggingWithoutBeginningDoesNothing()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };

            director.DragTo(At(5, 5));

            Assert.That(director.Dragging, Is.False);
            Assert.That(director.PreviewCount, Is.Zero);
        }

        /// <summary>Cancel is a tool like the others: it draws a box and the box means "take it off".</summary>
        [Test]
        public void CancelDrawsABoxLikeAnyOtherTool()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Cancel };

            Assert.That(director.Begin(At(0, 0)), Is.True);
            director.DragTo(At(1, 0));

            Assert.That(director.Commit(), Is.EqualTo(new[] { At(0, 0), At(1, 0) }));
        }

        // ---- the build tool ---------------------------------------------------------

        /// <summary>
        /// The one tool that is not fully described by its own name: a build order names a
        /// thing that is not there yet, so it carries which thing and which material.
        /// </summary>
        [Test]
        public void ArmingTheBuildToolRemembersWhatAndWhatOf()
        {
            var director = new DesignateDirector();

            Assert.That(director.Building, Is.EqualTo(BuildingHandle.Wall));
            Assert.That(director.Stuff, Is.EqualTo(StuffHandle.Wood), "wood is what a colony has first");

            director.ArmBuild(BuildingHandle.Wall);

            Assert.That(director.Tool, Is.EqualTo(DesignateTool.Build));
            Assert.That(director.Building, Is.EqualTo(BuildingHandle.Wall));
        }

        [Test]
        public void ArmingTheSameThingAgainPutsTheToolDown()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);

            director.ArmBuild(BuildingHandle.Wall);

            Assert.That(director.Tool, Is.EqualTo(DesignateTool.None),
                "a tool picked up by pressing a button is put down by pressing it again");
        }

        /// <summary>
        /// Choosing a material is a statement about the next wall, not an order to place one.
        /// A material button that also armed a tool would leave the player holding something
        /// they only meant to configure.
        /// </summary>
        [Test]
        public void ChoosingAMaterialDoesNotArmAnything()
        {
            var director = new DesignateDirector();

            director.ChooseStuff(StuffHandle.Stone);

            Assert.That(director.Stuff, Is.EqualTo(StuffHandle.Stone));
            Assert.That(director.Tool, Is.EqualTo(DesignateTool.None));
        }

        /// <summary>
        /// A player who puts the wall tool down and picks it up again wants the wall back,
        /// in the material they chose. The choice outlives the tool deliberately.
        /// </summary>
        [Test]
        public void TheChoiceSurvivesPuttingTheToolDown()
        {
            var director = new DesignateDirector();
            director.ChooseStuff(StuffHandle.Stone);
            director.ArmBuild(BuildingHandle.Wall);

            director.Tool = DesignateTool.None;
            director.ArmBuild(BuildingHandle.Wall);

            Assert.That(director.Stuff, Is.EqualTo(StuffHandle.Stone));
            Assert.That(director.Tool, Is.EqualTo(DesignateTool.Build));
        }

        [Test]
        public void TheBuildChoiceIsAnnouncedSoThePaletteCanMarkIt()
        {
            var director = new DesignateDirector();
            var seen = new List<(int building, int stuff)>();
            director.BuildChoiceChanged += (b, s) => seen.Add((b, s));

            director.ChooseStuff(StuffHandle.Stone);
            director.ChooseStuff(StuffHandle.Stone);

            Assert.That(seen, Is.EqualTo(new[] { (BuildingHandle.Wall, StuffHandle.Stone) }),
                "choosing the material already chosen announced a change");
        }

        [Test]
        public void TheBuildToolDrawsABoxLikeAnyOtherTool()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);

            Assert.That(director.Begin(At(3, 3)), Is.True);
            director.DragTo(At(5, 3));

            Assert.That(director.Commit(), Is.EqualTo(new[] { At(3, 3), At(4, 3), At(5, 3) }),
                "a wall is dragged out as a run, which is the whole reason to drag one");
        }

        // ---- the live preview ------------------------------------------------------

        /// <summary>
        /// The preview and the order are the same object's answer a frame apart.
        ///
        /// <para>This is what the live preview is worth: <c>TryPreview</c> was written for
        /// drawing the box and nothing ever called it, so a player saw no feedback at all
        /// between pressing and releasing (owner, 2026-09-17). Driving the director live and
        /// drawing its own preview is what makes the picture and the order impossible to
        /// disagree, and this pins that they do not.</para>
        /// </summary>
        [Test]
        public void ThePreviewCoversExactlyWhatTheCommitPlaces()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(2, 7));
            director.DragTo(At(4, 9));

            Assert.That(director.TryPreview(out CellRef min, out CellRef max), Is.True);
            Assert.That(director.PreviewCount, Is.EqualTo(9));

            var previewed = new List<CellRef>();
            for (int z = min.Z; z <= max.Z; z++)
            for (int x = min.X; x <= max.X; x++)
                previewed.Add(new CellRef(x, z, min.Y));

            Assert.That(director.Commit(), Is.EqualTo(previewed));
        }

        /// <summary>A drag abandoned mid-way leaves nothing to draw, so the box vanishes with it.</summary>
        [Test]
        public void AnAbandonedDragHasNoPreviewToDraw()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(1, 1));
            director.DragTo(At(3, 3));

            director.Abandon();

            Assert.That(director.TryPreview(out _, out _), Is.False);
            Assert.That(director.PreviewCount, Is.Zero);
        }

        static List<CellRef> Cover(CellRef from, CellRef to)
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };
            director.Begin(from);
            director.DragTo(to);
            return new List<CellRef>(director.Commit());
        }
    }
}
