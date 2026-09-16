#nullable enable
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
        public int Mood;        // 0..100, the simulation's own scale
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

        public void Refresh(WorldSnapshot snapshot, PawnId selected)
        {
            Cards.Clear();
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                Cards.Add(new RosterCard
                {
                    Id = pawn.Id,
                    Name = ColonistNames.Of(pawn.Id),
                    Mood = pawn.Mood,
                    Food = pawn.Food,
                    Rest = pawn.Rest,
                    JobDef = pawn.JobDef,
                    Layer = pawn.Cell.Y,
                    Selected = pawn.Id == selected,
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
        public const int Content = 60;
        public const int Strained = 35;

        /// <summary>Content, Strained or Breaking — the band name, for the inspect pane.</summary>
        public static string Band(int mood) =>
            mood >= Content ? "content" : mood >= Strained ? "strained" : "breaking";
    }
}
