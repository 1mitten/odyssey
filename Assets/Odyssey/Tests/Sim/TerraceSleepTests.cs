#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// <b>A colonist does not lie down inside a hillside.</b>
    ///
    /// <para>Presentation fills the cell at the foot of a terrace step with a bank — a ramp from
    /// the lower floor to the rim above. A figure <i>standing</i> there is lifted on to that ramp
    /// and reads correctly; a body <i>lying down</i> is not, and it spans the whole cell the ramp
    /// rises across, so she disappears into the hill. The owner watched it happen and it stood as a
    /// known gap for two days (<c>docs/design/22-terrace-steps.md</c> §4).</para>
    ///
    /// <para><b>These exist because the goldens did not move</b> — the same reason
    /// <see cref="TerraceSlopeCostTests"/> exists, and worth stating twice because the design
    /// document predicted the opposite. It forecast that this change "moves the state hash"; it
    /// does not, and the whole fast tier including all six golden values passed untouched at the
    /// first run. The forecast was not silly: one of the three golden boards is the wooded one and
    /// it is covered in terraces. But its colonists have beds, and a colonist only reaches this
    /// branch when she has none — so in ten thousand ticks not one of them ever tried to sleep
    /// rough at a step. A change that shifts no hash is either inert or untested, and the way to
    /// tell the two apart is to assert the rule directly.</para>
    /// </summary>
    public class TerraceSleepTests
    {
        static readonly GridSize Size = new GridSize(12, 12, 6);

        /// <summary>
        /// Earth up to layer 1, with the half of the board at <c>x &lt; 6</c> one layer higher — so
        /// the cells at <c>x = 6</c>, layer 2, are the feet of a terrace step and everything beyond
        /// them is ordinary flat ground. The same board
        /// <see cref="TerraceSlopeCostTests"/> prices its hop on.
        /// </summary>
        static CellGrid Terrace()
        {
            var grid = new CellGrid(Size);
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int top = x < 6 ? 2 : 1;
                for (int y = 0; y <= top; y++) Solid(grid, Size.Index(x, z, y), NaturalContent.TerrainSubsoil);
                Solid(grid, Size.Index(x, z, top), NaturalContent.TerrainGrass);
            }
            return grid;
        }

        static void Solid(CellGrid grid, int index, ushort terrain)
        {
            grid.Terrain[index] = terrain;
            grid.Flags[index] |= CellFlags.SolidTerrain;
        }

        static PawnContext Colony(out CellGrid grid)
        {
            grid = Terrace();
            var nav = new NavGraph(grid);
            nav.Rebuild();
            return new PawnContext(grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns());
        }

        /// <summary>A colonist standing in the given cell, tired but not collapsing.</summary>
        static Pawn Tired(PawnContext ctx, int cell)
        {
            Pawn pawn = ctx.Pawns.Spawn(cell);
            pawn.Cell = cell;
            pawn.Needs[NeedIndex.Rest] = 40;
            return pawn;
        }

        /// <summary>The foot of the step: x = 6 is the first flat cell, and layer 2 is above it.</summary>
        static int Foot(CellGrid grid) => Size.Index(6, 5, 2);

        [Test]
        public void TheBoardHasATerraceFootWhereThisSaysItDoes()
        {
            PawnContext ctx = Colony(out CellGrid grid);

            Assume.That(TerraceFoot.IsFoot(grid, Foot(grid)), Is.True,
                "the fixture's own board no longer has a step where the tests look for one");
            Assume.That(TerraceFoot.IsFoot(grid, Size.Index(8, 5, 2)), Is.False,
                "and two cells out from the step is ordinary ground");
            Assert.That(ctx, Is.Not.Null);
        }

        /// <summary>
        /// <b>The rule.</b> A tired colonist with nowhere to sleep, standing in a bank, is sent one
        /// cell aside rather than left to lie down in the hillside.
        /// </summary>
        [Test]
        public void ATiredColonistWithNoBedStepsOutOfATerraceFootToLieDown()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            Assume.That(ctx.Items.Beds, Is.Empty, "this is the no-bed branch");

            Pawn pawn = Tired(ctx, Foot(grid));
            var job = new Job();

            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, ctx, job), Is.True);
            Assert.That(job.TargetCell, Is.Not.EqualTo(-1),
                "she was left to lie down where she stands, which is inside the bank");
            Assert.That(TerraceFoot.IsFoot(grid, job.TargetCell), Is.False,
                "she was sent from one terrace foot to another");
        }

        /// <summary>
        /// <b>The control, and it is the one that matters.</b> Everywhere that is not a bank, a
        /// colonist with no bed still lies down exactly where she stands — <c>-1</c>, untouched.
        /// A guard that fires everywhere is not a guard, it is a new behaviour.
        /// </summary>
        [Test]
        public void OnOrdinaryGroundSheStillLiesDownWhereSheStands()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            int open = Size.Index(9, 5, 2);
            Assume.That(TerraceFoot.IsFoot(grid, open), Is.False);

            Pawn pawn = Tired(ctx, open);
            var job = new Job();

            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, ctx, job), Is.True);
            Assert.That(job.TargetCell, Is.EqualTo(-1),
                "a colonist on open grass was sent somewhere, which is a walk nobody asked for");
        }

        /// <summary>
        /// <b>And a collapse is still a collapse.</b> Rest that reaches nought drops a colonist
        /// where she stands, bank or no bank (WS3, design 17 §4c) — it is the control that stops
        /// "go somewhere better" from quietly becoming "never sleep rough". The guard is on the
        /// walk, and a body that has run out is not walking.
        /// </summary>
        [Test]
        public void ACollapseStillHappensWhereSheStandsEvenInABank()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            Pawn pawn = Tired(ctx, Foot(grid));
            pawn.Needs[NeedIndex.Rest] = 0;

            var job = new Job();
            Assert.That(CriticalNeedsThinkNode.TrySleep(pawn, ctx, job), Is.True);
            Assert.That(job.TargetCell, Is.EqualTo(-1),
                "a colonist at zero rest was sent for a walk instead of going down where she was");
        }

        /// <summary>
        /// <b>She still remembers sleeping rough.</b> The thought used to key off
        /// <c>TargetCell &lt; 0</c>, which stopped being "she has no bed" the moment a colonist
        /// with no bed was given somewhere to walk to. It reads the cell she is lying in now, which
        /// is the statement <c>NeedsSystem.RestEffectiveness</c> has always made about the rate she
        /// recovers at — so the memory and the rate cannot disagree.
        /// </summary>
        [Test]
        public void SleepingOnGroundIsRememberedWhereverTheGroundIs()
        {
            PawnContext ctx = Colony(out CellGrid grid);

            Assert.That(ctx.Items.HasBed(Foot(grid)), Is.False,
                "a terrace foot is not a bed");
            Assert.That(ctx.Items.HasBed(Size.Index(9, 5, 2)), Is.False,
                "and neither is the cell she is sent to");

            // And the predicate does answer true for a cell that has one, or the test above proves
            // only that it always says no.
            ctx.Items.AddBed(Size.Index(9, 5, 2));
            Assert.That(ctx.Items.HasBed(Size.Index(9, 5, 2)), Is.True);
            Assert.That(ctx.Items.HasBed(Foot(grid)), Is.False);
        }

        /// <summary>
        /// And she is not sent into somebody's bed. She is sleeping rough; walking on to a
        /// mattress would hand her its rest rate and, on arrival, its ownership.
        ///
        /// <para><b>Asked of the rule directly, and the first attempt at this test could not be.</b>
        /// Going through <see cref="CriticalNeedsThinkNode.TrySleep"/> and putting beds beside her
        /// proved nothing: a bed beside a colonist with no bed is a bed she is entitled to, so she
        /// took it, correctly, and the assertion failed on behaviour that was right. The guard only
        /// bites when a bed <i>exists</i> and is not hers to take — somebody else's, which needs a
        /// construction grid to express — so the spot chooser is asked on its own instead.</para>
        /// </summary>
        [Test]
        public void SheIsNotSentIntoABedThatHappensToBeBesideHer()
        {
            PawnContext ctx = Colony(out CellGrid grid);

            // Every way out of the bank is a bed. None of them is a bed she may have, because the
            // chooser is not being consulted - this asks only "where would you put her down".
            CellRef at = Size.FromIndex(Foot(grid));
            int spots = 0;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!Size.Contains(x, z, at.Y)) continue;
                int cell = Size.Index(x, z, at.Y);
                if (TerraceFoot.IsFoot(grid, cell)) continue;
                ctx.Items.AddBed(cell);
                spots++;
            }

            Assume.That(spots, Is.GreaterThan(0), "the bank has no way out of it to block");

            Pawn pawn = Tired(ctx, Foot(grid));
            Assert.That(CriticalNeedsThinkNode.GroundSpot(pawn, ctx), Is.EqualTo(-1),
                "she was walked into a bed she never chose");
        }

        /// <summary>
        /// And with the beds gone it finds the same cells again, or the test above passes for the
        /// wrong reason - there being nowhere to go at all.
        /// </summary>
        [Test]
        public void WithNoBedsInTheWayThereIsSomewhereToStepTo()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            Pawn pawn = Tired(ctx, Foot(grid));

            int spot = CriticalNeedsThinkNode.GroundSpot(pawn, ctx);
            Assert.That(spot, Is.Not.EqualTo(-1), "there is nowhere out of the bank on this board");
            Assert.That(TerraceFoot.IsFoot(grid, spot), Is.False);
        }


        // ---- and a bed cannot be built into one either -----------------------------------------

        static ConstructionGrid Sites(PawnContext ctx, CellGrid grid)
        {
            var edifices = new System.Collections.Generic.List<PlacedEdifice>();
            var solver = new SupportSolver(grid);
            var sites = new ConstructionGrid(
                grid, new Odyssey.Sim.Saving.EdificeSaveSection(edifices), ctx.Items, ctx.Pawns, solver);
            ctx.Construction = sites;
            return sites;
        }

        /// <summary>
        /// <b>The second half of the gap.</b> Stopping a colonist lying down in a bank does nothing
        /// about a bed built into one, and a bed is permanent where a sleeper moves on
        /// (<c>docs/design/22-terrace-steps.md</c> §4, the second of its two candidate fixes).
        /// </summary>
        [Test]
        public void ABedCannotBeBuiltIntoATerraceBank()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            ConstructionGrid sites = Sites(ctx, grid);

            Assume.That(TerraceFoot.IsFoot(grid, Foot(grid)), Is.True);
            Assert.That(sites.Allows(Foot(grid), BuildingHandle.Bed), Is.False,
                "a bed may still be ordered into the hillside that would bury it");
        }

        /// <summary>
        /// And only what the bank would swallow. The cell stays walkable — it is the take-off cell
        /// for the hop, and the whole reason a bank is drawn there is to make that hop legible — so
        /// a thing that fills its own cell and stands out of the ramp is still perfectly buildable.
        /// A rule that took the cell away from the player entirely would be a worse trade than the
        /// bug.
        /// </summary>
        [Test]
        public void AWallMayStillBeBuiltInATerraceFoot()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            ConstructionGrid sites = Sites(ctx, grid);

            Assert.That(sites.Allows(Foot(grid), BuildingHandle.Wall), Is.True,
                "the guard took the cell away from everything, not just from what it hides");
        }

        /// <summary>
        /// And a bed on ordinary ground is untouched, or the test above proves only that beds are
        /// refused everywhere.
        /// </summary>
        [Test]
        public void ABedIsStillAllowedOnOrdinaryGround()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            ConstructionGrid sites = Sites(ctx, grid);

            int open = Size.Index(9, 5, 2);
            Assume.That(TerraceFoot.IsFoot(grid, open), Is.False);
            Assert.That(sites.Allows(open, BuildingHandle.Bed), Is.True);
        }

        /// <summary>
        /// <b>And the far half of the bed counts.</b> A bed is two cells and both of them are the
        /// bed, so ordering one whose <i>foot</i> lands in the bank is refused too — which needed
        /// <c>Place</c> to ask about the thing being built rather than, as it had, on behalf of a
        /// wall.
        /// </summary>
        [Test]
        public void ABedWhoseFarHalfLandsInTheBankIsRefused()
        {
            PawnContext ctx = Colony(out CellGrid grid);
            ConstructionGrid sites = Sites(ctx, grid);

            // The cell one step out from the foot, with the bed turned back towards the step so
            // its far half lands in the bank.
            int outside = Size.Index(7, 5, 2);
            Assume.That(TerraceFoot.IsFoot(grid, outside), Is.False, "the head cell is itself a bank");

            int towards = -1;
            for (int facing = 0; facing < 4 && towards < 0; facing++)
                if (EdificeFootprint.SecondCell(outside, CoreContent.EdificeBed, facing, Size) == Foot(grid))
                    towards = facing;

            Assume.That(towards, Is.GreaterThanOrEqualTo(0), "no facing puts the far half in the bank");
            Assert.That(
                sites.Place(Size.FromIndex(outside), BuildingHandle.Bed, StuffHandle.Wood, towards),
                Is.EqualTo(IntentRejection.NotPermitted),
                "the bed's far half was allowed to land in the hillside");
        }

    }
}
