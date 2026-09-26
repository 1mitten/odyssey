#nullable enable
using Odyssey.Sim.Contracts;

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
            // The draft's two jobs (design 33 §2c): holding and walking to an order both read as
            // drafted, because what the player needs from the line is that this colonist is
            // theirs to command and not the work list's.
            "ui.status.drafted", "ui.status.drafted",
            // Power (design 32): laying a line is building, taking one up is deconstructing, and
            // feeding a generator has a word of its own.
            "ui.status.building", "ui.status.deconstructing", "ui.status.refuelling",
            // The combat line's five (design 33 §5), in JobHandle order 17 to 21, after power's three: attacking,
            // fleeing, lying downed, fetching a weapon, carrying the downed to bed.
            "ui.status.fighting", "ui.status.fleeing", "ui.status.downed",
            "ui.status.equipping", "ui.status.rescuing",
            // A bandit carrying something off the board (design 33 §17), JobHandle 22. With a
            // load in its arms the line reads "Stealing · Meal × 12" by Carrying below.
            "ui.status.stealing",
            // Medical supplies (design 37): Job_Treat and Job_Patient, 23 and 24, after Steal.
            "ui.status.treating", "ui.status.patient",
            // Picking a berry bush (design 45 §6), JobHandle 25, after medical supplies.
            "ui.status.foraging",
            // The kitchen (design 48): Job_Cook, 26 — fetching food for the pan reads as cooking,
            // because it is the bill being worked.
            "ui.status.cooking",
            // The ranged attack (design 47 §2d), JobHandle 27, after the kitchen's: a word of its own rather than
            // "Fighting", so a line of shooters reads as shooting.
            "ui.status.shooting",
            // The negotiator (design 65 §6), JobHandle 28: walking to a trader and trading.
            "ui.status.trading",
        };

        public static string IconKey(int jobDef) =>
            jobDef >= 0 && jobDef < IconKeys.Length ? IconKeys[jobDef] : Idle;

        public static string Label(int jobDef) => Registry.Label(IconKey(jobDef));

        /// <summary>
        /// The activity line for a colonist with something in her arms: what she is doing, then
        /// what she is holding and how much of it. Design 24 §8.
        ///
        /// <para><b>This is where the amount lives now.</b> The load is drawn in her arms as a
        /// constant armful whatever the stack (design 24 §3a), so the board no longer says how
        /// much anybody is carrying and this line is the only thing that does. That is the trade
        /// the owner took, and it is the reason this is on the activity line rather than tucked
        /// into a tab: the line is already on the pane's face.</para>
        ///
        /// <para><b>Both names come from <see cref="Registry"/>, and only the punctuation does
        /// not.</b> The rule <c>RegistryTests.NoPlayerFacingNameIsWrittenInCSharp</c> enforces is
        /// that a thing's <em>name</em> may not be written here, or the wiki and the screen
        /// disagree the first time somebody corrects one of the two copies. A separator and a
        /// multiplication sign name nothing and belong to the layout.</para>
        ///
        /// <para>Allocates, so callers cache it against the three values it is built from —
        /// <c>InspectModel</c> refreshes fifteen times a second and ADR 0003's flip condition F1
        /// forbids a string per refresh.</para>
        /// </summary>
        /// <summary>
        /// The name the simulation publishes a carried load's item def under.
        ///
        /// <para>A string literal and not a shared constant, on exactly the bargain
        /// <see cref="ColonistNames.RollSeedAspect"/> makes and for the same reason: this assembly
        /// cannot reference <c>Odyssey.Sim</c> at all, and a constant both sides imported would be
        /// the shared file the <c>PawnAspect</c> seam exists to avoid. Tests on both sides hold
        /// the two spellings together.</para>
        /// </summary>
        public const string CarryingAspect = "odyssey.pawn.carrying";

        /// <summary>And how many are in it. See <see cref="CarryingAspect"/>.</summary>
        public const string CarryStackAspect = "odyssey.pawn.carrying.stack";

        static readonly AspectKey CarryingKey = AspectKey.Of(CarryingAspect);
        static readonly AspectKey CarryStackKey = AspectKey.Of(CarryStackAspect);

        /// <summary>
        /// What this colonist has in her arms, or def -1 for empty-handed.
        ///
        /// <para>Absence is the answer rather than a sentinel: a pawn carrying nothing publishes
        /// no row, which is what makes the aspect sparse and free.</para>
        /// </summary>
        public static void CarriedBy(WorldSnapshot snapshot, PawnId id, out int def, out int stack)
        {
            stack = 0;
            if (!snapshot.TryGetPawnAspect(id, CarryingKey, out def)) { def = -1; return; }
            if (!snapshot.TryGetPawnAspect(id, CarryStackKey, out stack)) stack = 1;
        }

        public static string Carrying(int jobDef, int carriedDef, int stack)
        {
            string doing = Label(jobDef);
            if (carriedDef < 0) return doing;

            string load = ItemLabels.Label(carriedDef);
            return stack > 1 ? doing + " · " + load + " × " + stack : doing + " · " + load;
        }
    }
}
