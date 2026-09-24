#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The board, the orders and the death the weapons lane's tests share (design 33 §6D).
    ///
    /// <para><b>A death here is lane A's death, done by hand</b>: the corpse written, the hook
    /// raised, the job ended and the pawn despawned, in the order <c>docs/plans/combat-contracts.md</c>
    /// gives lane A. Lane D's listener is what the tests are about, and it hears exactly what a
    /// real death would say to it; the fight itself is not needed to prove where a weapon lands.</para>
    /// </summary>
    internal static class WeaponFixture
    {
        public static readonly GridSize Size = new GridSize(60, 60, 16);

        public static ColonyWorld Board(int colonists = 2, uint seed = 7u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: false);
        }

        public static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        /// <summary><c>OrderEquip(cell, A = pawn, B = thing)</c>, aimed at where the thing lies.</summary>
        public static IntentRejection Equip(ColonyWorld colony, Pawn pawn, ColonyItem item)
        {
            int at = colony.Pawns.WhereIs(item);
            CellRef cell = at >= 0 ? Size.FromIndex(at) : Size.FromIndex(pawn.Cell);
            return Send(colony, new Intent(IntentKind.OrderEquip, cell, pawn.Id.Value, item.Id.Value));
        }

        /// <summary>A pawn of a kind, spawned the way the debug menu does it, beside the first colonist.</summary>
        public static Pawn SpawnKind(ColonyWorld colony, int kind)
        {
            Pawn near = colony.Pawns.Pawns.All[0];
            int before = colony.Pawns.Pawns.Count;
            Assert.That(Send(colony, new Intent(IntentKind.SpawnPawn, Size.FromIndex(near.Cell), kind)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(before + 1));
            return colony.Pawns.Pawns.All[colony.Pawns.Pawns.Count - 1];
        }

        /// <summary>
        /// Lay one of <paramref name="def"/> on the ground about <paramref name="away"/> cells east
        /// of <paramref name="pawn"/>, on a cell she can walk to.
        /// </summary>
        public static ColonyItem PutDown(ColonyWorld colony, Pawn pawn, int def, int away = 6)
        {
            PawnContext ctx = colony.Pawns;
            CellRef at = Size.FromIndex(pawn.Cell);
            int x = Math.Min(at.X + away, Size.SizeX - 2);
            int column = ctx.Cells.NearestWalkableInColumn(x, at.Z, at.Y);
            Assume.That(column, Is.GreaterThanOrEqualTo(0), "nowhere east to put it");
            int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, column, def, 1, maxRadius: 8);
            Assume.That(cell, Is.GreaterThanOrEqualTo(0), "no room east to put it");
            ThingId id = ctx.Items.Spawn(def, cell);
            return ctx.Items.Get(id)!;
        }

        /// <summary>Tick until <paramref name="done"/> holds or the budget runs out; true if it held.</summary>
        public static bool RunUntil(ColonyWorld colony, Func<bool> done, int maxTicks)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return true;
                colony.World.Tick();
            }
            return done();
        }

        /// <summary>
        /// Lane A's death, by hand (<c>docs/plans/combat-contracts.md</c>, lane A): the corpse, the
        /// <c>Died</c> hook, the job ended and the pawn despawned, in that order. Returns the corpse.
        /// </summary>
        public static Corpse Kill(ColonyWorld colony, Pawn pawn)
        {
            PawnContext ctx = colony.Pawns;
            int tick = colony.World.CurrentTick;
            int corpse = ctx.Corpses.Add(pawn, tick, facing: 0);
            ctx.CombatHooks.RaiseDied(pawn, null, corpse, tick);
            colony.Jobs.EndJob(pawn, JobStatus.Failed);
            ctx.Pawns.Despawn(pawn);
            Assert.That(ctx.Corpses.TryGet(corpse, out Corpse written), Is.True);
            return written;
        }

        /// <summary>Take whatever lies on this cell away, so a test can say where a drop lands.</summary>
        public static void ClearGround(ColonyWorld colony, int cell)
        {
            ColonyItem? here = colony.Pawns.Items.ItemAt(cell);
            if (here != null) colony.Pawns.Items.Despawn(here);
        }

        public static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;
    }
}
