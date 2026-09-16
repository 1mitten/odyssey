#nullable enable
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Worldgen's structural gate (docs/design/02-world-and-layers.md section 4, pass 10): does the
    /// finished map stand up on the ordinary support rule alone, with no credit for having been
    /// stamped that way? Every stamped slab starts life "supported by construction"
    /// (<see cref="SupportSolver.MarkSupportedByConstruction"/> via
    /// <see cref="WorldGenContext.SetSlab"/>'s <c>constructedSupport</c> write), so a fresh
    /// <see cref="SupportSolver"/> — which has never been told to trust anything — is exactly the
    /// judge this pass needs: it has to earn its footing from the ground and its neighbours or it
    /// falls.
    ///
    /// A collapse here is a content bug — a template with a span wider than <c>S_max</c> and no
    /// pillar to break it up — never a solver bug: <c>SupportSolverTests</c> is what proves the
    /// solver itself, against random edits, not this check.
    /// </summary>
    public sealed class SupportConsistencyCheck : IStructuralConsistencyCheck
    {
        public void Verify(CellGrid grid, WorldGenContext context)
        {
            var solver = new SupportSolver(grid, context.Gen.constructedSupport);
            solver.ClearAllConstructionMarks();
            var collapsed = solver.SolveFull();
            if (collapsed.Count == 0) return;

            var cell = collapsed[0];
            int shellIndex = context.ShellIndexAt(cell.X, cell.Z, cell.Y);
            string templateId = shellIndex >= 0
                ? context.Templates[context.Shells[shellIndex].TemplateIndex].Id
                : "(no stamped shell at this cell)";

            throw new WorldGenException(
                $"Structural consistency failed at ({cell.X}, {cell.Z}, {cell.Y}): " +
                $"{collapsed.Count} cell(s) collapse under the ordinary support rule with no " +
                $"construction trust. Template {templateId} needs a pillar, not a solver change.");
        }
    }
}
