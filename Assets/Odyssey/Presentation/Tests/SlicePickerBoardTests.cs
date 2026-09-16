#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The picker against the board that is actually played, rather than a hand-built fixture.
    ///
    /// <para><b>Why this file exists.</b> The owner reported twice that a click would not leave the
    /// active layer — "I couldn't select the stones for mining", then "it still only wants to
    /// select anything on its own height" — and both times the unit tests were green. A fixture
    /// proves the arithmetic; it cannot prove that the shape the generator makes is the shape the
    /// fixture assumed. So this generates the real 120 x 120 x 16 wooded board, mirrors it exactly
    /// as the game does, puts the slice where the composition root puts it, and measures.</para>
    ///
    /// <para>Every case carries a <b>control</b> run against the old single-layer behaviour — the
    /// four-argument <c>Pick</c>, which is literally the code that shipped — because a passing
    /// measurement means nothing until the same measurement is seen to fail.</para>
    /// </summary>
    public class SlicePickerBoardTests
    {
        const int Size = 120;
        const int Layers = 16;

        sealed class Board : IDisposable
        {
            public GridSize Size = default!;
            public CellGrid Grid = default!;
            public WorldRenderModel Model = default!;
            public ModuleLibrary Library = default!;
            public int ActiveLayer;
            public SliceSettings Slice = default!;

            public void Dispose() => Library?.Dispose();
        }

        static Board Generate(uint seed = 1u)
        {
            var size = new GridSize(SlicePickerBoardTests.Size, SlicePickerBoardTests.Size, Layers);
            var grid = new CellGrid(size);
            var result = NaturalMapGenerator.Generate(grid, seed, NaturalMapGenDef.For(size).MakeWooded());

            var library = new ModuleLibrary(null);
            var model = new WorldRenderModel(size, new ChunkGrid(size), library);
            model.RefreshAll(grid, result.Context.Edifices);

            // Exactly what OdysseyBootstrap does: the slice opens on the colony's own layer, and
            // that same layer is what followDepth measures "underground" against.
            int active = result.StartCell.Y;
            return new Board
            {
                Size = size,
                Grid = grid,
                Model = model,
                Library = library,
                ActiveLayer = active,
                Slice = new SliceSettings { surfaceLayer = active, followDepth = true },
            };
        }

        /// <summary>The topmost solid cell of a column, or -1 where the column is empty.</summary>
        static int TopSolid(Board board, int x, int z)
        {
            for (int y = Layers - 1; y >= 0; y--)
                if (board.Model.IsSolid(board.Size.Index(x, z, y))) return y;
            return -1;
        }

        static Ray StraightDownAt(int x, int z) => new Ray(
            new Vector3((x + 0.5f) * CellMetrics.SizeXZ, Layers * CellMetrics.SizeY + 40f,
                        (z + 0.5f) * CellMetrics.SizeXZ),
            Vector3.down);

        /// <summary>
        /// <b>The owner's report, measured on the real board.</b>
        ///
        /// <para>Every column whose rock stands above the layer the game opens at — the outcrops,
        /// which are the "stones for mining" — clicked straight down from above. The answer must be
        /// that column's own top block, on whatever layer it happens to stand.</para>
        ///
        /// <para>Straight down rather than obliquely, because a straight ray cannot be occluded by
        /// anything but the same column: any failure is the picker's, not the geometry's.</para>
        /// </summary>
        [Test]
        public void EveryOutcropStandingAboveTheSliceIsPickedWhereItStands()
        {
            using Board board = Generate();

            int candidates = 0, wrong = 0, missed = 0, controlWrong = 0;

            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                int top = TopSolid(board, x, z);
                if (top <= board.ActiveLayer) continue;   // not standing proud of the slice
                candidates++;

                var target = new CellRef(x, z, top);
                Ray ray = StraightDownAt(x, z);

                if (!SlicePicker.Pick(ray, board.Model, board.ActiveLayer, board.Slice, out CellRef got))
                    missed++;
                else if (!got.Equals(target))
                    wrong++;

                // The control: the code that shipped, clipped to the active layer. It answers with
                // a lower cell of the same outcrop, which is exactly the complaint -- the click
                // never leaves the layer being worked.
                if (!SlicePicker.Pick(ray, board.Model, board.ActiveLayer, out CellRef old)
                    || !old.Equals(target))
                    controlWrong++;
            }

            Console.WriteLine(
                $"[Picker] wooded board, slice at L{board.ActiveLayer}: {candidates:N0} columns stand " +
                $"above it. Banded picker: {wrong} wrong, {missed} missed. " +
                $"Single-layer control: {controlWrong} wrong.");

            Assert.That(candidates, Is.GreaterThan(0),
                "no rock on the played board stands above the opening layer, so this test proves " +
                "nothing -- the board changed and the measurement has to change with it");
            Assert.That(controlWrong, Is.EqualTo(candidates),
                "the control must fail on every one of them, or a green result below means nothing");
            Assert.That(missed, Is.Zero, "a click straight down onto rock returned nothing");
            Assert.That(wrong, Is.Zero, "a click straight down onto rock returned the wrong block");
        }

        /// <summary>
        /// The same board and the same rocks, aimed at the way a player aims: down the board
        /// camera's own line, at the middle of the block's top face.
        ///
        /// <para><b>This is the failure the owner actually saw.</b> Clipped to the active layer,
        /// an oblique ray aimed at the top of an outcrop enters that layer's slab <i>past</i> the
        /// rock and lands on the grass behind it — so the player clicks a rock and selects the
        /// ground beyond it, which reads as "it only selects things on my own height".</para>
        ///
        /// <para>A fraction rather than a count, because an oblique ray can legitimately be
        /// occluded by a taller rock in front of the one aimed at. The control is what makes the
        /// number mean something.</para>
        /// </summary>
        [Test]
        public void ClickingTheTopOfAnOutcropSelectsTheOutcropAndNotTheGroundBehindIt()
        {
            using Board board = Generate();

            // The rig's default pitch, and a yaw off the axes so the ray crosses cell corners the
            // way a real camera does.
            Vector3 direction = Quaternion.Euler(48f, 25f, 0f) * Vector3.forward;

            int candidates = 0, hit = 0, controlHit = 0;

            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                int top = TopSolid(board, x, z);
                if (top <= board.ActiveLayer) continue;
                candidates++;

                var target = new CellRef(x, z, top);
                Vector3 face = CellMetrics.FloorCentre(x, z, top) + Vector3.up * CellMetrics.SizeY;
                face = GroundRelief.Lift(face);
                var ray = new Ray(face - direction * 60f, direction);

                if (SlicePicker.Pick(ray, board.Model, board.ActiveLayer, board.Slice, out CellRef got)
                    && got.Equals(target))
                    hit++;

                if (SlicePicker.Pick(ray, board.Model, board.ActiveLayer, out CellRef old)
                    && old.Equals(target))
                    controlHit++;
            }

            double share = candidates == 0 ? 0 : (double)hit / candidates;
            Console.WriteLine(
                $"[Picker] oblique clicks at {candidates:N0} outcrop tops: banded {hit:N0} " +
                $"({share:P1}), single-layer control {controlHit:N0}.");

            Assert.That(candidates, Is.GreaterThan(0));
            Assert.That(controlHit, Is.Zero,
                "the shipped picker cannot return a cell above the slice, so the control must be " +
                "nought -- anything else means this test is not measuring what it claims");
            Assert.That(share, Is.GreaterThan(0.9),
                $"only {share:P1} of clicks aimed at the top of a rock reached that rock");
        }

        /// <summary>
        /// The control for the control: the ordinary meadow, which is most of the board, must keep
        /// answering with the ground block under the cursor. A picker that reached upwards by
        /// breaking the common case would be worse than the one it replaced.
        /// </summary>
        [Test]
        public void TheOpenMeadowStillAnswersWithTheGroundUnderTheCursor()
        {
            using Board board = Generate();

            int candidates = 0, wrong = 0;

            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                int top = TopSolid(board, x, z);

                // Flat ground at the opening layer, with nothing standing in the cell above it.
                if (top != board.ActiveLayer - 1) continue;
                if (board.Model.EdificeDef(board.Size.Index(x, z, board.ActiveLayer)) != 0) continue;
                candidates++;

                if (!SlicePicker.Pick(StraightDownAt(x, z), board.Model, board.ActiveLayer,
                        board.Slice, out CellRef got)
                    || !got.Equals(new CellRef(x, z, top)))
                    wrong++;
            }

            Console.WriteLine($"[Picker] open meadow: {candidates:N0} cells, {wrong} wrong.");

            Assert.That(candidates, Is.GreaterThan(1000), "the meadow is most of the board");
            Assert.That(wrong, Is.Zero, "a click on open ground must give the ground block");
        }
    }
}
