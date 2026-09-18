#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Presentation.Bootstrap
{
    /// <summary>
    /// The colonists the player named themselves, written into the save beside the camera pose.
    ///
    /// <para><b>Why this is a save section at all.</b> Every other identity a colonist has —
    /// their name from the pool, their face, their age, their trade — is *derived* from a roll
    /// seed the simulation already saves, which is what lets the game carry a whole cast in four
    /// bytes a head. A name the player typed cannot be derived from anything. It is the first
    /// piece of colonist identity that has to be written down, and if it is not written down then
    /// renaming somebody lasts until the next save and load, which is worse than not offering
    /// it.</para>
    ///
    /// <para><b>It is an <see cref="ISaveable"/> and deliberately not an <c>IStateHashable</c></b>,
    /// exactly as <see cref="ViewStateSection"/> is and for the same argument, which is written out
    /// in full there. What a colonist is called is not a fact about the colony: two colonies that
    /// tick identically must compare equal whether or not somebody typed a name over one of them,
    /// or the ten-day soak would start depending on the player's typing. The simulation never sees
    /// this and cannot.</para>
    ///
    /// <para><b>A save with no name section is the normal case</b> — every save written before
    /// this existed, and every colony where the player took the three they were dealt.
    /// <c>WorldSave.Load</c> simply never calls <see cref="Load"/>, and the book stays as
    /// <see cref="Apply"/>'s caller left it, which is empty.</para>
    /// </summary>
    public sealed class ColonistNameSection : ISaveable
    {
        /// <summary>The payload's own version, for the reason
        /// <see cref="ViewStateSection.SectionVersion"/> gives.</summary>
        public const int SectionVersion = 1;

        /// <summary>Stable, and separate from the class name: renaming it silently orphans every
        /// save that carries it.</summary>
        public string SaveKey => "colonistnames";

        readonly List<KeyValuePair<int, string>> _loaded = new List<KeyValuePair<int, string>>();

        /// <summary>How many names came out of the file. Zero is the ordinary case.</summary>
        public int Count => _loaded.Count;

        /// <summary>
        /// Read the live book, immediately before the section is handed to <c>WorldSave.Save</c>.
        ///
        /// <para><b>Sorted by pawn id.</b> A dictionary's order is its own business and can differ
        /// between two runs that did the same thing, so writing it raw would make two saves of one
        /// colony differ byte for byte — which is a needless obstacle to anybody diffing a save to
        /// find out what moved.</para>
        /// </summary>
        public void Capture(ColonistNameBook book)
        {
            _loaded.Clear();
            foreach (KeyValuePair<int, string> entry in book.Entries) _loaded.Add(entry);
            _loaded.Sort((a, b) => a.Key.CompareTo(b.Key));
        }

        /// <inheritdoc/>
        public void Save(SaveWriter writer)
        {
            writer.Write(SectionVersion);
            writer.Write(_loaded.Count);
            foreach (KeyValuePair<int, string> entry in _loaded)
            {
                writer.Write(entry.Key);
                writer.Write(entry.Value);
            }
        }

        /// <inheritdoc/>
        public void Load(SaveReader reader)
        {
            _loaded.Clear();

            int version = reader.ReadInt();
            if (version > SectionVersion) return; // a newer build wrote it; nothing here is safe to read

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt();
                string name = reader.ReadString();
                _loaded.Add(new KeyValuePair<int, string>(id, name));
            }
        }

        /// <summary>
        /// Put the names back into the book.
        ///
        /// <para><b>The book is cleared first, unconditionally.</b> The caller has just torn one
        /// colony down and built another, and a name left over from the last one would attach
        /// itself to whichever colonist happened to be given that id here — a stranger answering
        /// to somebody else's name, which reads as the feature working and is the worst way for it
        /// to be wrong. That is the same trap <see cref="ViewStateSection"/> records for the
        /// camera, and it is why this clears rather than merges.</para>
        ///
        /// <para>Cleaned on the way in as well as on the way out, so a hand-edited or
        /// older-format file cannot put a name on screen that the field would not let anybody
        /// type.</para>
        /// </summary>
        public void Apply(ColonistNameBook book)
        {
            book.Clear();
            foreach (KeyValuePair<int, string> entry in _loaded)
                book.Rename(new PawnId(entry.Key), entry.Value);
        }
    }
}
