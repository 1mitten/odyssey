#nullable enable

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// The worker of an incident that is <b>written down, never fired</b> (design 33 §17): the world
    /// records it in the ledger at the moment it happens — a bandit carrying a stack off the
    /// board, or walking off it empty-handed — and nothing asks for it. So it is on the Events panel
    /// and in the History to come, with its own name, ink and chime, exactly as a fired incident is,
    /// and it is never on the debug menu's Events tab (<see cref="Fireable"/>), where a row would do
    /// nothing. The recorder is the caller of <see cref="IncidentLedger.Record(int, int, int, int, int)"/>,
    /// today <c>Theft.Leave</c>.
    ///
    /// <para>A worker rather than a flag on the Def because every Def names one and the loader
    /// refuses a Def that names none; this is the name a recorded incident gives.</para>
    /// </summary>
    public sealed class RecordedIncidentWorker : IncidentWorker
    {
        public override string Name => "Recorded";

        public override bool Fireable => false;

        public override bool CanFireNow(IncidentContext ctx, in IncidentParms parms) => false;

        public override bool TryExecute(IncidentContext ctx, in IncidentParms parms) => false;
    }
}
