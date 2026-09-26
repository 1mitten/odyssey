#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Every colonist's kit slots (design 54 §6), saved and hashed. The slots live on the pawn
    /// (<see cref="Pawn.KitItems"/>); the things in them are <see cref="ColonyItems"/>' and are saved
    /// and hashed there, carrier and all. This is the record of which of those things is in which
    /// slot of whose kit.
    ///
    /// <para><b>A keyed section</b>, written only for colonists with something in a kit, so a save
    /// from before the kit has none and loads with every kit empty — which is what every kit then
    /// was — and <b>no world format bump</b>, the rule <c>HealthSection</c> states.</para>
    ///
    /// <para><b>A hashable of its own rather than a bit in <c>Pawn.ContributeTo</c>'s flag word</b>:
    /// that word's last bit went to the home area, and two branches have already taken the same
    /// bit of it once (<c>docs/bug-patterns.md</c>). This adds nothing for an empty kit, so a colony
    /// that never used one hashes as before, and its registration moved no golden.</para>
    ///
    /// <para>Scales with the pawns, in id order: the registry's own order, never a hash table's.</para>
    /// </summary>
    public sealed class KitLedger : ISaveable, IStateHashable
    {
        /// <summary>The record layout this build writes. 1 is design 54's.</summary>
        public const int Layout = 1;

        readonly PawnRegistry _pawns;
        readonly PawnContext _ctx;
        readonly List<Pawn> _scratch = new List<Pawn>();

        public KitLedger(PawnContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _pawns = ctx.Pawns;
        }

        public string SaveKey => "odyssey.gear";

        /// <summary>The colonists with a filled slot, in the registry's order.</summary>
        List<Pawn> Kitted()
        {
            _scratch.Clear();
            IReadOnlyList<Pawn> all = _pawns.All;
            for (int i = 0; i < all.Count; i++) if (!Kit.IsEmpty(all[i], _ctx)) _scratch.Add(all[i]);
            return _scratch;
        }

        public void ContributeTo(ref StateHash hash)
        {
            List<Pawn> kitted = Kitted();
            if (kitted.Count == 0) return;
            hash.Add(kitted.Count);
            for (int i = 0; i < kitted.Count; i++)
            {
                Pawn pawn = kitted[i];
                hash.Add(pawn.Id.Value);
                for (int slot = 0; slot < Kit.Slots; slot++)
                    hash.Add(Kit.Held(pawn, _ctx, slot) != null ? pawn.KitItems[slot] : 0);
            }
            kitted.Clear();
        }

        public void Save(SaveWriter writer)
        {
            List<Pawn> kitted = Kitted();
            writer.Write(Layout);
            writer.Write(kitted.Count);
            for (int i = 0; i < kitted.Count; i++)
            {
                Pawn pawn = kitted[i];
                writer.Write(pawn.Id.Value);
                writer.Write(Kit.Slots);
                for (int slot = 0; slot < Kit.Slots; slot++)
                    writer.Write(Kit.Held(pawn, _ctx, slot) != null ? pawn.KitItems[slot] : 0);
            }
            kitted.Clear();
        }

        public void Load(SaveReader reader)
        {
            int layout = reader.ReadInt();
            if (layout < 1 || layout > Layout)
                throw new SaveLoadException($"The gear section has layout {layout} and this build reads up to {Layout}.");

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                Pawn? pawn = _pawns.Get(new PawnId(reader.ReadInt()));
                int slots = reader.ReadInt();
                for (int slot = 0; slot < slots; slot++)
                {
                    int id = reader.ReadInt();
                    // A slot this build does not have is read and dropped: a later build's pack
                    // slots, read by this one, leave their things carried by nobody's kit.
                    if (pawn != null && slot < Kit.Slots) pawn.KitItems[slot] = id;
                }
            }
        }
    }
}
