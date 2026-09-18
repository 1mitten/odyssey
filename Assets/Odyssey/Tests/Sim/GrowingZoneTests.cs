#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Growing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The player's growing zones: where a seed may go, how a brush stroke becomes one field,
    /// and how a field stops being one.
    ///
    /// <para>The fixture is a hand-built meadow rather than a generated board because the siting
    /// gate is about <em>shapes</em> of refusal — water, gravel, a slab, a roof, a tree — and a
    /// fixture that places each of them deliberately names what refused. The ground layer is
    /// solid grass exactly as the surface pass writes it, which makes the standing cell the air
    /// above it: the one fact of this unit's geometry that everything else here depends on.</para>
    /// </summary>
    public class GrowingZoneTests
    {
        const int Ground = 0;
        const int Layer = 1;
        static readonly GridSize Size = new GridSize(8, 8, 3);

        static (CellGrid grid, GrowingZones zones) Meadow()
        {
            var grid = new CellGrid(Size);
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int ground = Size.Index(x, z, Ground);
                grid.Terrain[ground] = NaturalContent.TerrainGrass;
                grid.Flags[ground] |= CellFlags.SolidTerrain;
            }

            // A wadeable patch, ground the seed refuses, a slab, a roof over one cell and a
            // tree: each is one reason the gate says no, and the tests below name them.
            grid.Terrain[Size.Index(1, 1, Layer)] = NaturalContent.TerrainShallowWater;

            int gravel = Size.Index(3, 1, Ground);
            grid.Terrain[gravel] = CoreContent.TerrainGravel;

            grid.Floor[Size.Index(4, 1, Layer)] = CoreContent.SlabStructural;
            grid.Floor[Size.Index(5, 1, Layer + 1)] = CoreContent.SlabRoof;

            int tree = Size.Index(6, 1, Layer);
            grid.Edifice[tree] = 0; // slot zero of an empty list is all a refusal needs to see

            var zones = new GrowingZones(grid, ContentPack.Plants());
            return (grid, zones);
        }

        [Test]
        public void AZoneGoesOnOpenGroundAndIsRefusedEverywhereElse()
        {
            var (_, zones) = Meadow();

            Assert.That(zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.None));
            Assert.That(zones.Designate(new CellRef(1, 1, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.NotPermitted), "a paddy is not a carrot bed");
            Assert.That(zones.Designate(new CellRef(3, 1, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.NotPermitted), "gravel grows nothing at fertility 55");
            Assert.That(zones.Designate(new CellRef(4, 1, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.NotPermitted), "nothing grows through a slab");
            Assert.That(zones.Designate(new CellRef(5, 1, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.NotPermitted), "the light hook refuses a roof");
            Assert.That(zones.Designate(new CellRef(6, 1, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.NotPermitted), "the tree is felled first");
            Assert.That(zones.Designate(new CellRef(2, 2, Ground), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.NotPermitted), "the turf itself is not standable");
            Assert.That(zones.Designate(new CellRef(9, 9, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.OutOfBounds));
        }

        [Test]
        public void PaintingTwiceAndPaintingADifferentCropAreBothRefused()
        {
            var (_, zones) = Meadow();

            Assert.That(zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.None));
            Assert.That(zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.AlreadyInThatState));

            // A second species exists only to prove the refusal is about the plant, not the
            // brush: the array is the table, and the table is what a zone names.
            var plants = new PlantDef[]
            {
                ContentPack.Plants()[0],
                new PlantDef { defName = "Plant_TestTurnip", label = "test turnip", yields = "Item_Carrots" },
            };
            var two = new GrowingZones(Meadow().grid, plants);
            Assert.That(two.Designate(new CellRef(2, 2, Layer), 0), Is.EqualTo(IntentRejection.None));
            Assert.That(two.Designate(new CellRef(2, 2, Layer), 1),
                Is.EqualTo(IntentRejection.NotPermitted), "repainting means cancelling first");
        }

        [Test]
        public void TouchingCellsJoinOneFieldAndDiagonalsCount()
        {
            var (_, zones) = Meadow();

            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(3, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(3, 3, Layer), PlantHandle.Carrot);

            Assert.That(zones.ZoneCount, Is.EqualTo(1),
                "an eight-neighbour stroke is one field, not three records and three scans");
        }

        [Test]
        public void ACornerStrokeFoldsTwoFieldsIntoOne()
        {
            var (_, zones) = Meadow();

            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(4, 2, Layer), PlantHandle.Carrot);
            Assert.That(zones.ZoneCount, Is.EqualTo(2));

            // Touches both existing fields only diagonally. If diagonal contact did not count,
            // this cell would found a third record between them and the field would scan as two.
            zones.Designate(new CellRef(3, 3, Layer), PlantHandle.Carrot);
            Assert.That(zones.ZoneCount, Is.EqualTo(1));
            Assert.That(zones.Cells.Count, Is.EqualTo(3));
        }

        [Test]
        public void CancellingTheLastCellDissolvesTheZone()
        {
            var (_, zones) = Meadow();

            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(3, 2, Layer), PlantHandle.Carrot);

            Assert.That(zones.Cancel(new CellRef(2, 2, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.ZoneCount, Is.EqualTo(1), "the field survives while a cell of it stands");
            Assert.That(zones.Cancel(new CellRef(3, 2, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.ZoneCount, Is.EqualTo(0), "an emptied record dissolves");
            Assert.That(zones.Cells, Is.Empty);

            Assert.That(zones.Cancel(new CellRef(3, 2, Layer)),
                Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        [Test]
        public void CancellingACellUprootsItsCrop()
        {
            var (_, zones) = Meadow();

            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            int index = Size.Index(2, 2, Layer);
            zones.Sow(index);
            zones.Advance(index, 1_000);

            Assert.That(zones.Cancel(new CellRef(2, 2, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.IsPlanted(index), Is.False,
                "a crop exists only inside its zone: the rubber takes both");
            Assert.That(zones.Planted, Is.Empty);
        }

        [Test]
        public void SowingOffAZoneIsAnErrorNotASilence()
        {
            var (_, zones) = Meadow();
            int index = Size.Index(2, 2, Layer);
            Assert.Throws<System.ArgumentException>(() => zones.Sow(index));
        }

        [Test]
        public void ARoofRaisedOverAZoneDoesNotUnzoneIt()
        {
            var (grid, zones) = Meadow();

            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            grid.Floor[Size.Index(2, 2, Layer + 1)] = CoreContent.SlabRoof;

            Assert.That(zones.ZonePlantAt(Size.Index(2, 2, Layer)), Is.EqualTo(PlantHandle.Carrot),
                "the zone is the player's order, not a opinion about daylight");
            Assert.That(zones.SiteAllows(Size.Index(2, 2, Layer), zones.Plant(PlantHandle.Carrot)),
                Is.False, "but the next sowing is asked again, and the roof now answers");
        }

        [Test]
        public void TheZoneIsStateTheHashCanSee()
        {
            var (grid, zones) = Meadow();
            var empty = StateHash.New();
            zones.ContributeTo(ref empty);

            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            zones.Sow(Size.Index(2, 2, Layer));
            zones.Advance(Size.Index(2, 2, Layer), 1_000);
            var planted = StateHash.New();
            zones.ContributeTo(ref planted);

            Assert.That(planted, Is.Not.EqualTo(empty),
                "a field that moved nothing would be a field a save/load could lose silently");

            zones.Cancel(new CellRef(2, 2, Layer));
            var cleared = StateHash.New();
            zones.ContributeTo(ref cleared);
            Assert.That(cleared, Is.EqualTo(empty),
                "painting and then erasing everything is the empty world again, hash included");
        }

        [Test]
        public void TheIntentCarriesThePlantOneBased()
        {
            var (_, zones) = Meadow();

            Assert.That(zones.HandleDesignate(new Intent(IntentKind.DesignateZone, new CellRef(2, 2, Layer), a: 0)),
                Is.EqualTo(IntentRejection.NotPermitted), "nought means not set, as for a designation kind");
            Assert.That(zones.HandleDesignate(new Intent(IntentKind.DesignateZone, new CellRef(2, 2, Layer), a: 99)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(zones.HandleDesignate(new Intent(IntentKind.DesignateZone, new CellRef(2, 2, Layer), a: PlantHandle.Carrot + 1)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(zones.HandleCancel(new Intent(IntentKind.CancelZone, new CellRef(2, 2, Layer))),
                Is.EqualTo(IntentRejection.None));
        }

        [Test]
        public void TheSnapshotCarriesZonesAndCropsApart()
        {
            var (grid, zones) = Meadow();
            zones.Designate(new CellRef(2, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(3, 2, Layer), PlantHandle.Carrot);
            zones.Sow(Size.Index(3, 2, Layer));
            zones.Advance(Size.Index(3, 2, Layer), 90_000); // past two thirds: a mature row

            var world = new SimWorldBuilder()
                .WithSize(Size)
                .AddSnapshotContributor(zones)
                .Build();
            world.RepublishViews();

            var view = world.Views.Current;
            Assert.That(view.ZoneCount, Is.EqualTo(2), "both cells are zone, crop or not");
            Assert.That(view.PlantCount, Is.EqualTo(1), "the crop is one row the tint does not double");

            PlantView crop = view.Plants[0];
            Assert.That(crop.CellIndex, Is.EqualTo(Size.Index(3, 2, Layer)));
            Assert.That(crop.Stage, Is.EqualTo(3), "two thirds grown draws as the mature stage");
            Assert.That(crop.Growth, Is.GreaterThan(0), "and the quantised bar has moved off zero");
        }
    }
}
