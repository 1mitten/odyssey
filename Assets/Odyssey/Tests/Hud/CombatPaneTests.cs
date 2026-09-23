#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The inspect pane's fight (design 33 §1, §5f): a corpse named "Corpse of X", a marauder with
    /// none of a colonist's pane, and the Health tab's rows. The shape answers are the model's so
    /// the shell never decides them; these hold the answers lane C gave.
    /// </summary>
    public class CombatPaneTests
    {
        static readonly PawnId Ada = new PawnId(1), Hog = new PawnId(2), Raider = new PawnId(3);

        const int Pool = 100_000;

        /// <summary>A colonist, a hog, a marauder; the three corpses 7 (a colonist), 8 (a hog), 9 (a marauder).</summary>
        static WorldSnapshot Board(int tick = 0)
        {
            WorldSnapshot frame = Frame.Write(tick: tick);
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 600, 800, 800, JobHandle.Wait,
                flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Hog, new CellRef(2, 1, 1), 800, 800, 800, JobHandle.Wander,
                kind: 1, flags: PawnFlags.None));
            frame.AddPawn(new PawnView(Raider, new CellRef(3, 1, 1), 800, 800, 800, JobHandle.AttackMelee,
                kind: 3, flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpMaxKey, Pool));
            frame.AddPawnAspect(new PawnAspect(Raider, CombatAspectNames.HpMaxKey, Pool));

            // Died at 07h on day 3 of Larkspur.
            int died = 2 * GameClock.TicksPerDay + 7 * GameClock.TicksPerHour;
            frame.AddCorpse(new CorpseView(7, new PawnId(11), 0, 42u, new CellRef(5, 6, 2), died, 3, PawnFlags.Person));
            frame.AddCorpse(new CorpseView(8, new PawnId(12), 1, 43u, new CellRef(6, 6, 2), died, 0, PawnFlags.None));
            frame.AddCorpse(new CorpseView(9, new PawnId(13), 3, 44u, new CellRef(7, 6, 2), died, 0,
                PawnFlags.Person | PawnFlags.Hostile));
            return frame;
        }

        static InspectModel Corpse(int id, WorldSnapshot? frame = null)
        {
            var pane = new InspectModel();
            pane.SetCorpse(id);
            pane.Refresh(frame ?? Board());
            return pane;
        }

        // ---- the corpse ------------------------------------------------------------------------

        /// <summary>
        /// A dead colonist keeps her name (design 33 §1: "Corpse of X"): the one the pool dealt her
        /// seed and id, which is the name she wore alive, so the roster and the corpse agree.
        /// </summary>
        [Test]
        public void AColonistsCorpseIsCalledByHerName()
        {
            string name = ColonistNames.Of(42u, new PawnId(11));
            Assert.That(Corpse(7).Title, Is.EqualTo(Registry.Label(InspectModel.CorpseKey) + " of " + name));
        }

        /// <summary>A player's own name for her outlives her, as it outlives a reroll.</summary>
        [Test]
        public void AColonistTheyNamedIsMournedByThatName()
        {
            ColonistNames.Book.Clear();
            try
            {
                ColonistNames.Book.Rename(new PawnId(11), "Marisol");
                Assert.That(Corpse(7).Title, Is.EqualTo(Registry.Label(InspectModel.CorpseKey) + " of Marisol"));
            }
            finally { ColonistNames.Book.Clear(); }
        }

        /// <summary>An animal and a marauder have no names: the kind's word, with its article.</summary>
        [Test]
        public void AnAnimalsAndAMaraudersCorpseAreCalledByTheirKind()
        {
            string corpse = Registry.Label(InspectModel.CorpseKey);
            Assert.That(Corpse(8).Title, Is.EqualTo(corpse + " of a " + Registry.Label("ui.pawn.hog").ToLowerInvariant()));
            Assert.That(Corpse(9).Title, Is.EqualTo(corpse + " of a " + Registry.Label("ui.pawn.marauder").ToLowerInvariant()));
        }

        [Test]
        public void ACorpsesStateLineSaysWhenItDied()
        {
            InspectModel pane = Corpse(7);
            Assert.That(pane.Job, Is.EqualTo(Registry.Label("ui.combat.dead") + " · since 07h, day 3 of Larkspur"));
            // The registry's words, lower-cased, as the living pane says them: a renamed kind is
            // renamed on the corpse too (RegistryTests.TheInspectPaneWritesNoPawnKindItself).
            Assert.That(pane.Subtitle, Is.EqualTo(Registry.Label("ui.pawn.colonist").ToLowerInvariant()), "what it was");
            Assert.That(Corpse(8).Subtitle, Is.EqualTo(Registry.Label("ui.pawn.animal").ToLowerInvariant()));
            Assert.That(Corpse(9).Subtitle, Is.EqualTo(Registry.Label("ui.pawn.hostile").ToLowerInvariant()));
            Assert.That(pane.Subtitle, Is.EqualTo("colonist"), "the words themselves did not change");
        }

        /// <summary>
        /// A corpse that is gone from the frame — no hauling yet, but a load or a later cleanup —
        /// says so rather than wearing the last one's name.
        /// </summary>
        [Test]
        public void ACorpseTheFrameNoLongerCarriesSaysSo()
        {
            InspectModel pane = Corpse(7);
            pane.SetCorpse(99);
            pane.Refresh(Board());
            Assert.That(pane.Title, Is.EqualTo(Registry.Label(InspectModel.CorpseKey)));
            Assert.That(pane.Job, Is.Empty);
        }

        /// <summary>
        /// The pane is one long-lived model, and a corpse's three strings are composed once per
        /// selection. Before the fix (review, 2026-09-23) "once" outlived the selection: click a
        /// corpse, then a colonist or a cell, then the same corpse, and the corpse's pane wore the
        /// colonist's name, kind and activity, because the corpse cache still said it had written
        /// them. The control is the colonist's own pane in between, which must differ.
        /// </summary>
        [Test]
        public void ACorpseChosenAgainAfterSomethingElseIsNamedAgain()
        {
            WorldSnapshot frame = Board();
            InspectModel pane = Corpse(7, frame);
            string title = pane.Title, subtitle = pane.Subtitle, job = pane.Job;

            pane.SetColonist(Ada);
            pane.Refresh(frame);
            Assert.That(pane.Title, Is.Not.EqualTo(title), "the control: the colonist's pane wrote its own name");
            pane.SetCorpse(7);
            pane.Refresh(frame);
            Assert.That((pane.Title, pane.Subtitle, pane.Job), Is.EqualTo((title, subtitle, job)), "after a colonist");

            pane.SetCell(new CellRef(1, 1, 1));
            pane.Refresh(frame);
            pane.SetCorpse(7);
            pane.Refresh(frame);
            Assert.That((pane.Title, pane.Subtitle, pane.Job), Is.EqualTo((title, subtitle, job)), "after a cell");

            pane.ClearSelection();
            pane.Refresh(frame);
            pane.SetCorpse(7);
            pane.Refresh(frame);
            Assert.That((pane.Title, pane.Subtitle, pane.Job), Is.EqualTo((title, subtitle, job)), "after nothing");
        }

        [Test]
        public void ACorpseHasNoLivingPawnsPaneAndAMaraudersCorpseWearsTheCorpseBadge()
        {
            foreach (int id in new[] { 7, 8, 9 })
            {
                InspectModel pane = Corpse(id);
                Assert.That(pane.ShowsFace || pane.ShowsColonistBody || pane.ShowsTabBox, Is.False, $"corpse {id}");
                Assert.That(pane.AvatarKey, Is.EqualTo(InspectModel.CorpseKey), $"corpse {id}");
                Assert.That(pane.Tabs, Is.Empty);
                Assert.That(pane.Commands, Is.Empty);
            }
        }

        // ---- the marauder ------------------------------------------------------------------------

        /// <summary>
        /// A marauder is a person and not ours (design 33 §1, §5c): no face or portrait, no needs,
        /// no skills, no Health tab and no Draft button — none of which it has, or could be given.
        /// The colonist on the same frame is the control.
        /// </summary>
        [Test]
        public void AMaraudersPaneHasNoNeedsSkillsHealthTabOrDraftButton()
        {
            WorldSnapshot frame = Board();
            var pane = new InspectModel();

            pane.SetColonist(Ada);
            pane.Refresh(frame);
            Assert.That(pane.ShowsColonistBody && pane.ShowsFace && pane.ShowsTabBox, Is.True, "the control");
            Assert.That(Names(pane.Tabs), Does.Contain("Health"));
            Assert.That(Keys(pane.Commands), Does.Contain(InspectModel.DraftKey));

            pane.SetColonist(Raider);
            pane.Refresh(frame);
            Assert.That(pane.ShowsFace, Is.False, "a marauder wore a colonist's face");
            Assert.That(pane.ShowsColonistBody, Is.False, "a marauder had needs");
            Assert.That(pane.ShowsTabBox, Is.False, "a marauder had a tab box");
            Assert.That(pane.Tabs, Is.Empty, "a marauder had tabs");
            Assert.That(pane.Skills, Is.Empty, "a marauder had skills");
            Assert.That(pane.Commands, Is.Empty, "a marauder could be drafted");
            Assert.That(pane.AvatarKey, Is.EqualTo("ui.pawn.marauder"));
            Assert.That(pane.Title, Is.EqualTo(Registry.Label("ui.pawn.marauder")));
            Assert.That(pane.Subtitle, Is.EqualTo("hostile"));
            Assert.That(pane.Job, Is.EqualTo(Registry.Label("ui.status.fighting")));
            Assert.That(pane.Layer, Is.EqualTo(1));
        }

        /// <summary>
        /// A pawn that leaves the frame keeps the pane open, greyed (design 09 §2.3) — and keeps
        /// the shape it had. Before this, a hog or a marauder that died fell to the colonist's
        /// tombstone and grew a Health tab and a Draft button for the grace frames.
        /// </summary>
        [Test]
        public void AMarauderOrAnimalThatLeavesTheFrameKeepsItsShape()
        {
            foreach (PawnId id in new[] { Raider, Hog })
            {
                var pane = new InspectModel();
                pane.SetColonist(id);
                pane.Refresh(Board());
                pane.Refresh(Frame.Write());

                Assert.That(pane.Tombstoned, Is.True, $"pawn {id.Value}");
                Assert.That(pane.Tabs, Is.Empty, $"pawn {id.Value} grew tabs");
                Assert.That(pane.Commands, Is.Empty, $"pawn {id.Value} grew commands");
                Assert.That(pane.ShowsFace || pane.ShowsColonistBody, Is.False, $"pawn {id.Value}");
            }

            // The control: a colonist's tombstone is the colonist's pane, greyed.
            var colonist = new InspectModel();
            colonist.SetColonist(Ada);
            colonist.Refresh(Board());
            colonist.Refresh(Frame.Write());
            Assert.That(colonist.Tabs, Is.Not.Empty);
        }

        // ---- the Health tab ----------------------------------------------------------------------

        static InspectModel Colonist(WorldSnapshot frame)
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(frame);
            return pane;
        }

        static string Row(InspectModel pane, string key)
        {
            string name = Registry.Label(key);
            foreach (InspectRow row in pane.HealthRows) if (row.Name == name) return row.Value;
            Assert.Fail($"no {name} row");
            return string.Empty;
        }

        /// <summary>
        /// A whole colonist publishes the pool and no hit points (design 33 §5d), and her tab says
        /// "100 / 100", unhurt, bare hands.
        /// </summary>
        [Test]
        public void TheHealthTabOfAWholeColonist()
        {
            InspectModel pane = Colonist(Board());
            Assert.That(pane.HealthValue, Is.EqualTo("100 / 100"));
            Assert.That(pane.HealthPerMille, Is.EqualTo(1000));
            Assert.That(Row(pane, "ui.combat.condition"), Is.EqualTo(Registry.Label("ui.combat.unhurt")));
            Assert.That(Row(pane, "ui.combat.weapon"), Is.EqualTo(Registry.Label("ui.combat.barehands")));
            Assert.That(pane.HealthInk, Is.EqualTo(HudTheme.Good));
        }

        [Test]
        public void TheHealthTabOfAHurtArmedColonist()
        {
            WorldSnapshot frame = Board();
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpKey, 37_200));
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.WeaponKey, ItemHandle.Machete));
            InspectModel pane = Colonist(frame);

            // Up to the next whole point: a colonist on her feet never reads nought.
            Assert.That(pane.HealthValue, Is.EqualTo("38 / 100"));
            Assert.That(pane.HealthPerMille, Is.EqualTo(372));
            Assert.That(Row(pane, "ui.combat.condition"), Is.EqualTo(Registry.Label("ui.combat.hurt")));
            Assert.That(Row(pane, "ui.combat.weapon"), Is.EqualTo(Registry.Label("ui.item.machete")));
            Assert.That(pane.HealthInk, Is.EqualTo(HudTheme.Bad));
        }

        [Test]
        public void TheHealthTabOfADownedAndOfAStunnedColonist()
        {
            WorldSnapshot down = Frame.Write();
            down.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 600, 800, 800, JobHandle.Downed,
                flags: PawnFlags.Person | PawnFlags.Downed));
            down.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpMaxKey, Pool));
            down.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpKey, -8_000));
            InspectModel pane = Colonist(down);
            Assert.That(pane.HealthValue, Is.EqualTo("0 / 100"), "below nought reads as nought");
            Assert.That(pane.HealthPerMille, Is.Zero);
            Assert.That(Row(pane, "ui.combat.condition"), Is.EqualTo(Registry.Label("ui.status.downed")));

            WorldSnapshot stunned = Frame.Write();
            stunned.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 600, 800, 800, JobHandle.Wait,
                flags: PawnFlags.Person | PawnFlags.Stunned));
            stunned.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpMaxKey, Pool));
            stunned.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpKey, 90_000));
            Assert.That(Row(Colonist(stunned), "ui.combat.condition"), Is.EqualTo(Registry.Label("ui.combat.stunned")));
        }

        /// <summary>
        /// A frame from a world with no pool published at all (a view built before combat) says
        /// nothing rather than "0 / 0".
        /// </summary>
        [Test]
        public void AColonistWithNoPoolPublishedShowsNoNumber()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 600, 800, 800, flags: PawnFlags.Person));
            InspectModel pane = Colonist(frame);
            Assert.That(pane.HealthValue, Is.Empty);
            Assert.That(pane.HealthPerMille, Is.Zero);
        }

        /// <summary>
        /// The pane refreshes fifteen times a second; its health strings are rebuilt when a value
        /// they quote moves and not otherwise, so the same string instance comes back.
        /// </summary>
        [Test]
        public void TheHealthStringsAreNotRebuiltWhenNothingMoved()
        {
            WorldSnapshot frame = Board();
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.HpKey, 37_200));
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(frame);
            string first = pane.HealthValue;
            pane.Refresh(frame);
            Assert.That(ReferenceEquals(first, pane.HealthValue), Is.True);
        }

        static List<string> Names(List<InspectTab> tabs)
        {
            var names = new List<string>();
            foreach (InspectTab tab in tabs) names.Add(tab.Name);
            return names;
        }

        static List<string> Keys(List<InspectCommand> commands)
        {
            var keys = new List<string>();
            foreach (InspectCommand command in commands) keys.Add(command.IconKey);
            return keys;
        }
    }
}
