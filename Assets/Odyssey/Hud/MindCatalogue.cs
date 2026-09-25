#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// What the Thoughts tab can list (design 44 §5b): the situational sources and the memories,
    /// each with the registry key that names it and the aspect it is published under. The names
    /// are <see cref="ThoughtHandle.Names"/> on the contract, so this class spells only the
    /// prefixes; <c>MindAspectTests</c> (Sim) holds the simulation's spelling to the same strings.
    /// </summary>
    public static class MindCatalogue
    {
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

            public Source(string key, AspectKey value, AspectKey left = default, AspectKey count = default,
                bool memory = false)
            {
                Key = key;
                Value = value;
                Left = left;
                Count = count;
                Memory = memory;
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
            new Source("ui.thought.temperature", AspectKey.Of("odyssey.pawn.mood.temperature")),
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

        /// <summary>How long until a memory thins, as the clock's hours or days: "5h", "2d".</summary>
        public static string Left(int ticks)
        {
            long hour = GameClock.TicksPerDay / 24;
            if (ticks < GameClock.TicksPerDay) return ((ticks + hour - 1) / hour) + "h";
            return ((ticks + GameClock.TicksPerDay / 2) / GameClock.TicksPerDay) + "d";
        }

        /// <summary>
        /// <see cref="Left"/> as a number that changes exactly when its text does: hours below a day,
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
