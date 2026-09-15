#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The geometric role a module plays in a cell. It decides where the module is placed and,
    /// when no licensed art is present, which primitive stands in for it.
    /// </summary>
    public enum ModuleShape
    {
        /// <summary>Nothing is drawn.</summary>
        None = 0,

        /// <summary>A panel on the vertical face between two cells: wall, window, door.</summary>
        WallPanel = 1,

        /// <summary>A slab at the cell's lower boundary.</summary>
        FloorSlab = 2,

        /// <summary>A block filling the whole cell: rock, fill, salvage, rubble.</summary>
        SolidBlock = 3,

        /// <summary>A vertical member at the cell centre.</summary>
        Pillar = 4,

        /// <summary>Half a flight, rising 1.5 m across one cell. Two chained halves climb a layer.</summary>
        StairFlight = 5,

        /// <summary>A full-layer ladder against one face of the cell.</summary>
        Ladder = 6,
    }

    /// <summary>
    /// One row of the catalogue: a module id the simulation emits, and the art that draws it.
    ///
    /// <see cref="prefab"/> is a direct reference into <c>Assets/Synty</c>, which is licensed and
    /// not in the repository. On a clone without the packs the reference resolves to null and the
    /// renderer falls back to <see cref="shape"/>, so the world still draws and nothing throws.
    /// <see cref="prefabName"/> records what the reference was, so the editor tool can rebuild it
    /// once the packs are imported and so a missing row can name itself in the log.
    /// </summary>
    [Serializable]
    public sealed class ModuleEntry
    {
        [Tooltip("The module id the simulation emits, e.g. odyssey.module.wall.panel.")]
        public string moduleId = string.Empty;

        [Tooltip("Where the module sits in its cell, and which primitive stands in without Synty.")]
        public ModuleShape shape = ModuleShape.SolidBlock;

        [Tooltip("Synty prefab. Null on a clone without the licensed packs; the fallback is used.")]
        public GameObject? prefab;

        [Tooltip("The prefab this row wants, by name. Used to rebuild the reference and to report gaps.")]
        public string prefabName = string.Empty;

        [Tooltip("Centre the art on the cell in x and z. Off for pieces whose pivot is deliberate.")]
        public bool centreXZ = true;

        [Tooltip("Sit the art's lowest point on the placement height. Off for pieces with a skirt.")]
        public bool baseAtY = true;

        [Tooltip("Extra local offset in metres, applied after normalisation.")]
        public Vector3 offset = Vector3.zero;

        [Tooltip("Extra local yaw in degrees. Use it when a piece faces the wrong way.")]
        public float yaw;

        [Tooltip("Uniform scale applied to the art. 1 unless a piece must be stretched to the cell.")]
        public Vector3 scale = Vector3.one;
    }

    /// <summary>
    /// Module id to art, as data rather than code.
    ///
    /// Worldgen names presentation modules by **id string only** (<c>TemplateDef.wallModuleId</c>
    /// and its siblings), and never resolves one — that is what lets the simulation run headless
    /// in a clone with no <c>Assets/</c> content at all. This asset is the other half of that
    /// contract: the single place where an id becomes a mesh. Changing which prefab draws a wall
    /// is an edit to an asset, not to a renderer.
    ///
    /// Build or refresh it with <c>Odyssey &gt; Presentation &gt; Rebuild module catalogue</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Odyssey/Module catalogue", fileName = "ModuleCatalogue")]
    public sealed class ModuleCatalogue : ScriptableObject
    {
        [SerializeField] List<ModuleEntry> entries = new List<ModuleEntry>();

        public IReadOnlyList<ModuleEntry> Entries => entries;

        public ModuleEntry? Find(string moduleId)
        {
            for (int i = 0; i < entries.Count; i++)
                if (string.Equals(entries[i].moduleId, moduleId, StringComparison.Ordinal))
                    return entries[i];
            return null;
        }

        /// <summary>Replace the whole table. Used by the editor generator; not a runtime call.</summary>
        public void SetEntries(List<ModuleEntry> newEntries) => entries = newEntries;

        /// <summary>How many rows have live art. The rest fall back to primitives.</summary>
        public int ResolvedPrefabCount()
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++) if (entries[i].prefab != null) n++;
            return n;
        }
    }

    /// <summary>
    /// The module ids this build knows about.
    ///
    /// The template ids are the ones authored in <c>TemplateLibrary</c> plus the defaults declared
    /// on <c>TemplateDef</c>; the terrain ids are ours, because terrain is not authored per
    /// template. Nothing here is required to exist in the catalogue — an unknown id falls back to
    /// a primitive and logs once.
    /// </summary>
    public static class ModuleIds
    {
        public const string Prefix = "odyssey.module.";

        // Defaults declared on TemplateDef, used by any template that does not override them.
        public const string Wall = Prefix + "wall";
        public const string Door = Prefix + "door";
        public const string Window = Prefix + "window";
        public const string Pillar = Prefix + "pillar";
        public const string Stair = Prefix + "stair";
        public const string Ladder = Prefix + "ladder";
        public const string Slab = Prefix + "slab";

        // Edifices worldgen places outside a template.
        public const string VaultWall = Prefix + "wall.vault";
        public const string UtilityTap = Prefix + "utility.tap";

        /// <summary>Terrain is not authored per template, so its ids are derived from the def name.</summary>
        public static string Terrain(string terrainDefName) =>
            Prefix + "terrain." + terrainDefName.ToLowerInvariant();
    }
}
