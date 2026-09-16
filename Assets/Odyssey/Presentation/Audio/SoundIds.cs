#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// The buses every sound routes through. The set is the mix: master carries everything, and
    /// the other four exist because they are the groups a player is separately allowed to turn
    /// down — music, the ambience of the world, one-shot effects, and alerts. An alert is its own
    /// bus and not an effect because it must always be audible: its bus starts at full volume and
    /// is the last thing a player turns down, not the first.
    /// </summary>
    public enum SoundBus
    {
        Master = 0,
        Music = 1,
        Ambience = 2,
        Effects = 3,
        Alerts = 4,
    }

    /// <summary>
    /// Every sound the game can name, as dotted constants in the house style of
    /// <c>ModuleIds</c> ("odyssey.module.…"). A sound id is not a label: nothing here is shown to
    /// a player, so these do not go through the naming registry, which exists for words on
    /// screen. The <see cref="AudioCatalogue"/> maps each id to clips and settings, the way
    /// <c>ModuleCatalogue</c> maps a module id to art.
    /// </summary>
    public static class SoundIds
    {
        public const string SoundPrefix = "odyssey.sound.";
        public const string AmbiencePrefix = "odyssey.ambience.";
        public const string MusicPrefix = "odyssey.music.";

        /// <summary>An axe biting wood: the felling stroke landing.</summary>
        public const string WorkChop = SoundPrefix + "work.chop";

        /// <summary>A pick striking stone: the mining stroke landing.</summary>
        public const string WorkPick = SoundPrefix + "work.pick";

        /// <summary>The chime for a colonist past starving. The one alert the snapshot can raise today.</summary>
        public const string AlertStarving = SoundPrefix + "alert.starving";

        /// <summary>
        /// A campfire burning. **In the library, not yet in the game.**
        ///
        /// <para>The clip and its catalogue row are here so the sound exists the day the fire
        /// does. Nothing plays it, and nothing can yet: a fire is a <i>looping sound belonging to
        /// a thing at a place</i>, which is a third kind of emitter the director does not have —
        /// the one-shot pool is for moments, and the beds are one-per-environment measured from
        /// the world. What is missing is an emitter that follows a thing and starts and stops
        /// with it, which is a job for whoever builds fires, not a guess made in advance.</para>
        /// </summary>
        public const string Campfire = SoundPrefix + "campfire";

        /// <summary>Looping water: ponds, streams and the river, scaled by how much of it is near.</summary>
        public const string AmbienceWater = AmbiencePrefix + "water";

        /// <summary>
        /// The sound of the world outdoors by day — the bed under everything else, birds and air
        /// and distance. Unlike the water bed it is not measured from anything: being outdoors is
        /// not a quantity, it is where you are, so it plays flat whenever the slice is at or
        /// above the surface and stops when it is not.
        /// </summary>
        public const string AmbienceOutdoorDay = AmbiencePrefix + "outdoor.day";

        /// <summary>The same, after dark. A different world rather than a quieter one: the day's
        /// birds are gone and something else has started.</summary>
        public const string AmbienceOutdoorNight = AmbiencePrefix + "outdoor.night";

        /// <summary>The daytime track.</summary>
        public const string MusicDay = MusicPrefix + "day";

        /// <summary>The night-time track.</summary>
        public const string MusicNight = MusicPrefix + "night";

        /// <summary>Every id the shipped catalogue defines, for a generator or a test to walk.</summary>
        public static readonly string[] All =
        {
            WorkChop, WorkPick, AlertStarving, Campfire,
            AmbienceWater, AmbienceOutdoorDay, AmbienceOutdoorNight,
            MusicDay, MusicNight,
        };

        /// <summary>
        /// The sound a blow makes, from the work style the figure director already resolved.
        /// Mining is the only style with its own impact sound today; everything else swings an
        /// axe, exactly as <c>WorkStyle.IndexForJob</c> decides for the pose.
        /// </summary>
        public static string ForBlow(int workStyleIndex) =>
            workStyleIndex == World.WorkStyle.MiningIndex ? WorkPick : WorkChop;
    }

    /// <summary>
    /// Which track the clock wants. Two phases rather than four because that is what a prototype
    /// can ship placeholder music for and because a day/night split is the whole audible
    /// difference at this scale; dawn and dusk crossfades are a constant away when there is music
    /// worth crossing to.
    /// </summary>
    public enum MusicPhase
    {
        None = 0,
        Day = 1,
        Night = 2,
    }

    /// <summary>
    /// The tick, read through <see cref="Hud.GameClock"/>, as a music phase.
    ///
    /// The simulation counts ticks and nothing else; <c>GameClock</c> is the one place ticks
    /// become hours, and this is the one place hours become music. Kept as its own pure step so
    /// the boundaries are testable without an audio source in sight.
    /// </summary>
    public static class MusicClock
    {
        /// <summary>The hour day begins, inclusive.</summary>
        public const int DayStartHour = 6;

        /// <summary>The hour night begins, inclusive.</summary>
        public const int NightStartHour = 19;

        public static MusicPhase PhaseOf(long tick)
        {
            int hour = Hud.GameClock.HourOfDay(tick);
            return hour >= DayStartHour && hour < NightStartHour ? MusicPhase.Day : MusicPhase.Night;
        }
    }

    /// <summary>The alerts audio can raise. One exists today; the list grows when the alert
    /// system (design 10, region A5) does, and the chime ids land beside it in
    /// <see cref="SoundIds"/>.</summary>
    [Flags]
    public enum AudioAlert
    {
        None = 0,

        /// <summary>A colonist's food need has crossed into starvation.</summary>
        Starving = 1 << 0,
    }

    /// <summary>
    /// Watches the published pawn list and raises an <see cref="AudioAlert"/> the moment a need
    /// crosses its threshold — once, not every frame, and not again until the need has recovered
    /// past a re-arm point.
    ///
    /// The hysteresis is the whole point of the class. A need sitting at the threshold flaps
    /// either side of it for hours of game time, and a chime per flap is worse than no warning at
    /// all; the re-arm band (recover to <see cref="RearmAbove"/> before the crossing can fire
    /// again) is how thermostats solve the same problem.
    ///
    /// Reads only the published <see cref="Sim.Contracts.PawnView"/> list, the way ADR 0004 says
    /// presentation learns anything: no sim object is touched, and a fixed tick with a fixed pawn
    /// list raises exactly the same alert in a test as in the game.
    /// </summary>
    public sealed class AlertWatch
    {
        /// <summary>
        /// Food, in the published 0–100 units, at which the starving chime fires. 12% is deep
        /// hunger rather than peckishness — by then the colonist has been visibly hungry on the
        /// roster for a long while and the chime is the "now it matters" signal.
        /// </summary>
        public const int StarveThreshold = 12;

        /// <summary>
        /// Food a colonist must recover to before the starving alert can fire for them again.
        /// A wide band, because eating one meal clears the condition outright and the only thing
        /// the band really guards against is a need oscillating around the threshold.
        /// </summary>
        public const int RearmAbove = 30;

        readonly List<(int Pawn, bool Raised)> _raised = new();

        /// <summary>
        /// Step over one published frame and return any alerts that fire on it. Several colonists
        /// can cross at once; they chime once between them, because one sound carrying "somebody
        /// is starving" is the message, and the roster is what says who.
        /// </summary>
        public AudioAlert Step(System.ReadOnlySpan<Sim.Contracts.PawnView> pawns)
        {
            AudioAlert fired = AudioAlert.None;

            for (int i = 0; i < pawns.Length; i++)
            {
                int pawn = pawns[i].Id.Value;
                bool already = Raised(pawn);

                if (pawns[i].Food <= StarveThreshold)
                {
                    SetRaised(pawn, true);
                    if (!already) fired |= AudioAlert.Starving;
                }
                else if (already && pawns[i].Food >= RearmAbove)
                {
                    SetRaised(pawn, false);
                }
            }

            return fired;
        }

        bool Raised(int pawn)
        {
            for (int i = 0; i < _raised.Count; i++)
                if (_raised[i].Pawn == pawn) return _raised[i].Raised;
            return false;
        }

        void SetRaised(int pawn, bool raised)
        {
            for (int i = 0; i < _raised.Count; i++)
            {
                if (_raised[i].Pawn != pawn) continue;
                _raised[i] = (pawn, raised);
                return;
            }
            _raised.Add((pawn, raised));
        }
    }
}
