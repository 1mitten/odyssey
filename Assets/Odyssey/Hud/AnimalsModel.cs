#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>One animal on the board, as the Animals tab lists it (design 30 §6).</summary>
    public struct AnimalRow
    {
        public PawnId Id;
        public int Kind;
        public string KindKey;
        public string ActivityKey;
        public int Layer;

        /// <summary>Cells from the colony, Chebyshev, where the colony is where its people stand. Not drawn; it orders the rows.</summary>
        public int Away;

        public bool Selected;
    }

    /// <summary>How many of a kind are on the board: the count strip.</summary>
    public struct AnimalCount
    {
        public int Kind;
        public string KindKey;
        public int Count;
    }

    /// <summary>The column a click on a header sorts by. Distance breaks every tie.</summary>
    public enum AnimalsSort
    {
        Kind = 0,
        Doing = 1,
    }

    /// <summary>
    /// What is out there (design 30 §6): every animal in the frame, by kind and then by how far
    /// it is from the colony, with a count per kind above and a page of rows below. Unity-free by
    /// construction (ADR 0003), so the whole of it runs in the fast tier.
    ///
    /// <para><b>The colony is where its people stand.</b> The rows are ordered by distance, and
    /// "from what" has to be answered by the frame alone: the mean of the colonists' cells is a
    /// point the frame carries, the start cell is not, and a colony that has moved house has
    /// moved its animals' distances with it. With no colonists the distance is from the board's
    /// origin, which is a number rather than a lie.</para>
    ///
    /// <para><b>Paged, never scrolled</b>, for the reason the Work tab's rows are: a page of
    /// twelve is a bounded number of elements whatever the board carries, and the level-keeper
    /// caps the board at twenty-four anyway.</para>
    /// </summary>
    public sealed class AnimalsModel
    {
        /// <summary>Every animal, in the current sort.</summary>
        public readonly List<AnimalRow> All = new List<AnimalRow>();

        /// <summary>The current page of <see cref="All"/>.</summary>
        public readonly List<AnimalRow> Rows = new List<AnimalRow>();

        /// <summary>One entry per kind on the board, ascending by kind.</summary>
        public readonly List<AnimalCount> Counts = new List<AnimalCount>();

        public int Page { get; private set; }

        public int PageCapacity { get; set; } = AnimalsLayout.RowsPerPage;

        public AnimalsSort Sort { get; private set; } = AnimalsSort.Kind;

        public int TotalCount => All.Count;

        public int PageCount => PageCapacity <= 0 || TotalCount <= 0 ? 1 : Math.Max(1, (TotalCount + PageCapacity - 1) / PageCapacity);

        /// <summary>The colony's centre as last computed: the mean colonist cell, x and z.</summary>
        public int HomeX { get; private set; }

        public int HomeZ { get; private set; }

        readonly List<AnimalRow> _scratch = new List<AnimalRow>();

        public void SetPage(int page)
        {
            int clamped = Math.Max(0, Math.Min(page, PageCount - 1));
            if (clamped == Page) return;
            Page = clamped;
            Slice();
        }

        /// <summary>Sort by a column. True if the order changed; the caller refreshes.</summary>
        public bool SortBy(AnimalsSort sort)
        {
            if (Sort == sort) return false;
            Sort = sort;
            All.Sort(Comparer());
            Slice();
            return true;
        }

        /// <summary>Bring the page that holds this animal up. False if it is not on the board.</summary>
        public bool EnsurePageFor(PawnId id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id.Value != id.Value) continue;
                SetPage(PageCapacity <= 0 ? 0 : i / PageCapacity);
                return true;
            }
            return false;
        }

        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId> selected)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            long sumX = 0, sumZ = 0;
            int people = 0;
            foreach (PawnView view in snapshot.Pawns)
            {
                // The colony's home is its own people (design 33 §5): not the animals, and not a
                // marauder standing in the middle of it.
                if (!view.IsColonist) continue;
                sumX += view.Cell.X;
                sumZ += view.Cell.Z;
                people++;
            }
            HomeX = people == 0 ? 0 : (int)(sumX / people);
            HomeZ = people == 0 ? 0 : (int)(sumZ / people);

            _scratch.Clear();
            foreach (PawnView view in snapshot.Pawns)
            {
                if (!PawnKindLabels.IsAnimal(view)) continue;
                _scratch.Add(new AnimalRow
                {
                    Id = view.Id,
                    Kind = view.Kind,
                    KindKey = PawnKindLabels.IconKey(view.Kind),
                    ActivityKey = PawnKindLabels.ActivityKey(view.JobDef),
                    Layer = view.Cell.Y,
                    Away = Math.Max(Math.Abs(view.Cell.X - HomeX), Math.Abs(view.Cell.Z - HomeZ)),
                    Selected = IsSelected(selected, view.Id),
                });
            }
            _scratch.Sort(Comparer());

            All.Clear();
            All.AddRange(_scratch);

            Counts.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                int at = IndexOfCount(All[i].Kind);
                if (at >= 0)
                {
                    AnimalCount count = Counts[at];
                    count.Count++;
                    Counts[at] = count;
                }
                else
                {
                    Counts.Add(new AnimalCount { Kind = All[i].Kind, KindKey = All[i].KindKey, Count = 1 });
                }
            }
            Counts.Sort((a, b) => a.Kind.CompareTo(b.Kind));

            if (Page > PageCount - 1) Page = PageCount - 1;
            Slice();
        }

        int IndexOfCount(int kind)
        {
            for (int i = 0; i < Counts.Count; i++) if (Counts[i].Kind == kind) return i;
            return -1;
        }

        Comparison<AnimalRow> Comparer() => Sort == AnimalsSort.Doing ? ByDoingThenDistance : ByKindThenDistance;

        static readonly Comparison<AnimalRow> ByKindThenDistance = (a, b) =>
        {
            int kind = a.Kind.CompareTo(b.Kind);
            if (kind != 0) return kind;
            return ThenDistance(a, b);
        };

        static readonly Comparison<AnimalRow> ByDoingThenDistance = (a, b) =>
        {
            int doing = string.CompareOrdinal(a.ActivityKey, b.ActivityKey);
            if (doing != 0) return doing;
            int kind = a.Kind.CompareTo(b.Kind);
            if (kind != 0) return kind;
            return ThenDistance(a, b);
        };

        static int ThenDistance(AnimalRow a, AnimalRow b)
        {
            int away = a.Away.CompareTo(b.Away);
            return away != 0 ? away : a.Id.Value.CompareTo(b.Id.Value);
        }

        static bool IsSelected(IReadOnlyList<PawnId> selected, PawnId id)
        {
            for (int i = 0; i < selected.Count; i++) if (selected[i].Value == id.Value) return true;
            return false;
        }

        void Slice()
        {
            Rows.Clear();
            if (PageCapacity <= 0) { Rows.AddRange(All); return; }
            int from = Page * PageCapacity;
            for (int i = from; i < All.Count && i < from + PageCapacity; i++) Rows.Add(All[i]);
        }
    }
}
