#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The materials a player chooses between have to look like different materials.
    ///
    /// <para>This exists because two of them did not. Stone was <c>(0.86, 0.87, 0.88)</c> and steel
    /// <c>(0.82, 0.86, 0.92)</c> — the same pale blue-grey, with stone the brighter of the two — so
    /// a stone floor read as a steel one (owner, 2026-09-17). Nothing failed, because nothing was
    /// asking. A tint table is exactly the kind of content that drifts one plausible number at a
    /// time, and the drift is only visible when the numbers are put side by side.</para>
    /// </summary>
    public class StuffPaletteTests
    {
        /// <summary>How far apart two tints must be to count as different materials.</summary>
        const float Apart = 0.12f;

        static float Distance(Color a, Color b)
        {
            float r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }

        [Test]
        public void StoneDoesNotLookLikeSteel()
        {
            Color stone = StuffPalette.For(NaturalContent.StuffStone, overArt: true);
            Color steel = StuffPalette.For(CoreContent.StuffSteel, overArt: true);

            Assert.That(Distance(stone, steel), Is.GreaterThan(Apart),
                $"stone {stone} and steel {steel} are the same colour");
            Assert.That(stone.r + stone.g + stone.b, Is.LessThan(steel.r + steel.g + steel.b),
                "stone is brighter than steel, which is how it came to read as polished metal");
        }

        /// <summary>
        /// Stone is a neutral or warm grey, never a cool one. A blue cast is what says "metal"
        /// here — it is the one thing steel and composite have in common and stone had borrowed.
        /// </summary>
        [Test]
        public void StoneIsNotACoolGrey()
        {
            Color stone = StuffPalette.For(NaturalContent.StuffStone, overArt: true);
            Assert.That(stone.b, Is.LessThanOrEqualTo(stone.r), $"stone {stone} leans blue");
        }

        /// <summary>
        /// The tint used where there is art and the one used where there is none describe the same
        /// material, so they may differ in brightness and not in character. The over-art entry had
        /// drifted away from a fallback that was right all along.
        /// </summary>
        [Test]
        public void StoneMeansTheSameThingWithArtAndWithout()
        {
            Color over = StuffPalette.For(NaturalContent.StuffStone, overArt: true);
            Color flat = StuffPalette.StuffSolid(NaturalContent.StuffStone);

            Assert.That(over.b, Is.LessThanOrEqualTo(over.r));
            Assert.That(flat.b, Is.LessThanOrEqualTo(flat.r));
            Assert.That(Distance(over, flat), Is.LessThan(0.25f),
                $"with art {over} and without {flat} are not the same material");
        }

        /// <summary>
        /// Every material a colonist can actually build with is told apart from every other one.
        ///
        /// <para><b>The buildable table rather than a list written here</b>, so the day steel gets
        /// an item this starts asking about steel without anybody remembering to add it — which is
        /// the failure that produced the bug above, where the one comparison nobody had made was
        /// the one that mattered.</para>
        ///
        /// <para><b>A known gap, deliberately not asserted:</b> concrete, steel and composite are
        /// within this distance of one another — a pale near-white trio, at most 0.09 apart. None
        /// is buildable, so no player is asked to choose between them and nothing is broken today;
        /// they are the generator's, and the ruined city is meant to be uniform. The moment one of
        /// them gets an item, this test starts failing and that is correct.</para>
        /// </summary>
        [Test]
        public void EveryBuildableMaterialIsToldApartFromEveryOther()
        {
            var tints = new System.Collections.Generic.List<(int Handle, Color Tint)>();
            for (int handle = 1; handle < ConstructionContent.Stuffs.Count; handle++)
            {
                if (!ConstructionContent.IsBuildable(handle)) continue;
                tints.Add((handle, StuffPalette.For(ConstructionContent.StuffAt(handle).stuff, overArt: true)));
            }

            Assert.That(tints.Count, Is.GreaterThanOrEqualTo(2),
                "fewer than two buildable materials makes this test vacuous");

            for (int i = 0; i < tints.Count; i++)
            for (int j = i + 1; j < tints.Count; j++)
                Assert.That(Distance(tints[i].Tint, tints[j].Tint), Is.GreaterThan(Apart),
                    $"{ConstructionContent.StuffAt(tints[i].Handle).defName} {tints[i].Tint} and " +
                    $"{ConstructionContent.StuffAt(tints[j].Handle).defName} {tints[j].Tint} look the same");
        }
    }
}
