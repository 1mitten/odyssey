#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Doors hold marauders out, and marauders leave beds alone (design 33 §16; the owner,
    /// 2026-09-24: "do what you recommend"). A marauder moves in <see cref="TraverseMode.Marauder"/> —
    /// a colonist's ladders, stairs, hop and wade, but a closed door is a wall — so a door between
    /// it and the colonists sends it to §14b's building fallback, and it breaks the door down. A bed
    /// is never its own choice of target; a player's order may still strike one.
    /// </summary>
    public class MarauderDoorTests
    {
        // ---- the board ----------------------------------------------------------------------

        static ColonyWorld Bare(int colonists = 1)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick();
            return colony;
        }

        static void RaiseAt(ColonyWorld colony, int cell, int building = BuildingHandle.Wall, int stuff = StuffHandle.Wood, int facing = 0)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, stuff, facing), Is.EqualTo(IntentRejection.None),
                $"could not order building {building} at {Size.FromIndex(cell)}");
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        /// <summary>
        /// The colonist drafted — so she holds her cell rather than wandering out through the door —
        /// and sealed in a ring of seven wooden walls with a door on the east, (1, 0). Returns the
        /// ring by offset; the door is <c>ring[(1, 0)]</c>.
        /// </summary>
        static Dictionary<(int, int), int> RoomWithADoor(ColonyWorld colony, Pawn colonist)
        {
            Stand(colony, colonist, Near(colony, 0, 0));
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            CellRef at = Size.FromIndex(colonist.Cell);
            var ring = new Dictionary<(int, int), int>();
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int cell = Size.Index(at.X + dx, at.Z + dz, at.Y);
                ring[(dx, dz)] = cell;
                if ((dx, dz) != (1, 0)) RaiseAt(colony, cell);
            }
            RaiseAt(colony, ring[(1, 0)], BuildingHandle.Door);
            colony.World.Tick();
            Assert.That((colony.Pawns.Nav.Grid.Flags[ring[(1, 0)]] & NavFlags.Door), Is.Not.EqualTo(NavFlags.None),
                "the control: the east side is a door");
            Assert.That(colony.Doors.IsOpen(ring[(1, 0)]), Is.False, "the control: the door is shut");
            return ring;
        }

        static int East(Pawn colonist, int cells)
        {
            CellRef at = Size.FromIndex(colonist.Cell);
            return Size.Index(at.X + cells, at.Z, at.Y);
        }

        static void TickUntil(ColonyWorld colony, System.Func<bool> done, int limit, string what)
        {
            for (int t = 0; t < limit && !done(); t++) colony.World.Tick();
            Assert.That(done(), Is.True, what);
        }

        static bool AttackingBuilding(Pawn pawn, int handle) =>
            pawn.CurrentJob is { DefIndex: JobIndex.AttackMelee } job && pawn.CombatTarget == 0 && job.DestCell == handle;

        /// <summary>Knock the colonist down with one exact blow from the marauder, so nobody is standing.</summary>
        static void Down(ColonyWorld colony, Pawn marauder, Pawn colonist)
        {
            Strike(colony, marauder, colonist, colonist.HpMilli);
            Assert.That(colonist.Downed, Is.True, "the control: she is down");
        }

        // ---- the mode -----------------------------------------------------------------------

        /// <summary>
        /// Who moves how: the marauder's kind names <see cref="TraverseMode.Marauder"/> while its
        /// species stays the colonist's, and every other kind keeps its species' mode.
        /// </summary>
        [Test]
        public void EachKindMovesInItsOwnMode()
        {
            var colony = Bare();
            PawnContent content = colony.Pawns.Content;
            Assert.That(content.ModeOf(PawnKindIndex.Colonist), Is.EqualTo(TraverseMode.Colonist));
            Assert.That(content.ModeOf(PawnKindIndex.MiddenHog), Is.EqualTo(TraverseMode.Animal));
            Assert.That(content.ModeOf(PawnKindIndex.DuctRat), Is.EqualTo(TraverseMode.Climber));
            Assert.That(content.ModeOf(PawnKindIndex.Marauder), Is.EqualTo(TraverseMode.Marauder));
            Assert.That(content.SpeciesOf(PawnKindIndex.Marauder).traverseMode, Is.EqualTo(TraverseMode.Colonist),
                "the marauder is still a person: the mode is its kind's, not its species'");

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, 8, 0));
            Assert.That(marauder.OwnMode, Is.EqualTo(TraverseMode.Marauder));
            Assert.That(colony.Pawns.Pawns.All[0].OwnMode, Is.EqualTo(TraverseMode.Colonist));
        }

        /// <summary>
        /// A closed door is a wall to the hog and to the marauder and nobody else; an open one is a
        /// floor to everybody. The marauder wades, climbs ladders and takes stairs as a colonist does.
        /// </summary>
        [Test]
        public void AClosedDoorIsAWallToTheMarauderAndTheHogOnly()
        {
            var colony = Bare();
            NavGrid grid = colony.Pawns.Nav.Grid;
            int door = Near(colony, 6, 0);
            RaiseAt(colony, door, BuildingHandle.Door);
            colony.World.Tick();

            var shut = new Dictionary<TraverseMode, bool>
            {
                [TraverseMode.Colonist] = true, [TraverseMode.Hauler] = true, [TraverseMode.Climber] = true,
                [TraverseMode.Animal] = false, [TraverseMode.Marauder] = false,
            };
            foreach (var row in shut)
                Assert.That(grid.CanEnter(door, row.Key), Is.EqualTo(row.Value), $"{row.Key} at a closed door");

            colony.Pawns.Nav.SetDoorOpen(door, true);
            foreach (var row in shut)
                Assert.That(grid.CanEnter(door, row.Key), Is.True, $"{row.Key} at an open door");

            Assert.That(TraverseModes.Swims(TraverseMode.Marauder), Is.True, "a marauder wades as a person does");
            var ladder = new Connector(0, ConnectorKind.Ladder, new[] { 0 }, new[] { Size.LayerStride }, Size);
            Assert.That(TraverseModes.Allows(ladder.ModeMask, TraverseMode.Marauder), Is.True, "a marauder climbs a ladder");
        }

        /// <summary>
        /// The marauder's mode is the colonist's less a closed door, on every link of a real board: the
        /// ruined city, with its thirty-four doors. Every link and portal edge that carries the
        /// colonist carries the marauder, except a link into a closed door, which carries only the
        /// colonist. So a new rule that forgot the marauder — a mask, a hop — fails here.
        /// </summary>
        [Test]
        public void OnEveryLinkTheMaraudersModeIsTheColonistsLessAClosedDoor()
        {
            ColonyWorld colony = Golden.City.Build();
            NavGraph nav = colony.Pawns.Nav;
            NavGrid grid = nav.Grid;
            int colonistBit = TraverseModes.Mask(TraverseMode.Colonist), marauderBit = TraverseModes.Mask(TraverseMode.Marauder);
            int doors = 0, doorLinks = 0, links = 0, edges = 0;

            for (int c = 0; c < grid.Flags.Length; c++)
                if ((grid.Flags[c] & NavFlags.Door) != 0 && (grid.Flags[c] & NavFlags.DoorOpen) == 0) doors++;
            Assert.That(doors, Is.GreaterThan(0), "the control: the city has closed doors");

            for (int r = 0; r < nav.RegionCapacity; r++)
            {
                if (!nav.IsRegionAlive(r)) continue;
                int s = nav.AdjacencyStart(r), n = nav.AdjacencyCount(r);
                for (int i = 0; i < n; i++)
                {
                    int l = nav.AdjacencyLink(s + i);
                    int mask = nav.LinkModeMask(l);
                    bool closedDoor = TouchesAClosedDoor(colony.Pawns.Size, grid, nav.LinkCellA(l), nav.LinkCellB(l));
                    bool colonist = (mask & colonistBit) != 0, marauder = (mask & marauderBit) != 0;
                    links++;
                    if (closedDoor && colonist)
                    {
                        doorLinks++;
                        Assert.That(marauder, Is.False, $"link {l} into a closed door carries the marauder");
                    }
                    else
                    {
                        Assert.That(marauder, Is.EqualTo(colonist), $"link {l} ({mask:x2}) treats the marauder unlike a colonist");
                    }
                }
            }
            Assert.That(doorLinks, Is.GreaterThan(0), "the control: some link leads into a closed door");

            for (int c = 0; c < grid.Flags.Length; c++)
                for (int e = nav.FirstPortalEdge(c); e >= 0; e = nav.PortalEdgeNext(e))
                {
                    int mask = nav.PortalEdgeMode(e);
                    edges++;
                    Assert.That((mask & marauderBit) != 0, Is.EqualTo((mask & colonistBit) != 0),
                        $"portal edge {e} from {c} treats the marauder unlike a colonist");
                }
            TestContext.WriteLine($"{links} link ends, {doorLinks} into a closed door, {edges} portal edges, {doors} closed doors");
        }

        static bool IsClosedDoor(NavGrid grid, int cell) =>
            cell >= 0 && (grid.Flags[cell] & NavFlags.Door) != 0 && (grid.Flags[cell] & NavFlags.DoorOpen) == 0;

        /// <summary>Either end is a shut door, or — a diagonal step on one layer — either corner it cuts is.</summary>
        static bool TouchesAClosedDoor(GridSize size, NavGrid grid, int a, int b)
        {
            if (IsClosedDoor(grid, a) || IsClosedDoor(grid, b)) return true;
            if (a < 0 || b < 0) return false;
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            if (p.Y != q.Y || p.X == q.X || p.Z == q.Z) return false;
            return IsClosedDoor(grid, size.Index(p.X, q.Z, p.Y)) || IsClosedDoor(grid, size.Index(q.X, p.Z, p.Y));
        }

        // ---- a door between them -------------------------------------------------------------

        /// <summary>
        /// Behind a closed door a colonist cannot be reached by a marauder, and can by a colonist,
        /// who is also free to walk out. The district says so, and so does the step the mover checks.
        /// </summary>
        [Test]
        public void BehindAClosedDoorSheIsUnreachableToAMarauderAndNotToAColonist()
        {
            var colony = Bare(colonists: 2);
            Pawn inside = colony.Pawns.Pawns.All[0], outside = colony.Pawns.Pawns.All[1];
            Stand(colony, outside, Near(colony, 12, 8));
            var ring = RoomWithADoor(colony, inside);
            int door = ring[(1, 0)];
            Stand(colony, outside, East(inside, 5));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, East(inside, 4));
            var ctx = colony.Pawns;

            Assert.That(ctx.Reachable(marauder, inside.Cell, TraverseMode.Marauder), Is.False, "a marauder can reach her through a shut door");
            Assert.That(ctx.Nav.IsLegalStep(East(inside, 2), door, TraverseMode.Marauder), Is.False, "a marauder may step into a shut door");
            Assert.That(new PathFinder(ctx.Nav).FindPath(marauder.Cell, inside.Cell, TraverseMode.Marauder).Status,
                Is.Not.EqualTo(PathStatus.Success), "a marauder found a path through a shut door");

            Assert.That(ctx.Reachable(outside, inside.Cell, TraverseMode.Colonist), Is.True, "a colonist cannot reach her through her own door");
            Assert.That(ctx.Reachable(inside, outside.Cell), Is.True, "she cannot walk out through her own door");
            Assert.That(new PathFinder(ctx.Nav).FindPath(outside.Cell, inside.Cell, TraverseMode.Colonist).Status,
                Is.EqualTo(PathStatus.Success), "a colonist found no path through the door");
        }

        /// <summary>
        /// The owner's C6 answer, "kill colonists, destroy base", with the door holding: the marauder
        /// goes for the door — the nearest colony building it can get beside — never sets foot in it
        /// while it stands, breaks it down, strikes no wall of the ring, and turns on her.
        /// </summary>
        [Test]
        public void AMarauderBreaksTheDoorDownAndThenGoesForHer()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            var ring = RoomWithADoor(colony, colonist);
            int door = ring[(1, 0)];
            int handle = colony.Grid.Edifice[door];
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, East(colonist, 5));

            TickUntil(colony, () => AttackingBuilding(marauder, handle), 60, "the marauder never went for the door");
            Assert.That(marauder.CurrentJob!.PlayerForced, Is.False, "a marauder's own choice is not an order");

            for (int t = 0; t < 20_000 && colony.Grid.Edifice[door] == handle; t++)
            {
                colony.World.Tick();
                if (colony.Grid.Edifice[door] != handle) break;
                Assert.That(marauder.Cell, Is.Not.EqualTo(door), $"tick {t}: the marauder stood in the doorway of a door still standing");
                Assert.That(colony.Doors.IsOpen(door), Is.False, $"tick {t}: the door was opened");
            }
            Assert.That(colony.Grid.Edifice[door], Is.Not.EqualTo(handle), "the marauder never broke the door down");
            foreach (var side in ring)
                if (side.Value != door)
                    Assert.That(colony.Pawns.EdificeDamage.TryGet(side.Value, out _), Is.False, $"it struck the wall at {side.Key} too");

            TickUntil(colony, () => marauder.CombatTarget == colonist.Id.Value, 600, "through the door, and it did not turn on her");
        }

        /// <summary>
        /// The control on the door: open, the same room is a room with a way in, and the marauder
        /// goes straight for her and never touches the door.
        /// </summary>
        [Test]
        public void WithTheDoorOpenItGoesStraightIn()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            var ring = RoomWithADoor(colony, colonist);
            int door = ring[(1, 0)];
            colony.Pawns.Nav.SetDoorOpen(door, true);
            colony.World.Tick();
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, East(colonist, 5));

            TickUntil(colony, () => marauder.CombatTarget == colonist.Id.Value, 60, "through an open door, and it did not go for her");
            TickUntil(colony, () => Melee.InReach(colony.Pawns, marauder, colonist, TraverseMode.Marauder), 600,
                "it never got to her through the open door");
            Assert.That(colony.Pawns.EdificeDamage.TryGet(door, out _), Is.False, "it struck an open door");
        }

        /// <summary>
        /// Target choice is unchanged (design 33 §16c): the nearest colony building to the marauder,
        /// not the door. With the door on the far side it breaks the nearest wall, as §14b's ring did.
        /// </summary>
        [Test]
        public void ItTakesTheNearestBuildingNotTheDoor()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            var ring = RoomWithADoor(colony, colonist);
            CellRef at = Size.FromIndex(colonist.Cell);
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Size.Index(at.X - 5, at.Z, at.Y));
            int west = colony.Grid.Edifice[ring[(-1, 0)]];

            TickUntil(colony, () => AttackingBuilding(marauder, west), 60, "the marauder did not go for the wall nearest it");
        }

        /// <summary>
        /// A marauder knocked back is not knocked into a shut door (<see cref="CombatSystem.KnockbackCell"/>
        /// asks the target's own mode), which would stand it in the doorway and open the door. The
        /// control is a colonist in the same place, who may be.
        /// </summary>
        [Test]
        public void AMarauderIsNotKnockedIntoAShutDoor()
        {
            var colony = Bare(colonists: 2);
            Pawn hitter = colony.Pawns.Pawns.All[0], colonist = colony.Pawns.Pawns.All[1];
            int door = Near(colony, 6, 0);
            RaiseAt(colony, door, BuildingHandle.Door);
            colony.World.Tick();
            CellRef d = Size.FromIndex(door);
            int before = Size.Index(d.X - 1, d.Z, d.Y), behind = Size.Index(d.X - 2, d.Z, d.Y);
            Stand(colony, hitter, behind);
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, before);

            Assert.That(CombatSystem.KnockbackCell(colony.Pawns, hitter, marauder), Is.EqualTo(-1),
                "a marauder was knocked into a shut door");

            colony.Pawns.Pawns.Despawn(marauder);
            Stand(colony, colonist, before);
            Assert.That(CombatSystem.KnockbackCell(colony.Pawns, hitter, colonist), Is.EqualTo(door),
                "the control: a colonist in the same place is knocked into the doorway");
        }

        /// <summary>
        /// An idle marauder wanders in its own mode. With nobody standing and nothing of the colony's
        /// to break, it wanders — and never through a shut door, here a door of the ruined city's
        /// (not colony-built, so not a target) that is the only way out of the yard it stands in.
        /// </summary>
        [Test]
        public void AnIdleMarauderDoesNotWanderThroughAShutDoor()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            CellRef c = Size.FromIndex(Near(colony, 10, 0));
            // A yard five by five inside, walled on its edge, a door in the middle of the east side.
            var walls = new List<int>();
            int door = Size.Index(c.X + 3, c.Z, c.Y);
            for (int dz = -3; dz <= 3; dz++)
            for (int dx = -3; dx <= 3; dx++)
            {
                if (dx > -3 && dx < 3 && dz > -3 && dz < 3) continue;
                int cell = Size.Index(c.X + dx, c.Z + dz, c.Y);
                if (cell == door) continue;
                RaiseAt(colony, cell);
                walls.Add(cell);
            }
            RaiseAt(colony, door, BuildingHandle.Door);
            var records = colony.Construction.Edifices.Records;
            foreach (int cell in new List<int>(walls) { door })
            {
                int h = colony.Grid.Edifice[cell];
                var placed = records[h];
                placed.Built = false; // the city's, so nothing here is a target of its own choice
                records[h] = placed;
            }
            colony.World.Tick();

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Size.Index(c.X, c.Z, c.Y));
            Down(colony, marauder, colonist);

            int wanders = 0;
            for (int t = 0; t < 6_000; t++)
            {
                colony.World.Tick();
                if (marauder.CurrentJob?.DefIndex == JobIndex.Wander) wanders++;
                Assert.That(marauder.Cell, Is.Not.EqualTo(door), $"tick {t}: the idle marauder walked into the shut door");
                Assert.That(marauder.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee), $"tick {t}: it attacked something");
            }
            Assert.That(wanders, Is.GreaterThan(0), "the control: it wandered at all");
            Assert.That(colony.Doors.IsOpen(door), Is.False);
        }

        // ---- beds ----------------------------------------------------------------------------

        /// <summary>
        /// With everybody down and only a bed to break, a marauder leaves it alone (design 33 §16):
        /// a downed colony keeps somewhere to be carried to. With a wall beside it too — further off
        /// than the bed — it breaks the wall.
        /// </summary>
        [Test]
        public void AMarauderLeavesABedAloneAndBreaksTheWallInstead()
        {
            var colony = Bare();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            CellRef at = Size.FromIndex(Near(colony, 8, 0));
            int bed = Size.Index(at.X + 2, at.Z, at.Y);
            RaiseAt(colony, bed, BuildingHandle.Bed, facing: 1);
            colony.World.Tick();
            int bedHandle = colony.Grid.Edifice[bed];
            Assert.That(BuildingTargets.TryStanding(colony.Pawns, bedHandle, out _), Is.True, "the control: a bed is a target");

            // A marauder that came for nothing (design 33 §17): with nobody standing and only a bed,
            // a looter would carry off the scenario's meals and be gone before the wall went up.
            // This test is about the bed, so what it came for is set aside, in this colony's own
            // content record and nowhere else.
            colony.Pawns.Content.KindMotive[PawnKindIndex.Marauder] = Motive.None;

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Size.Index(at.X, at.Z, at.Y));
            Down(colony, marauder, colonist);
            for (int t = 0; t < 600; t++)
            {
                colony.World.Tick();
                Assert.That(marauder.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee), $"tick {t}: it attacked the bed");
            }
            Assert.That(colony.Pawns.EdificeDamage.TryGet(bed, out _), Is.False, "the bed took a blow");

            int wall = Size.Index(at.X - 6, at.Z, at.Y);
            RaiseAt(colony, wall);
            colony.World.Tick();
            Assert.That(colony.Pawns.Distance(marauder.Cell, bed), Is.LessThan(colony.Pawns.Distance(marauder.Cell, wall)),
                "the control: the bed is the nearer");
            TickUntil(colony, () => AttackingBuilding(marauder, colony.Grid.Edifice[wall]), 600, "it did not go for the wall");
        }

        /// <summary>
        /// The bed is spared from the marauder's choice alone: a player may still order an attack on
        /// one (C6's answer (c)), and the blows land.
        /// </summary>
        [Test]
        public void APlayerCanStillOrderAnAttackOnABed()
        {
            var colony = Board(colonists: 1, beds: 0);
            colony.World.Tick();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Stand(colony, colonist, Near(colony, 0, 0));
            int bed = Near(colony, 3, 0);
            RaiseAt(colony, bed, BuildingHandle.Bed, facing: 1);
            colony.World.Tick();
            Assert.That(BuildingTargets.IsMarauderTarget(colony.Grid.Edifice[bed] >= 0
                ? colony.Construction.Edifices.Records[colony.Grid.Edifice[bed]].Def : (ushort)0), Is.False,
                "the control: a bed is not a marauder's target");
            Assert.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));

            Assert.That(Send(colony, new Intent(IntentKind.OrderAttack, Size.FromIndex(bed), colonist.Id.Value, 0)),
                Is.EqualTo(IntentRejection.None), "the order on a bed was refused");
            TickUntil(colony, () => colony.Pawns.EdificeDamage.TryGet(bed, out _), 2_000, "the ordered blows never landed on the bed");
        }
    }
}
