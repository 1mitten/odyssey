#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// The combat state of the pawns that have any (design 33 §2a, §5), as one save section on the
    /// kind and wildlife sections' terms: keyed by pawn id, absent from an older save — which then
    /// loads with nobody drafted and everybody whole — and no world format bump.
    ///
    /// <para><b>Only pawns with something to say are written</b>, so a colony that has never
    /// fought writes a count of nought. The record opens with its own layout number so the units
    /// after C1 can append fields without moving the world's format number either.</para>
    ///
    /// <para><b>Layout 2</b> is the contracts step's (design 33 §5): C1's four fields, then every
    /// field the combat line needs, claimed at once so that no lane has to bump the layout on a
    /// branch of its own — hit points, the downed flag (in the flags word beside the draft), the
    /// next swing tick, the stun, the retaliation and its end, the equipped weapon, the order's
    /// target pawn and the carrier. Layout 1 is still read.</para>
    ///
    /// <para><b>Layout 3</b> is the third playtest's round (design 33 §9b, §9g): the knock-down
    /// clock and the swing in the air — its result word, damage and stun, decided when the wind-up
    /// began. Four ints appended to every record, so a record is read the same way whatever is set;
    /// layouts 1 and 2 still load, with nobody knocked down and no swing in the air.</para>
    ///
    /// <para><b>Layout 4</b> is the jump over a stream (design 46 §6): where the jump in the air
    /// lands, one int appended to every record. It lives here beside the finishing step because
    /// both are a step the world cannot re-derive; layouts 1 to 3 load with nobody in the air.</para>
    ///
    /// <para><b>Layout 5</b> is medical supplies (design 37): the cooldown until this pawn may be
    /// treated again, appended after the jump — the jump reached main first, and a shipped layout
    /// number is a save contract like everything else that is. Layouts 1 to 4 load with nobody
    /// ever treated.</para>
    ///
    /// <para><b>The response</b> (design 33 §18c) rides in two bits of the flags word, with no
    /// layout change, and a colonist whose response is not the default has a record for it.</para>
    ///
    /// <para>Hashing is <see cref="Pawn.ContributeTo"/>'s, not this section's. What it writes is
    /// exactly what that hashes, and <see cref="HasState"/> is the same question
    /// <c>Pawn.HasCombatState</c> answers, plus the draft's two.</para>
    ///
    /// <para><b>Read after the kind section</b>, which it is by its place in
    /// <c>ColonyWorld.SaveComponents</c>: a pawn's pool depends on its kind, and a hog's hit points
    /// read against a colonist's pool would be nonsense.</para>
    /// </summary>
    public sealed class CombatSection : ISaveable
    {
        /// <summary>
        /// The record layout this build writes. 1 is C1's: flags, quiet tick, finishing step. 2 is
        /// the combat contracts step's: C1's four, then the eight combat fields. 3 appends the
        /// knock-down clock and the pending swing (design 33 §9b, §9g). 4 appends the jump's landing
        /// (design 46 §6). 5 appends the treatment cooldown (design 37). 6 appends a sweep's
        /// knockback immunity (design 62 §7).
        /// </summary>
        public const int Layout = 6;

        const int FlagDrafted = 1;
        const int FlagDowned = 2;

        // The response (design 33 §18c): two bits of the flags word, so no layout moved. A build
        // from before §18 reads the word for its two flags and never looks at these.
        const int ResponseShift = 2, ResponseMask = 3;

        readonly PawnRegistry _pawns;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public CombatSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.combat";

        static bool HasState(Pawn pawn) =>
            pawn.Drafted || pawn.FinishingStepTo >= 0 || pawn.HasCombatState
            || pawn.Response != HostilityResponse.FightBack || pawn.JumpLanding >= 0;

        public void Save(SaveWriter writer)
        {
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (HasState(all[i])) _scratch.Add(all[i]);

            writer.Write(Layout);
            writer.Write(_scratch.Count);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Pawn pawn = _scratch[i];
                writer.Write(pawn.Id.Value);
                writer.Write((pawn.Drafted ? FlagDrafted : 0) | (pawn.Downed ? FlagDowned : 0)
                    | ((int)pawn.Response << ResponseShift));
                writer.Write(pawn.DraftQuietSinceTick);
                writer.Write(pawn.FinishingStepTo);

                // Layout 2.
                writer.Write(pawn.HpMilli);
                writer.Write(pawn.NextSwingTick);
                writer.Write(pawn.StunnedUntilTick);
                writer.Write(pawn.RetaliateAgainst);
                writer.Write(pawn.RetaliateUntilTick);
                writer.Write(pawn.EquippedItem);
                writer.Write(pawn.CombatTarget);
                writer.Write(pawn.CarriedBy);

                // Layout 3.
                writer.Write(pawn.KnockedDownUntilTick);
                writer.Write(pawn.PendingSwing);
                writer.Write(pawn.PendingDamageMilli);
                writer.Write(pawn.PendingStunTicks);

                // Layout 4.
                writer.Write(pawn.JumpLanding);

                // Layout 5: medical supplies (design 37), after the jump — the jump reached main
                // first and a shipped layout number is a save contract.
                writer.Write(pawn.TreatedUntilTick);

                // Layout 6: the butcher's fling (design 62 §7).
                writer.Write(pawn.KnockbackImmuneUntilTick);
            }
            _scratch.Clear();
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException(
                    $"The combat section has layout {layout} and this build reads up to {Layout}.");

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                int flags = reader.ReadInt();
                int quiet = reader.ReadInt();
                int finishing = reader.ReadInt();

                // A layout-1 record ends here and its pawn is whole: C1 had no hit points to save.
                bool two = layout >= 2;
                int hp = two ? reader.ReadInt() : int.MinValue;
                int nextSwing = two ? reader.ReadInt() : 0;
                int stunnedUntil = two ? reader.ReadInt() : 0;
                int retaliateAgainst = two ? reader.ReadInt() : 0;
                int retaliateUntil = two ? reader.ReadInt() : 0;
                int equipped = two ? reader.ReadInt() : 0;
                int target = two ? reader.ReadInt() : 0;
                int carriedBy = two ? reader.ReadInt() : 0;

                // Layout 3: nobody knocked down and no swing in the air before it.
                bool three = layout >= 3;
                int knockedUntil = three ? reader.ReadInt() : 0;
                int pendingSwing = three ? reader.ReadInt() : 0;
                int pendingDamage = three ? reader.ReadInt() : 0;
                int pendingStun = three ? reader.ReadInt() : 0;

                // Layout 4: nobody in the air before it.
                int jumpLanding = layout >= 4 ? reader.ReadInt() : -1;

                // Layout 5: nobody had been treated before medicine existed.
                int treatedUntil = layout >= 5 ? reader.ReadInt() : 0;

                // Layout 6: nobody had been flung before the butcher.
                int immuneUntil = layout >= 6 ? reader.ReadInt() : 0;

                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn == null) continue;

                pawn.Drafted = (flags & FlagDrafted) != 0;
                pawn.DraftQuietSinceTick = pawn.Drafted ? quiet : 0;

                pawn.Downed = (flags & FlagDowned) != 0;
                int response = (flags >> ResponseShift) & ResponseMask;
                pawn.Response = response < HostilityResponses.Count ? (HostilityResponse)response : HostilityResponse.FightBack;
                pawn.HpMilli = hp == int.MinValue ? pawn.HpMaxMilli : hp;
                pawn.NextSwingTick = nextSwing;
                pawn.StunnedUntilTick = stunnedUntil;
                pawn.RetaliateAgainst = retaliateAgainst;
                pawn.RetaliateUntilTick = retaliateUntil;
                pawn.EquippedItem = equipped;
                pawn.CombatTarget = target;
                pawn.CarriedBy = carriedBy;
                pawn.KnockedDownUntilTick = knockedUntil;
                pawn.PendingSwing = pendingSwing;
                pawn.PendingDamageMilli = pendingDamage;
                pawn.PendingStunTicks = pendingStun;
                pawn.TreatedUntilTick = treatedUntil;
                pawn.KnockbackImmuneUntilTick = immuneUntil;

                // A step an order interrupted, rebuilt as the one-step path it was (design 33
                // §2d). The pawn section has already restored the progress into it, and
                // AdoptPath leaves progress alone for exactly this case — a resume.
                if (finishing >= 0)
                {
                    pawn.AdoptPath(new[] { pawn.Cell, finishing }, 2);
                    pawn.FinishingStepTo = finishing;
                }

                // A jump in the air (design 46 §6), rebuilt as the one step it is, landing where it
                // was rolled to land. Never rolled again: the roll is made only while no landing
                // is set. When an order interrupted the jump the finishing step is this same cell
                // and the path above is already right.
                if (jumpLanding >= 0)
                {
                    if (finishing < 0) pawn.AdoptPath(new[] { pawn.Cell, jumpLanding }, 2);
                    pawn.JumpLanding = jumpLanding;
                }
            }
        }
    }
}
