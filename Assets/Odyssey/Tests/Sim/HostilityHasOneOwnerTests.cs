#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Who fights whom is decided in one place, <see cref="Allegiance"/> (design 61 §5, unit F0).
    ///
    /// <para><b>Why.</b> Before F0, "is this an enemy?" was answered in about a dozen places. Every
    /// one of them assumed the colony was at the centre. <c>Ranged.cs</c> spelled the asymmetry out
    /// as <c>me.IsColonist ? IsThreatTo(other, me) : other.IsColonist</c>, and <c>CombatJobs</c> had a
    /// copy of the same line. Factions (F1) make "hostile" a relation between two peoples, not a
    /// property of one pawn. Without one owner, that change would be a dozen edits, each able to
    /// disagree with the others in silence: a raider who hunts a trader its gun will not shoot at.</para>
    ///
    /// <para><b>F0 changes no behaviour.</b> <see cref="Allegiance.AreHostile"/>,
    /// <see cref="Allegiance.IsFoe"/> and <see cref="Allegiance.AreAllies"/> give exactly the answers
    /// the scattered copies gave. The first three tests hold them to those old expressions, written
    /// out here as the oracle, over every kind of pawn the game has. The last test fails the build if
    /// a second copy of a side rule appears anywhere in the simulation.</para>
    /// </summary>
    public class HostilityHasOneOwnerTests
    {
        static readonly GridSize Size = new GridSize(48, 48, 16);
        const uint Seed = 20260926;

        /// <summary>
        /// Every kind of pawn a side question can be asked about. It holds:
        /// <list type="bullet">
        /// <item>a colonist, and a bandit and a hog at large;</item>
        /// <item>a bandit held prisoner, and one let go;</item>
        /// <item>an arrested colonist breaking out;</item>
        /// <item>a recruit (a bandit by kind, ours by custody).</item>
        /// </list>
        /// </summary>
        static (ColonyWorld colony, Dictionary<string, Pawn> cast) Cast()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            ColonyWorld colony = ColonyWorld.Build(Size, Seed, scenario, barren: true, wooded: false);
            int at = Size.Index(colony.Start);
            PawnRegistry pawns = colony.Pawns.Pawns;

            var cast = new Dictionary<string, Pawn>
            {
                ["colonist"] = pawns.All[0],
                ["escapee"] = pawns.All[1],
                ["bandit"] = pawns.Spawn(at, PawnKindIndex.Bandit),
                ["gunman"] = pawns.Spawn(at, PawnKindIndex.Gunman),
                ["hog"] = pawns.Spawn(at, PawnKindIndex.MiddenHog),
                ["held"] = pawns.Spawn(at, PawnKindIndex.Bandit),
                ["released"] = pawns.Spawn(at, PawnKindIndex.Bandit),
                ["recruit"] = pawns.Spawn(at, PawnKindIndex.Bandit),
            };
            cast["escapee"].Custody = PawnCustody.Escaping;
            cast["held"].Custody = PawnCustody.Prisoner;
            cast["released"].Custody = PawnCustody.Released;
            cast["recruit"].Prison = new PrisonRecord { Joined = true };
            return (colony, cast);
        }

        [Test]
        public void AreHostileIsTheOldRuleOverTheWholeCast()
        {
            var (_, cast) = Cast();
            int hostilePairs = 0;
            foreach (var a in cast)
            foreach (var b in cast)
            {
                if (a.Value == b.Value) continue;
                bool old = (a.Value.IsHostile && b.Value.IsColonist) || (b.Value.IsHostile && a.Value.IsColonist);
                bool now = Allegiance.AreHostile(a.Value, b.Value);
                Assert.That(now, Is.EqualTo(old), $"{a.Key} against {b.Key}");
                Assert.That(Allegiance.AreHostile(b.Value, a.Value), Is.EqualTo(now), $"{a.Key} and {b.Key}: symmetric");
                if (now) hostilePairs++;
            }
            Assert.That(hostilePairs, Is.GreaterThan(0), "the control: the cast holds some enemies, so the equality above says something");
            Assert.That(Allegiance.AreHostile(cast["colonist"], cast["colonist"]), Is.False, "nobody is her own enemy");
        }

        /// <summary>
        /// What a pawn would fight unordered is what the two copies of
        /// <c>me.IsColonist ? IsThreatTo(other, me) : other.IsColonist</c> said, for every pawn that
        /// ever chooses a target:
        /// <list type="bullet">
        /// <item>colonists, recruits included;</item>
        /// <item>bandits and gunmen;</item>
        /// <item>an escapee.</item>
        /// </list>
        /// Nobody here is mid-attack, so the "attacking me" clause is false throughout. Its
        /// behaviour is held by the combat gate in the Long tier, which F0 leaves identical.
        /// </summary>
        [Test]
        public void AFoeIsWhatTheOldRuleSaidForEveryPawnThatChoosesOne()
        {
            var (_, cast) = Cast();
            string[] choosers = { "colonist", "recruit", "bandit", "gunman", "escapee" };
            foreach (string m in choosers)
            foreach (var other in cast)
            {
                Pawn me = cast[m];
                if (other.Value == me) continue;
                bool old = me.IsColonist
                    ? other.Value.IsHostile || Melee.IsAttacking(other.Value, me)
                    : other.Value.IsColonist;
                Assert.That(Allegiance.IsFoe(me, other.Value), Is.EqualTo(old), $"{m} looking at {other.Key}");
            }
        }

        /// <summary>
        /// The one deliberate difference. The old ternary made every colonist a foe of **any**
        /// non-colonist who asked, a held prisoner or a hog included. Neither ever asks: a prisoner
        /// thinks as a prisoner, and an animal has no gun and hunts only whoever struck it. The one
        /// rule says what is true: a held prisoner and an animal at peace have no enemies.
        /// </summary>
        [Test]
        public void AHeldPrisonerAndAnAnimalAtPeaceHaveNoEnemies()
        {
            var (_, cast) = Cast();
            foreach (string m in new[] { "held", "hog", "released" })
            foreach (var other in cast)
            {
                if (other.Value == cast[m]) continue;
                Assert.That(Allegiance.IsFoe(cast[m], other.Value), Is.False, $"{m} looking at {other.Key}");
            }
        }

        [Test]
        public void TheColonyIsOneSide()
        {
            var (_, cast) = Cast();
            Assert.That(Allegiance.AreAllies(cast["colonist"], cast["recruit"]), Is.True, "a recruit is one of ours");
            Assert.That(Allegiance.AreAllies(cast["colonist"], cast["bandit"]), Is.False);
            Assert.That(Allegiance.AreAllies(cast["colonist"], cast["held"]), Is.False, "a prisoner is nobody's ally");
            Assert.That(Allegiance.AreAllies(cast["colonist"], cast["escapee"]), Is.False, "an arrested colonist running is not");
            Assert.That(Allegiance.AreAllies(cast["colonist"], cast["colonist"]), Is.False, "a pawn is not her own ally");
            Assert.That(Allegiance.AreAllies(cast["bandit"], cast["gunman"]), Is.False,
                "at F0 only the colony is a side; F1 makes the bandits one too");
        }

        // ---- the source: nobody else decides ----------------------------------------------------

        /// <summary>
        /// Files that may read a pawn's side directly for a purpose that is not "who fights whom",
        /// each with its reason. Anything else matching a pattern below is a second opinion.
        /// </summary>
        static readonly (string file, string why)[] Allowed =
        {
            ("Allegiance.cs", "the owner"),
            ("BedRules.cs", "which kind of bed a pawn sleeps in, not a fight"),
            ("WeaponDraw.cs", "whether a weapon is drawn: the pawn's own stance, not a pairing"),
        };

        static readonly (Regex pattern, string what)[] Forbidden =
        {
            (new Regex(@"\.IsColonist\s*\?"), "a choice made on whether one pawn is a colonist"),
            (new Regex(@"(?<!!)\b[A-Za-z_]\w*\.IsHostile\s*\|\|"), "'is hostile, or ...' written out as a threat test"),
            (new Regex(@"\b(\w+)\.IsColonist\b.*\b(?!\1\b)\w+\.IsColonist\b"), "two pawns' sides compared on one line"),
        };

        [Test]
        public void NoSecondRuleDecidesWhoFightsWhom()
        {
            string simRoot = SimSourceRoot();
            var offenders = new List<string>();
            foreach (string file in Directory.EnumerateFiles(simRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (Array.Exists(Allowed, a => a.file == name)) continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (var (pattern, what) in Forbidden)
                        if (pattern.IsMatch(code)) offenders.Add($"{name}:{i + 1}: {what}: {lines[i].Trim()}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "Who fights whom is Allegiance's to decide (AreHostile, IsFoe, AreAllies; design 61 §5). " +
                "Factions make it a relation between peoples, and a second copy will disagree with the first " +
                "the day it changes. Call Allegiance instead. Offenders:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>The patterns catch what they are meant to: the lines F0 replaced.</summary>
        [Test]
        public void ThePatternsCatchTheLinesF0Replaced()
        {
            string[] before =
            {
                "bool foe = me.IsColonist ? Melee.IsThreatTo(other, me) : other.IsColonist;",
                "other != me && IsStanding(other) && (other.IsHostile || IsAttacking(other, me));",
                "if (stray && attacker.IsColonist && target.IsColonist) return;",
                "if (by == null || by == target || !by.IsColonist || !target.IsColonist || Melee.IsDead(target)) return;",
            };
            foreach (string line in before)
                Assert.That(Array.Exists(Forbidden, f => f.pattern.IsMatch(line)), Is.True, line);

            string[] fine =
            {
                "if (!target.IsHostile || target.Custody != PawnCustody.Free) return false;",
                "if (other == pawn || !other.IsColonist) continue;",
                "if (pawns[i].IsColonist && Melee.IsStanding(pawns[i])) n++;",
            };
            foreach (string line in fine)
                Assert.That(Array.Exists(Forbidden, f => f.pattern.IsMatch(line)), Is.False, line);
        }

        static string StripComment(string line)
        {
            int at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 ? line : line.Substring(0, at);
        }

        static string SimSourceRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Assets", "Odyssey", "Sim");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("could not find Assets/Odyssey/Sim from " + TestContext.CurrentContext.TestDirectory);
        }
    }
}
