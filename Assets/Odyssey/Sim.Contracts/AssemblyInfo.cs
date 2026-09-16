#nullable enable
using System.Runtime.CompilerServices;

// The snapshot writing surface is internal on purpose: presentation must be able to read a
// published frame and must not be able to write one. The simulation needs that access, so it is
// granted explicitly here rather than by making the methods public to everyone.
[assembly: InternalsVisibleTo("Odyssey.Sim")]
[assembly: InternalsVisibleTo("Odyssey.Tests.Sim")]
// The HUD model tests are in the position the seam tests are: they must be able to write a
// frame to feed the models, while the HUD assembly itself keeps read-only access.
[assembly: InternalsVisibleTo("Odyssey.Tests.Hud")]
