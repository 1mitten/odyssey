#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The world a player actually runs can be written to a file.
    ///
    /// <para>It could not, and the reason was structural rather than missing: <c>WorldSave</c> has
    /// been complete, versioned and tested for weeks, but every caller was a test holding a
    /// <see cref="ColonyWorld"/>. The composition root composed its world by hand instead, so it
    /// held no <see cref="ColonyWorld.SaveComponents"/> list and had nothing to hand the save
    /// format. The scene was the one build in the project that could not save.</para>
    ///
    /// <para>The fast tier proves the <see cref="ColonyRequest"/> the scene uses round-trips. This
    /// is the other half, and the half that can only be asked with Unity running: that the live
    /// bootstrap, having started for real, is holding that world rather than a private copy of the
    /// wiring.</para>
    /// </summary>
    public class SceneSaveTests
    {
        [UnityTest]
        public IEnumerator TheLiveSceneHoldsAWorldItCanSave()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out _);
            yield return RigWorld.WarmUp();

            Assert.That(boot.Colony, Is.Not.Null, "the bootstrap built no ColonyWorld");
            ColonyWorld colony = boot.Colony!;

            Assert.That(colony.SaveComponents, Is.Not.Empty, "the scene's world has nothing to save");
            Assert.That(colony.World, Is.SameAs(boot.World), "the bootstrap is running some other world");

            byte[] saved = colony.Save();
            Assert.That(saved.Length, Is.GreaterThan(0));

            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator TheSceneWorldSurvivesASaveAndLoad()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out _);
            yield return RigWorld.WarmUp();

            ColonyWorld colony = boot.Colony!;
            byte[] saved = colony.Save();

            // The state hash covers the cell grid as of 2026-09-17, so this really does mean "the
            // whole world" -- the terrain, the floors and the edifices as well as the pawns.
            StateHash before = colony.World.ComputeStateHash();

            // Built from the request the live world was built from, which is the contract the save
            // format asks for: construct from Defs and a seed exactly as a new game would, then
            // load state over the top. Chunks are deliberately left off — a chunk grid is how
            // presentation is told what to redraw, and if dropping it moved the simulation then
            // presentation would be in the save.
            ColonyRequest again = colony.Request;
            ColonyWorld reloaded = ColonyWorld.Build(new ColonyRequest
            {
                Size = again.Size,
                Seed = again.Seed,
                Scenario = again.Scenario,
                Barren = again.Barren,
                Wooded = again.Wooded,
                Map = again.Map,
                StartTick = again.StartTick,
            });

            SaveHeader header = reloaded.Load(saved);

            Assert.That(header.Seed, Is.EqualTo(again.Seed));
            Assert.That(header.Size, Is.EqualTo(again.Size));
            Assert.That(reloaded.World.ComputeStateHash(), Is.EqualTo(before),
                "the scene's own world did not come back the same");

            Object.Destroy(root);
        }

    }
}
