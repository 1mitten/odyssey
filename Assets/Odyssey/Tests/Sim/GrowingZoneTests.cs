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

        /// <summary>
        /// Folding two fields must leave every field recorded after them answering for itself.
        ///
        /// <para><b>Found by the PlayMode field benchmark, not by this tier</b> (U50): a field
        /// painted across water and trees fragments into several zones, one stroke folds a
        /// middle one, and the dissolve left every later zone wearing a stale slot — the next
        /// stroke on one of its cells read past the end of the list and the session threw
        /// forever after. The fast tier had only ever folded two zones, where the dissolved
        /// one is the last and nothing sits behind it; four is the smallest that catches it.</para>
        /// </summary>
        [Test]
        public void FoldingAFieldLeavesEveryFieldRecordedAfterItAnswering()
        {
            var (_, zones) = Meadow();

            // Four fields with gaps between them, so nothing has joined: slots 0, 1, 2, 3.
            zones.Designate(new CellRef(1, 3, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(3, 3, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(1, 6, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(6, 6, Layer), PlantHandle.Carrot);
            Assert.That(zones.ZoneCount, Is.EqualTo(4));

            // Touches the first two and folds them into one. The dissolve moves a later
            // zone into the vacated slot — which is exactly the move that used to leave the
            // zones after <em>that</em> pointing where no zone was.
            zones.Designate(new CellRef(2, 3, Layer), PlantHandle.Carrot);
            Assert.That(zones.ZoneCount, Is.EqualTo(3), "the fold left one field, not two");
            Assert.That(zones.Cells.Count, Is.EqualTo(5), "four fields and the folding stroke");

            Assert.That(zones.Designate(new CellRef(1, 6, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.AlreadyInThatState),
                "the field recorded after the fold still answers its own cell");
            Assert.That(zones.Designate(new CellRef(6, 6, Layer), PlantHandle.Carrot),
                Is.EqualTo(IntentRejection.AlreadyInThatState),
                "and so does the last one, two slots behind the dissolve");

            Assert.That(zones.Cancel(new CellRef(6, 6, Layer)), Is.EqualTo(IntentRejection.None),
                "the last field's rubber still finds it");
            Assert.That(zones.ZoneCount, Is.EqualTo(2));
        }

        [Test]
        public void ZoningAndUnzoningMarkTheChunkTheGroundChangedIn()
        {
            // The field's tilled ground is drawn from the zone channel, so the chunk must be
            // told to redraw exactly when a cell joins or leaves a zone - the same mark a crop's
            // appearance makes, or the rows would arrive a refresh late.
            var grid = Meadow().grid;
            var chunks = new Odyssey.Sim.World.ChunkGrid(Size);
            var zones = new GrowingZones(grid, ContentPack.Plants(), chunks);
            CellRef at = new CellRef(2, 2, Layer);

            Assert.That(zones.Designate(at, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            Assert.That(chunks.IsDirty(chunks.ChunkIndexOfCell(at)),
                "painting a field marks its chunk for the tilled ground");

            chunks.ClearDirty(chunks.ChunkIndexOfCell(at));
            Assert.That(zones.Cancel(at), Is.EqualTo(IntentRejection.None));
            Assert.That(chunks.IsDirty(chunks.ChunkIndexOfCell(at)),
                "taking a cell out of its field marks it again, for the grass coming back");
        }

        [Test]
        public void CancellingFromAFoldedFieldFindsItsOwnCell()
        {
            var (_, zones) = Meadow();

            // Two one-cell fields with a gap, then the stroke that folds them. The fold's
            // survivor is the eastern zone — the neighbour scan meets it first — so the
            // absorbed western cell, the smaller index, lands after it in the record unless
            // the merge restores the order itself.
            zones.Designate(new CellRef(3, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(5, 2, Layer), PlantHandle.Carrot);
            zones.Designate(new CellRef(4, 2, Layer), PlantHandle.Carrot);
            Assert.That(zones.ZoneCount, Is.EqualTo(1));
            Assert.That(zones.Cells.Count, Is.EqualTo(3));

            // The cancel a fold leaves behind must find its own entry. A record whose cells
            // are not ascending makes the binary search in Cancel answer negatively, and
            // RemoveAt(negative) throws inside the intent drain — the same poisoned queue
            // the Dissolve fix paid for, reached from the merge one layer above it. The
            // absorbed cell is the one that proves it: the fold leaves the record as
            // [bridge, eastern, western], and the western cell's search probes two larger
            // values before giving up.
            Assert.That(zones.Cancel(new CellRef(3, 2, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.Cancel(new CellRef(5, 2, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.Cancel(new CellRef(4, 2, Layer)), Is.EqualTo(IntentRejection.None));
            Assert.That(zones.ZoneCount, Is.EqualTo(0), "every cell of the folded field left it");
            Assert.That(zones.Cells, Is.Empty);
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
            zones.Advance(Size.Index(3, 2, Layer), 120_000); // past 85 per cent: a mature row

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
            Assert.That(crop.Stage, Is.EqualTo(3), "past the 85 per cent band, grown draws as the mature stage");
            Assert.That(crop.Growth, Is.GreaterThan(0), "and the quantised bar has moved off zero");
        }

        [Test]
        public void APublishedPlantCarriesItsHandleAndNotTheCropSlot()
        {
            // The view contract says Plant is a PlantHandle, which is nought-based; _cropAt is
            // one-based so nought can mean fallow. The first contributor published _cropAt
            // itself, the render mirror added one of its own, and the carrot landed on slot two
            // of a one-plant table - where CropModule's bounds guard quietly answered nought and
            // a ripe field drew nothing at all, while every render test fed the contract's own
            // bytes and passed. Only a test that reads the real contributor back can see the
            // difference between the two conventions, so this is that test.
            var (grid, zones) = Meadow();
            var cell = new CellRef(2, 2, Layer);
            Assert.That(zones.Designate(cell, PlantHandle.Carrot), Is.EqualTo(IntentRejection.None));
            zones.Sow(Size.Index(cell));

            SimWorld world = zones.Attach(new SimWorldBuilder().WithSeed(1u).WithSize(Size)).Build();
            world.Tick();

            System.ReadOnlySpan<PlantView> plants = world.Views.Current.Plants;
            Assert.That(plants.Length, Is.EqualTo(1), "the sown cell published no plant");
            Assert.That(plants[0].CellIndex, Is.EqualTo(Size.Index(cell)));
            Assert.That(plants[0].Plant, Is.EqualTo(PlantHandle.Carrot),
                "the published plant is the crop slot, one-based, not the handle the contract promises");
            Assert.That(plants[0].Stage, Is.EqualTo(0),
                "a freshly sown seed is in its seed day - specks and no plant");
        }

    }
}
