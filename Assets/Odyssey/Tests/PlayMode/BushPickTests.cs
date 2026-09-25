#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A click on a drawn bush gives the bush (owner, 2026-09-25, playing #222: "I couldn't click on
    /// some of the berry bushes properly"). Measured through the real pick path — the rig's own
    /// <c>CellAt</c>, the camera's ray at a screen point handed to <c>SlicePicker</c> with the rig's
    /// slice — at the screen positions of each bush on screen: the middle of its drawn crown and
    /// four points on the crown half way to its drawn edge, taken through the bush's own drawn
    /// matrix (<see cref="ChunkMesher.TryBushPlacement"/>). Plain bushes, ripe berry bushes and
    /// picked ones alike. The open ground beside each bush is clicked too, as the control that the
    /// fix does not take the neighbouring cells' clicks (design 45 §12).
    /// </summary>
    public class BushPickTests
    {
        [UnityTest]
        public IEnumerator AClickOnADrawnBushGivesTheBush()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                yield return Settle(boot, rig);
                var after = new Tally();
                yield return Measure(boot, rig, after);
                // Before the fix (the same measurement, 2026-09-25, the bush claimed only where the
                // ray crossed its cell's floor): centre 14/18 wrong, crown 56/73 wrong.
                Debug.Log($"[BushPick] {after}");

                Assert.That(after.Bushes, Is.GreaterThan(10), "too few bushes on screen to measure");
                Assert.That(after.Berry, Is.GreaterThan(0), "no ripe berry bush was on screen");
                Assert.That(after.Picked, Is.GreaterThan(0), "no picked berry bush was on screen");
                Assert.That(after.CentreWrong, Is.Zero, "a click on the middle of a drawn bush must give the bush");
                Assert.That(after.CrownWrong * 20, Is.LessThanOrEqualTo(after.CrownPoints),
                    "a click on a drawn bush's crown must give the bush (at most one in twenty lost at its rim)");
                Assert.That(after.NamedWrong, Is.Zero, "the pane must name the bush that was clicked");
                Assert.That(after.GroundTakenByTheBush, Is.Zero,
                    "a click on open ground beside a bush must never be taken by that bush");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
        }

        /// <summary>
        /// Close-ups of a ripe berry bush and a picked one from the play camera, for judging the
        /// berries (design 45 §12). Explicit; <c>ODYSSEY_BERRY_BEFORE=1</c> draws the berries as they
        /// were first built, prefixed <c>before-</c>. Written to <c>Logs/look/</c>.
        /// </summary>
        [UnityTest, Explicit("a photograph for judging the berries, not a test")]
        public IEnumerator TheBerryBushAtThePlayCamera()
        {
            bool old = Environment.GetEnvironmentVariable("ODYSSEY_BERRY_BEFORE") == "1";
            GameObject root = Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            RenderTexture? target = null;
            Camera cam = rig.Camera;
            try
            {
                yield return Settle(boot, rig);
                ChunkMesher.BerriesOnTheCrown = !old;
                var grid = boot.Colony!.Grid;
                var size = grid.Size;
                CellRef start = boot.Colony.Start;
                int ripe = -1, bare = -1, bestRipe = int.MaxValue, bestBare = int.MaxValue;
                for (int i = 0; i < size.CellCount; i++)
                {
                    if (!boot.Colony.Designations.IsRipeBerryBush(i)) continue;
                    CellRef at = size.FromIndex(i);
                    // In the open, so no crown hangs over the bush being judged.
                    bool shaded = false;
                    for (int dz = -2; dz <= 2 && !shaded; dz++)
                    for (int dx = -2; dx <= 2 && !shaded; dx++)
                        if (size.Contains(at.X + dx, at.Z + dz, at.Y)
                            && boot.Colony.Designations.IsTree(size.Index(at.X + dx, at.Z + dz, at.Y))) shaded = true;
                    if (shaded) continue;
                    int d = Math.Abs(at.X - start.X) + Math.Abs(at.Z - start.Z);
                    if (d < bestRipe) { bestBare = bestRipe; bare = ripe; bestRipe = d; ripe = i; }
                    else if (d < bestBare) { bestBare = d; bare = i; }
                }
                Assert.That(ripe, Is.GreaterThanOrEqualTo(0), "no berry bush on the board");
                if (bare >= 0) boot.Colony.Pawns.Nature!.Pick(bare, boot.World!.CurrentTick);
                yield return Remesh(boot);

                target = new RenderTexture(1920, 1080, 24) { name = "berries" };
                cam.targetTexture = target;
                Directory.CreateDirectory(Path.GetFullPath("Logs/look"));
                string prefix = old ? "before-" : string.Empty;
                rig.FocusOn(size.FromIndex(ripe), 9f);
                for (int i = 0; i < 150; i++) yield return null;
                yield return Photograph(prefix + "berry-ripe", target);
                if (bare >= 0)
                {
                    rig.FocusOn(size.FromIndex(bare), 9f);
                    for (int i = 0; i < 150; i++) yield return null;
                    yield return Photograph(prefix + "berry-picked", target);
                }
            }
            finally
            {
                ChunkMesher.BerriesOnTheCrown = true;
                cam.targetTexture = null;
                if (target != null) target.Release();
                UnityEngine.Object.Destroy(root);
            }
        }

        sealed class Tally
        {
            public int Occluded, GroundTakenByTheBush;
            public int Bushes, Berry, Picked, CentrePoints, CentreWrong, CrownPoints, CrownWrong, NamedWrong,
                GroundPoints, GroundWrong;
            public readonly List<string> Examples = new List<string>();
            public override string ToString() =>
                $"{Bushes} bushes ({Berry} berry, {Picked} picked); centre {CentreWrong}/{CentrePoints} wrong, " +
                $"crown {CrownWrong}/{CrownPoints} wrong, named wrong {NamedWrong}, hidden by a hill {Occluded}; ground beside {GroundWrong}/{GroundPoints} wrong ({GroundTakenByTheBush} taken by the bush). " +
                string.Join("; ", Examples);
        }

        static IEnumerator Settle(OdysseyBootstrap boot, SliceCameraRig rig)
        {
            for (int i = 0; i < 60; i++) yield return null;
            // Two picked berry bushes near the start, so the bare kind is on screen and measured
            // too. Picked while the world runs, so the mirror publishes the change before the pause.
            var colony = boot.Colony!;
            var size = colony.Grid.Size;
            CellRef start = colony.Start;
            for (int picked = 0; picked < 2; picked++)
            {
                int best = -1, bestD = int.MaxValue;
                for (int i = 0; i < size.CellCount; i++)
                {
                    if (!colony.Designations.IsRipeBerryBush(i)) continue;
                    CellRef at = size.FromIndex(i);
                    int d = Math.Abs(at.X - start.X) + Math.Abs(at.Z - start.Z);
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) colony.Pawns.Nature!.Pick(best, boot.World!.CurrentTick);
            }
            for (int i = 0; i < 30; i++) yield return null;
            for (int i = 0; i < 120 && boot.World!.GameSpeed != 0; i++)
            {
                boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                yield return null;
            }
            for (int i = 0; i < 10; i++) yield return null;
        }

        static IEnumerator Remesh(OdysseyBootstrap boot)
        {
            boot.Model!.Remesh();
            for (int i = 0; i < 240; i++) yield return null;
        }

        static IEnumerator Measure(OdysseyBootstrap boot, SliceCameraRig rig, Tally tally)
        {
            var colony = boot.Colony!;
            var grid = colony.Grid;
            var size = grid.Size;
            var model = boot.Model!;
            ChunkMesher mesher = boot.Renderer!.Mesher;
            MethodInfo cellAt = typeof(SliceCameraRig).GetMethod("CellAt", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.That(cellAt, Is.Not.Null, "SliceCameraRig.CellAt is the pick path this measures");
            Camera cam = rig.Camera;

            // Framed on the bushes nearest the start, then on a second patch further out.
            var focuses = new List<CellRef>();
            CellRef start = colony.Start;
            int best = int.MaxValue; CellRef near = start;
            for (int i = 0; i < size.CellCount; i++)
            {
                if (!grid.IsUndergrowth(i)) continue;
                CellRef at = size.FromIndex(i);
                int d = Math.Abs(at.X - start.X) + Math.Abs(at.Z - start.Z);
                if (d < best) { best = d; near = at; }
            }
            focuses.Add(near);
            focuses.Add(new CellRef(Mathf.Clamp(near.X + 20, 2, size.SizeX - 3), Mathf.Clamp(near.Z + 12, 2, size.SizeZ - 3), near.Y));

            bool Hidden(CellRef got, Vector3 aimed) => HiddenBy(cam, grid, got, aimed);
            float[] du = { 0.5f, -0.5f, 0f, 0f };
            float[] dv = { 0f, 0f, 0.5f, -0.5f };
            int undergrowth = 0, placedCount = 0;
            for (int i = 0; i < size.CellCount; i++)
                if (grid.IsUndergrowth(i))
                {
                    undergrowth++;
                    CellRef c = size.FromIndex(i);
                    if (mesher.TryBushPlacement(c.X, c.Z, c.Y, out _, out _)) placedCount++;
                }
            Debug.Log($"[BushPick] {undergrowth} bush cells on the board, {placedCount} with a drawn placement; focus {near}");
            if (placedCount == 0) Assert.Ignore("no bush art resolved on this machine, so there is no drawn bush to aim at");
            foreach (CellRef focus in focuses)
            {
                rig.FocusOn(focus, 36f);
                for (int i = 0; i < 240; i++) yield return null;
                Debug.Log($"[BushPick] framed on {focus}: selectable {rig.LowestSelectableLayer}..{rig.HighestSelectableLayer}, active {rig.ActiveLayer}");

                for (int z = Math.Max(1, focus.Z - 14); z < Math.Min(size.SizeZ - 1, focus.Z + 14); z++)
                for (int x = Math.Max(1, focus.X - 14); x < Math.Min(size.SizeX - 1, focus.X + 14); x++)
                for (int y = rig.LowestSelectableLayer; y <= rig.HighestSelectableLayer && y < size.SizeY; y++)
                {
                    int index = size.Index(x, z, y);
                    if (!grid.IsUndergrowth(index)) continue;
                    if (!mesher.TryBushPlacement(x, z, y, out Matrix4x4 placed, out int module)) continue;
                    Bounds crown = model.Library[module].Bounds;
                    ushort def = model.EdificeDef(index);
                    var target = new CellRef(x, z, y);
                    if (!Visible(cam, placed.MultiplyPoint3x4(crown.center))) continue;
                    tally.Bushes++;
                    if (def == NaturalContent.EdificeBerryBush) tally.Berry++;
                    if (def == NaturalContent.EdificeBerryBushPicked) tally.Picked++;
                    string title = EdificeLabels.Title(def);

                    // The middle of the crown, at the top of the bush the camera sees.
                    Vector3 top = crown.center + new Vector3(0f, crown.extents.y * 0.6f, 0f);
                    Click(placed.MultiplyPoint3x4(top), "centre", ref tally.CentrePoints, ref tally.CentreWrong);
                    for (int k = 0; k < du.Length; k++)
                    {
                        Vector3 local = crown.center + new Vector3(du[k] * crown.extents.x, crown.extents.y * 0.3f, dv[k] * crown.extents.z);
                        Click(placed.MultiplyPoint3x4(local), $"crown({du[k]},{dv[k]})", ref tally.CrownPoints, ref tally.CrownWrong);
                    }

                    void Click(Vector3 world, string what, ref int points, ref int wrong)
                    {
                        if (!Visible(cam, world)) return;
                        Vector3 screen = cam.WorldToScreenPoint(world);
                        object?[] args = { new Vector2(screen.x, screen.y), null };
                        bool hit = (bool)cellAt.Invoke(rig, args)!;
                        CellRef got = hit ? (CellRef)args[1]! : default;
                        points++;
                        if (hit && got.Equals(target))
                        {
                            if (EdificeLabels.Title(model.EdificeDef(size.Index(got))) != title) tally.NamedWrong++;
                            return;
                        }
                        // A click that lands on another bush drawn in front is that bush's, fairly; and
                        // one that meets a hillside nearer the camera than the point aimed at was aimed
                        // at a bush the hill hides, which is not a pick fault.
                        if (hit && grid.IsUndergrowth(size.Index(got)) && !got.Equals(target)) { points--; return; }
                        if (hit && Hidden(got, world)) { points--; tally.Occluded++; return; }
                        wrong++;
                        if (tally.Examples.Count < 14)
                            tally.Examples.Add($"{(def == NaturalContent.EdificeBush ? "bush" : "berry")} {target} {what} -> {(hit ? got.ToString() : "nothing")}");
                    }

                    // The ground beside it: the four neighbours that hold nothing, clicked at the
                    // middle of their drawn top.
                    for (int dir = 0; dir < 4; dir++)
                    {
                        int nx = x + (dir == 0 ? 1 : dir == 1 ? -1 : 0), nz = z + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                        int n = size.Index(nx, nz, y);
                        if (grid.Edifice[n] >= 0 || !grid.IsWalkable(n)) continue;
                        float gx = (nx + 0.5f) * CellMetrics.SizeXZ, gz = (nz + 0.5f) * CellMetrics.SizeXZ;
                        var ground = new Vector3(gx, y * CellMetrics.SizeY + GroundRelief.HeightAt(gx, gz), gz);
                        if (!Visible(cam, ground)) continue;
                        Vector3 screen = cam.WorldToScreenPoint(ground);
                        object?[] args = { new Vector2(screen.x, screen.y), null };
                        bool hit = (bool)cellAt.Invoke(rig, args)!;
                        CellRef got = hit ? (CellRef)args[1]! : default;
                        tally.GroundPoints++;
                        if (hit && got.X == nx && got.Z == nz && (got.Y == y || got.Y == y - 1)) continue;
                        if (hit && got.Equals(target))
                        {
                            // Ground behind the bush, as the camera sees it, is hidden by the bush:
                            // the bush is what was clicked. Ground in front of it never is.
                            Vector3 eye = cam.transform.position;
                            Vector3 bushFoot = CellMetrics.FloorCentre(x, z, y);
                            var flatEye = new Vector2(eye.x, eye.z);
                            bool behind = Vector2.Distance(flatEye, new Vector2(ground.x, ground.z))
                                          > Vector2.Distance(flatEye, new Vector2(bushFoot.x, bushFoot.z));
                            if (behind) { tally.GroundPoints--; continue; }
                            tally.GroundTakenByTheBush++; tally.GroundWrong++; continue;
                        }
                        if (hit && (grid.IsUndergrowth(size.Index(got)) || Hidden(got, ground))) { tally.GroundPoints--; continue; }
                        tally.GroundWrong++;
                        if (tally.Examples.Count < 20) tally.Examples.Add($"ground {new CellRef(nx, nz, y)} -> {(hit ? got.ToString() : "nothing")}");
                    }
                }
            }
        }

        /// <summary>Is the cell picked solid ground standing nearer the camera than the point aimed at?</summary>
        static bool HiddenBy(Camera cam, Odyssey.Sim.World.CellGrid grid, CellRef got, Vector3 aimed)
        {
            int index = grid.Size.Index(got);
            bool solid = (grid.Flags[index] & Odyssey.Sim.World.CellFlags.SolidTerrain) != 0;
            if (!solid) return false;
            Vector3 top = CellMetrics.FloorCentre(got.X, got.Z, got.Y) + Vector3.up * CellMetrics.SizeY;
            return got.Y * CellMetrics.SizeY + CellMetrics.SizeY > aimed.y + 0.5f
                && Vector3.Distance(cam.transform.position, top) < Vector3.Distance(cam.transform.position, aimed);
        }

        static bool Visible(Camera cam, Vector3 world)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            return s.z > 0f && s.x >= 0 && s.y >= 0 && s.x <= cam.pixelWidth && s.y <= cam.pixelHeight;
        }

        static IEnumerator Photograph(string name, RenderTexture target)
        {
            for (int i = 0; i < 8; i++) yield return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string path = Path.GetFullPath($"Logs/look/{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            Debug.Log($"[Look] {name}: {path}");
        }

        static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig)
        {
            var root = new GameObject("BushPick");
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1800f;
            rig = cameraObject.AddComponent<SliceCameraRig>();

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);
            boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.buildOnPlay = true;
            boot.sizeX = 120;
            boot.sizeZ = 120;
            boot.layers = 16;
            boot.seed = 1;
            boot.mapType = MapType.Natural;
            boot.barrenMap = true;
            boot.cameraRig = rig;
            // The art, because the thing measured is where the drawn bush is: without the packs
            // there is no bush to aim at and the test ignores itself (it asks whether the art
            // resolved, never whether a catalogue exists — CLAUDE.md, the runner has none).
#if UNITY_EDITOR
            boot.moduleCatalogue = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(
                "Assets/Odyssey/Presentation/ModuleCatalogue.asset");
#endif
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 2.0f;
            sun.transform.rotation = Quaternion.Euler(30f, 135f, 0f);
            bootObject.SetActive(true);
            return root;
        }
    }
}
