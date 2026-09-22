#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Where a load is going: a cell of a painted zone, or a built store.
    ///
    /// <para><b>One answer, not two.</b> The destination rule — accepts, then space for the whole
    /// load, then the highest priority, then nearest — is one walk with one owner, and the day it
    /// became two walks is the day a zone and a shelf could disagree about which of them is better.
    /// That is the fault <c>HopPriceHasOneOwnerTests</c> exists for one level down, where the cell
    /// search, the region graph and the mover each had to price a hop and a disagreement failed
    /// silently.</para>
    ///
    /// <para><see cref="Stand"/> is the only field the job record ever sees. Both ends of a haul are
    /// named by a <b>cell</b>, and a store at either end is named by the cell it stands in: a cell
    /// holds at most one edifice, so that is unambiguous, and it costs no new field on
    /// <see cref="Job"/> — which is written inside the pawns section and read sequentially, so two
    /// more ints there would be a save-format bump bought for nothing.</para>
    /// </summary>
    public readonly struct StorageSlot
    {
        /// <summary>The ground cell the load is put down on, or -1 when the destination is a store.</summary>
        public readonly int Cell;

        /// <summary>The store the load goes into, or 0 when the destination is a cell.</summary>
        public readonly int ContainerId;

        /// <summary>
        /// The cell the hauler walks to: the ground cell itself, or the cell the store stands in.
        /// -1 when there is no destination at all.
        /// </summary>
        public readonly int Stand;

        public StorageSlot(int cell, int containerId, int stand)
        {
            Cell = cell;
            ContainerId = containerId;
            Stand = stand;
        }

        public bool IsContainer => ContainerId != 0;

        /// <summary>
        /// No destination at all.
        ///
        /// <para><b>Spelled out because <c>default</c> is not it.</b> A defaulted struct has
        /// <see cref="Stand"/> 0, and 0 is a perfectly good cell index — the corner of the board —
        /// so a failed search that fell back on <c>default</c> would send every unhaulable load to
        /// cell 0 rather than leaving it where it fell.</para>
        /// </summary>
        public static StorageSlot None => new StorageSlot(-1, 0, -1);
    }
}
