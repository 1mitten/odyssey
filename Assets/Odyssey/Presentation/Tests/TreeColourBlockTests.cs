#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The per-instance tree colours survive a property block being reused at two different sizes.
    ///
    /// <para><b>The bug this is here for.</b> A <see cref="MaterialPropertyBlock"/> fixes the length
    /// of a vector array the first time it is set and silently caps every later set to that length
    /// — Unity says so in a warning and carries on. The renderer keeps <em>one</em> block for the
    /// solid half of a sight-partitioned bucket, so the first partitioned bucket of a session locked
    /// the length for all of them, and every larger bucket afterwards drew with colours that had
    /// never been written for it. Switching see-through on repainted the whole wood; switching it
    /// off put the wood back, because with nothing partitioned every tree draws from its own
    /// bucket's block. It was reported as a bug in the trees and it was never about the trees.</para>
    ///
    /// <para>The invariant is therefore not "the colours come back" but "the length never varies",
    /// and that is what is asserted: a short write and a long write through the same block both
    /// leave an array of the same size, and the long write's values are all there. Asserting only
    /// the values would pass on a block that had merely not been reused yet.</para>
    /// </summary>
    public class TreeColourBlockTests
    {
        static readonly int LeafDeepId = Shader.PropertyToID("_LeafDeepColour");

        /// <summary>A run of <paramref name="count"/> colours no two of which are alike.</summary>
        static List<Vector4> Colours(int count)
        {
            var values = new List<Vector4>(count);
            for (int i = 0; i < count; i++) values.Add(new Vector4(i / 255f, 0.25f, 0.5f, 1f));
            return values;
        }

        [Test]
        public void ABiggerBucketAfterASmallerOneKeepsItsOwnColours()
        {
            var props = new MaterialPropertyBlock();

            List<Vector4> small = Colours(25);
            ChunkRenderer.WritePadded(props, LeafDeepId, small);
            Assert.That(props.GetVectorArray(LeafDeepId).Length,
                Is.EqualTo(ChunkRenderer.MaxInstancesPerCall),
                "a short write did not pad to the ceiling, so the next one will be capped to it");

            // The sizes the warning in the field carried, so the numbers are the reported ones.
            List<Vector4> big = Colours(31);
            ChunkRenderer.WritePadded(props, LeafDeepId, big);

            Vector4[] read = props.GetVectorArray(LeafDeepId);
            Assert.That(read.Length, Is.EqualTo(ChunkRenderer.MaxInstancesPerCall),
                "the array length moved, so some write is being capped");
            for (int i = 0; i < 31; i++)
                Assert.That(read[i], Is.EqualTo(big[i]), $"instance {i} wears a colour it was not given");
        }

        /// <summary>
        /// The padding is inert: a draw of <c>n</c> instances reads the first <c>n</c> entries, so
        /// what sits past them must not disturb what sits before them.
        /// </summary>
        [Test]
        public void PaddingLeavesTheRealColoursAtTheFrontOfTheArray()
        {
            var props = new MaterialPropertyBlock();
            List<Vector4> values = Colours(3);
            ChunkRenderer.WritePadded(props, LeafDeepId, values);

            Vector4[] read = props.GetVectorArray(LeafDeepId);
            Assert.That(read[0], Is.EqualTo(new Vector4(0f, 0.25f, 0.5f, 1f)));
            Assert.That(read[1], Is.EqualTo(new Vector4(1 / 255f, 0.25f, 0.5f, 1f)));
            Assert.That(read[2], Is.EqualTo(new Vector4(2 / 255f, 0.25f, 0.5f, 1f)));
        }
    }
}
