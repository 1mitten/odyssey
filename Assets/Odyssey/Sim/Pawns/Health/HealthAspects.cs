#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The names the body is published under (design 43 §9), minted the way
    /// <see cref="CombatAspects"/> mints the fight's: <c>Sim.Contracts</c> never hears that a body
    /// exists, and the interface asks for each row by name. <b>Every one is sparse</b> — published
    /// only for a pawn with anything on its ledger — so the rows a healthy colonist publishes a
    /// tick (design 31: 57) did not move.
    ///
    /// <para>The injuries go out as rows too, rather than as a view of their own, for the reason
    /// skills do: at most eighteen records, each two rows, and nothing in the shared assembly had
    /// to learn what an injury is. A record's row is its points in thousandths; its care row is
    /// nought untended and <c>1 + quality</c> tended, so "tended at nought" is not "untended".</para>
    ///
    /// <para><b>The spellings are a contract with the interface</b>, which keeps literal copies
    /// (<c>Odyssey.Hud.HealthAspectNames</c>); a test on each side holds its copy to the literal.</para>
    /// </summary>
    public static class HealthAspects
    {
        public const string Prefix = "odyssey.pawn.health.";

        /// <summary>The most regions a body has: the size of every per-region table here.</summary>
        public const int Regions = 6;

        /// <summary>The kinds of injury, the size of every per-kind table here.</summary>
        public const int Kinds = 3;

        public const string PainName = Prefix + "pain";
        public const string ConsciousnessName = Prefix + "consciousness";
        public const string MovingName = Prefix + "moving";
        public const string ManipulationName = Prefix + "manipulation";

        /// <summary>Blood lost, per mille.</summary>
        public const string BloodName = Prefix + "blood";

        /// <summary>Whole hours until the bleeding kills her, rounded up; absent while nothing bleeds.</summary>
        public const string BleedHoursName = Prefix + "bleed.hours";

        /// <summary>How many injury records she carries.</summary>
        public const string InjuriesName = Prefix + "injuries";

        /// <summary>How many of them are tended.</summary>
        public const string TendedName = Prefix + "tended";

        /// <summary>A region's remaining share, per mille: <c>odyssey.pawn.health.region.{index}</c>.</summary>
        public static string RegionName(int region) => Prefix + "region." + region;

        /// <summary>A record's points, thousandths: <c>odyssey.pawn.health.injury.{region}.{kind}</c>.</summary>
        public static string InjuryName(int region, int kind) => Prefix + "injury." + region + "." + kind;

        /// <summary>A record's care: nought untended, <c>1 + quality</c> tended.</summary>
        public static string CareName(int region, int kind) => InjuryName(region, kind) + ".care";

        public static readonly AspectKey Pain = AspectKey.Of(PainName);
        public static readonly AspectKey Consciousness = AspectKey.Of(ConsciousnessName);
        public static readonly AspectKey Moving = AspectKey.Of(MovingName);
        public static readonly AspectKey Manipulation = AspectKey.Of(ManipulationName);
        public static readonly AspectKey Blood = AspectKey.Of(BloodName);
        public static readonly AspectKey BleedHours = AspectKey.Of(BleedHoursName);
        public static readonly AspectKey Injuries = AspectKey.Of(InjuriesName);
        public static readonly AspectKey Tended = AspectKey.Of(TendedName);

        public static readonly AspectKey[] Region = Mint(r => RegionName(r), Regions);
        public static readonly AspectKey[] Injury = MintRecords(false);
        public static readonly AspectKey[] Care = MintRecords(true);

        static AspectKey[] Mint(System.Func<int, string> name, int count)
        {
            var keys = new AspectKey[count];
            for (int i = 0; i < count; i++) keys[i] = AspectKey.Of(name(i));
            return keys;
        }

        static AspectKey[] MintRecords(bool care)
        {
            var keys = new AspectKey[Regions * Kinds];
            for (int r = 0; r < Regions; r++)
                for (int k = 0; k < Kinds; k++)
                    keys[r * Kinds + k] = AspectKey.Of(care ? CareName(r, k) : InjuryName(r, k));
            return keys;
        }

        /// <summary>
        /// Whole hours until blood loss kills, rounded up, or nought while nothing bleeds: the one
        /// number on the tab a player acts on, and a number the simulation knows, so it is published
        /// rather than derived (<c>docs/process.md</c> §3).
        /// </summary>
        public static int HoursToBleedOut(HealthDef body, PawnHealth health, int dayTicks, int hourTicks)
        {
            int bleeding = health.BleedingSeverityMilli;
            if (bleeding <= 0 || dayTicks <= 0 || hourTicks <= 0) return 0;
            long perDay = (long)bleeding * body.bleedPerPointPerDay;
            long left = (long)body.bloodDeathAtPerMille * 1_000 - health.BloodLossMicro;
            if (left <= 0) return 0;
            long hoursPerDay = dayTicks / hourTicks;
            long hours = (left * hoursPerDay + perDay - 1) / perDay;
            return hours > int.MaxValue ? int.MaxValue : (int)hours;
        }

        /// <summary>Publish one pawn's body. Only called for a pawn with anything on its ledger.</summary>
        public static void Publish(SnapshotWriter writer, Pawn pawn, int dayTicks)
        {
            HealthDef? body = pawn.Body;
            PawnHealth? health = pawn.Health;
            if (body == null || health == null || health.IsEmpty) return;

            Vitals vitals = Vitals.Of(body, health);
            PawnId id = pawn.Id;
            for (int r = 0; r < body.regions.Count && r < Regions; r++)
            {
                int pool = body.RegionMilli(r);
                int left = pool - health.RegionDamageMilli(r);
                writer.AddPawnAspect(id, Region[r], pool <= 0 || left <= 0 ? 0 : (int)((long)left * 1_000 / pool));
            }
            writer.AddPawnAspect(id, Pain, vitals.PainPerMille);
            writer.AddPawnAspect(id, Consciousness, vitals.ConsciousnessPerMille);
            writer.AddPawnAspect(id, Moving, vitals.MovingPerMille);
            writer.AddPawnAspect(id, Manipulation, vitals.ManipulationPerMille);
            writer.AddPawnAspect(id, Blood, vitals.BloodLossPerMille);
            int hours = HoursToBleedOut(body, health, dayTicks, dayTicks / 24);
            if (hours > 0) writer.AddPawnAspect(id, BleedHours, hours);
            writer.AddPawnAspect(id, Injuries, health.Count);
            writer.AddPawnAspect(id, Tended, health.TendedCount);

            for (int i = 0; i < health.Count; i++)
            {
                ref Affliction record = ref health[i];
                if (record.Region < 0 || record.Region >= Regions || (int)record.Kind >= Kinds) continue;
                int slot = record.Region * Kinds + (int)record.Kind;
                writer.AddPawnAspect(id, Injury[slot], record.SeverityMilli);
                writer.AddPawnAspect(id, Care[slot], record.Tended ? 1 + record.TendQualityPerMille : 0);
            }
        }
    }
}
