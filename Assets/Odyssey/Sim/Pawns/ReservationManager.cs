#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>What a claim is against. The kind is folded into the key so one table serves both.</summary>
    public enum ReservationTargetKind : byte
    {
        Cell = 0,
        Item = 1,
    }

    /// <summary>
    /// The only inter-pawn coordination in the simulation, and it earns its place.
    ///
    /// The contract, from <c>docs/design/05-ai-and-jobs.md</c> section 2: a claim is tested during
    /// the work-giver scan, claimed <b>all or nothing</b> before the toils run, and released
    /// whenever the job ends for any reason including failure. Check-then-claim needs no locking
    /// because the tick is single-threaded by decision — one of several places where
    /// determinism-first quietly removes a whole class of bug.
    ///
    /// <para>A leak here is the fault a ten-day unattended run surfaces and a two-minute test does
    /// not, which is why <see cref="ActiveClaims"/> is public and why the tests assert on it after
    /// thousands of ticks rather than after ten.</para>
    ///
    /// <para>The table is a dictionary keyed by target, which is <em>looked up</em> in the tick and
    /// never <em>iterated</em> in it. Iteration order of a hash table is not a simulation input.
    /// Everything that must be walked in order — a pawn's own claims — is walked from the ordered
    /// list the pawn carries.</para>
    /// </summary>
    public sealed class ReservationManager
    {
        /// <summary>Concurrent claimants on one target. Four is far more than the slice needs.</summary>
        public const int MaxClaimants = 4;

        struct Slot
        {
            public int Count;
            public int MaxPawns;
            public int StackCount;
            public int C0, C1, C2, C3;
        }

        readonly Dictionary<long, Slot> _slots = new Dictionary<long, Slot>();
        int _activeClaims;

        /// <summary>Live claims across every pawn. Zero when nothing holds a job.</summary>
        public int ActiveClaims => _activeClaims;

        /// <summary>Distinct targets under claim. Never equal to a count of anything iterated.</summary>
        public int ClaimedTargets => _slots.Count;

        public static long Key(ReservationTargetKind kind, int target) =>
            ((long)kind << 40) | (uint)target;

        public static ReservationTargetKind KindOf(long key) => (ReservationTargetKind)(key >> 40);

        public static int TargetOf(long key) => (int)(key & 0xFF_FFFF_FFFFL);

        /// <summary>
        /// Could this pawn claim this target right now? Called during the scan, for potentially
        /// many candidates, so it does no work beyond one dictionary probe.
        /// </summary>
        public bool CanReserve(PawnId claimant, long key, int maxPawns = 1)
        {
            if (!_slots.TryGetValue(key, out var slot)) return true;
            if (Holds(slot, claimant)) return true;
            return slot.Count < slot.MaxPawns && slot.MaxPawns >= maxPawns;
        }

        /// <summary>
        /// Take the claim. Returns false rather than throwing, because the caller's answer to a
        /// lost race is to abandon the job, not to crash.
        /// </summary>
        public bool Reserve(PawnId claimant, long key, int maxPawns = 1, int stackCount = 1)
        {
            if (maxPawns < 1 || maxPawns > MaxClaimants) return false;

            if (!_slots.TryGetValue(key, out var slot))
            {
                slot = new Slot { Count = 1, MaxPawns = maxPawns, StackCount = stackCount, C0 = claimant.Value };
                _slots[key] = slot;
                _activeClaims++;
                return true;
            }

            if (Holds(slot, claimant)) return true;
            if (slot.Count >= slot.MaxPawns) return false;

            switch (slot.Count)
            {
                case 1: slot.C1 = claimant.Value; break;
                case 2: slot.C2 = claimant.Value; break;
                case 3: slot.C3 = claimant.Value; break;
                default: return false;
            }

            slot.Count++;
            slot.StackCount += stackCount;
            _slots[key] = slot;
            _activeClaims++;
            return true;
        }

        public bool IsReservedBy(PawnId claimant, long key) =>
            _slots.TryGetValue(key, out var slot) && Holds(slot, claimant);

        public bool IsReservedByAnyone(long key) => _slots.ContainsKey(key);

        /// <summary>Drop one claim. Silent when it was not held: releasing twice is not an error.</summary>
        public void Release(PawnId claimant, long key)
        {
            if (!_slots.TryGetValue(key, out var slot)) return;
            if (!Holds(slot, claimant)) return;

            // Compact the claimant slots so Count stays the number of live claimants. Four
            // locals rather than a scratch array: this runs on every job end and the tick
            // allocates nothing.
            int c = claimant.Value;
            int k0 = 0, k1 = 0, k2 = 0, k3 = 0, n = 0;
            Keep(slot.C0, c, ref k0, ref k1, ref k2, ref k3, ref n);
            Keep(slot.C1, c, ref k0, ref k1, ref k2, ref k3, ref n);
            Keep(slot.C2, c, ref k0, ref k1, ref k2, ref k3, ref n);
            Keep(slot.C3, c, ref k0, ref k1, ref k2, ref k3, ref n);

            _activeClaims--;
            if (n == 0)
            {
                _slots.Remove(key);
                return;
            }

            slot.C0 = k0;
            slot.C1 = k1;
            slot.C2 = k2;
            slot.C3 = k3;
            slot.Count = n;
            _slots[key] = slot;
        }

        /// <summary>
        /// Release every claim a pawn holds, walking the pawn's own ordered list. This is what
        /// job end calls, for every kind of end there is.
        /// </summary>
        public void ReleaseAll(Pawn pawn)
        {
            var held = pawn.HeldReservations;
            for (int i = 0; i < held.Count; i++) Release(pawn.Id, held[i]);
            held.Clear();
        }

        /// <summary>Wipe the table. Used by load, before pawn claims are re-registered.</summary>
        public void Clear()
        {
            _slots.Clear();
            _activeClaims = 0;
        }

        static void Keep(int candidate, int dropped,
            ref int k0, ref int k1, ref int k2, ref int k3, ref int n)
        {
            if (candidate == 0 || candidate == dropped) return;
            switch (n)
            {
                case 0: k0 = candidate; break;
                case 1: k1 = candidate; break;
                case 2: k2 = candidate; break;
                default: k3 = candidate; break;
            }
            n++;
        }

        static bool Holds(in Slot slot, PawnId claimant)
        {
            int c = claimant.Value;
            return slot.C0 == c || slot.C1 == c || slot.C2 == c || slot.C3 == c;
        }
    }
}
