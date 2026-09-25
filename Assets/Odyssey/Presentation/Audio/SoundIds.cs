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

        /// <summary>
        /// A load coming up off the ground and into a colonist's arms. The lighter, quicker,
        /// brighter half of one recording — see <c>tools/audio/bake_carry.sh</c> for why the two
        /// ends of a carry are the same sound resampled rather than two sounds.
        /// </summary>
        public const string CarryLift = SoundPrefix + "carry.lift";

        /// <summary>
        /// A load going back down out of them: onto a stockpile, into a building site, or
        /// wherever an interrupted haul set it. The heavier, slower, duller half.
        /// </summary>
        public const string CarryDrop = SoundPrefix + "carry.drop";

        /// <summary>
        /// A blade drawn: a colonist has been drafted (design 33 §2i). 2D — it confirms an order
        /// the player gave, wherever the camera is — and played once on the frame the snapshot
        /// first shows a colonist drafted, never on the key press, so a refused draft is silent.
        /// Baked by <c>tools/audio/bake_draft.sh</c> from the owner's recording.
        /// </summary>
        public const string Draft = SoundPrefix + "draft";

        /// <summary>
        /// A weapon through the air — the whoosh (design 33 §9g): every swing of a bat, a crowbar,
        /// a machete or an arc blade, hit or miss; fists and bites are silent. Two takes,
        /// <c>combat-whoosh.wav</c> and <c>combat-whoosh_01.wav</c>, picked at random per swing.
        /// <b>Scheduled, not played on its event's frame</b>: <see cref="Odyssey.Hud.CombatSoundTiming"/>
        /// starts it so its loudest moment is a tenth of a second before the blow connects.
        /// </summary>
        public const string CombatSwing = SoundPrefix + "combat.swing";

        /// <summary>
        /// A blow landing on somebody — the thud (design 33 §9g): every landed hit, any weapon,
        /// fists and bites too, and under the slice on a critical. Played on the hit's own frame;
        /// <c>combat-hit.wav</c> is trimmed so its transient is on its first sample.
        /// </summary>
        public const string CombatHit = SoundPrefix + "combat.hit";

        /// <summary>
        /// A sharp weapon's critical — the slice (design 33 §9g): a machete or an arc blade whose
        /// blow, rolled as the wind-up starts, is critical (<c>CombatEventKind.SwingCritical</c>).
        /// Played <b>instead of</b> the whoosh and scheduled so its loudest moment lands on the
        /// impact; a blunt critical keeps the whoosh. <c>combat-crit-slice.wav</c>.
        /// </summary>
        public const string CombatCritSlice = SoundPrefix + "combat.crit-slice";

        /// <summary>A blow meeting nothing — a miss or a dodge. Named, and in no catalogue yet.</summary>
        public const string CombatMiss = SoundPrefix + "combat.miss";

        /// <summary>Somebody going down. Named, and in no catalogue yet.</summary>
        public const string CombatDown = SoundPrefix + "combat.down";

        /// <summary>Somebody dying. Named, and in no catalogue yet.</summary>
        public const string CombatDeath = SoundPrefix + "combat.death";

        /// <summary>
        /// The sound a moment of a fight makes <b>on its own frame</b>, or null for one that makes
        /// none: a stun is the blow that caused it and is already heard, and getting up is quiet.
        /// A swing is null here because its sound is not on its frame — the whoosh or the slice
        /// is scheduled against the blow (<see cref="Odyssey.Hud.CombatSoundSchedule"/>).
        /// </summary>
        public static string? ForCombat(Odyssey.Sim.Contracts.CombatEventKind kind) => kind switch
        {
            Odyssey.Sim.Contracts.CombatEventKind.Hit => CombatHit,
            Odyssey.Sim.Contracts.CombatEventKind.Miss => CombatMiss,
            Odyssey.Sim.Contracts.CombatEventKind.Dodge => CombatMiss,
            Odyssey.Sim.Contracts.CombatEventKind.Downed => CombatDown,
            Odyssey.Sim.Contracts.CombatEventKind.Died => CombatDeath,
            _ => null,
        };

        /// <summary>The sound a cue of a blow plays (design 33 §9g), or null for none.</summary>
        public static string? ForCue(Odyssey.Hud.CombatCue cue) => cue switch
        {
            Odyssey.Hud.CombatCue.Whoosh => CombatSwing,
            Odyssey.Hud.CombatCue.Slice => CombatCritSlice,
            Odyssey.Hud.CombatCue.Thud => CombatHit,
            _ => null,
        };

        /// <summary>
        /// A sliding door opening. <b>Named, and in no catalogue yet</b>:
        /// the director declines a sound it has no clip for, so this plays the day the owner adds
        /// the row and not before.
        /// </summary>
        public const string DoorOpen = SoundPrefix + "door.open";

        /// <summary>
        /// A sliding door closing. <b>Named, and in no catalogue yet</b>:
        /// the director declines a sound it has no clip for, so this plays the day the owner adds
        /// the row and not before.
        /// </summary>
        public const string DoorClose = SoundPrefix + "door.close";

        /// <summary>
        /// A supply drop hitting the ground (design 23 §6). <b>Named, and in no catalogue yet</b>:
        /// the director declines a sound it has no clip for, so this plays the day the owner adds
        /// the row and not before. Raised by <see cref="AudioDirector"/> itself, from the
        /// published frame, on the first frame a thing that was in the air is not.
        /// </summary>
        public const string DropLand = SoundPrefix + "drop.land";

        /// <summary>
        /// The neutral chime: something has happened that is worth a glance and is nobody's
        /// emergency. What an <see cref="Hud.AlertSeverity.Notice"/> row sounds like.
        /// </summary>
        public const string AlertNormal = SoundPrefix + "alert.normal";

        /// <summary>
        /// The chime for a colonist starving, breaking, cold, hurt — anything the player is
        /// being asked to go and fix. What a <see cref="Hud.AlertSeverity.Warning"/> or
        /// <see cref="Hud.AlertSeverity.Danger"/> row sounds like.
        /// </summary>
        public const string AlertNegative = SoundPrefix + "alert.negative";

        /// <summary>
        /// The chime for something going right. <b>In the library, played by nothing.</b>
        ///
        /// <para>There is no <c>Good</c> severity on <see cref="Hud.AlertRow"/> and no event
        /// that would carry one: a visitor arriving, a trade closing and a research project
        /// finishing are all things the game does not have yet. The clip is imported, mixed and
        /// named so that the day one of them lands the sound is already there and the work is
        /// whatever raises it — the same bargain <see cref="Campfire"/> makes.</para>
        /// </summary>
        public const string AlertHappy = SoundPrefix + "alert.happy";

        /// <summary>
        /// A colonist has joined the colony. <b>In the library, played by nothing</b> — a pawn
        /// arriving from outside is not a thing that happens yet. See <see cref="AlertHappy"/>
        /// for why the row exists in advance.
        /// </summary>
        public const string AlertJoined = SoundPrefix + "alert.joined";

        /// <summary>
        /// A raid. <b>In the library, and the one unplayed chime that is already wired</b>:
        /// <see cref="AlertChime.RaidKey"/> is declared in <c>icon-keys.csv</c>, so the moment
        /// something raises an alert row under that key this sound plays with no code change.
        /// </summary>
        public const string AlertRaid = SoundPrefix + "alert.raid";

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
        /// Light rain: drips and patter over a soft hiss, the owner's gentle-rain recording
        /// (<c>tools/audio/bake_rain.sh</c>). The drizzle end of rain, and kept faintly under a
        /// downpour for its close detail. How loud it is is <see cref="RainMix"/>'s, read off the
        /// published sky, never the clip's (design 43 §7).
        /// </summary>
        public const string AmbienceRainLight = AmbiencePrefix + "rain.light";

        /// <summary>
        /// Heavy rain: a steady roar with no individual drops, the owner's heavier recording. Rises
        /// under the light bed as rain passes about half intensity, and carries a storm.
        /// </summary>
        public const string AmbienceRainHeavy = AmbiencePrefix + "rain.heavy";

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

        /// <summary>
        /// The bed behind the title screen, the load list and the world-setup page — and behind
        /// nothing else.
        ///
        /// <para><b>It is the one sound that is explicitly not part of the colony.</b> Everything
        /// else in this file is something happening in a world; this plays when there is no world
        /// and stops the moment there is one, crossing with the outdoor bed's own arrival fade.
        /// It rides the Music bus rather than Ambience because that is what it is — a menu track —
        /// and because a player who turns music off should get a silent title screen.</para>
        /// </summary>
        public const string AmbienceMenu = AmbiencePrefix + "menu";

        /// <summary>The daytime track.</summary>
        public const string MusicDay = MusicPrefix + "day";

        /// <summary>The night-time track.</summary>
        public const string MusicNight = MusicPrefix + "night";

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

    /// <summary>
    /// Which chime an alert row gets.
    ///
    /// <para><b>Severity first, key second.</b> The alerts panel already sorts every condition
    /// into notice, warning and danger, and that is the distinction a chime can carry: "look
    /// when you can" against "go and fix it". So the default is the severity's sound and nothing
    /// has to be listed here to be audible — the nineteen <c>ui.alert.*</c> keys in
    /// <c>icon-keys.csv</c> that nothing raises yet will chime correctly on the day they are
    /// implemented, without anybody remembering to come back to this file.</para>
    ///
    /// <para><see cref="Overrides"/> is for the few conditions that want their own sound
    /// because severity undersells them. A raid is a danger like a starving colonist is a
    /// danger, and they should plainly not make the same noise.</para>
    /// </summary>
    public static class AlertChime
    {
        /// <summary>The raid alert's key, as <c>docs/design/icon-keys.csv</c> declares it.
        /// Nothing raises it yet; the row below is what makes that a seam rather than a
        /// to-do.</summary>
        public const string RaidKey = "ui.alert.raid";

        /// <summary>Conditions whose own sound beats their severity's. Ordinal, and short
        /// enough that a linear scan is cheaper than a dictionary.</summary>
        static readonly (string Key, string Sound)[] Overrides =
        {
            (RaidKey, SoundIds.AlertRaid),
        };

        /// <summary>The sound a row makes when it first appears.</summary>
        public static string For(string key, Hud.AlertSeverity severity)
        {
            for (int i = 0; i < Overrides.Length; i++)
                if (string.Equals(Overrides[i].Key, key, StringComparison.Ordinal))
                    return Overrides[i].Sound;
            return ForSeverity(severity);
        }

        /// <summary>The default chime for a severity: a notice is worth a glance, and everything
        /// else is worth getting up for.</summary>
        public static string ForSeverity(Hud.AlertSeverity severity) =>
            severity == Hud.AlertSeverity.Notice ? SoundIds.AlertNormal : SoundIds.AlertNegative;
    }

    /// <summary>
    /// Turns the alerts panel's rows into chimes: one sound the moment a row appears, and
    /// nothing at all for a row that is merely still there.
    ///
    /// <para><b>Why it reads the panel rather than the pawns.</b> It used to read the published
    /// pawn list and carry its own starvation threshold, which was a second owner of a rule
    /// <see cref="Hud.AlertModel"/> already owned — and the two had drifted: the audio copy
    /// tested food against 12 on a scale that runs to 1000, so the chime fired at a hundredth
    /// of the food the red row appears at, which is to say never. Reading the rows means the
    /// sound and the panel cannot disagree about what an alert is, and a row the player has
    /// dismissed is silent for free.</para>
    ///
    /// <para><b>One chime, not one per row.</b> Several conditions can cross on the same frame;
    /// they chime once between them, at the loudest severity present, because one sound saying
    /// "something needs you" is the message and the panel is what says what.</para>
    ///
    /// <para><b>The first step is silent.</b> The watch arms itself on whatever is already on
    /// screen when a world arrives, so loading a save with a hungry colonist does not chime at
    /// the player before they have found the mouse.</para>
    /// </summary>
    public sealed class AlertChimeWatch
    {
        readonly HashSet<int> _sounded = new();
        readonly HashSet<int> _present = new();
        bool _armed;

        /// <summary>Rows sounded so far, for a test and for the developer overlay.</summary>
        public int Sounding => _sounded.Count;

        /// <summary>
        /// Step one refresh of the panel and return the sound to play, or <c>null</c>.
        /// </summary>
        public string? Step(IReadOnlyList<Hud.AlertRow> rows)
        {
            _present.Clear();

            string? chime = null;
            Hud.AlertSeverity loudest = default;

            for (int i = 0; i < rows.Count; i++)
            {
                Hud.AlertRow row = rows[i];
                _present.Add(row.DismissKey);

                if (!_armed || _sounded.Contains(row.DismissKey)) continue;
                if (chime != null && row.Severity <= loudest) continue;

                loudest = row.Severity;
                chime = AlertChime.For(row.Key, row.Severity);
            }

            // What is on screen is what has been sounded. A row that clears drops out, so the
            // condition returning is a new alert and chimes again — which is the behaviour a
            // player expects and the reason this is not a set that only grows.
            _sounded.Clear();
            foreach (int key in _present) _sounded.Add(key);

            _armed = true;
            return chime;
        }
    }
}
