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

        // ---- colonist body, the Needs tab
        public string Job = "idle";
        public string JobIconKey = "ui.status.idle";
        public int Food;
        public int Rest;      // 0..1000, the simulation's scale
        public int Mood;      // 0..100

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
                    Position = $"at {pawn.Cell.X}, {pawn.Cell.Z}";
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
                    Position = $"at {thing.Cell.X}, {thing.Cell.Z}";
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
                Position = $"at {_cell.X}, {_cell.Z}";
                Layer = _cell.Y;
                return;
            }

            // Nothing selected: the colony as a whole, which answers "is anything happening".
            Title = "The holding";
            Subtitle = "nothing selected";
            Position = string.Empty;
            Layer = -1;
            ColonySize = snapshot.PawnCount;
            JobCounts.Clear();
            for (int job = 0; job < JobHandle.Count; job++) JobCounts.Add(0);
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
                if (pawns[i].JobDef >= 0 && pawns[i].JobDef < JobHandle.Count)
                    JobCounts[pawns[i].JobDef]++;
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
