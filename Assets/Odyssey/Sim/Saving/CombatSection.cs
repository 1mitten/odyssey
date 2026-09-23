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
        /// the combat contracts step's: C1's four, then the eight combat fields.
        /// </summary>
        public const int Layout = 2;

        const int FlagDrafted = 1;
        const int FlagDowned = 2;

        readonly PawnRegistry _pawns;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public CombatSection(PawnRegistry pawns) =>
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));

        public string SaveKey => "odyssey.combat";

        static bool HasState(Pawn pawn) => pawn.Drafted || pawn.FinishingStepTo >= 0 || pawn.HasCombatState;

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
                writer.Write((pawn.Drafted ? FlagDrafted : 0) | (pawn.Downed ? FlagDowned : 0));
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

                Pawn? pawn = _pawns.Get(new Contracts.PawnId(id));
                if (pawn == null) continue;

                pawn.Drafted = (flags & FlagDrafted) != 0;
                pawn.DraftQuietSinceTick = pawn.Drafted ? quiet : 0;

                pawn.Downed = (flags & FlagDowned) != 0;
                pawn.HpMilli = hp == int.MinValue ? pawn.HpMaxMilli : hp;
                pawn.NextSwingTick = nextSwing;
                pawn.StunnedUntilTick = stunnedUntil;
                pawn.RetaliateAgainst = retaliateAgainst;
                pawn.RetaliateUntilTick = retaliateUntil;
                pawn.EquippedItem = equipped;
                pawn.CombatTarget = target;
                pawn.CarriedBy = carriedBy;

                // A step an order interrupted, rebuilt as the one-step path it was (design 33
                // §2d). The pawn section has already restored the progress into it, and
                // AdoptPath leaves progress alone for exactly this case — a resume.
                if (finishing >= 0)
                {
                    pawn.AdoptPath(new[] { pawn.Cell, finishing }, 2);
                    pawn.FinishingStepTo = finishing;
                }
            }
        }
    }
}
