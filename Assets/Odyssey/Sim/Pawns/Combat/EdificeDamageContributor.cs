#nullable enable
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What presentation and the interface see of buildings as targets (design 33 §13i): a row per
    /// struck building (<see cref="EdificeDamageView"/>), and the content's hit points per edifice
    /// id (<see cref="WorldSnapshot.EdificeHitPoints"/>) — which is how a right-click tells a wall
    /// from a floor without the interface keeping a copy of <see cref="BuildingTargets"/>' rule.
    ///
    /// <para><b>Neither saved nor hashed</b>: a report of <see cref="EdificeDamage"/> and of the
    /// content. <b>Scales with</b> the buildings that have been struck, plus one number per row of
    /// the building table (twelve today) a publish.</para>
    /// </summary>
    public sealed class EdificeDamageContributor : ISnapshotContributor
    {
        readonly PawnContext _ctx;

        public EdificeDamageContributor(PawnContext ctx)
        {
            _ctx = ctx ?? throw new System.ArgumentNullException(nameof(ctx));
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            var buildings = ConstructionContent.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                ushort edifice = buildings[i].edifice;
                if (edifice != 0) writer.SetEdificeHitPoints(edifice, BuildingTargets.MaxHitPointsOf(edifice));
            }

            EdificeDamage damage = _ctx.EdificeDamage;
            for (int i = 0; i < damage.Count; i++)
            {
                int cell = damage.CellAt(i);
                if (!BuildingTargets.TryFind(_ctx, cell, out BuildingTarget target)) continue;
                writer.AddEdificeDamage(new EdificeDamageView(target.Anchor, target.Edifice, damage.HpMilliAt(i), target.MaxMilli));
            }
        }
    }
}
