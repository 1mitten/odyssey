#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The one writer of the wind the grass reads.
    ///
    /// <para><b>Driven by the tick, not by wall time.</b> A shader can read <c>_Time</c> for
    /// nothing, and it would have been wrong twice over. A paused game holds the frame it is on
    /// (<c>06-rendering-and-camera.md</c> §6b) and a meadow that goes on rippling through a pause
    /// says the world is still running; and at speed 3 everything else in the world moves three
    /// times as fast, so wind that did not would read as the grass being in a different game from
    /// the colonists walking through it. Taking the phase from the tick gives both for free, and
    /// makes the wind the same on two machines at the same tick, which is what lets a contact
    /// sheet be compared with another one.</para>
    ///
    /// <para><b>Nothing here is simulation.</b> The wind is a pure function of the tick: not
    /// saved, not hashed, and unreadable from the simulation, exactly as <see cref="Daylight"/>
    /// is. If weather ever moves a colonist or a fire, that is a simulation feature with its own
    /// grid and its own tests, and this becomes its reader.</para>
    ///
    /// <para><b>All-zero is a valid state.</b> The globals start unset, and unset means still air
    /// — grass drawn before anything calls <see cref="Apply"/> stands up straight rather than
    /// vanishing. That matters because several things draw the world without a bootstrap: the
    /// editor contact sheets, the frame-time harness, and the first frame of a session.</para>
    /// </summary>
    public sealed class WindDirector
    {
        static readonly int WindId = Shader.PropertyToID("_OdysseyWind");
        static readonly int WavelengthId = Shader.PropertyToID("_OdysseyWindWavelength");

        /// <summary>
        /// How far the wind bows a blade at its tip, <b>in radians</b>.
        ///
        /// <para>It used to be a fraction of the blade's length, because the shader dragged the
        /// tip sideways. The shader rotates the blade about its root now, so the honest unit is
        /// an angle: 0.45 is about 26 degrees at full gust, and the gust and swell terms spend
        /// most of their time well below it.</para>
        ///
        /// <para>Past about 0.6 the shader's own cap takes over, at which point the tall blades
        /// stop moving before the short ones do — which does not read as a clamp, it reads as
        /// the grass being broken.</para>
        /// </summary>
        public float Strength { get; set; } = 0.45f;

        /// <summary>
        /// How many ticks one gust takes to pass. 180 is three seconds at speed 1.
        ///
        /// Slower reads as a swell and faster as a shiver; three seconds is about where a field
        /// of grass stops looking like either and starts looking like weather.
        /// </summary>
        public float TicksPerGust { get; set; } = 180f;

        /// <summary>
        /// How quickly the gust wave crosses the ground, in radians a metre.
        ///
        /// This is the setting that decides whether the meadow breathes as one thing or a gust
        /// visibly travels across it. At 0.08 a full wave is about 78 m — a third of the board,
        /// so two or three crests are in view at once, which is what reads as wind rather than as
        /// a pulse.
        /// </summary>
        public float Wavelength { get; set; } = 0.08f;

        /// <summary>
        /// How long the wind takes to box the compass, in ticks. Ten days, so it never looks like
        /// it is turning and never looks stuck either.
        /// </summary>
        public float TicksPerRevolution { get; set; } = GameClock.TicksPerDay * 10f;

        /// <summary>Where the wind was blowing at tick zero, in degrees clockwise from +x.</summary>
        public float StartBearingDegrees { get; set; } = 35f;

        /// <summary>The phase last written, in radians. For tests and the debug readout.</summary>
        public float Phase { get; private set; }

        /// <summary>The direction last written, times the strength. For tests.</summary>
        public Vector3 Push { get; private set; }

        /// <summary>Write the wind for this tick. Cheap enough to call every frame, and it is.</summary>
        public void Apply(long tick)
        {
            float bearing = (StartBearingDegrees + tick * (360f / Mathf.Max(1f, TicksPerRevolution)))
                * Mathf.Deg2Rad;
            Phase = tick * (Mathf.PI * 2f / Mathf.Max(1f, TicksPerGust));
            Push = new Vector3(Mathf.Cos(bearing), 0f, Mathf.Sin(bearing)) * Strength;

            Shader.SetGlobalVector(WindId, new Vector4(Push.x, Push.y, Push.z, Phase));
            Shader.SetGlobalFloat(WavelengthId, Wavelength);
        }

        /// <summary>
        /// Still air, and put back exactly as it was found.
        ///
        /// A global outlives the thing that set it, so a session that ends with a gale leaves the
        /// next picture taken in this editor — a portrait, a contact sheet, an asset preview —
        /// with grass leaning in a wind nothing is blowing.
        /// </summary>
        public void Dispose()
        {
            Phase = 0f;
            Push = Vector3.zero;
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalFloat(WavelengthId, 0f);
        }
    }
}
