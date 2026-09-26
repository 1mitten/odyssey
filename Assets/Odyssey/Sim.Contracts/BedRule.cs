namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// Who a bed is for (design 58 §5). A colony bed is the default; a prison bed is one the player
    /// marked, or one standing in a room with a marked bed in it.
    /// </summary>
    public enum BedPurpose : byte
    {
        Colony = 0,
        Prison = 1,
    }

    /// <summary>
    /// Which pool of beds a pawn sleeps from (design 58 §5a). <see cref="None"/> is everybody who
    /// never sleeps in a bed: an animal, a bandit at large, a pawn leaving the board.
    /// </summary>
    public enum BedUser : byte
    {
        None = 0,
        Colonist = 1,
        Prisoner = 2,
    }

    /// <summary>
    /// <b>The one rule for who may use and own a bed</b> (design 58 §5a).
    ///
    /// <para>Before it there were four near-copies of "my own bed, else the nearest bed nobody
    /// owns" in the simulation — the sleep chooser, the patient's, the rescue's and the sleeper's
    /// claim — and a fifth in the owner picker, which offered hogs and bandits as owners. Adding
    /// "unless it is a prison bed" to five places is the fault this project calls "one rule with
    /// two owners" (<c>docs/bug-patterns.md</c> P1), so the rule lives here, in the contracts both
    /// the simulation and the interface compile against, and every chooser asks it.</para>
    ///
    /// <para>Pure data in, answer out: who the pawn is, which bed pool it belongs to, the bed's
    /// purpose and its owner. The simulation answers <see cref="BedUser"/> from the pawn
    /// (<c>BedRules.UserOf</c>) and the interface from the published view (<see cref="UserOf"/>);
    /// <c>BedRulesTests</c> holds the two to the same answer for every pawn in a colony.</para>
    /// </summary>
    public static class BedRule
    {
        /// <summary>Whether a pawn from this pool belongs in a bed of this purpose at all.</summary>
        public static bool Fits(BedUser user, BedPurpose purpose) =>
            (user == BedUser.Colonist && purpose == BedPurpose.Colony)
            || (user == BedUser.Prisoner && purpose == BedPurpose.Prison);

        /// <summary>
        /// Whether <paramref name="pawnId"/> may sleep, heal or be laid in this bed: it is the
        /// right kind of bed for them, and it is theirs or nobody's. Nobody sleeps in another's bed,
        /// which is the whole of what ownership is (design 20 §7).
        /// </summary>
        public static bool MayUse(BedUser user, int pawnId, BedPurpose purpose, int owner) =>
            Fits(user, purpose) && (owner == 0 || owner == pawnId);

        /// <summary>Whether a pawn from this pool may be given this bed as its own.</summary>
        public static bool MayOwn(BedUser user, BedPurpose purpose) => Fits(user, purpose);

        /// <summary>
        /// The pool a published pawn sleeps from, as the interface sees it: a colonist's, a
        /// prisoner's — held or breaking out, so an escapee keeps the bed she will be carried back
        /// to — or none: an animal, a bandit at large, a pawn let go.
        /// </summary>
        public static BedUser UserOf(in PawnView view) =>
            view.IsColonist ? BedUser.Colonist
            : view.Custody == PawnCustody.Prisoner || view.Custody == PawnCustody.Escaping ? BedUser.Prisoner
            : BedUser.None;
    }
}
