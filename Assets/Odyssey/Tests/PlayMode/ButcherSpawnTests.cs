#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The butcher's debug row end to end under the real bootstrap (design 62 §10): the intent, the
    /// tick that spawns it, and the figure director drawing it as <b>itself</b> — its own row, its
    /// own height, its cleaver in the hand — and not as a rolled person in the gang's clothes.
    ///
    /// <para><b>Asks whether the butcher's row resolved</b> (<see cref="PawnFigureDirector.CanDrawKind"/>),
    /// never whether a catalogue exists: the catalogue is committed and its reference to the pack
    /// is null on a machine without <c>Assets/Synty</c> (docs/lessons.md). There it ignores itself.</para>
    /// </summary>
    public class ButcherSpawnTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [UnityTest]
        public IEnumerator SpawningTheButcherDrawsItAsItselfWithItsCleaver()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
#if UNITY_EDITOR
                if (boot.moduleCatalogue == null)
                    boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                var colony = boot.Colony!;
                var world = boot.World!;
                PawnFigureDirector? figures = boot.Figures;
                Assert.That(figures, Is.Not.Null, "no figure director");
                if (!figures!.CanDrawKind(PawnKindIndex.Butcher) || !figures.CanDrawColonists)
                    Assert.Ignore("the butcher's row resolved to no art: POLYGON Fantasy Rivals is not on this machine");

                int before = colony.Pawns.Pawns.Count;
                var at = new CellRef(colony.Start.X + 3, colony.Start.Z + 3, colony.Start.Y);
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Butcher));
                world.Tick();
                Assert.That(world.Intents.Rejected, Is.Empty, "the spawn was refused");
                Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));
                Pawn butcher = colony.Pawns.Pawns.All[before];
                Assert.That(butcher.Kind, Is.EqualTo(PawnKindIndex.Butcher));

                for (int i = 0; i < 12; i++) yield return null;
                Assert.That(figures.HasFigureFor(butcher.Id.Value), Is.True, "the butcher has no figure");
                Assert.That(figures.TryGetAnimalBox(butcher.Id, out _, out Vector3 box), Is.True,
                    "the butcher carries no box of its own, so its bar and cursor are a colonist's");
                Assert.That(box.y, Is.GreaterThan(3.0f), $"drawn {box.y:0.00} m tall: not the giant design 62 §8a measured");
                Assert.That(figures.WeaponOf(butcher.Id), Is.Not.Null, "the cleaver is not in its hand");
                TestContext.WriteLine($"butcher drawn {box.x:0.00} x {box.y:0.00} x {box.z:0.00} m");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
