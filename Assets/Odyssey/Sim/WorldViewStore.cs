#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim
{
    /// <summary>Fills the back buffer with whatever presentation needs to see this tick.</summary>
    public interface ISnapshotContributor
    {
        void Contribute(SimWorld world, SnapshotWriter writer);
    }

    /// <summary>
    /// The narrow writing surface handed to contributors. Deliberately not the snapshot itself,
    /// so a contributor cannot read the front buffer, retain the back buffer, or reorder anything.
    /// </summary>
    public sealed class SnapshotWriter
    {
        WorldSnapshot _target = null!;

        internal void Retarget(WorldSnapshot target) => _target = target;

        public void AddPawn(in PawnView view) => _target.AddPawn(view);

        public void AddThing(in ThingView view) => _target.AddThing(view);

        /// <summary>Claim the slice buffer and write one byte per cell of the active layer.</summary>
        public Span<byte> BeginSlice(int cellCount) => _target.BeginSlice(cellCount);

        /// <summary>Publish one standing order, wherever in the world it is.</summary>
        public void AddOrder(in OrderView view) => _target.AddOrder(view);

        /// <summary>Publish one part-mined cell with no order on it (design 57 §3).</summary>
        public void AddPartMined(in PartMinedView view) => _target.AddPartMined(view);

        /// <summary>Publish one built store — a shelf — and how full it is.</summary>
        public void AddStorageUnit(in StorageUnitView view) => _target.AddStorageUnit(view);

        /// <summary>Publish one cooking station, after its bills: <see cref="StationView.FirstBill"/> is
        /// <see cref="BillCursor"/> read before the first of them was added.</summary>
        public void AddStation(in StationView view) => _target.AddStation(view);

        /// <summary>Publish one bill. See <see cref="AddStation"/>.</summary>
        public void AddBill(in BillView view) => _target.AddBill(view);

        /// <summary>How many bills this frame holds so far: where the next one will land.</summary>
        public int BillCursor => _target.BillCount;

        /// <summary>Publish one building site, wherever in the world it is.</summary>
        public void AddSite(in SiteView view) => _target.AddSite(view);

        /// <summary>Publish one growing-zone cell, wherever in the world it is.</summary>
        public void AddZone(in ZoneView view) => _target.AddZone(view);

        /// <summary>Publish one storage-zone cell, wherever in the world it is.</summary>
        public void AddStore(in StoreView view) => _target.AddStore(view);

        /// <summary>Publish one planted cell, wherever in the world it is.</summary>
        public void AddPlant(in PlantView view) => _target.AddPlant(view);

        /// <summary>
        /// Publish one number about one pawn, under a name the feature owns. See
        /// <see cref="PawnAspect"/> for why this exists rather than another field on
        /// <see cref="PawnView"/>.
        ///
        /// <para>The pawn need not have been added to this frame by anyone, and the contributor
        /// that adds pawns need not have run yet: an aspect is a row keyed by id, not a field on a
        /// row. A row naming a pawn that is no longer published is simply never read, because a
        /// reader looks aspects up for a pawn it already holds — the same reason a view carries an
        /// id and never a reference.</para>
        /// </summary>
        public void AddPawnAspect(PawnId pawn, AspectKey key, int value) =>
            _target.AddPawnAspect(new PawnAspect(pawn, key, value));

        /// <summary>
        /// Publish the answer for one asked-about cell. See <see cref="CellDetail"/> for why this
        /// is a row per question rather than a channel per layer.
        /// </summary>
        public void AddCellDetail(in CellDetail detail) => _target.AddCellDetail(detail);

        /// <summary>The answer to the standing <c>QueryShot</c> (design 53 §8b).</summary>
        public void SetShotReport(in ShotReportView report) => _target.SetShotReport(report);

        /// <summary>Publish one entry of the incident ledger. See <see cref="BulletinView"/>.</summary>
        public void AddBulletin(in BulletinView view) => _target.AddBulletin(view);

        /// <summary>Publish one thing in the air. See <see cref="FallingView"/>.</summary>
        public void AddFalling(in FallingView view) => _target.AddFalling(view);

        public void AddProjectile(in ProjectileView view) => _target.AddProjectile(view);

        /// <summary>Publish one raid. See <see cref="RaidView"/>.</summary>
        public void AddRaid(in RaidView view) => _target.AddRaid(view);

        /// <summary>Publish one line cell. See <see cref="ConduitView"/> for which are published when.</summary>
        public void AddConduit(in ConduitView view) => _target.AddConduit(view);

        /// <summary>Publish one power building.</summary>
        public void AddPowerDevice(in PowerDeviceView view) => _target.AddPowerDevice(view);

        /// <summary>Publish one power net's balance.</summary>
        public void AddPowerNet(in PowerNetView view) => _target.AddPowerNet(view);

        /// <summary>Say which drawing of the lines this frame's rows are. See <see cref="WorldSnapshot.PowerVersion"/>.</summary>
        public void SetPowerVersion(int version) => _target.SetPowerVersion(version);

        /// <summary>Publish the sky. See <see cref="WorldSnapshot.Weather"/>.</summary>
        public void SetWeather(in WeatherView view) => _target.SetWeather(view);

        /// <summary>Say where the hearth is. See <see cref="WorldSnapshot.HearthCell"/>.</summary>
        public void SetHearthCell(int cell) => _target.SetHearthCell(cell);

        /// <summary>Say which working-out of the home this frame's rows are. See <see cref="WorldSnapshot.HomeVersion"/>.</summary>
        public void SetHomeVersion(int version) => _target.SetHomeVersion(version);

        /// <summary>Publish one border cell of the home. See <see cref="HomeCellView"/>.</summary>
        public void AddHomeCell(in HomeCellView view) => _target.AddHomeCell(view);

        /// <summary>Publish one moment of a fight. See <see cref="CombatEventView"/>.</summary>
        public void AddCombatEvent(in CombatEventView view) => _target.AddCombatEvent(view);

        /// <summary>Publish one corpse. See <see cref="CorpseView"/>.</summary>
        public void AddCorpse(in CorpseView view) => _target.AddCorpse(view);

        /// <summary>Publish one struck building (design 33 §13i). See <see cref="EdificeDamageView"/>.</summary>
        public void AddEdificeDamage(in EdificeDamageView view) => _target.AddEdificeDamage(view);

        /// <summary>Publish the content's hit points for one edifice id: nought is "not a target".</summary>
        public void SetEdificeHitPoints(int edifice, int points) => _target.SetEdificeHitPoints(edifice, points);
    }

    /// <summary>
    /// Double-buffered publication of world state to presentation.
    ///
    /// The simulation builds the next view into a pooled back buffer at tick end and swaps a
    /// single reference. Readers always see a complete, self-consistent frame; there is no moment
    /// at which a half-written snapshot is observable, and no lock.
    ///
    /// Budget, adopted from the UI design line and confirmed by the D1 benchmark: 0.8 ms per tick
    /// and 2 MB across both buffers. The measured figure for a full 62,500-cell slice plus pawn
    /// and thing views was 0.186 ms and 69 KB, so the budget has real headroom.
    /// </summary>
    public sealed class WorldViewStore
    {
        readonly WorldSnapshot _a = new WorldSnapshot();
        readonly WorldSnapshot _b = new WorldSnapshot();
        readonly SnapshotWriter _writer = new SnapshotWriter();
        bool _frontIsA = true;

        /// <summary>
        /// The most recently published frame. Valid until the next publish, so presentation reads
        /// it within the frame and does not retain it.
        /// </summary>
        public WorldSnapshot Current => _frontIsA ? _a : _b;

        WorldSnapshot Back => _frontIsA ? _b : _a;

        /// <summary>The layer presentation is currently slicing at. Set through an intent.</summary>
        public int SliceLayer { get; internal set; }

        /// <summary>
        /// The cell the interface has asked detail about, as a whole-world index, or -1 when no
        /// question stands. Set through a <c>QueryCell</c> intent, and like the slice layer it is
        /// view state: not saved, not hashed, and a question changes nothing the simulation owns.
        /// </summary>
        public int QueryCell { get; internal set; } = -1;

        /// <summary>
        /// Is presentation showing the power lines (design 32 §9)? While it is, built lines are
        /// published; while it is not, only orders and marks are. Set through a
        /// <c>WatchPower</c> intent, and view state like <see cref="QueryCell"/>: not saved, not
        /// hashed, and changing nothing the simulation owns.
        /// </summary>
        public bool WatchPower { get; internal set; }

        /// <summary>
        /// Is presentation showing the home (design 43 §5c)? While it is, the home's border cells are
        /// published. Set through a <c>WatchHome</c> intent; view state like <see cref="WatchPower"/>.
        /// </summary>
        public bool WatchHome { get; internal set; }

        /// <summary>
        /// The shot the interface has asked about (design 53 §8b): the shooter's and the target's
        /// pawn ids, nought for no question. View state like <see cref="QueryCell"/>.
        /// </summary>
        public int QueryShotShooter { get; internal set; }

        /// <inheritdoc cref="QueryShotShooter"/>
        public int QueryShotTarget { get; internal set; }

        public int PublishCount { get; private set; }

        /// <summary>
        /// Build the back buffer and swap. Called once per tick, at the end, after every system
        /// has finished mutating the world.
        /// </summary>
        public void Publish(SimWorld world, ISnapshotContributor[] contributors)
        {
            var back = Back;
            back.BeginWrite(world.CurrentTick, world.Size, SliceLayer, world.GameSpeed, world.Seed);
            _writer.Retarget(back);

            for (int i = 0; i < contributors.Length; i++) contributors[i].Contribute(world, _writer);

            _frontIsA = !_frontIsA;
            PublishCount++;
        }
    }
}
