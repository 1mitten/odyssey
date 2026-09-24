#nullable enable
using System.Collections.Generic;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The weapons' measured shapes, by mesh name (design 33 §9c). The table is
    /// <c>WeaponProfiles.g.cs</c>, written by <c>Odyssey.EditorTools.WeaponProfileBake.Run</c>
    /// (<c>scripts/unity.sh exec Odyssey.EditorTools.WeaponProfileBake.Run</c>) from the pack's
    /// meshes, which the editor can read and a player cannot. Numbers only: no art is copied, and
    /// a clone without the packs has the numbers and no weapon to use them on.
    /// </summary>
    public static partial class WeaponProfiles
    {
        /// <summary>The profile measured for a mesh of this name, or false.</summary>
        public static bool TryGet(string meshName, out WeaponProfile? profile) =>
            Table.TryGetValue(meshName, out profile);

        /// <summary>Every mesh name in the table.</summary>
        public static IEnumerable<string> Names => Table.Keys;
    }
}
