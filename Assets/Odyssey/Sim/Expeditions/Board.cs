#nullable enable
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// One live board in a <see cref="Campaign"/> (design 64 §4a): home, or a site board built for a
    /// place and discarded when the last colonist walks off it. Slot 0 is home and never goes away.
    /// </summary>
    public sealed class Board
    {
        /// <summary>
        /// Where this board sits in the campaign's tick and hash order. Slots are handed out once and
        /// never reused within a campaign, so a save that names a slot names one board.
        /// </summary>
        public int Slot { get; }

        /// <summary>The place this board was built for, or -1 for home.</summary>
        public int Place { get; }

        public ColonyWorld World { get; }

        public bool IsHome => Place < 0;

        internal Board(int slot, int place, ColonyWorld world)
        {
            Slot = slot;
            Place = place;
            World = world;
        }
    }
}
