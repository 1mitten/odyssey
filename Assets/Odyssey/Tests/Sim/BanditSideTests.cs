#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Bandits at a building with fewer free sides than bandits (design 33 §19; owner,
    /// 2026-09-24: <i>"Only one of them started attacking the building after destroying a campfire
    /// - the other 2 said they were fighting but kinda stood around - maybe it was because it
    /// didn't read the building was up on the hill at another depth"</i>).
    ///
    /// <para>Measured in the owner's own save: all three took the same wall of a house standing on
    /// the edge of a terrace step. A side is a cell beside the wall <b>on its own layer</b>, and on
    /// a terrace edge most of those are air over the step below, so that wall had one. One bandit
    /// took it; the other two waited on "Fighting" for as long as the run lasted (3,245 and 3,312
    /// ticks), because the choice asked whether a side could be <i>reached</i> and the driver asked
    /// whether one was <i>free</i>, and every 300 ticks the choice sent them back to the same wall.
    /// Now the choice asks the driver's own question (<see cref="BuildingTargets.HasAFreeSide"/>),
    /// and a bandit whose every side is taken thinks again at once.</para>
    /// </summary>
    public class BanditSideTests
    {
        /// <summary>The real arming rule with the hand pinned to one weapon, for a fixture that needs every bandit alike.</summary>
        sealed class ArmedWithAMachete : WeaponRules
        {
            public override void ArmOnSpawn(Pawn pawn, PawnContext ctx)
            {
                if (pawn.EquippedItem != 0) return;
                int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, pawn.Cell, ItemIndex.Machete, 1, JobDriver.DropSearchRadius);
                if (cell < 0) return;
                ThingId id = ctx.Items.Spawn(ItemIndex.Machete, cell);
                WeaponHand.TakeUp(pawn, ctx.Items.Get(id)!, ctx);
            }
        }

        /// <summary>Paint a cell's terrain and tell the graph, the way <c>KnockbackTests</c> does.</summary>
        static void Paint(ColonyWorld colony, int index, ushort terrain)
        {
            var grid = colony.Grid;
            grid.Terrain[index] = terrain;
            CellRef at = grid.FromIndex(index);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (grid.Size.Contains(at.X + dx, at.Z + dz, at.Y))
                    colony.Pawns.Nav.MarkDirty(grid.Size.Index(at.X + dx, at.Z + dz, at.Y));
            if (NaturalContent.IsSolid(terrain)) grid.Flags[index] |= CellFlags.SolidTerrain;
            else grid.Flags[index] &= ~CellFlags.SolidTerrain;
            if (NaturalContent.IsImpassable(terrain)) grid.Flags[index] |= CellFlags.ImpassableTerrain;
            else grid.Flags[index] &= ~CellFlags.ImpassableTerrain;
        }

        static void RaiseAt(ColonyWorld colony, int cell)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None), $"could not order a wall at {Size.FromIndex(cell)}");
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        /// <summary>
        /// The owner's case, built small: a colonist sealed in eight walls, so no bandit can reach
        /// her and all of them turn on the base (§14b); a step one layer up, two cells long, with a
        /// wall on its near cell — so the wall's only side on its own layer is the step's far cell,
        /// the cells round it being air over the ground below; and three bandits on the ground
        /// beyond it, to whom that wall is the nearest colony building by far.
        /// </summary>
        static (ColonyWorld colony, Pawn[] bandits, int edge, int side) AWallOnTheEdgeOfAStep()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick();

            CellRef s = colony.Start;
            int near = Size.Index(s.X + 8, s.Z, s.Y), far = Size.Index(s.X + 9, s.Z, s.Y);
            Paint(colony, near, NaturalContent.TerrainGrass);
            Paint(colony, far, NaturalContent.TerrainGrass);
            colony.Pawns.Nav.Rebuild();

            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None), "drafted, so she holds inside her walls");
            CellRef at = Size.FromIndex(colonist.Cell);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (dx != 0 || dz != 0) RaiseAt(colony, Size.Index(at.X + dx, at.Z + dz, at.Y));

            int edge = near + Size.LayerStride, side = far + Size.LayerStride;
            RaiseAt(colony, edge);
            colony.World.Tick();

            // All three armed alike, with the machete this was measured with. A bandit is dealt a
            // crowbar or a bat now (design 42 §3), both slower, and in 2,400 ticks the slower gang
            // breaks fewer walls and so strikes fewer distinct ones — which would make the count
            // below a measure of swing speed rather than of whether anybody stood about.
            colony.Pawns.WeaponRules = new ArmedWithAMachete();
            var bandits = new[]
            {
                Spawn(colony, PawnKindIndex.Bandit, Size.Index(s.X + 13, s.Z - 1, s.Y)),
                Spawn(colony, PawnKindIndex.Bandit, Size.Index(s.X + 13, s.Z, s.Y)),
                Spawn(colony, PawnKindIndex.Bandit, Size.Index(s.X + 13, s.Z + 1, s.Y)),
            };
            return (colony, bandits, edge, side);
        }

        /// <summary>
        /// The owner's report: none of the three stands about. One takes the wall on the edge of
        /// the step from its one side; the other two go to other walls; each of them swings within
        /// the run; and no bandit spends more than two ticks attacking a building it has no side of.
        /// Before the fix two of them waited the whole run, from a side the third was holding.
        /// </summary>
        [Test]
        public void ThreeBanditsAtAWallWithOneSideDoNotStandAbout()
        {
            var (colony, bandits, edge, side) = AWallOnTheEdgeOfAStep();
            var ctx = colony.Pawns;
            int handle = colony.Grid.Edifice[edge];

            // The controls: the wall on the edge has exactly one side, a bandit can get to it, and
            // the colonist cannot be reached — so this is the building fallback and nothing else.
            Assert.That(BuildingTargets.TryStanding(ctx, handle, out BuildingTarget wall), Is.True);
            int sides = 0;
            CellRef e = Size.FromIndex(edge);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if ((dx != 0 || dz != 0) && ctx.Nav.Grid.CanEnter(Size.Index(e.X + dx, e.Z + dz, e.Y), TraverseMode.Bandit)) sides++;
            Assert.That(sides, Is.EqualTo(1), "the control: the wall on the step's edge has one side on its layer");
            Assert.That(BuildingTargets.CanReach(ctx, bandits[0], wall, TraverseMode.Bandit), Is.True,
                "the control: a bandit can get up the step to that side");
            Pawn colonist = ctx.Pawns.All[0];
            Assert.That(ctx.Reachable(bandits[0], colonist.Cell, TraverseMode.Bandit), Is.False,
                "the control: the colonist is out of every bandit's reach");

            var tape = new Tape();
            var waiting = new int[bandits.Length];
            var longest = new int[bandits.Length];
            bool edgeTaken = false;
            for (int t = 0; t < 2_400; t++)
            {
                tape.Tick(colony, 1);
                for (int i = 0; i < bandits.Length; i++)
                {
                    Pawn m = bandits[i];
                    if (m.CurrentJob is { DefIndex: JobIndex.AttackMelee } job && m.CombatTarget == 0
                        && job.DestCell == handle) edgeTaken = true;

                    // Waiting at a building: in a building attack, walking nowhere, and not in reach.
                    bool idle = m.CurrentJob is { DefIndex: JobIndex.AttackMelee } j && m.CombatTarget == 0
                        && m.Destination < 0 && BuildingTargets.TryStanding(ctx, j.DestCell, out BuildingTarget at)
                        && !BuildingTargets.InReach(ctx, m.Cell, at);
                    waiting[i] = idle ? waiting[i] + 1 : 0;
                    if (waiting[i] > longest[i]) longest[i] = waiting[i];
                }
            }

            Assert.That(edgeTaken, Is.True, "the control: nobody went for the wall on the edge, so this saw nothing");
            Assert.That(ctx.EdificeDamage.TryGet(edge, out _), Is.True, "the wall on the edge was never struck");
            var struck = new HashSet<int>();
            for (int i = 0; i < bandits.Length; i++)
            {
                Assert.That(tape.Swings(bandits[i]), Is.Not.Empty, $"bandit {i} never swung at anything: it stood about");
                // Two ticks at most: a job given at the end of a tick has no destination until its
                // driver's first look, and two bandits choosing one side in the same tick costs
                // the slower one a second. Before the fix this was the whole run.
                Assert.That(longest[i], Is.LessThanOrEqualTo(2),
                    $"bandit {i} waited {longest[i]} ticks at a building whose every side was taken");
                foreach (var swing in tape.Swings(bandits[i])) struck.Add(Size.Index(swing.Cell));
            }
            Assert.That(struck.Count, Is.GreaterThanOrEqualTo(3), "three bandits, and fewer than three walls struck");
            TestContext.WriteLine($"longest waits {string.Join(", ", longest)}; {struck.Count} cells struck");
            _ = side;
        }

        /// <summary>
        /// The one rule, asked directly: with the wall's one side held by another bandit the wall
        /// has no free side for this one, and the choice passes it over for a wall further off —
        /// while <see cref="BuildingTargets.CanReach"/>, the order's question, still says yes.
        /// </summary>
        [Test]
        public void AWallWhoseOnlySideIsHeldIsPassedOverForOneWithASide()
        {
            var (colony, bandits, edge, side) = AWallOnTheEdgeOfAStep();
            var ctx = colony.Pawns;
            int handle = colony.Grid.Edifice[edge];
            Assert.That(BuildingTargets.TryStanding(ctx, handle, out BuildingTarget wall), Is.True);

            Assert.That(BuildingTargets.TryNearestColonyTarget(ctx, bandits[1], TraverseMode.Bandit, out BuildingTarget first), Is.True);
            Assert.That(first.Handle, Is.EqualTo(handle), "the control: with its side free, the wall on the edge is the nearest");
            Assert.That(BuildingTargets.HasAFreeSide(ctx, bandits[1], wall, TraverseMode.Bandit), Is.True);

            // The first bandit takes the side, as the driver would: attacking the wall, standing on it.
            Pawn holder = bandits[0];
            Stand(colony, holder, side);
            var job = holder.JobBuffer;
            Assert.That(AttackJob.FillBuilding(ctx, holder, wall, job, TraverseMode.Bandit), Is.True);
            Assert.That(colony.Jobs.StartJob(holder, job, colony.World.CurrentTick), Is.True);
            Assert.That(Melee.Holds(ctx, bandits[1], side), Is.True, "the control: the side is held");

            Assert.That(BuildingTargets.CanReach(ctx, bandits[1], wall, TraverseMode.Bandit), Is.True,
                "reach is the order's question and is unchanged: a side can still be got to");
            Assert.That(BuildingTargets.HasAFreeSide(ctx, bandits[1], wall, TraverseMode.Bandit), Is.False,
                "a side another bandit holds was counted as free");
            Assert.That(BuildingTargets.TryNearestColonyTarget(ctx, bandits[1], TraverseMode.Bandit, out BuildingTarget next), Is.True,
                "with the edge taken it found nothing, though the colonist's walls stand");
            Assert.That(next.Handle, Is.Not.EqualTo(handle), "it took the wall whose only side is held");
        }
    }
}
