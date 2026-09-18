#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// What the sight fade refuses to touch, and why the refusal is not a hole in the feature.
    ///
    /// <para>The fade ghosts whatever stands between the eye and a selected colonist. Owner,
    /// 2026-09-18: *"it shouldn't do it on the artificial façade terrain on the height edges and in
    /// water/around water"*. Both of those are <b>surfaces</b> rather than objects — a pond is a body
    /// of faces and a bank is a sheet leaning on a terrace step that no cell in the simulation even
    /// contains — so half of one is not a view through it, it is a hole in the landscape. Neither
    /// can hide anybody either: a colonist in the water is standing in it, and one at the top of a
    /// step is above the bank rather than behind it.</para>
    ///
    /// <para>Each test below carries its own <b>control</b>, because an exemption is the easiest
    /// thing in the world to assert vacuously — a beam that crosses nothing proves nothing. The
    /// water test puts rock in the cell the water will occupy and shows that the same beam ghosts
    /// it; the bank test draws the same board with banks switched off and shows the count does not
    /// move.</para>
    /// </summary>
    public class SightFadeExemptionTests
    {
        [SetUp]
        public void Reset() => BankLayout.Reset();

        [TearDown]
        public void Restore() => BankLayout.Reset();

        static ChunkRenderer RendererFor(RenderTestWorld world)
        {
            var renderer = new ChunkRenderer(world.Model) { SubmitToGpu = false, ScatterDensity = 0 };
            renderer.Skirt.Enabled = false;
            return renderer;
        }

        // --------------------------------------------------------------- the rule itself

        [Test]
        public void FoliageWaterAndBanksAreExemptAndNothingElseIs()
        {
            Assert.That(ChunkRenderer.NeverFades(TintCode.Foliage(0)), Is.True, "grass is ankle-high");
            Assert.That(ChunkRenderer.NeverFades(TintCode.Water(NaturalContent.TerrainShallowWater)), Is.True);
            Assert.That(ChunkRenderer.NeverFades(TintCode.Bank(NaturalContent.TerrainGrass)), Is.True);

            // The half that matters as much: an exemption written too wide turns the feature off.
            Assert.That(ChunkRenderer.NeverFades(TintCode.Terrain(NaturalContent.TerrainGrass)), Is.False,
                "ordinary ground must still fade, or nobody behind a hill is ever seen");
            Assert.That(ChunkRenderer.NeverFades(TintCode.Stuff(CoreContentStuff)), Is.False, "a wall must fade");
            Assert.That(ChunkRenderer.NeverFades(TintCode.Tree(TreeSpecies.Broadleaf)), Is.False, "a tree must fade");

            // A bank is still terrain and still tinted as terrain — the marker says what it is, not
            // what colour it is, and a bank that stopped reading as terrain would draw grey.
            Assert.That(TintCode.IsTerrain(TintCode.Bank(NaturalContent.TerrainGrass)), Is.True);
            Assert.That(TintCode.Value(TintCode.Bank(NaturalContent.TerrainGrass)),
                Is.EqualTo(TintCode.Value(TintCode.Terrain(NaturalContent.TerrainGrass))));
        }

        /// <summary>Any construction stuff; which one is beside the point.</summary>
        const int CoreContentStuff = 3;

        // --------------------------------------------------------------- water

        /// <summary>Bare grass with one cell of layer 1 occupied, and a colonist east of it.</summary>
        static RenderTestWorld Board(bool water)
        {
            GroundRelief.Reset();
            var world = new RenderTestWorld(8, 8, 4);
            for (int z = 0; z < 8; z++)
            for (int x = 0; x < 8; x++)
                world.Solid(x, z, 0, NaturalContent.TerrainGrass);

            if (water) world.Surface(2, 4, 1, NaturalContent.TerrainShallowWater);
            else world.Solid(2, 4, 1);

            return world.Publish();
        }

        static Vector3 ChestAt(int x, int z) => CellMetrics.FloorCentre(x, z, 1) + Vector3.up * 1.35f;

        static Vector3 EyeFor(int z) => new Vector3(-20f, 8f, CellMetrics.FloorCentre(2, z, 1).z);

        static int FadedAcross(RenderTestWorld world)
        {
            ChunkRenderer renderer = RendererFor(world);
            var sight = new SightLines();
            sight.Add(EyeFor(4), ChestAt(6, 4));
            renderer.Sight = sight;
            renderer.Render(1, new SliceSettings());

            Assert.That(renderer.ChunksSightTested, Is.GreaterThan(0), "no chunk was even tested");
            return renderer.InstancesFaded;
        }

        /// <summary>
        /// The same beam through the same cell: rock in it ghosts, water in it does not.
        ///
        /// <para>The rock half is the control, and it is what stops this passing on a beam that
        /// misses. It is the same assertion <c>SightFadeRenderTests</c> makes, made here again over
        /// the one cell this test then fills with water instead.</para>
        /// </summary>
        [Test]
        public void WaterInTheBeamIsNotGhostedThoughRockInTheSameCellIs()
        {
            Assert.That(FadedAcross(Board(water: false)), Is.GreaterThan(0),
                "the control failed: this beam ghosts nothing even when a rock is standing in it");

            Assert.That(FadedAcross(Board(water: true)), Is.Zero,
                "the beam opened a hole in the water");
        }

        // --------------------------------------------------------------- banks

        /// <summary>
        /// A six-deep, one-layer terrace step, which grows exactly one bank per cell along it
        /// (<c>BankMeshTests.EveryOneLayerStepGrowsExactlyOneBank</c>).
        /// </summary>
        static int FadedOverTheStep(bool banks, out int drawn)
        {
            GroundRelief.Reset();
            BankLayout.Reset();
            BankLayout.Enabled = banks;

            RenderTestWorld world = RenderTestWorld.Terrace(rise: 1, stepTerrain: NaturalContent.TerrainGrass);
            ChunkRenderer renderer = RendererFor(world);

            // Downhill over the step, which is the case the feature exists for: the colonist is on
            // the low ground and the camera is out beyond the high side, so the brow of the terrace
            // and the bank leaning against it are both squarely in the beam. Worked out rather than
            // eyeballed — at the bank's own cell the beam is 2.0 m above the layer floor and at the
            // last raised cell it is 2.4 m, both well inside a 3 m layer — and the two controls
            // below are what prove it, since a beam that missed would satisfy the exemption for
            // nothing.
            var sight = new SightLines();
            sight.Add(new Vector3(-20f, 12f, CellMetrics.FloorCentre(5, 3, 2).z),
                CellMetrics.FloorCentre(5, 3, 2) + Vector3.up * 1.35f);
            renderer.Sight = sight;
            renderer.Render(2, new SliceSettings());

            Assert.That(renderer.ChunksSightTested, Is.GreaterThan(0), "no chunk was even tested");
            drawn = renderer.InstancesDrawn;
            return renderer.InstancesFaded;
        }

        [Test]
        public void ABankOnTheStepIsNotGhosted()
        {
            int withBanks = FadedOverTheStep(banks: true, out int drawnWith);
            int without = FadedOverTheStep(banks: false, out int drawnWithout);

            // The control: banks really are being drawn on this board, so "nothing extra faded" is
            // a statement about the exemption rather than about an empty step.
            Assert.That(drawnWith, Is.GreaterThan(drawnWithout),
                "no bank was drawn at all, so this test is asserting nothing");

            Assert.That(withBanks, Is.EqualTo(without),
                "the banks added to what the beam ghosted, so the hillside has a gap cut in it");

            // And the beam is live: the ground itself still fades, which is the feature working.
            Assert.That(without, Is.GreaterThan(0),
                "this beam ghosts nothing even without banks, so it crosses nothing");
        }
    }
}
