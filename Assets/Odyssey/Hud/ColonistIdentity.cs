#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What a colonist is besides their skills: how old they are, and what they did before the
    /// city fell.
    ///
    /// <para><b>Derived, not stored.</b> Both come from the pawn's own <c>RollSeed</c> and its id —
    /// the same pair <see cref="ColonistNames"/> keys on — so neither is saved and neither is in
    /// the state hash. That is the project's standing rule for derived state, the one that keeps
    /// the support solve out of the hash, and here it buys something concrete: **no golden moves.**
    /// U40 re-baked all six this afternoon when <c>RollSeed</c> entered the hash; storing three
    /// more fields the ordinary way would have re-baked them a second time in one day for values
    /// carrying no information the hash cannot already derive.</para>
    ///
    /// <para><b>Why it is here and not in the simulation.</b> Nothing simulates an age or a trade
    /// yet — the owner's ruling is that occupation is flavour now and shapes skills later, and that
    /// age exists because *"it will be important later"*. Until something reads them they are an
    /// interface-side identity exactly like a name, and this assembly is where that already lives.
    /// <b>When ageing, skill decay or health do read an age, this moves into <c>Odyssey.Sim</c></b>
    /// — and it is one function, so that is a move rather than a rewrite, and the formula can go
    /// across unchanged so that nobody's colonists change age on the way.</para>
    ///
    /// <para><b>The occupations are the registry's, not a list here.</b> They are every key under
    /// <c>ui.occupation.</c>, read out of the generated <see cref="Registry"/> — so the CSV stays
    /// the one place a name is written, adding one needs no code, and the owner can strike any of
    /// the ninety-two in a line. Sorted explicitly rather than trusted to come out of a dictionary
    /// in order, because an occupation that moved between runtimes would quietly re-employ
    /// everybody.</para>
    ///
    /// <para>Unity-free by construction (ADR 0003): everything here runs in the fast tier.</para>
    /// </summary>
    public static class ColonistIdentity
    {
        /// <summary>The prefix every occupation's key carries.</summary>
        public const string OccupationPrefix = "ui.occupation.";

        /// <summary>
        /// The youngest a colonist can be. <b>Eighteen, by the owner's instruction</b> — *"an
        /// actual age 18+ only, will be important later"* — so nothing downstream ever has to
        /// decide what a colony does with a child.
        /// </summary>
        public const int MinimumAge = 18;

        /// <summary>The oldest. Sixty-five, flat across the range: a weighted curve is a decision
        /// about what a colony looks like and nobody has made it.</summary>
        public const int MaximumAge = 65;

        /// <summary>
        /// Distinct salts, so that two facts about one person are drawn from two streams. Sharing
        /// one would correlate them — every Nepo Rich Kid the same age — and the correlation would
        /// be invisible until somebody noticed the colony looked odd.
        /// </summary>
        const uint AgeSalt = 0xA6E0u;
        const uint TradeSalt = 0x7BADEu;

        static string[]? _occupations;

        /// <summary>
        /// Every occupation's key, in a fixed order. Read from the registry once.
        /// </summary>
        public static IReadOnlyList<string> Occupations => _occupations ??= BuildOccupations();

        static string[] BuildOccupations()
        {
            var keys = new List<string>();
            foreach (KeyValuePair<string, string> row in Registry.Labels)
                if (row.Key.StartsWith(OccupationPrefix, System.StringComparison.Ordinal))
                    keys.Add(row.Key);

            keys.Sort(System.StringComparer.Ordinal);
            return keys.ToArray();
        }

        /// <summary>How old this colonist is, in years.</summary>
        public static int Age(uint rollSeed, PawnId id)
        {
            if (!id.IsValid) return MinimumAge;

            int span = MaximumAge - MinimumAge + 1;
            return MinimumAge + (int)(Mix(rollSeed, id, AgeSalt) % (uint)span);
        }

        /// <summary>
        /// The registry key of what this colonist did before. Empty when the CSV holds no
        /// occupations at all, which a presenter draws as nothing rather than as a missing key.
        /// </summary>
        public static string OccupationKey(uint rollSeed, PawnId id)
        {
            IReadOnlyList<string> all = Occupations;
            if (all.Count == 0 || !id.IsValid) return string.Empty;

            return all[(int)(Mix(rollSeed, id, TradeSalt) % (uint)all.Count)];
        }

        /// <summary>What that trade is called, ready to draw.</summary>
        public static string Occupation(uint rollSeed, PawnId id)
        {
            string key = OccupationKey(rollSeed, id);
            return key.Length == 0 ? string.Empty : Registry.Label(key);
        }

        /// <summary>The age of a colonist in the published frame — the ordinary way to ask.</summary>
        public static int Age(WorldSnapshot snapshot, PawnId id) =>
            Age(ColonistNames.RollSeedOf(snapshot, id), id);

        /// <summary>The trade of a colonist in the published frame.</summary>
        public static string Occupation(WorldSnapshot snapshot, PawnId id) =>
            Occupation(ColonistNames.RollSeedOf(snapshot, id), id);

        /// <summary>
        /// FNV-1a over the seed, the id and the salt, then the avalanche the rest of the project
        /// uses. The avalanche matters here as much as anywhere: without it, rerolling a candidate
        /// to the neighbouring seed would step one place down the occupation list, and a player
        /// pressing Reroll would watch the alphabet go by.
        /// </summary>
        static uint Mix(uint seed, PawnId id, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ seed) * 16777619u;
                h = (h ^ (uint)id.Value) * 16777619u;
                h = (h ^ salt) * 16777619u;
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
