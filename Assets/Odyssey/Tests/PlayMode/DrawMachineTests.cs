#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The draw end to end (design 41 §5, §6): Gamble chosen on the real page, each colonist pulled
    /// and stopped through the machine's own button, and the colony built from what landed. The
    /// fast tier proves the model and the roll; this proves the page, the machine's frame driver,
    /// the bootstrap and <c>ColonyRequest.Profiles</c> are wired to each other.
    /// </summary>
    public class DrawMachineTests
    {
        static IEnumerator Settle()
        {
            for (int frame = 0; frame < 8; frame++) yield return null;
        }

        /// <summary>Frames until the machine reaches a phase, on real time, with a ceiling.</summary>
        static IEnumerator Until(HudShell shell, MachinePhase phase, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (shell.DrawPhase != phase && Time.realtimeSinceStartup < until) yield return null;
        }

        static IEnumerator Spin(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        [UnityTest]
        public IEnumerator AGambleColonyIsWhatTheMachineLandedOn()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                ColonistSelect select = shell.Menu.Colonists!;
                Assert.That(select.SetMode(CreationMode.Gamble, SeedEntry.Draw), Is.True);
                yield return Settle();

                var doc = boot.GetComponent<UIDocument>();
                VisualElement? detail = doc.rootVisualElement.Q(className: "setup__detail--machine");
                Assert.That(detail, Is.Not.Null, "Gamble did not frame the detail pane as a machine");
                Assert.That(shell.Menu.Start(), Is.False, "Start built a colony nobody had pulled");

                for (int slot = 0; slot < ColonistSelect.Slots; slot++)
                {
                    Assert.That(select.Selected, Is.EqualTo(slot), $"the page is not on colonist {slot + 1}");
                    shell.PressDrawAction(); // Pull
                    yield return Settle();
                    Assert.That(shell.DrawPhase, Is.EqualTo(MachinePhase.Spinning), $"colonist {slot + 1} did not spin");
                    Assert.That(select.StateOf(slot), Is.EqualTo(SlotState.Spinning));

                    yield return Spin(0.6f);
                    shell.PressDrawAction(); // Stop
                    yield return Until(shell, MachinePhase.Landed, 12f);
                    Assert.That(shell.DrawPhase, Is.EqualTo(MachinePhase.Landed), $"colonist {slot + 1} never landed");
                    yield return Settle();
                    Assert.That(select.StateOf(slot), Is.EqualTo(SlotState.Landed), "the machine landed and the card was not kept");

                    if (slot + 1 < ColonistSelect.Slots)
                    {
                        Assert.That(shell.Menu.Start(), Is.False, "Start with a colonist still to pull");
                        shell.PressDrawAction(); // Next colonist
                        yield return Settle();
                    }
                }

                var cards = new List<Candidate>(select.Cards);
                Assert.That(shell.Menu.Start(), Is.True, "Start refused three landed colonists");
                yield return Settle();

                Assert.That(boot.Colony, Is.Not.Null, "Start built no world");

                // Starting skills are rolled on the world's first tick (StartingSkillsSystem), and a
                // new colony may not have taken one yet; the traits and the profile were placed with
                // the colonist. Tick once, as ColonistDrawTests does, rather than wait on the clock.
                int tickAtStart = boot.World!.CurrentTick;
                if (tickAtStart == 0) boot.World.Tick();
                var people = new List<Pawn>();
                foreach (Pawn pawn in boot.Colony!.Pawns.Pawns.All) if (pawn.IsPerson) people.Add(pawn);
                Assert.That(people.Count, Is.EqualTo(ColonistSelect.Slots));

                for (int slot = 0; slot < people.Count; slot++)
                {
                    Pawn real = people[slot];
                    Pawn card = ColonistDraw.Roll(cards[slot].Seed, slot, RollProfile.Gamble);
                    Assert.That(real.Profile, Is.EqualTo(RollProfile.Gamble), $"colonist {slot + 1} was not rolled as a gamble");
                    Assert.That(real.RollSeed, Is.EqualTo(cards[slot].Seed));
                    Assert.That(real.Traits, Is.EqualTo(cards[slot].Traits), $"colonist {slot + 1}'s traits are not the ones that landed");
                    string realLevels = string.Join(",", Enumerable.Range(0, SkillIndex.Count).Select(s => real.SkillLevel(s)));
                    string cardLevels = string.Join(",", Enumerable.Range(0, SkillIndex.Count).Select(s => card.SkillLevel(s)));
                    Assert.That(realLevels, Is.EqualTo(cardLevels),
                        $"colonist {slot + 1}'s skills are not the ones that landed: id {real.Id.Value} vs {card.Id.Value}, " +
                        $"tick at start {tickAtStart}, same content {ReferenceEquals(real.Content, card.Content)}, " +
                        $"real xp [{string.Join(",", real.Skills)}] card xp [{string.Join(",", card.Skills)}]");
                }

                Assert.That(select.Mode, Is.EqualTo(CreationMode.Standard), "the gamble outlived the colony it built");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Standard looks as it did: no machine frame, no windows, Keep and Reroll where they were
        /// (the spec's first acceptance line).
        /// </summary>
        [UnityTest]
        public IEnumerator StandardShowsNoneOfTheMachine()
        {
            GameObject root = RigWorld.BuildWithHud(out OdysseyBootstrap boot, out SliceCameraRig _,
                out HudShell shell, buildOnPlay: false);
            try
            {
                yield return Settle();
                shell.Menu.Choose(SessionCommands.NewGameKey);
                yield return Settle();

                var doc = boot.GetComponent<UIDocument>();
                Assert.That(shell.Menu.Colonists!.Mode, Is.EqualTo(CreationMode.Standard));
                Assert.That(doc.rootVisualElement.Q(className: "setup__detail--machine"), Is.Null);
                foreach (VisualElement window in doc.rootVisualElement.Query(className: "draw__window--skill").ToList())
                    Assert.That(window.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "a reel window in Standard");
                foreach (VisualElement rail in doc.rootVisualElement.Query(className: "draw__rail").ToList())
                    Assert.That(rail.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "bulbs in Standard");
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
