#nullable enable

namespace Odyssey.Hud
{
    /// <summary>The five steps of the proficiency ramp a cell's border is drawn in.</summary>
    public enum ProficiencyBand
    {
        /// <summary>There is no skill behind this column at all. Hauling, and only hauling.</summary>
        None = -1,

        Novice = 0,      // 0–3
        Apprentice = 1,  // 4–6
        Competent = 2,   // 7–10
        Skilled = 3,     // 11–14
        Master = 4,      // 15–20
    }

    /// <summary>
    /// What a Work-grid cell is coloured by: the skill in its border, and the priority in its ink.
    ///
    /// <para><b>The border is the only carrier of skill, and the fill is the only carrier of
    /// priority.</b> Tinting the fill by skill as well puts two scales in one 28px square and the
    /// digit loses; the supplied mockup says so and it is right.</para>
    ///
    /// <para><b>The ramp is built out of our own two accents</b> rather than the mockup's five
    /// invented hues, so three of the five steps are tokens the HUD already ships:
    /// <see cref="HudTheme.Bad"/> darkened at the bottom, <see cref="HudTheme.TextPrimary"/>
    /// dimmed in the neutral middle, <see cref="HudTheme.Warn"/> unchanged at <i>skilled</i>, and
    /// the same amber brightened at the top. Measured against the assigned fill in
    /// <c>27-work-tab.md</c> §9; the weakest is the novice red at 3.79:1, which clears the 3:1 a
    /// two-pixel border owes.</para>
    /// </summary>
    public static class WorkBands
    {
        /// <summary>Which band a skill level falls in. Levels run 0 to 20.</summary>
        public static ProficiencyBand BandOf(int level) =>
            level <= 3 ? ProficiencyBand.Novice
            : level <= 6 ? ProficiencyBand.Apprentice
            : level <= 10 ? ProficiencyBand.Competent
            : level <= 14 ? ProficiencyBand.Skilled
            : ProficiencyBand.Master;

        /// <summary><see cref="HudTheme.Bad"/> darkened — the dull red end.</summary>
        public static readonly HudColour Novice = new HudColour(0xa8, 0x52, 0x4a);

        /// <summary>The muddy midpoint between Bad and Warn.</summary>
        public static readonly HudColour Apprentice = new HudColour(0x8f, 0x7a, 0x5e);

        /// <summary><see cref="HudTheme.TextPrimary"/> dimmed — the step with no opinion.</summary>
        public static readonly HudColour Competent = new HudColour(0xc7, 0xd0, 0xd6);

        /// <summary><see cref="HudTheme.Warn"/>, unchanged.</summary>
        public static readonly HudColour Skilled = HudTheme.Warn;

        /// <summary>Warn brightened — the one colour on this panel allowed to shout.</summary>
        public static readonly HudColour Master = new HudColour(0xf5, 0xc9, 0x4a);

        /// <summary>
        /// The border of a column with no skill behind it. Drawn at one pixel rather than two, so
        /// that a hauling cell is visibly *not on the ramp* rather than sitting at some invented
        /// point along it. A ramp colour invented for hauling would be a lie told in a colour the
        /// player has learned to trust.
        /// </summary>
        public static readonly HudColour NoSkill = HudTheme.TextFaint;

        public const int BorderWidth = 2;
        public const int NoSkillBorderWidth = 1;

        /// <summary>The border colour for a band.</summary>
        public static HudColour ColourOf(ProficiencyBand band) => band switch
        {
            ProficiencyBand.Novice => Novice,
            ProficiencyBand.Apprentice => Apprentice,
            ProficiencyBand.Competent => Competent,
            ProficiencyBand.Skilled => Skilled,
            ProficiencyBand.Master => Master,
            _ => NoSkill,
        };

        // ------------------------------------------------------------------ priority

        /// <summary>
        /// The digit's ink, brightness descending with importance. Measured against the assigned
        /// fill: 17.95, 14.57, 9.06 and <b>5.58</b>:1 — the four is the one to check and it clears
        /// 4.5.
        /// </summary>
        public static HudColour InkOf(int priority) => priority switch
        {
            1 => HudTheme.TextPrimary,
            2 => new HudColour(0xd4, 0xdd, 0xe3),
            3 => new HudColour(0xa4, 0xb0, 0xb8),
            4 => new HudColour(0x7c, 0x89, 0x90),
            _ => HudTheme.TextFaint,
        };

        /// <summary>A cell carrying a priority.</summary>
        public static readonly HudColour AssignedFill = new HudColour(0, 0, 0, 0.55f);

        /// <summary>
        /// A capable cell set to never. <b>Lighter than an assigned one, not darker</b>, so that a
        /// column of blanks reads as empty rather than as a hole punched through the panel.
        /// </summary>
        public static readonly HudColour BlankFill = new HudColour(0, 0, 0, 0.42f);

        /// <summary>A cell this colonist cannot use. Inert: no hover, no cursor, one glyph.</summary>
        public static readonly HudColour IncapableFill = new HudColour(255, 255, 255, 0.035f);

        public static readonly HudColour IncapableBorder = new HudColour(255, 255, 255, 0.07f);

        /// <summary>
        /// The em-dash. <b>The one glyph on this panel that does not meet 4.5:1</b> — it measures
        /// 3.24, and that is the design: it is not information to read, it is the absence of
        /// information, and it meets the 3:1 a non-text graphic owes.
        /// </summary>
        public static readonly HudColour IncapableInk = new HudColour(255, 255, 255, 0.35f);

        /// <summary>The wash laid down a column the simulation does not run yet.</summary>
        public static readonly HudColour UnbuiltWash = new HudColour(255, 255, 255, 0.03f);

        /// <summary>Simple mode's two glyphs.</summary>
        public static readonly HudColour WillDo = HudTheme.Good;

        public static readonly HudColour WontDo = HudTheme.Bad;

        /// <summary>The passion flame. The HUD has one amber and this is it.</summary>
        public static readonly HudColour Flame = HudTheme.Warn;

        /// <summary>
        /// A flame's box. Square, because the glyph it is drawn with is: the mockup's 9 × 11 is a
        /// CSS clip-path and this one carries its own proportions inside a square, which keeps two
        /// flames to 19px and clear of a 28px cell's corner.
        /// </summary>
        public const int FlameSize = 9;
    }
}
