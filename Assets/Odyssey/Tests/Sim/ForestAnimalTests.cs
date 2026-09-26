#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The forest animals as content (plan forest-animals, FA1): nine wild species appended after
    /// the butcher, every one on foot, and the deer's and the moose's two forms derived from the
    /// pawn rather than saved.
    /// </summary>
    public class ForestAnimalTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 16);

        /// <summary>The contract (plan §1): kind const, def name, species def name, forms.</summary>
        static readonly (int Kind, string Name, int Forms)[] Forest =
        {
            (PawnKindIndex.VergeRabbit, "VergeRabbit", 1),
            (PawnKindIndex.HedgerowDeer, "HedgerowDeer", 2),
            (PawnKindIndex.AshFox, "AshFox", 1),
            (PawnKindIndex.GutterRaccoon, "GutterRaccoon", 1),
            (PawnKindIndex.RubbleSkunk, "RubbleSkunk", 1),
            (PawnKindIndex.ThicketBoar, "ThicketBoar", 1),
            (PawnKindIndex.MireMoose, "MireMoose", 2),
            (PawnKindIndex.RidgeWolf, "RidgeWolf", 1),
            (PawnKindIndex.QuarryBear, "QuarryBear", 1),
        };

        // ---- the content ----------------------------------------------------------------

        [Test]
        public void TheForestKindsAreAppendedAfterTheButcherInTheContractsOrder()
        {
            Assert.That(PawnKindIndex.VergeRabbit, Is.EqualTo(PawnKindIndex.ButcherKing + 1), "appended, never inserted");
            PawnContent content = ContentPack.Pawns();
            Assert.That(content.Kinds, Has.Length.EqualTo(PawnKindIndex.Count));
            for (int i = 0; i < Forest.Length; i++)
            {
                Assert.That(Forest[i].Kind, Is.EqualTo(PawnKindIndex.VergeRabbit + i), $"{Forest[i].Name} out of order");
                Assert.That(content.Kinds[Forest[i].Kind].defName, Is.EqualTo("PawnKind_" + Forest[i].Name));
                Assert.That(content.SpeciesOf(Forest[i].Kind).defName, Is.EqualTo("Species_" + Forest[i].Name));
                // Species 8 onward, one per kind, so a species index never has to be looked up.
                Assert.That(content.KindSpecies[Forest[i].Kind], Is.EqualTo(8 + i), $"{Forest[i].Name}'s species index");
            }
        }

        [Test]
        public void EveryForestAnimalIsAWildAnimalOnFootThatCannotOpenADoor()
        {
            PawnContent content = ContentPack.Pawns();
            foreach (var f in Forest)
            {
                SpeciesDef species = content.SpeciesOf(f.Kind);
                Assert.That(species.person, Is.False, f.Name);
                Assert.That(content.Kinds[f.Kind].faction, Is.EqualTo(Faction.Wild), f.Name);
                // The raccoon too (plan §1): a closed door must keep a store raid out.
                Assert.That(content.KindMode[f.Kind], Is.EqualTo(TraverseMode.Animal), f.Name);
                Assert.That(species.formCount, Is.EqualTo(f.Forms), f.Name);
                Assert.That(species.bodySizePerMille, Is.GreaterThan(0), f.Name);
                Assert.That(species.meatYield, Is.GreaterThan(0), $"{f.Name}: the owner asked for the yields now (answer 15)");
                Assert.That(species.hideYield, Is.GreaterThan(0), f.Name);
                Assert.That(species.labelKey, Does.StartWith("ui.pawn."), f.Name);
            }
            Assert.That(TraverseModes.OpensDoors(TraverseMode.Animal), Is.False, "the premise");
            // The control: the rat still climbs, which is why it is not the raccoon's mode.
            Assert.That(content.KindMode[PawnKindIndex.DuctRat], Is.EqualTo(TraverseMode.Climber));
        }

        [Test]
        public void AnAnimalThatRunsCarriesNoGrudgeAndOneThatStandsHitsBack()
        {
            // FA1's revenge (the Defs' header): nought for the ones that run, the hog's rule or
            // more for the ones that stand, and nothing turns without an attack to turn with.
            PawnContent content = ContentPack.Pawns();
            foreach (var f in Forest)
            {
                SpeciesDef s = content.SpeciesOf(f.Kind);
                if (s.revengePerMille > 0)
                    Assert.That(s.naturalAttack, Is.Not.Null, $"{f.Name} turns on its attacker with nothing to hit it with");
            }
            Assert.That(content.SpeciesOf(PawnKindIndex.VergeRabbit).naturalAttack, Is.Null, "the rabbit carries no attack");
        }

        // ---- the form ---------------------------------------------------------------------

        [Test]
        public void FormIsAPureFunctionOfTheSeedAndTheId()
        {
            var seen = new int[2];
            for (int id = 1; id <= 400; id++)
            {
                int form = Pawn.FormOf(12345u, id, 2);
                Assert.That(form, Is.InRange(0, 1));
                Assert.That(Pawn.FormOf(12345u, id, 2), Is.EqualTo(form), "the same inputs, the same form");
                seen[form]++;
            }
            // Both forms, in roughly even numbers: a herd is not all does.
            Assert.That(seen[0], Is.InRange(150, 250), $"{seen[0]} does of 400");
            Assert.That(seen[1], Is.InRange(150, 250), $"{seen[1]} stags of 400");
            Assert.That(Pawn.FormOf(12345u, 7, 1), Is.EqualTo(0), "a one-form species is always form 0");

            int differs = 0;
            for (int id = 1; id <= 64; id++) if (Pawn.FormOf(1u, id, 2) != Pawn.FormOf(2u, id, 2)) differs++;
            Assert.That(differs, Is.GreaterThan(0), "the seed takes part, not only the id");
        }

        [Test]
        public void ADeerIsTheFormItsSeedSaysAndARabbitIsFormNought()
        {
            ColonyWorld colony = Board();
            Pawn deer = colony.Pawns.Pawns.Spawn(Ground(colony, 10, 10), PawnKindIndex.HedgerowDeer);
            Pawn rabbit = colony.Pawns.Pawns.Spawn(Ground(colony, 14, 10), PawnKindIndex.VergeRabbit);
            Assert.That(deer.Form, Is.EqualTo(Pawn.FormOf(deer.RollSeed, deer.Id.Value, 2)));
            Assert.That(rabbit.Form, Is.EqualTo(0));
        }

        [Test]
        public void TheFormIsPublishedForDeerAndMooseAndForNothingElse()
        {
            ColonyWorld colony = Board();
            var deer = new List<Pawn>();
            for (int i = 0; i < 6; i++) deer.Add(colony.Pawns.Pawns.Spawn(Ground(colony, 6 + 3 * i, 8), PawnKindIndex.HedgerowDeer));
            Pawn moose = colony.Pawns.Pawns.Spawn(Ground(colony, 8, 20), PawnKindIndex.MireMoose);
            Pawn wolf = colony.Pawns.Pawns.Spawn(Ground(colony, 14, 20), PawnKindIndex.RidgeWolf);
            colony.World.Tick();

            WorldSnapshot view = colony.World.Views.Current;
            foreach (Pawn d in deer)
            {
                Assert.That(view.TryGetPawnAspect(d.Id, AnimalAspects.Form, out int form), Is.True, $"deer {d.Id.Value}");
                Assert.That(form, Is.EqualTo(d.Form));
            }
            Assert.That(view.TryGetPawnAspect(moose.Id, AnimalAspects.Form, out int mooseForm) && mooseForm == moose.Form, Is.True);
            Assert.That(view.TryGetPawnAspect(wolf.Id, AnimalAspects.Form, out _), Is.False, "one form, nothing published");
            foreach (Pawn colonist in colony.Pawns.Pawns.All)
                if (colonist.IsPerson)
                    Assert.That(view.TryGetPawnAspect(colonist.Id, AnimalAspects.Form, out _), Is.False, "a person has no form");
        }

        [Test]
        public void NoAnimalIsEatingYet()
        {
            // FA1 has no eating job for an animal; FA2's feeding and FA3's grazing make it so.
            ColonyWorld colony = Board();
            Pawn boar = colony.Pawns.Pawns.Spawn(Ground(colony, 10, 10), PawnKindIndex.ThicketBoar);
            for (int t = 0; t < 600; t++)
            {
                colony.World.Tick();
                Assert.That(colony.World.Views.Current.TryGetPawnAspect(boar.Id, AnimalAspects.Grazing, out _), Is.False);
            }
        }

        [Test]
        public void TheAspectNamesAreTheOnesPresentationCopies()
        {
            // Presentation copies the strings (as it does sheltering's); this pins what it copies.
            Assert.That(AnimalAspects.FormName, Is.EqualTo("odyssey.pawn.form"));
            Assert.That(AnimalAspects.GrazingName, Is.EqualTo("odyssey.pawn.grazing"));
        }

        [Test]
        public void EveryForestAnimalSpawnsWandersAndSurvivesADay()
        {
            ColonyWorld colony = Board();
            var animals = new List<Pawn>();
            for (int i = 0; i < Forest.Length; i++)
                animals.Add(colony.Pawns.Pawns.Spawn(Ground(colony, 4 + 3 * i, 30), Forest[i].Kind));
            var start = new int[animals.Count];
            for (int i = 0; i < animals.Count; i++) start[i] = animals[i].Cell;
            for (int t = 0; t < 6_000; t++) colony.World.Tick();
            int moved = 0;
            for (int i = 0; i < animals.Count; i++)
            {
                Assert.That(colony.Grid.IsWalkable(animals[i].Cell), Is.True, Forest[i].Name);
                if (animals[i].Cell != start[i]) moved++;
            }
            Assert.That(moved, Is.GreaterThanOrEqualTo(animals.Count - 1), "they wander, as every animal does today");
        }

        // ---- fixtures ---------------------------------------------------------------------

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
        }

        static int Ground(ColonyWorld colony, int x, int z) => colony.Grid.NearestWalkableInColumn(x, z, Size.SizeY - 1);
    }
}
