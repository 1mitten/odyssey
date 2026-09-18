#nullable enable
using System.Runtime.CompilerServices;

// The walk seam on a pawn — Destination, PathPending, AdoptPath — is internal on purpose: a walk
// is asked for by a job, served by the movement system, and nothing outside that pair has
// business installing one. The simulation tests stand on the other side of that line, and the
// rate-seam tests (WS1) must be able to stand a pawn on a path to measure what a rate does to
// the walk — the position the HUD tests are in, granted here explicitly rather than by making
// the seam public to everyone.
[assembly: InternalsVisibleTo("Odyssey.Tests.Sim")]
