#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The rest of lane C's interface (design 33 §1, §5f): choosing a corpse, the debug menu's
    /// Spawn rows, and the colony-wide surfaces — alerts, the Work tab, the Almanac — reading the
    /// flags so that a bandit is neither a colonist nor an animal.
    /// </summary>
    public class CombatDirectorsTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Raider = new PawnId(3), Hog = new PawnId(4);

        static WorldSnapshot Board(int raiderMood = 800, int raiderFood = 800)
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 800, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Raider, new CellRef(3, 1, 1), raiderFood, 800, raiderMood, JobHandle.AttackMelee,
                kind: 3, flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddCorpse(new CorpseView(7, new PawnId(9), 1, 5u, new CellRef(5, 6, 2), 100, 0, PawnFlags.None));
            return frame;
        }

        // ---- choosing a corpse ------------------------------------------------------------------

        /// <summary>
        /// Lane B's hit-test finds a corpse and calls this (design 33 §5f): a corpse in the frame is
        /// selected and the answer is true; one that is not is refused and the selection is left
        /// alone, so a stale hit cannot empty it.
        /// </summary>
        [Test]
        public void ChoosingACorpseInTheFrameSelectsItAndOneNotInItIsRefused()
        {
            var directors = new HudDirectors(4, 1);
            WorldSnapshot frame = Board();
            directors.Selection.Choose(Ada);

            Assert.That(directors.ChooseCorpse(99, frame), Is.False, "a corpse not in the frame was chosen");
            Assert.That(directors.Selection.Pawn, Is.EqualTo(Ada), "a refused corpse emptied the selection");

            Assert.That(directors.ChooseCorpse(7, frame), Is.True);
            Assert.That(directors.Selection.Corpse, Is.EqualTo(7));
            Assert.That(directors.Selection.HasPawn, Is.False);
            Assert.That(directors.ChooseCorpse(0, frame), Is.False, "nought names no corpse");
        }

        // ---- the Spawn tab -----------------------------------------------------------------------

        static DebugDirector.SpawnRow RowFor(string key)
        {
            foreach (DebugDirector.SpawnRow row in DebugDirector.SpawnRows) if (row.Key == key) return row;
            Assert.Fail($"no Spawn row for {key}");
            return default;
        }

        /// <summary>
        /// The bandit is a pawn of kind 3, spawned as a colonist or an animal is (design 33 §5a);
        /// each weapon is one item, granted as wood is (§5j: no simulation work). The colonist's row
        /// is the control on the kind.
        /// </summary>
        [Test]
        public void TheSpawnTabPutsABanditAndOneOfEachWeaponNearTheCamera()
        {
            var anchor = new CellRef(4, 5, 1);

            Intent bandit = RowFor("ui.debug.spawnbandit").ToIntent(anchor);
            Assert.That(bandit.Kind, Is.EqualTo(IntentKind.SpawnPawn));
            Assert.That(bandit.A, Is.EqualTo(PawnKindLabels.Bandit));
            Assert.That(PawnKindLabels.IconKey(PawnKindLabels.Bandit), Is.EqualTo("ui.pawn.bandit"));
            Assert.That(bandit.Cell, Is.EqualTo(anchor));
            Assert.That(RowFor(DebugDirector.SpawnPawnKey).ToIntent(anchor).A, Is.Zero, "the control: a colonist is kind 0");

            var weapons = new Dictionary<string, int>
            {
                ["ui.debug.spawnbat"] = ItemHandle.Bat,
                ["ui.debug.spawncrowbar"] = ItemHandle.Crowbar,
                ["ui.debug.spawnmachete"] = ItemHandle.Machete,
                ["ui.debug.spawnarcblade"] = ItemHandle.ArcBlade,
            };
            foreach (KeyValuePair<string, int> weapon in weapons)
            {
                Intent grant = RowFor(weapon.Key).ToIntent(anchor);
                Assert.That(grant.Kind, Is.EqualTo(IntentKind.GiveResource), weapon.Key);
                Assert.That(grant.A, Is.EqualTo(weapon.Value), weapon.Key);
                Assert.That(grant.B, Is.EqualTo(1), $"{weapon.Key}: one to a stack, one at a time");
                Assert.That(grant.Cell, Is.EqualTo(anchor));
            }
        }

        [Test]
        public void EverySpawnRowIsNamedInTheRegistryAndSaysWhatItDoes()
        {
            var keys = new HashSet<string>(DebugDirector.IconKeys);
            foreach (DebugDirector.SpawnRow row in DebugDirector.SpawnRows)
            {
                Assert.That(Registry.Label(row.Key), Is.Not.EqualTo(row.Key), $"{row.Key} is not in the registry");
                Assert.That(keys, Does.Contain(row.Key), $"{row.Key} is not in DebugDirector.IconKeys");
                Assert.That(row.Tooltip, Is.Not.Empty, row.Key);
            }
            Assert.That(DebugDirector.SpawnRows.Length, Is.EqualTo(17),
                "colonist, arm-all, hurt, heal, kill, bandit, three bandits, two animals, four weapons, " +
                "three resources and the medkits");
            Assert.That(DebugDirector.SpawnRows[0].Key, Is.EqualTo(DebugDirector.SpawnPawnKey), "the colonist first");
        }

        /// <summary>
        /// The Spawn tab in headed groups (design 33 §9i; owner, 2026-09-24): every row sits under a
        /// heading the tab draws, every heading has rows under it and a registry name, and the rows
        /// run in the headings' order so the table reads top to bottom as the tab does.
        /// </summary>
        [Test]
        public void TheSpawnTabIsGroupedUnderNamedHeadings()
        {
            var groups = new List<string>(DebugDirector.SpawnGroups);
            var keys = new HashSet<string>(DebugDirector.IconKeys);
            int last = -1;
            foreach (DebugDirector.SpawnRow row in DebugDirector.SpawnRows)
            {
                int at = groups.IndexOf(row.Group);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), $"{row.Key} sits under no heading the tab draws");
                Assert.That(at, Is.GreaterThanOrEqualTo(last), $"{row.Key} is out of its heading's order");
                last = at;
            }
            foreach (string group in groups)
            {
                Assert.That(Registry.Label(group), Is.Not.EqualTo(group), $"{group} is not in the registry");
                Assert.That(keys, Does.Contain(group));
                Assert.That(System.Array.Exists(DebugDirector.SpawnRows, r => r.Group == group), Is.True, $"{group} is empty");
            }
        }

        [Test]
        public void TheNewRowsSendWhatTheySay()
        {
            DebugDirector.SpawnRow band = RowFor(DebugDirector.SpawnBanditsKey);
            Assert.That(band.Kind, Is.EqualTo(IntentKind.SpawnPawn));
            Assert.That(band.A, Is.EqualTo(PawnKindLabels.Bandit));
            Assert.That(band.Repeat, Is.EqualTo(3));
            Assert.That(band.Group, Is.EqualTo(DebugDirector.GroupHostilesKey));

            DebugDirector.SpawnRow arm = RowFor(DebugDirector.ArmColonistsKey);
            Assert.That(arm.Kind, Is.EqualTo(IntentKind.DebugArmColonists));
            Assert.That(arm.Group, Is.EqualTo(DebugDirector.GroupColonistsKey));

            DebugDirector.SpawnRow wood = RowFor(DebugDirector.GiveWoodKey);
            Assert.That(wood.Kind, Is.EqualTo(IntentKind.GiveResource));
            Assert.That(wood.A, Is.EqualTo(ItemHandle.Wood));
            Assert.That(wood.B, Is.EqualTo(DebugDirector.GiveAmount));
            Assert.That(wood.Group, Is.EqualTo(DebugDirector.GroupItemsKey));
            Assert.That(RowFor(DebugDirector.SpawnBatKey).Repeat, Is.EqualTo(1));
        }

        // ---- alerts, the Work tab, the Almanac ------------------------------------------------------

        /// <summary>
        /// A bandit does not eat (design 33 §5c) and is nobody's to worry about: its food and
        /// mood raise no alert. The same numbers on a colonist are the control.
        /// </summary>
        [Test]
        public void ABanditRaisesNoColonistAlert()
        {
            var alerts = new AlertModel();
            alerts.Refresh(Board(raiderMood: 10, raiderFood: 10), 0.0);
            Assert.That(alerts.Rows, Is.Empty, "a bandit was starving or breaking");

            WorldSnapshot control = Frame.Write();
            control.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 10, 800, 10, JobHandle.Wait, flags: PawnFlags.Person));
            alerts = new AlertModel();
            alerts.Refresh(control, 0.0);
            Assert.That(alerts.Rows.Count, Is.EqualTo(2), "the control: a colonist starving and breaking");
        }

        /// <summary>
        /// "The colony is idle" is about the colony: a hog wandering or a bandit fighting does
        /// not keep it from being said, and the one-colonist form names the colonist.
        /// </summary>
        [Test]
        public void AnIdleColonyIsIdleWhateverTheAnimalsAndBanditsAreDoing()
        {
            WorldSnapshot frame = Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 800, -1, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Raider, new CellRef(3, 1, 1), 800, 800, 800, JobHandle.AttackMelee,
                kind: 3, flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawn(new PawnView(Hog, new CellRef(4, 1, 1), 800, 800, 800, JobHandle.Wander,
                kind: 1, flags: PawnFlags.None));

            var alerts = new AlertModel();
            alerts.Refresh(frame, 0.0);
            alerts.Refresh(frame, AlertModel.IdleSustain + 0.1);

            Assert.That(alerts.Rows.Count, Is.EqualTo(1), "the colony's one colonist is idle");
            Assert.That(alerts.Rows[0].Key, Is.EqualTo(AlertModel.IdleKey));
            Assert.That(alerts.Rows[0].Pawn, Is.EqualTo(Ada), "named the wrong pawn");
        }

        /// <summary>
        /// The Work tab draws what the roster hands it; a bandit handed to it by a stale order is
        /// still left out, because a priority for it would be an intent the simulation refuses.
        /// </summary>
        [Test]
        public void TheWorkTabHasNoRowForABandit()
        {
            var model = new WorkGridModel();
            model.Refresh(Board(), new[] { Ada, Raider, Bo }, null);
            Assert.That(model.TotalRows, Is.EqualTo(2));
        }

        [Test]
        public void TheAlmanacOpensAnAnimalsCorpseOnItsKindAndNothingForABandit()
        {
            var pane = new InspectModel();
            pane.SetCorpse(7);
            pane.Refresh(Board());
            string hog = Registry.Label("ui.pawn.hog");
            Assume.That(AlmanacCatalogue.GetEntry(hog), Is.Not.Null, "the Fauna entry for the hog");
            Assert.That(AlmanacDirector.ResolveSelection(pane), Is.EqualTo(("Fauna", hog)));

            pane.SetColonist(Raider);
            pane.Refresh(Board());
            Assert.That(AlmanacDirector.ResolveSelection(pane), Is.Null,
                "a bandit opened a colonist's Skills entry");
        }

        [Test]
        public void TheAlmanacHasNoEntryForAWeaponRatherThanTheRationPack()
        {
            WorldSnapshot frame = Board();
            frame.AddThing(new ThingView(new ThingId(30), new CellRef(2, 2, 1), ItemHandle.Crowbar, 0));
            var pane = new InspectModel();
            pane.SetItem(new ThingId(30));
            pane.Refresh(frame);
            Assert.That(AlmanacDirector.ResolveSelection(pane), Is.Null);
        }
    }
}
