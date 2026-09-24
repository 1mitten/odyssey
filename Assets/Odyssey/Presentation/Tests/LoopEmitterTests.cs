#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Odyssey.Presentation.Audio;

namespace Odyssey.Presentation.Tests
{
    /// <summary>
    /// <see cref="LoopEmitters"/>: the third kind of emitter — a looping sound belonging to a
    /// thing at a place (docs/reference/audio-sourcing.md, <c>SoundIds.Campfire</c>).
    ///
    /// <para>The pool is exercised directly rather than through <c>AudioDirector</c> so that the
    /// choosing can be asserted without a catalogue, a clip or a scene. What it chooses is the
    /// whole of its behaviour.</para>
    /// </summary>
    public class LoopEmitterTests
    {
        GameObject _host = null!;
        readonly List<AudioSource> _made = new List<AudioSource>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("LoopEmitterTests");
            _made.Clear();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        AudioSource Make()
        {
            var holder = new GameObject($"voice {_made.Count}");
            holder.transform.SetParent(_host.transform, worldPositionStays: false);
            AudioSource source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _made.Add(source);
            return source;
        }

        static void Apply(AudioSource source) => source.volume = 1f;

        static LoopPoint At(int key, float x) => new LoopPoint(key, new Vector3(x, 0f, 0f));

        /// <summary>A voice each, up to the cap, and none for nothing.</summary>
        [Test]
        public void OneVoicePerThingUpToTheCap()
        {
            var pool = new LoopEmitters();
            var points = new List<LoopPoint>();

            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(pool.Sounding, Is.EqualTo(0), "nothing to hear and a voice was spent");

            for (int i = 0; i < LoopEmitters.Cap; i++) points.Add(At(i, i * 3f));
            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(pool.Sounding, Is.EqualTo(LoopEmitters.Cap));
            Assert.That(_made.Count, Is.EqualTo(LoopEmitters.Cap));

            // Past the cap nothing new is built, which is the point of a cap.
            for (int i = 0; i < 6; i++) points.Add(At(100 + i, 50f + i));
            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(pool.Sounding, Is.EqualTo(LoopEmitters.Cap));
            Assert.That(_made.Count, Is.EqualTo(LoopEmitters.Cap), "the cap built more voices");
        }

        /// <summary>
        /// The cap keeps the <b>nearest</b>, not the first it was handed.
        ///
        /// <para><b>This is the one that matters and the one that is easy to get wrong.</b> A
        /// budget applied in arrival order is a budget on identity, not on distance: the fires
        /// that fall silent would be chosen by cell index, so walking up to one could leave it
        /// mute while another across the map sang. CLAUDE.md records exactly that against
        /// <c>PawnFigureDirector.MaxFigures</c>, whose own comment claimed the ones it dropped
        /// were "a long way off" while it was in fact dropping the highest pawn ids.</para>
        /// </summary>
        [Test]
        public void TheCapKeepsTheNearestAndNotTheFirst()
        {
            var pool = new LoopEmitters();

            // Declared far-to-near on purpose, so "the first Cap of them" is the wrong answer.
            var points = new List<LoopPoint>();
            for (int i = 0; i < 8; i++) points.Add(At(i, 100f - i * 10f));

            pool.Sync(points, Vector3.zero, Make, Apply);

            Assert.That(pool.Sounding, Is.EqualTo(LoopEmitters.Cap));

            // Keys 7,6,5,4 are at x = 30,40,50,60 — the four nearest the listener at the origin.
            var sounding = new List<float>();
            foreach (AudioSource source in _made)
                if (source.isPlaying || source.clip == null) sounding.Add(source.transform.position.x);

            for (int i = 0; i < _made.Count; i++)
                Assert.That(Mathf.Abs(_made[i].transform.position.x), Is.LessThanOrEqualTo(60f),
                    "a voice is on a far thing while a nearer one is silent — the cap is being " +
                    "spent in arrival order rather than on distance");
        }

        /// <summary>
        /// A thing that goes away takes its voice with it, and the voice is reused rather than
        /// left running on a fire that no longer exists.
        /// </summary>
        [Test]
        public void AThingThatGoesAwayStopsSounding()
        {
            var pool = new LoopEmitters();
            var points = new List<LoopPoint> { At(1, 1f), At(2, 2f) };

            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(pool.Sounding, Is.EqualTo(2));

            points.RemoveAt(1);
            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(pool.Sounding, Is.EqualTo(1), "the removed thing is still sounding");

            points.Clear();
            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(pool.Sounding, Is.EqualTo(0));

            // And no voice was built for the churn — the pool reuses what it has.
            Assert.That(_made.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// A voice stays with the thing it started on.
        ///
        /// <para>Identity is why <see cref="LoopPoint.Key"/> exists. Assigning voices by position
        /// alone would swap them between two fires the moment the caller's list reordered, and
        /// re-assigning a voice restarts its clip — a phase jump mid-crackle, which is a click.
        /// </para>
        /// </summary>
        [Test]
        public void AVoiceStaysWithItsOwnThingWhenTheListReorders()
        {
            var pool = new LoopEmitters();
            var points = new List<LoopPoint> { At(1, 1f), At(2, 2f) };

            pool.Sync(points, Vector3.zero, Make, Apply);
            Assert.That(_made.Count, Is.EqualTo(2));

            AudioSource first = _made[0];
            Vector3 was = first.transform.position;

            // Same two things, handed over the other way round.
            points.Clear();
            points.Add(At(2, 2f));
            points.Add(At(1, 1f));
            pool.Sync(points, Vector3.zero, Make, Apply);

            Assert.That(_made.Count, Is.EqualTo(2), "a reorder built new voices");
            Assert.That(first.transform.position, Is.EqualTo(was),
                "the first voice moved to the other thing when the list reordered, which restarts " +
                "its clip mid-crackle");
        }
    }
}
