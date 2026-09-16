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

        /// <summary>
        /// Take only the *material* from the prefab and keep the primitive box for the mesh.
        ///
        /// Solid ground needs this. A terrain cell is meshed as a body filling the cell, so
        /// pointing it at a flat ground-tile prefab drew a thin plane floating inside each cell
        /// and z-fought with its neighbours. What is actually wanted is the cell-shaped box
        /// wearing the pack texture, which is what this gives.
        /// </summary>
        public bool materialOnly;

        /// <summary>
        /// A material to dress the primitive box in directly, with no prefab involved.
        ///
        /// This is how ground gets a real texture, and the distinction from
        /// <see cref="materialOnly"/> is the whole point rather than a detail. A pack *prop*
        /// wears the shared colour atlas, where every material in the set is one small swatch of
        /// one big image; stretched over a cell-sized box it samples the entire atlas across each
        /// face, which is where the stray blades of grass and the dark patches came from. A pack
        /// *terrain* material is an ordinary tiling texture with none of that, so it is the one
        /// that can be worn by a box.
        ///
        /// Seamlessness comes free and is worth stating, because it is the reason this works at
        /// all: each face carries one unit of UV, the texture tiles twice across it, and the
        /// texture is authored to wrap — so the pattern runs on across a cell boundary instead of
        /// restarting, and the ground reads as a field rather than as a grid of stamps.
        ///
        /// Null on a clone without the licensed packs, exactly like <see cref="prefab"/>, and the
        /// row falls back to a flat tint.
        /// </summary>
        public Material? material;

        [Tooltip("The material this row wants, by asset name. Used to rebuild the reference.")]
        public string materialName = string.Empty;

        /// <summary>
        /// How many times the texture repeats across one cell. Zero leaves the material alone.
        ///
        /// One is what makes ground read as tiles laid on the floor. A pack terrain material is
        /// authored for a Unity terrain hundreds of metres across, so it carries a repeat count
        /// suited to that; worn by a 2.5 m cell the same setting crams two copies of the pattern
        /// into every tile, and the surface reads as fine noise rather than as a floor. Matching
        /// one repeat to one cell puts the pattern on the game's own grid, which is what the eye
        /// is looking for.
        /// </summary>
        public float materialTilesPerCell;

        /// <summary>
        /// Drop the material's normal map.
        ///
        /// Pack terrain materials carry one so that ground looks rough under a moving first-person
        /// light. This game looks down at a board under a fixed overhead sun, where the same
        /// normal map only breaks the light up into a bumpy mottle and stops a tile reading as
        /// flat and evenly lit. The art direction is flat-shaded low poly; the ground should match.
        /// </summary>
        public bool flattenNormalMap;

        /// <summary>
        /// An animation clip to pose the prefab in before its skinned mesh is baked.
        ///
        /// Without one the bake captures the **bind pose**, which for a humanoid is a T-pose: arms
        /// straight out, legs together. That is not an error and nothing would report it; the game
        /// would simply be full of people standing like scarecrows. Sampling a standing idle clip
        /// first is what makes a baked character read as a person.
        ///
        /// Null on a clone without the packs, in which case the bind pose is used and the figure
        /// looks wrong but still draws. Since a clone has no character prefab either, that case
        /// does not arise in practice.
        /// </summary>
        public AnimationClip? poseClip;

        [Tooltip("The clip this row wants, by asset name. Used to rebuild the reference.")]
        public string poseClipName = string.Empty;

        [Tooltip("Seconds into the pose clip to sample. Any settled frame of an idle will do.")]
        public float poseClipTime = 0f;

        /// <summary>
        /// Gaits this module can be animated through, slowest first, blended by how fast the thing
        /// is actually moving. Empty for everything that is not a pawn.
        ///
        /// The baked mesh in <see cref="poseClip"/> is not replaced by this and is not a rival to
        /// it: baking stays the cheap form for a crowd, and these drive the live figures that the
        /// handful of pawns on screen are given instead. A module with no gaits simply never gets
        /// a live figure.
        /// </summary>
        public List<LocomotionEntry> locomotion = new List<LocomotionEntry>();
    }

    /// <summary>
    /// One gait: a clip, and how fast over the ground that clip was authored to look right at.
    ///
    /// The speed is what stops the feet from sliding, and it is **measured rather than guessed**.
    /// The locomotion pack ships every clip twice, once in place and once with root motion; the
    /// in-place twin is what a pawn plays, because the simulation decides where anybody is, and
    /// the root-motion twin is what says how far that stride was meant to carry someone. The
    /// editor reads the second to calibrate the first, so nobody has to eyeball a number that the
    /// art already knows.
    /// </summary>
    [Serializable]
    public sealed class LocomotionEntry
    {
        [Tooltip("The in-place clip a pawn plays, by asset name.")]
        public string clipName = string.Empty;

        [Tooltip("The clip. Null on a clone without the packs, which falls back to the baked mesh.")]
        public AnimationClip? clip;

        [Tooltip("The root-motion twin this gait's speed was measured from, by asset name.")]
        public string speedFromClipName = string.Empty;

        [Tooltip("Metres per second the gait covers ground at. Zero means standing still.")]
        public float metresPerSecond;
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

        /// <summary>
        /// Every row whose id is <paramref name="moduleIdPrefix"/> or starts with it and a dot, in
        /// catalogue order.
        ///
        /// This is how a family of interchangeable variants is discovered — the colonist faces,
        /// and whatever comes next — so that how many there are is a fact about the asset rather
        /// than a constant somebody has to remember to raise. The dot matters: without it
        /// <c>pawn.colonist</c> would also collect a hypothetical <c>pawn.colonistguard</c>.
        /// </summary>
        public List<ModuleEntry> FindFamily(string moduleIdPrefix)
        {
            var family = new List<ModuleEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                string id = entries[i].moduleId;
                if (string.Equals(id, moduleIdPrefix, StringComparison.Ordinal) ||
                    (id.Length > moduleIdPrefix.Length &&
                     id.StartsWith(moduleIdPrefix, StringComparison.Ordinal) &&
                     id[moduleIdPrefix.Length] == '.'))
                    family.Add(entries[i]);
            }
            return family;
        }

        /// <summary>
        /// How many rows have live art, counting a resolved material as art in its own right —
        /// a textured ground row draws from the packs just as surely as one with a prefab does.
        /// The rest fall back to tinted primitives.
        /// </summary>
        public int ResolvedPrefabCount()
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].prefab != null || entries[i].material != null) n++;
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

        /// <summary>
        /// The colonist figures. Not placed in a cell by worldgen or the mesher: pawns move every
        /// tick and are drawn from the published snapshot, so these are resolved once and drawn by
        /// the actor pass rather than meshed into a chunk.
        ///
        /// A **family** rather than one id, because a colony of five identical people is the
        /// single most artificial thing on the board. Every character in the packs is built on the
        /// same humanoid rig, so any of them can be a colonist at no cost beyond a catalogue row
        /// and the locomotion clips retarget onto all of them without per-character work.
        ///
        /// How many there are is a question for the catalogue, not for this code: add a row and a
        /// new face appears. <see cref="ModuleCatalogue.FindFamily"/> is how a caller asks.
        /// </summary>
        public const string ColonistBase = Prefix + "pawn.colonist";

        /// <summary>The id of one colonist variant. Variant 0 keeps the unsuffixed id.</summary>
        public static string Colonist(int variant) =>
            variant <= 0 ? ColonistBase : ColonistBase + "." + variant.ToString();

        // Loose items lying in a cell: a crate of rations to be eaten, a heap of scrap to be
        // hauled. These are drawn by the actor pass for the same reason the colonist is — they
        // come from the published snapshot rather than from the mirror, because an item that is
        // picked up and carried moves without any cell changing.
        public const string ItemMeal = Prefix + "item.meal";
        public const string ItemSalvage = Prefix + "item.salvage";
        public const string ItemWood = Prefix + "item.wood";
        public const string ItemStone = Prefix + "item.stone";
        public const string ItemIronOre = Prefix + "item.ironore";
        public const string ItemCoal = Prefix + "item.coal";

        /// <summary>
        /// Module ids for item def indices, in <c>ItemIndex</c> order.
        ///
        /// The order is the coupling, and it is the whole point of the array: this is
        /// <c>ItemIndex</c> read from the presentation side, and <c>ModuleIdTests</c> fails if the
        /// two drift apart. Until this existed every item on the ground drew as the same orange
        /// stand-in box, which is what made a barren map look like it had been spattered with
        /// paint.
        /// </summary>
        static readonly string[] ItemModules =
        {
            ItemMeal, ItemSalvage, ItemWood, ItemStone, ItemIronOre, ItemCoal,
        };

        /// <summary>How many item def indices have a module. Must equal <c>ItemIndex.Count</c>.</summary>
        public static int ItemModuleCount => ItemModules.Length;

        /// <summary>The module for an item def index, or null when it has none and falls back.</summary>
        public static string? Item(int itemDefIndex) =>
            itemDefIndex >= 0 && itemDefIndex < ItemModules.Length ? ItemModules[itemDefIndex] : null;

        // Tufts of grass strewn over the ground. Decoration and nothing else: they block nothing,
        // are not in the save, and the simulation has never heard of them. What they are for is
        // that a field of one flat colour reads as a carpet, and a field with clumps standing up
        // out of it reads as ground.
        public const string GrassTuftA = Prefix + "scatter.grass.a";
        public const string GrassTuftB = Prefix + "scatter.grass.b";
        public const string GrassTuftC = Prefix + "scatter.grass.c";

        static readonly string[] GrassTufts = { GrassTuftA, GrassTuftB, GrassTuftC };

        public static int GrassTuftCount => GrassTufts.Length;

        public static string GrassTuft(int variant) => GrassTufts[variant];

        /// <summary>Terrain is not authored per template, so its ids are derived from the def name.</summary>
        public static string Terrain(string terrainDefName) =>
            Prefix + "terrain." + terrainDefName.ToLowerInvariant();
    }
}
