namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// Whether the colony holds a pawn (design 60 §4a). Saved on the pawn and hashed in bits 28–29
    /// of its word, nought while <see cref="Free"/>, so a colony that never takes a prisoner saves
    /// and hashes as it always did.
    ///
    /// <para><b>A pawn's kind never changes</b>: a bandit taken prisoner is still a bandit, and one
    /// who joins the colony is a bandit whose side reads Colony. Custody is the override, and
    /// <c>Allegiance</c> is the one place that turns it into a side.</para>
    /// </summary>
    public enum PawnCustody : byte
    {
        /// <summary>Nobody's prisoner: a colonist, an animal, a bandit at large.</summary>
        Free = 0,

        /// <summary>Held by the colony, in a prison bed or on the way to one.</summary>
        Prisoner = 1,

        /// <summary>A prisoner breaking out: hostile until downed, when she is a prisoner again.</summary>
        Escaping = 2,

        /// <summary>Let go — released or exiled — and walking off the board.</summary>
        Released = 3,
    }

    /// <summary>
    /// What the colony means to do with a prisoner (design 60 §8, §13), set on the prisoner tab.
    /// Saved in the prisoner's record. Appended only: the numbers are saved.
    /// </summary>
    public enum PrisonMode : byte
    {
        /// <summary>Feed her, tend her, keep her. Every prisoner starts here.</summary>
        Hold = 0,

        /// <summary>A warden talks to her, and she joins when her willingness is full.</summary>
        Recruit = 1,

        /// <summary>A warden walks her out and she leaves the board; an arrested colonist goes back to the colony.</summary>
        Release = 2,

        /// <summary>She is let go at the cell door and walks off alone.</summary>
        Exile = 3,

        /// <summary>Traded back to her own people. Reserved: refused until factions exist (M7).</summary>
        Ransom = 4,
    }
}
