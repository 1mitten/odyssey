#nullable enable
namespace Odyssey.Hud
{
    /// <summary>
    /// Which of its pack's colourways an animal wears (design 66 §4).
    ///
    /// <para><b>Presentation alone.</b> A colourway is a different mesh over one palette, not a
    /// different material, and nothing in the simulation may read it: it is dealt here from the
    /// pawn's id, so it needs no saved field, moves no hash and cannot disagree between the
    /// drawers. The live figure, the far form and any portrait all call this one function, which
    /// is why an animal cannot change coat on crossing the figure ceiling — the fault
    /// <c>ColonistAppearance</c>'s header warns about.</para>
    ///
    /// <para>Engine-free, so the fast tier holds it.</para>
    /// </summary>
    public static class AnimalLooks
    {
        /// <summary>Its own stream, so a colourway never correlates with anything else dealt from an id.</summary>
        const uint ColourwayStream = 0x3C6EF372u;

        /// <summary>
        /// The colourway, <c>0</c> to <paramref name="count"/> − 1, for this pawn. Stable for the
        /// life of the pawn and across a load, since a pawn's id is. A count of one or fewer is
        /// always <c>0</c>.
        /// </summary>
        public static int Colourway(int pawnId, int count)
        {
            if (count <= 1) return 0;
            return (int)(Mix(pawnId, ColourwayStream) % (uint)count);
        }

        /// <summary>One avalanche over (pawn, stream). Integers only, unchecked, no float.</summary>
        static uint Mix(int pawnId, uint stream)
        {
            unchecked
            {
                uint h = stream;
                h ^= (uint)pawnId * 2654435761u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
