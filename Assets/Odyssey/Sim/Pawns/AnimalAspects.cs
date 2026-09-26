#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What an animal publishes about itself for the art to draw (plan forest-animals §1), beside
    /// <see cref="AnimalShelterThinkNode.Sheltering"/>. Presentation copies the two names rather
    /// than referencing this assembly, as it does the sheltering one; a test holds the strings.
    /// </summary>
    public static class AnimalAspects
    {
        /// <summary>
        /// <see cref="Pawn.Form"/>, published every tick for an animal whose species has more than
        /// one form (the deer: 0 doe, 1 stag; the moose: 0 cow, 1 bull) and absent for every other
        /// pawn, which reads as form 0. Which mesh a deer wears is the art's; which form it is,
        /// the simulation's, so the rut (design 67 §4) and the drawing cannot disagree.
        /// </summary>
        public const string FormName = "odyssey.pawn.form";

        public static readonly AspectKey Form = AspectKey.Of(FormName);

        /// <summary>
        /// Present, at 1, while an animal is eating — the pack's Eat clip plays while it is
        /// (design 66 §6). Sparse, as sheltering is. Nothing an animal does in FA1 is eating, so
        /// it is never present yet; design 65's feeding and design 67's grazing are the jobs
        /// <see cref="IsEating"/> will name.
        /// </summary>
        public const string GrazingName = "odyssey.pawn.grazing";

        public static readonly AspectKey Grazing = AspectKey.Of(GrazingName);

        /// <summary>
        /// Is this animal eating? The one owner of the question, asked at publish time. False for
        /// everything in FA1: no animal job eats yet.
        /// </summary>
        public static bool IsEating(Pawn pawn) => false;

        /// <summary>Publish both for one animal. Called from the snapshot publish, animals only.</summary>
        public static void Publish(SnapshotWriter writer, Pawn pawn)
        {
            if (pawn.Species.formCount > 1) writer.AddPawnAspect(pawn.Id, Form, pawn.Form);
            if (IsEating(pawn)) writer.AddPawnAspect(pawn.Id, Grazing, 1);
        }
    }
}
