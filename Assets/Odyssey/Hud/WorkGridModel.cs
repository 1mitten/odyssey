#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
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
            if (mode == WorkGridMode.Simple) return Priority > 0 ? "✓" : "✕";
            return Priority > 0 ? Priority.ToString() : string.Empty;
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
        /// </summary>
        public bool TryClick(int row, int column, out Intent intent)
        {
            intent = default;
            if (row < 0 || row >= Rows.Count) return false;
            if (column < 0 || column >= Columns.Count) return false;

            WorkCell cell = Rows[row].Cells[column];
            if (!cell.Interactive) return false;

            intent = SetPriority(Rows[row].Id, Columns[column].Handle, Cycle(cell.Priority));
            return true;
        }

        /// <summary>
        /// The intent for one deliberate value, which is what a drag and a shift-click paint with.
        ///
        /// <para><b>The work type crosses as a <see cref="WorkHandle"/>, never as this panel's
        /// column number.</b> Twenty-two of those exist and four of these do; sending the column
        /// would make the simulation depend on the order of a list that lives in the HUD.</para>
        /// </summary>
        public static Intent SetPriority(PawnId pawn, int workHandle, int priority) =>
            new Intent(IntentKind.SetWorkPriority, default, pawn.Value, workHandle, priority);

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

        /// <summary>The subtitle: how many colonists, and which end is urgent.</summary>
        public string Subtitle() =>
            Rows.Count + (Rows.Count == 1 ? " colonist" : " colonists") +
            " · higher priority runs first";
    }
}
