#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>The safety net: no colonist stands inside solid world.</b> Every tick, any pawn whose
    /// own cell it could not walk into is moved to the nearest cell it can
    /// (<see cref="PawnEviction"/>).
    ///
    /// <para><b>Why a sweep as well as the guard at the wall.</b> <c>ConstructionGrid.Raise</c>
    /// already refuses to build over somebody, so a wall cannot entomb a colonist any more. That
    /// leaves two things this and only this can answer: the colonists already walled up in saves
    /// written before that guard existed, who are otherwise trapped for the life of the colony;
    /// and every other way a cell can close over a person — the floor under a bed deconstructed,
    /// a fall into a cell that then fills, whatever the next feature does that nobody has thought
    /// of yet. A guard is a rule about one edit; this is a statement about the world, and the
    /// owner asked for the statement (2026-09-21: <i>"this should never and can't happen"</i>).</para>
    ///
    /// <para><b>It costs one flag read per pawn per tick.</b> <c>NavGrid.CanEnter</c> is two array
    /// reads and a mask, and nothing happens at all in the overwhelming case where every colonist
    /// is standing somewhere legal — the loop over fifty colonists is well under a microsecond and
    /// does not scale with the board. The scan is in pawn-id order and the search is a fixed
    /// widening ring, so it is deterministic and the state hash agrees across runs.</para>
    ///
    /// <para>A pawn that cannot be freed — enclosed in solid rock for three cells in every
    /// direction — is left where it is and asked again next tick, because inventing a
    /// teleport across the map is a worse answer than standing still until somebody digs.</para>
    /// </summary>
    public sealed class TrappedPawnSystem : IWorldSystem
    {
        readonly PawnContext _pawns;

        public TrappedPawnSystem(PawnContext pawns)
        {
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
        }

        public string Name => "trapped-pawns";

        public TickPhase Phase => TickPhase.WorldSystems;

        /// <summary>
        /// After navigation (20), so the flags this reads are the ones the last tick's edits
        /// produced, and before anything that acts on where a pawn is.
        /// </summary>
        public int Order => 25;

        /// <summary>How many colonists this has had to dig out. For tests and the debug overlay.</summary>
        public int Freed { get; private set; }

        public void Tick(SimWorld world)
        {
            var pawns = _pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                // In somebody's arms: where the carrier is, which is the carrier's to be freed from.
                if (pawn.CarriedBy != 0) continue;

                // **Filled, not merely unwalkable**, and the difference is the whole of the
                // rule's safety. "Not walkable" is true of a great deal a colonist may legally
                // be in the middle of — a cell with no floor while falling, deep water, a
                // doorway — and evicting out of those would be a teleport in answer to a
                // situation that was never wrong. Something solid or blocking standing in the
                // cell is the only case this exists for, and a door the pawn can open is not it.
                Pathing.NavFlags flags = _pawns.Nav.Grid.Flags[pawn.Cell];
                if ((flags & (Pathing.NavFlags.Solid | Pathing.NavFlags.Blocked)) == 0) continue;
                if (_pawns.Nav.Grid.CanEnter(pawn.Cell, pawn.Mode)) continue;

                if (PawnEviction.Evict(_pawns, pawn)) Freed++;
            }
        }
    }
}
