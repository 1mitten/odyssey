#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// A colonist's response to danger on her pane (design 33 §18e): the button beside Draft shows
    /// the response she has, off the published aspect, and a press moves every selected colonist to
    /// the one after the first one's. The owner, 2026-09-24: <i>"Maybe a setting to configure
    /// this"</i>.
    /// </summary>
    public class ResponseModelTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Raider = new PawnId(10), Hog = new PawnId(11);

        /// <summary>Ada and Bo, colonists, at the responses given (0 unpublished); a bandit and a hog.</summary>
        static WorldSnapshot Frame(int ada = 0, int bo = 0)
        {
            WorldSnapshot frame = Odyssey.Tests.Hud.Frame.Write();
            frame.AddPawn(new PawnView(Ada, new CellRef(1, 1, 1), 800, 800, 700, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(2, 1, 1), 800, 800, 700, JobHandle.Wait, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Raider, new CellRef(5, 1, 1), 800, 800, 700, JobHandle.Wait, kind: 3,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            frame.AddPawn(new PawnView(Hog, new CellRef(6, 1, 1), 800, 800, 700, JobHandle.Wander, kind: 1, flags: PawnFlags.None));
            // Sparse, as the simulation publishes it: nothing at the default.
            if (ada != 0) frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.ResponseKey, ada));
            if (bo != 0) frame.AddPawnAspect(new PawnAspect(Bo, CombatAspectNames.ResponseKey, bo));
            return frame;
        }

        /// <summary>
        /// The response reads off the frame as the number she has, each the registry's name. The
        /// numbers are the simulation's (<c>HostilityResponse</c>), held on the other side by
        /// <c>ResponseTests.TheNumbersAreTheInterfaces</c>.
        /// </summary>
        [TestCase(0, ResponseModel.FightBackKey)]
        [TestCase(1, ResponseModel.DefendKey)]
        [TestCase(2, ResponseModel.FleeKey)]
        public void TheFrameSaysTheResponseSheHas(int response, string key)
        {
            Assert.That(ResponseModel.Of(Frame(ada: response), Ada), Is.EqualTo(response));
            Assert.That(ResponseModel.KeyOf(response), Is.EqualTo(key));
        }

        /// <summary>The registry's three names, as the owner will read them on the button.</summary>
        [Test]
        public void TheThreeNamesAreFightBackDefendAndFlee()
        {
            Assert.That(Registry.Label(ResponseModel.FightBackKey), Is.EqualTo("Fight back"));
            Assert.That(Registry.Label(ResponseModel.DefendKey), Is.EqualTo("Defend"));
            Assert.That(Registry.Label(ResponseModel.FleeKey), Is.EqualTo("Flee"));
        }

        /// <summary>
        /// The pane's header no longer carries the response (design 61): it left for the Assign
        /// tab's Response column, which sets it for every colonist from one table. The control is
        /// Draft, which the header still carries.
        /// </summary>
        [Test]
        public void TheHeaderNoLongerCarriesTheResponse()
        {
            var pane = new InspectModel();
            pane.SetColonist(Ada);
            pane.Refresh(Frame(ada: 1));
            Assert.That(pane.Commands.FindIndex(c => c.IconKey == InspectModel.DraftKey), Is.Not.EqualTo(-1),
                "the control: Draft is on the header");
            Assert.That(pane.Commands.FindIndex(c => ResponseModel.IsResponseKey(c.IconKey)), Is.EqualTo(-1),
                "the response came back to the header");
        }

        /// <summary>A press moves her round the three: Fight back, Defend, Flee, Fight back.</summary>
        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 0)]
        public void APressMovesHerToTheNext(int from, int to)
        {
            var into = new List<Intent>();
            ResponseModel.Cycle(new[] { Ada }, Frame(ada: from), into);
            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Kind, Is.EqualTo(IntentKind.SetHostilityResponse));
            Assert.That(into[0].A, Is.EqualTo(Ada.Value));
            Assert.That(into[0].B, Is.EqualTo(to));
        }

        /// <summary>
        /// A box selection: the next after the <b>first</b> colonist's, for everybody not already at
        /// it; the bandit and the hog passed over wherever they stand in the selection.
        /// </summary>
        [Test]
        public void ASelectionTakesTheFirstColonistsNextAndPassesOverTheRest()
        {
            var into = new List<Intent>();
            ResponseModel.Cycle(new[] { Raider, Hog, Ada, Bo }, Frame(ada: 0, bo: 2), into);
            Assert.That(into.Count, Is.EqualTo(2), "one intent a colonist");
            Assert.That(into[0].A, Is.EqualTo(Ada.Value));
            Assert.That(into[1].A, Is.EqualTo(Bo.Value));
            Assert.That(into[0].B, Is.EqualTo(ResponseModel.Defend), "not the next after Ada's, the first colonist's");
            Assert.That(into[1].B, Is.EqualTo(ResponseModel.Defend), "Bo was not brought to the squad's response");

            into.Clear();
            ResponseModel.Cycle(new[] { Ada, Bo }, Frame(ada: 0, bo: 1), into);
            Assert.That(into.Count, Is.EqualTo(1), "Bo was already on Defend and was sent it again");
            Assert.That(into[0].A, Is.EqualTo(Ada.Value));
        }

        [Test]
        public void NoColonistMeansNothing()
        {
            var into = new List<Intent>();
            ResponseModel.Cycle(new[] { Raider, Hog }, Frame(), into);
            ResponseModel.Cycle(new PawnId[0], Frame(), into);
            Assert.That(into, Is.Empty);
        }

        /// <summary>A number that is not a response reads as the default rather than as nonsense.</summary>
        [Test]
        public void AnUnknownNumberReadsAsFightBack()
        {
            WorldSnapshot frame = Frame();
            frame.AddPawnAspect(new PawnAspect(Ada, CombatAspectNames.ResponseKey, 7));
            Assert.That(ResponseModel.Of(frame, Ada), Is.EqualTo(ResponseModel.FightBack));
        }
    }
}
