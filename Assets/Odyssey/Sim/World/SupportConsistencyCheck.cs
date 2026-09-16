#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Worldgen pass 10's structural assertion: the finished map is judged by the ordinary support
    /// rule with every construction-trust mark revoked, and it is left in a state the first tick
    /// agrees with.
    ///
    /// <para><b>Why the trust is revoked.</b> Worldgen stamps pre-existing shells "supported by
    /// construction" (<see cref="MapGenDef.constructedSupport"/>, and section 4 of
    /// <c>docs/design/02-world-and-layers.md</c>), which is what lets a shell be stamped in any
    /// order without its half-built state coming down. That trust is also exactly what would hide
    /// a template that cannot hold itself up: it would stand until the first pawn mined something
    /// under it, and the map would then fall on a tick nobody could tie back to a template.
    /// Revoking it asks the honest question — <i>would this stand if it had to earn its
    /// support?</i> — at the one moment when the answer is cheap.</para>
    ///
    /// <para><b>Shedding.</b> A map with damage in it cannot answer yes, and should not have to.
    /// The damage pass removes walls that were holding slabs up, and a ruin that has stood for
    /// decades has already dropped whatever those walls carried; measured on the slice map, that
    /// is 21 to 85 slabs, 0.6% to 2.4% of them, almost all on the top two storeys, and it settles
    /// in two solves. So <see cref="AllowShedding"/> lets the check apply the rule and keep the
    /// result rather than complain about it: what cannot stand comes down here, once, instead of
    /// on tick one. With shedding off — an undamaged map, which is to say the templates as
    /// authored — a single collapse is a content bug and throws.</para>
    ///
    /// <para>Either way the solve is left in place, so the support values the world starts with
    /// are the ones the rule produces, and <see cref="SupportSystem"/>'s first tick has nothing to
    /// correct. The fix for a failure is a pillar under the span that fell, in
    /// <see cref="TemplateLibrary"/>. It is never a change to <see cref="SupportSolver"/>, which
    /// the incremental-versus-full oracle tests pin, nor a higher
    /// <see cref="MapGenDef.constructedSupport"/>, which would only re-hide it.</para>
    /// </summary>
    public class SupportConsistencyCheck : IStructuralConsistencyCheck
    {
        /// <summary>
        /// Settling converges in two solves on every measured map: the first sheds, the second
        /// confirms. More than this is a solver that disagrees with itself, not a lively ruin.
        /// </summary>
        public const int MaxSettleRounds = 8;

        readonly int _maxSupport;

        public SupportConsistencyCheck(bool allowShedding = true,
                                       int maxSupport = SupportSolver.DefaultMaxSupport)
        {
            if (maxSupport < 1 || maxSupport > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maxSupport));
            AllowShedding = allowShedding;
            _maxSupport = maxSupport;
        }

        /// <summary>
        /// Whether a slab that cannot stand may be dropped (a damaged map) or is a generation
        /// error (an undamaged one, where the template itself is at fault).
        /// </summary>
        public bool AllowShedding { get; }

        public virtual void Verify(CellGrid grid, WorldGenContext context)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var solver = new SupportSolver(grid, _maxSupport);
            solver.ClearAllConstructionMarks();

            int rounds = 0, shed = 0;
            while (true)
            {
                var collapsed = solver.SolveFull();
                rounds++;

                if (collapsed.Count == 0) break;

                if (!AllowShedding)
                {
                    // Report the lowest-indexed collapse rather than a count: a cascade has one
                    // cause and many symptoms, and the cause is nearest the bottom of the map.
                    var first = collapsed[0];
                    throw new WorldGenException(
                        $"The generated map does not stand up. {collapsed.Count} slab(s) collapsed under " +
                        $"the ordinary support rule with construction trust revoked; the first is " +
                        $"{Describe(first)} in {OwnerOf(context, first)}. Seed {context.Seed}, {context.Size}. " +
                        "The fix is a pillar under the span in TemplateLibrary, never a change to SupportSolver.");
                }

                shed += collapsed.Count;

                if (rounds >= MaxSettleRounds)
                    throw new WorldGenException(
                        $"The generated map did not settle in {MaxSettleRounds} solves — {collapsed.Count} " +
                        $"slab(s) were still coming down, the first at {Describe(collapsed[0])} in " +
                        $"{OwnerOf(context, collapsed[0])}. A full solve is meant to reach a fixed point; " +
                        "this is a SupportSolver bug, not a content one. Seed " + context.Seed + ".");
            }

            context.Report.SettledSlabs = shed;
            context.Report.SettleRounds = rounds;
        }

        static string Describe(CellRef cell) => $"({cell.X}, {cell.Z}, {cell.Y})";

        /// <summary>
        /// Which stamped shell covers a cell, by template id. Linear over the shell list because
        /// this runs once, on the way to throwing.
        /// </summary>
        static string OwnerOf(WorldGenContext context, CellRef cell)
        {
            for (int s = 0; s < context.Shells.Count; s++)
            {
                var shell = context.Shells[s];
                var template = context.Templates[shell.TemplateIndex];

                if (cell.X < shell.X0 || cell.X > shell.X0 + template.SizeX - 1) continue;
                if (cell.Z < shell.Z0 || cell.Z > shell.Z0 + template.SizeZ - 1) continue;

                int bottom = context.GroundLayer + template.BottomLayer;
                int top = context.GroundLayer + template.HighestLayer;
                if (cell.Y < bottom || cell.Y > top) continue;

                return $"template '{template.Id}' stamped at ({shell.X0}, {shell.Z0}), local cell " +
                       $"({cell.X - shell.X0}, {cell.Z - shell.Z0}, layer {cell.Y - context.GroundLayer})";
            }

            return "no stamped shell — the strata, vault or street passes put it there";
        }
    }
}
