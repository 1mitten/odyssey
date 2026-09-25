#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// A colonist's pace, as the pane says it (design 17 §5a): <c>Pace 90% · in the rain</c> under
    /// the activity line, and a tooltip naming whatever is making it other than the standard walk.
    ///
    /// <para><b>The headline is the product of the factors the simulation publishes</b>, in the
    /// order and with the truncation <c>Pawn.MoveRatePerMille</c> uses, starting at 1,000 — so the
    /// number and the tooltip under it can never disagree. It equals the published move rate while
    /// the walk is tuned at one cost unit a tick, which <c>PaceAspectTests</c> pins; the day that
    /// changes, the base goes out beside the factors.</para>
    ///
    /// <para>"Pace", not "walk speed": the tile readout has said <i>walk speed</i> since cell
    /// inspection shipped, and it means the ground's crossing cost. Two meanings of one phrase on
    /// one pane is design 17 §4g's double-counting trap in the interface's clothes.</para>
    /// </summary>
    public static class PaceModel
    {
        // The names the simulation publishes the factors under (Odyssey.Sim.Pawns.RateAspects).
        // Literals rather than a shared constant, on the bargain JobLabels.CarryingAspect makes:
        // this assembly cannot reference Odyssey.Sim, and a test on each side holds its spelling.
        // All but the rolled pace are sparse, and absent means exactly 1,000.

        public const string RolledAspect = "odyssey.pawn.rate.move.rolled";
        public const string ConditionAspect = "odyssey.pawn.rate.move.condition";
        public const string WeatherAspect = "odyssey.pawn.rate.move.weather";
        public const string UrgencyAspect = "odyssey.pawn.rate.move.urgency";

        static readonly AspectKey RolledKey = AspectKey.Of(RolledAspect);
        static readonly AspectKey ConditionKey = AspectKey.Of(ConditionAspect);
        static readonly AspectKey WeatherKey = AspectKey.Of(WeatherAspect);
        static readonly AspectKey UrgencyKey = AspectKey.Of(UrgencyAspect);

        // The words, by registry key (docs/design/icon-keys.csv).
        public const string PaceLabel = "ui.stat.pace";
        public const string RolledLabel = "ui.stat.pace.rolled";
        public const string ConditionLabel = "ui.stat.pace.condition";
        public const string RainLabel = "ui.stat.pace.rain";
        public const string DraftedLabel = "ui.stat.pace.drafted";
        public const string RunningLabel = "ui.stat.pace.running";
        public const string StandardLabel = "ui.stat.pace.standard";
        public const string InTheRain = "ui.status.inrain";

        /// <summary>What the pace is made of, per mille, for one colonist on one frame.</summary>
        public struct Factors
        {
            /// <summary>False when the frame published no pace for this pawn: an animal, a bandit, a frame from before.</summary>
            public bool Published;
            public int Rolled, Condition, Weather, Urgency;

            /// <summary>Whether the run is the draft's, which names it; otherwise it is a fight's.</summary>
            public bool Drafted;

            public bool Equals(in Factors other) =>
                Published == other.Published && Rolled == other.Rolled && Condition == other.Condition
                && Weather == other.Weather && Urgency == other.Urgency && Drafted == other.Drafted;
        }

        /// <summary>Four O(1) lookups (design 31) and the drafted flag, for the one pawn on the pane.</summary>
        public static Factors Of(WorldSnapshot snapshot, PawnId id)
        {
            var f = new Factors
            {
                Published = snapshot.TryGetPawnAspect(id, RolledKey, out int rolled),
                Rolled = 1000, Condition = 1000, Weather = 1000, Urgency = 1000,
            };
            if (!f.Published) return f;
            f.Rolled = rolled;
            if (snapshot.TryGetPawnAspect(id, ConditionKey, out int condition)) f.Condition = condition;
            if (snapshot.TryGetPawnAspect(id, WeatherKey, out int weather)) f.Weather = weather;
            if (snapshot.TryGetPawnAspect(id, UrgencyKey, out int urgency)) f.Urgency = urgency;
            f.Drafted = OrderModel.IsDrafted(snapshot, id);
            return f;
        }

        /// <summary>
        /// The pace against the standard walk, per mille: the factors in the rate's own order —
        /// rolled, condition, (the species, 1,000 for a person), weather, urgency — each step
        /// truncated as the rate truncates it.
        /// </summary>
        public static int PerMille(in Factors f) =>
            1000 * f.Rolled / 1000 * f.Condition / 1000 * f.Weather / 1000 * f.Urgency / 1000;

        /// <summary>"Pace 90%", or "Pace 90% · in the rain" while the sky is what is slowing her.</summary>
        public static string Line(in Factors f)
        {
            string line = Registry.Label(PaceLabel) + " " + Percent(PerMille(f));
            return f.Weather < 1000 ? line + " · " + Lower(InTheRain) : line;
        }

        /// <summary>
        /// "rolled 104% · condition 80% · rain −10% · drafted ×2": every factor that is not exactly
        /// 1,000, in the rate's order, each in the form that reads naturally — who she is and how she
        /// is as a share of the standard walk, the rain as what it takes off, the run as a multiple.
        /// </summary>
        public static string Tooltip(in Factors f)
        {
            string tip = string.Empty;
            if (f.Rolled != 1000) tip = Join(tip, Lower(RolledLabel) + " " + Percent(f.Rolled));
            if (f.Condition != 1000) tip = Join(tip, Lower(ConditionLabel) + " " + Percent(f.Condition));
            if (f.Weather != 1000) tip = Join(tip, Lower(RainLabel) + " " + Signed(f.Weather - 1000));
            if (f.Urgency != 1000)
                tip = Join(tip, Lower(f.Drafted ? DraftedLabel : RunningLabel) + " " + Multiple(f.Urgency));
            return tip.Length == 0 ? Registry.Label(StandardLabel) : tip;
        }

        static string Join(string a, string b) => a.Length == 0 ? b : a + " · " + b;

        static string Lower(string key) => Registry.Label(key).ToLowerInvariant();

        /// <summary>Per mille as a whole percent, to the nearest: 904 is "90%", 1,042 is "104%".</summary>
        public static string Percent(int perMille) => Round(perMille) + "%";

        /// <summary>A change in per mille as a signed whole percent: −100 is "−10%".</summary>
        static string Signed(int perMille) =>
            perMille < 0 ? "−" + Round(-perMille) + "%" : "+" + Round(perMille) + "%";

        /// <summary>Per mille as a multiple: 2,000 is "×2", 1,500 is "×1.5".</summary>
        static string Multiple(int perMille) =>
            "×" + (perMille % 1000 == 0
                ? (perMille / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : (perMille / 1000.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

        static int Round(int perMille) => (perMille + 5) / 10;
    }
}
