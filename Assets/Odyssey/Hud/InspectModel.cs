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

            if (Subject == InspectSubject.Colonist)
            {
                if (snapshot.TryGetPawn(Pawn, out PawnView pawn))
                {
                    Tombstoned = false;
                    Title = ColonistNames.Of(pawn.Id);
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
                    Title = thing.DefIndex == ItemHandle.Meal ? "Meal" : "Salvage";
                    Subtitle = "item";
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
                if (!DescribeSiteAt(snapshot, _cell))
                {
                    Title = "Ground";
                    Subtitle = "cell";
                    Site = string.Empty;
                    SiteIconKey = string.Empty;
                }

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

        void AddColonistTabs()
        {
            Tabs.Add(new InspectTab { Name = "Needs", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Skills", Enabled = false, Reason = "no skill data yet" });
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
