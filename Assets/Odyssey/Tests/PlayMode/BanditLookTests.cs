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
    /// The owner's report, 2026-09-24: <i>"The marauders look like colonists."</i> A bandit spawned
    /// from the debug menu under the real bootstrap, with a colonist beside it as the control, and
    /// the two figures' dress read back (<c>docs/design/42-bandits.md</c>): the bandit wears the
    /// welding helmet and exactly one vest, and no hair or beard shows under the helmet; the
    /// colonist wears no helmet and no vest.
    ///
    /// <para><b>Needs the licensed packs</b>, and asks about the gang's own rows rather than
    /// whether any art is here: the runner has animal art of the project's own.</para>
    /// </summary>
    public class BanditLookTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        [UnityTest]
        public IEnumerator ABanditWearsTheHelmetAndAVestAndAColonistDoesNot()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
#if UNITY_EDITOR
                if (boot.moduleCatalogue == null)
                    boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
                ModuleCatalogue? catalogue = boot.moduleCatalogue;
                if (catalogue == null) Assert.Ignore("no catalogue on this machine");
                ColonistCastPools pools = AppearanceBooks.PoolsFrom(catalogue);
                var rows = catalogue!.FindFamily(ModuleIds.ColonistBase);
                bool gangResolved = pools.BanditMale.Length > 0 && rows[pools.BanditMale[0]].prefab != null;
                if (!gangResolved || pools.Headgear.Length == 0)
                    Assert.Ignore("the bandit rows resolved no art — the licensed packs are absent");

                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                var colony = boot.Colony!;
                var world = boot.World!;
                int before = colony.Pawns.Pawns.Count;
                Pawn colonist = colony.Pawns.Pawns.All[0];
                var at = new CellRef(colony.Start.X, colony.Start.Z, colony.Start.Y);

                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Bandit));
                world.Tick();
                Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1), "the bandit did not arrive");
                Pawn bandit = colony.Pawns.Pawns.All[before];

                for (int i = 0; i < 10; i++) yield return null;

                PawnFigureDirector figures = boot.Figures!;
                Assert.That(figures.TryGetDress(bandit.Id.Value, out bool hair, out bool beard, out bool helmet, out int vests),
                    Is.True, "the bandit has no figure on screen");
                Assert.That(helmet, Is.True, "no welding helmet");
                Assert.That(vests, Is.EqualTo(1), "a bandit wears one vest");
                Assert.That(hair || beard, Is.False, "hair or a beard shows through the helmet");

                Assert.That(figures.TryGetDress(colonist.Id.Value, out _, out _, out bool colonistHelmet, out int colonistVests),
                    Is.True, "the control: the colonist has no figure");
                Assert.That(colonistHelmet, Is.False, "the control: a colonist wore the helmet");
                Assert.That(colonistVests, Is.Zero, "the control: a colonist wore a vest");

                // The same person underneath: the book deals the bandit a colonist's hair, kept.
                ColonistAppearanceBook book = figures.Appearances;
                uint seed = ColonistNames.RollSeedOf(world.Views.Current, bandit.Id);
                ColonistAppearance dressed = book.For(bandit.Id.Value, seed, PawnOutfit.Bandit);
                ColonistAppearance person = book.For(bandit.Id.Value, seed, PawnOutfit.Issued);
                Assert.That(dressed.HairPiece, Is.EqualTo(person.HairPiece), "the helmet deleted the hair");
                Assert.That(dressed.Skin, Is.EqualTo(person.Skin));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
