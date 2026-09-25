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

        /// <summary>
        /// A block filling the whole cell, chipped and faceted: stone rather than masonry.
        ///
        /// Fills the cell exactly like <see cref="SolidBlock"/> and is interchangeable with it —
        /// the difference is only which mesh stands in, and <see cref="RockMesh"/> guarantees the
        /// lump never shrinks inside the cell, so a run of them still tiles without a crack.
        /// </summary>
        RockBlock = 7,

        /// <summary>
        /// A block filling the whole cell, with an uneven top: earth rather than a cast slab.
        ///
        /// Fills the cell exactly like <see cref="SolidBlock"/> and is interchangeable with it.
        /// <see cref="GroundMesh"/> keeps the middle of the top face pinned at the layer height,
        /// because that is where everything in the world is drawn standing, and moves nothing in
        /// plan, so a run of them tiles without a crack.
        /// </summary>
        GroundBlock = 8,

        /// <summary>
        /// The same block where a side of it can be seen: the walls are built in courses so a
        /// terrace riser is a broken face rather than one ruled 3 m rectangle.
        ///
        /// A separate shape rather than a flag on <see cref="GroundBlock"/> because the two are
        /// different meshes and a mesh is what a bucket is keyed by. Ground is the largest
        /// instance population in the world and nearly all of it never shows a side, so the
        /// expensive geometry is worth confining to the cells that do.
        /// </summary>
        GroundFace = 9,

        /// <summary>
        /// A stepped earth bank filling an empty cell beside a terrace step, climbing from the
        /// floor of its own cell to the top of it.
        ///
        /// Occupies the cell exactly, like the block shapes, but it is not a block: it is drawn in
        /// a cell that is empty, and nothing in the simulation knows it is there. See
        /// <see cref="BankMesh"/>.
        /// </summary>
        Bank = 10,

        /// <summary>
        /// The surface of a body of water: one sheet in plan at the height the water stands at,
        /// facing up.
        ///
        /// A sheet and not a slab, which is the difference that lets water have a side at all —
        /// see <see cref="WaterMesh"/> for why a box forced the shader to clip every vertical
        /// fragment, and what that cost.
        /// </summary>
        WaterSurface = 11,

        /// <summary>
        /// The face a body of water shows where nothing beside it holds it in: the side of the
        /// channel at a lip, and the falling sheet at a cascade step.
        ///
        /// One local unit tall, hanging from its origin, so the instance matrix decides how far it
        /// falls. See <see cref="WaterMesh.Fall"/>.
        /// </summary>
        WaterFall = 12,

        /// <summary>
        /// A pillow: a rounded box, smooth-shaded, spanning the unit box like every other stand-in.
        ///
        /// A shape of its own rather than a flag on <see cref="SolidBlock"/> because the two are
        /// different meshes and a mesh is what a bucket is keyed by — the same argument
        /// <see cref="GroundFace"/> makes. See <see cref="PillowMesh"/> for why a bed's pillow is
        /// the one soft thing in the renderer.
        /// </summary>
        Pillow = 13,
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
    /// <summary>
    /// Which gendered pool a body or a hair piece belongs to
    /// (<c>docs/design/29-modular-colonists.md</c> §6).
    ///
    /// <see cref="Either"/> is not "unknown": PolygonGeneric's hair is not authored per sex, and a
    /// piece marked Either is legal for anybody. A body is always one or the other.
    /// </summary>
    public enum BodySex
    {
        Either = 0,
        Male = 1,
        Female = 2,
    }

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

        [Tooltip("The folder under Assets/Synty the prefab is looked for in first, when more than one pack has a prefab of that name. Empty: any pack, in path order.")]
        // Because the packs share names: SM_Env_Bush_01 is in Battle Royale, Western Frontier
        // and Meadow Forest, and a lookup by name alone takes whichever sorts first — so the
        // Meadow dressing drew Battle Royale's bushes and rocks, which carry no levels of detail
        // (design 38 §18c).
        public string prefabUnder = string.Empty;

        [Tooltip("Centre the art on the cell in x and z. Off for pieces whose pivot is deliberate.")]
        public bool centreXZ = true;

        [Tooltip("Sit the art's lowest point on the placement height. Off for pieces with a skirt.")]
        public bool baseAtY = true;

        /// <summary>
        /// Sit the art's <b>highest</b> point just above the placement height — by
        /// <see cref="CellMetrics.SlabLift"/> — which is the rule for anything walked <i>on</i>
        /// rather than stood <i>in</i>.
        ///
        /// <para><b>Just above, never on.</b> The cell's floor plane is also the top face of the
        /// block filling the cell below, so a surface levelled exactly on to it is coplanar with
        /// the ground and z-fights. <see cref="CellMetrics.SlabLift"/> holds the clearance and the
        /// measurement behind it; it lives with this rule so that a future walked-on piece cannot
        /// forget it.</para>
        ///
        /// <para><b>A floor slab is drawn at the cell's lower boundary</b>, which is the plane a
        /// colonist's feet are on, so the face that matters is the slab's <em>top</em> and not its
        /// pivot. Without this each slab art landed on whatever convention its own prefab used, and
        /// two floors on one layer drew at two heights: measured 2026-09-18, the plank deck's top
        /// at +0.008 m and the street tile's at +0.033 m, so a stone floor stood 25 mm proud of the
        /// wood beside it (`SlabHeightProbe`). The owner reported that as a stone tile "one height
        /// below" the deck (`docs/design/15-building.md`).</para>
        ///
        /// <para>Mutually exclusive with <see cref="baseAtY"/>, which asks the opposite question.
        /// <see cref="baseAtY"/> wins if both are set, because it is the older rule and every piece
        /// already using it is placed correctly.</para>
        /// </summary>
        [Tooltip("Sit the art's highest point on the placement height. For floors, which are walked on.")]
        public bool topAtY;

        [Tooltip("Extra local offset in metres, applied after normalisation.")]
        public Vector3 offset = Vector3.zero;

        [Tooltip("Extra local yaw in degrees. Use it when a piece faces the wrong way.")]
        public float yaw;

        /// <summary>
        /// Lay the art on its broadest face: its thinnest axis turned vertical before anything
        /// else is measured. For a thing modelled standing up that is dropped on the ground — a
        /// weapon is modelled haft-up, the way a hand holds it, and a bat stood on its end in a
        /// field reads as a post (C3, the integration, 2026-09-23). Applied before the centring
        /// and the base, so both measure the piece as it lies.
        /// </summary>
        [Tooltip("Lay the art on its broadest face (thinnest axis up). For props modelled standing, dropped on the ground.")]
        public bool lieFlat;

        [Tooltip("Uniform scale applied to the art. 1 unless a piece must be stretched to the cell.")]
        public Vector3 scale = Vector3.one;

        /// <summary>
        /// Fit the model into this rectangle, in metres — <c>x</c> across, <c>y</c> along the
        /// facing, the model's own +Z being its front — scaled uniformly unless
        /// <see cref="fitStretch"/> says otherwise (design 32 §14, §14c). Zero is off, which every row before power is. It exists because a pack
        /// prop is modelled at whatever size its artist chose, and a row that had to carry a
        /// measured scale would be a number nobody could check without opening the editor.
        /// </summary>
        public Vector2 fitFootprint;

        /// <summary>With <see cref="fitFootprint"/>: never taller than this, in metres. Zero is no ceiling.</summary>
        public float fitHeight;

        /// <summary>
        /// With <see cref="fitFootprint"/>: scale each axis on its own so the model <b>fills</b>
        /// the rectangle and the height, rather than fitting its tightest axis and leaving gaps on
        /// the other two. For a machine that is two cells long (design 32 §14c): fitted uniformly,
        /// the generator stood 3.1 m long in 5 m of footprint with a metre of daylight at each end.
        /// </summary>
        public bool fitStretch;

        /// <summary>
        /// With <see cref="fitFootprint"/>: stand the model's back on the back edge of the
        /// rectangle rather than centring it, so a thing placed beside a wall stands against it
        /// (design 32 §14c). The back is the model's −Z; its front looks out along the facing.
        /// </summary>
        public bool fitAgainstBack;


        /// <summary>
        /// May a colonist be dealt this body?
        ///
        /// <para><b>False does not mean the row is dead.</b> The Farm, Sci-Fi City and Western
        /// Frontier bodies stay in the colonist family and stay resolvable — the city's own
        /// inhabitants, traders and raiders will want them — they simply leave the lottery
        /// (owner, 2026-09-22). The family index space is unchanged, which is the point: it is
        /// the look index space, and compacting it is the fault
        /// <c>ColonistAppearanceBook</c> records.</para>
        /// </summary>
        public bool colonistPool;

        /// <summary>
        /// Which gendered pool this row belongs to — a body's shape, or a hair piece's register.
        /// </summary>
        public BodySex sex = BodySex.Either;

        /// <summary>
        /// Does this piece recolour?
        ///
        /// <para>Most hair and beard meshes map every vertex to the single atlas texel the scalp
        /// already uses, so repainting the hair rectangle recolours all three together. Six do
        /// not — they span real texture, and repainting them throws art away
        /// (<c>docs/research/e-06-modular-colonists.md</c> §7). Those are excluded here, in data,
        /// rather than by a rule in code.</para>
        /// </summary>
        public bool recolours = true;

        /// <summary>
        /// Is this body the colony's issued uniform?
        ///
        /// <para>Exactly one row per sex carries it. Until clothing is a thing a colonist can be
        /// given (<c>docs/design/29-modular-colonists.md</c> §9), every colonist wears the uniform
        /// and the rest of the pool is what the clothing system will draw from — so the pool is
        /// kept and flagged rather than emptied.</para>
        /// </summary>
        public bool uniform;

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

        /// <summary>
        /// The clips a <b>combat row</b> plays (design 33 §1, <c>ModuleIds.CombatRows</c>): one
        /// entry per variant of the role — a direction, a combo step, or a begin, loop or end.
        /// Empty on every other row, and every clip null on a checkout without the Sword Combat
        /// pack, which is what sends the figure to <c>CombatPose</c>'s computed version.
        /// </summary>
        public List<CombatClipEntry> combat = new List<CombatClipEntry>();

        /// <summary>
        /// The settled idle a live figure blends into while it sits beside a fire (design 31
        /// §18d, <c>SitPose</c>). Not a gait: gaits are chosen by speed and a sitter has none, so
        /// this is its own input on the figure's mixer, weighted by how far down she is.
        ///
        /// <para>Null on every row that cannot sit, and on a clone without the packs; either way
        /// the figure stands at the fire, which is what it did before this existed.</para>
        /// </summary>
        public AnimationClip? sitClip;

        [Tooltip("The settled idle this row sits in, by asset name. Used to rebuild the reference.")]
        public string sitClipName = string.Empty;

        /// <summary>
        /// Lay a <b>computed</b> four-legged gait over this row's idle (design 29 §8a,
        /// <c>QuadrupedGait</c>). The stride is measured off the rig's own legs at build, not
        /// declared here. Off, the default, means the row's clips are its whole locomotion. On for
        /// the hog, which has no walk clip; the rat walks on its own clips.
        /// </summary>
        [Tooltip("Lay a computed four-legged gait over the idle. Off = the clips are the locomotion.")]
        public bool quadrupedGait;

        /// <summary>
        /// Which parts of this body's atlas are its skin, its hair and its clothes, so a colonist
        /// can be recoloured. Empty on everything that is not a colonist.
        /// </summary>
        public AppearanceCells appearance = new AppearanceCells();

        /// <summary>
        /// One of the bandit gang's bodies (<c>docs/design/42-bandits.md</c>): never in the
        /// colonist lottery, dealt only to a hostile person by <c>ColonistCastPools.BanditMale</c>
        /// and <c>BanditFemale</c>, which read this.
        /// </summary>
        public bool bandit;

        /// <summary>
        /// A skinned overlay the rig already carries, switched <b>on</b> for this row after the
        /// head is bared — the bandit's armour vest, <c>SM_Char_Attach_Male_Armor_02</c> and its
        /// kin. Every Battle Royale rig ships six of them inactive. Empty on every other row.
        /// </summary>
        public string overlayName = string.Empty;

        /// <summary>
        /// Where the overlay takes its colour from, which is <b>not</b> where the body does: the
        /// vest's camo and the male rig's trousers share one region of the atlas, so one set of
        /// rectangles would paint the trousers red or the vest black. Filled by
        /// <c>CharacterSwatches</c> with the rest of the appearance.
        /// </summary>
        public AppearanceCells overlayAppearance = new AppearanceCells();
    }

    /// <summary>How confidently a body was carved into recolourable regions.</summary>
    public enum AppearanceQuality
    {
        /// <summary>Never classified. Draws exactly as the art was painted.</summary>
        None = 0,

        /// <summary>Skin, hair and clothing all found.</summary>
        Full = 1,

        /// <summary>No hair region: bald, hooded or helmeted. Skin and clothing still vary.</summary>
        NoHair = 2,

        /// <summary>
        /// No skin region. A robot, a full helmet, a body in gloves and a visor. Correct rather
        /// than broken — the cop really has no skin showing, which the swatch probe confirmed.
        /// </summary>
        NoSkin = 3,

        /// <summary>Clothing only: neither skin nor hair could be told apart.</summary>
        ClothOnly = 4,

        /// <summary>
        /// Two slots wanted the same swatch cell and the smaller one was dropped. The honest cost
        /// of recolouring by UV region rather than by an authored per-vertex mask.
        /// </summary>
        Shared = 5,
    }

    /// <summary>
    /// The rectangles of the pack atlas that one body's skin, hair and clothing are painted from.
    ///
    /// <para>These are <b>measurements, not art</b>: UV coordinates read off a licensed mesh, in
    /// exactly the standing the mesh bounds and gait speeds already committed to this asset have.
    /// No pixel is copied anywhere, which is what keeps <c>Assets/Synty</c> the only place the
    /// packs live.</para>
    ///
    /// <para>Two rectangles per slot, because a slot is not always one cell — the farmer's skin is
    /// two clusters and the bandit's hair is two. An unused rectangle is left empty, and the
    /// shader treats an empty rectangle as one no UV is inside, so an unclassified slot needs no
    /// special case anywhere.</para>
    /// </summary>
    [Serializable]
    public sealed class AppearanceCells
    {
        public Rect[] skin = Array.Empty<Rect>();
        public Rect[] hair = Array.Empty<Rect>();
        public Rect[] cloth = Array.Empty<Rect>();
        public Rect[] cloth2 = Array.Empty<Rect>();

        [Tooltip("Vertices in each slot, and in the body, as the classifier counted them.")]
        public int skinVerts;
        public int hairVerts;
        public int clothVerts;
        public int cloth2Verts;
        public int totalVerts;

        public AppearanceQuality quality = AppearanceQuality.None;

        /// <summary>True when this body has anything to recolour at all.</summary>
        public bool Any => quality != AppearanceQuality.None &&
                           (skin.Length > 0 || hair.Length > 0 || cloth.Length > 0);
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
    /// One clip of a combat row: which variant of the role it is and, for a blow, where in it the
    /// blow lands (design 33 §3, <c>docs/research/synty-sword-combat.md</c>).
    ///
    /// <para><b>The impact is measured, not typed.</b> Every attack in the Sword Combat pack is
    /// cut by its author into WindUp, Hit and FollowThrough sub-clips, so the frame the blade
    /// lands is the WindUp's last frame. The catalogue build reads it off the importer's own clip
    /// ranges and writes it here in seconds from the clip's start; the figure then plays the clip
    /// at the rate that puts this instant on the simulation's <c>windupTicks</c>.</para>
    /// </summary>
    [Serializable]
    public sealed class CombatClipEntry
    {
        [Tooltip("The Polygon, in-place, non-returning clip, by its own name.")]
        public string clipName = string.Empty;

        [Tooltip("The clip. Null on a clone without the Sword Combat pack; the computed pose stands in.")]
        public AnimationClip? clip;

        [Tooltip("Which variant: F/B/L/R for a direction, A/B/C for a combo step, Begin/Loop/End for a phase.")]
        public string variant = string.Empty;

        [Tooltip("Seconds from the clip's start to the blow landing: where its WindUp sub-clip ends. 0 for a clip with no blow.")]
        public float impactSeconds;

        /// <summary>
        /// The file the clip lives in, when that is not its own name — a jump's landing is inside
        /// the take-off's file (design 46 §7). Empty for every row whose clip is its file's.
        /// </summary>
        [Tooltip("The FBX the clip is in, by name, when that is not the clip's own name. Empty = the same.")]
        public string fileName = string.Empty;
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
        public const string DoorLeaf = Prefix + "door.leaf";
        public const string Window = Prefix + "window";
        public const string Pillar = Prefix + "pillar";
        public const string Stair = Prefix + "stair";
        public const string Ladder = Prefix + "ladder";
        public const string Slab = Prefix + "slab";

        /// <summary>
        /// The face a body of water shows where nothing holds it in.
        ///
        /// One id for both water depths, because the mesh is the same sheet and shallow or deep is
        /// carried by the tint — the same bargain the surface itself makes.
        /// </summary>
        public const string WaterFall = Prefix + "water.fall";

        /// <summary>
        /// The slab art for one material, when that material deserves its own.
        ///
        /// <para>A slab took its mesh from the template's group and its colour from a stuff tint,
        /// which works while every material is a shade of concrete and fails the moment two of
        /// them are not. Every floor id in the catalogue resolved to the same wooden deck mesh, so
        /// a stone floor was wooden planks multiplied by a near-white grey — indistinguishable
        /// from the wood one beside it (owner, 2026-09-17). A tint cannot fix that: multiply only
        /// darkens, and brown times grey is browner.</para>
        ///
        /// <para>No row is required. An id with no art falls back, and
        /// <c>WorldRenderModel</c> takes the group's slab instead — so a clone without the
        /// licensed packs draws exactly what it drew before.</para>
        /// </summary>
        public static string SlabOf(string material) => Prefix + "slab." + material;

        /// <summary>
        /// The mass a wall is made of, behind the panels on its faces.
        ///
        /// A wall cell is drawn as a panel on each face something can be seen through, which is
        /// what stops a one-cell wall reading as a 2.5 m slab. It also left the cell hollow and
        /// open at the top, so from a high camera every wall had a black slot down its middle and
        /// a slice looked straight into it (owner, 2026-09-17). This fills the cell behind the
        /// panels and caps it.
        ///
        /// No row is expected and none is needed: it falls back to the cell-shaped primitive and
        /// takes the wall's own stuff tint, so it matches the panels in colour if not in texture.
        /// The day a capping course is worth art, a row here is all it takes.
        /// </summary>
        public const string WallCore = Prefix + "wall.core";

        // Edifices worldgen places outside a template.
        public const string VaultWall = Prefix + "wall.vault";
        public const string UtilityTap = Prefix + "utility.tap";

        /// <summary>
        /// The bed. No catalogue row exists and none is owed yet: the id resolves to the plain
        /// block placeholder, tinted by the stuff the bed was built of, and drawn by the mesher as
        /// frame, mattress and pillow from scaled instances of it (design 20 §9) — the computed
        /// swing's idiom, an honest stand-in rather than borrowed art. The day real two-cell bed
        /// art lands, one row on this id upgrades every bed with no code change.
        /// </summary>
        public const string Bed = Prefix + "bed";

        /// <summary>
        /// The campfire. As the bed is: no catalogue row owed, the plain block placeholder in
        /// the stuff's tint until real art lands — a ring of stones reads fine as a low block,
        /// and the fire's warmth is a number the pane carries, not a thing the mesh does. One
        /// row on this id upgrades every campfire when the art arrives.
        /// </summary>
        public const string Campfire = Prefix + "campfire";

        /// <summary>
        /// The wood-fired generator and the heater (design 32). The campfire's deal: no catalogue
        /// row owed yet, the plain block in the stuff's tint until the owner picks art — whether a
        /// generator is running is a colour on the lines and a row on the pane, not a thing the
        /// mesh does. One row on either id upgrades every one of them when the art arrives.
        /// </summary>
        public const string Generator = Prefix + "generator";

        /// <summary>See <see cref="Generator"/>.</summary>
        public const string Heater = Prefix + "heater";

        /// <summary>
        /// The galley (design 48 §5), the electric cooker: the POLYGON Shops stove, drawn once from
        /// the head and turned to the player's facing like the generator. A clone without that pack
        /// resolves it to nothing and draws the tinted block, as every machine does.
        /// </summary>
        public const string Galley = Prefix + "galley";

        /// <summary>
        /// The bed's pillow, which is a module of its own so it can be a different shape and a
        /// different colour from the rest of the bed. Bedding is linen whatever the frame is made
        /// of: a stone bed has a white pillow, exactly as a wooden one does.
        /// </summary>
        public const string BedPillow = Prefix + "bed.pillow";

        /// <summary>
        /// The shelf. No catalogue row and none owed: the id resolves to the plain block
        /// placeholder, tinted by the stuff it was built of, and drawn by the mesher as a carcass,
        /// a deck and a back lip from scaled instances of it — the bed's idiom exactly, an honest
        /// stand-in rather than borrowed art. One row on this id upgrades every shelf in the game
        /// the day real art lands.
        /// </summary>
        public const string Shelf = Prefix + "shelf";

        /// <summary>
        /// The line round a stockpile's outer edge (owner, 2026-09-23: "wash + edge outline"). No
        /// art is meant to exist for it: it resolves to the plain slab primitive, which the edge
        /// tint colours flat, the way the bed's placeholder is a box the tint colours.
        /// </summary>
        public const string StoreEdge = Prefix + "storeedge";

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

        /// <summary>
        /// The animals (design 29 §1), one row per <b>kind</b> of pawn that is not a person. The
        /// name table is parallel to the simulation's <c>PawnKindIndex</c> — 0 is the colonist and
        /// has no row here — exactly as the interface's <c>PawnKindLabels</c> is, so the three
        /// tables can be checked against each other and none has to see the others' assembly.
        /// </summary>
        public const string AnimalBase = Prefix + "pawn.animal";

        public static readonly string[] AnimalNames = { string.Empty, "hog", "rat" };

        /// <summary>The row for a kind, or empty for the colonist and for a kind past the table.</summary>
        public static string Animal(int kind) =>
            kind > 0 && kind < AnimalNames.Length ? AnimalBase + "." + AnimalNames[kind] : string.Empty;

        /// <summary>
        /// The fight's clip rows (design 33 §1, <c>docs/research/synty-sword-combat.md</c>), one
        /// row per <b>role</b>, never per clip name: a row's clips are the Sword Combat pack's
        /// Polygon, in-place, non-returning clips for that role — its directional or combo variants
        /// as the row's entries — and a checkout without the pack has the row with no clips and
        /// falls back to <c>CombatPose</c>. Claimed by the combat contracts step so the rows' ids
        /// are fixed before lane B writes the catalogue build (<c>PlayScene.cs</c>) that fills them.
        /// </summary>
        public const string CombatBase = Prefix + "anim.combat";

        /// <summary>A light one-handed swing: <c>LightCombo01A/B/C</c>, alternated.</summary>
        public const string CombatSwingLight = CombatBase + ".swing.light";

        /// <summary>A heavy swing: <c>HeavyCombo01A/B/C</c>, alternated. Never a stab — it is the blunt weapons' row.</summary>
        public const string CombatSwingHeavy = CombatBase + ".swing.heavy";

        /// <summary>Taking a blow, by direction: <c>Hit_F/B/L/R_React</c>.</summary>
        public const string CombatHitReact = CombatBase + ".react.hit";

        /// <summary>A big blow or a stun's first beat, by direction: <c>Hit_F/B/L/R_Stagger</c>.</summary>
        public const string CombatStagger = CombatBase + ".react.stagger";

        /// <summary>Getting out of the way: <c>Dodge_F/B/L</c> (never <c>_R</c>, which imports Generic).</summary>
        public const string CombatDodge = CombatBase + ".dodge";

        /// <summary>Stunned: begin, loop, end.</summary>
        public const string CombatStun = CombatBase + ".stun";

        /// <summary>Downed: <c>KnockDown_Begin</c>, then <c>_Loop</c>; the get-up for a recovery.</summary>
        public const string CombatDowned = CombatBase + ".downed";

        /// <summary>Dying, by direction: <c>Death_F/B/L/R</c>.</summary>
        public const string CombatDeath = CombatBase + ".death";

        /// <summary>The corpse: each death's one-frame <c>_Pose</c> clip, held.</summary>
        public const string CombatDeathPose = CombatBase + ".death.pose";

        /// <summary>Every combat row, for the catalogue build and the test that each resolves or falls back.</summary>
        public static readonly string[] CombatRows =
        {
            CombatSwingLight, CombatSwingHeavy, CombatHitReact, CombatStagger, CombatDodge,
            CombatStun, CombatDowned, CombatDeath, CombatDeathPose,
        };

        /// <summary>
        /// The weapon out of its sheath at the left hip and into the right hand (design 33 §8b):
        /// <c>A_Draw_Sword_Masc</c> and <c>_Femn</c>, the variant by the body's sex. Played on an
        /// upper-body layer over the walk. <b>Not in <see cref="CombatRows"/></b>, whose order is
        /// the combat roles' and whose test holds every clip without a blow to an impact of nought.
        /// </summary>
        public const string CombatDraw = CombatBase + ".sheath.draw";

        /// <summary>The weapon back from the hand to the hip: <c>A_Sheathe_Sword_Masc</c> and <c>_Femn</c>.</summary>
        public const string CombatSheathe = CombatBase + ".sheath.sheathe";

        /// <summary>The two sheath rows, for the catalogue build and the test that each resolves or snaps.</summary>
        public static readonly string[] SheathRows = { CombatDraw, CombatSheathe };

        /// <summary>
        /// Jumping a one-cell stream (design 46 §7): the take-off, <c>A_Jump_Walking_Masc</c> and
        /// <c>_Femn</c> from Base Locomotion, the variant by the body's sex as the sheath's is.
        /// In place; the arc is <c>JumpArc</c>'s. Played in the combat action slot.
        /// </summary>
        public const string JumpTakeOff = Prefix + "anim.jump.takeoff";

        /// <summary>
        /// The landing, <c>A_Land_Walking_Masc</c> and <c>_Femn</c>. <b>Inside the take-off's own
        /// file</b> (<c>A_Jump_Walking_*.fbx</c> holds both), which is why the row carries a file name
        /// beside the clip name: a lookup by file would return whichever clip came first.
        /// </summary>
        public const string JumpLand = Prefix + "anim.jump.land";

        /// <summary>The two jump rows, for the catalogue build and the test that each resolves.</summary>
        public static readonly string[] JumpRows = { JumpTakeOff, JumpLand };

        /// <summary>
        /// Hair pieces a colonist can be dealt, as a family
        /// (<c>docs/design/29-modular-colonists.md</c>).
        ///
        /// <para>A rigid prop parented to <c>HumanBodyBones.Head</c> with an identity transform —
        /// measured, in both packs (<c>docs/research/e-06-modular-colonists.md</c> §4). There is
        /// no offset to fit and no per-body special case.</para>
        /// </summary>
        public const string HairBase = Prefix + "attach.hair";

        /// <summary>The id of one hair piece.</summary>
        public static string Hair(int variant) =>
            variant <= 0 ? HairBase : HairBase + "." + variant.ToString();

        /// <summary>Beards, on exactly the same terms as <see cref="HairBase"/>.</summary>
        public const string BeardBase = Prefix + "attach.beard";

        /// <summary>The id of one beard.</summary>
        public static string Beard(int variant) =>
            variant <= 0 ? BeardBase : BeardBase + "." + variant.ToString();

        /// <summary>
        /// What covers a whole head — the bandit's welding helmet (<c>docs/design/42-bandits.md</c>
        /// §4) — on the hair's terms: a rigid prop on the head bone with an identity transform.
        /// Worn in the pack's own paint, so its rows are not recoloured.
        /// </summary>
        public const string HeadgearBase = Prefix + "attach.head";

        /// <summary>The id of one piece of headgear.</summary>
        public static string Headgear(int variant) =>
            variant <= 0 ? HeadgearBase : HeadgearBase + "." + variant.ToString();

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
        public const string ItemCarrots = Prefix + "item.carrots";

        // The four melee weapons lying on the ground (design 33 §1, C3), claimed by the combat
        // contracts step so the table below stays as long as ItemIndex. Their rows came at the
        // C2/C3 integration, and the same row is the prop a figure holds
        // (PawnFigureDirector.Weapons.cs): one piece of art for the weapon wherever it is.
        public const string ItemBat = Prefix + "item.bat";
        public const string ItemCrowbar = Prefix + "item.crowbar";
        public const string ItemMachete = Prefix + "item.machete";
        public const string ItemArcBlade = Prefix + "item.arcblade";

        /// <summary>Medical supplies (design 37): a small box, the same art on the ground, on a shelf and in an armful.</summary>
        public const string ItemMedicalSupplies = Prefix + "item.medicalsupplies";

        /// <summary>The kitchen's three meals (design 48 §4): a tray of food on the ground, on a shelf and in an armful.</summary>
        public const string ItemCookedMeal = Prefix + "item.meal.cooked";
        public const string ItemVegetableMeal = Prefix + "item.meal.vegetable";
        public const string ItemBurntMeal = Prefix + "item.meal.burnt";


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
            ItemMeal, ItemSalvage, ItemWood, ItemStone, ItemIronOre, ItemCoal, ItemCarrots,
            ItemBat, ItemCrowbar, ItemMachete, ItemArcBlade,
            ItemMedicalSupplies,
            // The kitchen (design 48 §4), handles 12 to 14.
            ItemCookedMeal, ItemVegetableMeal, ItemBurntMeal,
        };

        /// <summary>How many item def indices have a module. Must equal <c>ItemIndex.Count</c>.</summary>
        public static int ItemModuleCount => ItemModules.Length;

        /// <summary>The module for an item def index, or null when it has none and falls back.</summary>
        public static string? Item(int itemDefIndex) =>
            itemDefIndex >= 0 && itemDefIndex < ItemModules.Length ? ItemModules[itemDefIndex] : null;

        /// <summary>
        /// The axe a colonist holds while felling. Not placed in a cell and never meshed into a
        /// chunk: it is parented to a live figure's hand for as long as the work lasts, and there
        /// is nothing of it in the world the rest of the time.
        ///
        /// A catalogue row rather than a path in code, like everything else that comes out of the
        /// licensed packs, so a clone without them resolves it to null and colonists fell trees
        /// bare-handed instead of failing to start.
        /// </summary>
        public const string ToolAxe = Prefix + "tool.axe";

        /// <summary>
        /// The pick. Same pack as the axe, same pivot convention and the same haft length to the
        /// centimetre, which is why it is the one prop in the packs already known to suit the
        /// fitting path (<c>12-work-poses-and-tools.md</c> §5).
        /// </summary>
        public const string ToolPickaxe = Prefix + "tool.pickaxe";

        /// <summary>
        /// The builder's hammer. Unlike the axe and the pick it is <em>not</em> from the Generic
        /// pack, because there is no hammer in it: <c>SM_Wep_Hammer_01</c> in Western Frontier is
        /// the only one in all 7,222 imported assets. See the catalogue row in <c>PlayScene</c>
        /// for what that costs and why it is accepted.
        /// </summary>
        public const string ToolHammer = Prefix + "tool.hammer";

        /// <summary>
        /// The cook's frying pan (design 48 §10). Battle Royale's <c>SM_Wep_Pan_01</c>, the one pan in
        /// the imported packs: a handle and a head, so the fitting path treats it as a short haft
        /// with the pan where a blade would be, and the stir is the stroke that moves it.
        /// </summary>
        public const string ToolPan = Prefix + "tool.pan";

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

        // The Meadow dressing: everything that stands on the ground to make a meadow of it, and
        // none of it simulated (the look pass, owner 2026-09-24: "scenery first, sim after";
        // design 38 §17). Families rather than single rows, so the variety is the catalogue's and
        // the layout only ever asks for "a bush". Kept apart from the tufts because they are
        // placed by a different rule — patches over the land, not a count per cell.
        const string DressPrefix = Prefix + "dress.";

        /// <summary>The big tall-grass mats, four to seven metres across: the stands in a meadow.</summary>
        public static readonly string[] DressTallGrass = { DressPrefix + "grass.tall.a", DressPrefix + "grass.tall.b" };

        /// <summary>Low leafy cover and grass bushes, filling between the stands.</summary>
        public static readonly string[] DressCover =
            { DressPrefix + "cover.a", DressPrefix + "cover.b", DressPrefix + "cover.c", DressPrefix + "cover.grassbush" };

        /// <summary>Flowers: the standing wildflowers and the flat cards that dot a field with colour.</summary>
        public static readonly string[] DressFlowers =
            { DressPrefix + "flower.wild.a", DressPrefix + "flower.wild.b", DressPrefix + "flower.wild.c" };

        /// <summary>A sunflower, for the odd cluster; tall, so used sparingly.</summary>
        public static readonly string[] DressSunflower = { DressPrefix + "flower.sun" };

        /// <summary>The round bushes, which in the reference are the biggest thing in the picture.</summary>
        public static readonly string[] DressBushes = { DressPrefix + "bush.a", DressPrefix + "bush.b", DressPrefix + "bush.c" };

        /// <summary>Rocks lying in the grass: single stones and small piles.</summary>
        public static readonly string[] DressRocks =
        {
            DressPrefix + "rock.a", DressPrefix + "rock.b", DressPrefix + "rock.c",
        };

        /// <summary>
        /// A tree's art variant. Variant 0 is the species' own row, which every other reader of a
        /// tree (the cursor, the sight test, the surround) keeps using; the rest are only chosen by
        /// the mesher, per cell, so a wood is not one tree repeated.
        /// </summary>
        public static string TreeVariant(string baseId, int variant) =>
            variant == 0 ? baseId : baseId + "." + variant;

        /// <summary>How many art variants a tree species may have, the base row included.</summary>
        public const int MaxTreeVariants = 6;

        /// <summary>Terrain is not authored per template, so its ids are derived from the def name.</summary>
        public static string Terrain(string terrainDefName) =>
            Prefix + "terrain." + terrainDefName.ToLowerInvariant();

        /// <summary>
        /// One of the several lumps a stone terrain is drawn with, suffixed by number.
        ///
        /// Variant 0 keeps the unsuffixed id, the way <see cref="Colonist"/> does, so the plain
        /// terrain id stays meaningful and a catalogue that knows nothing about variants still
        /// answers for the first one.
        /// </summary>
        public static string TerrainVariant(string terrainDefName, int variant) =>
            variant <= 0 ? Terrain(terrainDefName) : Terrain(terrainDefName) + "." + variant.ToString();

        /// <summary>
        /// One of the several coursed blocks an earth terrain is drawn with where a side of it
        /// shows — a terrace riser, the rim of the board, the wall of a cutting.
        ///
        /// <para>Unlike <see cref="TerrainVariant"/>, variant 0 does <em>not</em> keep the plain
        /// id: a face is a different mesh from the turf, so it needs an id of its own at every
        /// variant or the two would collide in the library's cache and whichever resolved first
        /// would be drawn for both.</para>
        /// </summary>
        public static string TerrainFace(string terrainDefName, int variant) =>
            Terrain(terrainDefName) + ".face" + variant.ToString();

        /// <summary>
        /// One of the stepped banks an earth terrace is climbed by. Named after the terrain at the
        /// <em>top</em> of the step, because that is the ground the bank is made of.
        /// </summary>
        public static string TerrainBank(string terrainDefName, int variant) =>
            Terrain(terrainDefName) + ".bank" + variant.ToString();
    }
}
