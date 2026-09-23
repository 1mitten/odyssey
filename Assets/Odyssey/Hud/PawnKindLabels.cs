#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a pawn's kind is called, and what an animal is doing, by registry key (design 29
    /// §2, §8). Parallel to the simulation's <c>PawnKindIndex</c> exactly as
    /// <see cref="JobLabels"/> is parallel to its <c>JobIndex</c>: the colonist is 0, the midden
    /// hog 1, the duct rat 2, appended and never inserted. A kind past the table reads as an
    /// animal with the generic label, never as a colonist — the failure that would put a hog on
    /// the roster is the one this must not have.
    /// </summary>
    public static class PawnKindLabels
    {
        public const string Colonist = "ui.pawn.colonist";
        const string Animal = "ui.pawn.animal";

        public static readonly string[] IconKeys =
        {
            Colonist, "ui.pawn.hog", "ui.pawn.rat",
        };

        /// <summary>The two states an animal's mind has (design 29 §3), by the job it is running.</summary>
        public const string Wandering = "ui.status.wandering";

        public const string Resting = "ui.status.resting";

        public static bool IsAnimal(int kind) => kind != 0;

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
