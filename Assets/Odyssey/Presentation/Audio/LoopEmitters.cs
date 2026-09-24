#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// One thing that is making a continuous sound somewhere, as the director is told about it.
    ///
    /// <para><see cref="Key"/> is the thing's identity — a cell index for a campfire — and it is
    /// what makes a voice stay with the fire it started on. Positions alone would do for placing
    /// the sound and would be wrong the moment two fires swapped places in the caller's list: the
    /// voices would swap clips mid-crackle, which is a phase jump the ear hears as a click.</para>
    /// </summary>
    public readonly struct LoopPoint
    {
        public readonly int Key;
        public readonly Vector3 At;

        public LoopPoint(int key, Vector3 at)
        {
            Key = key;
            At = at;
        }
    }

    /// <summary>
    /// The third kind of emitter: a looping sound belonging to a thing at a place, which starts
    /// and stops with the thing.
    ///
    /// <para><b>Why the director had neither of its other two.</b> <c>SoundIds.Campfire</c> has
    /// carried the note since the clip was imported — the one-shot pool is for <i>moments</i>, and
    /// its voices are handed out for the length of a clip and taken back; the ambience beds are
    /// one-per-environment, 2D or measured from the world as a whole, and there is only ever one
    /// of each. A fire is neither: there can be six of them, they are in particular places, and
    /// each runs until somebody takes it apart.</para>
    ///
    /// <para><b>The cap keeps the nearest, not the first.</b> Six fires in a room and a cap of
    /// four has to drop two, and dropping the two that happen to be last in the caller's list
    /// means the silent ones are chosen by cell index — so walking up to a fire might leave it
    /// mute while one across the map sings. That is the fault CLAUDE.md records against
    /// <c>PawnFigureDirector.MaxFigures</c>, whose comment said the rest were "a long way off"
    /// while it was in fact keeping the highest pawn ids. Sorted by distance to the listener,
    /// every frame, and the sort is over a handful of items.</para>
    ///
    /// <para><b>Nothing here allocates per frame.</b> The caller owns its list and refills it; the
    /// pool reuses its voices and its scratch. A sound that plays forever must not cost a
    /// collection to keep playing.</para>
    /// </summary>
    public sealed class LoopEmitters
    {
        /// <summary>
        /// How many of one looping sound may play at once.
        ///
        /// <para>Four, because the point of more is nothing: the fifth fire in earshot adds
        /// another copy of the same crackle at a lower gain, and past about this many the result
        /// is a wash rather than several fires. It is also four voices not available to anything
        /// else, which is the real argument — the one-shot pool has sixteen.</para>
        /// </summary>
        public const int Cap = 4;

        readonly List<Voice> _voices = new List<Voice>(Cap);
        readonly List<LoopPoint> _nearest = new List<LoopPoint>(Cap);

        sealed class Voice
        {
            public AudioSource Source = null!;
            public int Key = -1;
        }

        /// <summary>How many are sounding. Diagnostic, and what a test counts.</summary>
        public int Sounding { get; private set; }

        /// <summary>
        /// Bring the voices into line with what is making the sound this frame.
        /// </summary>
        /// <param name="points">Everything of this kind that is audible, in any order. The caller
        /// owns the list and may refill it every frame.</param>
        /// <param name="listener">Where the ear is, for choosing the nearest.</param>
        /// <param name="make">Builds a voice on demand — the director's own <c>Voice()</c>, so a
        /// loop is spatialised by the same rolloff curve everything else uses.</param>
        /// <param name="apply">Sets the def's clip, gain, bus and ranges on a voice. Called once
        /// when a voice takes up a new thing, never per frame: assigning a clip restarts it.</param>
        public void Sync(IReadOnlyList<LoopPoint> points, Vector3 listener,
            System.Func<AudioSource> make, System.Action<AudioSource> apply)
        {
            Nearest(points, listener);

            // Voices whose thing is gone, or has fallen out of the nearest set, are freed first —
            // before anything is assigned, so a freed voice can be reused in the same frame by a
            // fire that has just come into range.
            for (int i = 0; i < _voices.Count; i++)
            {
                Voice voice = _voices[i];
                if (voice.Key < 0) continue;
                if (Holds(_nearest, voice.Key)) continue;

                voice.Source.Stop();
                voice.Key = -1;
            }

            for (int i = 0; i < _nearest.Count; i++)
            {
                LoopPoint point = _nearest[i];
                Voice? voice = Find(point.Key);

                if (voice == null)
                {
                    voice = Free() ?? Grow(make);
                    if (voice == null) break;      // capped; the rest are further away

                    voice.Key = point.Key;
                    apply(voice.Source);
                    voice.Source.loop = true;

                    // Start somewhere random in the clip. Two fires built in the same minute
                    // would otherwise run in lockstep, and two identical crackles in phase read
                    // as one loud fire with a doubled attack rather than as two.
                    AudioClip? clip = voice.Source.clip;
                    if (clip != null && clip.samples > 1)
                        voice.Source.timeSamples = Random.Range(0, clip.samples - 1);

                    voice.Source.Play();
                }

                // The position every frame, because the fire does not move but the ground under
                // a drawn cell can be re-draped, and because a voice reassigned above needs it.
                voice.Source.transform.position = point.At;
            }

            Sounding = 0;
            for (int i = 0; i < _voices.Count; i++) if (_voices[i].Key >= 0) Sounding++;
        }

        /// <summary>Stop everything — a session ending, or the last fire going out.</summary>
        public void StopAll()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Key < 0) continue;
                _voices[i].Source.Stop();
                _voices[i].Key = -1;
            }
            Sounding = 0;
        }

        /// <summary>The <see cref="Cap"/> closest to the listener, by insertion into a tiny list.
        /// A full sort would allocate a comparer and this is at most a handful of items.</summary>
        void Nearest(IReadOnlyList<LoopPoint> points, Vector3 listener)
        {
            _nearest.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                LoopPoint candidate = points[i];
                float distance = (candidate.At - listener).sqrMagnitude;

                int slot = _nearest.Count;
                while (slot > 0 && (_nearest[slot - 1].At - listener).sqrMagnitude > distance) slot--;

                if (slot >= Cap) continue;
                _nearest.Insert(slot, candidate);
                if (_nearest.Count > Cap) _nearest.RemoveAt(_nearest.Count - 1);
            }
        }

        static bool Holds(List<LoopPoint> points, int key)
        {
            for (int i = 0; i < points.Count; i++) if (points[i].Key == key) return true;
            return false;
        }

        Voice? Find(int key)
        {
            for (int i = 0; i < _voices.Count; i++) if (_voices[i].Key == key) return _voices[i];
            return null;
        }

        Voice? Free()
        {
            for (int i = 0; i < _voices.Count; i++) if (_voices[i].Key < 0) return _voices[i];
            return null;
        }

        Voice? Grow(System.Func<AudioSource> make)
        {
            if (_voices.Count >= Cap) return null;
            var voice = new Voice { Source = make(), Key = -1 };
            _voices.Add(voice);
            return voice;
        }
    }
}
