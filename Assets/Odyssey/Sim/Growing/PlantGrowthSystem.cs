#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Growing
{
    /// <summary>
    /// Grows the crops. The whole system is one pass over the planted cells, run on the Rare
    /// cadence and charged to the daylight window.
    ///
    /// <para><b>Why a system and not a tick on the zones.</b> The zone grid is authored state
    /// that never ticks — it is parked in <see cref="TickGroup.Never"/> so the hash and the save
    /// can see it — while growth is the one piece of behaviour the crop owns, and behaviour runs
    /// in the WorldSystems phase with the rest of the world's own processes. A system also gets
    /// a name in the schedule, which is where a profile names it from.</para>
    ///
    /// <para><b>The cost is O(planted), and only every 250th tick.</b> The pass walks the sparse
    /// planted list, adds one interval to each counter and re-meshes a chunk only when a cell's
    /// drawn stage bucket changes — three re-meshes in a crop's lifetime. An empty zone costs
    /// nothing and a field of thousands costs a comparison each on the ticks in between, which
    /// is why the three standard ten-day seeds do not notice this system exists
    /// (docs/design/22-growing.md §3).</para>
    /// </summary>
    public sealed class PlantGrowthSystem : IWorldSystem
    {
        /// <summary>How often the pass runs, in ticks — the Rare tick group's interval. Each run inside the window credits every crop one interval of growth.</summary>
        public const int IntervalTicks = 250;

        readonly PawnContext _pawns;
        readonly GrowingZones _zones;

        public PlantGrowthSystem(PawnContext pawns, GrowingZones zones)
        {
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
        }

        public string Name => "growing";
        public TickPhase Phase => TickPhase.WorldSystems;

        /// <summary>
        /// After movement, so the schedule reads ground first and everything that lives on it
        /// second. Nothing reads growth within a tick — the sowing job asks at its own pace —
        /// so the position is a convention and not a dependency.
        /// </summary>
        public int Order => 40;

        public void Tick(SimWorld world)
        {
            if (world.CurrentTick % IntervalTicks != 0) return;

            int tickOfDay = world.CurrentTick % _pawns.Content.DayTicks;
            var planted = _zones.Planted;
            for (int i = 0; i < planted.Count; i++)
            {
                int index = planted[i];
                PlantDef def = _zones.Plant(_zones.CropPlant(index));
                if (!def.GrowsAt(tickOfDay)) continue;

                // How much of this interval the temperature lets the crop keep — the hook
                // design 22 §8 recorded and design 28 §8 fills. The gain is the interval scaled
                // by the response and floored by integer division, so a crop out of its band
                // waits rather than creeps, and the two numbers cannot disagree about which
                // happened.
                int gain = IntervalTicks;
                if (_pawns.Temperature != null)
                {
                    int rate = def.GrowRatePerMille(_pawns.Temperature.CellTemp(index, world.CurrentTick));
                    if (rate <= 0) continue;
                    if (rate < 1_000) gain = IntervalTicks * rate / 1_000;
                }

                int before = def.StageOfTicks(_zones.GrowthTicks(index));
                int after = _zones.Advance(index, gain);
                if (after != before) _pawns.Chunks?.MarkDirty(_zones.Size.FromIndex(index));
            }
        }
    }
}
