#nullable enable
using System.Collections;
using NUnit.Framework;
using Odyssey.Presentation.Bootstrap;

using UnityEngine;
using UnityEngine.TestTools;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// A world can be built and put down at run time, and putting one down leaves nothing behind
    /// (U35, the session seam).
    ///
    /// <para><b>Why this is the unit the menu waits on.</b> Until now a world existed because
    /// <c>Start</c> made one and stopped existing because the scene closed. A main screen needs
    /// both halves on demand: build when the player presses New Game, put down when they leave,
    /// build again without reloading the scene. `U38`–`U40` are all blocked on it.</para>
    ///
    /// <para><b>The two things that can go wrong are different in kind.</b> A second world can be
    /// *wrong* — carrying state the first one left behind, so a seed no longer determines a world
    /// — and that is what the hash test below catches. Or it can be *expensive*: correct in every
    /// observable way while the first world's meshes, figures and animation graphs sit in memory
    /// for the rest of the session. Nothing a player or a test looks at would notice the second,
    /// which is why it is checked by counting rather than by looking.</para>
    /// </summary>
    public class SessionSeamTests
    {
        /// <summary>
        /// Build, put down, build again — and the second world must be the world a fresh process
        /// would have made from the same seed, not merely a world that runs.
        ///
        /// <para><b>Every hash is taken at the same age.</b> The first version of this test read
        /// one hash after a timed warm-up and the other after a different timed warm-up, and the
        /// tick counter is *in* the hash — so it compared two worlds of different ages and called
        /// the difference a leak. Each hash here is read immediately after an explicit
        /// <c>BuildSession</c>, with no frame in between, so all three worlds are at the identical
        /// point in their lives.</para>
        ///
        /// <para>The hash is the full one, over the whole colony including the board, because the
        /// world joined the state hash on the same day this was written (OQ-50). Before that, this
        /// could have passed while the second build produced a different map.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ARebuiltWorldIsTheSameWorldAsAFreshOne()
        {
            GameObject first = RigWorld.Build(out OdysseyBootstrap boot, out _);
            yield return RigWorld.WarmUp();

            Assert.That(boot.HasSession, Is.True, "Play did not land in a world; buildOnPlay is the flag for that");

            // Down to a known state, so what follows is a build rather than the tail of Start's.
            boot.TeardownSession();
            Assert.That(boot.HasSession, Is.False, "a torn-down bootstrap still claims a session");
            Assert.That(boot.World, Is.Null, "the world reference outlived the teardown");

            boot.BuildSession();
            ulong once = boot.Colony!.World.ComputeStateHash().Value;

            boot.TeardownSession();
            boot.BuildSession();
            ulong again = boot.Colony!.World.ComputeStateHash().Value;

            Object.Destroy(first);
            yield return null;

            // A second rig, from the same seed, to say what the number ought to be. Comparing the
            // rebuild only against the first build would pass if both were wrong in the same way,
            // which is exactly what a leaked static does.
            GameObject fresh = RigWorld.Build(out OdysseyBootstrap other, out _);
            yield return RigWorld.WarmUp();
            other.TeardownSession();
            other.BuildSession();
            ulong elsewhere = other.Colony!.World.ComputeStateHash().Value;
            Object.Destroy(fresh);

            Assert.That(again, Is.EqualTo(once),
                "rebuilding after a teardown produced a different world from the same seed");
            Assert.That(again, Is.EqualTo(elsewhere),
                "the rebuilt world differs from one built in a fresh rig, so something survived the teardown");
        }

        /// <summary>
        /// The expensive failure: a second world that is correct in every observable way and twice
        /// the size in memory.
        ///
        /// <para><b>Meshes, not figures.</b> The first version of this counted colonist figures and
        /// found none — the test rig passes no module catalogue, so no character prefab is ever
        /// instantiated and the assertion was vacuous. Meshes are the right target anyway: a
        /// <c>ModuleLibrary</c> bakes one per module, primitives included when there is no art, and
        /// <c>OdysseyBootstrap.TeardownSession</c>'s own comment records that forgetting them
        /// "leaked the whole cast, every session, until the graphics device was reset out from
        /// under the editor". That is the failure this counts.</para>
        ///
        /// <para>A delta against a baseline rather than an absolute: an editor process holds
        /// thousands of unrelated meshes, and the question is only whether a session gives back
        /// what it took.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator PuttingAWorldDownGivesBackTheMeshesItTook()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out _);
            yield return RigWorld.WarmUp();

            boot.TeardownSession();
            yield return null;
            int baseline = MeshCount();

            boot.BuildSession();
            yield return RigWorld.WarmUp();
            int withAWorld = MeshCount();

            TestContext.WriteLine($"meshes {baseline} -> {withAWorld} with a world built");

            // Measured, and the answer was 45 -> 45. With no module catalogue the library resolves
            // every module to one of Unity's built-in primitives, which already exist and are
            // shared, so a session bakes nothing and there is no allocation to give back. The leak
            // this test is named for needs a catalogue to appear at all.
            //
            // **And it may not have one.** A test that required the licensed packs would break the
            // rule that a clone without them still builds and runs, so this cannot simply be fixed
            // by pointing the rig at the real catalogue. It ignores instead of passing, because a
            // green tick here would claim coverage of exactly the failure it cannot see.
            if (withAWorld <= baseline)
                Assert.Ignore(
                    "this rig has no module catalogue, so the library bakes no meshes and a mesh " +
                    "leak cannot appear. Run with a catalogue to make this meaningful; do not make " +
                    "the test depend on the licensed packs to get one.");

            boot.TeardownSession();
            yield return null;
            int afterwards = MeshCount();

            // Slack, not equality: a frame of editor churn either side is not a leak, and the thing
            // being caught is a whole library's worth — one per module, tens to hundreds.
            int took = withAWorld - baseline;
            int kept = afterwards - baseline;
            Assert.That(kept, Is.LessThan(took / 4),
                $"the session took {took} meshes and gave back only {took - kept}; a library that " +
                "outlives its world is the leak TeardownSession exists to prevent");

            Object.Destroy(root);
        }

        /// <summary>
        /// Teardown is reached from a menu unwinding and from a scene closing, and they can arrive
        /// in either order, so it has to be safe with no session and safe twice.
        /// </summary>
        [UnityTest]
        public IEnumerator TearingDownTwiceOrWithNoWorldIsHarmless()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out _);
            yield return RigWorld.WarmUp();

            boot.TeardownSession();
            boot.TeardownSession();
            Assert.That(boot.HasSession, Is.False);

            Object.Destroy(root);
            yield return null;
        }

        /// <summary>
        /// And building over a live session is refused rather than silently doubling it — the
        /// failure that would leak a whole world and show up as memory rather than as a bug.
        /// </summary>
        [UnityTest]
        public IEnumerator BuildingOverALiveSessionIsRefused()
        {
            GameObject root = RigWorld.Build(out OdysseyBootstrap boot, out _);
            yield return RigWorld.WarmUp();

            Assert.That(() => boot.BuildSession(), Throws.InvalidOperationException,
                "a second build over a live session would leak the first world's meshes and figures");

            Object.Destroy(root);
            yield return null;
        }

        /// <summary>
        /// Every mesh the process is holding, loaded or not. Absolute numbers mean nothing here —
        /// an editor holds thousands — so callers compare it with itself.
        /// </summary>
        static int MeshCount() => Count<Mesh>();

        static int Count<T>() where T : Object => Resources.FindObjectsOfTypeAll<T>().Length;

    }
}
