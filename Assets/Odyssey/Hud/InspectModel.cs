#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    public enum InspectSubject
    {
        None,
        Colonist,
        Item,
        Cell,
    }

    /// <summary>
    /// One pane tab (B1): its name, whether it is live, and the reason shown when it is not.
    /// Tabs whose systems do not exist yet are present and disabled, so the shape of the game is
    /// visible from the first version — the panel catalogue's guarantee, made concrete.
    /// </summary>
    public struct InspectTab
    {
        public string Name;
        public bool Enabled;
        public string Reason;
    }

    /// <summary>
    /// One command-grid slot (A10). Every command a colonist will eventually offer is visible
    /// now, greyed, with the honest reason it is not available — never hidden, per the
    /// catalogue's "disabled with a reason" rule.
    /// </summary>
    public struct InspectCommand
    {
        public string IconKey;
        public string Label;
        public bool Enabled;
        public string Reason;
    }

    /// <summary>
    /// One line of the Skills tab: what it is called, what the colonist's standing in it is, and
    /// — where there is no simulation behind it yet — why there is no number.
    ///
    /// <para>The level is not a percentage and is not stored as one: it is the simulation's own
    /// 0 to 20, derived there from the experience ladder and published as a byte. Passion is the
    /// simulation's own three values, and is the field that decides who you put on a job, so it
    /// is on the row rather than in a tooltip (owner, 2026-09-17).</para>
    /// </summary>
    public struct SkillRow
    {
        public string IconKey;
        public string Name;

        /// <summary>False when nothing in the simulation trains it; <see cref="Reason"/> says why.</summary>
        public bool Live;

        public int Level;

        /// <summary>0 none, 1 minor, 2 major.</summary>
        public int Passion;

        /// <summary>Experience in thousandths of a point, for a progress bar when one is wanted.</summary>
        public int Experience;

        public string Reason;
        public string Note;
    }

    /// <summary>
    /// One line of the tile readout: what the fact is called, and what it reads. See
    /// <see cref="InspectModel.CellRows"/> for why the facts are rows rather than a sentence.
    /// </summary>
    public struct InspectRow
    {
        public string Name;
        public string Value;
    }

    /// <summary>
    /// Assembles the inspect pane (A9) for the current selection: which subject, which header,
    /// which tabs, which commands. Pure function of the selection handle and the published
    /// frame; holds no reference to a simulation object, which is the whole contract.
    ///
    /// The tombstone case is specified behaviour, not an accident: a colonist who is gone from
    /// the frame keeps the pane open with last-known values greyed and every command disabled,
    /// rather than closing under the cursor (design 09 §2.3). The slice has no death yet, but
    /// the pane behaves correctly the day it arrives.
    /// </summary>
    public sealed class InspectModel
    {
        public InspectSubject Subject;
        public PawnId Pawn;
        public ThingId Thing;

        /// <summary>Last-known values of a colonist who has left the frame, shown greyed.</summary>
        public bool Tombstoned;

        // ---- header
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public string Position = string.Empty;
        public int Layer = -1;

        /// <summary>
        /// The one line a building site has to say for itself: what it is waiting for, or how much
        /// longer it will take. Empty when the selected cell has no site on it.
        /// </summary>
        public string Site = string.Empty;

        /// <summary>The registry key for what is being built, so the pane can draw its icon.</summary>
        public string SiteIconKey = string.Empty;

        /// <summary>Units of material delivered and wanted, for a bar. Both 0 when there is no site.</summary>
        public int SiteDelivered;
        public int SiteCost;

        /// <summary>How far through the work, 0 to 1. Separate from the material, per <see cref="SiteView"/>.</summary>
        public float SiteProgress;

        // ---- item body
        /// <summary>How many are in the selected pile. The ledger counts these, never the piles.</summary>
        public int Stack;

        /// <summary>The pile's own icon key, so a pile of wood stops drawing a meal.</summary>
        public string ItemIconKey = "ui.res.meal";

        // ---- cell body
        /// <summary>
        /// The tile's facts, one row each in a fixed order: any order standing on it, the work of
        /// it, what crossing it costs, the floor, what it bears. A row is a label and a value
        /// because the pane prints them in two columns — the same fact is always in the same
        /// place, which a joined line never gave (owner, 2026-09-17). Empty when the world has
        /// not answered the question yet, and the pane says nothing rather than pretending.
        /// </summary>
        public readonly List<InspectRow> CellRows = new List<InspectRow>();

        /// <summary>The tile's own icon key, so the pane's avatar is the thing that was clicked.</summary>
        public string CellIconKey = "ui.overlay.zones";

        // ---- colonist body, the Needs tab
        public string Job = "idle";
        public string JobIconKey = "ui.status.idle";
        public int Food;
        public int Rest;      // 0..1000, the simulation's scale
        public int Mood;      // 0..1000, like Food and Rest

        // ---- no selection: the colony summary
        public int ColonySize;
        public readonly List<int> JobCounts = new List<int>();

        public readonly List<InspectTab> Tabs = new List<InspectTab>();
        public readonly List<InspectCommand> Commands = new List<InspectCommand>();

        /// <summary>
        /// The Skills tab's rows: every skill the design names, whether or not the simulation can
        /// train it. Rebuilt in place on every refresh, so the pane allocates nothing per frame
        /// once the list has reached its length.
        /// </summary>
        public readonly List<SkillRow> Skills = new List<SkillRow>();

        /// <summary>
        /// Which tab the pane is showing, as an index into <see cref="Tabs"/>. Held on the model
        /// rather than in the view, because "which tab" survives a refresh and a reselection and
        /// the view is rebuilt from the model, never the other way round.
        /// </summary>
        public int ActiveTab;

        /// <summary>
        /// Show a tab, if it is one that can be shown. A disabled tab is not a no-op by accident:
        /// the catalogue's rule is that a dead control says why rather than doing nothing
        /// silently, and the reason is already on the chip's tooltip.
        /// </summary>
        public void ShowTab(int index)
        {
            if (index < 0 || index >= Tabs.Count || !Tabs[index].Enabled) return;
            ActiveTab = index;
        }

        /// <summary>The name of the tab being shown, or empty when nothing is selected.</summary>
        public string ActiveTabName =>
            ActiveTab >= 0 && ActiveTab < Tabs.Count ? Tabs[ActiveTab].Name : string.Empty;

        // Selection state is set by the pick resolver; the model never reads input itself.
        public void SetColonist(PawnId id)
        {
            Subject = InspectSubject.Colonist;
            Pawn = id;
            Thing = ThingId.None;
        }

        public void SetItem(ThingId id)
        {
            Subject = InspectSubject.Item;
            Thing = id;
            Pawn = PawnId.None;
        }

        public void SetCell(CellRef cell)
        {
            Subject = InspectSubject.Cell;
            Pawn = PawnId.None;
            Thing = ThingId.None;
            _cell = cell;
        }

        public void ClearSelection()
        {
            Subject = InspectSubject.None;
            Pawn = PawnId.None;
            Thing = ThingId.None;
            Tombstoned = false;
        }

        CellRef _cell;

        // The last cell Position was written for. The pane refreshes fifteen times a second and
        // "at 78, 59" is an interpolated string, so without this the model allocates one per
        // refresh for as long as anything is selected — which ADR 0003's flip condition F1
        // forbids, and which also defeats the view's own guard, since a fresh string instance
        // every time makes "has this changed" unanswerable by comparison.
        CellRef _positionFor;
        bool _positionWritten;

        void SetPosition(CellRef cell)
        {
            if (_positionWritten && _positionFor == cell) return;
            _positionFor = cell;
            _positionWritten = true;
            Position = $"at {cell.X}, {cell.Z}";
        }

        /// <summary>
        /// Refill every field from the current frame. Called on the pane's cadence (15 Hz in the
        /// catalogue), and once more the moment the selection changes, so a click answers in the
        /// same frame it happened.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot)
        {
            Tabs.Clear();
            Commands.Clear();
            _bedUnderPane = false;

            if (Subject == InspectSubject.Colonist)
            {
                if (snapshot.TryGetPawn(Pawn, out PawnView pawn))
                {
                    Tombstoned = false;
                    Title = ColonistNames.Of(snapshot, pawn.Id);
                    Subtitle = "colonist";
                    Job = JobLabels.Label(pawn.JobDef);
                    JobIconKey = JobLabels.IconKey(pawn.JobDef);
                    Food = pawn.Food;
                    Rest = pawn.Rest;
                    Mood = pawn.Mood;
                    SetPosition(pawn.Cell);
                    Layer = pawn.Cell.Y;
                }
                else
                {
                    // Gone from the frame. The pane stays open on last-known values, greyed by
                    // the view, with no way to command a subject that no longer exists.
                    Tombstoned = true;
                }

                AddColonistTabs();
                AddColonistCommands();
                RefreshSkills(snapshot);
                return;
            }

            Tombstoned = false;
            if (Subject == InspectSubject.Item)
            {
                bool found = false;
                var things = snapshot.Things;
                for (int i = 0; i < things.Length; i++)
                {
                    if (things[i].Id != Thing) continue;
                    ThingView thing = things[i];
                    Title = ItemLabels.Label(thing.DefIndex);
                    Subtitle = "item";
                    Stack = thing.Stack;
                    ItemIconKey = ItemLabels.IconKey(thing.DefIndex);
                    SetPosition(thing.Cell);
                    Layer = thing.Cell.Y;
                    found = true;
                    break;
                }
                if (!found) Subtitle = "item · no longer present";
                return;
            }

            if (Subject == InspectSubject.Cell)
            {
                SetPosition(_cell);
                Layer = _cell.Y;
                if (DescribeSiteAt(snapshot, _cell))
                {
                    // A site leads and is the whole answer: the tile under a blueprint is the
                    // least interesting thing about the click.
                    CellRows.Clear();
                    _cellRowsFor = -1;
                    CellIconKey = "ui.overlay.zones";
                    return;
                }

                Site = string.Empty;
                SiteIconKey = string.Empty;
                DescribeCellAt(snapshot, _cell);

                return;
            }

            // Nothing selected: the colony as a whole, which answers "is anything happening".
            Title = "The holding";
            Subtitle = "nothing selected";
            Position = string.Empty;
            _positionWritten = false;   // or a later selection on the same cell keeps the blank
            Layer = -1;
            ColonySize = snapshot.PawnCount;
            JobCounts.Clear();
            for (int job = 0; job < JobHandle.Count; job++) JobCounts.Add(0);
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
                if (pawns[i].JobDef >= 0 && pawns[i].JobDef < JobHandle.Count)
                    JobCounts[pawns[i].JobDef]++;
        }

        /// <summary>
        /// What is going up here, whether it has its material, and how much longer.
        ///
        /// <para><b>The three questions a player asks of a blueprint, and the pane could answer
        /// none of them</b> — a click on a site said "Ground · cell", exactly as a click on bare
        /// grass did, so the one thing on the board that is <i>about</i> a plan had nothing to say
        /// about it (owner, 2026-09-17).</para>
        ///
        /// <para><b>The material line leads when the material is missing</b>, because that is the
        /// actionable half: a site with no wood is not slow, it is stuck, and telling the player
        /// "0%" would describe the symptom rather than the cause. Once it is fed, the time left is
        /// what they want, so that is what the line becomes. It is the same rule <c>AlertModel</c>
        /// follows — lead with the actionable clause.</para>
        ///
        /// <para>The estimate is honest about being one: it is the work remaining at one colonist's
        /// pace, and two builders halve it while none makes it infinite. "about" is doing real work
        /// in that sentence.</para>
        /// </summary>
        bool DescribeSiteAt(WorldSnapshot snapshot, CellRef cell)
        {
            int index = snapshot.Size.Index(cell);
            var sites = snapshot.Sites;

            for (int i = 0; i < sites.Length; i++)
            {
                if (sites[i].CellIndex != index) continue;

                SiteView site = sites[i];
                string thing = BuildLabels.Building(site.Building);
                string stuff = BuildLabels.Stuff(site.Stuff);

                Title = thing.Length == 0 ? "Building site" : thing;
                Subtitle = stuff.Length == 0 ? "planned" : "planned · " + stuff;
                SiteIconKey = BuildLabels.BuildingKey(site.Building);
                SetSiteLine(site, stuff);
                SiteDelivered = site.Delivered;
                SiteCost = site.Cost;
                SiteProgress = site.Progress;
                return true;
            }

            SiteDelivered = 0;
            SiteCost = 0;
            SiteProgress = 0f;
            _siteDeliveredFor = -1;
            _siteSecondsFor = -1;
            return false;
        }

        // The last values Site was written for. Same argument as _positionFor: the pane refreshes
        // fifteen times a second and this is an interpolated string, so rebuilding it every time
        // would allocate for as long as a site is selected — ADR 0003 F1 — and would also defeat
        // the view's own guard, which asks "is this the same instance".
        //
        // The seconds reading is quantised to whole seconds before it is compared, so a countdown
        // rebuilds once a second rather than fifteen times.
        int _siteDeliveredFor = -1;
        int _siteSecondsFor = -1;
        bool _siteFramedFor;

        void SetSiteLine(SiteView site, string stuff)
        {
            int seconds = site.IsFrame ? (site.WorkTotal - site.WorkDone + 59) / 60 : -1;
            if (_siteFramedFor == site.IsFrame
                && _siteDeliveredFor == site.Delivered
                && _siteSecondsFor == seconds) return;

            _siteFramedFor = site.IsFrame;
            _siteDeliveredFor = site.Delivered;
            _siteSecondsFor = seconds;

            // The material leads while it is missing, because that is the actionable half: a site
            // with no wood is not slow, it is stuck.
            Site = site.IsFrame
                ? "about " + Seconds(site.WorkTotal - site.WorkDone) + " left"
                : $"{site.Delivered} of {site.Cost} {stuff} delivered";
        }

        /// <summary>
        /// Ticks as a rough wall-clock reading at speed 1, which is the only pace a player can
        /// judge a wait against. 60 ticks a second, per the composition root's own rate.
        /// </summary>
        static string Seconds(int ticks)
        {
            if (ticks <= 0) return "no time";
            int seconds = (ticks + 59) / 60;
            if (seconds < 60) return seconds + "s";
            return seconds / 60 + "m " + seconds % 60 + "s";
        }

        // ---- the cell readout --------------------------------------------------------------

        /// <summary>
        /// What the clicked cell is, when no site stands on it.
        ///
        /// <para><b>The title names the thing that was clicked, in the picker's own order of
        /// ownership:</b> the edifice standing in the cell above all (a tree is what a click on a
        /// tree means), then the built slab (a bridge is walked on, and the tile under the click
        /// is air), then the terrain itself. "Ground" is what a blank key leaves — the city's
        /// finished surfaces, which are not the played board's to name yet.</para>
        ///
        /// <para><b>No answer is a state, not a gap.</b> The row arrives one publish after the
        /// click and is withdrawn when the question is, so a pane may honestly show "Ground" for
        /// the frame between the two. What it must never do is invent a reading.</para>
        /// </summary>
        void DescribeCellAt(WorldSnapshot snapshot, CellRef cell)
        {
            if (!snapshot.TryGetCellDetail(snapshot.Size.Index(cell), out CellDetail detail))
            {
                Title = "Ground";
                Subtitle = "cell";
                CellIconKey = "ui.overlay.zones";
                if (_cellRowsFor >= 0)
                {
                    CellRows.Clear();
                    _cellRowsFor = -1;
                }
                return;
            }

            Subtitle = "cell";

            string edifice = EdificeLabels.Title(detail.Edifice);
            string terrain = TerrainLabels.Label(detail.Terrain);
            if (edifice.Length > 0)
            {
                Title = edifice;
                CellIconKey = EdificeLabels.IconKey(detail.Edifice);
            }
            else if (detail.FloorStuff != StuffHandle.None)
            {
                string stuff = BuildLabels.Stuff(detail.FloorStuff);
                Title = stuff.Length == 0
                    ? "Built floor"
                    : char.ToUpperInvariant(stuff[0]) + stuff.Substring(1) + " floor";
                CellIconKey = BuildLabels.StuffKey(detail.FloorStuff);
            }
            else if (terrain.Length > 0)
            {
                Title = terrain;
                CellIconKey = TerrainLabels.IconKey(detail.Terrain);
            }
            else
            {
                Title = "Ground";
                CellIconKey = "ui.overlay.zones";
            }

            SetCellRows(snapshot, detail);
        }

        // The last cell the readout rows were written for, and everything they quote. The pane
        // refreshes fifteen times a second and the rows are strings, so they are rebuilt only
        // when something they say has moved — the same argument as _positionFor and
        // _siteSecondsFor, and the same flip condition F1 from ADR 0003. Order progress is
        // compared as the whole percent it prints, so a face being cut rebuilds the rows once
        // per percent rather than fifteen times a second.
        int _cellRowsFor = -1;
        int _cellRowsCost;
        int _cellRowsFloor;
        int _cellRowsEdifice;
        int _cellRowsSupport;
        int _cellRowsWork;
        int _cellRowsOrderKind;
        int _cellRowsOrderPercent;
        int _cellRowsQuality;
        int _cellRowsOwner;

        /// <summary>
        /// Whether the tile under the pane is a bed whose owner row can be pressed — the pane's
        /// first interactive fact, and the one thing a tile readout can do rather than only say
        /// (design 20 §8). Cleared every refresh and set only by <see cref="SetCellRows"/>, so a
        /// stale true cannot outlive the bed it described.
        /// </summary>
        public bool BedUnderPane => _bedUnderPane;

        bool _bedUnderPane;

        /// <summary>
        /// The cell the pane is describing, for whoever must name it back to the world — the
        /// owner picker's pick is an intent about this cell.
        /// </summary>
        public CellRef Cell => _cell;

        /// <summary>
        /// The tile's facts, one row each, in a fixed order so a fact is always in the same
        /// place: any order standing on the cell first — the actionable clause leads, the same
        /// rule <c>AlertModel</c> and the site line follow — then the work of it, what crossing
        /// it costs, the floor, and what it bears.
        /// </summary>
        void SetCellRows(WorldSnapshot snapshot, in CellDetail detail)
        {
            byte progress = 0, kind = 0;
            bool ordered = false;
            var orders = snapshot.Orders;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].CellIndex != detail.CellIndex) continue;
                ordered = true;
                kind = orders[i].Kind;
                progress = orders[i].Progress;
                break;
            }
            int orderPercent = (progress * 100 + 127) / 255;

            if (_cellRowsFor == detail.CellIndex
                && _cellRowsCost == detail.MoveCostPerMille
                && _cellRowsFloor == detail.FloorStuff
                && _cellRowsEdifice == detail.Edifice
                && _cellRowsSupport == detail.Support
                && _cellRowsWork == detail.WorkToClear
                && _cellRowsOrderKind == (ordered ? kind : 0)
                && _cellRowsOrderPercent == (ordered ? orderPercent : 0)
                && _cellRowsQuality == detail.EdificeQuality
                && _cellRowsOwner == detail.EdificeOwner) return;

            _cellRowsFor = detail.CellIndex;
            _cellRowsCost = detail.MoveCostPerMille;
            _cellRowsFloor = detail.FloorStuff;
            _cellRowsEdifice = detail.Edifice;
            _cellRowsSupport = detail.Support;
            _cellRowsWork = detail.WorkToClear;
            _cellRowsOrderKind = ordered ? kind : 0;
            _cellRowsOrderPercent = ordered ? orderPercent : 0;
            _cellRowsQuality = detail.EdificeQuality;
            _cellRowsOwner = detail.EdificeOwner;

            // Written in place, like the skills list: the count is a handful and changes rarely,
            // so the list never churns while a tile is held.
            int n = 0;
            if (ordered)
                Row(n++, OrderVerb(kind), orderPercent + "% done");
            else if (detail.WorkToClear > 0)
                Row(n++, "minable", "about " + Seconds(detail.WorkToClear) + " of work");

            // A bed's own two facts, beside what it is (design 20 §8): how well it was made, and
            // whose it is. The owner row is the pane's first interactive row — the shell turns a
            // press on it into the assign popover — so it is said even where nobody owns the bed
            // yet, because "give this to somebody" is the actionable clause and the actionable
            // clause leads.
            if (detail.EdificeQuality > 0)
            {
                _bedUnderPane = true;
                Row(n++, "quality", QualityLabels.Label(detail.EdificeQuality));
                Row(n++, "owner", detail.EdificeOwner > 0
                    ? ColonistNames.Of(new PawnId(detail.EdificeOwner))
                    : "—");
            }

            Row(n++, "walk speed", detail.MoveCostPerMille == 0
                ? "cannot walk"
                : (100_000 + detail.MoveCostPerMille / 2) / detail.MoveCostPerMille + "%");

            // The floor is said once: as the title when that is what was clicked, as a row when
            // something above it — a tree, an order — is the headline instead.
            bool floorIsTitle = EdificeLabels.Title(detail.Edifice).Length == 0
                && detail.FloorStuff != StuffHandle.None;
            if (detail.FloorStuff != StuffHandle.None && !floorIsTitle)
            {
                string stuff = BuildLabels.Stuff(detail.FloorStuff);
                Row(n++, "floor", stuff.Length == 0 ? "built" : stuff);
            }

            // Support is a solid's own fact — what the column can still bear — and is shown
            // where there is something to dig, which is where a collapse is a question.
            if (detail.WorkToClear > 0)
                Row(n++, "support", detail.Support.ToString());

            while (CellRows.Count > n) CellRows.RemoveAt(CellRows.Count - 1);
        }

        void Row(int index, string name, string value)
        {
            while (CellRows.Count <= index) CellRows.Add(new InspectRow());
            InspectRow row = CellRows[index];
            row.Name = name;
            row.Value = value;
            CellRows[index] = row;
        }

        /// <summary>
        /// The verb for an order standing on the selected cell. The numbers are
        /// <c>DesignationKind</c>'s — Mine 1, Deconstruct 2, Fell 3 — restated here because the
        /// enum lives in the simulation and <c>OrderView.Kind</c> carries only its value. Fell
        /// reads as chopping, the game's own word for it since the palette stopped saying
        /// "Harvest".
        /// </summary>
        static string OrderVerb(byte kind) => kind switch
        {
            1 => "mining",
            2 => "deconstructing",
            3 => "chopping",
            _ => "working",
        };

        /// <summary>
        /// Fill the Skills tab from the frame.
        ///
        /// <para>The list is written in place rather than cleared and refilled, because it is one
        /// row per skill for as long as a colonist is selected and the pane refreshes fifteen
        /// times a second: clearing a <see cref="List{T}"/> of structs and re-adding does not
        /// allocate, but rebuilding it does churn, and the count never changes. So the rows are
        /// laid down once at the catalogue's length and overwritten after that.</para>
        ///
        /// <para>A tombstoned colonist keeps its last-known rows, like every other field on this
        /// pane: the frame no longer carries the pawn, so the loop below finds nothing for it and
        /// leaves what was there.</para>
        /// </summary>
        void RefreshSkills(WorldSnapshot snapshot)
        {
            // In reading order, not declaration order (owner, 2026-09-17): alphabetical by the word
            // on screen, laid down so the two-column grid reads down the left and then down the
            // right. The same order feeds a candidate's card on the setup page, which is the point
            // — the two grids are looked at side by side and must not disagree.
            IReadOnlyList<SkillCatalogue.Entry> order = SkillCatalogue.ReadingOrder;

            while (Skills.Count < order.Count) Skills.Add(default);

            for (int i = 0; i < order.Count; i++)
            {
                SkillCatalogue.Entry entry = order[i];
                SkillRow row = Skills[i];
                row.IconKey = entry.Key;
                row.Name = Registry.Label(entry.Key);
                row.Live = entry.Live;
                row.Reason = entry.Reason;
                row.Note = entry.Note;
                if (!entry.Live)
                {
                    row.Level = 0;
                    row.Passion = 0;
                    row.Experience = 0;
                }
                Skills[i] = row;
            }

            if (Tombstoned) return;

            // One walk of the published aspects rather than three lookups per row, which is what
            // TryGetPawnAspect's own remarks recommend for a reader that wants every aspect of a
            // pawn: the lookup is a scan, so calling it thirty-nine times would be thirty-nine
            // scans of the same span.
            var published = snapshot.PawnAspects;
            for (int i = 0; i < published.Length; i++)
            {
                PawnAspect aspect = published[i];
                if (aspect.Pawn != Pawn) continue;
                for (int r = 0; r < order.Count; r++)
                {
                    SkillCatalogue.Entry entry = order[r];
                    if (!entry.Live) continue;

                    SkillRow row = Skills[r];
                    if (aspect.Key == entry.Level) row.Level = aspect.Value;
                    else if (aspect.Key == entry.Passion) row.Passion = aspect.Value;
                    else if (aspect.Key == entry.Experience) row.Experience = aspect.Value;
                    else continue;
                    Skills[r] = row;
                }
            }
        }

        void AddColonistTabs()
        {
            Tabs.Add(new InspectTab { Name = "Needs", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Skills", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Gear", Enabled = false, Reason = "equipment arrives with the inventory" });
            Tabs.Add(new InspectTab { Name = "Thoughts", Enabled = false, Reason = "arrives with the thought log" });
            Tabs.Add(new InspectTab { Name = "Social", Enabled = false, Reason = "M6" });
            Tabs.Add(new InspectTab { Name = "Health", Enabled = false, Reason = "M6" });
            Tabs.Add(new InspectTab { Name = "Log", Enabled = false, Reason = "M6" });
        }

        void AddColonistCommands()
        {
            Commands.Add(new InspectCommand
            {
                IconKey = "ui.command.inspect", Label = "Inspect",
                Enabled = false, Reason = "the record view arrives with the log (M6)",
            });
            Commands.Add(new InspectCommand
            {
                IconKey = "ui.command.prioritise", Label = "Prioritise",
                Enabled = false, Reason = "job priorities arrive with the work grid (M7)",
            });
            Commands.Add(new InspectCommand
            {
                IconKey = "ui.command.draft", Label = "Draft",
                Enabled = false, Reason = "combat arrives with M6",
            });
        }
    }
}
