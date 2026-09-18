#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One roster card's content (A2 in the panel catalogue): identity, mood, current activity,
    /// and which layer they are on. The layer is not decoration — on a layered map the roster
    /// bar is the player's primary answer to "where is everyone".
    /// </summary>
    public struct RosterCard
    {
        public PawnId Id;
        public uint Seed;
        public string Name;
        public int Mood;        // 0..1000, the simulation's own scale, as food and rest are
        public int Food;        // 0..1000
        public int Rest;        // 0..1000
        public int JobDef;      // JobHandle value, -1 idle
        public int Layer;       // Cell.Y
        public bool Selected;
    }

    /// <summary>
    /// Builds the roster from a published frame. Rows are written into a reused list, so a
    /// refresh costs no allocation once the colony stops growing — the interface's steady-state
    /// rule, applied from the first version rather than retrofitted.
    ///
    /// Supports pagination across pages of cards and persistent player-directed slot ordering.
    /// </summary>
    public sealed class RosterModel
    {
        public readonly List<RosterCard> Cards = new List<RosterCard>();
        readonly List<PawnId> _customOrder = new List<PawnId>();

        public IReadOnlyList<PawnId> CustomOrder => _customOrder;

        public int Page { get; private set; }
        public int PageCapacity { get; set; } = int.MaxValue;
        public int TotalCount => _customOrder.Count;
        public int PageCount => PageCapacity <= 0 || TotalCount <= 0 ? 1 : Math.Max(1, (TotalCount + PageCapacity - 1) / PageCapacity);

        public void SetPage(int page)
        {
            int maxPage = Math.Max(0, PageCount - 1);
            Page = Math.Max(0, Math.Min(page, maxPage));
        }

        public bool EnsurePageFor(PawnId pawnId)
        {
            if (!pawnId.IsValid) return false;
            int idx = _customOrder.IndexOf(pawnId);
            if (idx < 0) return false;
            int targetPage = PageCapacity > 0 ? idx / PageCapacity : 0;
            SetPage(targetPage);
            return true;
        }

        public bool Swap(PawnId a, PawnId b)
        {
            if (a == b || !a.IsValid || !b.IsValid) return false;
            int idxA = _customOrder.IndexOf(a);
            int idxB = _customOrder.IndexOf(b);
            if (idxA < 0 || idxB < 0) return false;
            _customOrder[idxA] = b;
            _customOrder[idxB] = a;
            return true;
        }

        public bool SwapIndices(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= _customOrder.Count || indexB < 0 || indexB >= _customOrder.Count || indexA == indexB)
                return false;
            PawnId tmp = _customOrder[indexA];
            _customOrder[indexA] = _customOrder[indexB];
            _customOrder[indexB] = tmp;
            return true;
        }

        public void LoadOrder(IEnumerable<PawnId> order, int page = 0)
        {
            _customOrder.Clear();
            foreach (PawnId id in order)
            {
                if (id.IsValid && !_customOrder.Contains(id))
                    _customOrder.Add(id);
            }
            SetPage(page);
        }

        public void Refresh(WorldSnapshot snapshot, PawnId selected, int capacity = int.MaxValue) =>
            Refresh(snapshot, selected.IsValid ? new[] { selected } : Array.Empty<PawnId>(), capacity);

        /// <summary>
        /// Marks a card selected for every member of a multi-selection, so the roster bar answers
        /// a drag box on the world the same frame the brackets do.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId> selected, int capacity = int.MaxValue)
        {
            PageCapacity = Math.Max(1, capacity);

            // Reconcile custom order: remove dead/despawned pawns, append new pawns.
            for (int i = _customOrder.Count - 1; i >= 0; i--)
            {
                if (!snapshot.TryGetPawn(_customOrder[i], out _))
                    _customOrder.RemoveAt(i);
            }

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnId id = pawns[i].Id;
                if (!_customOrder.Contains(id))
                    _customOrder.Add(id);
            }

            // Clamp active page
            SetPage(Page);

            Cards.Clear();
            int start = Page * PageCapacity;
            int end = Math.Min(start + PageCapacity, _customOrder.Count);

            for (int i = start; i < end; i++)
            {
                PawnId id = _customOrder[i];
                if (!snapshot.TryGetPawn(id, out PawnView pawn)) continue;

                bool isSelected = false;
                for (int s = 0; s < selected.Count; s++)
                {
                    if (selected[s] == id) { isSelected = true; break; }
                }

                uint seed = ColonistNames.RollSeedOf(snapshot, pawn.Id);

                Cards.Add(new RosterCard
                {
                    Id = pawn.Id,
                    Seed = seed,
                    Name = ColonistNames.Of(seed, pawn.Id),
                    Mood = pawn.Mood,
                    Food = pawn.Food,
                    Rest = pawn.Rest,
                    JobDef = pawn.JobDef,
                    Layer = pawn.Cell.Y,
                    Selected = isSelected,
                });
            }
        }
    }

    /// <summary>
    /// The three display bands of the mood bar. These are interface-side reading aids, not
    /// simulation thresholds: the break threshold is simulation content and is not published,
    /// so the bar says "this one looks unhappy" rather than claiming a number it was not given.
    /// </summary>
    public static class MoodBands
    {
        /// <summary>
        /// Thousandths, like every other need the frame publishes.
        ///
        /// These read 60 and 35 until 2026-09-16, against a mood the simulation keeps from 0 to
        /// 1000 and starts a colonist at 600. Nothing in the interface had ever shown a mood
        /// correctly as a result: every bar was clamped to a hundred and therefore drawn full,
        /// every colonist was described as "content" whatever had happened to them, and the
        /// red low-mood state could not be reached at all, because it wanted a value under 35
        /// and the lowest a colonist can actually reach is 0 — which is to say it would only
        /// ever have fired on a colonist already at the very bottom. The fixtures agreed with
        /// the bug: the model tests passed moods of 80 and 30, which is not a scale the game
        /// ever produces.
        /// </summary>
        public const int Content = 600;
        public const int Strained = 350;

        /// <summary>Content, Strained or Breaking — the band name, for the inspect pane.</summary>
        public static string Band(int mood) =>
            mood >= Content ? "content" : mood >= Strained ? "strained" : "breaking";
    }
}
