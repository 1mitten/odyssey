#nullable enable
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>The eight things the Almanac says about how an animal behaves, in the order it says them.</summary>
    public enum AnimalFact
    {
        Temperament,
        Approached,
        Struck,
        Group,
        Active,
        Hunts,
        Danger,
        Signature,
    }

    /// <summary>
    /// How often a struck animal turns on whoever struck it, in words, from the species'
    /// <c>revengePerMille</c> (design 29 §6, the roll <c>CombatSystem.Apply</c> makes per blow).
    /// </summary>
    public enum StruckBand
    {
        Never,
        Rarely,
        Sometimes,
        Usually,
        Always,
    }

    /// <summary>One line of an animal's behaviour, and whether the game does it today.</summary>
    public readonly struct AnimalFactLine
    {
        public readonly AnimalFact Fact;
        public readonly string Text;
        public readonly bool Live;

        public AnimalFactLine(AnimalFact fact, string text, bool live)
        {
            Fact = fact;
            Text = text;
            Live = live;
        }
    }

    /// <summary>One animal kind's behaviour: what it does today, and what the plan builds next.</summary>
    public sealed class AnimalBehaviourRecord
    {
        public int Kind { get; }

        /// <summary>The kind's Def, so a test can hold the lines to the content they describe.</summary>
        public string KindDefName { get; }

        /// <summary>The rung's registry key (<c>ui.temperament.*</c>), derived from the fields in FA2.</summary>
        public string TemperamentKey { get; }

        public StruckBand Struck { get; }
        public int GroupMin { get; }
        public int GroupMax { get; }
        public bool Nocturnal { get; }
        public bool ShelterFromRain { get; }

        /// <summary>Every line, ordered by <see cref="AnimalFact"/> and then as written.</summary>
        public IReadOnlyList<AnimalFactLine> Lines { get; }

        internal AnimalBehaviourRecord(int kind, string kindDefName, string temperamentKey, StruckBand struck,
            int groupMin, int groupMax, bool nocturnal, bool shelterFromRain, AnimalFactLine[] extra)
        {
            Kind = kind;
            KindDefName = kindDefName;
            TemperamentKey = temperamentKey;
            Struck = struck;
            GroupMin = groupMin;
            GroupMax = groupMax;
            Nocturnal = nocturnal;
            ShelterFromRain = shelterFromRain;

            var lines = new List<AnimalFactLine>(extra.Length + 6)
            {
                // The rung is FA2's: derived from design 64's fields, which do not exist yet.
                new AnimalFactLine(AnimalFact.Temperament,
                    Registry.Label(temperamentKey) + ". " + Registry.Describe(temperamentKey), false),
                new AnimalFactLine(AnimalFact.Struck, AnimalBehaviour.StruckText(struck), true),
                new AnimalFactLine(AnimalFact.Group, AnimalBehaviour.GroupText(groupMin, groupMax), true),
                new AnimalFactLine(AnimalFact.Active, nocturnal
                    ? "Out at night; by day it takes a third as many walks and rests three times as long"
                    : "Out by day; at night it takes a third as many walks and rests three times as long", true),
            };
            if (shelterFromRain)
                lines.Add(new AnimalFactLine(AnimalFact.Active, "Makes for cover within its range in heavy rain", true));
            // Today no animal starts a fight: it wanders, rests, and answers a blow (design 29, 33).
            lines.Add(new AnimalFactLine(AnimalFact.Danger, "Never starts a fight", true));
            lines.AddRange(extra);

            // Stable by fact, so a record reads in the Almanac's order whatever order it was written in.
            var ordered = new List<AnimalFactLine>(lines.Count);
            for (var fact = AnimalFact.Temperament; fact <= AnimalFact.Signature; fact++)
                foreach (AnimalFactLine line in lines)
                    if (line.Fact == fact)
                        ordered.Add(line);
            Lines = ordered;
        }
    }

    /// <summary>
    /// What every animal in the game does, as the Almanac tells it — the one source for the Fauna
    /// entries' behaviour rows (plan <c>forest-animals.md</c>; designs 64, 65, 67 for what is not yet
    /// built). **A line is <c>Live</c> only when the build does it today**; every other line is
    /// drawn with the not-yet marker, so the Almanac can show an animal's whole character without
    /// claiming anything the game does not do.
    ///
    /// <para><b>FA2 and FA3 must flip a line to live in the same commit that builds it</b>, and edit
    /// <c>AnimalBehaviourTests.TheLiveLinesAreExactlyTheseToday</c> beside it. Nothing else checks that
    /// a line's liveness is true (the SK5 lesson: a skill row greyed out for days after it began to
    /// train).</para>
    /// </summary>
    public static class AnimalBehaviour
    {
        /// <summary>Drawn beside a line the game does not do yet.</summary>
        public const string NotYetKey = "ui.almanac.notyet";

        /// <summary>The row title for each fact. The temperament's is the pane's own (design 64 §9a).</summary>
        public static string FactKey(AnimalFact fact) => fact switch
        {
            AnimalFact.Temperament => "ui.temperament.label",
            AnimalFact.Approached => "ui.almanac.fact.approached",
            AnimalFact.Struck => "ui.almanac.fact.struck",
            AnimalFact.Group => "ui.almanac.fact.group",
            AnimalFact.Active => "ui.almanac.fact.active",
            AnimalFact.Hunts => "ui.almanac.fact.hunts",
            AnimalFact.Danger => "ui.almanac.fact.danger",
            _ => "ui.almanac.fact.signature",
        };

        /// <summary>
        /// The band a <c>revengePerMille</c> falls in: 0 never, under 300 rarely, under 700
        /// sometimes, under 1,000 usually, 1,000 always.
        /// </summary>
        public static StruckBand BandOf(int revengePerMille) =>
            revengePerMille <= 0 ? StruckBand.Never
            : revengePerMille < 300 ? StruckBand.Rarely
            : revengePerMille < 700 ? StruckBand.Sometimes
            : revengePerMille < 1000 ? StruckBand.Usually
            : StruckBand.Always;

        public static string StruckText(StruckBand band) => band switch
        {
            StruckBand.Never => "Never fights back: it runs from whoever struck it",
            StruckBand.Rarely => "Rarely turns on whoever struck it; nearly always runs",
            StruckBand.Sometimes => "Sometimes turns on whoever struck it; otherwise runs",
            StruckBand.Usually => "Usually turns on whoever struck it; otherwise runs",
            _ => "Always turns on whoever struck it",
        };

        public static string GroupText(int min, int max) =>
            max <= 1 ? "Alone"
            : min <= 1 && max == 2 ? "Alone or in pairs"
            : min <= 1 ? "Alone or in groups of up to " + max
            : "In groups of " + min + " to " + max;

        static AnimalFactLine Now(AnimalFact fact, string text) => new AnimalFactLine(fact, text, true);

        static AnimalFactLine Later(AnimalFact fact, string text) => new AnimalFactLine(fact, text, false);

        const AnimalFact Approached = AnimalFact.Approached, Struck = AnimalFact.Struck, Group = AnimalFact.Group,
            Active = AnimalFact.Active, Hunts = AnimalFact.Hunts, Danger = AnimalFact.Danger, Signature = AnimalFact.Signature;

        const string Timid = "ui.temperament.timid", Skittish = "ui.temperament.skittish",
            Defensive = "ui.temperament.defensive", Territorial = "ui.temperament.territorial",
            Predator = "ui.temperament.predator";

        const string TakesNothing = "Takes nothing of the colony's";
        const string GivesWay = "A colonist who is not drafted backs off and walks round it while it warns";

        /// <summary>Every animal kind, in kind order. The butcher is a hostile, not an animal.</summary>
        public static readonly IReadOnlyList<AnimalBehaviourRecord> Records = new[]
        {
            new AnimalBehaviourRecord(PawnKindLabels.MiddenHogKind, "PawnKind_MiddenHog", Defensive, StruckBand.Usually, 2, 3, false, true, new[]
            {
                Later(Approached, "Ignores people and stands its ground"),
                Later(Struck, "At arm's length it always fights; an enraged hog hunts its attacker for four hours"),
                Later(Group, "Strike one and the sounder within 15 m turns with it"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Hunted by wolves"),
                Now(Signature, "The ruined city's animal; the meadow's woods belong to the thicket boar"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.DuctRatKind, "PawnKind_DuctRat", Skittish, StruckBand.Rarely, 1, 2, true, true, new[]
            {
                Later(Approached, "Bolts from anyone within 7 m"),
                Later(Struck, "Three times as likely to bite back at arm's length, and fights if cornered"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Hunted by foxes and wolves"),
                Now(Signature, "Climbs ladders, which no other animal takes"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.CulvertFrogKind, "PawnKind_CulvertFrog", Timid, StruckBand.Never, 3, 5, false, false, new[]
            {
                Later(Approached, "Hops away from anyone within 7 m"),
                Later(Struck, "Never fights, even cornered"),
                Later(Group, "When one hops off, those within 10 m go with it"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Hunted by foxes and wolves"),
                Now(Signature, "Every walk ends within 7 m of water, and it stays out in the rain"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.VergeRabbit, "PawnKind_VergeRabbit", Timid, StruckBand.Never, 1, 3, false, true, new[]
            {
                Later(Approached, "Freezes for a second and a half when someone comes within 12 m, then bolts about 35 m"),
                Later(Struck, "Never fights, even cornered"),
                Later(Group, "When one bolts, those within 15 m bolt with it"),
                Later(Active, "Out at dawn and dusk (04:00 to 08:00 and 17:00 to 21:00)"),
                Later(Hunts, "Grazes growing crops at dawn and dusk, one tile at a time: the plant is uprooted and the zone sows it again. A walled field with a door keeps it out"),
                Later(Hunts, "Hunted by foxes and wolves"),
                Now(Signature, "The quickest thing on the board: 130% of a colonist's walk"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.HedgerowDeer, "PawnKind_HedgerowDeer", Timid, StruckBand.Never, 3, 6, false, true, new[]
            {
                Later(Approached, "Bolts about 40 m from anyone within 20 m: the most wary animal in the forest"),
                Later(Struck, "Never fights, even cornered"),
                Later(Group, "One bolts and the herd within 25 m follows"),
                Later(Active, "Out at dawn and dusk (04:00 to 08:00 and 17:00 to 21:00)"),
                Later(Hunts, "Grazes growing crops at dawn and dusk, one tile at a time: the plant is uprooted and the zone sows it again. A walled field with a door keeps it out"),
                Later(Hunts, "Hunted by wolves"),
                Later(Danger, "A stag in rut (Ember) stamps at, and may charge, a colonist who crowds it"),
                Now(Signature, "A doe or a stag, dealt by its own seed; the pane names which"),
                Later(Signature, "Stags rut in Ember, the month before Rime, and play Territorial for that month"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.AshFox, "PawnKind_AshFox", Predator, StruckBand.Never, 1, 1, true, true, new[]
            {
                Later(Approached, "Bolts from anyone within 12 m"),
                Later(Struck, "Rarely fights back at range, three times as likely at arm's length, and fights if cornered"),
                Later(Hunts, "Hunts rabbits, rats and frogs when hungry, alone; the kill is left where it fell"),
                Later(Hunts, "Hunted by wolves"),
                Later(Danger, "Bites if cornered"),
                Later(Signature, "The busier hunter: small, and hungry again within a day"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.GutterRaccoon, "PawnKind_GutterRaccoon", Skittish, StruckBand.Never, 1, 2, true, true, new[]
            {
                Later(Approached, "Bolts from anyone within 10 m"),
                Later(Struck, "Rarely fights back at range, three times as likely at arm's length, and fights if cornered"),
                Later(Group, "A pair turns together"),
                Later(Hunts, "Raids stockpiles and shelves by night, taking up to a meal's worth. A closed door keeps it out"),
                Later(Hunts, "Hunted by wolves"),
                Later(Danger, "Bites if cornered"),
                Later(Signature, "The thief: leaves the board with what it took, and drops it if startled on the way"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.RubbleSkunk, "PawnKind_RubbleSkunk", Skittish, StruckBand.Never, 1, 1, true, true, new[]
            {
                Later(Approached, "Does not run: within 7 m it stamps and lifts its tail for four seconds"),
                Later(Struck, "Rarely fights back at range, three times as likely at arm's length, and sprays if cornered"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Hunted by wolves"),
                Later(Danger, "Crowd it past its warning and it sprays: no wound, but a miserable day"),
                Later(Signature, "Sprays anyone within 5 m when its warning runs out: -45 mood for a day, and -10 to anyone standing within 5 m of them"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.ThicketBoar, "PawnKind_ThicketBoar", Defensive, StruckBand.Usually, 3, 5, true, true, new[]
            {
                Later(Approached, "Ignores people and stands its ground"),
                Later(Struck, "One blow in four at range turns it, three in four at arm's length; an enraged boar hunts its attacker for 4 to 10 hours"),
                Later(Group, "Strike one and the sounder within 20 m turns with it"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Hunted by wolves"),
                Now(Signature, "The midden hog's forest cousin, out at night in sounders"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.MireMoose, "PawnKind_MireMoose", Defensive, StruckBand.Always, 1, 3, false, true, new[]
            {
                Later(Approached, "Within 10 m it warns for three seconds, then charges; closer than 5 m it charges at once. Eight charges in ten stop short"),
                Later(Struck, "A third of blows at range turn it, nearly every one at arm's length; an enraged moose hunts its attacker for 4 to 10 hours"),
                Later(Group, "Strike one and the others within 20 m join"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Too big for any hunter"),
                Later(Danger, "Charges a colonist who comes too close; most charges stop short"),
                Later(Danger, GivesWay),
                Later(Danger, "A bull in rut (Ember) is worse"),
                Now(Signature, "A cow or a bull, dealt by its own seed: the biggest animal on the board"),
                Later(Signature, "Bulls rut in Ember, the month before Rime, and play Territorial for that month"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.RidgeWolf, "PawnKind_RidgeWolf", Predator, StruckBand.Always, 2, 4, true, true, new[]
            {
                Later(Approached, "Keeps about 17 m from people and slips away"),
                Later(Struck, "Always turns on whoever struck it; an enraged wolf hunts its attacker for 4 to 10 hours"),
                Later(Group, "The pack within 30 m joins a hunt and a fight"),
                Later(Hunts, "Hunts when hungry, as a pack: deer, boar, hogs, foxes, raccoons, skunks, rabbits, rats and frogs, never a moose or a bear. The kill is left where it fell"),
                Later(Danger, "A starving wolf may stalk a colonist who is alone, if the world allows it, and charges from 12 m"),
                Later(Danger, "A colonist it downs has about an hour: strike it, wound it past half or carry her away, or she dies. The only animal that kills"),
                Later(Signature, "A pack acts as one"),
            }),
            new AnimalBehaviourRecord(PawnKindLabels.QuarryBear, "PawnKind_QuarryBear", Territorial, StruckBand.Always, 1, 1, false, true, new[]
            {
                Later(Approached, "Within 7 m, or 15 m of where it rests, it warns for four seconds, then charges; closer than 5 m it charges at once. Seven charges in ten stop short"),
                Later(Struck, "Half of blows at range turn it, every one at arm's length; an enraged bear hunts its attacker for 4 to 10 hours"),
                Now(Hunts, TakesNothing),
                Later(Hunts, "Too big for any hunter"),
                Later(Danger, "Charges a colonist who lingers where it rests; three charges in ten are real"),
                Later(Danger, GivesWay),
                Later(Signature, "Away for the winter: leaves the board at the start of Hollow and comes back from Larkspur"),
            }),
        };

        /// <summary>The record for a kind, or null for a kind that is not an animal.</summary>
        public static AnimalBehaviourRecord? For(int kind)
        {
            foreach (AnimalBehaviourRecord record in Records)
                if (record.Kind == kind)
                    return record;
            return null;
        }

        /// <summary>
        /// The Almanac's behaviour rows for a kind: the fact's title, the line, and the not-yet marker
        /// in the detail column for a line the game does not do yet (the detail is drawn dimmer).
        /// </summary>
        public static IReadOnlyList<(string Title, string Value, string Detail)> SpecsFor(int kind)
        {
            AnimalBehaviourRecord? record = For(kind);
            var specs = new List<(string, string, string)>();
            if (record == null)
                return specs;
            foreach (AnimalFactLine line in record.Lines)
                specs.Add((Registry.Label(FactKey(line.Fact)), line.Text, line.Live ? "" : Registry.Label(NotYetKey)));
            return specs;
        }
    }
}
