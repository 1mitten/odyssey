#nullable enable
using System;
using System.IO;
using System.Text;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Turning a save this build wrote into the bytes an older build would have written, for the
    /// tests that prove an old file still loads.
    ///
    /// <para><b>Why a helper and not a version poke.</b> Relabelling the version field alone was enough
    /// while every format since 3 shared one header layout. Format 11 (design 57) added the planet
    /// site to the header, so a relabelled file carried a byte no older build wrote, the reader took
    /// it for the start of the section list, and two ranged-combat tests failed on a negative length
    /// — nothing to do with what they test. This is the one place that knows what each version's
    /// header lacks.</para>
    /// </summary>
    public static class SaveFixtures
    {
        const int VersionOffset = 8; // after the magic

        /// <summary>
        /// The save as the given older format would have written it. Only a save with no planet site
        /// can go below 11, because no older build could have recorded one.
        /// </summary>
        public static byte[] AsFormat(byte[] save, int version)
        {
            int written = BitConverter.ToInt32(save, VersionOffset);
            if (version > written) throw new ArgumentException("a save can be made older, never newer");

            byte[] result = save;
            if (written >= 11 && version < 11)
            {
                int site = SiteFlagOffset(save);
                if (save[site] != 0)
                    throw new ArgumentException("this save has a planet site, which no format before 11 could record");
                result = new byte[save.Length - 1];
                Array.Copy(save, 0, result, 0, site);
                Array.Copy(save, site + 1, result, site, save.Length - site - 1);
            }
            else
            {
                result = (byte[])save.Clone();
            }

            BitConverter.GetBytes(version).CopyTo(result, VersionOffset);
            return result;
        }

        /// <summary>Where format 11's site flag sits: after the scalars, the recipe's strings and its two board flags.</summary>
        static int SiteFlagOffset(byte[] save)
        {
            using var binary = new BinaryReader(new MemoryStream(save), Encoding.UTF8);
            binary.ReadUInt64();   // magic
            binary.ReadInt32();    // version
            binary.ReadUInt32();   // seed
            binary.ReadInt32(); binary.ReadInt32(); binary.ReadInt32(); // size
            binary.ReadInt32();    // tick
            binary.ReadInt32();    // map
            binary.ReadBytes(binary.ReadInt32()); // scenario
            binary.ReadBytes(binary.ReadInt32()); // colony name
            binary.ReadInt32();    // day
            binary.ReadBoolean();  // barren
            binary.ReadBoolean();  // wooded
            return (int)binary.BaseStream.Position;
        }
    }
}
