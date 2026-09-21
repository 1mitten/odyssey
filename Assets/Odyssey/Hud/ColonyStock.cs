#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// How much of a commodity the colony is holding, anywhere.
    ///
    /// <para><b>Anywhere is the whole point.</b> The Build palette greys a material the colony
    /// cannot pay for, and it worked out the number by summing loose piles — which was every pile
    /// there was, until a shelf could hold one. A colony that tidies its timber on to a shelf and
    /// is then told it cannot afford a wall is not a cosmetic fault; it is the palette lying about
    /// the world.</para>
    ///
    /// <para><b>It lives here, in <c>Odyssey.Hud</c>, rather than in the shell that draws the
    /// palette, for one reason: the fast tier compiles this assembly and does not compile
    /// Presentation.</b> A correctness bug with no test in the tier that runs in twenty seconds is
    /// a correctness bug nobody re-runs.</para>
    ///
    /// <para>Contained rows are counted like any other, because they are published at their store's
    /// cell carrying its id — the same decision that leaves the stores panel correct without being
    /// told anything about shelves.</para>
    /// </summary>
    public static class ColonyStock
    {
        /// <summary>Units of one item def held anywhere: on the floor, and in every store.</summary>
        public static int Of(WorldSnapshot snapshot, int itemDef)
        {
            if (itemDef < 0) return 0;

            int held = 0;
            System.ReadOnlySpan<ThingView> things = snapshot.Things;
            for (int i = 0; i < things.Length; i++)
                if (things[i].DefIndex == itemDef) held += things[i].Stack;
            return held;
        }
    }
}
