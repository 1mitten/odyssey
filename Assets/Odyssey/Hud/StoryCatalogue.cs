#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// The storytellers and difficulty rungs the interface offers (design 59, Claude Design's
    /// mockups 25a–25h).
    ///
    /// <para><b>Interface content.</b> The names, blurbs and portraits are the registry's
    /// (<c>ui.storyteller.*</c>, <c>ui.difficulty.*</c>); the emblems and the rhythm strips are
    /// here because they are drawings, not words. The rung values are design 59 §6's and are
    /// written <b>only here</b>: a press sends the four lever values themselves
    /// (<see cref="StoryDirector.DifficultyIntent"/>), so the simulation stores numbers and never
    /// needs a copy of the ladder. The storytellers' pacing is the simulation's Defs
    /// (<c>Storytellers.xml</c>), in <c>StorytellerHandle</c>'s order.</para>
    ///
    /// <para>Unity-free (ADR 0003), so all of it runs in the fast tier.</para>
    /// </summary>
    public static class StoryCatalogue
    {
        /// <summary>One threat drawn on a rhythm strip: the day of a 24-day season, and how tall
        /// its mark stands above the baseline in strip units.</summary>
        public readonly struct Mark
        {
            public readonly int Day;
            public readonly int Size;

            public Mark(int day, int size)
            {
                Day = day;
                Size = size;
            }
        }

        /// <summary>A storyteller as the interface draws it.</summary>
        public sealed class Teller
        {
            public readonly string Key;
            public readonly string PortraitKey;

            /// <summary>The placeholder emblem, a stroked path on a 24 x 24 box, drawn until the
            /// commissioned portrait arrives. Looked up beside <see cref="PortraitKey"/> so the
            /// art can replace it with no layout change.</summary>
            public readonly string Emblem;

            /// <summary>An illustrative season under this storyteller, never this colony's own.</summary>
            public readonly Mark[] Marks;

            public Teller(string key, string portraitKey, string emblem, Mark[] marks)
            {
                Key = key;
                PortraitKey = portraitKey;
                Emblem = emblem;
                Marks = marks;
            }

            public string Label => Registry.Label(Key);

            /// <summary>The card's blurb: the registry's description, so the wiki and the card
            /// can never say two things.</summary>
            public string Blurb => Registry.Describe(Key);
        }

        public const string StorytellerHeadingKey = "ui.storyteller.heading";
        public const string DifficultyHeadingKey = "ui.difficulty.heading";
        public const string ThreatScaleKey = "ui.difficulty.threatscale";
        public const string BigThreatsKey = "ui.difficulty.bigthreats";
        public const string AdaptationKey = "ui.difficulty.adaptation";
        public const string GraceKey = "ui.difficulty.grace";
        public const string SetByKey = "ui.difficulty.setby";
        public const string OwnKey = "ui.difficulty.own";

        /// <summary>The three, in the order the owner gave them: Jacob, Trent, Kano.</summary>
        public static readonly IReadOnlyList<Teller> Tellers = new[]
        {
            new Teller("ui.storyteller.jacob", "ui.storyteller.portrait.jacob",
                "M3 17h4v-6h4v6h4v-6h4v6h2",
                new[] { new Mark(3, 10), new Mark(10, 13), new Mark(12, 15), new Mark(20, 19) }),
            new Teller("ui.storyteller.trent", "ui.storyteller.portrait.trent",
                "M3 15l4-7 3 9 3-12 3 9 2-4 3 5",
                new[] { new Mark(2, 9), new Mark(3, 22), new Mark(11, 12), new Mark(22, 27) }),
            new Teller("ui.storyteller.kano", "ui.storyteller.portrait.kano",
                "M2 17h20M7.5 17a4.5 4.5 0 0 1 9 0",
                new[] { new Mark(14, 30) }),
        };

        /// <summary>Jacob: the rhythm, and the owner's default.</summary>
        public const int DefaultTeller = 0;

        public static Teller TellerAt(int index) =>
            Tellers[index < 0 || index >= Tellers.Count ? DefaultTeller : index];

        // ------------------------------------------------------------------ difficulty

        /// <summary>One rung of the ladder and the four levers it sets.</summary>
        public readonly struct Rung
        {
            public readonly string Key;
            public readonly int ThreatPercent;
            public readonly bool BigThreats;
            public readonly int AdaptationPercent;

            /// <summary>The grace stretch in hundredths: 100 is the storyteller's own.</summary>
            public readonly int GraceHundredths;

            public Rung(string key, int threat, bool bigThreats, int adaptation, int grace)
            {
                Key = key;
                ThreatPercent = threat;
                BigThreats = bigThreats;
                AdaptationPercent = adaptation;
                GraceHundredths = grace;
            }

            public string Label => Registry.Label(Key);
        }

        /// <summary>Design 59 §6. Custom's values are only where it starts; the player moves them.</summary>
        public static readonly IReadOnlyList<Rung> Rungs = new[]
        {
            new Rung("ui.difficulty.peaceful", 10, false, 0, 100),
            new Rung("ui.difficulty.gentle", 30, true, 150, 150),
            new Rung("ui.difficulty.easy", 60, true, 125, 125),
            new Rung("ui.difficulty.normal", 100, true, 100, 100),
            new Rung("ui.difficulty.hard", 150, true, 75, 85),
            new Rung("ui.difficulty.brutal", 220, true, 50, 70),
            new Rung("ui.difficulty.custom", 100, true, 100, 100),
        };

        public const int NormalRung = 3;
        public const int CustomRung = 6;

        public const int ThreatMin = 0, ThreatMax = 500, ThreatStep = 10;
        public const int AdaptationMin = 0, AdaptationMax = 200, AdaptationStep = 10;
        public const int GraceMin = 50, GraceMax = 200, GraceStep = 5;

        public static Rung RungAt(int index) =>
            Rungs[index < 0 || index >= Rungs.Count ? NormalRung : index];

        /// <summary>"250%": a percentage lever as its figure.</summary>
        public static string Percent(int percent) => percent + "%";

        /// <summary>"x1.25", "x0.5", "x2": the grace stretch as its figure, trailing zeros gone.</summary>
        public static string Stretch(int hundredths)
        {
            int whole = hundredths / 100, part = hundredths % 100;
            if (part == 0) return "x" + whole;
            return part % 10 == 0 ? "x" + whole + "." + part / 10 : "x" + whole + "." + part.ToString("00");
        }

        /// <summary>"Set by Hard. Pick Custom to change." or "Your own settings".</summary>
        public static string CustomNote(int rung) =>
            rung == CustomRung
                ? Registry.Label(OwnKey)
                : Registry.Label(SetByKey).Replace("{rung}", RungAt(rung).Label);

        // ------------------------------------------------------------------ the rhythm strip

        /// <summary>The strip's box, 190 x 40 in strip units (25h).</summary>
        public const float StripWidth = 190f, StripHeight = 40f;

        /// <summary>The dashed baseline, <c>M4 34H186</c>.</summary>
        public const float BaselineY = 34f, BaselineStart = 4f, BaselineEnd = 186f;

        /// <summary>Where a threat on <paramref name="day"/> of a 24-day season stands: 6 + day / 24 x 176.</summary>
        public static float MarkX(int day) => 6f + day / 24f * 176f;

        /// <summary>Every key the catalogue names, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new List<string>
            {
                StorytellerHeadingKey, DifficultyHeadingKey, ThreatScaleKey, BigThreatsKey,
                AdaptationKey, GraceKey, SetByKey, OwnKey,
            };
            foreach (Teller t in Tellers)
            {
                keys.Add(t.Key);
                keys.Add(t.PortraitKey);
            }
            foreach (Rung r in Rungs) keys.Add(r.Key);
            return keys.ToArray();
        }
    }

    /// <summary>
    /// Everything the player has chosen about the story: who tells it and how hard (design 59 §7).
    ///
    /// <para><b>A value, so the New game page and the Settings rows share one rule</b> for what a
    /// press does. Picking a rung other than Custom sets all four levers to that rung's values;
    /// picking Custom keeps whatever the levers read, so Custom starts from the rung the player
    /// was looking at; and a lever moves only while Custom is picked — which is what the dimmed
    /// block on the page says.</para>
    /// </summary>
    public readonly struct StoryChoice : IEquatable<StoryChoice>
    {
        /// <summary>An index into <see cref="StoryCatalogue.Tellers"/>, or <see cref="None"/>.</summary>
        public readonly int Teller;
        public readonly int Rung;
        public readonly int ThreatPercent;
        public readonly bool BigThreats;
        public readonly int AdaptationPercent;
        public readonly int GraceHundredths;

        /// <summary>No storyteller: what a loaded save has until the storyteller is saved (design
        /// 59 §7, the old-save rule).</summary>
        public const int None = -1;

        public StoryChoice(int teller, int rung, int threat, bool bigThreats, int adaptation, int grace)
        {
            Teller = teller;
            Rung = rung;
            ThreatPercent = threat;
            BigThreats = bigThreats;
            AdaptationPercent = adaptation;
            GraceHundredths = grace;
        }

        /// <summary>Jacob at Normal: the owner's defaults.</summary>
        public static StoryChoice Default => AtRung(StoryCatalogue.DefaultTeller, StoryCatalogue.NormalRung);

        /// <summary>No storyteller, and Normal's levers behind it.</summary>
        public static StoryChoice Nobody => AtRung(None, StoryCatalogue.NormalRung);

        static StoryChoice AtRung(int teller, int rung)
        {
            StoryCatalogue.Rung r = StoryCatalogue.RungAt(rung);
            return new StoryChoice(teller, rung, r.ThreatPercent, r.BigThreats, r.AdaptationPercent, r.GraceHundredths);
        }

        public bool HasTeller => Teller >= 0 && Teller < StoryCatalogue.Tellers.Count;
        public bool IsCustom => Rung == StoryCatalogue.CustomRung;

        public StoryChoice WithTeller(int teller) =>
            teller < 0 || teller >= StoryCatalogue.Tellers.Count
                ? this
                : new StoryChoice(teller, Rung, ThreatPercent, BigThreats, AdaptationPercent, GraceHundredths);

        public StoryChoice WithRung(int rung)
        {
            if (rung < 0 || rung >= StoryCatalogue.Rungs.Count) return this;
            if (rung == StoryCatalogue.CustomRung)
                return new StoryChoice(Teller, rung, ThreatPercent, BigThreats, AdaptationPercent, GraceHundredths);
            StoryCatalogue.Rung r = StoryCatalogue.Rungs[rung];
            return new StoryChoice(Teller, rung, r.ThreatPercent, r.BigThreats, r.AdaptationPercent, r.GraceHundredths);
        }

        public StoryChoice WithThreat(int percent) => !IsCustom ? this
            : new StoryChoice(Teller, Rung, Snap(percent, StoryCatalogue.ThreatMin, StoryCatalogue.ThreatMax, StoryCatalogue.ThreatStep),
                BigThreats, AdaptationPercent, GraceHundredths);

        public StoryChoice WithBigThreats(bool on) => !IsCustom ? this
            : new StoryChoice(Teller, Rung, ThreatPercent, on, AdaptationPercent, GraceHundredths);

        public StoryChoice WithAdaptation(int percent) => !IsCustom ? this
            : new StoryChoice(Teller, Rung, ThreatPercent, BigThreats,
                Snap(percent, StoryCatalogue.AdaptationMin, StoryCatalogue.AdaptationMax, StoryCatalogue.AdaptationStep),
                GraceHundredths);

        public StoryChoice WithGrace(int hundredths) => !IsCustom ? this
            : new StoryChoice(Teller, Rung, ThreatPercent, BigThreats, AdaptationPercent,
                Snap(hundredths, StoryCatalogue.GraceMin, StoryCatalogue.GraceMax, StoryCatalogue.GraceStep));

        static int Snap(int value, int min, int max, int step)
        {
            int clamped = Math.Max(min, Math.Min(max, value));
            return min + (clamped - min + step / 2) / step * step;
        }

        public bool Equals(StoryChoice other) =>
            Teller == other.Teller && Rung == other.Rung && ThreatPercent == other.ThreatPercent
            && BigThreats == other.BigThreats && AdaptationPercent == other.AdaptationPercent
            && GraceHundredths == other.GraceHundredths;

        public override bool Equals(object? obj) => obj is StoryChoice other && Equals(other);

        public override int GetHashCode() =>
            ((Teller * 31 + Rung) * 31 + ThreatPercent) * 31 + AdaptationPercent * 7 + GraceHundredths + (BigThreats ? 1 : 0);
    }
}
