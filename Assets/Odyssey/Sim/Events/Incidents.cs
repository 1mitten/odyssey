#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// The events of one colony (design 23): what can happen, what has, and what is in the air.
    /// One object so the composition root attaches it in one call and a save lists its sections
    /// from one place.
    ///
    /// <para><b>What is deliberately not here: a storyteller.</b> Nothing in this class decides
    /// <em>when</em> an incident fires. The one caller today is the debug menu, through
    /// <see cref="IntentKind.InvokeIncident"/>, which fires whatever it is told to regardless of
    /// the Def's gates. A scheduler, when one exists, reads the gates and <see cref="Ledger"/>
    /// and calls <see cref="TryFire"/> — the same door — so an earned event and a forced one
    /// cannot behave differently (design 23 §3).</para>
    /// </summary>
    public sealed class Incidents
    {
        readonly PawnContext _pawns;
        SimWorld? _world;

        public Incidents(PawnContext pawns, IncidentContent content)
        {
            _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Ledger = new IncidentLedger(content, pawns.Size);
            Skyfallers = new Skyfallers(pawns);
        }

        public IncidentContent Content { get; }
        public IncidentLedger Ledger { get; }
        public Skyfallers Skyfallers { get; }

        /// <summary>
        /// Join a world: the things in the air tick and publish, the ledger is hashed and
        /// published, and the invoke command is claimed. The world itself arrives through the
        /// tickable factory, which is the one place the builder hands it out.
        /// </summary>
        public void Attach(SimWorldBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder
                .AddTickable(world =>
                {
                    _world = world;
                    return Skyfallers;
                })
                .AddHashable(Ledger)
                .AddSnapshotContributor(Ledger)
                .AddSnapshotContributor(Skyfallers)
                .AddIntentHandler(IntentKind.InvokeIncident, HandleInvoke);
        }

        /// <summary>
        /// <c>InvokeIncident(A = incident def, B = size, C = mix + 1)</c>. B and C are the raid's
        /// (design 53 §9): 0 in either leaves it to the incident, so every other caller, which sends
        /// neither, is unchanged. A bad index, a size past the pawn ceiling or a mix the content does
        /// not have is <see cref="IntentRejection.OutOfBounds"/>; a world that cannot take the event
        /// right now is <see cref="IntentRejection.NotPermitted"/>, which for the supply drop means no
        /// column on the board can land it and for a raid that the band would not fit.
        /// </summary>
        public IntentRejection HandleInvoke(Intent intent)
        {
            int def = intent.A;
            if (def < 0 || def >= Content.Count) return IntentRejection.OutOfBounds;
            int size = intent.B;
            if (size < 0 || size > PawnRegistry.PawnCeiling) return IntentRejection.OutOfBounds;
            int mix = intent.C - 1;
            if (mix < -1 || mix >= Content.Mixes.Length) return IntentRejection.OutOfBounds;
            return TryFire(new IncidentParms(def, size, null, mix)) ? IntentRejection.None : IntentRejection.NotPermitted;
        }

        /// <summary>Could this incident fire right now? Changes nothing.</summary>
        public bool CanFire(in IncidentParms parms) =>
            Content.Workers[parms.Def].CanFireNow(Context(), parms);

        /// <summary>Fire it, if the world can take it. True when it happened.</summary>
        public bool TryFire(in IncidentParms parms)
        {
            IncidentContext ctx = Context();
            IncidentWorker worker = Content.Workers[parms.Def];
            return worker.CanFireNow(ctx, parms) && worker.TryExecute(ctx, parms);
        }

        IncidentContext Context()
        {
            if (_world == null)
                throw new InvalidOperationException(
                    "the events have not joined a world yet: Attach runs at Build(), and nothing can fire before it");
            return new IncidentContext(_world, _pawns, Content, Ledger, Skyfallers);
        }
    }

    /// <summary>
    /// Named random purposes for the events, beside <see cref="PawnPurpose"/> and for its
    /// reason: each consumer draws from its own stream, so adding one cannot shift the numbers
    /// another sees.
    ///
    /// <para>Both are the sixth and seventh constants outside the spent xxHash family, taken from
    /// SHA-256's round constants as <see cref="PawnPurpose.MovePace"/> was: the primes are gone,
    /// and distinct families read as the discipline they are. Both draws mix the tick in — see
    /// <see cref="SupplyDropWorker"/> for why a drop is a fact about the moment.</para>
    /// </summary>
    public static class IncidentPurpose
    {
        /// <summary>Which column a supply drop comes down on.</summary>
        public const uint Landing = 0x7137_4491;

        /// <summary>How much of it there is.</summary>
        public const uint Payload = 0xB5C0_FBCF;
    }
}
