#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Construction;

namespace Odyssey.Sim.Temperature
{
    /// <summary>
    /// The conductance table — how fast each kind of surface passes heat, in per-mille of the
    /// temperature difference per pass (design 28 §3). Structural constants live here; the
    /// material factor lives on <see cref="StuffDef.thermalConductancePerMille"/> with the rest
    /// of the material's stats, and the wall base (4) is applied where the wall is classified.
    ///
    /// <para>The scale is set by the reference's own measured envelope: a single wall moves a
    /// 1×1 room about 1.7% of the difference per 120-tick pass, which four walls at 4‰ of the
    /// standard material reproduce. A roof cell at 6‰ is size-independent per area, exactly as
    /// the reference's roof row is — which is why roofing matters more than wall material in a
    /// big room, and why the two numbers are different.</para>
    /// </summary>
    public static class TemperatureConductance
    {
        /// <summary>Wall base, per wall cell, multiplied by the material's own factor.</summary>
        public const int WallBasePerMille = 4;

        /// <summary>Roof against the open sky, per roof cell. Per area, not perimeter.</summary>
        public const int RoofPerMille = 6;

        /// <summary>Any surface whose far side is earth: walls into rock, floors onto ground,
        /// ceilings into the rock above. The earth is the boundary; the wall's material is
        /// irrelevant.</summary>
        public const int GroundPerMille = 4;

        /// <summary>A slab shared between the rooms above and below it.</summary>
        public const int SlabPerMille = 2;

        /// <summary>A closed door — on its own clock in the reference, and slow here
        /// too.</summary>
        public const int DoorClosedPerMille = 8;

        /// <summary>An open door, described by the reference only as "a very high rate": a
        /// near-merge, short of an actual one.</summary>
        public const int DoorOpenPerMille = 350;

        /// <summary>A vertical opening carrying heat <b>up</b> — the lower room warmer, warm air
        /// climbing freely.</summary>
        public const int OpeningUpPerMille = 400;

        /// <summary>A vertical opening carrying heat <b>down</b> — the upper room warmer, and
        /// cold does not fall as fast as warmth rises. The 4:1 with the above is the whole of
        /// buoyancy (design 28 §4).</summary>
        public const int OpeningDownPerMille = 100;

        /// <summary>A hole in the floor over open air: nearly open to it.</summary>
        public const int FloorHolePerMille = 250;

        /// <summary>
        /// A wall's material factor, from its raw stuff value — the thing a standing
        /// <c>PlacedEdifice</c> carries. Rock and unknown materials read as the standard, which
        /// is what the generator's walls are.
        /// </summary>
        public static int WallStuffPerMille(ushort stuffValue)
        {
            int handle = ConstructionContent.StuffForValue(stuffValue);
            return handle <= StuffHandle.None
                ? 1_000
                : ConstructionContent.StuffAt(handle).thermalConductancePerMille;
        }
    }
}
