#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Hand a colonist a marked cell to dig out.
    ///
    /// The scan walks the designation grid's own list of designated cells, never the map, so it
    /// costs what the orders cost and not what the board costs — the same shape
    /// <see cref="FellWorkGiver"/> uses, and for the same reason.
    /// </summary>
    public sealed class MineWorkGiver : WorkGiver
    {
        public override string Name => "Mine";

        public override int WorkType => WorkTypeIndex.Mining;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var designations = ctx.Designations;
            if (designations == null) return false;

            var cells = designations.Cells;
            int best = -1;
            int bestStand = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (designations.At(cell) != DesignationKind.Mine) continue;
                if (!designations.CanMine(cell)) continue;
                if (IsBuriedUnderAnotherOrder(designations, cell)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;

                int stand = StandToMine(ctx, pawn, cell);
                if (stand < 0) continue;

                bestDistance = distance;
                best = cell;
                bestStand = stand;
            }

            if (best < 0) return false;

            job.Reset(JobIndex.Mine);
            job.TargetCell = bestStand;
            job.DestCell = best;
            return true;
        }

        /// <summary>
        /// Is there another standing order directly above this cell?
        ///
        /// <para>A marked stack is worked from the top down, because cutting out the bottom of one
        /// first leaves the rock above it hanging in the air. Nothing catches that yet — the
        /// generator's column check runs at generation only, and collapse is U29's work — so a
        /// quarry dug bottom-first would simply leave boulders floating over the hole.</para>
        ///
        /// <para>A tapering outcrop is safe from this by its own geometry: the ring below a peak
        /// has rock on every side until the peak goes, so nobody can reach the lower cell anyway.
        /// A **terrace step** is not, and that is the case this exists for — a vertical face two
        /// cells tall whose bottom can be cut out from the side while the top is still there.</para>
        ///
        /// <para>It asks only about <em>designated</em> cells, which is what keeps it from blocking
        /// ordinary work: cutting an adit into a cliff has undug rock above it too, and that is
        /// not a stack being worked in the wrong order — it is the cliff.</para>
        /// </summary>
        static bool IsBuriedUnderAnotherOrder(DesignationGrid designations, int cell)
        {
            int above = cell + designations.Size.LayerStride;
            if (above >= designations.Size.CellCount) return false;
            return designations.At(above) == DesignationKind.Mine && designations.CanMine(above);
        }

        /// <summary>
        /// Where a colonist stands to cut this cell out. Three stances, tried in this order:
        /// beside it, on the rim above it, and only then on top of it.
        ///
        /// <para><b>Beside</b> — one of the eight neighbours on the same layer, diagonals
        /// included, because a colonist can swing round a corner. This is an adit into a terrace
        /// or an outcrop: the rock is at eye level, the swing is level, and nothing moves under
        /// the colonist when the cell goes.</para>
        ///
        /// <para><b>On the rim</b> — one of the eight neighbours a layer up, and only where the
        /// rock's own ceiling is open. The top face is then exactly at the colonist's feet, one
        /// cell across, and the stroke comes down into it. It costs nothing either: the floor that
        /// goes is not the one being stood on. This is how a person digs, and it is the stance the
        /// owner asked for after watching a miner swing horizontally through the air above a cell
        /// it was cutting a layer below. The ceiling test is what keeps it honest — a buried cell
        /// has no top face to strike, and without it the giver hands out stances at rock nobody
        /// can get near.</para>
        ///
        /// <para><b>On top</b> — last, because it is the one that costs. It is also unavoidable:
        /// the first cut into flat ground has no rim to stand on, and there is no other stance for
        /// starting a shaft. The floor goes with the cell, and the colonist steps down into the
        /// hole it made (<c>MineJobDriver.StepDownOntoTheFloorJustCut</c>). Ordering it last means
        /// a step down is a consequence of the shape of the dig rather than a surprise: once one
        /// cell of a shaft is open, every cell beside it is worked from the rim.</para>
        ///
        /// <para>Both of the stances above the rock want the same thing from presentation — a
        /// stroke aimed downward rather than level — and <c>WorkStyle.Dip</c> is where that
        /// lives.</para>
        /// </summary>
        public static int StandToMine(PawnContext ctx, Pawn pawn, int cell)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);

            int beside = NearestStandOnLayer(ctx, pawn, at, at.Y);
            if (beside >= 0) return beside;

            int above = cell + size.LayerStride;
            if (above >= size.CellCount) return -1;

            // Only if the rock's own ceiling is open. A cell with solid rock on top of it has no
            // top face to strike: a colonist stood on the rim of a *buried* cell would be swinging
            // at the cell above instead, which is the wall of the tunnel it is standing in. That
            // is not a cosmetic mistake — it is the work giver claiming a stance that does not
            // exist, and it showed up as a dozen orders accepted on rock nobody could get near.
            if (!ctx.Cells.IsSolidTerrain(above))
            {
                int rim = NearestStandOnLayer(ctx, pawn, at, at.Y + 1);
                if (rim >= 0) return rim;
            }

            if (!ctx.Cells.IsWalkable(above) || !ctx.Reachable(pawn, above)) return -1;
            return above;
        }

        /// <summary>
        /// The nearest walkable, reachable cell of the eight around <paramref name="at"/> in XZ,
        /// taken on <paramref name="layer"/> rather than on the cell's own.
        /// </summary>
        static int NearestStandOnLayer(PawnContext ctx, Pawn pawn, CellRef at, int layer)
        {
            GridSize size = ctx.Size;
            int best = -1, bestDistance = int.MaxValue;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, layer)) continue;

                int candidate = size.Index(x, z, layer);
                if (!ctx.Cells.IsWalkable(candidate)) continue;

                int distance = ctx.Distance(pawn.Cell, candidate);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, candidate)) continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }
    }

    /// <summary>
    /// Walk to a marked cell, cut it out of the world, and leave whatever it was made of on the
    /// floor.
    ///
    /// <para>Priced per material: the work is <c>TerrainAt(terrain).workToClear</c>, read as the
    /// colonist swings rather than taken from the job def, so rock costs 700 ticks, an iron seam
    /// 900 and a coal seam 760. One number on the job def would have made every cell cost the
    /// same, which is the difference between materials.</para>
    ///
    /// <para>The order is cleared the moment the last swing lands, so no other colonist sets off
    /// for it. The world edit — the cell going, the seams around it becoming known, the stone
    /// appearing — is a structural event and runs in the deferred phase of the same tick, like
    /// every collapse and removal: a scan walking the grid must never see it move.</para>
    /// </summary>
    public class MineJobDriver : JobDriver
    {
        /// <summary>
        /// The rock face, once the walk is over and the swings have started. Presentation turns
        /// this into a tool in the hands and an arm that comes down on it; before the walk ends it
        /// is -1, so a colonist crossing the map does it empty-handed.
        ///
        /// <para>The destination cell in preference to the target cell, exactly as
        /// <see cref="FellJobDriver"/> does: the destination is the cell being cut and the target
        /// is where the colonist stands to cut it, and the figure has to face the rock rather than
        /// its own feet.</para>
        ///
        /// <para>Without this a miner publishes <c>Working = false</c> and stands at the face with
        /// its arms down, however good the pose work is — the seam is only half a contract until
        /// both ends are wired. The pose, the pickaxe and the stone chips are a separate piece of
        /// work; this is the half the simulation owes it.</para>
        /// </summary>
        public override int WorkFocus =>
            ToilIndex < 1 ? -1 : Job.DestCell >= 0 ? Job.DestCell : Job.TargetCell;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Job.DestCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var designations = ctx.Designations;
            if (designations == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // Somebody else dug it, or the player changed their mind: stop, do not swing at air.
            if (designations.At(cell) != DesignationKind.Mine || !designations.CanMine(cell))
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            ushort terrain = ctx.Cells.Terrain[cell];

            // Banked on the cell rather than counted on the job. A miner who breaks off for a
            // meal, a sleep or a mental break used to take the whole morning's work with it, and
            // the next colonist started the face from nothing — hours quietly thrown away, and
            // the reason a half-cut face could never be drawn as half cut.
            ToilProgress++;
            if (designations.AddWork(cell, 1) < NaturalContent.TerrainAt(terrain).workToClear)
                return JobStatus.Ongoing;

            designations.Clear(cell);
            ctx.Defer(_ => MineCell(ctx, cell, terrain));
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// Take the cell out of the world. Five things change, every one of them has to, and they
        /// are listed here in one place rather than discovered one bug at a time.
        /// </summary>
        public static void MineCell(PawnContext ctx, int cell, ushort terrain)
        {
            var grid = ctx.Cells;
            GridSize size = ctx.Size;

            grid.Terrain[cell] = NaturalContent.TerrainAir;
            grid.Flags[cell] &= ~CellFlags.SolidTerrain;

            // 1. The colony can now see what the walls of the hole are made of.
            grid.RevealAround(cell);

            // 2. The cell and everything touching it must be re-meshed. The neighbours are not an
            //    optimisation to skip: a seam revealed by this dig draws differently now, and the
            //    vertical neighbours are in different chunks entirely, a chunk being one layer.
            MarkChunksAround(ctx, cell);

            // 3. What is walkable changed here and, through the floor rule, in the cell above.
            ctx.Nav.MarkDirty(cell);
            int above = cell + size.LayerStride;
            if (above < size.CellCount) ctx.Nav.MarkDirty(above);

            // 3a. A climb is the claim that there is a rock face here. This cut may have just
            //     taken one away, and a climb against a wall that is gone is a colonist going up
            //     through clear air — the thing the owner reported seeing.
            RetireClimbsThatLostTheirWall(ctx, cell);

            // 3b. A shaft has to be climbable, or the colonist that cut it is lost down it.
            ClimbTheShaft(ctx, cell);

            // 4. Anybody standing on this cell is now standing on nothing.
            StepDownOntoTheFloorJustCut(ctx, cell);

            // 4a. And anything LYING on it. The cell above has just lost its floor, so whatever
            //     was resting there goes down the hole — which is also how a quarry ends up with
            //     its spoil in one place at the bottom rather than shelved up its sides.
            DropWhatWasRestingOnIt(ctx, cell);

            // 5. What the cell was made of, if it left anything.
            SpawnYield(ctx, cell, terrain);
        }

        /// <summary>
        /// Join a newly opened cell to whichever of its vertical neighbours is also open.
        ///
        /// <para>Both directions, because a shaft is cut in both: downward, where the cell above
        /// is the one the colonist was standing in, and upward, where a dig breaks into a chamber
        /// or an older working from below. Asking the question as "is my vertical neighbour open"
        /// covers the two without caring which happened.</para>
        ///
        /// <para><b>There is deliberately nothing to look at.</b> A ladder edifice was placed
        /// here for one commit and taken out again: the owner's words were "these ladders
        /// shouldn't be visible". They were right — a shaft a colonist digs with a pick does not
        /// come with a ladder in it, and drawing one put a free, unbuilt fixture in every pit on
        /// the board. What a colonist does instead is climb the rock, and making that read is
        /// presentation's job (<c>PawnFigureDirector</c>), not a prop's.</para>
        /// </summary>
        static void ClimbTheShaft(PawnContext ctx, int cell)
        {
            GridSize size = ctx.Size;
            CellGrid grid = ctx.Cells;

            ClimbOutOf(ctx, cell);

            // And out of whatever is under it, which may only now have a way up.
            int below = cell - size.LayerStride;
            if (below >= 0 && !grid.IsSolidTerrain(below) && !grid.IsBlockedByEdifice(below))
                ClimbOutOf(ctx, below);
        }

        /// <summary>
        /// Declare the way up out of one open cell, if there is one to declare.
        ///
        /// <para><b>Onto the block, not over the hole.</b> The first version climbed straight up,
        /// from the floor of a pit to the cell directly above it — which is open air with nothing
        /// under it. That was survivable only while a colonist could stand on such a cell and walk
        /// off it, and standing on it is precisely what the owner reported as walking across air.
        /// Forbid that and a pit seals itself: the one cell joining it to the world is a cell
        /// nobody may enter. Ending the climb on top of the block beside the hole fixes both at
        /// once — it is where a person actually ends up, and it is ground.</para>
        ///
        /// <para>The fallback is the old vertical climb, and it is needed: at the bottom of a shaft
        /// two or more cells deep the block beside you is taller than you can reach past, so the
        /// climb goes up the face to the cell above and that one carries on. Those intermediate
        /// cells are the only ones left that are standable without a floor, they are entered and
        /// left by climbing alone, and <c>NavFlags.ClimbOnly</c> is how the rest of the code knows
        /// not to walk into one.</para>
        /// </summary>
        static void ClimbOutOf(PawnContext ctx, int from)
        {
            GridSize size = ctx.Size;
            CellGrid grid = ctx.Cells;

            int above = from + size.LayerStride;
            if (above >= size.CellCount) return;
            if (grid.IsSolidTerrain(above) || grid.IsBlockedByEdifice(above)) return;
            if (!HasWallBeside(grid, from)) return;

            CellRef at = size.FromIndex(from);

            // Onto the top of whichever neighbouring block can be stepped off onto.
            if (TryStepOff(at.X - 1, at.Z)) return;
            if (TryStepOff(at.X + 1, at.Z)) return;
            if (TryStepOff(at.X, at.Z - 1)) return;
            if (TryStepOff(at.X, at.Z + 1)) return;

            // Nothing to step off onto: go up the face and let the cell above carry on.
            ctx.Nav.EnsureClimb(from, above);

            bool TryStepOff(int x, int z)
            {
                if (!size.Contains(x, z, at.Y)) return false;
                if (!grid.IsSolidTerrain(size.Index(x, z, at.Y))) return false;

                // The cell on top of that block. Solid below it is what gives it a floor, so
                // there is no need to ask a second time — only whether it is clear.
                int landing = size.Index(x, z, at.Y + 1);
                if (grid.IsSolidTerrain(landing) || grid.IsBlockedByEdifice(landing)) return false;

                return ctx.Nav.EnsureClimb(from, landing) >= 0;
            }
        }

        /// <summary>
        /// Drop any climb beside this cell that no longer has a face to go up.
        ///
        /// <para>The four cells this cut could have been the wall for, on its own layer. Checking
        /// only at the moment a climb is created leaves every one of them behind as the quarry
        /// widens: a colonist cuts a shaft, the shaft's walls are then mined out around it, and the
        /// climb goes on insisting there is rock to hold on to.</para>
        /// </summary>
        static void RetireClimbsThatLostTheirWall(PawnContext ctx, int cell)
        {
            GridSize size = ctx.Size;
            CellGrid grid = ctx.Cells;
            CellRef at = size.FromIndex(cell);

            Beside(at.X - 1, at.Z);
            Beside(at.X + 1, at.Z);
            Beside(at.X, at.Z - 1);
            Beside(at.X, at.Z + 1);

            void Beside(int x, int z)
            {
                if (!size.Contains(x, z, at.Y)) return;
                int neighbour = size.Index(x, z, at.Y);
                if (HasWallBeside(grid, neighbour)) return;
                if (!ctx.Nav.RemoveClimbAt(neighbour)) return;

                ctx.Nav.MarkDirty(neighbour);

                // The cell may have been standable only because of the climb that was just taken
                // out of it. Whoever was on it has nothing to hold now, so they fall.
                DropAnyoneStandingIn(ctx, neighbour);
                int overIt = neighbour + size.LayerStride;
                if (overIt < size.CellCount) DropAnyoneStandingIn(ctx, overIt);
            }
        }

        /// <summary>
        /// Is there a block beside this cell to climb against?
        ///
        /// <para><b>The owner's rule, and the fault it fixes.</b> "A climb can only happen if there
        /// is a tile in front of you — a height block above and you climb against the edge of that
        /// block." Without it a connector was laid on <em>every</em> cut cell whose ceiling was
        /// open, which in the middle of an open quarry is a colonist going up through clear air
        /// with nothing anywhere near it. Measured over 40,000 ticks before the rule: <b>1,890 of
        /// 9,880</b> climbing pawn-ticks — near enough one in five — had no wall beside them.</para>
        ///
        /// <para>The four faces and not the diagonals. A corner is not something you can get your
        /// weight against, and a colonist climbing the outside of a rock's edge on the diagonal
        /// would be hanging off a line rather than pressed to a face.</para>
        ///
        /// <para>Asked of the <em>lower</em> cell, which is the one whose walls you are inside
        /// while you climb. The block beside it is by definition a whole cell tall, so its top is
        /// the floor of the cell you are climbing out to — which is why the same test also
        /// guarantees there is somewhere to arrive.</para>
        ///
        /// <para>Nobody is stranded by this. A quarry's <em>edge</em> cells always have untouched
        /// ground beside them, so the way out is at the rim; what the rule removes is the middle,
        /// where there was never anything to climb and a colonist could stand on open air.</para>
        /// </summary>
        public static bool HasWallBeside(CellGrid grid, int cell)
        {
            GridSize size = grid.Size;
            CellRef at = size.FromIndex(cell);

            if (at.X > 0 && grid.IsSolidTerrain(size.Index(at.X - 1, at.Z, at.Y))) return true;
            if (at.X < size.SizeX - 1 && grid.IsSolidTerrain(size.Index(at.X + 1, at.Z, at.Y))) return true;
            if (at.Z > 0 && grid.IsSolidTerrain(size.Index(at.X, at.Z - 1, at.Y))) return true;
            if (at.Z < size.SizeZ - 1 && grid.IsSolidTerrain(size.Index(at.X, at.Z + 1, at.Y))) return true;
            return false;
        }

        static void MarkChunksAround(PawnContext ctx, int cell)
        {
            if (ctx.Chunks == null) return;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);

            ctx.Chunks.MarkDirty(at);
            if (at.X > 0) ctx.Chunks.MarkDirty(new CellRef(at.X - 1, at.Z, at.Y));
            if (at.X < size.SizeX - 1) ctx.Chunks.MarkDirty(new CellRef(at.X + 1, at.Z, at.Y));
            if (at.Z > 0) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z - 1, at.Y));
            if (at.Z < size.SizeZ - 1) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z + 1, at.Y));
            if (at.Y > 0) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z, at.Y - 1));
            if (at.Y < size.SizeY - 1) ctx.Chunks.MarkDirty(new CellRef(at.X, at.Z, at.Y + 1));
        }

        /// <summary>
        /// A colonist digging the cell under its own feet steps down into the hole it just made.
        ///
        /// <para>This is not a courtesy, it is the alternative to a colonist standing on nothing.
        /// Cutting a shaft means mining downward, and mining downward means standing on the cell
        /// being cut away — there is no other stance for it. The moment the floor goes, the pawn's
        /// cell has nothing under it, and a pawn in an unstandable cell is a pawn whose next path
        /// starts from a lie.</para>
        ///
        /// <para>It is also how a shaft gets cut at all, and why
        /// <see cref="MineWorkGiver.StandToMine"/> prefers a stance beside the cell: a step down
        /// should be a decision about the shape of the dig, never a surprise.</para>
        /// </summary>
        static void StepDownOntoTheFloorJustCut(PawnContext ctx, int cell)
        {
            int above = cell + ctx.Size.LayerStride;
            if (above < ctx.Size.CellCount) DropAnyoneStandingIn(ctx, above);
        }

        /// <summary>
        /// Anybody standing in this cell falls to the first real floor below it.
        ///
        /// <para><b>The first floor, not one layer.</b> Dropping a colonist exactly one layer is
        /// right only when the cell below is solid, and a shaft is by definition the case where it
        /// is not — so a colonist over a two-deep hole was moved into the middle of it and left
        /// there. The item rule already landed on the first real floor and this is the same rule
        /// for people, asking the same <see cref="CellGrid.FirstFloorAtOrBelow"/>.</para>
        ///
        /// <para><b>Real floor, and a climb is not one.</b> A cell with a climb footprint counts as
        /// standable to navigation — that is what lets a colonist be on a rock face at all — but
        /// there is nothing under it, so it is not somewhere to be left. This matters because a
        /// climb can now be <em>retired</em> when the wall it went up is mined away, and the
        /// colonist that was on it has to go somewhere. Measured without this: two of five
        /// colonists spent the last 12,600 ticks of a 40,000-tick run standing still in mid-air,
        /// which is precisely the fault the owner reported.</para>
        /// </summary>
        static void DropAnyoneStandingIn(PawnContext ctx, int cell)
        {
            int landing = ctx.Cells.FirstFloorAtOrBelow(cell);
            if (landing == cell) return;

            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Cell != cell) continue;
                pawn.Cell = landing;
                pawn.ClearPath();
                pawn.Destination = -1;
            }
        }

        /// <summary>
        /// Anything resting on the cell just cut falls to the first real floor below.
        ///
        /// <para>The item half of <see cref="StepDownOntoTheFloorJustCut"/>, and it had no half at
        /// all before: items have never had a support rule, so a stack simply stayed where it was
        /// spawned however much was dug out from under it.</para>
        ///
        /// <para>It <em>merges</em> where it lands, up to the ordinary stack limit, because
        /// <see cref="ColonyItems.MoveTo"/> merges — which is the owner's decision and the point of
        /// the exercise: two loads that fall into the same hole become one load, and one hauler
        /// trip carries what took two. A load that will not fit goes to the nearest cell that can
        /// take it rather than being lost.</para>
        /// </summary>
        static void DropWhatWasRestingOnIt(PawnContext ctx, int cell)
        {
            int above = cell + ctx.Size.LayerStride;
            if (above >= ctx.Size.CellCount) return;

            ColonyItem? resting = ctx.Items.ItemAt(above);
            if (resting == null || resting.Despawned) return;
            if (ctx.Cells.HasFloor(above)) return;

            int landing = ctx.Cells.FirstFloorAtOrBelow(above);
            if (landing == above) return;

            int room = ctx.Items.NearestCellWithSpace(
                ctx.Cells, landing, resting.DefIndex, resting.Stack, maxRadius: 3);
            if (room >= 0) ctx.Items.MoveTo(resting, room);
        }

        static void SpawnYield(PawnContext ctx, int cell, ushort terrain)
        {
            if (!Yield(ctx, cell, terrain, out int item, out int count)) return;

            // Spoil lands on the floor, not in the hole it came out of. A cell cut over open space
            // has nothing under it, and the first version dropped the stone into it regardless: a
            // quarter of all the spoil on a worked board — 26 stacks of 107, measured — hung in
            // mid-air a layer or two above the ground.
            cell = ctx.Cells.FirstFloorAtOrBelow(cell);

            // The cell just dug, or the nearest that can take the load — which includes a pile of
            // the same stuff from the cell next door with room on it, so a worked seam comes out
            // as a few stacks rather than a scatter of small ones. Nowhere within three cells is a
            // board packed solid with things, which nothing in the game can produce yet; losing
            // the stone then is the least bad answer, because spawning onto a cell that cannot
            // take it would corrupt the cell index.
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, item, count, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(item, at, count);
        }

        /// <summary>
        /// What a cell leaves behind.
        ///
        /// <para>A seam always gives up its metal. Plain rock gives up stone one time in
        /// <see cref="PawnContent.StoneChanceOneIn"/>, and that roll is drawn from the world seed
        /// and the <b>cell index</b> — never the tick, never a live stream. The answer belongs to
        /// the cell, so it is the same whether the cell is mined on the first day or the
        /// hundredth, it survives a save and a reload, and no amount of re-ordering the colony's
        /// work can reroll it. A live draw would make the yield depend on how many other things
        /// happened to roll dice earlier in the same tick, which is a determinism leak that
        /// surfaces as a save-and-resume divergence days later.</para>
        ///
        /// <para>Subsoil, grass, earth and sand leave nothing. They are dug through, not mined.</para>
        /// </summary>
        public static bool Yield(PawnContext ctx, int cell, ushort terrain, out int item, out int count)
        {
            if (terrain == NaturalContent.TerrainIronOre)
            {
                item = ItemIndex.IronOre;
                count = ctx.Content.OrePerCell;
                return true;
            }

            if (terrain == NaturalContent.TerrainCoalSeam)
            {
                item = ItemIndex.Coal;
                count = ctx.Content.OrePerCell;
                return true;
            }

            if (terrain == NaturalContent.TerrainRock)
            {
                var rng = DeterministicRandom.ForTick(ctx.Seed, cell, PawnPurpose.StoneYield);
                if (rng.NextInt(ctx.Content.StoneChanceOneIn) == 0)
                {
                    item = ItemIndex.Stone;
                    count = ctx.Content.StonePerRock;
                    return true;
                }
            }

            item = -1;
            count = 0;
            return false;
        }
    }
}
