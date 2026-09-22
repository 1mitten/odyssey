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
    /// The owner's report, 2026-09-22: <i>"When I tried spawn a rat and pig... absolutely nothing
    /// happened."</i> This is the debug row's whole path under the real bootstrap — the intent
    /// with a kind, the tick that spawns, and the figure director drawing the result — asserted
    /// end to end, because the fast tier proves the simulation half and the EditMode figure test
    /// proves the drawing half and nothing before this proved the two joined.
    /// </summary>
    public class AnimalSpawnTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [UnityTest]
        public IEnumerator SpawningAHogAndARatFromTheDebugMenuPutsBothOnTheBoardAndDrawsThem()
        {
            // The HUD flow, as FigureCapTests: the catalogue must be on the bootstrap before
            // Start builds the session, and a bare rig world starts at once with none.
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

                Assert.That(boot.World, Is.Not.Null, "the bootstrap never built a world");
                Assert.That(boot.Colony, Is.Not.Null, "the bootstrap never built a colony");
                var colony = boot.Colony!;
                var world = boot.World!;
                int before = colony.Pawns.Pawns.Count;
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);

                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.MiddenHog));
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.DuctRat));
                world.Tick();

                Assert.That(world.Intents.Rejected, Is.Empty,
                    "a spawn was refused: " + (world.Intents.Rejected.Count > 0 ? world.Intents.Rejected[0].Reason.ToString() : ""));
                Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 2), "two animals joined the colony's pawns");
                Pawn hog = colony.Pawns.Pawns.All[before];
                Pawn rat = colony.Pawns.Pawns.All[before + 1];
                Assert.That(hog.Kind, Is.EqualTo(PawnKindIndex.MiddenHog));
                Assert.That(rat.Kind, Is.EqualTo(PawnKindIndex.DuctRat));
                Assert.That(colony.Grid.IsWalkable(hog.Cell), Is.True, "the hog stands somewhere it can stand");

                // Let the presentation catch up: a few frames for the figure director to see the
                // published pawns and lease figures for them.
                for (int i = 0; i < 10; i++) yield return null;

                PawnFigureDirector? figures = boot.Figures;
                Assert.That(figures, Is.Not.Null, "no figure director");
                // The animal rows are the project's own art and resolve everywhere, so this is
                // asserted rather than ignored where the packs are absent.
                Assert.That(figures!.Enabled, Is.True, "the director can draw: the animal rows alone make it able to");
                Assert.That(figures.HasFigureFor(hog.Id.Value), Is.True, "the hog has a figure on screen");
                Assert.That(figures.HasFigureFor(rat.Id.Value), Is.True, "and so does the rat");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
