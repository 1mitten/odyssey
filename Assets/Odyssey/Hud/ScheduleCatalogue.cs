#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// The names the simulation publishes a colonist's day under, minted on this side of the seam.
    ///
    /// <para>The same arrangement <see cref="SkillCatalogue"/> uses and for the same architectural
    /// reason: this assembly cannot reference <c>Odyssey.Sim</c> at all, so it spells the string
    /// itself and a Sim-side test holds the two spellings to each other. A shared constant would be
    /// the shared file the aspect seam exists to avoid.</para>
    /// </summary>
    public static class ScheduleKeys
    {
        /// <summary>Everything published about a pawn's day shares this prefix.</summary>
        public const string Prefix = "odyssey.pawn.schedule.";

        /// <summary>The full name of one hour, zero-padded so the keys sort as the day runs.</summary>
        public static string Name(int hour) => Prefix + "h" + hour.ToString("00");

        /// <summary>One key per hour, in the order the day runs.</summary>
        public static readonly AspectKey[] Hour = Mint();

        static AspectKey[] Mint()
        {
            var keys = new AspectKey[ScheduleHandle.Hours];
            for (int h = 0; h < keys.Length; h++) keys[h] = AspectKey.Of(Name(h));
            return keys;
        }
    }

    /// <summary>
    /// The six things an hour of a colonist's day can be, and the colour each is drawn in.
    ///
    /// <para><b>This is the HUD's first categorical colour scale, and that is why it exists as its
    /// own file.</b> Every other colour in this interface is a <i>signal</i> — good, bad, warn,
    /// accent, four tokens carrying meaning by intensity — and six nominal categories cannot be
    /// drawn from four signal tokens without two of them colliding. <see cref="HudCategory"/> is
    /// the nearest thing we already have and it is about icon strokes in stores and commands, not
    /// about filled bands. So the scale is new, it is small, and it is closed.</para>
    ///
    /// <para><b>The bands are the most saturated thing on the panel and nothing else may
    /// compete.</b> The work half's icons stay muted, the chrome stays near-black, and the only
    /// other saturated element is <see cref="HudTheme.Accent"/> on the now-line — which is why
    /// every block below is measured against the accent as well as against its neighbours.</para>
    ///
    /// <para><b>Distinctness is asserted, not assumed</b> (<c>ScheduleCatalogueTests</c>), because
    /// these sit edge to edge in an unbroken band rather than as separate chips, which is a harder
    /// test than two swatches in a legend. The first pass put <i>Anything</i> at 77 channel-points
    /// from <i>Sleep</i> — a flat grey and a dark indigo, the one pair a player actually has to
    /// tell apart at a glance in a night row. Both moved; the closest pair is now 96.</para>
    /// </summary>
    public static class ScheduleCatalogue
    {
        public readonly struct Entry
        {
            /// <summary>The registry key, which is also the legend's name.</summary>
            public readonly string Key;

            /// <summary>The <see cref="ScheduleHandle"/> the intent carries.</summary>
            public readonly int Handle;

            /// <summary>The band's fill. The whole signal: no text, no icon, no border.</summary>
            public readonly HudColour Colour;

            public Entry(string key, int handle, HudColour colour)
            {
                Key = key;
                Handle = handle;
                Colour = colour;
            }

            /// <summary>The name the legend draws, out of the naming registry.</summary>
            public string Label => Registry.Label(Key);
        }

        /// <summary>
        /// The blocks, in the order the cycle walks them and the legend lists them.
        ///
        /// <para><b>Anything is first and is handle zero</b>, so a colonist nobody has scheduled
        /// reads as "no instruction" rather than as an instruction somebody forgot to give.</para>
        /// </summary>
        public static readonly IReadOnlyList<Entry> All = new[]
        {
            // Neutral and deliberately the least interesting thing on the row: it is the absence
            // of a decision. Pushed off blue so it cannot be mistaken for Sleep in a night row.
            new Entry("ui.schedule.anything", ScheduleHandle.Anything, new HudColour(0x4e, 0x4e, 0x50)),

            // HudTheme.Warn, unchanged. Work is gold here and gold in the skill ramp's "skilled"
            // band, and that is a coincidence the two never share a surface over.
            new Entry("ui.schedule.work", ScheduleHandle.Work, HudTheme.Warn),

            // Deep blue, and saturated rather than dark: the night rows are a third of the table
            // and a near-black band would read as a hole in it.
            new Entry("ui.schedule.sleep", ScheduleHandle.Sleep, new HudColour(0x32, 0x3f, 0xa0)),

            new Entry("ui.schedule.recreation", ScheduleHandle.Recreation, new HudColour(0x4f, 0x9a, 0x63)),
            new Entry("ui.schedule.eat", ScheduleHandle.Eat, new HudColour(0xd9, 0x78, 0x2a)),
            new Entry("ui.schedule.meditate", ScheduleHandle.Meditate, new HudColour(0x7a, 0x4f, 0xa8)),
        };

        /// <summary>Every key the legend puts on screen, for <c>RegistryTests</c>.</summary>
        public static readonly string[] IconKeys = BuildKeys();

        static string[] BuildKeys()
        {
            var keys = new string[All.Count];
            for (int i = 0; i < All.Count; i++) keys[i] = All[i].Key;
            return keys;
        }

        /// <summary>The entry for a handle, or <i>Anything</i> for one this build does not know.</summary>
        public static Entry Of(int handle)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Handle == handle) return All[i];
            return All[0];
        }

        /// <summary>The colour an hour is filled with.</summary>
        public static HudColour ColourOf(int handle) => Of(handle).Colour;

        /// <summary>
        /// What one click makes an hour: the next block round the ring. Six states and a ring is
        /// the same idiom the priority cell uses, so one gesture works across the whole row.
        /// </summary>
        public static int Cycle(int handle)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Handle == handle) return All[(i + 1) % All.Count].Handle;
            return All[0].Handle;
        }

        /// <summary>The same ring walked backwards, for the right-click.</summary>
        public static int CycleBack(int handle)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Handle == handle) return All[(i - 1 + All.Count) % All.Count].Handle;
            return All[0].Handle;
        }

        /// <summary>
        /// The distance between two block colours, as the sum of the channel differences — the
        /// same crude measure <c>OrderColours</c> is held to, and crude on purpose: it is
        /// answering "could somebody confuse these", not "are these perceptually uniform".
        /// </summary>
        public static int Distance(in HudColour a, in HudColour b) =>
            System.Math.Abs(a.R - b.R) + System.Math.Abs(a.G - b.G) + System.Math.Abs(a.B - b.B);

        /// <summary>
        /// How far apart two bands must sit. Below this they are one colour to a player scanning
        /// a row of twenty-four.
        /// </summary>
        public const int MinimumDistance = 90;
    }
}
