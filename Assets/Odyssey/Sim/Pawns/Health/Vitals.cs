#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What a body can do right now (design 43 §3): pain and the three capacities, per mille,
    /// derived from the ledger and the blood on demand and never saved. A pawn with no body, or
    /// with nothing on its ledger, is <see cref="Whole"/>: pain nought and every capacity 1,000,
    /// which is exact, so nobody's rates move until they are hurt.
    ///
    /// <para><b>Scales with the pawn's records</b>, eighteen at most, and is asked by the rates on
    /// every step of a hurt pawn's walk and by the needs cadence. A whole pawn costs one flag.</para>
    /// </summary>
    public readonly struct Vitals
    {
        public readonly int PainPerMille;
        public readonly int ConsciousnessPerMille;
        public readonly int MovingPerMille;
        public readonly int ManipulationPerMille;

        /// <summary>Blood lost, per mille.</summary>
        public readonly int BloodLossPerMille;

        /// <summary>Down by the body (design 43 §3): pain shock, too little consciousness or too little moving.</summary>
        public readonly bool Incapacitated;

        public Vitals(int pain, int consciousness, int moving, int manipulation, int bloodLoss, bool incapacitated)
        {
            PainPerMille = pain;
            ConsciousnessPerMille = consciousness;
            MovingPerMille = moving;
            ManipulationPerMille = manipulation;
            BloodLossPerMille = bloodLoss;
            Incapacitated = incapacitated;
        }

        public static readonly Vitals Whole = new Vitals(0, 1_000, 1_000, 1_000, 0, false);

        /// <summary>The vitals of a pawn: <see cref="Whole"/> unless it has a body and something on its ledger.</summary>
        public static Vitals Of(Pawn pawn)
        {
            HealthDef? body = pawn.Body;
            PawnHealth? health = pawn.Health;
            if (body == null || health == null || health.IsEmpty) return Whole;
            return Of(body, health);
        }

        /// <summary>
        /// The arithmetic, apart from any pawn so a test can hold it to a-02's numbers:
        /// <list type="bullet">
        /// <item>Pain is every point of live injury × 1.25 % (a-02:30).</item>
        /// <item>Consciousness loses <c>(pain − 10 %) × 4/9</c>, up to 40 % (a-02:24), then takes
        /// the blood-loss stage's factor and cap (a-02:35). A vital region at nought puts it at
        /// nought.</item>
        /// <item>Moving and manipulation are the mean of their regions' remaining shares, times
        /// consciousness.</item>
        /// <item>Down at pain shock, below 30 % consciousness, or at 15 % moving or less (a-02:47-50).</item>
        /// </list>
        /// </summary>
        public static Vitals Of(HealthDef body, PawnHealth health)
        {
            int total = health.TotalSeverityMilli;
            long painLong = (long)total * body.painPerPointTenths / 10_000;
            int pain = painLong > 1_000 ? 1_000 : (int)painLong;

            int fromPain = (pain - body.painConsciousnessFromPerMille) * 4 / 9;
            if (fromPain < 0) fromPain = 0;
            if (fromPain > body.painConsciousnessMaxPerMille) fromPain = body.painConsciousnessMaxPerMille;
            int consciousness = 1_000 - fromPain;

            int blood = health.BloodLossMicro / 1_000;
            for (int s = body.bloodStages.Count - 1; s >= 0; s--)
            {
                BloodStage stage = body.bloodStages[s];
                if (blood < stage.atPerMille) continue;
                consciousness = consciousness * stage.consciousnessPerMille / 1_000;
                if (consciousness > stage.consciousnessCapPerMille) consciousness = stage.consciousnessCapPerMille;
                break;
            }

            int movingSum = 0, movingCount = 0, handsSum = 0, handsCount = 0;
            for (int r = 0; r < body.regions.Count; r++)
            {
                BodyRegionDef region = body.regions[r];
                int pool = body.RegionMilli(r);
                int left = pool - health.RegionDamageMilli(r);
                int share = pool <= 0 || left <= 0 ? 0 : (int)((long)left * 1_000 / pool);
                // Nought is no points left, not a share that rounds to nothing: a head with a
                // thousandth of a point left is still a head (HealthTests found the difference).
                if (region.vital && left <= 0) consciousness = 0;
                if (region.capacity == BodyCapacity.Moving) { movingSum += share; movingCount++; }
                else if (region.capacity == BodyCapacity.Manipulation) { handsSum += share; handsCount++; }
            }

            if (consciousness < 0) consciousness = 0;
            int moving = (movingCount == 0 ? 1_000 : movingSum / movingCount) * consciousness / 1_000;
            int manipulation = (handsCount == 0 ? 1_000 : handsSum / handsCount) * consciousness / 1_000;

            bool down = pain >= body.painShockPerMille
                || consciousness < body.downedConsciousnessBelowPerMille
                || moving <= body.downedMovingAtPerMille;

            return new Vitals(pain, consciousness, moving, manipulation, blood, down);
        }
    }
}
