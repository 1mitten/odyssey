#nullable enable
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Worldgen.Natural
{
    /// <summary>
    /// A wild thing that stands in a cell — a tree of one species, or a bush — and what it costs
    /// and gives (design 45 §2). One Def per kind, in <c>Defs/Core/World/WildPlants.xml</c>;
    /// which edifice id each one is decided by <see cref="WorldContent.WildPlantOrder"/> and
    /// <see cref="NaturalContent.WildPlantSlot"/>, never here.
    ///
    /// <para><b>Clearing</b> is felling a tree or grubbing out a bush: the same order, the same
    /// work giver and the same driver, which reads the work and the yield off this Def. A bush
    /// may yield nothing, which is an empty <see cref="clearYields"/>.</para>
    ///
    /// <para><b>Fruit</b> is what a berry bush gives when it is picked, and how long it takes to
    /// come back. Zero <see cref="fruitCount"/> means the kind bears nothing — every tree, for
    /// now, fruit trees included (the owner deferred their fruit).</para>
    /// </summary>
    public class WildPlantDef : Def
    {
        /// <summary>Ticks of work at the standard rate to fell or clear it.</summary>
        [DefRequired] public int clearWorkTicks = 800;

        /// <summary>What felling or clearing leaves, or empty for nothing.</summary>
        [DefReference(typeof(ItemDef), Optional = true)] public string clearYields = string.Empty;

        /// <summary>How many of <see cref="clearYields"/>.</summary>
        public int clearYieldCount;

        /// <summary>What a picking gives, or empty for a plant that bears nothing.</summary>
        [DefReference(typeof(ItemDef), Optional = true)] public string fruitYields = string.Empty;

        /// <summary>How many of <see cref="fruitYields"/> a picking gives.</summary>
        public int fruitCount;

        /// <summary>Ticks of work at the standard rate to pick it.</summary>
        public int fruitWorkTicks;

        /// <summary>Ticks from a picking until the fruit is back.</summary>
        public int fruitRegrowTicks;

        /// <summary>
        /// What it is worth as cover to a pawn standing beside it, per mille (design 53 §3): a
        /// tree 250, the reference's number; a bush 150, ours. Read only through
        /// <c>Cover.BaseAt</c>.
        /// </summary>
        public int coverPerMille;

        /// <summary>Tall cover (a tree) rather than low (a bush): design 53 §2b's two classes.</summary>
        public bool coverTall;

        /// <summary>Does this kind bear anything to pick?</summary>
        public bool BearsFruit => fruitCount > 0 && fruitYields.Length > 0;
    }
}
