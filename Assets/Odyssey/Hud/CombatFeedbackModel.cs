#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What the interface says about a fight (design 33 §1): whether a health bar is drawn over a
    /// pawn and how full, what a floating word says, in what ink and for how long, whether a pawn
    /// wears the hostile marker. Unity-free, so the fast tier owns the rules; presentation only
    /// draws the answers. <b>Lane C's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>The seam lane B draws through.</b> Lane B (the fight, drawn) owns the health bars,
    /// the floating text and the marker in the world and calls these to know what to draw; lane C
    /// owns the answers. The first four signatures were fixed by the contracts step;
    /// <see cref="FloatingSeconds"/> and <see cref="HealthBarColour"/> were added by lane C for the
    /// lifetime and the bar's ink, which the four could not say.</para>
    ///
    /// <para>The inputs are the snapshot's and nothing else: <see cref="PawnView.Flags"/>, the
    /// aspects in <see cref="CombatAspectNames"/>, and one <see cref="CombatEventView"/> at a time.
    /// Every word goes through <see cref="Registry"/>.</para>
    ///
    /// <para><b>What it scales with.</b> <see cref="HealthBar"/> and <see cref="HostileMarker"/>
    /// are asked once per drawn pawn per frame: two O(1) aspect lookups and a flag test, no
    /// allocation. The floating answers are asked once per new event, a few a second in a brawl,
    /// and the damage words come from a table built once, so none of them allocates either.</para>
    /// </summary>
    public static class CombatFeedbackModel
    {
        public const string MissKey = "ui.combat.miss";
        public const string DodgeKey = "ui.combat.dodge";

        /// <summary>The word over cover that took a bullet (design 53 §7e).</summary>
        public const string CoverKey = "ui.combat.cover";
        public const string StunnedKey = "ui.combat.stunned";

        /// <summary>A fling stopped short by a wall or a body (design 62 §7): the word, then the points it cost.</summary>
        public const string SlamKey = "ui.combat.slam";
        public const string DownedKey = "ui.status.downed";
        public const string DeadKey = "ui.combat.dead";

        /// <summary>
        /// Should a health bar stand over this pawn, and how full is it? The owner's rule is "over
        /// hurt and drafted pawns" (design 33 §1), and the simulation already says which: it
        /// publishes <c>odyssey.pawn.hp</c> while a pawn is hurt, downed or drafted and not
        /// otherwise (design 33 §5d), so <b>the presence of hit points is the rule</b>, not a
        /// second copy of it here. The pool is <c>odyssey.pawn.hp.max</c>; a frame with hit points
        /// and no pool beside it draws nothing rather than dividing by nought.
        ///
        /// <para><paramref name="hpMilli"/> is clamped to 0 to the pool: a downed pawn sits below
        /// nought until it dies at −50 %, and a bar is a fill, not a signed number.</para>
        /// </summary>
        public static bool HealthBar(WorldSnapshot snapshot, in PawnView pawn, out int hpMilli, out int hpMaxMilli)
        {
            hpMilli = 0;
            hpMaxMilli = 0;
            if (!snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.HpKey, out int hp)) return false;
            if (!snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.HpMaxKey, out int max) || max <= 0) return false;

            hpMaxMilli = max;
            hpMilli = hp < 0 ? 0 : hp > max ? max : hp;
            return true;
        }

        /// <summary>
        /// Should a hit-point bar stand over the building whose own cell is
        /// <paramref name="cellIndex"/>, and how full is it (design 33 §13i, §13k)? <b>Owed exactly
        /// where the simulation publishes a row</b> — a building somebody has struck and that still
        /// stands — as a pawn's is owed where it publishes hit points. Clamped to 0 to the pool;
        /// never without a pool. The drawing is owed (§13k); this is its answer.
        /// </summary>
        public static bool BuildingHealthBar(WorldSnapshot snapshot, int cellIndex, out int hpMilli, out int hpMaxMilli)
        {
            hpMilli = 0;
            hpMaxMilli = 0;
            if (!snapshot.TryGetEdificeDamage(cellIndex, out EdificeDamageView row) || row.MaxMilli <= 0) return false;
            hpMaxMilli = row.MaxMilli;
            hpMilli = row.HpMilli < 0 ? 0 : row.HpMilli > row.MaxMilli ? row.MaxMilli : row.HpMilli;
            return true;
        }

        /// <summary>
        /// The words that float up from one moment of a fight — "miss", "dodge", the damage in
        /// whole points, "stunned", "downed", "dead" — or empty for a moment that floats nothing
        /// (a swing starting, a pawn getting up).
        /// </summary>
        public static string FloatingText(in CombatEventView combatEvent) => combatEvent.Kind switch
        {
            CombatEventKind.Hit => Damage(combatEvent.Amount),
            CombatEventKind.Miss => Registry.Label(MissKey),
            CombatEventKind.Dodge => Registry.Label(DodgeKey),
            CombatEventKind.Covered => Registry.Label(CoverKey),
            CombatEventKind.Slam => Registry.Label(SlamKey) + " " + Damage(combatEvent.Amount),
            CombatEventKind.Stun => Registry.Label(StunnedKey),
            CombatEventKind.Downed => Registry.Label(DownedKey),
            CombatEventKind.Died => Registry.Label(DeadKey),
            _ => string.Empty,
        };

        /// <summary>
        /// The ink those words are drawn in. <b>Damage and the two ends of a fight are the bad
        /// red</b> — whoever took them, because a number over a bandit and over a colonist are
        /// read the same way, as a blow landing; a miss is dim, since nothing happened; a dodge is
        /// the information blue, since something was avoided; a stun is the warning amber.
        /// Transparent for a moment that floats nothing.
        /// </summary>
        public static HudColour FloatingColour(in CombatEventView combatEvent) => combatEvent.Kind switch
        {
            CombatEventKind.Hit => HudTheme.Bad,
            CombatEventKind.Miss => HudTheme.TextMeta,
            CombatEventKind.Dodge => HudTheme.Info,
            CombatEventKind.Covered => HudTheme.Info,
            CombatEventKind.Slam => HudTheme.Bad,
            CombatEventKind.Stun => HudTheme.Warn,
            CombatEventKind.Downed => HudTheme.Bad,
            CombatEventKind.Died => HudTheme.Bad,
            _ => default,
        };

        /// <summary>
        /// How long those words stand, in seconds of real time. A number is read at a glance and a
        /// brawl throws several a second, so it is brief; "downed" and "dead" end a fight and are
        /// the ones a player looking elsewhere most needs to catch, so they stay longest. Nought for
        /// a moment that floats nothing.
        /// </summary>
        public static float FloatingSeconds(in CombatEventView combatEvent) => combatEvent.Kind switch
        {
            CombatEventKind.Hit => 1.2f,
            CombatEventKind.Miss => 0.9f,
            CombatEventKind.Dodge => 0.9f,
            CombatEventKind.Covered => 0.9f,
            CombatEventKind.Slam => 1.4f,
            CombatEventKind.Stun => 1.4f,
            CombatEventKind.Downed => 2.2f,
            CombatEventKind.Died => 2.2f,
            _ => 0f,
        };

        /// <summary>
        /// Does this pawn wear the hostile marker (design 33 §1: a red marker)? A hostile, and
        /// nothing else — a wild animal fights back but is not an enemy, and a colonist who struck
        /// another is still ours. From the flags, never the kind (design 33 §6).
        /// </summary>
        public static bool HostileMarker(in PawnView pawn) => pawn.IsHostile;

        /// <summary>
        /// The bar's fill: green from 60 % left, amber from 40 %, red below. The thresholds are a
        /// need bar's (<c>HudTokens.NeedBand</c>, Presentation), so the bar over a colonist and the
        /// bars on her pane speak one scale — and the hues are the need bars' too, deepened
        /// (<see cref="HealthGood"/>). The one owner of the bar's colour: the bar over the head and
        /// the Health tab's fill both ask here.
        /// </summary>
        public static HudColour HealthBarColour(int hpMilli, int hpMaxMilli)
        {
            if (hpMaxMilli <= 0) return HealthBad;
            long perMille = (long)hpMilli * 1000 / hpMaxMilli;
            return perMille >= 600 ? HealthGood : perMille >= 400 ? HealthWarn : HealthBad;
        }

        // The owner, 2026-09-23 (design 33 §8a): "use a green like the one used in the colony
        // stats - more greener - deeper colours please". Each is its stat token's hue held to a
        // degree — HudTheme.Good 130.5°, Warn 38.1°, Bad 6.4° — with the saturation raised to
        // about 0.7 to 0.8 and the value lowered, because the bar is drawn over a lit, sunny board
        // through a translucent, glowing material that lifts every colour towards white, and the
        // stat tints, chosen for a dark panel, came out pale there. HealthBarLayoutTests holds the
        // hue, the extra saturation and the depth, so a retune stays a deeper stat colour.

        /// <summary>The bar's green: <see cref="HudTheme.Good"/>'s hue, saturation 0.72, value 0.70.</summary>
        public static readonly HudColour HealthGood = new HudColour(0x32, 0xb3, 0x49);

        /// <summary>The bar's amber: <see cref="HudTheme.Warn"/>'s hue, saturation 0.82, value 0.85.</summary>
        public static readonly HudColour HealthWarn = new HudColour(0xd9, 0x98, 0x27);

        /// <summary>The bar's red: <see cref="HudTheme.Bad"/>'s hue, saturation 0.80, value 0.80.</summary>
        public static readonly HudColour HealthBad = new HudColour(0xcc, 0x3a, 0x29);

        /// <summary>
        /// The dark plate behind the fill and the share lost: the panel's own ink, see-through
        /// enough that a figure behind the bar is still seen through it.
        /// </summary>
        public static readonly HudColour HealthBarPlate = HudTheme.PanelFill.WithAlpha(0.72f);

        /// <summary>The thin rim round the plate: darker than it, so the bar has an edge on dark rock and at night.</summary>
        public static readonly HudColour HealthBarOutline = new HudColour(0x02, 0x03, 0x04, 0.85f);

        /// <summary>
        /// Thousandths of a hit point to "-7": to the nearest whole point, and never "-0" — a blow
        /// that landed took something. Read from a table so a brawl allocates nothing; a blow past
        /// the table (none in the content does 200 points) is formatted on the spot.
        /// </summary>
        static string Damage(int milli)
        {
            int points = (milli + 500) / 1000;
            if (points < 1) points = 1;
            return points < DamageWords.Length ? DamageWords[points] : "-" + points;
        }

        static readonly string[] DamageWords = BuildDamageWords(200);

        static string[] BuildDamageWords(int count)
        {
            var words = new string[count];
            for (int i = 0; i < count; i++) words[i] = "-" + i;
            return words;
        }
    }
}
