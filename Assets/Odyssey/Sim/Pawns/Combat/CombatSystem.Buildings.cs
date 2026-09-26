#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // What a blow does to a building (design 33 §13g). The building's ApplySwing: every hit point
    // a building loses is lost in StrikeBuilding and nowhere else. C6's file.
    public partial class CombatSystem
    {
        /// <summary>
        /// Apply one blow to a building — the one method (design 33 §13g).
        /// <b>No experience</b>: a blow at a building trains nothing (design 33 §14c). In order:
        /// <list type="number">
        /// <item><see cref="CombatEventKind.Hit"/> reported against target 0 at
        /// <paramref name="struckCell"/>, with the armament's item def — so the floating number
        /// and the thud a landed blow already has play for a wall;</item>
        /// <item>what is left written into <see cref="EdificeDamage"/> on the building's own
        /// cell;</item>
        /// <item>at nought, <see cref="CombatEventKind.Demolished"/> reported now and the building
        /// demolished at the end of the tick (<see cref="Demolish"/>).</item>
        /// </list>
        /// <b>No hooks</b>: <see cref="DamageReport"/> names a pawn, and every listener is about
        /// pawns. <b>Only the blow that crosses nought demolishes</b>: a blow at a building already
        /// at nought this tick changes and reports nothing.
        ///
        /// <para>Public so a test can land an exact blow; the only caller in the game is the
        /// resolver, inside <see cref="Tick"/>.</para>
        /// </summary>
        public void StrikeBuilding(Pawn? attacker, in BuildingTarget building, int struckCell, in Armament armament,
            in SwingOutcome outcome, int tick)
        {
            int before = BuildingTargets.HpMilli(_ctx, building);
            if (before <= 0 || !outcome.Landed) return;

            // No experience (design 33 §14c; the owner, 2026-09-24): a wall is not a practice
            // dummy. A swing at a pawn still trains, in ApplySwing.

            int weapon = armament.ItemDef;
            int after = before - outcome.DamageMilli;
            _ctx.EdificeDamage.Set(building.Anchor, after);
            PawnId by = attacker?.Id ?? default;
            _ctx.CombatLog.Report(CombatEventKind.Hit, by, default, _ctx.Size.FromIndex(struckCell), tick,
                outcome.DamageMilli, weapon);

            if (after > 0) return;

            _ctx.CombatLog.Report(CombatEventKind.Demolished, by, default, _ctx.Size.FromIndex(building.Anchor), tick,
                building.Edifice, weapon);
            int handle = building.Handle;
            _ctx.Defer(_ => Demolish(handle, tick));
        }

        /// <summary>
        /// A building beaten to nought comes down (design 33 §13g), at the end of the tick for
        /// death's reason — a removal inside the pawn loop edits what the loop is reading — and
        /// <b>through <c>ConstructionGrid.Demolish</c>, the call deconstruction makes</b>, so the
        /// door's navigation flag, the bed index, the power device, a shelf's contents, the chunks,
        /// navigation, support, the ladder connectors, the damage row and any deconstruct order all
        /// go exactly as they do for a building taken apart. <b>No refund</b>: the salvage is
        /// <c>DeconstructJobDriver.TakeApart</c>'s, which is not called. Nothing if it has already
        /// gone by another route in the same tick.
        /// </summary>
        void Demolish(int handle, int tick)
        {
            if (_ctx.Construction == null) return;
            if (!BuildingTargets.TryStanding(_ctx, handle, out BuildingTarget building)) return;
            if (!_ctx.Construction.Demolish(_ctx, building.Anchor, out _)) return;
            LeaveWreck(building, tick);
        }

        /// <summary>
        /// What a building destroyed in a fight leaves (design 53 §2e, the owner: a quarter of its
        /// cost for cover): its row's <see cref="Construction.BuildingDef.wreckRefundPerMille"/> of
        /// the material it was built of, the fraction settled by a seeded draw so it is neither
        /// always lost nor always kept. Nought for every building older than cover, which keeps
        /// design 33's "no refund" for them.
        /// </summary>
        void LeaveWreck(in BuildingTarget building, int tick)
        {
            int row = Construction.ConstructionContent.BuildingForEdifice(building.Edifice);
            if (row == Contracts.BuildingHandle.None) return;
            Construction.BuildingDef def = Construction.ConstructionContent.BuildingAt(row);
            if (def.wreckRefundPerMille <= 0 || def.costCount <= 0) return;
            int stuff = Construction.ConstructionContent.StuffForValue(building.Stuff);
            if (stuff == Contracts.StuffHandle.None) return;
            int item = Construction.ConstructionContent.StuffAt(stuff).item;
            if (item < 0) return;

            long share = (long)def.costCount * def.wreckRefundPerMille;
            int count = (int)(share / 1_000);
            int fraction = (int)(share % 1_000);
            if (fraction > 0)
            {
                var rng = DeterministicRandom.ForTick(_ctx.Seed, (building.Anchor ^ tick) + 104_729, PawnPurpose.DeconstructRefund);
                if (rng.NextInt(1_000) < fraction) count++;
            }
            if (count <= 0) return;
            int landing = _ctx.Cells.FirstFloorAtOrBelow(building.Anchor);
            int at = _ctx.Items.NearestCellWithSpace(_ctx.Cells, landing, item, count, maxRadius: 3);
            if (at >= 0) _ctx.Items.Spawn(item, at, count);
        }
    }
}
