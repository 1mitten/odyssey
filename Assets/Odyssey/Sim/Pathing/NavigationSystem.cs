#nullable enable
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pathing
{
    /// <summary>
    /// Keeps the navigation graph current inside the tick.
    ///
    /// This system exists to close a gap that is easy to miss and expensive to find: the pathing
    /// library maintains regions and districts perfectly well, but nothing was calling it during
    /// a tick. Pawns would then read a stale district table in a changing world and conclude that
    /// a place they can walk to is unreachable, or worse, that a place they cannot reach is
    /// reachable — a bug that only shows up once the world starts being mined and built, which is
    /// to say in M3, a long way from its cause.
    ///
    /// Order 20 in the world phase, deliberately **after** the support solver at order 10. A
    /// collapse changes what is walkable, so navigation must see the world after it has settled
    /// structurally, not before.
    /// </summary>
    public sealed class NavigationSystem : IWorldSystem
    {
        readonly NavGraph _nav;
        readonly SupportSystem? _support;

        public NavigationSystem(NavGraph nav, SupportSystem? support = null)
        {
            _nav = nav;
            _support = support;
        }

        public string Name => "Navigation";

        public TickPhase Phase => TickPhase.WorldSystems;

        public int Order => 20;

        /// <summary>How many ticks actually did rebuilding work. Useful when profiling.</summary>
        public int RebuildCount { get; private set; }

        public void Tick(SimWorld world)
        {
            // Anything the support solver brought down this tick changed what is walkable there.
            // Feeding those cells in here, rather than having the solver know about navigation,
            // keeps the two libraries independent of each other.
            if (_support != null)
            {
                var collapsed = _support.LastCollapses;
                for (int i = 0; i < collapsed.Count; i++)
                {
                    var cell = collapsed[i];
                    _nav.MarkDirty(cell.X, cell.Z, cell.Y);

                    // A slab that fell also changed the cell below it, which may now be open to
                    // the sky, and the cell above, which may now have nothing to stand on.
                    if (cell.Y > 0) _nav.MarkDirty(cell.X, cell.Z, cell.Y - 1);
                    if (cell.Y + 1 < world.Size.SizeY) _nav.MarkDirty(cell.X, cell.Z, cell.Y + 1);
                }
            }

            // Rebuild is dirty-block-local and returns false when there was nothing to do, so
            // calling it every tick costs almost nothing in a still world.
            if (_nav.Rebuild()) RebuildCount++;
        }
    }
}
