#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The names the simulation publishes the body under (design 43 §9), as the interface reads
    /// them. String literals and not a shared constant, on the bargain
    /// <see cref="CombatAspectNames"/> makes: this assembly cannot reference <c>Odyssey.Sim</c>, and
    /// a test on each side holds its copy to the literal — <c>HealthAspectNamesTests</c> here,
    /// <c>HealthContractTests</c> in the simulation's suite, which reads
    /// <c>Odyssey.Sim.Pawns.HealthAspects</c>.
    ///
    /// <para>Every one is sparse: published only for a pawn with anything on its ledger, so absent
    /// means whole.</para>
    /// </summary>
    public static class HealthAspectNames
    {
        public const string Prefix = "odyssey.pawn.health.";

        /// <summary>The regions a body has, and the kinds of injury, as the simulation counts them.</summary>
        public const int Regions = 6, Kinds = 3;

        public const string Pain = Prefix + "pain";
        public const string Consciousness = Prefix + "consciousness";
        public const string Moving = Prefix + "moving";
        public const string Manipulation = Prefix + "manipulation";
        public const string Blood = Prefix + "blood";
        public const string BleedHours = Prefix + "bleed.hours";
        public const string Injuries = Prefix + "injuries";
        public const string Tended = Prefix + "tended";

        public static string Region(int region) => Prefix + "region." + region;
        public static string Injury(int region, int kind) => Prefix + "injury." + region + "." + kind;
        public static string Care(int region, int kind) => Injury(region, kind) + ".care";

        public static readonly AspectKey PainKey = AspectKey.Of(Pain);
        public static readonly AspectKey ConsciousnessKey = AspectKey.Of(Consciousness);
        public static readonly AspectKey MovingKey = AspectKey.Of(Moving);
        public static readonly AspectKey ManipulationKey = AspectKey.Of(Manipulation);
        public static readonly AspectKey BloodKey = AspectKey.Of(Blood);
        public static readonly AspectKey BleedHoursKey = AspectKey.Of(BleedHours);
        public static readonly AspectKey InjuriesKey = AspectKey.Of(Injuries);
        public static readonly AspectKey TendedKey = AspectKey.Of(Tended);

        public static readonly AspectKey[] RegionKeys = MintRegions();
        public static readonly AspectKey[] InjuryKeys = MintRecords(false);
        public static readonly AspectKey[] CareKeys = MintRecords(true);

        static AspectKey[] MintRegions()
        {
            var keys = new AspectKey[Regions];
            for (int r = 0; r < Regions; r++) keys[r] = AspectKey.Of(Region(r));
            return keys;
        }

        static AspectKey[] MintRecords(bool care)
        {
            var keys = new AspectKey[Regions * Kinds];
            for (int r = 0; r < Regions; r++)
                for (int k = 0; k < Kinds; k++)
                    keys[r * Kinds + k] = AspectKey.Of(care ? Care(r, k) : Injury(r, k));
            return keys;
        }
    }
}
