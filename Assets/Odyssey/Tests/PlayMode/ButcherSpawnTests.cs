#nullable enable
using System.Collections;
using System.IO;
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
                Assert.That(box.y, Is.InRange(3.3f, 4.0f), $"drawn {box.y:0.00} m tall: not the 3.64 m design 62 §8a measured");
                Assert.That(Mathf.Max(box.x, box.z), Is.LessThan(3.0f),
                    $"a box {box.x:0.00} x {box.z:0.00} m across: the bind pose's arm span or the pack's other giants, not the body");
                Assert.That(figures.WeaponOf(butcher.Id), Is.Not.Null, "the cleaver is not in its hand");
                TestContext.WriteLine($"butcher drawn {box.x:0.00} x {box.y:0.00} x {box.z:0.00} m");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Pictures for the handover, not a gate: the butcher against three drafted colonists, shot
        /// at the play camera when its first sweep is winding up (the red arc of three) and on the
        /// frame after its first fling. Written to <c>Logs/look/butcher-*.png</c>.
        /// </summary>
        [UnityTest, Explicit("photographs for the handover, not a test")]
        public IEnumerator TheButcherInAFightPhotographed()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig rig,
                out HudShell shell, buildOnPlay: false);
            RenderTexture? target = null;
            Camera cam = rig.Camera;
            try
            {
#if UNITY_EDITOR
                if (boot.moduleCatalogue == null)
                    boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
#endif
                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True);
                for (int i = 0; i < 20; i++) yield return null;
                var colony = boot.Colony!;
                var world = boot.World!;
                if (boot.Figures == null || !boot.Figures.CanDrawKind(PawnKindIndex.Butcher)) Assert.Ignore("no butcher art here");

                // Drafted, so they hold their ground in the clearing rather than running for the
                // trees, where the camera can see nothing through the crowns.
                foreach (Pawn p in colony.Pawns.Pawns.All)
                    if (p.IsColonist) world.Intents.Submit(new Intent(IntentKind.SetDrafted, default, p.Id.Value, 1));
                world.Tick();
                var at = new CellRef(colony.Start.X + 3, colony.Start.Z + 3, colony.Start.Y);
                world.Intents.Submit(new Intent(IntentKind.SpawnPawn, at, PawnKindIndex.Butcher));
                world.Tick();
                Pawn butcher = colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];

                target = new RenderTexture(1920, 1080, 24) { name = "butcher" };
                cam.targetTexture = target;
                Directory.CreateDirectory(Path.GetFullPath("Logs/look"));

                bool telegraphed = false, flung = false;
                for (int frame = 0; frame < 3_000 && !(telegraphed && flung); frame++)
                {
                    world.Tick();
                    rig.FocusOn(colony.Pawns.Size.FromIndex(butcher.Cell), 20f);
                    yield return null;
                    if (!telegraphed && butcher.HeldFacing != 0)
                    {
                        telegraphed = true;
                        yield return Photograph("butcher-windup", target);
                    }
                    if (!flung && FlungBy(world.Views.Current, butcher.Id))
                    {
                        flung = true;
                        for (int i = 0; i < 6; i++) yield return null;
                        yield return Photograph("butcher-fling", target);
                    }
                }
                TestContext.WriteLine($"telegraphed {telegraphed}, flung {flung}");
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (target != null) target.Release();
                Object.Destroy(root);
            }
        }

        /// <summary>A span cannot live in an iterator, so the frame's events are read here.</summary>
        static bool FlungBy(WorldSnapshot frame, PawnId butcher)
        {
            var events = frame.CombatEvents;
            for (int e = 0; e < events.Length; e++)
                if (events[e].Kind == CombatEventKind.KnockedBack && events[e].Attacker == butcher) return true;
            return false;
        }

        static IEnumerator Photograph(string name, RenderTexture target)
        {
            for (int i = 0; i < 2; i++) yield return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string path = Path.GetFullPath($"Logs/look/{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
            Debug.Log($"[Look] {name}: {path}");
        }
    }
}
