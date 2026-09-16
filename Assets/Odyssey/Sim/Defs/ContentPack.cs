#nullable enable
namespace Odyssey.Sim.Defs
{
    /// <summary>
    /// The one place that knows what the core content pack is made of.
    ///
    /// <para>It exists because half a pack is worse than none. The Def types are registered in
    /// three families — the pawn tuning (<see cref="Pawns.PawnContent.Register"/>), the world
    /// tables (<see cref="Worldgen.WorldContent.Register"/>) and what can be built
    /// (<see cref="Construction.ConstructionContent.Register"/>) — and a loader given only some of
    /// them reads the others' files as "unknown Def type" and fails with a list of errors that
    /// blames the content rather than the caller. Every caller that wants the shipped pack asks
    /// here instead, so adding a fourth family is one edit rather than a hunt.</para>
    ///
    /// <para>Nothing in the running game calls this yet: the simulation still builds from the
    /// in-code tables, which remain the oracle the loaded pack is tested against. See
    /// <see cref="Pawns.PawnContent"/> for why that switch is its own change.</para>
    /// </summary>
    public static class ContentPack
    {
        /// <summary>The shipped pack's id, as it appears in an error message.</summary>
        public const string CoreId = "Core";

        /// <summary>Register every Def type the core pack contains.</summary>
        public static DefLoader Register(DefLoader loader) =>
            Construction.ConstructionContent.Register(
                Worldgen.WorldContent.Register(Pawns.PawnContent.Register(loader)));

        /// <summary>Load the core pack from a directory: every <c>.xml</c> beneath it.</summary>
        public static DefDatabase LoadCore(string root) =>
            Register(new DefLoader()).AddSource(new DirectoryDefSource(CoreId, root)).Load();
    }
}
