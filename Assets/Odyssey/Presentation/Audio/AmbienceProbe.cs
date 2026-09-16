#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// Read-only access to one cell's terrain, so the probe can be driven from the render mirror
    /// in the game and from a plain array in a test. The mirror, not the snapshot, is the source:
    /// the snapshot's slice channel knows walkable and solid but not wet, and terrain is the one
    /// thing the mirror already carries for every cell on every layer.
    /// </summary>
    public interface ITerrainLookup
    {
        ushort TerrainAt(int cellIndex);
    }

    /// <summary>
    /// What the environment around the camera is made of: how much water is near, and where the
    /// middle of it is.
    /// </summary>
    public readonly struct AmbienceField
    {
        /// <summary>
        /// 0 to 1: how much of the camera's neighbourhood is water, saturated at 1. This is the
        /// water bed's gain, and it is a coverage and not a nearest-distance on purpose: a river
        /// crossing the whole view is louder than a puddle at the screen's edge, and "how much of
        /// what you are looking at is water" is exactly what the ear expects the number to be.
        /// </summary>
        public readonly float WaterIntensity;

        /// <summary>
        /// The weighted centre of that water, in cell coordinates (x, z). The bed's voice is
        /// placed here, so the sound of the river comes from the river and pans across as the
        /// camera orbits it. Meaningless when <see cref="WaterIntensity"/> is zero.
        /// </summary>
        public readonly Vector2 WaterCentreCell;

        public AmbienceField(float waterIntensity, Vector2 waterCentreCell)
        {
            WaterIntensity = waterIntensity;
            WaterCentreCell = waterCentreCell;
        }

        public static AmbienceField Silent => new(0f, Vector2.zero);
    }

    /// <summary>
    /// Measures the world around the camera as ambience, by sampling the terrain mirror in a
    /// disc.
    ///
    /// <para><b>One voice per environment, not one per cell.</b> A map holds thousands of water
    /// cells; a water sound per cell is thousands of AudioSources, most of them under others,
    /// all of them louder where they overlap. The colony-sim answer — the one the reference games
    /// in this genre all use — is to keep one looping bed per environment type and drive its
    /// volume from how much of that environment is near the camera. This class is the "how
    /// much": a fixed disc of samples, each water cell weighted by how close it is to the centre,
    /// the sum saturating at "clearly full water".</para>
    ///
    /// <para><b>Sampled, not exhaustive, and stride 2.</b> The disc is roughly 600 cells at the
    /// shipped radius; visiting every cell every frame for a number that changes slowly is the
    /// kind of spend the frame budget does not notice until it does. Every second cell in both
    /// directions measures the same coverage to within a percent for a quarter of the visits, and
    /// a pond's centroid does not move between frames.</para>
    ///
    /// <para>Pure: no engine state beyond the vectors, deterministic in iteration order, and the
    /// terrain arrives through <see cref="ITerrainLookup"/> so a test drives it with an
    /// array.</para>
    /// </summary>
    public static class AmbienceProbe
    {
        /// <summary>
        /// The disc's radius, in cells: 14 cells is 35 m, about the rectangle the default camera
        /// distance actually looks at. Water beyond the view can be heard a little — the disc is
        /// bigger than the screen — but a lake a screen away is silence, which is what the ear
        /// expects too.
        /// </summary>
        public const float RadiusCells = 14f;

        /// <summary>Visit every second cell. See the class note.</summary>
        public const int Step = 2;

        /// <summary>
        /// The summed weight that reads as full intensity. Tuned so a river crossing the disc
        /// saturates and a single shore pond nearby reads around a third: the weight is
        /// (1 − d²/R²), so a cell at the centre is worth 1 and one at the rim worth ~0.
        /// </summary>
        public const float SaturateWeight = 12f;

        public static AmbienceField Sample(
            ITerrainLookup terrain, GridSize size, int layer, Vector2 centreCell)
        {
            float radius = RadiusCells;
            float weightSum = 0f, centreX = 0f, centreZ = 0f;

            int minX = Mathf.Max(0, Mathf.CeilToInt(centreCell.x - radius));
            int maxX = Mathf.Min(size.SizeX - 1, Mathf.FloorToInt(centreCell.x + radius));
            int minZ = Mathf.Max(0, Mathf.CeilToInt(centreCell.y - radius));
            int maxZ = Mathf.Min(size.SizeZ - 1, Mathf.FloorToInt(centreCell.y + radius));

            // The stride visits the same parity of world cell whichever way the minimum falls, so
            // the sampled set is anchored on the grid and not on the focus: a set anchored on the
            // focus would swim as the camera glides, and the measured intensity with it.
            int startX = minX + (Step - minX % Step) % Step;
            int startZ = minZ + (Step - minZ % Step) % Step;

            for (int z = startZ; z <= maxZ; z += Step)
            {
                for (int x = startX; x <= maxX; x += Step)
                {
                    float dx = x - centreCell.x;
                    float dz = z - centreCell.y;
                    float weight = 1f - (dx * dx + dz * dz) / (radius * radius);
                    if (weight <= 0f) continue;

                    if (!IsWet(terrain, size, x, z, layer)) continue;

                    weightSum += weight;
                    centreX += x * weight;
                    centreZ += z * weight;
                }
            }

            if (weightSum <= 0f) return AmbienceField.Silent;

            return new AmbienceField(
                Mathf.Clamp01(weightSum / SaturateWeight),
                new Vector2(centreX / weightSum, centreZ / weightSum));
        }

        /// <summary>
        /// Whether the column at (x, z) is water at the slice, reading the layer itself
        /// <b>and the one below it</b>.
        ///
        /// <para>The slice layer is where a colonist <i>stands</i>, not what it stands on: the
        /// start cell is <c>TopSolidY + 1</c>, the air above the ground. Terrain is a property of
        /// the solid cell, so grass, rock and the surface of a pond all live one layer under the
        /// slice, and a probe reading the slice layer alone measures nothing but air — the water
        /// bed would have been silent on every map the game actually generates, with the fixture
        /// passing because it put its pond on the layer it also probed.</para>
        ///
        /// <para>Two layers and not the whole column, because that is exactly the floor underfoot
        /// and the air in front of the eye: water further down is under a floor, and the point of
        /// reading the slice at all is that a player who has descended into a shaft does not hear
        /// the river through the rock.</para>
        /// </summary>
        static bool IsWet(ITerrainLookup terrain, GridSize size, int x, int z, int layer)
        {
            if (NaturalContent.IsWater(terrain.TerrainAt(size.Index(x, z, layer)))) return true;
            return layer > 0
                   && NaturalContent.IsWater(terrain.TerrainAt(size.Index(x, z, layer - 1)));
        }
    }
}
