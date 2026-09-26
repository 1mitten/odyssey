#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// Past <see cref="Odyssey.Presentation.World.PawnFigureDirector.MaxFigures"/> a colonist is
    /// drawn as a baked instanced mesh, which glides rather than walks. That is the design and it
    /// is not in question; <b>which</b> colonists it happens to is
    /// (<c>docs/design/20-avatars.md</c> §11).
    ///
    /// <para>Nothing sorted, so the cap took the first N in snapshot order — which is pawn id.
    /// The colonists that lost their animation were therefore a fixed set chosen when they were
    /// created, and no amount of moving the camera changed it: measured with eighty-five
    /// colonists spread across the board, the nearest frozen one was 134 m from the camera while
    /// an animated one stood at 179 m.</para>
    /// </summary>
    public class FigureCapTests
    {
        const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";

        /// <summary>
        /// Small on purpose. The rule is about which side of the cap a colonist falls, not about
        /// how big the cap is, and eight live Synty characters prove it as well as sixty-four do
        /// in a fraction of the time.
        /// </summary>
        const int Cap = 8;

        const int Colonists = 20;

        /// <summary>
        /// How far along the line each successive colonist is put, modulo the count. Coprime with
        /// <see cref="Colonists"/>, so every place on the line is used exactly once and the order
        /// they are created in has nothing to do with how far away they end up.
        /// </summary>
        const int Stride = 7;

        static void GiveItACatalogue(OdysseyBootstrap boot)
        {
#if UNITY_EDITOR
            if (boot.moduleCatalogue == null)
                boot.moduleCatalogue =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
        }

        [UnityTest]
        public IEnumerator TheColonistsLeftUnanimatedAreTheFarthestAway()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                GiveItACatalogue(boot);
                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 57 §9)
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True, "Start built no world");
                for (int i = 0; i < 20; i++) yield return null;

                Assert.That(boot.World, Is.Not.Null, "no world");
                Assert.That(boot.Figures, Is.Not.Null, "no figure director");
                // **Whether the art resolved, not whether there is a catalogue.** The catalogue
                // is committed and its prefab references point into the gitignored Assets/Synty,
                // so on the self-hosted runner it loads perfectly with every reference null — and
                // a catalogue null-check says "carry on" on exactly the machine that can draw
                // nobody. See PortraitLightingTests.NoPacks, where that cost a red build.
                // And the colonist rows in particular: the animal rows are the project's own art
                // and resolve on the runner, so `Enabled` is true there while no colonist can be
                // drawn, which is exactly the frozen-figure count this test then read as zero.
                if (!boot.Figures!.CanDrawColonists)
                    Assert.Ignore("the colonist rows resolved to no art (no Assets/Synty), " +
                                  "so nothing is drawn as a figure");

                boot.Figures.MaxFigures = Cap;

                // Along a line away from the camera, and **scrambled**, so that distance and pawn
                // id are two different orderings. Spawn them in order of distance and the old
                // behaviour — the first N in snapshot order, which is the first N by id — would
                // pass this test without sorting anything.
                CellRef start = boot.World!.Views.Current.Pawns[0].Cell;
                GridSize size = boot.World.Views.Current.Size;
                for (int i = 0; i < Colonists; i++)
                {
                    int along = i * Stride % Colonists + 1;
                    var at = new CellRef(
                        Mathf.Clamp(start.X + along, 1, size.SizeX - 2), start.Z, start.Y);
                    boot.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, at));
                    yield return null;
                }

                for (int i = 0; i < 20; i++) yield return null;

                // The camera's own position, because that is what the bootstrap feeds the
                // director every frame — writing one here would be overwritten on the next.
                Assert.That(boot.Figures.ViewerPosition.HasValue, Is.True,
                    "the bootstrap never told the director where the camera is");
                Vector3 eye = boot.Figures.ViewerPosition!.Value;

                WorldSnapshot frame = boot.World.Views.Current;
                Assert.That(frame.Pawns.Length, Is.GreaterThan(Cap),
                    "fewer colonists than the cap, so the cap was never reached");
                Assert.That(boot.Figures.Drawn.Count, Is.EqualTo(Cap), "the cap was not honoured");

                float farthestAnimated = 0f, nearestBaked = float.MaxValue;
                int animated = 0;
                for (int n = 0; n < frame.Pawns.Length; n++)
                {
                    PawnView pawn = frame.Pawns[n];
                    float range = Vector3.Distance(CellMetrics.FloorCentre(pawn.Cell), eye);
                    if (boot.Figures.HasFigureFor(pawn.Id.Value))
                    {
                        animated++;
                        if (range > farthestAnimated) farthestAnimated = range;
                    }
                    else if (range < nearestBaked) nearestBaked = range;
                }

                Assert.That(animated, Is.EqualTo(Cap));

                // Not `nearestBaked >= farthestAnimated`: a colonist who already has a figure
                // counts as a quarter nearer than they are, so that panning across a crowd does
                // not swap figures in and out every few frames. The margin is that discount.
                Assert.That(nearestBaked, Is.GreaterThan(farthestAnimated * Mathf.Sqrt(0.75f)),
                    $"a near colonist was left unanimated while a far one walked: " +
                    $"nearest baked {nearestBaked:F1} m, farthest animated {farthestAnimated:F1} m");
            }
            finally { Object.Destroy(root); }
        }
    }
}
