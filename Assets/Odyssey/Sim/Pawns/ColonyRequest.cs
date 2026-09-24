#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Everything it takes to build a colony world, in one object.
    ///
    /// <para><b>Why an object and not more parameters.</b> <see cref="ColonyWorld.Build"/> had
    /// seven, and the two the composition root still needed — a clock that does not start at
    /// midnight, and a snapshot contributor for the renderer — would have made nine. That is the
    /// point at which a call site stops being readable and people start writing their own build
    /// instead, which is exactly what the play scene had done: the same wiring, re-typed, quietly
    /// out of step on two counts and assembling no save components at all. A request is also what
    /// a new-game screen needs to hand around, so it is the shape this was going to take anyway.</para>
    ///
    /// <para>Pure simulation, like everything else here: no UnityEngine, so it runs in the fast
    /// test tier and in any container.</para>
    /// </summary>
    public sealed class ColonyRequest
    {
        public GridSize Size;

        /// <summary>The one number the whole world comes from, map and colonists alike.</summary>
        public uint Seed;

        /// <summary>
        /// What the player called this colony, for the save header (U36) — a load screen's list
        /// entry, nothing simulated reads it. Empty until the new-game screen exists to ask for
        /// one; <see cref="Saving.SaveRecipe.Unknown"/> is what an unnamed save reads back as.
        /// </summary>
        public string Name = string.Empty;

        /// <summary>
        /// Who and what is placed at the start, and which orders are already given. Headless runs
        /// and tests take <see cref="ScenarioDef.Bare"/>, the scene takes
        /// <see cref="ScenarioDef.Playtest"/>.
        /// </summary>
        public ScenarioDef Scenario = ScenarioDef.Bare();

        /// <summary>
        /// One roll seed per colonist the player chose on the select screen (U40), in the order
        /// they were shown, or null for a colony nobody chose.
        ///
        /// <para>It decides <i>who</i> the colonists are — their passions and starting skills, and
        /// so the name the interface gives them. It does not decide <i>how many</i>: that is
        /// <see cref="ScenarioDef.colonists"/>, and a caller wanting the three from the screen and
        /// nothing else sets both. Null is the ordinary case, and every colonist then rolls from
        /// this request's own <see cref="Seed"/>, which is what a colony rolled before this
        /// existed.</para>
        /// </summary>
        public IReadOnlyList<uint>? Colonists;

        /// <summary>
        /// Flat grass with no rock, ore or bare patches. False gives the full natural generator
        /// with hills, rock and ore.
        /// </summary>
        public bool Barren = true;

        /// <summary>
        /// With <see cref="Barren"/>: keep the woodland, which is what the scene loads. False is
        /// the bare board the tests baseline on, where anything that is not grass is a bug.
        /// </summary>
        public bool Wooded;

        /// <summary>
        /// Whether the world's own animals are seeded and kept (design 30). On by default and
        /// off only for a fixture that deals pawn ids by hand: a seeded hog would take the id
        /// the test meant for a colonist. Not a player setting.
        /// </summary>
        public bool Wildlife = true;

        /// <summary>
        /// Natural by owner instruction, which is what the scene loads. The ruined city is still
        /// generated and still tested (ADR 0008).
        /// </summary>
        public MapType Map = MapType.Natural;

        /// <summary>
        /// A natural map's surface relief in layers, or -1 for the def's own. A measurement seam
        /// for the board-depth arms (<c>docs/design/38-meadow-overhaul.md</c> §7); the game never
        /// sets it, and it is <b>not saved</b> — a colony built with it would reload on the def's
        /// own relief.
        /// </summary>
        public int SurfaceRelief = -1;

        /// <summary>
        /// The presentation chunk grid, when a renderer will be attached, so the support system
        /// and the jobs that edit the world can mark chunks dirty. Null for a headless run.
        /// </summary>
        public ChunkGrid? Chunks;

        /// <summary>
        /// Where to put the clock before the world runs. Zero — midnight — is inert and leaves
        /// every baked hash alone, which is why it is the default and why headless runs never set
        /// it. The scene passes noon.
        ///
        /// <para>A tick and not an hour, deliberately: the calendar that knows how long an hour is
        /// lives in the Hud assembly, and the simulation may not reference it. The caller converts.</para>
        /// </summary>
        public int StartTick;

        /// <summary>
        /// Builds the presentation's grid mirror, once, from the world that was just generated.
        ///
        /// <para>A factory rather than a ready-made contributor because the mirror is built *from*
        /// the generated grid and its edifices, so it cannot exist before generation has run —
        /// and generation is the first thing <see cref="ColonyWorld.Build"/> does. It is
        /// registered before the colony, so the geometry a frame shows is the one that frame's
        /// pawns and orders were computed against.</para>
        ///
        /// <para>Null for a headless run, which draws nothing. Whatever it returns may write the
        /// published frame and nothing else: a mirror that reached simulation state would put
        /// presentation into the save and the hash.</para>
        /// </summary>
        public Func<CellGrid, MapGenOutcome, ISnapshotContributor>? Mirror;
    }
}
