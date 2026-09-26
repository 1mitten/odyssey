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
        /// Where a colonist stands to cut this cell out. Four stances, tried in this order:
        /// beside it, on the rim above it, from below it, and only then on top of it — and within
        /// each, square on to a face before round a corner (see <see cref="NearestOfRing"/>).
        ///
        /// <para><b>Beside</b> — one of the eight neighbours on the same layer. This is an adit
        /// into a terrace or an outcrop: the rock is at eye level, the swing is level, and nothing
        /// moves under the colonist when the cell goes. The four faces are taken before the four
        /// corners, because a miner steps in towards what it cuts and on a diagonal that step goes
        /// into the block's corner — and into the cells sharing it.</para>
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
        /// <para><b>From below</b> — one of the nine cells a layer down, the centre included,
        /// reaching up. A pick goes overhead, so a colonist on the ground can cut the rock above
        /// its head or the overhang beside it. Before this existed, a marked cell whose only
        /// approach was from underneath had no stance at all and the order simply waited.</para>
        ///
        /// <para><b>On top</b> — last, because it is the one that costs. It is also unavoidable:
        /// the first cut into flat ground has no rim to stand on, and there is no other stance for
        /// starting a shaft. The floor goes with the cell, and the colonist steps down into the
        /// hole it made (<see cref="Falling.OutOf"/>). Ordering it last means
        /// a step down is a consequence of the shape of the dig rather than a surprise: once one
        /// cell of a shaft is open, every cell beside it is worked from the rim.</para>
        ///
        /// <para>Both of the stances above the rock want the same thing from presentation — a
        /// stroke aimed downward rather than level — and <c>WorkStyle.Dip</c> is where that
        /// lives.</para>
        /// </summary>
        public static int StandToMine(PawnContext ctx, Pawn pawn, int cell) =>
            StandAtFace(ctx, pawn, cell, cutting: true);

        /// <summary>
        /// Where a colonist stands to work at this rock face: <see cref="StandToMine"/>'s four
        /// stances in its order, for any job done at a face.
        ///
        /// <para><paramref name="cutting"/> is whether the work takes the cell out. Only then does
        /// the on-top stance drop the worker into the hole, so only then must that hole be one she
        /// can jump out of (<see cref="DesignationGrid.CanBeLeftAfterCutting"/>). A prospect
        /// (design 62 §7) leaves the face standing, so standing on it strands nobody; the
        /// stances and their order are otherwise one rule for both, written once here.</para>
        /// </summary>
        public static int StandAtFace(PawnContext ctx, Pawn pawn, int cell, bool cutting)
        {
            // The rock itself must be hers to work (design 43 §4c), not only the stance: the
            // stances above and below may be home through the vertical margin when the rock is not.
            if (!ctx.MayWork(pawn, cell)) return -1;
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

            // From underneath, reaching up. A pick goes overhead, so a miner standing on the
            // ground can cut the rock above its head or the overhang beside it — which is how a
            // face is undercut, and the owner's decision after watching marked rock above a
            // colonist simply wait. Without it every cell whose only approach is from below had
            // no stance at all: 33 standing orders, measured over 40,000 ticks.
            if (at.Y > 0)
            {
                int under = NearestStandOnLayer(ctx, pawn, at, at.Y - 1, includeCentre: true);
                if (under >= 0) return under;
            }

            if (!ctx.Cells.IsWalkable(above) || !ctx.Reachable(pawn, above)) return -1;

            // Nobody digs a hole they cannot get out of.
            //
            // This is the ONLY stance that puts the miner in the cell it just cut: the floor goes
            // with the cell and Falling.OutOf drops whoever was on top of it into
            // the hole. Beside, on the rim and from below all leave the colonist standing on
            // something that the cut did not touch, so none of them can strand anybody.
            //
            // A colonist jumps up one block and no more (owner, 2026-09-16), so the hole has to be
            // one it can jump out of. The first cut into flat ground always is — the rim it was
            // standing on is one block up, right beside it. Deepening a one-wide shaft is not, and
            // that is the cut this refuses: it waits for a ladder instead of leaving a miner two
            // below the surface with nothing to climb. A quarry therefore comes out as a flight of
            // benches.
            //
            // **Asked here and not in CanMine.** Marking is the player saying what they want;
            // whether it can be done safely is a fact about the world at the moment somebody goes
            // to do it, and about the stance they would take. Put in CanMine it refused every
            // buried cell — a cell in the middle of rock has no standable neighbour at all — which
            // made the first cut of a tunnel impossible and stopped mining almost dead: measured,
            // 143 of 8,885 marked cells survived the test.
            if (cutting && !DesignationGrid.CanBeLeftAfterCutting(ctx.Cells, cell)) return -1;

            return above;
        }

        /// <summary>
        /// The nearest walkable, reachable cell of the eight around <paramref name="at"/> in XZ,
        /// taken on <paramref name="layer"/> rather than on the cell's own.
        /// </summary>
        static int NearestStandOnLayer(
            PawnContext ctx, Pawn pawn, CellRef at, int layer, bool includeCentre = false)
        {
            // Square on to the face first, and only round a corner when there is no face to stand
            // at. **A diagonal is a worse stance than a further orthogonal one**, so the nearest
            // cell is the wrong thing to ask for until the good cells are exhausted.
            int square = NearestOfRing(ctx, pawn, at, layer, diagonals: false, includeCentre);
            if (square >= 0) return square;

            return NearestOfRing(ctx, pawn, at, layer, diagonals: true, includeCentre);
        }

        /// <summary>
        /// The nearest walkable, reachable cell of one ring around <paramref name="at"/> — the four
        /// faces, or the four corners.
        ///
        /// <para><b>Why the faces are tried first</b> (owner, 2026-09-16: "when mining, the
        /// colonists sometimes try to mine from the side and end up clipping whatever is in the
        /// next tile … they should place themselves in front of the block"). A miner is *drawn*
        /// stepping in towards what it is cutting — <c>WorkStance.StandAt</c> solves the figure's
        /// position so the head lands in the rock — and on a diagonal that step goes towards the
        /// block's <i>corner</i>. The two cells sharing that corner are the rock's own orthogonal
        /// neighbours, which when cutting into a face are exactly the cells most likely to be solid
        /// stone, so the figure ends up standing in them.</para>
        ///
        /// <para>Measured on the played board before this existed: of 1,980 stances taken on the
        /// rock's own layer, <b>1,500 were diagonal</b> and only 480 square on. Of those diagonals
        /// <b>1,330 had something solid in a corner-sharing cell</b> — and <b>961 had an orthogonal
        /// stance available and reachable anyway</b>, lost purely because the ring was ranked by
        /// walking distance and a corner is often one step nearer. Two thirds of the fault was a
        /// preference costing nothing to reverse.</para>
        ///
        /// <para>Nothing becomes unmineable: a diagonal is still taken when no face is available,
        /// which is the remaining third. Those want the drawn figure to stand further back instead,
        /// and that is presentation's to fix — a body clears the corner-sharing cells only from
        /// about 2.4 m out along the diagonal, against 1.65 m square on.</para>
        /// </summary>
        static int NearestOfRing(
            PawnContext ctx, Pawn pawn, CellRef at, int layer, bool diagonals, bool includeCentre)
        {
            GridSize size = ctx.Size;
            int best = -1, bestDistance = int.MaxValue;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                // The cell straight under the rock counts when reaching upward — that is a miner
                // cutting the ceiling over its own head. It never counts on the rock's own layer,
                // where it would be the rock itself. It goes with the faces, being no kind of
                // corner and the most square-on stance there is.
                bool centre = dx == 0 && dz == 0;
                if (centre && !includeCentre) continue;
                if (!centre && (dx != 0 && dz != 0) != diagonals) continue;
                if (centre && diagonals) continue;

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
        public override int WorkType => WorkTypeIndex.Mining;

        /// <summary>
        /// Present, at 1, on a colonist whose Mine job is on soft ground: the activity line's
        /// "Digging" rather than "Mining" (design 62 §4). Sparse — a miner at a rock face and
        /// everybody else publish nothing.
        /// </summary>
        public const string DiggingName = "odyssey.pawn.digging";

        public static readonly AspectKey Digging = AspectKey.Of(DiggingName);

        /// <summary>
        /// Whether the cut in hand was soft ground, taken at the instant it came out, for the
        /// settle that follows: by then the cell is air and cannot say what it was. Neither saved
        /// nor hashed — it chooses a word and nothing reads it but <see cref="IsDigging"/>. A load
        /// in the half-second of a settle reads "Mining" until the job ends, which is the whole
        /// cost of not saving it.
        /// </summary>
        bool _cutSoft;

        public override void Begin(Pawn pawn, Job job)
        {
            base.Begin(pawn, job);
            _cutSoft = false;
        }

        /// <summary>
        /// Is this colonist digging soft ground rather than mining rock? Asked at publish time, so
        /// the pane can say "Digging" without a job def of its own — which would be one more
        /// hashed per-job tally and would move every golden (the argument
        /// <see cref="AnimalShelterThinkNode.IsSheltering"/> makes). Read off the cell being cut:
        /// <see cref="TerrainHandle.IsSoftGround"/> is the one rule the banner and the tile pane
        /// ask too, so the three cannot disagree about one cell.
        /// </summary>
        public static bool IsDigging(Pawn pawn, CellGrid cells)
        {
            if (pawn.CurrentJob == null || pawn.CurrentJob.DefIndex != JobIndex.Mine) return false;
            if (pawn.Driver is not MineJobDriver driver) return false;
            if (driver.ToilIndex == SettleToil) return driver._cutSoft;
            int cell = driver.Job.DestCell;
            return cell >= 0 && cell < cells.Size.CellCount && TerrainHandle.IsSoftGround(cells.Terrain[cell]);
        }

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
        /// <para>Nothing during the settle toil either, which is the point of it: the rock is
        /// already gone and the figure should be easing out of its stance, not still swinging.</para>
        public override int WorkFocus =>
            ToilIndex != 1 ? -1 : Job.DestCell >= 0 ? Job.DestCell : Job.TargetCell;

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
            // Before every guard below: by now the cell is cut and its order cleared, so asking
            // whether it is still marked would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

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

            // The same question felling asks, with mining's own answer to it: a rock is cut from
            // beside it or from one layer up, which is the rim stance and the on-top stance, and
            // never from below. Mining is where the displacement comes FROM — a dig drops whoever
            // stands on the cell it cuts — so the job that causes it must check too.
            if (!StillInReach(ctx, Pawn, cell, layersAbove: 1, layersBelow: 1))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            ushort terrain = ctx.Cells.Terrain[cell];

            // Banked on the cell rather than counted on the job. A miner who breaks off for a
            // meal, a sleep or a mental break used to take the whole morning's work with it, and
            // the next colonist started the face from nothing — hours quietly thrown away, and
            // the reason a half-cut face could never be drawn as half cut.
            //
            // The cell's ledger is in milliwork (see Rates); the price it is read against is in
            // ticks. The pawn pays at its own speed and the cell charges at the standard one.
            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Mining);
            ToilProgress += rate;
            // Priced by the order (WorkFor), which reads a cell nobody has seen into as the rock it
            // is drawn as (design 62 §6); every other cell is its own terrain's price, as it was.
            if (designations.AddWork(cell, rate) < designations.WorkFor(cell) * Rates.Scale)
                return JobStatus.Ongoing;

            designations.Clear(cell);
            _cutSoft = TerrainHandle.IsSoftGround(terrain);
            ctx.Defer(_ => MineCell(ctx, cell, terrain));

            // The rock goes now; the colonist straightens up before walking off.
            NextToil();
            return JobStatus.Ongoing;
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

            // 1a. And if the cut broke into a chamber nobody had seen, all of it (design 62 §6) —
            //     the walls' ore discovered and their chunks marked with it. A cell that was itself
            //     unseen (worked from a diagonal stance, so no face of it was ever open) was air
            //     already; it and its chamber come into view here and it yields nothing below.
            CavernBreach.Open(ctx, cell);

            // 2. The cell and everything touching it must be re-meshed. The neighbours are not an
            //    optimisation to skip: a seam revealed by this dig draws differently now, and the
            //    vertical neighbours are in different chunks entirely, a chunk being one layer.
            MarkChunksAround(ctx, cell);

            // 3. What is walkable changed here and, through the floor rule, in the cell above.
            ctx.Nav.MarkDirty(cell);
            int above = cell + size.LayerStride;
            if (above < size.CellCount) ctx.Nav.MarkDirty(above);
            ctx.Enclosure?.MarkDirty(cell);

            // 3a. Nothing is declared to get out of the hole. A colonist jumps up one block and
            //     no more (owner, 2026-09-16), and DesignationGrid.CanBeLeftAfterCutting refuses
            //     to mark a cell that would leave one it could not jump out of — so a quarry comes
            //     out as benches. Deeper than that wants a ladder, and a ladder is built.

            // 4. Anybody standing on this cell is now standing on nothing, and so is anything
            //    LYING on it: the cell above has just lost its floor, so whatever was there goes
            //    down the hole — which is also how a quarry ends up with its spoil in one place at
            //    the bottom rather than shelved up its sides.
            //
            //    `Falling` is where both rules live now. They were written here and a collapsing
            //    floor asks the same question of the same cell (U29), so the second caller took
            //    them out rather than copying them. No thought is passed: a step down into the
            //    hole you were told to dig is not an event, which is the distinction
            //    `Falling.OutOf` exists to let the two callers disagree about.
            Falling.OutOf(ctx, cell + size.LayerStride);

            // 5. What the cell was made of, if it left anything.
            SpawnYield(ctx, cell, terrain);

            // 6. And what was resting on it. Rock holds up the boundary above it exactly as a wall
            //    does, so cutting it away is a structural edit — the third of the three places that
            //    said "support is deliberately not marked dirty, and U29 should wire all three
            //    together rather than let one of them quietly acquire behaviour the others lack".
            ctx.MarkStructureChanged(cell);
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
        /// <para>What a seam gives up is the ore table's (<c>Ores.xml</c>, design 62 §5c): the item
        /// and the count are read off its row, so a new ore is one row there and nothing here.
        /// Deep stone yields stone exactly as rock does.</para>
        ///
        /// <para>Subsoil, grass, earth and sand leave nothing. They are dug through, not mined.</para>
        /// </summary>
        public static bool Yield(PawnContext ctx, int cell, ushort terrain, out int item, out int count)
        {
            int kind = NaturalContent.OreKindOf(terrain);
            if (kind >= 0 && kind < ctx.Content.OreYields.Length)
            {
                item = ctx.Content.OreYields[kind].Item;
                count = ctx.Content.OreYields[kind].Count;
                return true;
            }

            if (NaturalContent.IsHostRock(terrain))
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
