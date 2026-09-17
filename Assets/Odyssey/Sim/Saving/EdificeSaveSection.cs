#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// Every wall, door and pillar standing on the board, as one save section and one hash
    /// contribution.
    ///
    /// <para><b>Why this exists, measured rather than supposed (2026-09-17).</b> The list of
    /// <see cref="PlacedEdifice"/> was owned by worldgen and by nothing else: it was not an
    /// <c>ISaveable</c>, so it was never written, and it was not an <c>IStateHashable</c>, so what
    /// a building was made of was outside the state hash. <see cref="World.CellGrid"/> saved and
    /// hashed <c>Edifice[cell]</c>, which is an <em>index into this list</em> — and the list was
    /// rebuilt from the seed on load, so it held only what the generator had stamped. Two tests
    /// were written to prove it before anything was built on top:</para>
    ///
    /// <list type="bullet">
    ///   <item><c>EdificeRoundTripTests.AWallRemembersWhatItIsMadeOfAcrossASave</c> — a wall a
    ///   colonist raised on a bare board took handle 0, and after a reload the restored list had
    ///   <b>no entries at all</b>. The cell still said a wall stood there and pointed at
    ///   nothing.</item>
    ///   <item><c>.AWallsMaterialIsInTheStateHash</c> — a wooden wall and a stone wall in the same
    ///   cell hashed <b>identically</b>.</item>
    /// </list>
    ///
    /// <para>That is the shape of the gap OQ-50 closed one level up, and the same argument applies:
    /// a hash that wrongly says two worlds are the same is worse than no hash, because every
    /// determinism gate in the project rests on it agreeing only when it should.</para>
    ///
    /// <para><b>The whole list is written, not just what the colony added.</b> The generator's own
    /// stamps could in principle be recovered by regenerating from the seed, which would be
    /// smaller — and would make every save file depend on the generator never changing. A save
    /// that describes itself survives a worldgen edit; one that is half a recipe does not.</para>
    ///
    /// <para><b>Handles are positions in this list</b> and are part of the determinism contract,
    /// so load replaces the contents in order rather than merging into them. A removed entry keeps
    /// its slot, which is why <see cref="PlacedEdifice.Removed"/> is written rather than the entry
    /// being dropped.</para>
    /// </summary>
    public sealed class EdificeSaveSection : ISaveable, IStateHashable
    {
        readonly List<PlacedEdifice> _edifices;

        public EdificeSaveSection(List<PlacedEdifice> edifices)
        {
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
        }

        /// <summary>
        /// The list itself, for the grids that read and append to it. Handed out rather than
        /// copied: this type is a save and hash channel onto the world's one edifice list, not a
        /// second copy of it, and a second copy is the failure this was written to prevent.
        /// </summary>
        public List<PlacedEdifice> Records => _edifices;

        public string SaveKey => "odyssey.edifices";

        public void Save(SaveWriter writer)
        {
            writer.Write(_edifices.Count);
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice placed = _edifices[i];
                writer.Write(placed.CellIndex);
                writer.Write((uint)placed.Def);
                writer.Write((uint)placed.Stuff);
                writer.Write(placed.Built);
                writer.Write(placed.Removed);
                // Version 3 (beds): the three fields a bed carries that a wall never does. A
                // version 2 reader would stop before them and misparse everything after; a
                // version 3 reader of a version 2 file reads none of them (see Load).
                writer.Write(placed.Facing);
                writer.Write(placed.Quality);
                writer.Write(placed.Owner);
            }
        }

        public void Load(SaveReader reader)
        {
            _edifices.Clear();
            int count = reader.ReadInt();
            bool beds = reader.FormatVersion >= 4;
            for (int i = 0; i < count; i++)
            {
                var placed = new PlacedEdifice
                {
                    CellIndex = reader.ReadInt(),
                    Def = (ushort)reader.ReadUInt(),
                    Stuff = (ushort)reader.ReadUInt(),
                    Built = reader.ReadBool(),
                    Removed = reader.ReadBool(),
                };
                if (beds)
                {
                    placed.Facing = reader.ReadByte();
                    placed.Quality = reader.ReadByte();
                    placed.Owner = reader.ReadInt();
                }
                _edifices.Add(placed);
            }
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_edifices.Count);
            for (int i = 0; i < _edifices.Count; i++)
            {
                PlacedEdifice placed = _edifices[i];
                hash.Add(placed.CellIndex);
                hash.Add(placed.Def);
                hash.Add(placed.Stuff);
                hash.Add(placed.Built ? 1 : 0);
                hash.Add(placed.Removed ? 1 : 0);
                // Beds' own three: a Poor bed and an Epic one must not hash alike, and who owns a
                // bed is world state the same way what it is made of is.
                hash.Add(placed.Facing);
                hash.Add(placed.Quality);
                hash.Add(placed.Owner);
            }
        }
    }
}
