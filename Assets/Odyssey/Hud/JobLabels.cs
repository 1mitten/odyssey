#nullable enable
namespace Odyssey.Hud
{
    /// <summary>
    /// Job driver indices, as the snapshot carries them, turned into words and icon keys. The
    /// index table itself is <c>Odyssey.Sim.Pawns.JobIndex</c>; the labels live here because a
    /// string per pawn per tick would allocate in the publish phase, and presentation is where
    /// indices become words (and where localisation belongs when it arrives).
    /// </summary>
    public static class JobLabels
    {
        /// <summary>Parallel to <c>JobIndex</c>: Haul, Eat, Sleep, Wander, Wait.</summary>
        public static readonly string[] Labels = { "hauling", "eating", "sleeping", "wandering", "waiting", "felling" };

        /// <summary>Symbolic icon keys. Wandering and waiting both read as idle at a glance,
        /// which is true, and <c>ui.status.idle</c> is the key that says so.</summary>
        public static readonly string[] IconKeys =
        {
            "ui.status.hauling", "ui.status.eating", "ui.status.sleeping",
            "ui.status.idle", "ui.status.idle", "ui.status.felling",
        };

        public static string Label(int jobDef) =>
            jobDef >= 0 && jobDef < Labels.Length ? Labels[jobDef] : "idle";

        public static string IconKey(int jobDef) =>
            jobDef >= 0 && jobDef < IconKeys.Length ? IconKeys[jobDef] : "ui.status.idle";
    }
}
