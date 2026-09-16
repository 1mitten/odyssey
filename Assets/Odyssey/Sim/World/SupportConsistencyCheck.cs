#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Worldgen's pass-10 structural assertion (docs/design/02-world-and-layers.md section 6,
    /// section 4): does the finished map hold itself up by the ordinary support rule alone, with
    /// no stamped-by-construction trust to lean on?
    ///
    /// A fresh <see cref="SupportSolver"/> starts with no construction marks, so clearing them is
    /// a formality here rather than a correction — but it is the same call the runtime loader will
    /// make once it seeds trust from a save, and stating it explicitly is what keeps this the one
    /// definition of "does it stand up" rather than a second one that happens to agree today.
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
            string templateId = FindTemplateId(context, cell) ?? "(no template)";
            throw new WorldGenException(
                $"Worldgen consistency check: {collapsed.Count} cell(s) cannot stand on the " +
                $"ordinary support rule alone, starting at ({cell.X}, {cell.Z}, {cell.Y}) in " +
                $"template '{templateId}'. Fix the template in TemplateLibrary.cs, not the solver.");
        }

        /// <summary>Which stamped shell, if any, a column falls inside. Shells never overlap.</summary>
        static string? FindTemplateId(WorldGenContext context, CellRef cell)
        {
            for (int s = 0; s < context.Shells.Count; s++)
            {
                var shell = context.Shells[s];
                var template = context.Templates[shell.TemplateIndex];
                if (cell.X < shell.X0 || cell.X >= shell.X0 + template.SizeX) continue;
                if (cell.Z < shell.Z0 || cell.Z >= shell.Z0 + template.SizeZ) continue;
                return template.Id;
            }
            return null;
        }
    }
}
