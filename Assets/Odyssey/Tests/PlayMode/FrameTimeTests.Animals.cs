#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    public partial class FrameTimeTests
    {
        /// <summary>
        /// What animals cost the frame (design 66 §9): the played meadow timed with <b>0, 48 and 80
        /// animals</b> — Standard's and Huge's wildlife ceilings — in one run, so the difference is
        /// the animals and not the machine. Design 29 planned an animal frame test and it was never
        /// built; this is it, and it is FA2's baseline: here the animals inside the 64-figure
        /// ceiling are live figures and the rest are not drawn at all, and FA2's far form is judged
        /// against these numbers.
        ///
        /// <para>The forest kinds when SIMPLE Forest Animals is installed; on a machine without it
        /// (the runner) the CC0 hog, rat and frog fill the count, so the arm still measures
        /// something (design 66 §7). The line says which. Asserts only that the animals were there.</para>
        /// </summary>
        [UnityTest, Category("Measurement")]
        public IEnumerator TheAnimalsAgainstTheFrame()
        {
            GameObject root = Build(Odyssey.Sim.Worldgen.Natural.MapType.Natural, barren: true,
                out OdysseyBootstrap boot);
            try
            {
                yield return null;
                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");

                var kinds = new List<int>();
                for (int kind = PawnKindIndex.VergeRabbit; kind <= PawnKindIndex.QuarryBear; kind++)
                    if (boot.Figures != null && boot.Figures.CanDrawKind(kind)) kinds.Add(kind);
                bool forest = kinds.Count > 0;
                if (!forest) kinds.AddRange(new[] { PawnKindIndex.MiddenHog, PawnKindIndex.DuctRat, PawnKindIndex.CulvertFrog });

                // The board arrives seeded with its own wildlife, so the control is made by taking
                // it away: 0 means none, and the colonists are the same in every arm.
                PawnRegistry registry = boot.Colony!.Pawns.Pawns;
                for (int i = registry.All.Count - 1; i >= 0; i--)
                    if (!registry.All[i].IsPerson) registry.Despawn(registry.All[i]);
                boot.World!.Tick();
                yield return null;
                int start = boot.World!.Views.Current.Pawns.Length;
                foreach (int animals in new[] { 0, 48, 80 })
                {
                    yield return SpawnAnimalsTo(boot, start + animals, kinds);
                    int pawns = boot.World!.Views.Current.Pawns.Length;
                    Assert.That(pawns - start, Is.EqualTo(animals), $"spawned {pawns - start} of {animals} animals");

                    float mean = 0f;
                    double[] split = new double[(int)OdysseyBootstrap.FrameSection.Count];
                    yield return TimeFrames($"animals/{animals}", boot, 30, x => mean = x, s => split = s);
                    Debug.Log($"[FrameTime] animals {animals} ({(forest ? "forest" : "CC0")} kinds), " +
                              $"{boot.Figures?.FigureCount ?? 0} figures: {mean:0.00} ms, " +
                              $"figures {split[(int)OdysseyBootstrap.FrameSection.Figures]:0.000} ms, " +
                              $"actors {split[(int)OdysseyBootstrap.FrameSection.Actors]:0.000} ms, " +
                              $"{boot.Renderer?.DrawCalls ?? 0} draw calls");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Spawn animals, the kinds dealt in turn, near the middle of the board — where the play
        /// camera looks — until the board has <paramref name="wanted"/> pawns.
        /// </summary>
        IEnumerator SpawnAnimalsTo(OdysseyBootstrap boot, int wanted, List<int> kinds)
        {
            ColonyWorld colony = boot.Colony!;
            GridSize size = colony.Grid.Size;
            PawnRegistry pawns = colony.Pawns.Pawns;
            int side = 12;
            int at = 0, attempts = 0;
            while (pawns.Count < wanted && attempts++ < 4000)
            {
                int x = size.SizeX / 2 - side + (at % (2 * side)) * 1;
                int z = size.SizeZ / 2 - side + (at / (2 * side)) * 1;
                at++;
                if (x < 1 || z < 1 || x >= size.SizeX - 1 || z >= size.SizeZ - 1) continue;
                int cell = colony.Grid.NearestWalkableInColumn(x, z, size.SizeY - 2);
                if (cell < 0) continue;
                pawns.Spawn(cell, kinds[pawns.Count % kinds.Count]);
            }
            boot.World!.Tick();
            yield return null;
        }
    }
}
