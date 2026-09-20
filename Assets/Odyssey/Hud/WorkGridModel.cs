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

        /// <summary>
        /// The rows on the current page, and <b>only those</b> — at most
        /// <see cref="WorkGridLayout.RowsPerPage"/> of them, whatever the colony's size.
        /// <see cref="TotalRows"/> is the colony.
        /// </summary>
        public readonly List<WorkRow> Rows = new List<WorkRow>();

        /// <summary>
        /// Every colonist, in the roster's order, which is what the pages are cut from. Held
        /// between refreshes so <see cref="EnsureRowPageFor"/> can answer without one.
        /// </summary>
        readonly List<PawnId> _all = new List<PawnId>();

        /// <summary>
        /// Rows lifted off the last page and kept. <b>A page is twelve rows at most, so the pool
        /// reaches its size within one page turn and a refresh allocates nothing after that</b> —
        /// which is ADR 0003's flip condition F1, and the reason the rows are recycled rather than
        /// rebuilt now that there is a bound on how many there can be. Unbounded, pooling would
        /// only have moved the allocation into a list that never shrank.
        /// </summary>
        readonly List<WorkRow> _pool = new List<WorkRow>();

        /// <summary>
        /// The column the colony is sorted by, or -1 for the roster's own order.
        ///
        /// <para><b>Clicking a header sorts by it, highest first</b> (owner, 2026-09-20). The sort
        /// is this panel's view of the roster and never the roster itself: the strip's order is
        /// something the player arranged by dragging cards, and a panel that reordered it from
        /// here would be answering a question nobody asked in a place they cannot see.</para>
        ///
        /// <para>It lasts as long as the panel is open and is dropped when it closes, which is the
        /// owner's reading of <i>"when you leave the control it resets"</i>. The reset button in
        /// the Colonist header is for changing your mind without closing.</para>
        /// </summary>
        public int SortColumn { get; private set; } = NoSort;

        /// <summary>No column: the rows come in the roster's order.</summary>
        public const int NoSort = -1;

        /// <summary>
        /// The schedule block the key has armed, or -1 for none.
        ///
        /// <para>Armed from the key, which is a row of buttons (owner, 2026-09-20). While one is
        /// armed an hour takes it; with none armed an hour cycles, exactly as it did before there
        /// was a key to press. So the gesture that already worked keeps working and the palette is
        /// an addition rather than a replacement.</para>
        /// </summary>
        public int ArmedBlock { get; private set; } = NoBlock;

        /// <summary>No block armed: an hour cycles.</summary>
        public const int NoBlock = -1;

        /// <summary>Which page of work columns is showing. Zero-based, as the roster's is.</summary>
        public int ColumnPage { get; private set; }

        /// <summary>Which page of colonists is showing.</summary>
        public int RowPage { get; private set; }

        /// <summary>Colonists in the colony, which is not the same as rows on this page.</summary>
        public int TotalRows { get; private set; }

        /// <summary>How many pages of columns there are. Two, while the catalogue is twenty-two.</summary>
        public int ColumnPageCount => WorkGridLayout.ColumnPagesFor(Columns.Count);

        /// <summary>How many pages of colonists there are.</summary>
        public int RowPageCount => WorkGridLayout.RowPagesFor(TotalRows);

        /// <summary>
        /// Columns actually on this page — eleven, and fewer only on a last page that does not
        /// divide. It does divide today; this is what stops a twenty-third work type drawing a
        /// column that is not there.
        /// </summary>
        public int VisibleColumns
        {
            get
            {
                int left = Columns.Count - ColumnPage * WorkGridLayout.ColumnsPerPage;
                return left <= 0 ? 0 : (left < WorkGridLayout.ColumnsPerPage
                    ? left
                    : WorkGridLayout.ColumnsPerPage);
            }
        }

        /// <summary>
        /// The catalogue index a page slot stands for, or -1 for a slot past the end.
        ///
        /// <para><b>A slot is not a column.</b> The shell builds eleven cells and this is what says
        /// which work type each one is showing; the cells themselves are still addressed by their
        /// catalogue index everywhere else, so nothing else in the model or its tests has to learn
        /// that pages exist.</para>
        /// </summary>
        public int ColumnAt(int slot)
        {
            if (slot < 0 || slot >= WorkGridLayout.ColumnsPerPage) return -1;
            int column = ColumnPage * WorkGridLayout.ColumnsPerPage + slot;
            return column < Columns.Count ? column : -1;
        }

        /// <summary>
        /// Sort the colony by a column, highest first, or do nothing for a column there is
        /// nothing to sort by.
        ///
        /// <para><b>A skill level where there is one and a priority where there is not.</b>
        /// Hauling is the single live column with no skill behind it, so "who is best at it" has
        /// no answer and the priority is the only ordering that means anything there. A column the
        /// simulation does not run yet has neither and is refused.</para>
        /// </summary>
        public bool SortBy(int column)
        {
            if (column < 0 || column >= Columns.Count) return false;
            if (!Columns[column].Live) return false;
            SortColumn = column;
            return true;
        }

        /// <summary>Back to the roster's order. The reset button, and closing the panel.</summary>
        public void ClearSort() => SortColumn = NoSort;

        /// <summary>
        /// Arm a block from the key, or disarm it by pressing the one already armed.
        /// </summary>
        public void ArmBlock(int handle)
        {
            ArmedBlock = ArmedBlock == handle ? NoBlock : handle;
        }

        /// <summary>Disarm whatever the key had armed. Closing the panel does this.</summary>
        public void DisarmBlock() => ArmedBlock = NoBlock;

        /// <summary>
        /// What a row sorts by in the given column, higher being earlier.
        ///
        /// <para>A skill level is already "higher is better". A priority is not: 1 is the most
        /// urgent and 0 means never, so it is turned round here into an <i>importance</i> that
        /// orders 1, 2, 3, 4, never. Both live on one scale so the comparer has one rule.</para>
        /// </summary>
        public static int SortKeyOf(in WorkCell cell, bool hasSkill)
        {
            if (hasSkill) return cell.Level;
            return cell.Priority <= Never ? 0 : Lowest + 1 - cell.Priority;
        }

        /// <summary>Show a page of columns, clamped to the ones that exist.</summary>
        public void SetColumnPage(int page) => ColumnPage = Clamp(page, 0, ColumnPageCount - 1);

        /// <summary>Show a page of colonists, clamped to the ones that exist.</summary>
        public void SetRowPage(int page) => RowPage = Clamp(page, 0, RowPageCount - 1);

        /// <summary>
        /// Bring the page holding this colonist up, and say whether that moved anything.
        ///
        /// <para><c>RosterModel.EnsurePageFor</c>'s job, for the same reason: selecting somebody on
        /// the board or in the strip has to be able to show them here, and a panel that answers a
        /// selection with a page it is not on is a panel that looks broken.</para>
        /// </summary>
        public bool EnsureRowPageFor(PawnId id)
        {
            int index = _all.IndexOf(id);
            if (index < 0) return false;

            int page = index / WorkGridLayout.RowsPerPage;
            if (page == RowPage) return false;
            SetRowPage(page);
            return true;
        }

        /// <summary>
        /// Presentation state, and pointedly not colony state: not saved into the world, not
        /// hashed, and carried in the view beside the camera and the slice.
        /// </summary>
        public WorkGridMode Mode { get; set; } = WorkGridMode.Detailed;

        /// <summary>The columns, whether or not the simulation runs them. Always twenty-two.</summary>
        public static IReadOnlyList<WorkCatalogue.Entry> Columns => WorkCatalogue.All;

        /// <summary>
        /// How wide the panel is. <b>A constant, not a function of the colony or the catalogue</b>
        /// — one page of columns and the whole day — which is what "the control never needs to
        /// resize" comes to in a number.
        /// </summary>
        public int Width => WorkGridLayout.PanelWidth;

        /// <summary>
        /// Rebuild from a snapshot. The order is the roster's, so the grid and the roster strip
        /// agree about who is third; passing the roster's order rather than the snapshot's is what
        /// makes a drag-reordered strip reorder this too.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId>? order,
            IReadOnlyList<PawnId>? selected)
        {
            // The whole colony first, because the page count comes out of it and so does
            // EnsureRowPageFor. Only the slice is turned into rows.
            _all.Clear();
            if (order != null)
                for (int i = 0; i < order.Count; i++)
                    if (snapshot.TryGetPawn(order[i], out _)) _all.Add(order[i]);

            // Sorted before it is paged, so page one holds the best of the colony rather than the
            // best of whoever happened to be on page one.
            if (SortColumn != NoSort) SortAll(snapshot);

            TotalRows = _all.Count;

            // Somebody died or the colony shrank: the page the player was on may not exist now.
            SetRowPage(RowPage);

            Recycle();

            int start = RowPage * WorkGridLayout.RowsPerPage;
            int end = start + WorkGridLayout.RowsPerPage;
            if (end > TotalRows) end = TotalRows;

            for (int i = start; i < end; i++)
            {
                PawnId id = _all[i];
                if (!snapshot.TryGetPawn(id, out PawnView pawn)) continue;

                uint seed = ColonistNames.RollSeedOf(snapshot, pawn.Id);
                WorkRow row = Take();
                row.Id = pawn.Id;
                row.Seed = seed;
                row.Name = ColonistNames.Of(seed, pawn.Id);
                row.Selected = Contains(selected, id);

                // Every column, not this page's eleven. A WorkCell is a struct in a list that is
                // already the right length after the first refresh, so reading all twenty-two
                // costs nothing measurable and keeps a cell addressable by its catalogue index
                // everywhere — which is what stops paging leaking into the rest of the model.
                for (int c = 0; c < Columns.Count; c++)
                    row.Cells.Add(ReadCell(snapshot, id, c));

                for (int h = 0; h < WorkGridLayout.Hours; h++)
                    row.Hours.Add(ReadHour(snapshot, id, h));

                Rows.Add(row);
            }
        }

        /// <summary>
        /// Order the whole colony by the sorted column, highest first.
        ///
        /// <para><b>Stable, and deliberately so.</b> <c>List.Sort</c> is not, and a colony where
        /// three people share a level would shuffle those three every refresh — five times a
        /// second, under the cursor, for as long as the panel was open. The roster position is the
        /// tie-break, so equal skill keeps the order the player already arranged.</para>
        ///
        /// <para>The keys are read once into a buffer rather than inside the comparer, because a
        /// comparer that reads the snapshot does so O(n log n) times for values that cannot change
        /// during a sort.</para>
        /// </summary>
        void SortAll(WorldSnapshot snapshot)
        {
            WorkCatalogue.Entry entry = Columns[SortColumn];

            _sortKeys.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                WorkCell cell = ReadCell(snapshot, _all[i], SortColumn);
                _sortKeys.Add(SortKeyOf(cell, entry.HasSkill));
            }

            _order.Clear();
            for (int i = 0; i < _all.Count; i++) _order.Add(i);
            _order.Sort((a, b) =>
            {
                int by = _sortKeys[b].CompareTo(_sortKeys[a]);   // descending
                return by != 0 ? by : a.CompareTo(b);            // then the roster's order
            });

            _sorted.Clear();
            for (int i = 0; i < _order.Count; i++) _sorted.Add(_all[_order[i]]);

            _all.Clear();
            for (int i = 0; i < _sorted.Count; i++) _all.Add(_sorted[i]);
        }

        readonly List<int> _sortKeys = new List<int>();
        readonly List<int> _order = new List<int>();
        readonly List<PawnId> _sorted = new List<PawnId>();

        /// <summary>Put this page's rows back in the pool, emptied and ready to be filled again.</summary>
        void Recycle()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].Cells.Clear();
                Rows[i].Hours.Clear();
                _pool.Add(Rows[i]);
            }
            Rows.Clear();
        }

        /// <summary>A recycled row, or a new one the first time through.</summary>
        WorkRow Take()
        {
            if (_pool.Count == 0) return new WorkRow();
            WorkRow row = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            return row;
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

            // An armed block paints; with none armed the hour cycles, as it always did.
            int next = ArmedBlock != NoBlock
                ? ArmedBlock
                : back ? ScheduleCatalogue.CycleBack(current) : ScheduleCatalogue.Cycle(current);

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
