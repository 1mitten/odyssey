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
    /// </summary>
    public sealed class RosterModel
    {
        public readonly List<RosterCard> Cards = new List<RosterCard>();

        public void Refresh(WorldSnapshot snapshot, PawnId selected) =>
            Refresh(snapshot, selected.IsValid ? new[] { selected } : Array.Empty<PawnId>());

        /// <summary>
        /// Marks a card selected for every member of a multi-selection, so the roster bar answers
        /// a drag box on the world the same frame the brackets do.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId> selected)
        {
            Cards.Clear();
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                bool isSelected = false;
                for (int s = 0; s < selected.Count; s++)
                    if (selected[s] == pawn.Id) { isSelected = true; break; }
                Cards.Add(new RosterCard
                {
                    Id = pawn.Id,
                    Name = ColonistNames.Of(pawn.Id),
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
