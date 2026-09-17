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
                Title = "Ground";
                Subtitle = "cell";
                SetPosition(_cell);
                Layer = _cell.Y;
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
            while (Skills.Count < SkillCatalogue.All.Length) Skills.Add(default);

            for (int i = 0; i < SkillCatalogue.All.Length; i++)
            {
                SkillCatalogue.Entry entry = SkillCatalogue.All[i];
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
                for (int r = 0; r < SkillCatalogue.All.Length; r++)
                {
                    SkillCatalogue.Entry entry = SkillCatalogue.All[r];
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
