#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A click on a drawn campfire gives the campfire (design 43; owner, 2026-09-25: a second
    /// campfire "did nothing" when clicked, one terrace above the layer being viewed, while the
    /// first opened its pane).
    ///
    /// <para><b>Two faults, both measured here through the rig's own pick path.</b> Building raises
    /// the walls, and the surface followed the raised walls, so a slice on a lower terrace became
    /// "underground" and the terrace above it an x-ray nothing on it could be clicked through —
    /// every click on a fire a terrace up missed. And a campfire offered only its floor, so a click
    /// on its flames crossed that floor beyond it and a rolling neighbour could take it — up to half
    /// the clicks on the lower terraces, with the walls down. The surface now follows the Walls down
    /// <i>choice</i> (<c>SliceSettings.landscapeGround</c>), and a campfire offers its flames
    /// (<c>WorldRenderModel.StandHeight</c>).</para>
    ///
    /// <para>Flat ground only, as both of the owner's fires stood: a fire just behind a terrace riser
    /// is hidden from a camera on that side, and the riser taking the click is then right. Views
    /// beneath the whole landscape are left out: there the layer above is an x-ray on purpose.</para>
    ///
    /// <para><b>A miss is sorted by where it went.</b> In front of the fire — a nearer column — is
    /// the rolling ground hiding its edge, which is the picker being right, and a few in a hundred
    /// are allowed. Under it, behind it or nothing at all is the ray going through the fire, and
    /// none is. Before the fix the building case put every click from a lower slice under the fire
    /// (its own column, the ground it stands on).</para>
    /// </summary>
    public class CampfirePickTests
    {
        [UnityTest]
        public IEnumerator AClickOnADrawnCampfireGivesTheCampfire()
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

                var colony = boot.Colony!;
                var grid = colony.Grid;
                var size = grid.Size;
                HudDirectors directors = boot.Directors!;
                Assert.That(directors.Settings.IsOn(GraphicsOption.WallsDown), Is.True, "Walls down is on by default");

                int Surface(int x, int z)
                {
                    for (int y = size.SizeY - 2; y >= 0; y--)
                        if ((grid.Flags[size.Index(x, z, y)] & CellFlags.SolidTerrain) != 0) return y + 1;
                    return -1;
                }

                bool Flat(int x, int z, int y)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (Surface(x + dx, z + dz) != y) return false;
                    return true;
                }

                // Three fires on every surface level, on flat ground, ten cells in from the edge
                // (the surround's rim beyond it takes clicks aimed near it).
                var fires = new List<CellRef>();
                var perLayer = new SortedDictionary<int, int>();
                for (int z = 10; z < size.SizeZ - 10; z += 4)
                for (int x = 10; x < size.SizeX - 10; x += 4)
                {
                    int y = Surface(x, z);
                    if (y < 1 || !Flat(x, z, y)) continue;
                    perLayer.TryGetValue(y, out int onLayer);
                    if (onLayer >= 3) continue;
                    var before = new HashSet<int>(colony.Construction.Sites);
                    if (colony.Construction.Place(new CellRef(x, z, y), BuildingHandle.Campfire, StuffHandle.Wood)
                        != IntentRejection.None) continue;
                    int site = -1;
                    foreach (int each in colony.Construction.Sites) if (!before.Contains(each)) site = each;
                    if (site < 0 || !colony.Construction.Raise(colony.Pawns, site)) continue;
                    fires.Add(size.FromIndex(site));
                    perLayer[y] = onLayer + 1;
                }
                boot.World!.RepublishViews();
                for (int i = 0; i < 30; i++) yield return null;
                Assert.That(perLayer.Count, Is.GreaterThan(1), "the board gave no flat ground on two levels");

                int ground = boot.Model!.LowestOutdoorLayer + 1;
                MethodInfo cellAt = typeof(SliceCameraRig).GetMethod("CellAt",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.That(cellAt, Is.Not.Null, "SliceCameraRig.CellAt is the pick path this measures");
                Camera cam = rig.Camera;
                float h = CellMetrics.SizeY, s = CellMetrics.SizeXZ;
                float[] du = { 0f, -0.2f, 0.2f, 0f, 0f };
                float[] dv = { 0f, 0f, 0f, -0.2f, 0.2f };

                var failures = new List<string>();
                foreach (bool building in new[] { false, true })
                {
                    // Building raises the walls: the owner's case was a fire clicked while building.
                    directors.Designate.Tool = building ? DesignateTool.Build : DesignateTool.None;

                    var points = new SortedDictionary<string, int>();
                    var wrong = new SortedDictionary<string, int>();
                    var examples = new List<string>();
                    int inFront = 0, through = 0;
                    foreach (CellRef fire in fires)
                    foreach (int below in new[] { 0, 1, 2 })
                    {
                        // The fire framed, with the slice on its layer or one or two beneath it.
                        if (fire.Y - below < ground) continue;
                        rig.FocusOn(fire, 36f);
                        directors.Slice.SetLayer(fire.Y - below);
                        for (int i = 0; i < 90; i++) yield return null;
                        int viewed = rig.ActiveLayer;
                        string key = $"fire L{fire.Y} from L{viewed}";
                        points.TryGetValue(key, out int p0); points[key] = p0;
                        wrong.TryGetValue(key, out int w0); wrong[key] = w0;

                        for (int k = 0; k < du.Length; k++)
                        {
                            // On the flames: FireDirector.FlameHeight up the drawn fire.
                            float wx = (fire.X + 0.5f + du[k]) * s, wz = (fire.Z + 0.5f + dv[k]) * s;
                            var point = new Vector3(wx, fire.Y * h + GroundRelief.HeightAt(wx, wz)
                                + Odyssey.Presentation.World.FireDirector.FlameHeight, wz);
                            Vector3 screen = cam.WorldToScreenPoint(point);
                            if (screen.z <= 0f || screen.x < 0 || screen.y < 0
                                || screen.x > cam.pixelWidth || screen.y > cam.pixelHeight) continue;
                            object?[] args = { new Vector2(screen.x, screen.y), null };
                            bool hit = (bool)cellAt.Invoke(rig, args)!;
                            CellRef got = hit ? (CellRef)args[1]! : default;
                            points[key]++;
                            if (hit && got.Equals(fire)) continue;
                            wrong[key]++;

                            // In front: another column, nearer the camera across the ground. Across
                            // the ground because a solid cell is clicked on its top, a layer above the
                            // centre it is named by.
                            Vector3 camAt = cam.transform.position;
                            float Across(int x, int z)
                            {
                                Vector3 c = CellMetrics.FloorCentre(x, z, 0);
                                return new Vector2(c.x - camAt.x, c.z - camAt.z).magnitude;
                            }
                            bool front = hit && (got.X != fire.X || got.Z != fire.Z)
                                && Across(got.X, got.Z) < Across(fire.X, fire.Z);
                            if (front) inFront++; else through++;
                            if (examples.Count < 12)
                                examples.Add($"{key} {fire}{(k == 0 ? " centre" : $" +({du[k]},{dv[k]})")} -> " +
                                             (hit ? got.ToString() : "nothing") + (front ? " (in front)" : " (THROUGH)"));
                        }
                    }

                    var tally = new List<string>();
                    int totalWrong = 0, totalPoints = 0;
                    foreach (var pair in points)
                    {
                        tally.Add($"{pair.Key}: {wrong[pair.Key]}/{pair.Value}");
                        totalWrong += wrong[pair.Key];
                        totalPoints += pair.Value;
                    }
                    string line = $"[CampfirePick] {(building ? "building (walls raised)" : "walls down")}: " +
                                  $"{totalWrong}/{totalPoints} clicks missed, {through} through the fire and " +
                                  $"{inFront} in front of it; {string.Join("; ", tally)}" +
                                  (examples.Count > 0 ? $" || {string.Join("; ", examples)}" : string.Empty);
                    Debug.Log(line);
                    if (through > 0 || inFront > totalPoints * 3 / 100) failures.Add(line);
                    Assert.That(totalPoints, Is.GreaterThan(0), "no click was aimed");
                }
                directors.Designate.Tool = DesignateTool.None;

                Assert.That(failures, Is.Empty, "a click on a drawn campfire gave something else");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject Build(out OdysseyBootstrap boot, out SliceCameraRig rig)
        {
            var root = new GameObject("CampfirePick");
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
            boot.woodedMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.SetActive(true);
            return root;
        }
    }
}
