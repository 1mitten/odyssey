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

        /// <summary>Parallel to <c>JobIndex</c>: Haul, Eat, Sleep, Wander, Wait, Fell, Mine,
        /// Deliver, Build. Wandering and waiting both read as idle at a glance, which is true, and
        /// <c>ui.status.idle</c> is the key that says so.
        ///
        /// <para>Delivering material to a site reads as <b>hauling</b>, because from across the
        /// board that is exactly what it looks like and what the player cares about is that
        /// something is being carried. The simulation counts it as construction — that is about who
        /// gets better at it, which is a different question from what it looks like.</para>
        ///
        /// <para><b>Two entries short is not a compile error, it is a colonist reading as idle.</b>
        /// The bounds check below turns an unlisted job into <c>ui.status.idle</c>, so when the
        /// build pipeline added two job indices every builder and every porter in the game showed
        /// as having nothing to do. <c>RegistryTests</c> holds the length to <c>JobHandle.Count</c>
        /// now, so the next job cannot arrive quietly.</para>
        /// </summary>
        public static readonly string[] IconKeys =
        {
            "ui.status.hauling", "ui.status.eating", "ui.status.sleeping",
            Idle, Idle, "ui.status.felling", "ui.status.mining",
            "ui.status.hauling", "ui.status.building", "ui.status.deconstructing",
            "ui.status.sowing", "ui.status.harvesting",
        };

        public static string IconKey(int jobDef) =>
            jobDef >= 0 && jobDef < IconKeys.Length ? IconKeys[jobDef] : Idle;

        public static string Label(int jobDef) => Registry.Label(IconKey(jobDef));
    }
}
