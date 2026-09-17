#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the world state hash covers (OQ-50).
    ///
    /// <para><b>Why this file exists.</b> Until 2026-09-17 the cell grid was not in the hash at
    /// all — <c>CellGrid</c> is neither an <c>ITickable</c> nor an <c>IWorldSystem</c>, which were
    /// the only two things <c>ComputeStateHash</c> walked, so terrain, floors, edifices and flags
    /// contributed nothing. Mining a cell moved no hash; felling a tree moved no hash; and
    /// <c>WorldRoundTripTests</c> proved a save round-tripped "exactly" by comparing numbers that
    /// could not see the world. It went unnoticed for months because every hash test compares one
    /// run against another run of the same build, and both sides were equally blind.</para>
    ///
    /// <para>The tests below are the ones that would have caught it. Each edits exactly one field
    /// of one cell and requires the hash to move — which is a stronger statement than "the hash
    /// changes when the world changes", because it names the field.</para>
    /// </summary>
    public class StateHashCoverageTests
    {
        static ColonyWorld Build() =>
            ColonyWorld.Build(new GridSize(24, 24, 8), seed: 11u, ScenarioDef.Bare());

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        /// <summary>A cell with ground in it, so an edit to it is an edit to something real.</summary>
        static int SomeSolidCell(CellGrid grid)
        {
            for (int i = 0; i < grid.Terrain.Length; i++)
                if (grid.Terrain[i] == NaturalContent.TerrainGrass) return i;
            Assert.Fail("the fixture board has no grass in it, so these tests are editing nothing");
            return -1;
        }

        [Test]
        public void MiningACellAwayMovesTheHash()
        {
            ColonyWorld colony = Build();
            ulong before = Hash(colony);

            int cell = SomeSolidCell(colony.Grid);
            colony.Grid.Terrain[cell] = CoreContent.TerrainAir;

            Assert.That(Hash(colony), Is.Not.EqualTo(before));
        }

        [Test]
        public void AFloorMovesTheHash()
        {
            ColonyWorld colony = Build();
            ulong before = Hash(colony);

            colony.Grid.Floor[SomeSolidCell(colony.Grid)] = 1;

            Assert.That(Hash(colony), Is.Not.EqualTo(before));
        }

        [Test]
        public void AnEdificeMovesTheHash()
        {
            ColonyWorld colony = Build();
            ulong before = Hash(colony);

            colony.Grid.Edifice[SomeSolidCell(colony.Grid)] = NaturalContent.EdificeTreeConifer;

            Assert.That(Hash(colony), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// The exact case that exposed the gap: deep water's impassable bit reaches the hash only
        /// through <see cref="CellFlags"/>, and flags were the field most obviously missing.
        /// </summary>
        [Test]
        public void ACellFlagMovesTheHash()
        {
            ColonyWorld colony = Build();
            ulong before = Hash(colony);

            colony.Grid.Flags[SomeSolidCell(colony.Grid)] |= CellFlags.ImpassableTerrain;

            Assert.That(Hash(colony), Is.Not.EqualTo(before));
        }

        /// <summary>
        /// Support is derived and is rebuilt on load, so it must stay <em>out</em> of the hash —
        /// otherwise a freshly loaded world would look divergent until the first solve. The other
        /// half of the contract, and the one a careless "hash everything" would break.
        /// </summary>
        [Test]
        public void SupportStaysOutOfTheHash()
        {
            ColonyWorld colony = Build();
            ulong before = Hash(colony);

            colony.Grid.Support[SomeSolidCell(colony.Grid)] ^= 0x7F;

            Assert.That(Hash(colony), Is.EqualTo(before),
                "support is derived state and hashing it would make every load look like a desync");
        }

        /// <summary>
        /// The control for all of the above: the edits must be what moves the hash, not the act of
        /// asking for it twice.
        /// </summary>
        [Test]
        public void AskingTwiceWithoutTouchingAnythingGivesTheSameHash()
        {
            ColonyWorld colony = Build();
            Assert.That(Hash(colony), Is.EqualTo(Hash(colony)));
        }
    }
}
