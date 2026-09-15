#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Sim
{
    sealed class RecordingSystem : IWorldSystem
    {
        readonly List<string> _log;

        public RecordingSystem(string name, TickPhase phase, int order, List<string> log)
        {
            Name = name;
            Phase = phase;
            Order = order;
            _log = log;
        }

        public string Name { get; }
        public TickPhase Phase { get; }
        public int Order { get; }
        public int Ticks { get; private set; }

        public void Tick(SimWorld world)
        {
            Ticks++;
            _log.Add(Name);
        }
    }

    public class WorldSystemTests
    {
        static SimWorld Build(List<string> log, params (string name, TickPhase phase, int order)[] systems)
        {
            var builder = new SimWorldBuilder().WithSeed(1).WithSize(new GridSize(4, 4, 2));
            foreach (var (name, phase, order) in systems)
                builder.AddSystem(_ => new RecordingSystem(name, phase, order, log));
            return builder.Build();
        }

        [Test]
        public void SystemsRunEveryTickInTheirPhase()
        {
            var log = new List<string>();
            var world = Build(log, ("a", TickPhase.WorldSystems, 0));
            world.Tick(3);
            Assert.That(log, Is.EqualTo(new[] { "a", "a", "a" }));
        }

        [Test]
        public void WorldSystemsRunBeforePawnSystems()
        {
            // Phase order is the determinism contract: the world settles, then pawns act on it.
            var log = new List<string>();
            var world = Build(log,
                ("pawnish", TickPhase.Pawns, 0),
                ("worldish", TickPhase.WorldSystems, 0));
            world.Tick();
            Assert.That(log, Is.EqualTo(new[] { "worldish", "pawnish" }));
        }

        [Test]
        public void OrderWithinAPhaseIsByOrderThenName()
        {
            // Registration sequence must not matter, or a refactor that moves an AddSystem call
            // would silently change simulation results.
            var log = new List<string>();
            var world = Build(log,
                ("zulu", TickPhase.WorldSystems, 5),
                ("alpha", TickPhase.WorldSystems, 10),
                ("bravo", TickPhase.WorldSystems, 5));
            world.Tick();
            Assert.That(log, Is.EqualTo(new[] { "bravo", "zulu", "alpha" }));
        }

        [Test]
        public void RegistrationOrderDoesNotChangeExecutionOrder()
        {
            var forward = new List<string>();
            Build(forward, ("a", TickPhase.WorldSystems, 1), ("b", TickPhase.WorldSystems, 2)).Tick();

            var reversed = new List<string>();
            Build(reversed, ("b", TickPhase.WorldSystems, 2), ("a", TickPhase.WorldSystems, 1)).Tick();

            Assert.That(reversed, Is.EqualTo(forward));
        }

        [Test]
        public void TheThingsPhaseIsNotOpenToSystems()
        {
            // That phase belongs to the tick-group dispatcher. Saying so loudly is better than
            // letting someone register a system that silently never runs.
            var ex = Assert.Throws<System.ArgumentException>(() =>
                new SimWorldBuilder()
                    .WithSize(new GridSize(4, 4, 2))
                    .AddSystem(_ => new RecordingSystem("nope", TickPhase.Things, 0, new List<string>()))
                    .Build());
            Assert.That(ex!.Message, Does.Contain("Things phase"));
            Assert.That(ex.Message, Does.Contain("ITickable"));
        }

        [Test]
        public void AWorldWithNoSystemsStillTicks()
        {
            var world = new SimWorldBuilder().WithSize(new GridSize(4, 4, 2)).Build();
            Assert.DoesNotThrow(() => world.Tick(10));
            Assert.That(world.Systems.WorldSystems, Is.Empty);
        }

        [Test]
        public void TheScheduleIsVisibleForInspection()
        {
            var log = new List<string>();
            var world = Build(log, ("a", TickPhase.WorldSystems, 0), ("b", TickPhase.Pawns, 0));
            Assert.That(world.Systems.WorldSystems.Count, Is.EqualTo(1));
            Assert.That(world.Systems.PawnSystems.Count, Is.EqualTo(1));
            Assert.That(world.Systems.WorldSystems[0].Name, Is.EqualTo("a"));
        }
    }
}
