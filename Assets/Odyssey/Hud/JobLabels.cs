#nullable enable
namespace Odyssey.Hud
{
    /// <summary>
    /// Job driver indices, as the snapshot carries them, turned into icon keys and words. The
    /// index table itself is <c>Odyssey.Sim.Pawns.JobIndex</c>; the keys live here because a
    /// string per pawn per tick would allocate in the publish phase, and presentation is where
    /// indices become names. The words themselves come from <see cref="Registry"/>: this class
    /// knows which key a job is, never what the key is called.
    /// </summary>
    public static class JobLabels
    {
        const string Idle = "ui.status.idle";

        /// <summary>Parallel to <c>JobIndex</c>: Haul, Eat, Sleep, Wander, Wait, Fell. Wandering
        /// and waiting both read as idle at a glance, which is true, and <c>ui.status.idle</c> is
        /// the key that says so.</summary>
        public static readonly string[] IconKeys =
        {
            "ui.status.hauling", "ui.status.eating", "ui.status.sleeping",
            Idle, Idle, "ui.status.felling",
        };

        public static string IconKey(int jobDef) =>
            jobDef >= 0 && jobDef < IconKeys.Length ? IconKeys[jobDef] : Idle;

        public static string Label(int jobDef) => Registry.Label(IconKey(jobDef));
    }
}
