#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// **A colonist may never be drawn behind where the last frame drew her.**
    ///
    /// <para>The owner reported colonists that vibrate "particularly where there is a terrain step
    /// tile ... mostly at the beginning ... when going up" (2026-09-19). The cause was the sub-tick
    /// term: a frame landing between two ticks carries the figure on, and presentation was
    /// inferring the rate to carry it by from a global <c>movePerTick</c> out of the Defs, as
    /// though every step cost <see cref="Odyssey.Sim.Pathing.MoveCost.Orthogonal"/>. It is wrong by
    /// the colonist's own pace and condition, and wrong again by the price of the terrain being
    /// entered. Whenever the guess ran ahead of what the tick retired, the next frame drew her
    /// behind the last one.</para>
    ///
    /// <para><b>Measured on the wooded meadow before the fix</b>, twelve colonists, 2,500 ticks,
    /// two frames to the tick: <b>3,172 frames of 59,000 moved backwards</b>, by up to 10.9 mm,
    /// and 1,406 reversed vertically — the terrace banks turn backward travel into a change of
    /// height, which is why a step tile is where it shows. After: nought of each.</para>
    ///
    /// <para>This runs the real thing — a generated board with real banks, a real colony walking
    /// it — because every cheaper fixture missed it. A hand-built world grows no banks, and the
    /// hand-built <c>PawnView</c>s the other steering tests use publish no rate, so they take the
    /// fallback path rather than the one the game takes.</para>
    /// </summary>
    public class WalkContinuityTests
    {
        /// <summary>Two frames to a tick: a 120 Hz display against the 60 Hz tick.</summary>
        const int FramesPerTick = 2;

        [Test]
        public void NoFrameEverDrawsAColonistBehindWhereTheLastOneDidHer()
        {
            BankLayout.Reset();
            GroundRelief.Reset();

            var size = new GridSize(60, 60, 16);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 8;
            scenario.beds = 8;
            ColonyWorld colony = ColonyWorld.Build(size, seed: 21u, scenario, barren: true, wooded: true);

            var model = new WorldRenderModel(size, new ChunkGrid(size), new ModuleLibrary(null));
            model.RefreshAll(colony.Grid, new List<PlacedEdifice>());

            int banks = 0;
            for (int i = 0; i < size.CellCount; i++)
                if (BankLayout.At(model, size.FromIndex(i)).Exists) banks++;
            Assert.That(banks, Is.GreaterThan(50),
                "this board has no terrace banks on it, so it cannot prove anything about them");

            var lastStep = new Dictionary<int, (CellRef, CellRef)>();
            var lastPos = new Dictionary<int, Vector3>();
            int frames = 0, backwards = 0;
            float worst = 0f;
            string where = string.Empty;

            for (int tick = 0; tick < 900; tick++)
            {
                colony.World.Tick();
                ReadOnlySpan<PawnView> pawns = colony.World.Views.Current.Pawns;

                for (int f = 0; f < FramesPerTick; f++)
                {
                    float alpha = f / (float)FramesPerTick;
                    for (int i = 0; i < pawns.Length; i++)
                    {
                        ref readonly PawnView pawn = ref pawns[i];
                        int key = pawn.Id.Value;
                        if (!pawn.Moving) { lastStep.Remove(key); lastPos.Remove(key); continue; }

                        Vector3 at = PawnPose.Of(in pawn, alpha, 1, out _, model, pawns);

                        // Only within one step. Crossing into the next one turns a corner, and a
                        // corner is not a stumble.
                        bool sameStep = lastStep.TryGetValue(key, out var step) &&
                                        step.Item1 == pawn.Cell && step.Item2 == pawn.NextCell;
                        lastStep[key] = (pawn.Cell, pawn.NextCell);
                        if (!sameStep) { lastPos[key] = at; continue; }

                        Vector3 heading = CellMetrics.FloorCentre(pawn.NextCell) -
                                          CellMetrics.FloorCentre(pawn.Cell);
                        heading.y = 0f;
                        if (heading.sqrMagnitude > 1e-6f && lastPos.TryGetValue(key, out Vector3 was))
                        {
                            Vector3 moved = at - was;
                            moved.y = 0f;
                            float forward = Vector3.Dot(moved, heading.normalized);
                            frames++;
                            if (forward < -1e-5f)
                            {
                                backwards++;
                                if (forward < worst)
                                {
                                    worst = forward;
                                    where = $"tick {tick}, colonist {key}, {pawn.Cell} -> {pawn.NextCell}";
                                }
                            }
                        }
                        lastPos[key] = at;
                    }
                }
            }

            Assert.That(frames, Is.GreaterThan(5000), "too few walking frames to prove anything");
            Assert.That(backwards, Is.Zero,
                $"{backwards} of {frames} frames drew a colonist backwards along her own step, " +
                $"worst {worst * 1000f:F1} mm at {where}");
        }

        /// <summary>
        /// The rate is published rather than inferred, and it never promises more than the tick
        /// delivers — an over-estimate is what walks the figure backwards, so it is truncated down.
        /// </summary>
        [Test]
        public void ThePublishedRateNeverPromisesMoreThanATickDelivers()
        {
            var size = new GridSize(40, 40, 16);
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 6;
            scenario.beds = 6;
            ColonyWorld colony = ColonyWorld.Build(size, seed: 5u, scenario, barren: true, wooded: true);

            var seen = new Dictionary<int, (CellRef, CellRef, int)>();
            int checks = 0;

            for (int tick = 0; tick < 600; tick++)
            {
                colony.World.Tick();
                ReadOnlySpan<PawnView> pawns = colony.World.Views.Current.Pawns;
                for (int i = 0; i < pawns.Length; i++)
                {
                    ref readonly PawnView pawn = ref pawns[i];
                    int key = pawn.Id.Value;
                    if (!pawn.Moving) { seen.Remove(key); continue; }

                    if (seen.TryGetValue(key, out var before) &&
                        before.Item1 == pawn.Cell && before.Item2 == pawn.NextCell)
                    {
                        int advanced = pawn.MovePerMille - before.Item3;
                        Assert.That(pawn.MoveDeltaPerMille, Is.LessThanOrEqualTo(advanced + 1),
                            "the published rate ran ahead of what the tick actually retired");
                        checks++;
                    }
                    seen[key] = (pawn.Cell, pawn.NextCell, pawn.MovePerMille);
                }
            }

            Assert.That(checks, Is.GreaterThan(1000), "too few steps to prove anything");
        }
    }
}
