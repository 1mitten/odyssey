#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// What comes off a material when a tool bites it: how much, how big, how fast, how long it
    /// lasts and what colour it is.
    ///
    /// **Why this is data and not three more directors.** Wood chips off an axe and stone chips
    /// off a pick are the same effect with different numbers. Every part of a burst that differs
    /// between them — colour, size, speed, lifetime, count, spread — is settable per particle at
    /// the moment of emission, so one <see cref="ChipDirector"/> with one particle system and one
    /// material serves every material in the game. Adding mining costs a recipe, not a system, and
    /// not a draw call: a second system would double the draw calls for a handful of quads.
    ///
    /// The one thing that is *not* per particle is gravity, which is a property of the system as a
    /// whole and applies to every chip in flight. That is a real limit and it is accepted rather
    /// than worked around: heavier debris is expressed by leaving the cut slower and dying sooner,
    /// which at board-camera height reads the same and costs nothing.
    ///
    /// **To add a material:** write a preset here, hand it to <see cref="ChipDirector.Throw"/> from
    /// whatever director knows the moment the tool lands, and that is the whole of it. The moment
    /// itself is the hard part and it belongs to the caller — for felling it is
    /// <see cref="WorkSwing.Lands"/>, which is arithmetic over the stroke phase and is the pattern
    /// to copy.
    /// </summary>
    public readonly struct ChipRecipe
    {
        /// <summary>How many pieces come off one blow.</summary>
        public readonly int Count;

        /// <summary>How fast they leave the cut, in metres per second: least, most.</summary>
        public readonly Vector2 Speed;

        /// <summary>How big a piece is, in metres: least, most.</summary>
        public readonly Vector2 Size;

        /// <summary>How long a piece lasts before it is gone, in seconds: least, most.</summary>
        public readonly Vector2 Life;

        /// <summary>How wide the spray is, in degrees either side of straight out of the cut.</summary>
        public readonly float Spread;

        /// <summary>
        /// The colours a piece may be, chosen per particle.
        ///
        /// Three or so, and deliberately not a gradient: one flat colour reads as a sprite sheet
        /// and a smooth ramp reads as smoke. A few discrete tones read as bits of something.
        /// </summary>
        public readonly Color[] Colours;

        public ChipRecipe(int count, Vector2 speed, Vector2 size, Vector2 life, float spread, Color[] colours)
        {
            Count = count;
            Speed = speed;
            Size = size;
            Life = life;
            Spread = spread;
            Colours = colours;
        }

        /// <summary>True when this recipe is filled in at all. A default one throws nothing.</summary>
        public bool IsSomething => Count > 0 && Colours != null && Colours.Length > 0;

        /// <summary>
        /// Wood off a felling axe: sawn timber, weathered bark and something between.
        ///
        /// Light, so it carries and hangs: a chip of wood leaves the cut fast and takes its time
        /// coming down.
        /// </summary>
        public static readonly ChipRecipe Wood = new ChipRecipe(
            count: 9,
            speed: new Vector2(0.9f, 2.3f),
            size: new Vector2(0.03f, 0.075f),
            life: new Vector2(0.35f, 0.75f),
            spread: 38f,
            colours: new[]
            {
                new Color(0.62f, 0.47f, 0.28f),
                new Color(0.45f, 0.33f, 0.20f),
                new Color(0.73f, 0.60f, 0.40f),
            });

        /// <summary>
        /// Stone off a pick, for when mining arrives. Not yet called by anything.
        ///
        /// Heavier than wood and it shows in the numbers rather than in the gravity, which the
        /// system shares: more pieces, smaller, thrown harder and gone sooner, in a tighter cone
        /// because rock shatters where it is struck rather than peeling away in shavings. Greys
        /// with a little warmth, because a pure grey against grey rock is invisible.
        /// </summary>
        public static readonly ChipRecipe Stone = new ChipRecipe(
            count: 12,
            speed: new Vector2(1.4f, 3.1f),
            size: new Vector2(0.02f, 0.05f),
            life: new Vector2(0.25f, 0.5f),
            spread: 26f,
            colours: new[]
            {
                new Color(0.55f, 0.54f, 0.52f),
                new Color(0.40f, 0.39f, 0.38f),
                new Color(0.68f, 0.66f, 0.62f),
            });
    }
}
