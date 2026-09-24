#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a pawn's kind is called, and what an animal is doing, by registry key (design 29
    /// §2, §8). Parallel to the simulation's <c>PawnKindIndex</c> exactly as
    /// <see cref="JobLabels"/> is parallel to its <c>JobIndex</c>: the colonist is 0, the midden
    /// hog 1, the duct rat 2, the bandit 3, appended and never inserted. A kind past the table
    /// reads with the generic animal label.
    ///
    /// <para><b>Whether a pawn is an animal is not this table's question any more</b> (design 33
    /// §5): a bandit is kind 3 and a person. <see cref="IsAnimal"/> reads the view's flags, and
    /// the kind is only which row names it.</para>
    /// </summary>
    public static class PawnKindLabels
    {
        public const string Colonist = "ui.pawn.colonist";

        /// <summary>
        /// The kind indices, parallel to the simulation's <c>PawnKindIndex</c> (which this assembly
        /// cannot see) as <see cref="IconKeys"/> is. What the debug Spawn tab sends
        /// (<see cref="DebugDirector.SpawnRows"/>); the icon table's order is what a test holds.
        /// </summary>
        public const int ColonistKind = 0, MiddenHogKind = 1, DuctRatKind = 2, Bandit = 3;
        const string Animal = "ui.pawn.animal";

        public static readonly string[] IconKeys =
        {
            Colonist, "ui.pawn.hog", "ui.pawn.rat",
            // The debug-spawned hostile person (design 33 §1).
            "ui.pawn.bandit",
        };

        /// <summary>The two states an animal's mind has (design 29 §3), by the job it is running.</summary>
        public const string Wandering = "ui.status.wandering";

        public const string Resting = "ui.status.resting";

        /// <summary>An animal, by the flags the simulation published (design 33 §5), never by the kind.</summary>
        public static bool IsAnimal(in PawnView view) => view.IsAnimal;

        public static string IconKey(int kind) =>
            kind >= 0 && kind < IconKeys.Length ? IconKeys[kind] : Animal;

        public static string Label(int kind) => Registry.Label(IconKey(kind));

        /// <summary>
        /// An animal's activity line. A wander is a leg and a wait is a rest; anything else an
        /// animal is somehow doing reads as what a colonist's would.
        /// </summary>
        public static string ActivityKey(int jobDef) =>
            jobDef == JobHandle.Wander ? Wandering
            : jobDef == JobHandle.Wait ? Resting
            : JobLabels.IconKey(jobDef);

        public static string Activity(int jobDef) => Registry.Label(ActivityKey(jobDef));
    }
}
