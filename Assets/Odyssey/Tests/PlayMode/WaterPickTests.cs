#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
    /// A click on water gives the water (owner, 2026-09-25, playing PR #205: "I couldn't click on a
    /// lot of the water tiles anymore, nothing happened, especially shallow ones and smaller water
    /// places"). Measured through the real pick path — the rig's own <c>CellAt</c>, which is the
    /// camera's ray at a screen point handed to <c>SlicePicker</c> with the rig's slice — at the
    /// screen position of the drawn water in each water cell on screen, from the play camera.
    ///
    /// <para>Aimed where the water is drawn: its surface, at <see cref="ChunkMesher.WaterSurface"/>
    /// of a layer over the bed, at the cell's centre and at four points a third of a cell out.
    /// Every bank beside that water is clicked at its centre too, as the control that the fix does
    /// not take clicks from the land.</para>
    /// </summary>
    public class WaterPickTests
    {
        int _landWrongControl;

        [UnityTest]
        public IEnumerator AClickOnDrawnWaterGivesTheWater()
        {
            bool shoreWas = WaterShore.Enabled;
            try
            {
                // The control first: the square shore, as main draws it.
                WaterShore.Enabled = false;
                yield return Measure(assert: false);
                WaterShore.Enabled = shoreWas;
                yield return Measure(assert: true);
            }
            finally
            {
                WaterShore.Enabled = shoreWas;
            }
        }

        IEnumerator Measure(bool assert)
        {
            GameObject root = Build(out OdysseyBootstrap boot, out SliceCameraRig rig);
            try
            {
                for (int i = 0; i < 60; i++) yield return null;
                for (int i = 0; i < 120 && boot.World!.GameSpeed != 0; i++)
                {
                    boot.World!.Intents.Submit(new Intent(IntentKind.SetGameSpeed, default, 0));
                    yield return null;
                }

                var model = boot.Model!;
                var size = model.Size;
                var grid = boot.Colony!.Grid;
                CellRef start = boot.Colony!.Start;

                // Two framings: the water nearest the colony, and the widest water on the board.
                CellRef nearest = default, widest = default;
                int bestDistance = int.MaxValue, bestCount = -1;
                for (int y = 0; y < size.SizeY; y++)
                for (int z = 2; z < size.SizeZ - 2; z++)
                for (int x = 2; x < size.SizeX - 2; x++)
                {
                    if (!NaturalContent.IsWater(grid.Terrain[size.Index(x, z, y)])) continue;
                    int d = Math.Abs(x - start.X) + Math.Abs(z - start.Z);
                    if (d < bestDistance) { bestDistance = d; nearest = new CellRef(x, z, y); }
                    int count = 0;
                    for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                        if (NaturalContent.IsWater(grid.Terrain[size.Index(x + dx, z + dz, y)])) count++;
                    if (count > bestCount) { bestCount = count; widest = new CellRef(x, z, y); }
                }
                Assert.That(bestCount, Is.GreaterThan(0), "the board grew no water");

                MethodInfo cellAt = typeof(SliceCameraRig).GetMethod("CellAt",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.That(cellAt, Is.Not.Null, "SliceCameraRig.CellAt is the pick path this measures");
                Camera cam = rig.Camera;

                int waterPoints = 0, waterWrong = 0, landPoints = 0, landWrong = 0;
                int shallowWrong = 0, deepWrong = 0, narrowWrong = 0;
                var examples = new List<string>();
                float h = CellMetrics.SizeY, s = CellMetrics.SizeXZ;
                float[] du = { 0f, -0.33f, 0.33f, 0f, 0f };
                float[] dv = { 0f, 0f, 0f, -0.33f, 0.33f };

                foreach (CellRef focus in new[] { nearest, widest })
                {
                    rig.FocusOn(focus, 36f);
                    for (int i = 0; i < 240; i++) yield return null;
                    int active = rig.ActiveLayer;

                    for (int z = Math.Max(1, focus.Z - 14); z < Math.Min(size.SizeZ - 1, focus.Z + 14); z++)
                    for (int x = Math.Max(1, focus.X - 14); x < Math.Min(size.SizeX - 1, focus.X + 14); x++)
                    {
                        int water = -1;
                        for (int y = size.SizeY - 1; y >= 0; y--)
                            if (NaturalContent.IsWater(grid.Terrain[size.Index(x, z, y)])) { water = y; break; }
                        if (water < 0) continue;
                        if (water < rig.LowestSelectableLayer || water > rig.HighestSelectableLayer) continue;

                        ushort terrain = grid.Terrain[size.Index(x, z, water)];
                        int wet = 0;
                        for (int dir = 0; dir < 4; dir++)
                        {
                            int nx = x + (dir == 0 ? 1 : dir == 1 ? -1 : 0), nz = z + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                            if (NaturalContent.IsWater(grid.Terrain[size.Index(nx, nz, water)])) wet++;
                        }
                        var target = new CellRef(x, z, water);
                        for (int k = 0; k < du.Length; k++)
                        {
                            float wx = (x + 0.5f + du[k]) * s, wz = (z + 0.5f + dv[k]) * s;
                            var point = new Vector3(wx, water * h + ChunkMesher.WaterSurface * h + GroundRelief.HeightAt(wx, wz), wz);
                            Vector3 screen = cam.WorldToScreenPoint(point);
                            if (screen.z <= 0f || screen.x < 0 || screen.y < 0 || screen.x > cam.pixelWidth || screen.y > cam.pixelHeight) continue;
                            object?[] args = { new Vector2(screen.x, screen.y), null };
                            bool hit = (bool)cellAt.Invoke(rig, args)!;
                            CellRef got = hit ? (CellRef)args[1]! : default;
                            waterPoints++;
                            if (hit && got.Equals(target)) continue;
                            waterWrong++;
                            if (terrain == NaturalContent.TerrainDeepWater) deepWrong++; else shallowWrong++;
                            if (wet <= 2) narrowWrong++;
                            if (examples.Count < 12)
                                examples.Add($"{target}{(k == 0 ? " centre" : $" +({du[k]},{dv[k]})")} -> {(hit ? got.ToString() : "nothing")}");
                        }

                        // The banks beside it: clicked at the centre of their drawn top.
                        for (int dir = 0; dir < 4; dir++)
                        {
                            int nx = x + (dir == 0 ? 1 : dir == 1 ? -1 : 0), nz = z + (dir == 2 ? 1 : dir == 3 ? -1 : 0);
                            int bank = size.Index(nx, nz, water);
                            if ((grid.Flags[bank] & Odyssey.Sim.World.CellFlags.SolidTerrain) == 0) continue;
                            if (water + 1 < size.SizeY && (grid.Flags[bank + size.LayerStride] & Odyssey.Sim.World.CellFlags.SolidTerrain) != 0) continue;
                            float bx = (nx + 0.5f) * s, bz = (nz + 0.5f) * s;
                            var top = new Vector3(bx, (water + 1) * h + GroundRelief.HeightAt(bx, bz), bz);
                            Vector3 screen = cam.WorldToScreenPoint(top);
                            if (screen.z <= 0f || screen.x < 0 || screen.y < 0 || screen.x > cam.pixelWidth || screen.y > cam.pixelHeight) continue;
                            object?[] args = { new Vector2(screen.x, screen.y), null };
                            bool hit = (bool)cellAt.Invoke(rig, args)!;
                            CellRef got = hit ? (CellRef)args[1]! : default;
                            landPoints++;
                            // The bank block, or whatever stands on it (a tree, a bush), is the land.
                            if (hit && got.X == nx && got.Z == nz && (got.Y == water || got.Y == water + 1)) continue;
                            landWrong++;
                            if (examples.Count < 18) examples.Add($"bank {new CellRef(nx, nz, water)} -> {(hit ? got.ToString() : "nothing")}");
                        }
                    }
                    Debug.Log($"[WaterPick] framed on {focus}, active layer {active}");
                }

                Debug.Log($"[WaterPick] shoreline {(WaterShore.Enabled ? "on" : "off")}: water {waterWrong}/{waterPoints} wrong " +
                          $"(shallow {shallowWrong}, deep {deepWrong}, in a stream or pool {narrowWrong}); " +
                          $"banks {landWrong}/{landPoints} wrong. " + string.Join("; ", examples));
                if (!assert) { _landWrongControl = landWrong; yield break; }
                Assert.That(waterPoints, Is.GreaterThan(50), "too little water on screen to measure");
                Assert.That(landPoints, Is.GreaterThan(20), "too few banks on screen to measure");
                Assert.That(waterWrong, Is.Zero, "a click on drawn water must give that water cell");
                Assert.That(landWrong, Is.LessThanOrEqualTo(_landWrongControl),
                    "a click on a bank must give the bank at least as often as before the fix");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
        }

        static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig)
        {
            var root = new GameObject("WaterPick");
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
            boot.mapType = Odyssey.Sim.Worldgen.Natural.MapType.Natural;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.SetActive(true);
            return root;
        }
    }
}
