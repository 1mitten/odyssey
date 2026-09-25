#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>Where a thought comes from, as the Thoughts tab words it (design 51 §10).</summary>
    public enum ThoughtSource : byte
    {
        /// <summary>A need that is short: hunger, tiredness, recreation.</summary>
        Need,

        /// <summary>Something about where she is: the temperature.</summary>
        Condition,

        /// <summary>Something that happened to her, fading after its own time.</summary>
        Memory,
    }

    /// <summary>
    /// What the Thoughts tab can list (design 51 §5b): the situational sources and the memories,
    /// each with the registry key that names it and the aspect it is published under. The names
    /// are <see cref="ThoughtHandle.Names"/> on the contract, so this class spells only the
    /// prefixes; <c>MindAspectTests</c> (Sim) holds the simulation's spelling to the same strings.
    /// </summary>
    public static class MindCatalogue
    {
        /// <summary>The registry key for a source's kind: the word beside a thought's name.</summary>
        public static string SourceKey(ThoughtSource kind) => kind switch
        {
            ThoughtSource.Condition => "ui.mind.source.condition",
            ThoughtSource.Memory => "ui.mind.source.memory",
            _ => "ui.mind.source.need",
        };

        /// <summary>One thing that can move a colonist's mood.</summary>
        public readonly struct Source
        {
            /// <summary>The registry key that names it: <c>ui.thought.*</c>.</summary>
            public readonly string Key;

            /// <summary>Its worth to her, in thousandths.</summary>
            public readonly AspectKey Value;

            /// <summary>A memory's time left and copies; default for a situational source.</summary>
            public readonly AspectKey Left, Count;

            /// <summary>True for a memory, false for something situational.</summary>
            public readonly bool Memory;

            /// <summary>What the Thoughts tab calls where it comes from (design 51 §10).</summary>
            public readonly ThoughtSource Kind;

            public Source(string key, AspectKey value, AspectKey left = default, AspectKey count = default,
                bool memory = false, ThoughtSource kind = ThoughtSource.Need)
            {
                Key = key;
                Value = value;
                Left = left;
                Count = count;
                Memory = memory;
                Kind = memory ? ThoughtSource.Memory : kind;
            }
        }

        /// <summary>The prefix a memory is published under, before its <see cref="ThoughtHandle.Names"/> entry.</summary>
        public const string ThoughtPrefix = "odyssey.pawn.thought.";

        /// <summary>The situational sources, in the order the simulation's needs are numbered, then the room.</summary>
        public static readonly Source[] Situational =
        {
            new Source("ui.thought.hunger", AspectKey.Of("odyssey.pawn.mood.need.food")),
            new Source("ui.thought.tiredness", AspectKey.Of("odyssey.pawn.mood.need.rest")),
            new Source("ui.thought.recreation", AspectKey.Of("odyssey.pawn.mood.need.joy")),
            new Source("ui.thought.temperature", AspectKey.Of("odyssey.pawn.mood.temperature"), kind: ThoughtSource.Condition),
        };

        /// <summary>Every memory, by <see cref="ThoughtHandle"/>.</summary>
        public static readonly Source[] Memories = MintMemories();

        static Source[] MintMemories()
        {
            var sources = new Source[ThoughtHandle.Count];
            for (int t = 0; t < sources.Length; t++)
            {
                string name = ThoughtPrefix + ThoughtHandle.Names[t];
                sources[t] = new Source("ui.thought." + ThoughtHandle.Names[t], AspectKey.Of(name),
                    AspectKey.Of(name + ".left"), AspectKey.Of(name + ".count"), memory: true);
            }
            return sources;
        }

        /// <summary>
        /// A mood amount as the interface writes one: thousandths as points out of a hundred,
        /// rounded half away from nought, signed in ASCII (P13). −80 is "-8".
        /// </summary>
        public static string Points(int thousandths)
        {
            int magnitude = PointsOf(thousandths);
            if (magnitude < 0) magnitude = -magnitude;
            if (magnitude == 0) return "0";
            return (thousandths > 0 ? "+" : "-") + magnitude;
        }

        /// <summary>
        /// <see cref="Points"/> as a number, signed: what the pane draws, for a signature to compare
        /// without building the string.
        /// </summary>
        public static int PointsOf(int thousandths)
        {
            int magnitude = (System.Math.Abs(thousandths) + 5) / 10;
            return thousandths < 0 ? -magnitude : magnitude;
        }

        /// <summary>
        /// How long until a memory thins, in the clock's words (design 51 §10): "5 hours" under a
        /// day, rounded up so a memory never reads as gone while it is not, then "1 day", "2 days".
        /// The words are the registry's.
        /// </summary>
        public static string Lasts(int ticks)
        {
            long left = LeftOf(ticks);
            if (left < 1_000)
                return left + " " + Registry.Label(left == 1 ? "ui.mind.hour" : "ui.mind.hours");
            long days = left - 1_000;
            if (days < 1) days = 1;
            return days + " " + Registry.Label(days == 1 ? "ui.mind.day" : "ui.mind.days");
        }

        /// <summary>
        /// <see cref="Lasts"/> as a number that changes exactly when its text does: hours below a day,
        /// and days offset clear of them above.
        /// </summary>
        public static long LeftOf(int ticks)
        {
            long hour = GameClock.TicksPerDay / 24;
            if (ticks < GameClock.TicksPerDay) return (ticks + hour - 1) / hour;
            return 1_000 + (ticks + GameClock.TicksPerDay / 2) / GameClock.TicksPerDay;
        }
    }
}
