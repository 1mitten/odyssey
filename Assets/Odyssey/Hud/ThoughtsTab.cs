#nullable enable
using System.Collections.Generic;
using System.Text;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The Thoughts tab's geometry (design 51 §10, mockup 23b): a mood meter and its breakdown on
    /// the left, the thoughts table on the right, the traits strip along the foot. Every number is
    /// the mockup's; the tab body is the tallest thing any tab needs, so this is what sets
    /// <see cref="HudLayout.InspectTabBody"/> for every tab, and switching tabs moves nothing.
    /// </summary>
    public static class ThoughtsLayout
    {
        /// <summary>The tab body, the mockup's <c>TAB_BODY_H</c>.</summary>
        public const int TabBody = 244;

        public const int LeftWidth = 168;
        public const int TraitsStrip = 40;
        public const int Pad = 12;
        public const int Gap = 9;

        /// <summary>The meter: a 10 px framed track, a 4 px fill 2 px down it, a 2 x 16 target tick.</summary>
        public const int MoodBar = 10, MoodFill = 4, MoodFillInset = 2, TickWidth = 2, TickHeight = 16;

        /// <summary>The mood line over the meter: the 19 px figure's line.</summary>
        public const int MoodLine = 24;

        /// <summary>The "Steady at 60" line.</summary>
        public const int TargetLine = 16;

        public const int BreakdownRow = 24, BreakdownRows = 4, BreakdownTop = 3;

        public const int Header = 24, Row = 30, Rail = 3;

        /// <summary>The table's two figure columns and the gap between the three.</summary>
        public const int LastsColumn = 52, MoodColumn = 36, ColumnGap = 9;

        /// <summary>Rows under the header; with more thoughts than this the last becomes the pager.</summary>
        public const int RowsPerPage = 5;

        public const int ChipHeight = 26, ChipPad = 9, ChipGap = 8;

        /// <summary>What the body leaves above the traits strip.</summary>
        public const int Upper = TabBody - TraitsStrip;

        /// <summary>
        /// The left column's content, top to bottom: padding, the mood line, the meter (its tick
        /// stands 3 px proud each side and is drawn over the gaps), the target line, the
        /// breakdown, padding; with the column's gap between each of the four.
        /// </summary>
        public const int LeftContent =
            Pad + MoodLine + Gap + MoodBar + Gap + TargetLine + Gap + BreakdownTop + BreakdownRows * BreakdownRow + Pad;

        /// <summary>The table: its header and the rows of one page.</summary>
        public const int TableContent = Header + RowsPerPage * Row;

        /// <summary>
        /// The width the traits strip has for chips: the pane's body inside its padding and
        /// border, less the strip's own padding, the TRAITS label and the gap after it.
        /// </summary>
        public static int ChipRoom =>
            HudLayout.InspectWidth - 2 * (HudLayout.Pad + HudTheme.BorderWidth) - 2 * Pad - TraitsLabel - Gap;

        /// <summary>
        /// "TRAITS" at 11/600 with its tracking, and the 3 px the mockup adds after it. An estimate —
        /// the model has no text engine — sized generously so a chip is folded into "+N more"
        /// before it can spill, never after.
        /// </summary>
        public const int TraitsLabel = 6 * 9 + 3;

        /// <summary>
        /// A chip's width, estimated: its padding and border, the name at 14/500 and the value in
        /// 12 mono with the gap between. Archivo Narrow averages under 7 px a character at 14 and
        /// Plex Mono is 7.2 at 12; both are rounded up, for the reason <see cref="TraitsLabel"/> is.
        /// </summary>
        public static int ChipWidth(string name, string value) =>
            2 * ChipPad + 2 * HudTheme.BorderWidth + name.Length * 8 + ChipGap + value.Length * 8;
    }

    /// <summary>One row of the thoughts table.</summary>
    public struct ThoughtLine
    {
        public string Name;

        /// <summary>Need, Condition or Memory, in the registry's words.</summary>
        public string Source;

        /// <summary>A memory's time left ("1 day"); empty for a need or a condition.</summary>
        public string Lasts;

        /// <summary>The signed points ("+10", "-6").</summary>
        public string Value;

        /// <summary>The value's colour and the rail's: good or bad.</summary>
        public HudColour Ink;

        public string Tip;

        /// <summary>Its worth in thousandths, signed: what the order and the breakdown read.</summary>
        public int Worth;

        public ThoughtSource Kind;
    }

    /// <summary>One row of the breakdown under the meter.</summary>
    public struct BreakdownLine
    {
        public string Label;
        public string Value;
        public HudColour Ink;
        public string Tip;

        /// <summary>Its worth in points, as drawn: the four sum to the target as drawn.</summary>
        public int Points;
    }

    /// <summary>A trait chip's tone: its mood effect's sign, or none.</summary>
    public enum ChipTone : byte { Neutral, Good, Bad }

    /// <summary>One chip of the traits strip.</summary>
    public struct TraitChip
    {
        public string Name;

        /// <summary>Its mood effect, signed, or "--" for none.</summary>
        public string Value;

        public ChipTone Tone;
        public string Tip;
    }

    /// <summary>
    /// The Thoughts tab's model (design 51 §10, mockup 23b), rebuilt from the simulation's published
    /// aspects: her mood and target, her base, her own minor and major lines, every need, condition
    /// and memory that is not nought, and her traits. <b>Every word, number and colour is decided
    /// here</b>, tested in the fast tier; <c>HudShell.Mind</c> only draws it.
    ///
    /// <para><b>The breakdown sums to the target, not the mood.</b> The target is what her needs,
    /// thoughts and traits add up to, and the mood drifts toward it; the line above the breakdown
    /// says where she is heading, so the arithmetic is the one the player can check. Rounding to
    /// points is absorbed by the Thoughts row, so the four drawn numbers add up to the drawn
    /// target exactly (a clamped target, at nought or a hundred, is the one exception and is left
    /// honest rather than made to add up).</para>
    ///
    /// <para><b>The colour of a mood is its lines'.</b> Good at or above her minor break line, warn
    /// between her major and minor lines, bad below the major (the mockup's 35 and 20, which are
    /// the untraited lines). The lines are hers, published, because a trait moves them. The Needs
    /// tab's mood bar asks <see cref="MoodInk"/> too, so the two tabs never disagree.</para>
    /// </summary>
    public sealed class ThoughtsTab
    {
        /// <summary>Mood is published in thousandths of this.</summary>
        public const int Scale = 1_000;

        // ---- the left column
        public int Mood, Target, Minor = -1, Major = -1;
        public string MoodText = string.Empty, TargetWord = string.Empty, TargetText = string.Empty;
        public HudColour MoodInk = HudTheme.TextPrimary;
        public string TargetTip = string.Empty;
        public readonly BreakdownLine[] Breakdown = new BreakdownLine[ThoughtsLayout.BreakdownRows];

        // ---- the table
        /// <summary>Every thought, in order: needs and conditions, then memories, each by size.</summary>
        public readonly List<ThoughtLine> Lines = new List<ThoughtLine>();

        /// <summary>The page on screen: at most <see cref="PageSize"/> of <see cref="Lines"/>.</summary>
        public readonly List<ThoughtLine> Shown = new List<ThoughtLine>();

        public int Page { get; private set; }
        public int Pages { get; private set; } = 1;
        public bool Paged => Pages > 1;
        public string PageText = "1 / 1";

        /// <summary>Rows a page shows: all five, or four and the pager.</summary>
        public int PageSize => Lines.Count > ThoughtsLayout.RowsPerPage ? ThoughtsLayout.RowsPerPage - 1 : ThoughtsLayout.RowsPerPage;

        // ---- the foot
        public readonly List<TraitChip> Chips = new List<TraitChip>();

        /// <summary>The room the chips have, in pixels. Settable so a test can fold them.</summary>
        public int ChipRoom = ThoughtsLayout.ChipRoom;

        /// <summary>Moves whenever anything drawn moved: the shell redraws on a change and never otherwise.</summary>
        public int Version { get; private set; }

        long _signature = long.MinValue;
        readonly List<ThoughtLine> _now = new List<ThoughtLine>();
        readonly List<ThoughtLine> _memories = new List<ThoughtLine>();

        /// <summary>The colour of a mood against her lines; the primary ink when the lines are unknown.</summary>
        public static HudColour InkFor(int mood, int minor, int major) =>
            minor < 0 ? HudTheme.TextPrimary
            : mood >= minor ? HudTheme.Good
            : mood >= major ? HudTheme.Warn
            : HudTheme.Bad;

        /// <summary>A new subject: the first page, and the next refresh rebuilds.</summary>
        public void Reset()
        {
            Page = 0;
            _signature = long.MinValue;
        }

        /// <summary>Show page <paramref name="page"/>, clamped. False when nothing moved.</summary>
        public bool SetPage(int page)
        {
            int clamped = page < 0 ? 0 : page >= Pages ? Pages - 1 : page;
            if (clamped == Page) return false;
            Page = clamped;
            Paginate();
            return true;
        }

        /// <summary>
        /// Rebuild from the frame, only when a drawn value moved: the signature is taken at the
        /// resolution the tab draws (points, hours and days), so a mood drifting a thousandth at a
        /// time rebuilds nothing.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, in PawnView pawn)
        {
            PawnId id = pawn.Id;
            int target = Read(snapshot, id, MindAspectNames.TargetKey, pawn.Mood);
            int baseMood = Read(snapshot, id, MindAspectNames.BaseKey, int.MinValue);
            int minor = Read(snapshot, id, MindAspectNames.MinorLineKey, -1);
            int major = Read(snapshot, id, MindAspectNames.MajorLineKey, -1);

            long signature = MindCatalogue.PointsOf(pawn.Mood);
            signature = signature * 31 + MindCatalogue.PointsOf(target);
            signature = signature * 31 + baseMood;
            signature = signature * 31 + minor;
            signature = signature * 31 + major;
            foreach (MindCatalogue.Source source in MindCatalogue.Situational)
                signature = signature * 31 + (snapshot.TryGetPawnAspect(id, source.Value, out int v) ? v : 0);
            foreach (MindCatalogue.Source source in MindCatalogue.Memories)
            {
                if (!snapshot.TryGetPawnAspect(id, source.Value, out int v)) continue;
                snapshot.TryGetPawnAspect(id, source.Left, out int left);
                snapshot.TryGetPawnAspect(id, source.Count, out int count);
                signature = signature * 31 + v;
                signature = signature * 31 + MindCatalogue.LeftOf(left);
                signature = signature * 31 + count;
            }
            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
                signature = signature * 31 + (snapshot.TryGetPawnAspect(id, MindAspectNames.TraitKey[slot], out int h) ? h + 1 : 0);
            signature = signature * 31 + ChipRoom;
            if (signature == _signature) return;
            _signature = signature;

            Mood = pawn.Mood;
            Target = target;
            Minor = minor;
            Major = major;
            BuildLeft(pawn.Mood, target, baseMood, minor, major, snapshot, id);
            BuildLines(snapshot, id);
            BuildChips(snapshot, id);
            Version++;
        }

        void BuildLeft(int mood, int target, int baseMood, int minor, int major, WorldSnapshot snapshot, PawnId id)
        {
            MoodInk = InkFor(mood, minor, major);
            MoodText = MindCatalogue.PointsOf(mood).ToString();
            int moodPoints = MindCatalogue.PointsOf(mood), targetPoints = MindCatalogue.PointsOf(target);
            string wordKey = targetPoints == moodPoints ? "ui.mind.steady"
                : targetPoints > moodPoints ? "ui.mind.rising" : "ui.mind.falling";
            TargetWord = Registry.Label(wordKey);
            TargetTip = Registry.Describe(wordKey);
            TargetText = targetPoints.ToString();

            // The four parts in thousandths, then in points as drawn.
            int needs = 0, conditions = 0, memories = 0, traits = 0;
            foreach (MindCatalogue.Source source in MindCatalogue.Situational)
            {
                if (!snapshot.TryGetPawnAspect(id, source.Value, out int v)) continue;
                if (source.Kind == ThoughtSource.Need) needs += v; else conditions += v;
            }
            foreach (MindCatalogue.Source source in MindCatalogue.Memories)
                if (snapshot.TryGetPawnAspect(id, source.Value, out int v)) memories += v;
            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
                if (snapshot.TryGetPawnAspect(id, MindAspectNames.TraitMoodKey[slot], out int v)) traits += v;

            // A frame without the base (one built by hand) takes it as what is left: exact while
            // the target is not clamped, which is every case such a frame has.
            if (baseMood == int.MinValue) baseMood = target - needs - conditions - memories - traits;

            int basePoints = MindCatalogue.PointsOf(baseMood);
            int needPoints = MindCatalogue.PointsOf(needs);
            int traitPoints = MindCatalogue.PointsOf(traits);
            bool clamped = baseMood + needs + conditions + memories + traits != target;
            int thoughtPoints = clamped
                ? MindCatalogue.PointsOf(conditions + memories)
                : targetPoints - basePoints - needPoints - traitPoints;

            Breakdown[0] = Part("ui.mind.base", basePoints, signed: false);
            Breakdown[1] = Part("ui.mind.thoughts", thoughtPoints, signed: true);
            Breakdown[2] = Part("ui.mind.traits", traitPoints, signed: true);
            Breakdown[3] = Part("ui.mind.needs", needPoints, signed: true);
        }

        static BreakdownLine Part(string key, int points, bool signed) => new BreakdownLine
        {
            Label = Registry.Label(key),
            Value = !signed ? points.ToString() : points > 0 ? "+" + points : points < 0 ? points.ToString() : "+0",
            Ink = !signed ? HudTheme.TextPrimary : points > 0 ? HudTheme.Good : points < 0 ? HudTheme.Bad : HudTheme.TextMeta,
            Tip = Registry.Describe(key),
            Points = points,
        };

        void BuildLines(WorldSnapshot snapshot, PawnId id)
        {
            _now.Clear();
            _memories.Clear();
            foreach (MindCatalogue.Source source in MindCatalogue.Situational)
            {
                if (!snapshot.TryGetPawnAspect(id, source.Value, out int v) || v == 0) continue;
                _now.Add(Line(source, v, string.Empty, 1));
            }
            foreach (MindCatalogue.Source source in MindCatalogue.Memories)
            {
                if (!snapshot.TryGetPawnAspect(id, source.Value, out int v) || v == 0) continue;
                snapshot.TryGetPawnAspect(id, source.Left, out int left);
                if (!snapshot.TryGetPawnAspect(id, source.Count, out int count)) count = 1;
                _memories.Add(Line(source, v, MindCatalogue.Lasts(left), count));
            }

            // Needs and conditions first, then memories; each by size, largest first. Sort is not
            // stable, so ties fall back to the catalogue's order through the name.
            _now.Sort(Larger);
            _memories.Sort(Larger);
            Lines.Clear();
            Lines.AddRange(_now);
            Lines.AddRange(_memories);

            int size = PageSize;
            Pages = Lines.Count == 0 ? 1 : (Lines.Count + size - 1) / size;
            if (Page >= Pages) Page = Pages - 1;
            Paginate();
        }

        static int Larger(ThoughtLine a, ThoughtLine b)
        {
            int bySize = System.Math.Abs(b.Worth).CompareTo(System.Math.Abs(a.Worth));
            return bySize != 0 ? bySize : string.CompareOrdinal(a.Name, b.Name);
        }

        static ThoughtLine Line(MindCatalogue.Source source, int worth, string lasts, int count)
        {
            string name = Registry.Label(source.Key);
            if (count > 1) name += " x" + count;
            return new ThoughtLine
            {
                Name = name,
                Source = Registry.Label(MindCatalogue.SourceKey(source.Kind)),
                Lasts = lasts,
                Value = MindCatalogue.Points(worth),
                Ink = worth > 0 ? HudTheme.Good : HudTheme.Bad,
                Tip = Registry.Describe(source.Key),
                Worth = worth,
                Kind = source.Kind,
            };
        }

        void Paginate()
        {
            Shown.Clear();
            int size = PageSize;
            for (int i = Page * size; i < Lines.Count && i < (Page + 1) * size; i++) Shown.Add(Lines[i]);
            PageText = (Page + 1) + " / " + Pages;
            Version++;
        }

        void BuildChips(WorldSnapshot snapshot, PawnId id)
        {
            Chips.Clear();
            int used = 0, count = 0;
            var rest = new StringBuilder();
            int total = 0;
            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
                if (snapshot.TryGetPawnAspect(id, MindAspectNames.TraitKey[slot], out _)) total++;

            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
            {
                if (!snapshot.TryGetPawnAspect(id, MindAspectNames.TraitKey[slot], out int handle)) continue;
                TraitChip chip = Chip(snapshot, id, slot, handle);
                int width = ThoughtsLayout.ChipWidth(chip.Name, chip.Value) + (count > 0 ? ThoughtsLayout.Gap : 0);

                // The chip fits, and so would a "+N more" after it if one is needed: keep it.
                int left = total - count - 1;
                int more = left > 0 ? ThoughtsLayout.Gap + MoreWidth(left) : 0;
                if (rest.Length == 0 && used + width + more <= ChipRoom)
                {
                    Chips.Add(chip);
                    used += width;
                    count++;
                    continue;
                }
                if (rest.Length > 0) rest.Append('\n');
                rest.Append(chip.Name);
            }

            if (rest.Length > 0)
                Chips.Add(new TraitChip
                {
                    Name = MoreName(total - count),
                    Value = string.Empty,
                    Tone = ChipTone.Neutral,
                    Tip = rest.ToString(),
                });
        }

        static string MoreName(int hidden) => "+" + hidden + " " + Registry.Label("ui.mind.more");

        static int MoreWidth(int hidden) => ThoughtsLayout.ChipWidth(MoreName(hidden), string.Empty) - ThoughtsLayout.ChipGap;

        static TraitChip Chip(WorldSnapshot snapshot, PawnId id, int slot, int handle)
        {
            string key = TraitSummary.Key(handle);
            int mood = Read(snapshot, id, MindAspectNames.TraitMoodKey[slot], 0);
            string effects = TraitSummary.Of(mood,
                Read(snapshot, id, MindAspectNames.TraitNerveKey[slot], 0),
                Read(snapshot, id, MindAspectNames.TraitLearnKey[slot], 1_000),
                Read(snapshot, id, MindAspectNames.TraitWorkKey[slot], 1_000),
                Read(snapshot, id, MindAspectNames.TraitCannotKey[slot], 0));
            string description = Registry.Describe(key);
            return new TraitChip
            {
                Name = Registry.Label(key),
                Value = mood == 0 ? "--" : MindCatalogue.Points(mood),
                Tone = mood > 0 ? ChipTone.Good : mood < 0 ? ChipTone.Bad : ChipTone.Neutral,
                Tip = effects.Length > 0 ? description + "\n" + effects : description,
            };
        }

        static int Read(WorldSnapshot snapshot, PawnId pawn, AspectKey key, int otherwise) =>
            snapshot.TryGetPawnAspect(pawn, key, out int value) ? value : otherwise;
    }
}
