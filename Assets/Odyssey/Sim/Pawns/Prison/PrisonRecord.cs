#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Everything the colony remembers about a pawn it holds, held, or means to take (design 59
    /// §4a). <b>Sparse</b>: a pawn carries one only once something has been said about it, so a
    /// colony that never takes a prisoner has none, saves none (<c>odyssey.prison</c> writes a count
    /// of nought) and hashes none.
    ///
    /// <para>Custody itself lives on <see cref="Pawn.Custody"/>, in the pawn's own hash word; this
    /// holds the rest. A recruit keeps a record with <see cref="Joined"/> set for ever, because her
    /// kind never changes and that flag is what makes her side read Colony.</para>
    /// </summary>
    public sealed class PrisonRecord
    {
        /// <summary>Recruited: her side reads Colony from now on, whatever her kind says.</summary>
        public bool Joined;

        /// <summary>Laid in a prison bed, so she wears the jumpsuit until she is free again (design 59 §11d).</summary>
        public bool Dressed;

        /// <summary>A colonist when she was taken (design 59 §10): released, she goes back to the colony.</summary>
        public bool Arrested;

        /// <summary>A downed pawn the player has asked a warden to bring in (design 59 §7).</summary>
        public bool CaptureMark;

        /// <summary>What the colony means to do with her. <see cref="PrisonMode.Hold"/> on arrival.</summary>
        public PrisonMode Mode;

        /// <summary>How far talked round, 0 to 1,000,000 (design 59 §8).</summary>
        public int Willingness;

        /// <summary>The tick a warden last talked to her, or 0 for never.</summary>
        public int LastChatTick;

        /// <summary>Whether this record still says anything, or can be dropped.</summary>
        public bool IsEmpty =>
            !Joined && !Dressed && !Arrested && !CaptureMark && Mode == PrisonMode.Hold
            && Willingness == 0 && LastChatTick == 0;

        /// <summary>Every field, in a fixed order: saved state that is not derived belongs in the hash.</summary>
        public void ContributeTo(ref StateHash hash)
        {
            hash.Add((byte)((Joined ? 1 : 0) | (Dressed ? 2 : 0) | (Arrested ? 4 : 0) | (CaptureMark ? 8 : 0)));
            hash.Add((byte)Mode);
            hash.Add(Willingness);
            hash.Add(LastChatTick);
        }
    }
}
