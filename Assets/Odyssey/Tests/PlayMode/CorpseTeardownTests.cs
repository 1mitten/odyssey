#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A session put down while a body is still falling (review of combat C2, 2026-09-23). The
    /// bootstrap disposed the figure director before the corpse director, and the corpse director
    /// hands a falling body's lent figure back as it goes — into a list the figures had just
    /// cleared. So a death on screen, a pause (which holds the fall where it is) and Load, New game
    /// or Leave to menu threw out of <c>TeardownSession</c>, skipped the rest of it, and on a load
    /// left a half-torn session with no new one built.
    ///
    /// <para>A hog, because its row is the project's own art and resolves on the runner too, so
    /// this is asserted there rather than ignored.</para>
    /// </summary>
    public class CorpseTeardownTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [UnityTest]
        public IEnumerator TearingDownWhileABodyFallsThrowsNothingAndPutsTheSessionDown()
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
                Assert.That(colony, Is.Not.Null, "the bootstrap never built a colony");
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);
                world.Intents.Submit(new Intent(IntentKind.SetGameSpeed, at, 1));
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.MiddenHog));
                world.Tick();
                Pawn hog = colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];
                Assume.That(hog.Kind, Is.EqualTo(PawnKindIndex.MiddenHog), "the hog was spawned");
                Pawn colonist = colony.Pawns.Pawns.All[0];

                // One blow past the death line: dead now, a corpse at the end of the next tick.
                colony.Pawns.Combat!.ApplySwing(colonist, hog, new Armament(colony.Pawns.Content.Combat.fists),
                    new SwingOutcome(CombatEventKind.Hit, hog.HpMaxMilli * 3), world.CurrentTick);
                world.Tick();
                Assume.That(colony.Pawns.Corpses.Count, Is.EqualTo(1), "the hog died");

                for (int i = 0; i < 10 && (boot.Corpses == null || boot.Corpses.Falling == 0); i++) yield return null;
                Assert.That(boot.Corpses, Is.Not.Null, "no corpse director");
                Assert.That(boot.Corpses!.Falling, Is.EqualTo(1), "the control: the death on screen is seen falling");

                Assert.DoesNotThrow(() => boot.TeardownSession(), "the teardown threw with a body falling");
                Assert.That(boot.HasSession, Is.False, "the session was left half torn down");
                Assert.That(boot.Corpses, Is.Null);
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
