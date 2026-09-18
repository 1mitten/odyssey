#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Hand a colonist a load of material to carry to a site that is waiting for it.
    ///
    /// <para>The scan walks the construction grid's own list of sites, never the map, so it costs
    /// what the colony has ordered and not what the board holds — the shape <see cref="FellWorkGiver"/>
    /// and <see cref="MineWorkGiver"/> both use, and for the same reason.</para>
    ///
    /// <para><b>Why this is construction work and not hauling.</b> A haul takes a thing to a
    /// stockpile because it is tidier there; this takes a thing to a site because a wall cannot be
    /// built without it. They compete for the same colonists, and if the delivery were a haul it
    /// would sort with the tidying — so a colony would sweep its floors while a half-ordered hut
    /// stood empty. It also means the labour of building a wall trains the builder rather than the
    /// porter, which is the more useful of the two lies available.</para>
    /// </summary>
    public sealed class DeliverWorkGiver : WorkGiver
    {
        public override string Name => "Deliver";

        public override int WorkType => WorkTypeIndex.Construction;

        /// <summary>Before <see cref="BuildWorkGiver"/>: a site cannot be worked until it is fed.</summary>
        public override int IntraPriority => 0;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var sites = ctx.Construction;
            if (sites == null) return false;

            var cells = sites.Sites;
            int bestSite = -1, bestStand = -1, bestDistance = int.MaxValue;
            ColonyItem? bestLoad = null;

            for (int i = 0; i < cells.Count; i++)
            {
                int site = cells[i];
                int outstanding = sites.Outstanding(site);
                if (outstanding <= 0) continue;

                // One deliverer per site. Without it two colonists each fetch a full stack for a
                // wall that wants five, and the second one walks the length of the map to put
                // nothing down. The builder claims the same key later, which is free: a site that
                // is still waiting for material is never offered to the build giver anyway.
                long key = ReservationManager.Key(ReservationTargetKind.Cell, site);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, site);
                if (distance >= bestDistance) continue;

                int wanted = ConstructionContent.StuffAt(sites.StuffAt(site)).item;
                if (wanted < 0) continue;

                ColonyItem? load = NearestLoad(pawn, ctx, wanted);
                if (load == null) continue;

                int stand = BuildWorkGiver.StandToBuild(
                    ctx, pawn, site, ConstructionContent.BuildingAt(sites.At(site)).slab);
                if (stand < 0) continue;

                bestDistance = distance;
                bestSite = site;
                bestStand = stand;
                bestLoad = load;
            }

            if (bestSite < 0 || bestLoad == null) return false;

            job.Reset(JobIndex.Deliver);
            job.TargetItem = bestLoad.Id;
            job.TargetCell = bestLoad.Cell;
            job.DestCell = bestSite;
            // Where to stand to put it down. The site itself is walkable right up until the moment
            // the wall goes up in it, so standing in it would work for a delivery and be exactly
            // wrong for the build that follows; one rule for both is the one worth having.
            job.WorkTicks = bestStand;
            return true;
        }

        /// <summary>
        /// The nearest stack of this material the colonist could actually go and get.
        ///
        /// <para>Loose piles and stored ones alike: a stockpile is where material is kept, so
        /// refusing to take wood out of one would mean a colony that can only build from wood it
        /// has not tidied away yet.</para>
        /// </summary>
        static ColonyItem? NearestLoad(Pawn pawn, PawnContext ctx, int defIndex)
        {
            ColonyItem? best = null;
            int bestDistance = int.MaxValue;

            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                if (item.DefIndex != defIndex) continue;

                // On the floor, which is Cell >= 0 and nothing else. NOT CarriedBy: that field
                // defaults to 0 and 0 is a plausible pawn id, so "CarriedBy >= 0" reads as "in
                // somebody's arms" for every stack in the game and this giver silently found
                // nothing at all. Being carried is expressed by having no cell, which is the test
                // HaulWorkGiver has always used.
                if (item.Cell < 0) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, item.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, item.Cell)) continue;

                bestDistance = distance;
                best = item;
            }

            return best;
        }
    }

    /// <summary>
    /// Hand a colonist a site that has its material and wants work.
    /// </summary>
    public sealed class BuildWorkGiver : WorkGiver
    {
        public override string Name => "Build";

        public override int WorkType => WorkTypeIndex.Construction;

        /// <summary>After <see cref="DeliverWorkGiver"/>.</summary>
        public override int IntraPriority => 1;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            var sites = ctx.Construction;
            if (sites == null) return false;

            var cells = sites.Sites;
            int best = -1, bestStand = -1, bestDistance = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                int site = cells[i];

                // Distance first, because it is arithmetic and everything below it reads the
                // world: a candidate that cannot beat the best so far need not be judged at all.
                // The winner is unchanged either way — a candidate that fails the legality test
                // never moves `bestDistance`, so the order the two are asked in cannot decide it.
                int distance = ctx.Distance(pawn.Cell, site);
                if (distance >= bestDistance) continue;

                if (!CanBuild(pawn, ctx, site, out int stand)) continue;

                bestDistance = distance;
                best = site;
                bestStand = stand;
            }

            if (best < 0) return false;

            Fill(job, best, bestStand);
            return true;
        }

        /// <summary>
        /// Could this colonist legally take a build job on this site <em>right now</em>, and where
        /// would it stand to do it? Nothing is claimed, nothing is started and no field moves.
        ///
        /// <para><b>The split is the point.</b> Until this existed every giver decided legality and
        /// claimed the target in one pass, so the only way to ask "could it" was to do it. A forced
        /// order needs the question asked of one named colonist rather than answered by the scan,
        /// and the A10 command grid needs it asked for a menu that is built <em>before</em> the
        /// player chooses anything (<c>docs/design/15-building.md</c>, "Forced orders and the
        /// context menu"). A query that reserved would grey out a command by claiming the thing the
        /// command is about.</para>
        ///
        /// <para>It is the scan's own test, not a second opinion beside it:
        /// <see cref="TryGiveJob"/> calls this for every candidate, so the forced path and the
        /// offered path cannot come to disagree about what a colonist may build. <c>WillWork</c> is
        /// in it because the forced path has no think tree above it to have asked already — for the
        /// scan it is always true by the time <c>WorkThinkNode</c> has called.</para>
        ///
        /// <para>What it does <em>not</em> answer is why not. A greyed command wants a reason, and
        /// the reason wants a vocabulary that does not exist yet — "nowhere to stand" and "somebody
        /// else has it" are not <see cref="IntentRejection"/> values. That belongs with A10, which
        /// is the first thing that can display one.</para>
        /// </summary>
        public static bool CanBuild(Pawn pawn, PawnContext ctx, int site, out int stand)
        {
            stand = -1;

            var sites = ctx.Construction;
            if (sites == null) return false;
            if ((uint)site >= (uint)ctx.Size.CellCount) return false;

            // Asleep or in a mental break: the break handler would take a forced job straight back
            // off the colonist on the next tick, so offering it is worse than refusing it.
            if (!pawn.WillWork()) return false;

            // A site still waiting for its wood is the deliverer's, not the builder's. It is also
            // what the driver's own first guard asks, so a job forced onto a blueprint would fail
            // on the tick it started.
            if (!sites.IsFrame(site)) return false;

            // A thing a colonist is not skilled enough to make is not offered to it, rather
            // than offered and botched: the reference gates a few defs on a construction level
            // and a wall is not one of them, so this costs nothing today and is the hook the
            // first gated thing needs.
            if (pawn.SkillLevel(SkillIndex.Construction) <
                ConstructionContent.BuildingAt(sites.At(site)).minSkill) return false;

            long key = ReservationManager.Key(ReservationTargetKind.Cell, site);
            if (!ctx.Reservations.CanReserve(pawn.Id, key)) return false;

            // Last, because it is the dearest: eight neighbours, each asked whether this pawn could
            // get to it. It is also the reachability test — a stance nobody can walk to is not a
            // stance — so a site in a sealed pocket answers no here and nowhere earlier.
            stand = StandToBuild(ctx, pawn, site, ConstructionContent.BuildingAt(sites.At(site)).slab);
            return stand >= 0;
        }

        /// <summary>
        /// Where a colonist stands to build this, or -1.
        ///
        /// <para><b>A wall and a floor are worked from different places, and the difference is not
        /// cosmetic — it is whether the job can be done at all.</b> A wall is built from beside it
        /// on the same floor, which is what <c>FellJobDriver.StandBeside</c> answers. A slab has no
        /// neighbours on its own layer to stand on until there is already a floor up there, so the
        /// first slab of a storey would have no stance and would simply never be offered to
        /// anybody: the order would sit there for ever and the feature would look broken.</para>
        ///
        /// <para>So a slab is reached from <b>below</b> as well: beside-and-one-down, then directly
        /// underneath. This is mining's envelope rather than felling's, and for the same reason
        /// mining has one — a pick goes overhead and so do a plank and a hammer. Extending an
        /// existing floor still prefers the stance on that floor, because <c>StandBeside</c> is
        /// asked first and answers whenever there is anything up there to stand on.</para>
        /// </summary>
        public static int StandToBuild(PawnContext ctx, Pawn pawn, int site, bool slab)
        {
            int beside = FellJobDriver.StandBeside(ctx, pawn, site);
            if (beside >= 0 || !slab) return beside;

            int below = site - ctx.Size.LayerStride;
            if (below < 0) return -1;

            int besideBelow = FellJobDriver.StandBeside(ctx, pawn, below);
            if (besideBelow >= 0) return besideBelow;

            // Directly under it, last: a colonist that floors over its own head is walled in only
            // if it has also walled itself in, and refusing the stance would refuse the first
            // ceiling of every room built from the inside.
            return ctx.Cells.IsWalkable(below) && ctx.Reachable(pawn, below) ? below : -1;
        }

        /// <summary>
        /// Write a build job for a site and a stance. One place, so the scan and a forced order
        /// produce the same job rather than two jobs that happen to look alike.
        /// </summary>
        public static void Fill(Job job, int site, int stand)
        {
            job.Reset(JobIndex.Build);
            job.TargetCell = stand;
            job.DestCell = site;
        }
    }

    /// <summary>
    /// Fetch a load of material and put it into a site.
    ///
    /// <para>Toils: walk to the material, take it up, carry it to the site, put it in. The same
    /// four a haul has, because it is the same journey — what differs is only where the load ends
    /// up, and that it goes into a tally rather than onto the floor.</para>
    /// </summary>
    public class DeliverJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            var sites = ctx.Construction;
            if (sites == null) return false;

            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || item.Cell < 0) return false;
            if (Job.DestCell < 0 || sites.Outstanding(Job.DestCell) <= 0) return false;

            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            long siteKey = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);

            if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
            Pawn.HeldReservations.Add(itemKey);

            if (!ctx.Reservations.Reserve(Pawn.Id, siteKey)) return false;
            Pawn.HeldReservations.Add(siteKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var sites = ctx.Construction;
            if (sites == null) return JobStatus.Failed;

            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            // The site may have been cancelled, replaced with a different material, or finished by
            // somebody else while this colonist walked. Checked every tick rather than on arrival,
            // so a cancelled order stops a colonist crossing the map for it.
            if (sites.At(Job.DestCell) == BuildingHandle.None) return JobStatus.Failed;
            if (ConstructionContent.StuffAt(sites.StuffAt(Job.DestCell)).item != item.DefIndex)
                return JobStatus.Failed;

            switch (ToilIndex)
            {
                case 0:
                {
                    if (item.Cell != Job.TargetCell) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                    return LiftToil(ctx, item);

                case 2:
                {
                    // The carry is the work, exactly as it is for a haul: the walk to fetch it is
                    // not counted, and putting it in is one tick and is not counted either.
                    Work(ctx);
                    JobStatus walk = GotoCell(ctx, Job.WorkTicks);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    int wanted = sites.Outstanding(Job.DestCell);
                    if (wanted <= 0) return JobStatus.Failed;

                    int given = item.Stack < wanted ? item.Stack : wanted;
                    sites.Deliver(Job.DestCell, given);
                    item.Stack -= given;

                    // The stoop is the same motion whether the load goes on the floor or into a
                    // frame, so it is reported here rather than only by PutDown.
                    Pawn.BeginGesture(PawnGesture.Stow);

                    if (item.Stack <= 0)
                    {
                        ctx.Items.Despawn(item);
                        Job.CarriedItem = -1;
                    }

                    // Anything left over is still in the colonist's arms; Cleanup puts it down.
                    // A site that wants five and a stack of seventy-five is the common case, and
                    // the alternative — splitting the stack where it lay — cannot be done while a
                    // cell holds exactly one item record.
                    return JobStatus.Succeeded;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }

    /// <summary>
    /// Work at a site until the thing stands.
    ///
    /// <para>Priced per thing and per material by <see cref="ConstructionContent.WorkFor"/>, read
    /// as the colonist swings rather than taken from the job def — so a stone wall is 1.7 times the
    /// work of a wooden one. One number on the def would make every wall cost the same, which is
    /// the difference between materials; it is the argument mining's def already makes.</para>
    ///
    /// <para>The work is banked on the cell, so a builder who breaks off for a meal, a sleep or a
    /// mental break does not throw the morning away and the next colonist picks the wall up where
    /// it was left. The world edit — the wall appearing — is a structural event and runs in the
    /// deferred phase of the same tick, like every collapse and removal.</para>
    /// </summary>
    public class BuildJobDriver : JobDriver
    {
        /// <summary>
        /// The site, once the walk is over and the work has started, so presentation puts a hammer
        /// in the hands and turns the figure to face what it is building. -1 during the walk and
        /// during the settle, exactly as felling and mining report theirs.
        /// </summary>
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
            // Before every guard below: by now the wall is up and the site cleared, so asking
            // whether there is still a site here would fail the job on the first settle tick.
            if (ToilIndex == SettleToil) return Settle(ctx);

            var sites = ctx.Construction;
            if (sites == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // Somebody else built it, or the player changed their mind: stop, do not swing at air.
            if (!sites.IsFrame(cell)) return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // A builder stands beside what it builds and on the same floor — felling's envelope —
            // for a wall, because there is no rim to work one from and no undercut. A slab is
            // reached from below as well, which is mining's envelope and the same argument: a
            // plank goes overhead exactly as a pick does. See StandToBuild, which chooses the
            // stance this has to agree with; a stance the work toil then rejects is a colonist
            // that walks to a wall and turns round again.
            bool slab = ConstructionContent.BuildingAt(sites.At(cell)).slab;
            if (!StillInReach(ctx, Pawn, cell, layersAbove: 0, layersBelow: slab ? 1 : 0))
            {
                WalkBack();
                return JobStatus.Ongoing;
            }

            int rate = Pawn.WorkRatePerMille(WorkTypeIndex.Construction);
            ToilProgress += rate;
            Work(ctx);
            if (sites.AddWork(cell, rate) < sites.WorkFor(cell) * Rates.Scale) return JobStatus.Ongoing;

            // The last hammer blow is rolled twice, and the two rolls are one question asked in
            // two halves: **does the thing stand at all**, and then **how well was it made**. Both
            // read the finishing colonist's Construction level — the reference's own exploitable
            // property, kept deliberately at both ends, because a building records no author and a
            // master rescuing a novice's half-built wall reads as a sensible thing for a colony to
            // do. They arrived from two directions (the success roll is U26's own last line; the
            // quality tier came with the bed, design 20 §6) and neither subsumes the other.
            //
            // **Success is asked first, and a botch never reaches the quality roll**, because a
            // thing that was not built has no quality to have. The two draw from separate salts,
            // so asking one does not move the other's answer.
            int tick = ctx.CurrentTick;
            int chance = ctx.Content.WorkTypes[WorkTypeIndex.Construction]
                .SuccessPerMille(Pawn.SkillLevel(SkillIndex.Construction));
            var roll = DeterministicRandom.ForTick(ctx.Seed, cell ^ tick, PawnPurpose.BuildSuccess);

            if (roll.NextInt(1_000) < chance)
            {
                ConstructionGrid grid = sites;

                // Walls take no quality and roll nothing; a bed does.
                byte quality = 0;
                if (ConstructionContent.BuildingAt(sites.At(cell)).takesQuality)
                {
                    var rng = DeterministicRandom.ForTick(
                        ctx.Seed, cell ^ tick, PawnPurpose.BuildQuality);
                    quality = QualityContent.Roll(Pawn.SkillLevel(SkillIndex.Construction), rng);
                }

                ctx.Defer(_ => grid.Raise(ctx, cell, quality));

                // The wall goes up now; the builder straightens up before walking off.
                NextToil();
                return JobStatus.Ongoing;
            }

            // Botched. The work is lost, some of the material with it, and the job ends as a
            // failure so the claim comes off and the scan can offer the site again — delivery
            // first, because what is left is a blueprint once more.
            sites.Botch(cell, KeptAfterABotch(ctx, sites, cell, tick));
            return JobStatus.Failed;
        }

        /// <summary>
        /// What a botched site keeps of its delivery: half, with the odd unit by a seeded coin
        /// flip — the deconstruct refund's arithmetic pointed the other way, and for the same
        /// reason both ends of it want the flip rather than a rounding rule. The reference wastes
        /// only "some" of a botched build's materials and its own fraction is not written down
        /// anywhere we could read (a-04, "Could not be determined"), so half is Odyssey's number:
        /// a botch costs what a demolition refunds, which is at least an argument rather than a
        /// guess.
        /// </summary>
        static int KeptAfterABotch(PawnContext ctx, ConstructionGrid sites, int cell, int tick)
        {
            int delivered = sites.Delivered(cell);
            int half = delivered / 2;
            if (delivered % 2 == 0) return half;

            var rng = DeterministicRandom.ForTick(ctx.Seed, cell ^ tick, PawnPurpose.BuildBotchLoss);
            return half + (rng.NextInt(2) == 0 ? 0 : 1);
        }
    }
}
