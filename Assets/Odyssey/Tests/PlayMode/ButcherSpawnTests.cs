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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
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
                Assert.That(box.y, Is.InRange(4.0f, 4.8f), $"drawn {box.y:0.00} m tall: not the 4.37 m design 62 §8a measured");
                Assert.That(Mathf.Max(box.x, box.z), Is.LessThan(3.0f),
                    $"a box {box.x:0.00} x {box.z:0.00} m across: the bind pose's arm span or the pack's other giants, not the body");
                Transform? cleaver = figures.WeaponOf(butcher.Id);
                Assert.That(cleaver, Is.Not.Null, "the butcher has no cleaver");
                // In the fist, not parked at the hip where every new prop is fitted first (owner,
                // 2026-09-26: "he wasn't using a weapon to strike people").
                Assert.That(cleaver!.parent != null && cleaver.parent.name.Contains("Hand"), Is.True,
                    $"the cleaver hangs from {(cleaver.parent != null ? cleaver.parent.name : "nothing")}, not the hand");
                TestContext.WriteLine($"butcher drawn {box.x:0.00} x {box.y:0.00} x {box.z:0.00} m");
                // Its card shows the butcher, not a bandit (owner, 2026-09-26).
                Assert.That(boot.Portraits.For(world.Views.Current, butcher.Id), Is.SameAs(boot.Portraits.ForKind(PawnKindIndex.Butcher)),
                    "the card's portrait is not the butcher's own");
                Assert.That(boot.Portraits.ForKind(PawnKindIndex.Butcher), Is.Not.Null, "no portrait of the butcher");
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
                // And its sounds, so the fight can be listened to (design 62 §8d).
                if (boot.audioCatalogue == null)
                    boot.audioCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<Odyssey.Presentation.Audio.AudioCatalogue>(
                        "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset");
#endif
                for (int i = 0; i < 8; i++) yield return null;
                shell.Menu.Choose(SessionCommands.NewGameKey);
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
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
                int swingFrames = -1;
                var log = new System.Text.StringBuilder();
                // Every sound the fight is heard in, by id (design 62 §8d).
                var heard = new System.Collections.Generic.SortedDictionary<string, int>();
                if (boot.Audio != null)
                    boot.Audio.Played += id => heard[id] = heard.TryGetValue(id, out int n) ? n + 1 : 1;
                else TestContext.WriteLine("heard nothing: no audio director in this rig");
                for (int frame = 0; frame < 3_000 && !(telegraphed && flung && swingFrames >= 40 && frame >= 600); frame++)
                {
                    world.Tick();
                    rig.FocusOn(colony.Pawns.Size.FromIndex(butcher.Cell), telegraphed && swingFrames < 40 ? 11f : 20f);
                    yield return null;
                    if (!telegraphed && butcher.HeldFacing != 0)
                    {
                        telegraphed = true;
                        swingFrames = 0;
                        yield return Photograph("butcher-windup", target);
                    }
                    if (swingFrames >= 0 && swingFrames < 40)
                    {
                        // What the figure shows through its first swing, frame by frame: the role on
                        // the action layer, how much of it shows, and where the cleaver is.
                        boot.Figures!.TryGetFight(butcher.Id, out CombatRole role, out float weight, out _, out bool computed);
                        Transform? cleaver = boot.Figures.WeaponOf(butcher.Id);
                        string where = cleaver == null ? "no cleaver"
                            : $"cleaver {(cleaver.gameObject.activeInHierarchy ? "shown" : "hidden")} at {cleaver.position} up {cleaver.up} scale {cleaver.lossyScale}";
                        log.AppendLine($"swing frame {swingFrames}: tick {world.CurrentTick} role {role} weight {weight:0.00} computed {computed} pending {butcher.HasPendingSwing}; {where}");
                        if (swingFrames == 8 || swingFrames == 20 || swingFrames == 32)
                            yield return Photograph($"butcher-swing-{swingFrames:00}", target);
                        swingFrames++;
                    }
                    if (!flung && FlungBy(world.Views.Current, butcher.Id))
                    {
                        flung = true;
                        for (int i = 0; i < 6; i++) yield return null;
                        yield return Photograph("butcher-fling", target);
                    }
                }
                // Its death, to hear the last of its four moments.
                colony.Pawns.Combat!.Kill(butcher, null, -1, world.CurrentTick);
                for (int i = 0; i < 20; i++) { world.Tick(); yield return null; }
                TestContext.WriteLine(log.ToString());
                foreach (var pair in heard) TestContext.WriteLine($"heard {pair.Key} x{pair.Value}");
                TestContext.WriteLine($"telegraphed {telegraphed}, flung {flung}");
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (target != null) target.Release();
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Pictures for the handover, not a gate: the four levels side by side in a paused world
        /// (design 62 §4b) — each a size up, each in its own colourway — and the four portraits the
        /// inspect card shows, to <c>Logs/look/butcher-levels.png</c> and <c>butcher-portrait-*.png</c>.
        /// </summary>
        [UnityTest, Explicit("photographs for the handover, not a test")]
        public IEnumerator TheFourLevelsPhotographed()
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
                shell.Menu.NextFromWorld(); // on to the setup page at the planet's suggested site (design 59 §9)
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(shell.Menu.Start(), Is.True);
                for (int i = 0; i < 20; i++) yield return null;
                var colony = boot.Colony!;
                var world = boot.World!;
                if (boot.Figures == null || !boot.Figures.CanDrawKind(PawnKindIndex.ButcherKing)) Assert.Ignore("no butcher art here");

                int[] kinds = { PawnKindIndex.Butcher, PawnKindIndex.ButcherScarred, PawnKindIndex.ButcherBlood, PawnKindIndex.ButcherKing };
                var at = colony.Start;
                for (int k = 0; k < kinds.Length; k++)
                    world.Intents.Submit(new Intent(IntentKind.SpawnPawn, new CellRef(at.X - 3 + 2 * k, at.Z - 6, at.Y), kinds[k]));
                world.Tick();
                for (int i = 0; i < 20; i++) yield return null;

                Directory.CreateDirectory(Path.GetFullPath("Logs/look"));
                for (int k = 0; k < kinds.Length; k++)
                {
                    Texture2D? portrait = boot.Portraits.ForKind(kinds[k]);
                    Assert.That(portrait, Is.Not.Null, $"no portrait for kind {kinds[k]}");
                    File.WriteAllBytes(Path.GetFullPath($"Logs/look/butcher-portrait-{k + 1}.png"), portrait!.EncodeToPNG());
                }

                target = new RenderTexture(1920, 1080, 24) { name = "levels" };
                cam.targetTexture = target;
                rig.FocusOn(new CellRef(at.X, at.Z - 6, at.Y), 26f);
                yield return Photograph("butcher-levels", target);
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
