#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Every colonist, in id order.
    ///
    /// The registry is where the pawn simulation meets the three things outside it: the state
    /// hash, the snapshot the interface reads, and the save file. It is registered as an
    /// <see cref="ITickable"/> in the <see cref="TickGroup.Never"/> group purely so that it folds
    /// into <see cref="SimWorld.ComputeStateHash"/> — it has no tick of its own, because the work
    /// is done by the three pawn systems.
    ///
    /// Iteration is always over the list, in ascending id. There is no dictionary walk anywhere
    /// in a tick; the by-id map is probed and never enumerated.
    /// </summary>
    public sealed class PawnRegistry : ITickable, IStateHashable, ISnapshotContributor, ISaveable
    {
        readonly List<Pawn> _pawns = new List<Pawn>();
        readonly Dictionary<int, int> _byId = new Dictionary<int, int>();
        readonly PawnContext _ctx;
        int _nextId = 1;

        internal PawnRegistry(PawnContext ctx) { _ctx = ctx; }

        /// <summary>Ascending by id. The one true iteration order for anything pawn-shaped.</summary>
        public IReadOnlyList<Pawn> All => _pawns;

        public int Count => _pawns.Count;

        public Pawn? Get(PawnId id) => _byId.TryGetValue(id.Value, out int index) ? _pawns[index] : null;

        /// <summary>
        /// True if any pawn is currently stationary in this cell (no active path).
        /// Used by the pathfinder to apply soft crowd avoidance bias.
        /// </summary>
        public bool IsCellOccupiedByStandingPawn(int cell)
        {
            for (int i = 0; i < _pawns.Count; i++)
            {
                var p = _pawns[i];
                // A carried patient is in her carrier's arms, not standing (design 33 §11f).
                if (p.Cell == cell && !p.HasPath && p.CarriedBy == 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Build a colonist at a cell. This is the whole public API for making a pawn: one call,
        /// no partially-initialised intermediate state, and the driver pool built up front so no
        /// job start ever allocates.
        /// </summary>
        public Pawn Spawn(int cell) => Spawn(cell, kind: 0);

        /// <summary>How many kinds this build has: the bound a saved kind is checked against.</summary>
        public int KindCount => _ctx.Content.Kinds.Length == 0 ? 1 : _ctx.Content.Kinds.Length;

        /// <summary>
        /// Build a pawn of a kind at a cell (design 29 §1). Kind 0 is the colonist and is what
        /// <see cref="Spawn(int)"/> makes; anything else is an animal, and the caller has checked
        /// the kind against <see cref="KindCount"/>.
        /// </summary>
        public Pawn Spawn(int cell, int kind)
        {
            var pawn = new Pawn(new PawnId(_nextId++), cell, _ctx.Content, kind);
            // The world's seed unless a caller says otherwise (U40). This is what keeps every
            // colony nobody chose rolling exactly what it rolled before pawns had seeds of their
            // own, so no scenario, headless run or fixture had to change.
            pawn.RollSeed = _ctx.Seed;
            Adopt(pawn);
            // What the kind arrives holding (design 33 §1: the marauder is armed). After the
            // adoption, so the rules see a pawn the registry knows; a kind naming no weapon is one
            // comparison. The loader never comes here — it restores the hand from the save.
            if (_ctx.Content.WeaponOf(kind) >= 0) _ctx.WeaponRules.ArmOnSpawn(pawn, _ctx);
            return pawn;
        }

        /// <summary>
        /// Debug menu: <c>IntentKind.SpawnPawn</c>. The intent names a <em>column</em>: the
        /// colonist arrives at the walkable cell nearest the layer asked for, and the command is
        /// refused only when the whole column has nowhere to stand — never a colonist nobody can
        /// reach or path out of.
        ///
        /// <para><b>The layer is the caller's guess, not its instruction.</b> The debug menu says
        /// "near the camera", and the camera's own layer over open ground is the air above the
        /// terrain, so a rule that took the layer literally refused every spawn the menu sent —
        /// and said <c>OutOfBounds</c> while doing it, for a cell that was plainly in bounds. See
        /// <see cref="Odyssey.Sim.World.CellGrid.NearestWalkableInColumn"/>, which is the one
        /// owner of that fall.</para>
        ///
        /// <para>Passions are rolled here off <see cref="Spawn"/>'s own <c>RollSeed</c> (the world's,
        /// since nothing here asks for one of its own — U40), exactly as
        /// <see cref="ColonyScenario.Place"/> rolls them for a starting colonist — a debug-spawned
        /// pawn is otherwise indistinguishable from one dealt at tick zero. Skill levels are not:
        /// <see cref="StartingSkillsSystem"/> rolls them for any pawn still at the constructor's
        /// zero, on the very next tick, which this pawn is.</para>
        /// </summary>
        /// <summary>
        /// The most pawns a world will hold, and a hard ceiling in the same sense
        /// <c>PawnFigureDirector.FigureCeiling</c> is one: **moving it is a measurement, not an
        /// edit**, and <c>PawnCeilingTests</c> fails on anything that raises it.
        ///
        /// <para><b>Why it exists</b> (owner, 2026-09-23). Spawning colonists from the debug menu
        /// past a certain number produced colonists "in an orange suit", textures that "kept
        /// switching", and a session that "got buggy". Nothing in the game stopped that: the spawn
        /// intent refused an unreachable column and nothing else, so the menu could add people
        /// until something gave way.</para>
        ///
        /// <para><b>The orange suit was not a pawn count at all</b>, and was found the day after
        /// this was written. The number was the 64-figure cap: past it, colonists are drawn in the
        /// baked far form, and that form wore the pack's own paint on the uniform, which is burnt
        /// orange. Fixed in presentation (<c>docs/design/29-modular-colonists.md</c> §13a). This
        /// ceiling is kept as the rail it always said it was.</para>
        ///
        /// <para><b>It is a rail, not a fix, and it is deliberately far above any real colony.</b>
        /// The audit's scale target is fifty; the figure ceiling is sixty-four; and a barren board
        /// was measured healthy at <b>384</b> colonists on 2026-09-23 — 3.90 ms a frame, no
        /// stand-ins drawn, 23 materials, two faces. So this number is not where things were found
        /// to break. It is four times the scale target, and its whole job is that a debug command
        /// cannot run a session into a state nobody designed for.</para>
        ///
        /// <para><b>A refusal, never a clamp.</b> The same rule the rest of this class follows: a
        /// caller asking for one more than the world holds has misunderstood something, and
        /// silently declining to spawn while reporting success is how a debug menu comes to lie.
        /// Only the <i>intent</i> path is bounded — <see cref="Spawn(int, int)"/> itself is what
        /// worldgen and the scenario call, and a starting colony is never anywhere near this.</para>
        /// </summary>
        public const int PawnCeiling = 200;

        public IntentRejection HandleSpawnPawn(Intent intent)
        {
            // A is the kind (design 29 §7): 0 is the colonist this intent always made, so nothing
            // that sends it today changed; a kind this build does not have is refused, not clamped.
            int kind = intent.A;
            if (kind < 0 || kind >= KindCount) return IntentRejection.NotPermitted;

            // The ceiling. Refused rather than clamped, and refused before anything is built, so a
            // caller that has asked for one too many is told so rather than quietly ignored.
            if (Count >= PawnCeiling) return IntentRejection.NotPermitted;

            CellRef cell = intent.Cell;
            if (!_ctx.Size.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            int index = _ctx.Cells.NearestWalkableInColumn(cell.X, cell.Z, cell.Y);
            if (index < 0) return IntentRejection.NotPermitted;
            index = FreeSpawnCell(index);
            Pawn pawn = Spawn(index, kind);
            // An animal has no skills to be passionate about (design 29 §2).
            if (pawn.IsPerson) pawn.RollPassions();
            return IntentRejection.None;
        }

        /// <summary>
        /// <c>DebugArmColonists</c> (design 33 §9i; owner, 2026-09-24: "an option to wield every colonist
        /// with a random melee weapon ... for testing"). Every colonist who is standing and holds
        /// nothing is dealt one of the content's melee weapons — a roll on her own stream, so a seed
        /// deals the same arms every time — made on the nearest cell that can take it and taken
        /// straight up (<see cref="WeaponHand.TakeUp"/>), exactly as <see cref="IWeaponRules.ArmOnSpawn"/>
        /// arms a marauder. A colonist already holding a weapon keeps it; a downed one is skipped.
        /// <c>AlreadyInThatState</c> when there was nobody to arm.
        /// </summary>
        public IntentRejection HandleDebugArmColonists(Intent intent)
        {
            var weapons = new List<int>();
            ItemDef[] items = _ctx.Content.Items;
            for (int i = 0; i < items.Length; i++) if (items[i].weapon != null) weapons.Add(i);
            if (weapons.Count == 0) return IntentRejection.NotPermitted;

            int tick = _ctx.World?.CurrentTick ?? _ctx.CurrentTick;
            int armed = 0;
            for (int i = 0; i < _pawns.Count; i++)
            {
                Pawn pawn = _pawns[i];
                if (!pawn.IsColonist || pawn.Downed || pawn.EquippedItem != 0) continue;

                var rng = DeterministicRandom.ForTick(_ctx.Seed, tick, PawnPurpose.DebugArm ^ (uint)pawn.Id.Value);
                int def = weapons[rng.NextInt(weapons.Count)];
                int cell = _ctx.Items.NearestCellWithSpace(_ctx.Cells, pawn.Cell, def, 1, JobDriver.DropSearchRadius);
                if (cell < 0) continue;

                ThingId id = _ctx.Items.Spawn(def, cell);
                WeaponHand.TakeUp(pawn, _ctx.Items.Get(id)!, _ctx);
                armed++;
            }
            return armed > 0 ? IntentRejection.None : IntentRejection.AlreadyInThatState;
        }

        /// <summary>How far round the spawn point a debug spawn looks for a free tile, in rings.</summary>
        public const int SpawnSpreadRings = 4;

        /// <summary>
        /// The spawn cell if nobody stands on it, else the nearest free tile round it (owner,
        /// 2026-09-24: marauders spawned in quick succession must not pile on one tile; design 33
        /// §9h). Rings outward from the spawn point, in a fixed scan order so the answer is a
        /// function of the world; each column is tried on the spawn layer, then one up, then one
        /// down — the same lift a move order uses — and a tile must be standable, unoccupied and
        /// reachable from the spawn point, so a pawn never arrives walled into a pocket. Falls back
        /// to the spawn cell itself if every ring is full. Every kind, not only marauders: a
        /// shared tile is the same fault whoever stands on it. A debug command, so it costs a scan
        /// of the pawns per candidate and nothing per tick.
        /// </summary>
        int FreeSpawnCell(int anchor)
        {
            if (!Occupied(anchor)) return anchor;

            GridSize size = _ctx.Size;
            CellRef at = size.FromIndex(anchor);
            for (int ring = 1; ring <= SpawnSpreadRings; ring++)
            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (System.Math.Abs(dx) != ring && System.Math.Abs(dz) != ring) continue;
                int x = at.X + dx, z = at.Z + dz;
                for (int dy = 0; dy <= 2; dy++)
                {
                    int y = at.Y + (dy == 0 ? 0 : dy == 1 ? 1 : -1);
                    if (!size.Contains(x, z, y)) continue;
                    int c = size.Index(x, z, y);
                    if (!_ctx.Cells.IsWalkable(c) || Occupied(c)) continue;
                    if (!_ctx.Nav.Reachable(anchor, c, TraverseMode.Colonist)) continue;
                    return c;
                }
            }
            return anchor;
        }

        bool Occupied(int cell)
        {
            for (int i = 0; i < _pawns.Count; i++) if (_pawns[i].Cell == cell) return true;
            return false;
        }

        /// <summary>
        /// <c>SetWorkPriority(A = pawn, B = work handle, C = priority)</c> — the Work tab's one
        /// command.
        ///
        /// <para><b>Every argument is checked and none is clamped.</b> A priority of 9 is not a
        /// player asking for something unusual, it is a caller that has misunderstood the range,
        /// and quietly storing 4 would put a number in the <i>hash</i> that nobody asked for. The
        /// same goes for the work handle: out of range is a refusal, not a modulus.</para>
        /// </summary>
        public IntentRejection HandleSetWorkPriority(Intent intent)
        {
            Pawn? pawn = Get(new PawnId(intent.A));
            if (pawn == null) return IntentRejection.NotPermitted;
            if (intent.B < 0 || intent.B >= WorkTypeIndex.Count) return IntentRejection.NotPermitted;
            if (intent.C < 0 || intent.C > 4) return IntentRejection.NotPermitted;

            if (pawn.WorkPriorities[intent.B] == (byte)intent.C)
                return IntentRejection.AlreadyInThatState;

            pawn.WorkPriorities[intent.B] = (byte)intent.C;
            return IntentRejection.None;
        }

        /// <summary>
        /// <c>SetScheduleBlock(A = pawn, B = hour, C = block)</c> — the Work tab's other command.
        ///
        /// <para>Checked and never clamped, for the reason <see cref="HandleSetWorkPriority"/>
        /// gives: an hour of 25 is a caller that has misunderstood the day, not a player asking
        /// for something unusual.</para>
        /// </summary>
        public IntentRejection HandleSetScheduleBlock(Intent intent)
        {
            Pawn? pawn = Get(new PawnId(intent.A));
            if (pawn == null) return IntentRejection.NotPermitted;
            if (intent.B < 0 || intent.B >= ScheduleHandle.Hours) return IntentRejection.NotPermitted;
            if (intent.C < 0 || intent.C >= ScheduleHandle.Count) return IntentRejection.NotPermitted;

            if (pawn.ScheduleHours[intent.B] == (byte)intent.C)
                return IntentRejection.AlreadyInThatState;

            pawn.ScheduleHours[intent.B] = (byte)intent.C;
            return IntentRejection.None;
        }

        /// <summary>
        /// Take a pawn off the board (design 30 §3). The first thing that ever removes one: there
        /// is no health model and no death, so until wildlife could walk off the edge nothing
        /// left the registry. Reservations are released; the job is the caller's to have ended,
        /// because the registry does not know the job system. Iteration order — ascending id — is
        /// kept by removing in place and renumbering the index after it, which is O(n) on an
        /// event that happens a few times a day.
        ///
        /// <para><b>And any bed the pawn owned goes back to nobody</b> (design 33 §5c). Until death
        /// only animals were ever despawned and they own no beds, so nothing needed it; a dead
        /// colonist would otherwise keep her bed for ever under an id that no longer exists. Here
        /// rather than in the fight's death so that every way off the board — death, a leaving
        /// animal, whatever comes next — releases the same things. One scan of the edifice list,
        /// on an event that happens a few times a day.</para>
        /// </summary>
        public void Despawn(Pawn pawn)
        {
            if (pawn == null) throw new System.ArgumentNullException(nameof(pawn));
            if (!_byId.TryGetValue(pawn.Id.Value, out int index) || !ReferenceEquals(_pawns[index], pawn)) return;
            // Nobody fights a pawn that is gone (design 33 §9e): every attack on it ends now, on the
            // tick it dies or leaves the board, rather than on each attacker's next tick.
            _ctx.Combat?.EndAttacksOn(pawn);
            _ctx.Reservations.ReleaseAll(pawn);
            _ctx.Construction?.ReleaseBedsOf(pawn.Id.Value);
            // A weapon in the hand goes down where the pawn stood, or it would stay carried by an
            // id that no longer exists, with no cell, for ever (design 33 §6D). A death has already
            // let go of it through the drop listener, so this is a no-op there; it is for every
            // other way off the board — a marauder that flees off the edge (integration, 2026-09-23).
            if (pawn.EquippedItem != 0) WeaponHand.PutDown(pawn, _ctx, pawn.Cell);
            _pawns.RemoveAt(index);
            _byId.Remove(pawn.Id.Value);
            for (int i = index; i < _pawns.Count; i++) _byId[_pawns[i].Id.Value] = i;
        }

        /// <summary>Register a pawn subclass. The seam a mod would use to add a pawn kind.</summary>
        public Pawn Adopt(Pawn pawn)
        {
            pawn.DriverPool = BuildDrivers();
            _byId[pawn.Id.Value] = _pawns.Count;
            _pawns.Add(pawn);
            if (pawn.Id.Value >= _nextId) _nextId = pawn.Id.Value + 1;
            return pawn;
        }

        /// <summary>One instance per job kind, indexed by the driver number a JobDef names.</summary>
        static JobDriver[] BuildDrivers() => new JobDriver[]
        {
            new HaulJobDriver(),
            new EatJobDriver(),
            new SleepJobDriver(),
            new WanderJobDriver(),
            new WaitJobDriver(),
            new FellJobDriver(),
            new MineJobDriver(),
            new DeliverJobDriver(),
            new BuildJobDriver(),
            new DeconstructJobDriver(),
            new SowJobDriver(),
            new HarvestJobDriver(),
            new DraftHoldJobDriver(),
            new GotoJobDriver(),
            new LayConduitJobDriver(),
            new RemoveConduitJobDriver(),
            new RefuelJobDriver(),
            // The combat line (design 33 §5), in JobHandle order: 17 to 21, after power's three.
            new AttackMeleeJobDriver(),
            new FleeJobDriver(),
            new DownedJobDriver(),
            new EquipJobDriver(),
            new RescueJobDriver(),
        };

        // ---- ITickable: registration only, so the hash sees the pawns --------------------

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_pawns.Count);
            for (int i = 0; i < _pawns.Count; i++) _pawns[i].ContributeTo(ref hash);
            _ctx.Items.ContributeTo(ref hash);
        }

        // ---- the snapshot seam ------------------------------------------------------------

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            GridSize size = world.Size;
            for (int i = 0; i < _pawns.Count; i++)
            {
                var pawn = _pawns[i];
                // Where the pawn is stepping to, and how far along, so presentation can glide it
                // between cells instead of snapping.
                //
                // **A fraction of THIS step, not a count of cost units.** It used to be the raw
                // progress clamped to 100, which is exact for a flat cell — one costs 100 units —
                // and wrong for every dearer step there is. A ladder down costs 400: the figure
                // reached the bottom a quarter of the way through and then stood frozen in the
                // shaft for the remaining three quarters, which at six and a half seconds a rung
                // is most of what reads as a colonist hanging in mid-air. Dividing by the step's
                // own cost makes the glide take exactly as long as the step does, whatever it is.
                var cell = size.FromIndex(pawn.Cell);
                var nextCell = cell;
                int movePercent = 0, movePerMille = 0, moveDeltaPerMille = 0;
                if (pawn.HasPath)
                {
                    nextCell = size.FromIndex(pawn.Path[pawn.PathIndex]);
                    // Progress and step cost count thousandths together (Rates), so the ratio is
                    // the one this publish has always given. The fallback scales with them.
                    int cost = pawn.MoveStepCost > 0 ? pawn.MoveStepCost
                        : MoveCost.Orthogonal * Rates.Scale;
                    movePercent = (int)((long)pawn.MoveProgress * 100 / cost);
                    if (movePercent < 0) movePercent = 0;
                    else if (movePercent > 100) movePercent = 100;

                    // The same ratio at ten times the resolution. A percent is too coarse to draw
                    // with on any step that does not cost 100: the figure then advances a whole
                    // point every few ticks and stands still in between, which is 25 mm on the
                    // flat and 134 mm up a terrace once the climb is drawn in strides. See
                    // PawnView.MovePerMille, where that measurement is recorded.
                    movePerMille = (int)((long)pawn.MoveProgress * 1000 / cost);
                    if (movePerMille < 0) movePerMille = 0;
                    else if (movePerMille > 1000) movePerMille = 1000;

                    // And how much of the step one tick retires, which is the only number a frame
                    // between two ticks can honestly carry the figure on by. It belongs here and
                    // nowhere else: the rate is this colonist's own (pace and condition) and the
                    // cost is this step's own (the terrain being entered is priced into it), so
                    // presentation cannot recover it from the two cells. See
                    // PawnView.MoveDeltaPerMille for what inferring it cost.
                    moveDeltaPerMille = (int)((long)pawn.MoveRatePerMille() * 1000 / cost);
                    if (moveDeltaPerMille < 0) moveDeltaPerMille = 0;
                    else if (moveDeltaPerMille > 1000) moveDeltaPerMille = 1000;
                }

                // What the pawn is working on, if anything. Asked of the driver rather than
                // derived from the job: only the driver knows whether the walk toil is over,
                // and a figure that swings an axe while walking is worse than one that glides.
                int workFocus = pawn.Driver != null ? pawn.Driver.WorkFocus : -1;

                // Sitting by a fire, and which fire: the figure faces it (design 31 §18d). The
                // cell rides in WorkCell, which is otherwise the pawn's own cell when idle.
                bool seated = pawn.CurrentJob != null && pawn.CurrentJob.Seated;
                CellRef faces = workFocus >= 0 ? size.FromIndex(workFocus)
                    : seated ? size.FromIndex(pawn.CurrentJob!.DestCell)
                    : cell;

                // What the pawn is and what state it is in (design 33 §5): the byte that replaced
                // every "kind is not 0, so an animal" in the interface. A report, derived here
                // from state hashed where it lives.
                PawnFlags flags = PawnFlags.None;
                if (pawn.IsPerson) flags |= PawnFlags.Person;
                if (pawn.IsHostile) flags |= PawnFlags.Hostile;
                if (pawn.Drafted) flags |= PawnFlags.Drafted;
                if (pawn.Downed) flags |= PawnFlags.Downed;
                if (pawn.StunnedAt(world.CurrentTick)) flags |= PawnFlags.Stunned;
                if (pawn.KnockedDownAt(world.CurrentTick)) flags |= PawnFlags.KnockedDown;
                if (pawn.CarriedBy != 0) flags |= PawnFlags.Carried;
                // Drawn or sheathed (design 33 §8b): a report derived here from saved state, so
                // it is neither saved nor hashed and no golden can move with it.
                if (WeaponDraw.IsDrawn(_ctx, pawn, world.CurrentTick))
                    flags |= PawnFlags.Drawn;

                writer.AddPawn(new PawnView(
                    pawn.Id,
                    cell,
                    pawn.Needs[NeedIndex.Food],
                    pawn.Needs[NeedIndex.Rest],
                    pawn.Mood,
                    pawn.CurrentJob != null ? pawn.CurrentJob.DefIndex : -1,
                    nextCell,
                    movePercent,
                    workFocus >= 0,
                    faces,
                    pawn.Gesture,
                    pawn.GestureSerial,
                    pawn.Asleep,
                    movePerMille,
                    moveDeltaPerMille,
                    pawn.Kind,
                    flags,
                    seated));

                // The fight (design 33 §5), sparse, and for animals as much as people: the health
                // bar is drawn over the hurt, the downed and the drafted, and a hog can be all
                // three but the last. A colony nobody has hurt publishes no hit points — the
                // presence of hp is what says a bar is owed.
                //
                // The pool goes out for every person, hurt or whole, because the Health tab says
                // "x / 100" for a colonist nobody has touched; an absent hp.max would leave it
                // nothing to divide by. An absent hp with a published hp.max reads as whole. An
                // animal has no Health tab, so its pool goes out only beside its hit points.
                bool hurt = pawn.HpMilli < pawn.HpMaxMilli || pawn.Downed || pawn.Drafted;
                if (hurt) writer.AddPawnAspect(pawn.Id, CombatAspects.Hp, pawn.HpMilli);
                if (hurt || pawn.IsPerson) writer.AddPawnAspect(pawn.Id, CombatAspects.HpMax, pawn.HpMaxMilli);
                if (pawn.CombatTarget != 0)
                    writer.AddPawnAspect(pawn.Id, CombatAspects.OrderTarget, pawn.CombatTarget);
                // Lying where she fell with no bed to be carried to (design 33 §11d): why nobody
                // comes. Asked only of the downed, so a colony nobody has hurt pays one flag.
                if (pawn.Downed && RescueRules.NeedsRescue(pawn, _ctx) && RescueRules.BedFor(pawn, pawn, _ctx) < 0)
                    writer.AddPawnAspect(pawn.Id, CombatAspects.RescueNoBed, 1);
                if (pawn.EquippedItem != 0)
                {
                    var weapon = _ctx.Items.Get(new ThingId(pawn.EquippedItem));
                    if (weapon != null && !weapon.Despawned)
                        writer.AddPawnAspect(pawn.Id, CombatAspects.Weapon, weapon.DefIndex);
                }

                // An animal publishes its kind and its pace and nothing else of what follows
                // (design 29 §2): it has no skills, no work, no schedule, no name and nothing in
                // its arms. The pace goes out because the figure's gait speed is read off it.
                if (!pawn.IsPerson)
                {
                    writer.AddPawnAspect(pawn.Id, RateAspects.Move, pawn.MoveRatePerMille());
                    continue;
                }

                // Skills go out as pawn aspects rather than as fields on the view, which is what
                // that mechanism is for: nothing in Sim.Contracts had to learn that skills exist.
                // Every colonist, not only whoever is selected — the snapshot has no notion of
                // selection, that belongs to presentation — and the rows go into a reused buffer,
                // so a steady-state publish still allocates nothing. The level is published as
                // well as the experience because the ladder that derives one from the other is
                // simulation content and is not published.
                for (int s = 0; s < SkillIndex.Count; s++)
                {
                    // The level and the progress come out of one walk of the ladder (SK2). Asking
                    // SkillLevel for one and ProgressPerMille for the other would scan the same
                    // twenty entries twice, every tick, for every skill of every colonist.
                    int progress = _ctx.Content.Skills[s].ProgressPerMille(pawn.Skills[s], out int level);
                    writer.AddPawnAspect(pawn.Id, SkillAspects.Level[s], level);
                    writer.AddPawnAspect(pawn.Id, SkillAspects.Passion[s], pawn.Passions[s]);
                    writer.AddPawnAspect(pawn.Id, SkillAspects.Experience[s], pawn.Skills[s]);
                    writer.AddPawnAspect(pawn.Id, SkillAspects.Progress[s], progress);
                }

                // The work priorities, on the same terms and through the same channel (design 27).
                // Every colonist rather than the selected one, because the Work tab is a grid of
                // everybody and a panel that had to ask for a subscription per row would be the
                // one panel in the game that cannot open. Eight rows a colonist.
                for (int w = 0; w < WorkTypeIndex.Count; w++)
                {
                    writer.AddPawnAspect(pawn.Id, WorkAspects.Priority[w], pawn.WorkPriorities[w]);

                    // Nothing can answer this with a no yet — there are no traits and no health
                    // model — so it is a constant one today. Published anyway: see WorkAspects.
                    writer.AddPawnAspect(pawn.Id, WorkAspects.Capable[w], 1);
                }

                // The day, one aspect an hour. Twenty-four rows a colonist is the most this
                // mechanism has ever been asked for, and it is still the right shape: packing the
                // day into three ints would save twenty-one rows and cost the reader a decode it
                // could get wrong, which is the argument SkillAspects already settled. The buffer
                // is reused, so a steady-state publish still allocates nothing.
                for (int h = 0; h < ScheduleHandle.Hours; h++)
                    writer.AddPawnAspect(pawn.Id, ScheduleAspects.Hour[h], pawn.ScheduleHours[h]);

                // The seed this colonist was rolled from (U40), which is what the interface names
                // them by: a reroll on the select screen has to give you a different person rather
                // than the same person with different numbers, and a name keyed on the pawn id
                // could only ever give the second. Reinterpreted rather than converted — an aspect
                // carries an int and a seed is a uint, and every bit of it matters.
                writer.AddPawnAspect(pawn.Id, SkillAspects.RollSeed, unchecked((int)pawn.RollSeed));

                // The rate she is paying work at right now (design 17 §3d), which is what the
                // stroke clock is scaled by. Asked of the driver on the same terms as the view's
                // Working above — a swing in place, not merely a job — because the clock only
                // turns while the pawn is working, and outside those ticks the standard rate is
                // the honest answer to a question nobody is asking.
                writer.AddPawnAspect(pawn.Id, RateAspects.Work,
                    workFocus >= 0 && pawn.Driver != null
                        ? pawn.WorkRatePerMille(pawn.Driver.WorkType)
                        : Rates.Scale);

                // And the rate she walks at (design 17 §5) — pace and condition composed. Unlike
                // the work rate this is a fact about the pawn wherever she stands, so it
                // publishes for every colonist and not only a working one: whatever draws a
                // colonist's pace wants to be able to ask it of an idle one.
                writer.AddPawnAspect(pawn.Id, RateAspects.Move, pawn.MoveRatePerMille());

                // The draft (design 33 §2e), sparse: a colony nobody drafts publishes nothing
                // new. The order cell only while an order is being walked: a drafted move, or a
                // weapon fetched from the context menu, drafted or not (§7a) — the board draws the
                // same order line to both. Straight after the drafted row, which is what lets the
                // interface attach one to the other without a search.
                if (pawn.Drafted) writer.AddPawnAspect(pawn.Id, CombatAspects.Drafted, 1);
                int orderCell = OrderCellOf(pawn);
                if (orderCell >= 0) writer.AddPawnAspect(pawn.Id, CombatAspects.OrderCell, orderCell);

                // What she has in her arms (design 24 §5b). Two rows, and only while there is
                // something to publish — a carried thing has no cell, so it is delisted from the
                // things below and this is the only channel by which anything can know it still
                // exists. That absence is the whole of the owner's report: "it disappears and they
                // walk off".
                //
                // Guarded on the item rather than on the id alone: a load despawned out from under
                // a job would otherwise publish a def index of -1 into the renderer's table.
                int carried = pawn.CurrentJob != null ? pawn.CurrentJob.CarriedItem : -1;
                if (carried >= 0)
                {
                    var load = _ctx.Items.Get(new ThingId(carried));
                    if (load != null && !load.Despawned)
                    {
                        writer.AddPawnAspect(pawn.Id, CarryAspects.Carrying, load.DefIndex);
                        writer.AddPawnAspect(pawn.Id, CarryAspects.Stack, load.Stack);
                        writer.AddPawnAspect(pawn.Id, CarryAspects.Thing, load.Id.Value);
                    }
                }
            }

            var items = _ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned) continue;

                if (item.Cell >= 0)
                {
                    writer.AddThing(new ThingView(item.Id, size.FromIndex(item.Cell), item.DefIndex, 0, item.Stack));
                    continue;
                }

                // In a store: published at the store's cell, carrying the store's id. A thing in a
                // pair of hands is still skipped — it is drawn by the carrier, through the carry
                // aspects above.
                if (item.ContainerId == 0) continue;

                int where = _ctx.WhereIs(item);
                if (where < 0) continue;

                // The thing's place in its store's ordered contents. A drawing position and
                // nothing else — nothing in the simulation reads it back, and a store re-packs when
                // something leaves it.
                //
                // **The def cannot stand in for it**, which was tried: a store holds several stacks
                // of one kind, so eight stacks of wood would all draw in the same place. The
                // contents index is the only number here that never collides.
                IReadOnlyList<int> holds = _ctx.Items.ContentsOf(item.ContainerId);
                int slot = 0;
                for (int h = 0; h < holds.Count; h++)
                {
                    if (holds[h] != i) continue;
                    slot = h;
                    break;
                }

                writer.AddThing(new ThingView(item.Id, size.FromIndex(where), item.DefIndex, 0,
                    item.Stack, item.ContainerId, (byte)slot));
            }
        }

        /// <summary>
        /// Where the pawn is walking under the player's orders, or -1 (design 33 §2e, §7a): a
        /// drafted colonist's move, or a weapon she was sent for — the only equip there is comes
        /// from an order, drafted or not. An attack or a rescue is drawn to its target, not a cell
        /// (<see cref="CombatAspects.OrderTarget"/>).
        /// </summary>
        static int OrderCellOf(Pawn pawn)
        {
            Job? job = pawn.CurrentJob;
            if (job == null) return -1;
            if (job.DefIndex == JobIndex.Goto && pawn.Drafted) return job.TargetCell;
            if (job.DefIndex == JobIndex.Equip && job.PlayerForced) return job.TargetCell;
            return -1;
        }

        // ---- saving ------------------------------------------------------------------------
        //
        // Mid-job state persists: the job, the toil index, the toil progress and the
        // reservations. Paths do not — they are re-derived on the next tick, because a
        // recomputed path is correct by construction and a saved one can be stale.

        public string SaveKey => "odyssey.pawns";

        public void Save(SaveWriter writer)
        {
            writer.Write(_nextId);
            writer.Write(_pawns.Count);

            for (int i = 0; i < _pawns.Count; i++)
            {
                var pawn = _pawns[i];
                writer.Write(pawn.Id.Value);
                writer.Write(pawn.Cell);

                writer.Write(pawn.Needs.Length);
                for (int n = 0; n < pawn.Needs.Length; n++) writer.Write(pawn.Needs[n]);

                writer.Write(pawn.Mood);
                writer.Write(pawn.MoodTarget);

                writer.Write(pawn.Skills.Length);
                for (int s = 0; s < pawn.Skills.Length; s++) writer.Write(pawn.Skills[s]);

                // Passions as ints, for the same reason the work priorities are below.
                writer.Write(pawn.Passions.Length);
                for (int s = 0; s < pawn.Passions.Length; s++) writer.Write((int)pawn.Passions[s]);

                writer.Write(pawn.SkillGainedToday.Length);
                for (int s = 0; s < pawn.SkillGainedToday.Length; s++) writer.Write(pawn.SkillGainedToday[s]);
                writer.Write(pawn.SkillDay);

                writer.Write(pawn.WorkPriorities.Length);
                // Written as an int, not as the byte it is stored in: the reader asks for an int,
                // and a width mismatch here corrupts every field after it.
                for (int w = 0; w < pawn.WorkPriorities.Length; w++) writer.Write((int)pawn.WorkPriorities[w]);

                // The day's schedule (design 27 §12). Format 7. Ints for the reason the two
                // arrays above are ints: the reader asks for an int and a width mismatch here
                // corrupts every field after it.
                writer.Write(pawn.ScheduleHours.Length);
                for (int h = 0; h < pawn.ScheduleHours.Length; h++) writer.Write((int)pawn.ScheduleHours[h]);

                writer.Write(pawn.BreakTicksLeft);
                writer.Write(pawn.Asleep);
                writer.Write(pawn.JobStartsInWindow);
                writer.Write(pawn.WindowStartTick);

                writer.Write(pawn.Memories.Count);
                for (int m = 0; m < pawn.Memories.Count; m++)
                {
                    writer.Write(pawn.Memories[m].ThoughtIndex);
                    writer.Write(pawn.Memories[m].ExpiryTick);
                }

                writer.Write(pawn.Destination);
                writer.Write(pawn.MoveProgress);

                var job = pawn.CurrentJob;
                writer.Write(job != null);
                if (job != null)
                {
                    writer.Write(job.DefIndex);
                    writer.Write(job.TargetItem.Value);
                    writer.Write(job.TargetCell);
                    writer.Write(job.DestCell);
                    writer.Write(job.CarriedItem);
                    writer.Write(job.PlayerForced);
                    writer.Write((int)job.Mode);
                    writer.Write(job.WorkTicks);
                    writer.Write(pawn.JobStartTick);
                    writer.Write(pawn.Driver != null ? pawn.Driver.ToilIndex : 0);
                    writer.Write(pawn.Driver != null ? pawn.Driver.ToilProgress : 0);
                }

                writer.Write(pawn.HeldReservations.Count);
                for (int r = 0; r < pawn.HeldReservations.Count; r++) writer.Write(pawn.HeldReservations[r]);

                // Last in the section on purpose (WS3): a v5 file ends here, so the field sits
                // where an older reader stops rather than where it would shift every read after
                // it. Format 6, see WorldSave's version history.
                writer.Write(pawn.StarvationSeverity);

                // And last again, for the same reason one field later (design 28 §9): format 8
                // ends at the severity above, and the temperature bar is appended where an
                // older reader stops rather than where it would shift nothing and mean it.
                writer.Write(pawn.TemperatureSeverity);

                // The ambient the colonist last felt, beside the bar it feeds. It is a sample,
                // not a derivation — the cell and the room may both have moved since — and for
                // up to an interval after a load the work rate reads it, so a file that left it
                // out diverged from its own game (design 28 §12, F8). Format 9 with the bar.
                writer.Write(pawn.AmbientTempC);
            }
        }

        public void Load(SaveReader reader)
        {
            _pawns.Clear();
            _byId.Clear();
            _ctx.Reservations.Clear();

            _nextId = reader.ReadInt();
            int count = reader.ReadInt();

            for (int i = 0; i < count; i++)
            {
                var pawn = new Pawn(new PawnId(reader.ReadInt()), reader.ReadInt(), _ctx.Content);
                pawn.DriverPool = BuildDrivers();

                int needCount = reader.ReadInt();
                for (int n = 0; n < needCount; n++)
                {
                    int value = reader.ReadInt();
                    if (n < pawn.Needs.Length) pawn.Needs[n] = value;
                }

                pawn.Mood = reader.ReadInt();
                pawn.MoodTarget = reader.ReadInt();

                int skillCount = reader.ReadInt();
                for (int s = 0; s < skillCount; s++)
                {
                    int value = reader.ReadInt();
                    if (s < pawn.Skills.Length) pawn.Skills[s] = value;
                }

                int passionCount = reader.ReadInt();
                for (int s = 0; s < passionCount; s++)
                {
                    int value = reader.ReadInt();
                    if (s < pawn.Passions.Length) pawn.Passions[s] = (byte)value;
                }

                int todayCount = reader.ReadInt();
                for (int s = 0; s < todayCount; s++)
                {
                    int value = reader.ReadInt();
                    if (s < pawn.SkillGainedToday.Length) pawn.SkillGainedToday[s] = value;
                }
                pawn.SkillDay = reader.ReadInt();

                int workCount = reader.ReadInt();
                for (int w = 0; w < workCount; w++)
                {
                    int value = reader.ReadInt();
                    if (w < pawn.WorkPriorities.Length) pawn.WorkPriorities[w] = (byte)value;
                }

                // Format 7 added the schedule. An older save has none, and the constructor's
                // default day is the right answer for it: a colony saved before schedules existed
                // was being played without one, so it gets the shape every new colonist gets
                // rather than twenty-four blank hours.
                if (reader.FormatVersion >= 7)
                {
                    int hourCount = reader.ReadInt();
                    for (int h = 0; h < hourCount; h++)
                    {
                        int value = reader.ReadInt();
                        if (h < pawn.ScheduleHours.Length) pawn.ScheduleHours[h] = (byte)value;
                    }
                }

                pawn.BreakTicksLeft = reader.ReadInt();
                pawn.Asleep = reader.ReadBool();
                pawn.JobStartsInWindow = reader.ReadInt();
                pawn.WindowStartTick = reader.ReadInt();

                int memoryCount = reader.ReadInt();
                for (int m = 0; m < memoryCount; m++)
                    pawn.Memories.Add(new Memory { ThoughtIndex = reader.ReadInt(), ExpiryTick = reader.ReadInt() });

                pawn.Destination = reader.ReadInt();
                pawn.MoveProgress = Rates.FromSave(reader.ReadInt(), reader.FormatVersion);

                if (reader.ReadBool())
                {
                    var job = pawn.JobBuffer;
                    job.Reset(reader.ReadInt());
                    job.TargetItem = new ThingId(reader.ReadInt());
                    job.TargetCell = reader.ReadInt();
                    job.DestCell = reader.ReadInt();
                    job.CarriedItem = reader.ReadInt();
                    job.PlayerForced = reader.ReadBool();
                    job.Mode = (TraverseMode)reader.ReadInt();
                    job.WorkTicks = reader.ReadInt();
                    pawn.JobStartTick = reader.ReadInt();

                    var driver = pawn.DriverPool[_ctx.Content.Jobs[job.DefIndex].driver];
                    driver.Begin(pawn, job);
                    driver.ToilIndex = reader.ReadInt();
                    driver.ToilProgress = Rates.FromSave(reader.ReadInt(), reader.FormatVersion);
                    pawn.CurrentJob = job;
                    pawn.Driver = driver;
                }

                int reservationCount = reader.ReadInt();
                for (int r = 0; r < reservationCount; r++)
                {
                    long key = reader.ReadLong();
                    _ctx.Reservations.Reserve(pawn.Id, key);
                    pawn.HeldReservations.Add(key);
                }

                // Last in the section from format 6 on; a v5 file simply ends here, and a
                // colonist from one had never been starving by a definition that did not exist.
                if (reader.FormatVersion >= 6)
                    pawn.StarvationSeverity = reader.ReadInt();

                // And last again, from format 9 on (design 28 §9): a colonist from an older
                // file had never been cold by a definition that did not exist.
                if (reader.FormatVersion >= 9)
                {
                    pawn.TemperatureSeverity = reader.ReadInt();
                    pawn.AmbientTempC = reader.ReadInt();
                }

                _byId[pawn.Id.Value] = _pawns.Count;
                _pawns.Add(pawn);
            }
        }
    }
}
