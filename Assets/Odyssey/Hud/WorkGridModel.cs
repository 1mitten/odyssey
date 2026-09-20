#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Simple mode's answer for one cell: a tick, a cross, or neither.
    ///
    /// <para><b>A value and not a character, because the character was a blank.</b> The two were
    /// U+2713 and U+2715 written into the cell's label until the two font files this HUD ships
    /// were read: Archivo Narrow's cmap has neither and IBM Plex Mono has only the tick, so the
    /// legend drew two empty boxes and every "won't do" cell drew one. Neither tier could see it —
    /// the fast tier has no text engine and the Unity tier asserts no pixels — so the mark is a
    /// decision here and a drawn shape in the shell, and <c>HudFontTests</c> now fails the fast
    /// tier on any HUD literal carrying a character the fonts cannot draw.</para>
    /// </summary>
    public enum WorkMark
    {
        /// <summary>Detailed mode, an unbuilt column or an incapable cell. The label speaks.</summary>
        None,

        /// <summary>This colonist will take this work.</summary>
        Will,

        /// <summary>This colonist is set to never take it.</summary>
        Wont,
    }

    /// <summary>Whether the grid shows the four priorities or a yes and a no.</summary>
    public enum WorkGridMode
    {
        /// <summary>A tick or a cross. Setting a tick writes the default priority.</summary>
        Simple,

        /// <summary>The digits one to four, and a blank for never.</summary>
        Detailed,
    }

    /// <summary>
    /// One cell of the Work grid: four independent signals, already resolved.
    ///
    /// <para><b>Capability is enforced here and not in the stylesheet.</b> When
    /// <see cref="Capable"/> is false, <see cref="Priority"/> and <see cref="Passion"/> are zero
    /// and <see cref="Glyph"/> is the em-dash — the shell has nothing to hide. A capability
    /// expressed as opacity is a capability that leaks: into a tooltip, into a screenshot, into a
    /// copy-and-paste. The supplied mockup asks for this by name and it is the single most
    /// load-bearing line in it.</para>
    /// </summary>
    public readonly struct WorkCell
    {
        /// <summary>The column's index in <see cref="WorkCatalogue.All"/>.</summary>
        public readonly int Column;

        /// <summary>False when the simulation does not run this work type at all.</summary>
        public readonly bool Built;

        /// <summary>False when this colonist cannot do this work.</summary>
        public readonly bool Capable;

        /// <summary>0 to 4. Zero is never. Always zero when not <see cref="Capable"/>.</summary>
        public readonly int Priority;

        /// <summary>0, 1 or 2 flames. Always zero without a skill or without capability.</summary>
        public readonly int Passion;

        /// <summary>0 to 20, or -1 when no skill governs this column.</summary>
        public readonly int Level;

        public WorkCell(int column, bool built, bool capable, int priority, int passion, int level)
        {
            Column = column;
            Built = built;
            Capable = capable && built;
            Priority = Capable ? priority : 0;
            Passion = Capable ? passion : 0;
            Level = Capable ? level : -1;
        }

        /// <summary>The border band, or <see cref="ProficiencyBand.None"/> for hauling.</summary>
        public ProficiencyBand Band =>
            Level < 0 ? ProficiencyBand.None : WorkBands.BandOf(Level);

        /// <summary>Whether this cell can be clicked at all.</summary>
        public bool Interactive => Built && Capable;

        /// <summary>
        /// What the cell reads, in the given mode. An unbuilt column and an incapable cell both
        /// answer without consulting the mode, which is what keeps a mode switch from moving
        /// anything but a glyph.
        /// </summary>
        public string Glyph(WorkGridMode mode)
        {
            if (!Built) return string.Empty;
            if (!Capable) return "—";

            // Simple mode's answer is a drawn shape rather than a character — see
            // <see cref="Mark"/> — so the label has nothing to say in it.
            if (mode == WorkGridMode.Simple) return string.Empty;
            return Priority > 0 ? Priority.ToString() : string.Empty;
        }

        /// <summary>
        /// The tick or the cross this cell draws, or <see cref="WorkMark.None"/> when the label
        /// carries the reading instead.
        ///
        /// <para>The unbuilt and incapable cases answer <see cref="WorkMark.None"/> without
        /// consulting the mode, on the same rule <see cref="Glyph"/> follows: a mode switch may
        /// move a reading and may never move a state.</para>
        /// </summary>
        public WorkMark Mark(WorkGridMode mode)
        {
            if (mode != WorkGridMode.Simple || !Built || !Capable) return WorkMark.None;
            return Priority > 0 ? WorkMark.Will : WorkMark.Wont;
        }
    }

    /// <summary>One row: a colonist, and their twenty-two cells.</summary>
    public sealed class WorkRow
    {
        public PawnId Id;
        public uint Seed;
        public string Name = string.Empty;
        public bool Selected;
        public readonly List<WorkCell> Cells = new List<WorkCell>();

        /// <summary>This colonist's twenty-four hours, as <c>ScheduleHandle</c> values.</summary>
        public readonly List<int> Hours = new List<int>();
    }

    /// <summary>
    /// The Work tab's contents: every colonist against every work type, read out of the snapshot's
    /// pawn aspects.
    ///
    /// <para><b>Nothing here draws.</b> This is the half the fast tier can prove — the reading, the
    /// cycle and the inertness rule — and <c>HudShell.Work.cs</c> is the half only Unity can. The
    /// split is the project's own: the fast tier compiles neither Presentation nor Editor, so
    /// anything decided in a stylesheet is unproven until an editor run, and the decisions worth
    /// testing are moved here on purpose.</para>
    ///
    /// <para><b>The default is three, not never</b> — <c>Pawn</c> initialises every work priority
    /// to 3, which is why <see cref="DefaultPriority"/> is a named constant here as well as a
    /// literal there: two copies of a number that must agree, with a test that says so. <b>But the
    /// grid does not open on a colony of threes</b>, and this comment said it did until the code
    /// was read: <c>ColonyScenario.AssignTrade</c> deals the first miners Mining 1 / Chopping 3 and
    /// everybody else the reverse, so the panel's first screen is a division of labour somebody
    /// else chose. Design 27 §6.3.</para>
    /// </summary>
    public sealed class WorkGridModel
    {
        /// <summary>What <c>Pawn</c>'s constructor fills every priority with.</summary>
        public const int DefaultPriority = 3;

        /// <summary>The most urgent priority. One is highest, four is lowest.</summary>
        public const int Highest = 1;

        public const int Lowest = 4;

        /// <summary>Never. The scan runs 1 to 4, so zero is the value it never matches.</summary>
        public const int Never = 0;

        public readonly List<WorkRow> Rows = new List<WorkRow>();

        /// <summary>
        /// Presentation state, and pointedly not colony state: not saved into the world, not
        /// hashed, and carried in the view beside the camera and the slice.
        /// </summary>
        public WorkGridMode Mode { get; set; } = WorkGridMode.Detailed;

        /// <summary>The columns, whether or not the simulation runs them. Always twenty-two.</summary>
        public static IReadOnlyList<WorkCatalogue.Entry> Columns => WorkCatalogue.All;

        /// <summary>How wide the panel wants to be, for the shell to ask once.</summary>
        public int Width => WorkGridLayout.WidthFor(Columns.Count);

        /// <summary>
        /// Rebuild from a snapshot. The order is the roster's, so the grid and the roster strip
        /// agree about who is third; passing the roster's order rather than the snapshot's is what
        /// makes a drag-reordered strip reorder this too.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId>? order,
            IReadOnlyList<PawnId>? selected)
        {
            Rows.Clear();
            if (order == null) return;

            for (int i = 0; i < order.Count; i++)
            {
                PawnId id = order[i];
                if (!snapshot.TryGetPawn(id, out PawnView pawn)) continue;

                uint seed = ColonistNames.RollSeedOf(snapshot, pawn.Id);
                var row = new WorkRow
                {
                    Id = pawn.Id,
                    Seed = seed,
                    Name = ColonistNames.Of(seed, pawn.Id),
                    Selected = Contains(selected, id),
                };

                for (int c = 0; c < Columns.Count; c++)
                    row.Cells.Add(ReadCell(snapshot, id, c));

                for (int h = 0; h < WorkGridLayout.Hours; h++)
                    row.Hours.Add(ReadHour(snapshot, id, h));

                Rows.Add(row);
            }
        }

        static bool Contains(IReadOnlyList<PawnId>? selected, PawnId id)
        {
            if (selected == null) return false;
            for (int i = 0; i < selected.Count; i++) if (selected[i] == id) return true;
            return false;
        }

        /// <summary>
        /// One cell, read out of the pawn aspects.
        ///
        /// <para><b>A missing aspect is not a zero.</b> An unbuilt column publishes nothing at all,
        /// and a column that published a priority of zero would be a colonist told never to do a
        /// job that does not exist. So the built flag comes from the catalogue and the absence of
        /// an aspect on a live column is read as the default rather than as never — a snapshot
        /// taken before the first publish must not silently un-assign the colony.</para>
        /// </summary>
        public static WorkCell ReadCell(WorldSnapshot snapshot, PawnId pawn, int column)
        {
            WorkCatalogue.Entry entry = Columns[column];
            if (!entry.Live) return new WorkCell(column, false, false, 0, 0, -1);

            int priority = snapshot.TryGetPawnAspect(pawn, entry.Priority, out int p)
                ? Clamp(p, Never, Lowest)
                : DefaultPriority;

            bool capable = !snapshot.TryGetPawnAspect(pawn, entry.Capable, out int cap) || cap != 0;

            int level = -1;
            int passion = 0;
            if (entry.HasSkill)
            {
                level = snapshot.TryGetPawnAspect(pawn, entry.Level, out int l) ? Clamp(l, 0, 20) : 0;
                passion = snapshot.TryGetPawnAspect(pawn, entry.Passion, out int f) ? Clamp(f, 0, 2) : 0;
            }

            return new WorkCell(column, true, capable, priority, passion, level);
        }

        /// <summary>
        /// One hour of one colonist's day.
        ///
        /// <para>A missing aspect reads as <c>Anything</c> — no instruction — which is both the
        /// honest answer for a build that does not publish schedules and the value a colonist
        /// nobody has scheduled carries.</para>
        /// </summary>
        public static int ReadHour(WorldSnapshot snapshot, PawnId pawn, int hour)
        {
            if (hour < 0 || hour >= WorkGridLayout.Hours) return ScheduleHandle.Anything;
            return snapshot.TryGetPawnAspect(pawn, ScheduleKeys.Hour[hour], out int block)
                ? Clamp(block, 0, ScheduleHandle.Count - 1)
                : ScheduleHandle.Anything;
        }

        /// <summary>
        /// Which hour the colony is in, for the now-line, or -1 when there is no clock to ask.
        /// <see cref="GameClock.HourOfDay"/> is the one owner of that arithmetic.
        /// </summary>
        public static int NowHour(WorldSnapshot snapshot) =>
            snapshot.Tick < 0 ? -1 : GameClock.HourOfDay(snapshot.Tick);

        static int Clamp(int value, int low, int high) =>
            value < low ? low : value > high ? high : value;

        // ------------------------------------------------------------------ the gestures

        /// <summary>
        /// What one click makes the priority. Detailed cycles 1 → 2 → 3 → 4 → never → 1; Simple
        /// toggles between never and <see cref="DefaultPriority"/>, so a player who never opens
        /// Detailed still writes the value the colony was born with, and a player who switches
        /// back finds their own numbers where they left them.
        /// </summary>
        public int Cycle(int priority) =>
            Mode == WorkGridMode.Simple
                ? (priority > Never ? Never : DefaultPriority)
                : (priority >= Lowest ? Never : priority + 1);

        /// <summary>
        /// What a right-click makes it: the same ring, walked the other way. Added because a
        /// five-state cycle you can only walk forwards is four clicks to undo one mistake.
        /// </summary>
        public int CycleBack(int priority) =>
            Mode == WorkGridMode.Simple
                ? Cycle(priority)
                : (priority <= Never ? Lowest : priority - 1);

        /// <summary>
        /// The intent one click emits, or none when the cell is inert. <c>A</c> is the pawn,
        /// <c>B</c> the work type's simulation index and <c>C</c> the new priority.
        ///
        /// <para><b>The shell calls this rather than repeating it</b>, which it did until the
        /// review: it had its own bounds check, its own inertness check and its own choice of
        /// <see cref="Cycle"/> or <see cref="CycleBack"/>, so the rule the tests hold was not the
        /// rule the panel ran. <see cref="TryClickHour"/> had taken <c>back</c> from the start and
        /// the schedule half went through it; this is the work half made to match, which is also
        /// what design 27 §12g claims about the two halves sharing one vocabulary.</para>
        /// </summary>
        public bool TryClick(int row, int column, bool back, out Intent intent)
        {
            intent = default;
            if (row < 0 || row >= Rows.Count) return false;
            if (column < 0 || column >= Columns.Count) return false;

            WorkCell cell = Rows[row].Cells[column];
            if (!cell.Interactive) return false;

            int next = back ? CycleBack(cell.Priority) : Cycle(cell.Priority);
            intent = SetPriority(Rows[row].Id, Columns[column].Handle, next);
            return true;
        }

        /// <summary>Whether the cell at this position can be clicked at all — the question the
        /// shell has to ask before it swallows a press.</summary>
        public bool CellIsInteractive(int row, int column) =>
            row >= 0 && row < Rows.Count && column >= 0 && column < Columns.Count &&
            Rows[row].Cells[column].Interactive;

        /// <summary>
        /// The intent for one deliberate value, which is what a drag and a shift-click paint with.
        ///
        /// <para><b>The work type crosses as a <see cref="WorkHandle"/>, never as this panel's
        /// column number.</b> Twenty-two of those exist and four of these do; sending the column
        /// would make the simulation depend on the order of a list that lives in the HUD.</para>
        /// </summary>
        public static Intent SetPriority(PawnId pawn, int workHandle, int priority) =>
            new Intent(IntentKind.SetWorkPriority, default, pawn.Value, workHandle, priority);

        /// <summary>The other half of the row: one colonist, one hour, one block.</summary>
        public static Intent SetSchedule(PawnId pawn, int hour, int block) =>
            new Intent(IntentKind.SetScheduleBlock, default, pawn.Value, hour, block);

        /// <summary>
        /// The intent one click on an hour emits. Unlike a work cell there is no inert case: every
        /// colonist has every hour, and nothing about a schedule can be unavailable to somebody.
        /// </summary>
        public bool TryClickHour(int row, int hour, bool back, out Intent intent)
        {
            intent = default;
            if (row < 0 || row >= Rows.Count) return false;
            if (hour < 0 || hour >= Rows[row].Hours.Count) return false;

            int current = Rows[row].Hours[hour];
            int next = back ? ScheduleCatalogue.CycleBack(current) : ScheduleCatalogue.Cycle(current);
            intent = SetSchedule(Rows[row].Id, hour, next);
            return true;
        }

        /// <summary>
        /// The sentence a cell says when hovered. Built here rather than in the shell because it
        /// is the one place the four signals are stated in words, and a reader checking that the
        /// colours mean what they think they mean reads this.
        /// </summary>
        public string Describe(int row, int column)
        {
            if (row < 0 || row >= Rows.Count) return string.Empty;
            if (column < 0 || column >= Columns.Count) return string.Empty;

            WorkCatalogue.Entry entry = Columns[column];
            WorkRow r = Rows[row];
            WorkCell cell = r.Cells[column];

            if (!cell.Built) return entry.Label + " — " + entry.Reason;
            if (!cell.Capable) return r.Name + " cannot do " + entry.Label.ToLowerInvariant();

            string priority = cell.Priority > Never ? "priority " + cell.Priority : "never";
            if (!entry.HasSkill) return r.Name + " · " + entry.Label + " · " + priority +
                                        " · hauling has no skill";

            string passion = cell.Passion == 2 ? " · passion"
                : cell.Passion == 1 ? " · interested"
                : string.Empty;
            return r.Name + " · " + entry.Label + " · " + priority +
                   " · level " + cell.Level + passion;
        }

        /// <summary>
        /// The subtitle: how many colonists, what the table is, and what hour it is.
        ///
        /// <para>It says <i>what they do, and when</i> rather than naming the two halves, because
        /// the whole claim of the combined table is that they are one question.</para>
        /// </summary>
        public string Subtitle(int nowHour = -1)
        {
            string people = Rows.Count + (Rows.Count == 1 ? " colonist" : " colonists");
            string clock = nowHour >= 0 && nowHour < WorkGridLayout.Hours
                ? " · " + nowHour.ToString("00") + "h"
                : string.Empty;
            return people + " · what they do, and when" + clock;
        }
    }
}
