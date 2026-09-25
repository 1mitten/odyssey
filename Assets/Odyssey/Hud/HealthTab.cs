#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>What a Health tab line wears at its end: nothing, a bleed, or a tend (design 43 §10).</summary>
    public enum HealthMark : byte
    {
        None = 0,
        Bleeding = 1,
        Tended = 2,
    }

    /// <summary>
    /// One line of the Health tab: the Skills tab's anatomy (icon, name, bar, figure, mark) so the
    /// two read as one hand. Every word and number is built here and tested in the fast tier; the
    /// shell only draws them.
    /// </summary>
    public struct HealthLine
    {
        /// <summary>A line with nothing on it, drawn as an empty slot so the grid never moves.</summary>
        public bool Empty;

        public string IconKey;
        public string Name;

        /// <summary>A short figure that fits the Skills tab's level column: a per cent, points, hours, "2/3".</summary>
        public string Value;

        /// <summary>The bar's fill, 0 to 1000, or -1 for a line with no bar.</summary>
        public int Bar;

        public HudColour Ink;
        public HealthMark Mark;

        /// <summary>The region this line is, or -1: what a click on it selects.</summary>
        public int Region;

        /// <summary>The selected region's line, drawn in the accent.</summary>
        public bool Selected;

        /// <summary>The whole reading, for the hover: "Left leg — 60 per cent left".</summary>
        public string Tip;
    }

    /// <summary>
    /// The Health tab (design 43 §10): two columns of seven on the Skills tab's own grid, so the tab
    /// costs no layout constant and moves no other tab. The left column is the six regions and pain;
    /// the right is the pool, the three capacities, the blood lost, the bleed and the tend. A region
    /// clicked swaps the right column for that region's injuries — at most three, one per kind,
    /// because the simulation merges them (design 43 §2).
    ///
    /// <para><b>Reads only sparse aspects</b>: a colonist nobody has hurt publishes none, and every
    /// line then reads whole. Rebuilds its strings only when a number it quotes moved, so a
    /// selected colonist fifteen times a second allocates nothing.</para>
    /// </summary>
    public sealed class HealthTab
    {
        public const int Rows = 7;

        public readonly HealthLine[] Left = new HealthLine[Rows];
        public readonly HealthLine[] Right = new HealthLine[Rows];

        /// <summary>The region clicked, or -1.</summary>
        public int SelectedRegion { get; private set; } = -1;

        /// <summary>The keys the six regions are named by, in the simulation's index order.</summary>
        public static readonly string[] RegionKeys =
        {
            "ui.health.head", "ui.health.torso",
            "ui.health.arm.left", "ui.health.arm.right",
            "ui.health.leg.left", "ui.health.leg.right",
        };

        /// <summary>The icon each region wears: the part's own art, the side said by the name.</summary>
        public static readonly string[] RegionIcons =
        {
            "ui.health.head", "ui.health.torso",
            "ui.health.arm", "ui.health.arm",
            "ui.health.leg", "ui.health.leg",
        };

        /// <summary>The kinds of injury, in the simulation's order: wound, bruise, fracture.</summary>
        public static readonly string[] KindKeys = { "ui.health.wound", "ui.health.bruise", "ui.health.fracture" };

        public const string PainKey = "ui.health.pain", ConsciousnessKey = "ui.health.consciousness",
            MovingKey = "ui.health.moving", ManipulationKey = "ui.health.manipulation",
            BloodKey = "ui.health.blood", BleedingKey = "ui.status.bleeding", TendedKey = "ui.health.tended",
            HealthKey = "ui.combat.health", ToDeathKey = "ui.health.todeath";

        // Everything a refresh reads, so the strings are rebuilt only when one of them moves.
        readonly int[] _read = new int[Inputs], _shown = new int[Inputs];
        const int Inputs = 12 + HealthAspectNames.Regions + 2 * HealthAspectNames.Regions * HealthAspectNames.Kinds;
        bool _built;

        /// <summary>Click a region: select it, or put it back if it was already selected. -1 clears.</summary>
        public void Select(int region)
        {
            SelectedRegion = region < 0 || region == SelectedRegion ? -1 : region;
            _built = false;
        }

        /// <summary>A new subject on the pane: nothing selected, everything rebuilt.</summary>
        public void Reset()
        {
            SelectedRegion = -1;
            _built = false;
        }

        /// <summary>
        /// Fill both columns from the frame. <paramref name="hp"/> and <paramref name="max"/> are the
        /// pool the pane already read (thousandths; <paramref name="max"/> nought when none is
        /// published).
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, PawnId id, int hp, int max)
        {
            int n = 0;
            _read[n++] = hp;
            _read[n++] = max;
            _read[n++] = SelectedRegion;
            _read[n++] = Read(snapshot, id, HealthAspectNames.PainKey, 0);
            _read[n++] = Read(snapshot, id, HealthAspectNames.ConsciousnessKey, 1000);
            _read[n++] = Read(snapshot, id, HealthAspectNames.MovingKey, 1000);
            _read[n++] = Read(snapshot, id, HealthAspectNames.ManipulationKey, 1000);
            _read[n++] = Read(snapshot, id, HealthAspectNames.BloodKey, 0);
            _read[n++] = Read(snapshot, id, HealthAspectNames.BleedHoursKey, 0);
            _read[n++] = Read(snapshot, id, HealthAspectNames.InjuriesKey, 0);
            _read[n++] = Read(snapshot, id, HealthAspectNames.TendedKey, 0);
            _read[n++] = 0;
            for (int r = 0; r < HealthAspectNames.Regions; r++)
                _read[n++] = Read(snapshot, id, HealthAspectNames.RegionKeys[r], 1000);
            for (int s = 0; s < HealthAspectNames.Regions * HealthAspectNames.Kinds; s++)
            {
                _read[n++] = Read(snapshot, id, HealthAspectNames.InjuryKeys[s], 0);
                _read[n++] = Read(snapshot, id, HealthAspectNames.CareKeys[s], 0);
            }

            if (_built && Same()) return;
            System.Array.Copy(_read, _shown, Inputs);
            _built = true;
            Build();
        }

        bool Same()
        {
            for (int i = 0; i < Inputs; i++) if (_read[i] != _shown[i]) return false;
            return true;
        }

        static int Read(WorldSnapshot snapshot, PawnId id, AspectKey key, int whole) =>
            snapshot.TryGetPawnAspect(id, key, out int value) ? value : whole;

        int Points(int region, int kind) => _read[12 + HealthAspectNames.Regions + 2 * (region * HealthAspectNames.Kinds + kind)];
        int Care(int region, int kind) => _read[12 + HealthAspectNames.Regions + 2 * (region * HealthAspectNames.Kinds + kind) + 1];
        int RegionLeft(int region) => _read[12 + region];

        void Build()
        {
            int hp = _read[0], max = _read[1];
            int pain = _read[3], consciousness = _read[4], moving = _read[5], manipulation = _read[6];
            int blood = _read[7], hours = _read[8], injuries = _read[9], tended = _read[10];

            for (int r = 0; r < HealthAspectNames.Regions; r++)
            {
                int left = RegionLeft(r);
                Left[r] = new HealthLine
                {
                    IconKey = RegionIcons[r],
                    Name = Registry.Label(RegionKeys[r]),
                    Value = Percent(left),
                    Bar = Clamp(left),
                    Ink = Remaining(left),
                    Mark = RegionMark(r),
                    Region = r,
                    Selected = r == SelectedRegion,
                    Tip = Registry.Label(RegionKeys[r]) + " — " + Percent(left) + " per cent left",
                };
            }
            Left[6] = new HealthLine
            {
                IconKey = PainKey,
                Name = Registry.Label(PainKey),
                Value = Percent(pain),
                Bar = Clamp(pain),
                Ink = Lost(pain, 300, 600),
                Region = -1,
                Tip = Registry.Label(PainKey) + " — " + Percent(pain) + " per cent",
            };

            if (SelectedRegion >= 0)
            {
                BuildInjuries(SelectedRegion);
                return;
            }

            int shown = hp < 0 ? 0 : hp > max ? max : hp;
            int pool = max > 0 ? (int)((long)shown * 1000 / max) : 0;
            Right[0] = new HealthLine
            {
                IconKey = HealthKey,
                Name = Registry.Label(HealthKey),
                Value = max > 0 ? Whole(hp).ToString() : string.Empty,
                Bar = max > 0 ? pool : 0,
                Ink = CombatFeedbackModel.HealthBarColour(shown, max),
                Region = -1,
                Tip = max > 0 ? Registry.Label(HealthKey) + " — " + Whole(hp) + " / " + Whole(max) : Registry.Label(HealthKey),
            };
            Right[1] = Capacity(ConsciousnessKey, consciousness);
            Right[2] = Capacity(MovingKey, moving);
            Right[3] = Capacity(ManipulationKey, manipulation);
            Right[4] = new HealthLine
            {
                IconKey = BloodKey,
                Name = Registry.Label(BloodKey),
                Value = Percent(blood),
                Bar = Clamp(blood),
                Ink = Lost(blood, 150, 450),
                Region = -1,
                Tip = Registry.Label(BloodKey) + " — " + Percent(blood) + " per cent",
            };
            Right[5] = new HealthLine
            {
                IconKey = BleedingKey,
                Name = Registry.Label(BleedingKey),
                Value = hours > 0 ? (hours > 99 ? ">99" : hours + "h") : string.Empty,
                Bar = -1,
                Ink = CombatFeedbackModel.HealthBad,
                Mark = hours > 0 ? HealthMark.Bleeding : HealthMark.None,
                Region = -1,
                Tip = hours > 0
                    ? Registry.Label(BleedingKey) + " — " + hours + " h " + Registry.Label(ToDeathKey) + ", until somebody tends it"
                    : Registry.Label(BleedingKey),
            };
            Right[6] = new HealthLine
            {
                IconKey = TendedKey,
                Name = Registry.Label(TendedKey),
                Value = injuries > 0 ? tended + "/" + injuries : string.Empty,
                Bar = -1,
                Ink = CombatFeedbackModel.HealthGood,
                Mark = injuries > 0 && tended == injuries ? HealthMark.Tended : HealthMark.None,
                Region = -1,
                Tip = Registry.Label(TendedKey) + (injuries > 0 ? " — " + tended + " of " + injuries : string.Empty),
            };
        }

        void BuildInjuries(int region)
        {
            int row = 0;
            for (int k = 0; k < HealthAspectNames.Kinds && row < Rows; k++)
            {
                int points = Points(region, k);
                if (points <= 0) continue;
                int care = Care(region, k);
                bool isTended = care > 0;
                bool bleeding = k == 0 && !isTended;
                string name = Registry.Label(KindKeys[k]);
                Right[row++] = new HealthLine
                {
                    IconKey = KindKeys[k],
                    Name = name,
                    Value = Whole(points).ToString(),
                    Bar = -1,
                    Ink = bleeding ? CombatFeedbackModel.HealthBad : CombatFeedbackModel.HealthWarn,
                    Mark = bleeding ? HealthMark.Bleeding : isTended ? HealthMark.Tended : HealthMark.None,
                    Region = -1,
                    Tip = name + " — " + Whole(points) + " points" + (isTended
                        ? ", " + Registry.Label(TendedKey).ToLowerInvariant() + " at " + Percent(care - 1) + " per cent"
                        : bleeding ? ", " + Registry.Label(BleedingKey).ToLowerInvariant() : string.Empty),
                };
            }
            while (row < Rows) Right[row++] = new HealthLine { Empty = true, Region = -1, Name = string.Empty, Value = string.Empty, IconKey = string.Empty, Tip = string.Empty, Bar = -1 };
        }

        HealthLine Capacity(string key, int perMille) => new HealthLine
        {
            IconKey = key,
            Name = Registry.Label(key),
            Value = Percent(perMille),
            Bar = Clamp(perMille),
            Ink = Remaining(perMille),
            Region = -1,
            Tip = Registry.Label(key) + " — " + Percent(perMille) + " per cent",
        };

        /// <summary>A bleed on the region outranks a tend; a tend is shown only when every injury there is tended.</summary>
        HealthMark RegionMark(int region)
        {
            bool any = false, allTended = true;
            for (int k = 0; k < HealthAspectNames.Kinds; k++)
            {
                if (Points(region, k) <= 0) continue;
                any = true;
                bool isTended = Care(region, k) > 0;
                if (k == 0 && !isTended) return HealthMark.Bleeding;
                allTended &= isTended;
            }
            return any && allTended ? HealthMark.Tended : HealthMark.None;
        }

        /// <summary>A bar of what is left: the need bars' bands, the health bar's deeper inks.</summary>
        static HudColour Remaining(int perMille) => CombatFeedbackModel.HealthBarColour(perMille, 1000);

        /// <summary>A bar of what is lost: good below the first line, warn below the second, bad past it.</summary>
        static HudColour Lost(int perMille, int warnAt, int badAt) =>
            perMille >= badAt ? CombatFeedbackModel.HealthBad
            : perMille >= warnAt ? CombatFeedbackModel.HealthWarn
            : CombatFeedbackModel.HealthGood;

        static int Clamp(int perMille) => perMille < 0 ? 0 : perMille > 1000 ? 1000 : perMille;

        /// <summary>Per mille to a whole per cent, rounded half up, from a table so no string is built.</summary>
        static string Percent(int perMille)
        {
            int p = (Clamp(perMille) + 5) / 10;
            return PercentText[p];
        }

        static readonly string[] PercentText = BuildPercents();

        static string[] BuildPercents()
        {
            var texts = new string[101];
            for (int i = 0; i <= 100; i++) texts[i] = i.ToString();
            return texts;
        }

        /// <summary>Thousandths to whole points, up to the next one; nought below it.</summary>
        static int Whole(int milli) => milli <= 0 ? 0 : (milli + 999) / 1000;
    }
}
