#nullable enable
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Custody and the one owner of sides (design 60 §4). A pawn's kind never changes; custody is
    /// the override, and <see cref="Allegiance"/> turns it into every answer about sides. These
    /// tests hold one row each of design 60 §4b's consumer table, the save and the hash, and the
    /// published view the interface draws from.
    /// </summary>
    public class CustodyTests
    {
        static readonly GridSize Size = new GridSize(48, 48, 16);
        const uint Seed = 20260926;

        static ColonyWorld Colony(int colonists = 2)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, Seed, scenario, barren: true, wooded: false);
        }

        static IntentRejection Imprison(ColonyWorld colony, Pawn pawn, bool free = false)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.DebugImprison, colony.Start, pawn.Id.Value, free ? 1 : 0));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static Pawn Bandit(ColonyWorld colony) =>
            colony.Pawns.Pawns.Spawn(Size.Index(colony.Start), PawnKindIndex.Bandit);

        // ---- the consumer table, a row at a time -------------------------------------------------

        [Test]
        public void AHeldBanditIsNeitherOursNorAnEnemy()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            Assume.That(bandit.IsHostile, Is.True, "the control: a bandit at large is an enemy");

            Assert.That(Imprison(colony, bandit), Is.EqualTo(IntentRejection.None));
            Assert.That(bandit.Custody, Is.EqualTo(PawnCustody.Prisoner));
            Assert.That(bandit.IsPrisoner, Is.True);
            Assert.That(bandit.IsHostile, Is.False, "a held prisoner is nobody's enemy");
            Assert.That(bandit.IsColonist, Is.False, "and not one of ours");
            Assert.That(bandit.Faction, Is.EqualTo(Faction.Hostile), "her side is still her kind's");
            Assert.That(bandit.Kind, Is.EqualTo(PawnKindIndex.Bandit), "and her kind never changes");
            Assert.That(bandit.OwnMode, Is.EqualTo(TraverseMode.Bandit), "a prisoner cannot open a door");
            Assert.That(bandit.Motive, Is.EqualTo(Motive.None), "and came for nothing");
            Assert.That(BedRules.UserOf(bandit), Is.EqualTo(BedUser.Prisoner));
        }

        [Test]
        public void AnArrestedColonistIsHeldRememberedAndLosesHerBedAndHerDraft()
        {
            ColonyWorld colony = Colony();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.World.Intents.Submit(new Intent(IntentKind.SetDrafted, colony.Start, colonist.Id.Value, 1));
            colony.World.Tick();
            Assume.That(colonist.Drafted, Is.True);

            Assert.That(Imprison(colony, colonist), Is.EqualTo(IntentRejection.None));
            Assert.That(colonist.IsColonist, Is.False, "an arrested colonist is not one of ours while held");
            Assert.That(colonist.IsHostile, Is.False);
            Assert.That(colonist.Drafted, Is.False, "a prisoner is nobody's to command");
            Assert.That(colonist.Prison, Is.Not.Null);
            Assert.That(colonist.Prison!.Arrested, Is.True, "remembered as a colonist when taken");
            Assert.That(colonist.OwnMode, Is.EqualTo(TraverseMode.Bandit));
            Assert.That(colony.Construction.PawnOwnsABed(colonist.Id.Value), Is.False);
        }

        [Test]
        public void ARecruitIsOneOfOursWhateverHerKind()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            bandit.Prison = new PrisonRecord { Joined = true };

            Assert.That(bandit.Kind, Is.EqualTo(PawnKindIndex.Bandit));
            Assert.That(bandit.Faction, Is.EqualTo(Faction.Colony));
            Assert.That(bandit.IsColonist, Is.True);
            Assert.That(bandit.IsHostile, Is.False);
            Assert.That(bandit.OwnMode, Is.EqualTo(TraverseMode.Colonist), "she opens the colony's doors now");
            Assert.That(bandit.Motive, Is.EqualTo(Motive.None));
            Assert.That(BedRules.UserOf(bandit), Is.EqualTo(BedUser.Colonist));
        }

        [Test]
        public void AnEscapeeIsAnEnemyAndStillAPrisoner()
        {
            ColonyWorld colony = Colony();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assume.That(Imprison(colony, colonist), Is.EqualTo(IntentRejection.None));
            colonist.Custody = PawnCustody.Escaping;

            Assert.That(colonist.IsHostile, Is.True, "an arrested colonist breaking out is fought");
            Assert.That(colonist.IsPrisoner, Is.True);
            Assert.That(colonist.IsColonist, Is.False);
            Assert.That(BedRules.UserOf(colonist), Is.EqualTo(BedUser.Prisoner),
                "and keeps the prison bed she will be carried back to (review 2026-09-26)");
        }

        [Test]
        public void APawnLetGoIsNobodysAndNotHostile()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            Assume.That(Imprison(colony, bandit), Is.EqualTo(IntentRejection.None));
            bandit.Custody = PawnCustody.Released;

            Assert.That(bandit.IsHostile, Is.False, "a released raider walks off; she is not fought");
            Assert.That(bandit.IsPrisoner, Is.False);
            Assert.That(bandit.IsColonist, Is.False);
            Assert.That(bandit.Motive, Is.EqualTo(Motive.None), "and steals nothing on the way");
        }

        [Test]
        public void AHeldPawnThinksAsAPrisonerNotAsARaider()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            Assume.That(Imprison(colony, bandit), Is.EqualTo(IntentRejection.None));
            colony.World.Tick();

            Assert.That(PrisonerTrees.HeldTree.Select(n => n.Name), Does.Contain("PrisonerWait"));
            Assert.That(bandit.CurrentJob, Is.Not.Null);
            Assert.That(bandit.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.Wait), "held, she stands and waits");
        }

        /// <summary>
        /// <b>Raiders pass a prisoner by.</b> A bandit hunts the nearest standing colonist (design
        /// 33 §5); the only colonist on the board is held, so for a game minute the bandit stands
        /// beside her and she is never struck. The control is the same colonist, not held.
        /// </summary>
        [Test]
        public void ABanditDoesNotAttackAPrisoner()
        {
            ColonyWorld colony = Colony(colonists: 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assume.That(Imprison(colony, colonist), Is.EqualTo(IntentRejection.None));
            Bandit(colony);

            for (int i = 0; i < 600; i++) colony.World.Tick();
            Assert.That(colonist.HpMilli, Is.EqualTo(colonist.HpMaxMilli), "never struck");
            Assert.That(Melee.IsThreatTo(colonist, colony.Pawns.Pawns.All.Last()), Is.False);
        }

        [Test]
        public void TheControlABanditDoesAttackTheSameColonistFree()
        {
            ColonyWorld colony = Colony(colonists: 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Bandit(colony);

            bool struck = false;
            for (int i = 0; i < 600 && !struck; i++)
            {
                colony.World.Tick();
                struck = colonist.HpMilli < colonist.HpMaxMilli;
            }
            Assert.That(struck, Is.True, "a free colonist is hunted, so the test above proves something");
        }

        // ---- the debug row ----------------------------------------------------------------------

        [Test]
        public void TheDebugRowTakesTheNearestAndFreesAgain()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.DebugImprison, colony.Start, 0, 0));
            colony.World.Tick();
            Pawn held = colony.Pawns.Pawns.All.Single(p => p.Custody == PawnCustody.Prisoner);
            Assert.That(colony.World.Intents.Rejected, Is.Empty);

            Assert.That(Imprison(colony, held), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Imprison(colony, held, free: true), Is.EqualTo(IntentRejection.None));
            Assert.That(held.Custody, Is.EqualTo(PawnCustody.Free));
            Assert.That(held.Prison, Is.Null, "a debug freeing is an undo");
            Assert.That(bandit.Custody, Is.EqualTo(PawnCustody.Free).Or.EqualTo(PawnCustody.Free));
        }

        [Test]
        public void AnAnimalCannotBeTaken()
        {
            ColonyWorld colony = Colony();
            Pawn hog = colony.Pawns.Pawns.Spawn(Size.Index(colony.Start), PawnKindIndex.MiddenHog);
            Assert.That(Imprison(colony, hog), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(hog.Custody, Is.EqualTo(PawnCustody.Free));
        }

        // ---- the published view -----------------------------------------------------------------

        [Test]
        public void ThePublishedViewSaysWhoIsHeldAndTheRosterLeavesThemOut()
        {
            ColonyWorld colony = Colony();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Pawn bandit = Bandit(colony);
            Assume.That(Imprison(colony, colonist), Is.EqualTo(IntentRejection.None));
            Assume.That(Imprison(colony, bandit), Is.EqualTo(IntentRejection.None));
            colonist.Prison!.Dressed = true;
            colony.World.Tick();

            var views = colony.World.Views.Current.Pawns.ToArray();
            PawnView arrested = views.Single(v => v.Id == colonist.Id);
            PawnView raider = views.Single(v => v.Id == bandit.Id);
            Assert.That(arrested.Custody, Is.EqualTo(PawnCustody.Prisoner));
            Assert.That(arrested.IsColonist, Is.False, "the roster, the Work tab and the draft leave her out");
            Assert.That(arrested.IsPrisoner, Is.True);
            Assert.That(arrested.Dressed, Is.True);
            Assert.That(raider.IsHostile, Is.False);
            Assert.That(raider.Dressed, Is.False, "not yet laid in a prison bed");

            foreach (PawnView view in views)
                Assert.That(BedRule.UserOf(view), Is.EqualTo(BedRules.UserOf(colony.Pawns.Pawns.Get(view.Id)!)),
                    $"the interface's pool agrees with the simulation's for pawn {view.Id.Value}");
        }

        // ---- save and hash ------------------------------------------------------------------------

        [Test]
        public void AColonyNobodyHoldsWritesNoPrisonRecords()
        {
            ColonyWorld colony = Colony();
            var section = colony.SaveComponents.OfType<PrisonSection>().Single();
            using var stream = new MemoryStream();
            using var binary = new BinaryWriter(stream);
            section.Save(new SaveWriter(binary));
            binary.Flush();
            Assert.That(stream.Length, Is.EqualTo(8), "the layout and a count of nought");
        }

        [Test]
        public void CustodyAndTheRecordSurviveASaveAndAreHashed()
        {
            ColonyWorld colony = Colony();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Pawn bandit = Bandit(colony);
            Assume.That(Imprison(colony, bandit), Is.EqualTo(IntentRejection.None));
            Assume.That(Imprison(colony, colonist), Is.EqualTo(IntentRejection.None));
            bandit.Prison!.Mode = PrisonMode.Recruit;
            bandit.Prison.Willingness = 123_456;
            bandit.Prison.LastChatTick = 77;
            bandit.Prison.Dressed = true;

            colony.RebuildDerived();
            ulong before = colony.World.ComputeStateHash().Value;
            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            stream.Position = 0;

            ColonyWorld fresh = Colony();
            WorldSave.Load(fresh.World, stream, fresh.SaveComponents);
            fresh.RebuildDerived();

            Assert.That(fresh.World.ComputeStateHash().Value, Is.EqualTo(before));
            Pawn back = fresh.Pawns.Pawns.Get(bandit.Id)!;
            Assert.That(back.Custody, Is.EqualTo(PawnCustody.Prisoner));
            Assert.That(back.Prison!.Mode, Is.EqualTo(PrisonMode.Recruit));
            Assert.That(back.Prison.Willingness, Is.EqualTo(123_456));
            Assert.That(back.Prison.LastChatTick, Is.EqualTo(77));
            Assert.That(back.Prison.Dressed, Is.True);
            Assert.That(fresh.Pawns.Pawns.Get(colonist.Id)!.Prison!.Arrested, Is.True);
        }

        /// <summary>
        /// <b>A prisoner freshly taken survives a save to the same hash</b> (review 2026-09-26). Her
        /// record is empty — Hold, undressed, nobody has talked to her — and the load drops an empty
        /// record, so the hash must never have seen it; and the pane still says her mode after the
        /// load, since it asks her custody and not whether a record exists.
        /// </summary>
        [Test]
        public void AFreshlyTakenPrisonerSurvivesASaveToTheSameHash()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            Assume.That(Imprison(colony, bandit), Is.EqualTo(IntentRejection.None));
            Assume.That(bandit.Prison, Is.Not.Null, "the control: taking her left a record");
            Assume.That(bandit.Prison!.IsEmpty, Is.True);

            colony.RebuildDerived();
            ulong before = colony.World.ComputeStateHash().Value;
            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            stream.Position = 0;
            ColonyWorld fresh = Colony();
            WorldSave.Load(fresh.World, stream, fresh.SaveComponents);
            fresh.RebuildDerived();

            Assert.That(fresh.World.ComputeStateHash().Value, Is.EqualTo(before));
            fresh.World.Tick();
            Assert.That(fresh.World.Views.Current.TryGetPawnAspect(bandit.Id, PrisonAspects.Mode, out _), Is.True,
                "and her pane still has a mode to show");
        }

        [Test]
        public void TheHashSeesCustody()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = Bandit(colony);
            colony.World.Tick();
            ulong free = colony.World.ComputeStateHash().Value;
            bandit.Custody = PawnCustody.Prisoner;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(free));
            bandit.Custody = PawnCustody.Free;
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(free), "the control");
            bandit.Prison = new PrisonRecord { Willingness = 1 };
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(free), "and the record");
        }

        // ---- the kind is never rewritten -------------------------------------------------------------

        /// <summary>
        /// <b>Nothing but the loader writes a pawn's kind</b> (design 60 §4a). A recruit is a
        /// bandit whose side reads Colony; a system that rewrote her kind instead would lose the
        /// arrested colonist and the home faction ransom needs. Read from source, because what it
        /// forbids leaves no trace a runtime test could catch until it had already happened.
        /// </summary>
        [Test]
        public void NothingButTheLoaderWritesAPawnsKind()
        {
            string sim = Path.Combine(RepoPaths.Root, "Assets", "Odyssey", "Sim");
            var offenders = Directory.EnumerateFiles(sim, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("PawnKindSection.cs", StringComparison.Ordinal))
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(t => System.Text.RegularExpressions.Regex.IsMatch(
                    t.line, @"\b(?!content\b)[a-z]\w*\.Kind\s*=[^=]") && !t.line.TrimStart().StartsWith("//"))
                .Select(t => $"{Path.GetFileName(t.f)}:{t.i + 1}: {t.line.Trim()}")
                .ToList();
            Assert.That(offenders, Is.Empty);
        }
    }
}
