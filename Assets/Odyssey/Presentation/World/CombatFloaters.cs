#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The words that float up off a fight — "miss", "dodge", the damage — as data: what each
    /// says, in what ink, where it started and how old it is (design 33 §1). What they say is
    /// <see cref="CombatFeedbackModel"/>'s; this only keeps them alive, lifts them and fades them,
    /// and the view (<c>Ui.CombatFloaterView</c>) puts each one on screen over its point.
    ///
    /// <para>Pure and Unity-light, so a test can step it without a panel. Aged on the frame's own
    /// seconds and stopped by a pause, like every ease in the figures.</para>
    ///
    /// <para><b>Bounded.</b> A fight of twenty against twenty swings about ten times a second; at
    /// <see cref="Seconds"/> each, that is a dozen alive at once, and <see cref="Capacity"/> drops
    /// the oldest past it so a brawl at speed three cannot grow the list without end.</para>
    /// </summary>
    public sealed class CombatFloaters
    {
        /// <summary>
        /// How long a word stays up, in seconds, when nobody says otherwise. The fight's own words
        /// are given theirs by <see cref="CombatFeedbackModel.FloatingSeconds"/> — a number is brief,
        /// "downed" and "dead" linger — so this is the fallback, not the rule (integration,
        /// 2026-09-23: lane B floated every word for this long and lane C had written the table).
        /// </summary>
        public const float Seconds = 1.2f;

        /// <summary>How far it rises over that time, in metres.</summary>
        public const float Rise = 0.9f;

        /// <summary>The last part of its life it spends fading, as a fraction.</summary>
        public const float FadeFrom = 0.6f;

        /// <summary>The most alive at once; the oldest goes first.</summary>
        public const int Capacity = 32;

        public struct Floater
        {
            public string Text;
            public HudColour Ink;
            public Vector3 From;
            public float Age;

            /// <summary>How long this word stays up, in seconds.</summary>
            public float Life;

            float T => Mathf.Clamp01(Age / (Life > 0f ? Life : Seconds));

            /// <summary>Where it is drawn now: risen from where it started, easing to a stop.</summary>
            public Vector3 At
            {
                get
                {
                    float t = T;
                    return From + Vector3.up * (Rise * (1f - (1f - t) * (1f - t)));
                }
            }

            /// <summary>How opaque it is now: whole, then fading out over its last part.</summary>
            public float Alpha
            {
                get
                {
                    float t = T;
                    return t <= FadeFrom ? 1f : 1f - (t - FadeFrom) / (1f - FadeFrom);
                }
            }
        }

        readonly List<Floater> _alive = new List<Floater>();

        /// <summary>The words alive now, oldest first.</summary>
        public IReadOnlyList<Floater> Alive => _alive;

        /// <summary>
        /// Put a word up at a point for <paramref name="seconds"/> (<see cref="Seconds"/> when
        /// nought or less). An empty word floats nothing.
        /// </summary>
        public void Add(string text, HudColour ink, Vector3 from, float seconds = Seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_alive.Count >= Capacity) _alive.RemoveAt(0);
            _alive.Add(new Floater { Text = text, Ink = ink, From = from, Age = 0f, Life = seconds > 0f ? seconds : Seconds });
        }

        /// <summary>Age every word by the frame's time and let go of the ones that are done.</summary>
        public void Step(float deltaTime)
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Floater floater = _alive[i];
                floater.Age += deltaTime;
                if (floater.Age >= floater.Life) _alive.RemoveAt(i);
                else _alive[i] = floater;
            }
        }

        /// <summary>Forget them all: a new world starts with a clear sky.</summary>
        public void Clear() => _alive.Clear();
    }
}
