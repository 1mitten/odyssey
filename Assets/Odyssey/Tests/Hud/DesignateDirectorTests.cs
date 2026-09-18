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

        /// <summary>
        /// <b>A floor is ordered on the layer being worked, not the one under the pointer.</b>
        ///
        /// <para>The picker can only ever name a surface — it stops the ray at the first thing
        /// that occludes it — so it cannot express the cell a floor is for, which is open air over
        /// a room with nothing beneath it to aim at. Measured on the real renderer on 2026-09-17:
        /// every click over a roofed room's interior named the floor of the room or missed
        /// outright, so a room's middle could not be roofed at all. The layer therefore comes from
        /// the slice, and only the column comes from the pointer.</para>
        ///
        /// <para>Set by whoever knows the slice and the content, which is not this assembly; what
        /// is tested here is the substitution, which is all that lives here.</para>
        /// </summary>
        [Test]
        public void AWorkingLayerOverridesTheLayerThePointerNamed()
        {
            var director = new DesignateDirector { WorkingLayer = 9 };
            director.ArmBuild(BuildingHandle.Floor);

            director.Begin(At(2, 2, y: 3));
            director.DragTo(At(5, 2, y: 1));

            IReadOnlyList<CellRef> cells = director.Commit();
            Assert.That(cells, Is.Not.Empty);
            foreach (CellRef cell in cells)
                Assert.That(cell.Y, Is.EqualTo(9),
                    $"{cell} took the pointer's layer instead of the slice's");
        }

        /// <summary>
        /// <b>And it only ever lifts.</b> The working layer is a floor under the order, not an
        /// override of it — a pointer that names something already <em>above</em> the slice keeps
        /// its own layer.
        ///
        /// <para>This is the owner's third report in one assertion. It overrode unconditionally,
        /// and the played board is terraced across five layers, so a click on a wall one terrace
        /// above the slice was rewritten down into the hillside and refused. The tool worked only
        /// where the ground happened to sit at exactly the slice's height.</para>
        /// </summary>
        [Test]
        public void TheWorkingLayerOnlyEverLiftsAnOrderAndNeverDropsIt()
        {
            var director = new DesignateDirector { WorkingLayer = 2 };
            director.ArmBuild(BuildingHandle.Floor);

            // The pointer names a wall standing two layers above the slice.
            director.Begin(At(2, 2, y: 4));

            IReadOnlyList<CellRef> cells = director.Commit();
            Assert.That(cells, Has.Count.EqualTo(1));
            Assert.That(cells[0].Y, Is.EqualTo(4),
                "a slice below what the pointer named must not drag the order down into the ground");
        }

        /// <summary>
        /// And with no working layer set, nothing changes. Every other tool aims at what the
        /// pointer is over, and this rule must not leak into them — a mine order on the layer the
        /// camera happens to be at, rather than on the rock the player clicked, would be the
        /// misclick fault ADR 0006 was written against.
        /// </summary>
        [Test]
        public void WithNoWorkingLayerTheAnchorStillDecides()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };

            director.Begin(At(2, 2, y: 3));
            director.DragTo(At(5, 5, y: 9));

            foreach (CellRef cell in director.Commit())
                Assert.That(cell.Y, Is.EqualTo(3), $"{cell} left the anchor's layer");
        }

        /// <summary>
        /// <b>One click places one cell and closes the run.</b> (Owner, 2026-09-17: *"it should
        /// just place the ladder with a click, no need to do many"*.)
        ///
        /// <para><b>Click-move-click was tried and taken out again.</b> For a few hours a click
        /// placed its cell and left the run open for a second click to extend. It reads well and it
        /// cost the player the cursor: while a run is open <c>TryPreview</c> succeeds, so the
        /// composition root draws the run's box and never calls <c>DrawHoverGhost</c>. So a single
        /// click swapped the cursor that follows the pointer for a box anchored to the last thing
        /// placed, for as long as the player did not happen to click again — and when they did, it
        /// placed the whole line between. The owner reported the cursor simply missing, which is
        /// what that looks like from the other side of the screen.</para>
        ///
        /// <para>A run is still a run: press, move, release is a drag and arrives through
        /// <c>Drag</c>. What is gone is the gesture that stayed open with nothing but a box to say
        /// so.</para>
        /// </summary>
        [Test]
        public void AClickPlacesOneCellAndLeavesNoRunOpen()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };

            Assert.That(director.Click(At(2, 2)), Is.EqualTo(new[] { At(2, 2) }),
                "a click places the cell it landed on");
            Assert.That(director.Dragging, Is.False, "and leaves no run open behind it");
            Assert.That(director.AwaitingSecondClick, Is.False);

            // The cursor's own gate: with nothing open, the hover ghost is what gets drawn.
            Assert.That(director.TryPreview(out _, out _), Is.False,
                "a run left open here is exactly what hides the build cursor");
        }

        /// <summary>
        /// A second click is a second thing, not the far end of a line from the first.
        ///
        /// <para>The failure this pins is the one the owner met: two separate ladders placed a few
        /// cells apart became a solid run of ladders between them.</para>
        /// </summary>
        [Test]
        public void TwoClicksApartPlaceTwoCellsAndNotTheLineBetween()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };

            Assert.That(director.Click(At(2, 2)), Is.EqualTo(new[] { At(2, 2) }));
            director.HoverAt(At(5, 2));
            Assert.That(director.Click(At(5, 2)), Is.EqualTo(new[] { At(5, 2) }),
                "the second click placed the line between the two rather than one cell");
        }

        /// <summary>
        /// <b>The press that opens a box has usually opened it already.</b> While a button is down
        /// the rig reports the pointer every frame, so by the time the release arrives a box exists
        /// even for a click that never moved a pixel. If that counted as the <em>second</em> click
        /// it would close the run, and click-move-click would not exist: every click would be a
        /// complete gesture and the pointer could never travel.
        /// </summary>
        [Test]
        public void APressThatAlreadyOpenedTheBoxStillPlacesOnlyItsOwnCell()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Fell };

            // What the rig does while the button is held, before the release.
            director.Begin(At(3, 3));
            director.DragTo(At(3, 3));
            Assume.That(director.Dragging, Is.True);

            Assert.That(director.Click(At(3, 3)), Is.EqualTo(new[] { At(3, 3) }),
                "the release of the anchoring press places that one cell and no more");
            Assert.That(director.Dragging, Is.False, "and closes the run it opened");
            Assert.That(director.AwaitingSecondClick, Is.False);
        }

        /// <summary>
        /// Both ways in still work and the hand decides which: a press that travels is a held drag
        /// and is finished by letting go, exactly as it always has been. A player reaching for the
        /// old gesture is never punished.
        /// </summary>
        [Test]
        public void AHeldDragStillFinishesOnItsOwnRelease()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };

            director.Begin(At(1, 1));
            director.DragTo(At(3, 1));
            IReadOnlyList<CellRef> cells = director.Commit();

            Assert.That(cells.Count, Is.EqualTo(3), "a held drag commits on release as before");
            Assert.That(director.AwaitingSecondClick, Is.False, "and leaves nothing waiting");
        }

        /// <summary>
        /// Right-click, and Escape, unwind one step at a time: the half-drawn run goes first and
        /// the tool stays in hand, so a misjudged anchor costs one click rather than a trip back to
        /// the palette. The answer is what tells the caller whether to unwind further.
        /// </summary>
        [Test]
        public void CancellingTakesThePendingRunFirstAndTheToolAfter()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };
            director.Click(At(2, 2));
            Assume.That(director.AwaitingSecondClick, Is.True);

            Assert.That(director.CancelPending(), Is.True, "there was a run to throw away");
            Assert.That(director.Dragging, Is.False);
            Assert.That(director.Tool, Is.EqualTo(DesignateTool.Mine), "and the tool is still held");

            Assert.That(director.CancelPending(), Is.False,
                "with nothing pending the caller is told to unwind the next step itself");
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
            // Far enough across to be a deliberate area, so this asks its own question rather than
            // re-asking the widening gate's.
            director.Begin(At(2, 7));
            director.DragTo(At(5, 10));

            Assert.That(director.TryPreview(out CellRef min, out CellRef max), Is.True);
            Assert.That(director.PreviewCount, Is.EqualTo(16));

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

        // ---- a build box does not widen by accident -------------------------------

        /// <summary>
        /// The owner's report, in one test: "the building is a tad sensitive and by accident you
        /// can build dual walls" (2026-09-17).
        ///
        /// <para>A wall dragged along x with the pointer one cell off the row used to cover two
        /// rows — two parallel walls, ordered, delivered to and paid for out of a gesture that
        /// meant one. One cell across is what perspective and the hand produce on their own, so
        /// the box holds its row until the drag has gone <c>WidenAcross</c> clear.</para>
        /// </summary>
        [Test]
        public void AWallDoesNotWidenBecauseThePointerWandered()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(3, 3));
            director.DragTo(At(8, 4));

            Assert.That(director.Widened, Is.False);
            Assert.That(director.PreviewCount, Is.EqualTo(6),
                "six cells of wall in one row, not twelve in two");

            // Two cells across as well, which is where the threshold used to be and where the
            // owner was still getting double walls on a long drag (second report, 2026-09-17).
            director.DragTo(At(8, 5));
            Assert.That(director.Widened, Is.False);
            Assert.That(director.PreviewCount, Is.EqualTo(6));

            Assert.That(director.Commit(), Is.EqualTo(new[]
            {
                At(3, 3), At(4, 3), At(5, 3), At(6, 3), At(7, 3), At(8, 3),
            }));
        }

        /// <summary>
        /// The other half, and the reason this is hysteresis rather than a snap to a line: a
        /// rectangle of wall is still one gesture. A drag that goes a clear two cells across says
        /// something a wandering pointer does not.
        /// </summary>
        [Test]
        public void AWallWidensWhenTheDragGoesClearAcross()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(3, 3));
            director.DragTo(At(8, 6));

            Assert.That(director.Widened, Is.True);
            Assert.That(director.PreviewCount, Is.EqualTo(24), "six by four");
        }

        /// <summary>
        /// Two thresholds, not one, and this is what the second one buys.
        ///
        /// <para>With a single threshold a pointer resting on the boundary would flicker the box
        /// between one row and two every frame, which is worse than either. Widened, the box stays
        /// widened while the drag is still well across the run; it re-arms on the way home, within
        /// a cell of the anchor's row, where a one-row box is what is being drawn anyway.</para>
        ///
        /// <para><b>Re-arming near the row rather than on it is the fix for the second report.</b>
        /// A gate that needed the exact row made a trip permanent in practice — a pointer that has
        /// strayed three cells rarely comes back to precisely the row it left — so one wander
        /// anywhere in a long drag left the player letting go over a rectangle.</para>
        /// </summary>
        [Test]
        public void OnceWidenedTheBoxDoesNotFlickerBackOnTheBoundary()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(3, 3));

            director.DragTo(At(8, 6));
            Assert.That(director.Widened, Is.True);
            Assert.That(director.PreviewCount, Is.EqualTo(24), "six by four");

            director.DragTo(At(8, 5));
            Assert.That(director.Widened, Is.True, "one back from the boundary is not a retreat");
            Assert.That(director.PreviewCount, Is.EqualTo(18), "six by three");

            director.DragTo(At(8, 4));
            Assert.That(director.Widened, Is.False,
                "back within a cell of the row re-arms it, without having to land on the row");
            Assert.That(director.PreviewCount, Is.EqualTo(6));
        }

        /// <summary>
        /// Build only. The same slip does not cost the same thing: a mine box one cell wider than
        /// intended marks one more cell to dig, and a build box one row wider is a second wall.
        /// </summary>
        [Test]
        public void TheGateIsForBuildingAndNotForTheAreaTools()
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };
            director.Begin(At(3, 3));
            director.DragTo(At(8, 4));

            Assert.That(director.PreviewCount, Is.EqualTo(12),
                "mine, fell and cancel are area tools and keep every cell the box covers");
        }

        /// <summary>
        /// The gate is across the run and never along it, so the shortest wall anybody would
        /// actually drag — two cells — is still two cells.
        /// </summary>
        [Test]
        public void TheGateNeverShortensTheRunItself()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(3, 3));
            director.DragTo(At(4, 3));

            Assert.That(director.Commit(), Is.EqualTo(new[] { At(3, 3), At(4, 3) }));
        }

        /// <summary>
        /// A drag that turns a corner is one gesture and the player never said which axis was the
        /// run, so the gated axis is decided afresh from the travel rather than latched at the
        /// first movement.
        /// </summary>
        [Test]
        public void TheRunAxisIsWhicheverOneTheDragHasTravelledFurther()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);
            director.Begin(At(3, 3));

            director.DragTo(At(7, 4));
            Assert.That(director.PreviewCount, Is.EqualTo(5), "east, so the run is along x");

            director.DragTo(At(4, 9));
            Assert.That(director.PreviewCount, Is.EqualTo(7), "north now, so the run is along z");
        }

        static List<CellRef> Cover(CellRef from, CellRef to)
        {
            var director = new DesignateDirector { Tool = DesignateTool.Mine };
            director.Begin(from);
            director.DragTo(to);
            return new List<CellRef>(director.Commit());
        }

            // ---- the bed: one thing, two cells, one key claimed (design 20 section 5) -------------

            /// <summary>A director with the build tool armed on the bed, as the palette leaves it.</summary>
            static DesignateDirector BedArmed()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Bed);
            return director;
        }

        [Test]
        public void ABedIsPlacedOnePerClickWhateverTheDragDid()
        {
            var director = BedArmed();

            Assert.That(director.Begin(At(10, 10)), Is.True);
            director.DragTo(At(14, 18));
            IReadOnlyList<CellRef> cells = director.Commit();

            Assert.That(cells, Is.EqualTo(new[] { At(10, 10) }),
                "a bed is one order about one cell; the facing — not the drag — says where the rest of it goes");
        }

        /// <summary>
        /// The ghost is the thing's own footprint, turned by the facing, and where the pointer has
        /// wandered since the press began is nothing to do with it.
        /// </summary>
        [Test]
        public void TheBedsGhostIsItsFootprintNotTheDrag()
        {
            var director = BedArmed();
            director.Begin(At(10, 10));
            director.DragTo(At(13, 13));

            // North, east, south, west: the span is the anchor plus the facing's own offset.
            (int minX, int minZ, int maxX, int maxZ)[] spans =
            {
                (10, 10, 10, 11), (10, 10, 11, 10), (10, 9, 10, 10), (9, 10, 10, 10),
            };

            for (int facing = 0; facing < 4; facing++)
            {
                var (minX, minZ, maxX, maxZ) = spans[facing];
                Assert.That(director.TryPreview(out CellRef min, out CellRef max), Is.True);
                Assert.That(min, Is.EqualTo(At(minX, minZ)), $"facing {facing}: the low corner moved");
                Assert.That(max, Is.EqualTo(At(maxX, maxZ)), $"facing {facing}: the high corner moved");
                director.Rotate();
            }
        }

        /// <summary>
        /// The wall's own regression, run beside the bed's: what was true of the build box yesterday
        /// is still true of it, because the bed's rules are the bed's.
        /// </summary>
        [Test]
        public void AWallStillDragsABoxAndNeverClaimsTheRotateKey()
        {
            var director = new DesignateDirector();
            director.ArmBuild(BuildingHandle.Wall);

            Assert.That(director.SinglePlacement, Is.False);
            Assert.That(director.RotatableArmed, Is.False,
                "R stays the slice-up key while a wall is armed, exactly as it always was");

            director.Rotate();
            Assert.That(director.Facing, Is.EqualTo(0), "a thing that does not rotate never turns");

            director.Begin(At(10, 10));
            director.DragTo(At(12, 10));
            Assert.That(director.TryPreview(out CellRef min, out CellRef max), Is.True);
            Assert.That((min.X, min.Z, max.X, max.Z), Is.EqualTo((10, 10, 12, 10)));
            Assert.That(director.Commit(), Is.EqualTo(new[]
            {
                At(10, 10), At(11, 10), At(12, 10),
            }));
        }

        [Test]
        public void ARotatableThingTurnsClockwiseAndComesBackToNorth()
        {
            var director = BedArmed();

            Assert.That(director.RotatableArmed, Is.True);
            for (int expected = 1; expected <= 3; expected++)
            {
                director.Rotate();
                Assert.That(director.Facing, Is.EqualTo(expected));
            }
            director.Rotate();
            Assert.That(director.Facing, Is.EqualTo(0), "four quarter turns are home again");
        }

        [Test]
        public void PickingADifferentThingUpResetsTheFacing()
        {
            var director = BedArmed();
            director.Rotate();
            director.Rotate();
            Assume.That(director.Facing, Is.EqualTo(2));

            director.ArmBuild(BuildingHandle.Wall);
            director.ArmBuild(BuildingHandle.Bed);
            Assert.That(director.Facing, Is.EqualTo(0),
                "a bed picked up after something else does not inherit a facing nothing showed");
        }

        [Test]
        public void PuttingTheBedDownAndBackUpKeepsTheFacing()
        {
            var director = BedArmed();
            director.Rotate();
            Assume.That(director.Facing, Is.EqualTo(1));

            director.ArmBuild(BuildingHandle.Bed); // down
            director.ArmBuild(BuildingHandle.Bed); // up again
            Assert.That(director.Facing, Is.EqualTo(1),
                "the same thing picked up again keeps the turn, because the player is the one who made it");
        }
    }
}
