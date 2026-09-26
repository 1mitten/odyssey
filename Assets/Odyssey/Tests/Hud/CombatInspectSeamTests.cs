#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The seams the combat seam review cut into the interface (design 33 §5f): the pane's shape
    /// is the model's answer rather than the shell's, and a corpse can be selected.
    ///
    /// <para><b>These pin the contracts step's values, which reproduce the pane as it was.</b>
    /// Lane C changes <see cref="InspectModel.ShowsFace"/> and the rest for the bandit and fills
    /// the corpse's pane; when it does, it changes the assertions here that name those two and
    /// leaves the colonist's, the animal's, the pile's and the tile's alone.</para>
    /// </summary>
    public class CombatInspectSeamTests
    {
        static WorldSnapshot Board()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 0), 600, 800, 800, JobHandle.Wait,
                flags: PawnFlags.Person));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(3, 1, 0), 800, 800, 600, JobHandle.Wander,
                kind: 1));
            snapshot.AddCorpse(new CorpseView(7, new PawnId(9), 0, 42u, new CellRef(5, 6, 2), 1_000, 3,
                PawnFlags.Person));
            return snapshot;
        }

        /// <summary>
        /// The shape answers as the pane had them before they were the model's: a colonist has a
        /// face, a body and a tab box; an animal a badge and the (empty) box; a pile and a tile
        /// neither. Each value is what HudShell.Inspect used to compute inline.
        /// </summary>
        [Test]
        public void TheShapeAnswersReproduceThePaneAsItWas()
        {
            WorldSnapshot snapshot = Board();
            var pane = new InspectModel();

            pane.SetColonist(new PawnId(1));
            pane.Refresh(snapshot);
            Assert.That(pane.ShowsFace, Is.True);
            Assert.That(pane.ShowsColonistBody, Is.True);
            Assert.That(pane.ShowsTabBox, Is.True);
            Assert.That(pane.AvatarKey, Is.EqualTo(PawnKindLabels.Colonist));

            pane.SetColonist(new PawnId(2));
            pane.Refresh(snapshot);
            Assert.That(pane.ShowsFace, Is.False);
            Assert.That(pane.ShowsColonistBody, Is.False);
            Assert.That(pane.ShowsTabBox, Is.True, "an animal's box was built, empty, before the seam");
            Assert.That(pane.AvatarKey, Is.EqualTo("ui.pawn.hog"));

            pane.SetCell(new CellRef(2, 2, 1));
            pane.Refresh(snapshot);
            Assert.That(pane.ShowsFace || pane.ShowsColonistBody || pane.ShowsTabBox, Is.False);
            Assert.That(pane.AvatarKey, Is.EqualTo(pane.CellIconKey));
        }

        /// <summary>
        /// A corpse is its own subject: the corpse badge, its name, where it lies, and nothing a
        /// living pawn has. The control is the colonist's pane on the
        /// same frame, which has all of it.
        /// </summary>
        [Test]
        public void ACorpseIsASubjectWithNoLivingPawnsPane()
        {
            WorldSnapshot snapshot = Board();
            var pane = new InspectModel();
            pane.SetColonist(new PawnId(1));
            pane.Refresh(snapshot);
            Assert.That(pane.Tabs, Is.Not.Empty, "the control");

            pane.SetCorpse(7);
            pane.Refresh(snapshot);

            Assert.That(pane.Subject, Is.EqualTo(InspectSubject.Corpse));
            Assert.That(pane.Corpse, Is.EqualTo(7));
            Assert.That(pane.Pawn, Is.EqualTo(PawnId.None));
            Assert.That(pane.AvatarKey, Is.EqualTo(InspectModel.CorpseKey), "a corpse wore another badge");
            // Named by lane C (CombatPaneTests holds the three kinds): "Corpse of X".
            Assert.That(pane.Title, Is.EqualTo(Registry.Label(InspectModel.CorpseKey) + " of "
                + ColonistNames.Of(42u, new PawnId(9))));
            Assert.That(pane.Layer, Is.EqualTo(2), "not where it lies");
            Assert.That(pane.ShowsFace || pane.ShowsColonistBody || pane.ShowsTabBox, Is.False);
            Assert.That(pane.Tabs, Is.Empty);
            Assert.That(pane.Commands, Is.Empty, "a corpse offered a command");

            pane.SetColonist(new PawnId(1));
            Assert.That(pane.Corpse, Is.EqualTo(0), "choosing a colonist left the corpse behind");
        }

        /// <summary>
        /// The selection can hold a corpse (design 33 §5f), as one subject, and anything else chosen
        /// after it replaces it — the terms a pile is held on.
        /// </summary>
        [Test]
        public void TheSelectionHoldsACorpseAsOneSubject()
        {
            WorldSnapshot snapshot = Board();
            var selection = new SelectionDirector();
            int changes = 0;
            selection.Changed += _ => changes++;

            selection.Choose(new PawnId(1));
            selection.ChooseCorpse(7);
            Assert.That(selection.HasCorpse, Is.True);
            Assert.That(selection.Corpse, Is.EqualTo(7));
            Assert.That(selection.HasPawn || selection.HasThing || selection.Cell.HasValue, Is.False,
                "a corpse is the whole selection");
            Assert.That(selection.IsEmpty, Is.False);
            Assert.That(changes, Is.EqualTo(2));

            selection.Refresh(snapshot);
            selection.Refresh(snapshot);
            Assert.That(selection.Corpse, Is.EqualTo(7), "a corpse still in the frame was dropped");

            selection.Choose(new PawnId(1));
            Assert.That(selection.HasCorpse, Is.False, "choosing a colonist kept the corpse");

            selection.ChooseCorpse(7);
            selection.Pick(new CellRef(2, 2, 1), PawnId.None, snapshot);
            Assert.That(selection.HasCorpse, Is.False, "a pick elsewhere kept the corpse");

            selection.ChooseCorpse(7);
            selection.Clear();
            Assert.That(selection.HasCorpse, Is.False);
            Assert.That(selection.IsEmpty, Is.True);
        }

        /// <summary>A corpse the frame no longer carries is let go after the grace, as a pile is.</summary>
        [Test]
        public void ACorpseThatLeavesTheFrameIsLetGo()
        {
            var selection = new SelectionDirector();
            SelectionChange? last = null;
            selection.Changed += reason => last = reason;
            selection.ChooseCorpse(7);

            WorldSnapshot empty = Frame.Write();
            for (int i = 0; i <= SelectionDirector.GraceFrames; i++) selection.Refresh(empty);

            Assert.That(selection.HasCorpse, Is.False);
            Assert.That(last, Is.EqualTo(SelectionChange.Died));
        }
    }
}
