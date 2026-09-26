#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// Incident def indices, as <see cref="BulletinView"/> carries them, turned into icon keys
    /// and words. Parallel to <see cref="IncidentHandle"/> exactly as <see cref="ItemLabels"/> is
    /// parallel to the item handles, and for the same reason: the real table lives where this
    /// assembly cannot see it (ADR 0003), and presentation is where an index becomes a name. The
    /// words come from <see cref="Registry"/>; this class knows which key an incident is, never
    /// what the key is called.
    ///
    /// <para><b>Two spellings of one list, held together by a test.</b> Each incident Def carries
    /// its own <c>bulletinKey</c>, and <c>RegistryTests</c> reads the Def file and requires the
    /// set of keys there to be the set here — the same bargain <see cref="JobLabels.CarryingAspect"/>
    /// makes about an aspect name, because this assembly cannot import the Def.</para>
    /// </summary>
    public static class IncidentLabels
    {
        /// <summary>Parallel to <see cref="IncidentHandle"/>: the supply drop.</summary>
        public static readonly string[] Keys =
        {
            "ui.bulletin.supplydrop",
            "ui.bulletin.scrapdrop",
            // Written down by the world when a bandit leaves the board (design 33 §17).
            "ui.bulletin.theft",
            "ui.bulletin.banditleft",
            "ui.bulletin.medicaldrop",
            // A band walking in from one edge (design 55): its Events row is the warning.
            "ui.bulletin.raidincoming",
            // The prison's four (design 59), recorded when they happen.
            "ui.bulletin.recruited",
            "ui.bulletin.escaped",
            "ui.bulletin.surrendered",
            "ui.bulletin.arrested",
        };

        /// <summary>The key for an incident the table does not know, so the fault is visible on screen.</summary>
        public const string Unknown = "ui.bulletin.crash";

        public static string IconKey(int incidentDef) =>
            incidentDef >= 0 && incidentDef < Keys.Length ? Keys[incidentDef] : Unknown;

        public static string Label(int incidentDef) => Registry.Label(IconKey(incidentDef));
    }
}
