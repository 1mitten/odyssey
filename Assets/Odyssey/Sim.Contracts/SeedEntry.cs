#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// The seed a new game starts from, in the three forms a start screen needs it: drawn,
    /// written out for the player to read, and read back from what they typed.
    ///
    /// <para><b>Why this is in the simulation and not in the screen.</b> A seed is the one number
    /// the whole world comes from (<c>ColonyRequest.Seed</c>), so what counts as a seed — and what
    /// counts as a player having typed one — belongs beside the number, not beside the text field.
    /// It also means the rules are testable in the fast tier, which is where a start screen's
    /// rules will never be.</para>
    ///
    /// <para><b>This is the one place in the simulation assemblies that is deliberately not
    /// deterministic</b>, and it is confined to <see cref="Draw()"/> and <see cref="Reroll(uint)"/>.
    /// Drawing a new game's seed is exactly the moment fresh randomness is correct; everything
    /// downstream of the drawn number is <see cref="DeterministicRandom"/> as usual. Nothing on a
    /// tick path may call these, and nothing does: the entropy is reached only through the two
    /// parameterless entry points, and every rule below is exercised through the seam that takes
    /// its randomness as an argument.</para>
    ///
    /// <para><b>Zero is an ordinary seed.</b> Every consumer reaches its stream through
    /// <see cref="DeterministicRandom.ForTick(uint, int)"/>, which avalanche-mixes the seed before
    /// it ever reaches the generator's <c>state == 0</c> guard, so seed 0 is a world like any
    /// other rather than a silent alias for seed 1. The field can therefore show a 0 honestly and
    /// nothing here has to special-case it.</para>
    /// </summary>
    public static class SeedEntry
    {
        /// <summary>
        /// <c>uint.MaxValue</c> is ten digits, so no seed needs more room than this. It is here
        /// for whatever sizes the field a seed is typed into; the parser deliberately does not
        /// use it, because rejecting a long run of leading zeros that would have parsed perfectly
        /// well is a rule with no one to serve.
        /// </summary>
        public const int MaxDigits = 10;

        /// <summary>
        /// The longest raw text <see cref="TryParse"/> will look at. A paste is unbounded and a
        /// seed is not, so the length is checked before anything is allocated to clean it.
        /// Generous enough that no plausible grouping of ten digits trips it.
        /// </summary>
        const int MaxRawLength = 32;

        /// <summary>
        /// Group separators a player may have in what they paste — "3 829 174 463" out of a
        /// document, "3,829,174,463" out of a spreadsheet, "3_829_174_463" out of code. They mean
        /// nothing to the number, so they are removed rather than rejected.
        /// </summary>
        static bool IsGrouping(char c) => c == ' ' || c == ',' || c == '_';

        /// <summary>A fresh seed from the machine, with no relation to any previous one.</summary>
        public static uint Draw() => Draw(MachineEntropy, avoid: null);

        /// <summary>
        /// A fresh seed that is <b>guaranteed</b> different from <paramref name="current"/>.
        ///
        /// <para>The guarantee is the point: a reroll button that lands on the number already in
        /// the field reads as a broken button, and "it will practically never happen" is not
        /// something the player can see. One in 2^32 is rare enough that nobody would ever catch
        /// it in play and cheap enough to rule out here.</para>
        /// </summary>
        public static uint Reroll(uint current) => Draw(MachineEntropy, current);

        /// <summary>
        /// The seam the tests drive: the draw with its randomness handed in.
        ///
        /// <para><paramref name="avoid"/> is a seed the result may not equal, or null for no
        /// constraint. The retry is <b>bounded</b>, because a caller — a test double, most
        /// likely — may hand in a source that returns one value forever, and an unbounded loop
        /// would hang rather than fail. After the attempts are spent the neighbouring seed is
        /// taken instead, so the contract holds absolutely and not merely usually.</para>
        /// </summary>
        public static uint Draw(Func<uint> entropy, uint? avoid)
        {
            if (entropy == null) throw new ArgumentNullException(nameof(entropy));

            for (int attempt = 0; attempt < 8; attempt++)
            {
                uint drawn = entropy();
                if (avoid == null || drawn != avoid.Value) return drawn;
            }

            unchecked { return avoid!.Value + 1u; }
        }

        /// <summary>
        /// The seed as the player reads it: plain decimal digits, no grouping, no prefix.
        ///
        /// <para>Decimal because it is already what the project prints a seed as, so a number
        /// copied out of a log is a number that can be typed back in, and because it needs no
        /// vocabulary a player has to be taught. Free-text seeds in the Minecraft idiom — type a
        /// word, have it hashed — are the genre's other convention and are the friendlier one,
        /// but they only work if the text itself is kept beside the number, and where that text
        /// would live is the save header's business (U36). Adding a second representation before
        /// then would be exactly the second source of truth to avoid.</para>
        ///
        /// <para>Invariant culture on purpose: a player whose system locale substitutes digits
        /// would otherwise be shown a seed that <see cref="TryParse"/> then refuses.</para>
        /// </summary>
        public static string Format(uint seed) => seed.ToString(NumberFormatInfo.InvariantInfo);

        /// <summary>
        /// Reads back what the player typed. True with the seed when the text names one; false
        /// with <paramref name="seed"/> zero when it does not, so a caller can hold on to the
        /// seed it already had rather than silently starting a different world.
        ///
        /// <para>Forgiving about presentation and strict about value. Surrounding whitespace and
        /// group separators are removed; a sign, a decimal point, letters, an empty field and
        /// anything above <c>uint.MaxValue</c> are all refused. A negative is refused rather than
        /// read as its digits, because someone who typed one meant something this field cannot
        /// give them.</para>
        /// </summary>
        public static bool TryParse(string? text, out uint seed)
        {
            seed = 0u;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string trimmed = text!.Trim();
            if (trimmed.Length > MaxRawLength) return false;

            var digits = new StringBuilder(trimmed.Length);
            foreach (char c in trimmed)
            {
                if (IsGrouping(c)) continue;
                digits.Append(c);
            }

            if (digits.Length == 0) return false;

            // NumberStyles.None after our own cleaning: no sign, no whitespace, no separators of
            // its own. Everything this accepts, it accepted because the rules above let it past.
            return uint.TryParse(
                digits.ToString(), NumberStyles.None, NumberFormatInfo.InvariantInfo, out seed);
        }

        /// <summary>
        /// Fresh randomness from the runtime, folded to a uint.
        ///
        /// <para><see cref="Guid.NewGuid"/> is the source because it is the only one a
        /// netstandard2.1 assembly with no engine and no extra references can reach, and because
        /// version-4 GUIDs are drawn from the platform's cryptographic generator on every runtime
        /// this ships on — two calls in the same millisecond are unrelated, which a clock-derived
        /// source could not promise.</para>
        ///
        /// <para>Six of a GUID's 128 bits are fixed version and variant markers, so the bytes are
        /// folded with FNV-1a and then put through the same avalanche
        /// <see cref="DeterministicRandom.ForTick(uint, int)"/> uses, rather than truncated. A
        /// truncation could land on those fixed bits and quietly narrow the range of seeds a
        /// player can ever be dealt.</para>
        /// </summary>
        static uint MachineEntropy()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();

            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < bytes.Length; i++)
                {
                    h ^= bytes[i];
                    h *= 16777619u;
                }

                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
