#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The answer to a <c>QueryShot</c> (design 50 §8b): what a shot from one pawn at another would
    /// come to, worked by the very rules that roll it — <see cref="IRangedRules.HitChancePerMille"/>
    /// and <see cref="IRangedRules.CoverPerMille"/> — so the hover readout can never promise odds
    /// the dice do not keep. Published only while the question stands; nothing saved, nothing hashed.
    /// </summary>
    public sealed class ShotReportContributor : ISnapshotContributor
    {
        readonly PawnContext _ctx;
        readonly CoverReport _cover = new CoverReport();

        public ShotReportContributor(PawnContext ctx) => _ctx = ctx;

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            if (world.Views.QueryShotShooter <= 0) return;
            Pawn? shooter = _ctx.Pawns.Get(new PawnId(world.Views.QueryShotShooter));
            Pawn? target = _ctx.Pawns.Get(new PawnId(world.Views.QueryShotTarget));
            if (shooter == null || target == null || shooter == target) return;
            if (!Report(shooter, target, out ShotReportView report)) return;
            writer.SetShotReport(report);
        }

        /// <summary>The report for one pair, or false when the shooter holds no gun. Public so a test reads the same numbers the readout does.</summary>
        public bool Report(Pawn shooter, Pawn target, out ShotReportView report)
        {
            report = default;
            Armament armament = _ctx.WeaponRules.ArmamentOf(shooter, _ctx);
            RangedDef? ranged = armament.Attack.ranged;
            if (ranged == null) return false;

            IRangedRules rules = _ctx.RangedRules;
            int distance = RangedGeometry.DistanceMm(_ctx.Size, shooter.Cell, target.Cell);
            int aim = rules.HitChancePerMille(shooter, distance, armament, _ctx);
            int cover = rules.CoverPerMille(shooter.Cell, target.Cell, _ctx, _cover);
            bool inRange = Ranged.InRange(_ctx.Size, shooter.Cell, target.Cell, ranged);
            bool inSight = LineOfSight.Clear(_ctx, shooter.Cell, target.Cell);
            int total = inRange && inSight ? aim * (1_000 - cover) / 1_000 : 0;

            report = new ShotReportView(shooter.Id, target.Id, aim, cover, total, distance,
                rules.ShootingLevel(shooter), armament.ItemDef, _cover.LowElevationPerMille,
                TopCover(), _cover.Count, inRange, inSight);
            return true;
        }

        /// <summary>The piece that gives the most, as an edifice id; -1 for a rock face; nought for none.</summary>
        int TopCover()
        {
            int best = -1, bestValue = 0;
            for (int i = 0; i < _cover.Count; i++)
            {
                if (_cover.PerMille[i] <= bestValue) continue;
                bestValue = _cover.PerMille[i];
                best = _cover.Cells[i];
            }
            if (best < 0) return 0;
            int handle = _ctx.Cells.Edifice[best];
            var construction = _ctx.Construction;
            if (handle < 0 || construction == null) return -1;
            List<PlacedEdifice> records = construction.Edifices.Records;
            return handle < records.Count ? records[handle].Def : -1;
        }
    }
}
