#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// A colonist's response to danger as the pane shows it and the button changes it (design 33
    /// §18c, §18e): <b>Fight back</b> (the default), <b>Defend</b>, <b>Flee</b>, round in that order.
    ///
    /// <para><b>Unity-free, so the fast tier owns the rule</b>, on <see cref="OrderModel"/>'s terms:
    /// the shell hears the click and carries the intents this returns to the world.</para>
    ///
    /// <para>The numbers are the simulation's <c>HostilityResponse</c> values, which this assembly
    /// cannot see; <c>ResponseModelTests</c> and <c>ResponseTests</c> hold the two sides to the
    /// same three.</para>
    /// </summary>
    public static class ResponseModel
    {
        /// <summary>The three responses, as the simulation numbers them.</summary>
        public const int FightBack = 0, Defend = 1, Flee = 2, Count = 3;

        /// <summary>The button's face for each response, a registry key each (and the button's icon).</summary>
        public const string FightBackKey = "ui.command.fightback", DefendKey = "ui.command.defend", FleeKey = "ui.command.flee";

        /// <summary>The colonist's response off the frame: the published number, or Fight back when absent.</summary>
        public static int Of(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawnAspect(pawn, CombatAspectNames.ResponseKey, out int response)
                && response >= 0 && response < Count ? response : FightBack;

        /// <summary>The key that names <paramref name="response"/>.</summary>
        public static string KeyOf(int response) => response switch
        {
            Defend => DefendKey,
            Flee => FleeKey,
            _ => FightBackKey,
        };

        /// <summary>Is <paramref name="key"/> one of the button's three faces?</summary>
        public static bool IsResponseKey(string key) => key == FightBackKey || key == DefendKey || key == FleeKey;

        /// <summary>The one after <paramref name="response"/>, round the three.</summary>
        public static int Next(int response) => (response + 1) % Count;

        /// <summary>
        /// What the button's tooltip says after the name: what the response does, and that a press
        /// changes it. Not names — the names are the registry's, on the button's face.
        /// </summary>
        public static string Describe(int response) => response switch
        {
            Defend => "joins a fight near her, then goes back to work; press to change",
            Flee => "runs from danger near her, and fights only when cornered; press to change",
            _ => "fights only whoever strikes her; press to change",
        };

        /// <summary>
        /// The button pressed (design 33 §18e): the next response after the <b>first selected
        /// colonist's</b>, set on every selected colonist not already at it — so one press on a box
        /// selection puts the whole squad on one response, as the draft key's rule for a mixed
        /// selection does. Animals and marauders in the selection are passed over. Adds nothing when
        /// there is no colonist to act on.
        /// </summary>
        public static void Cycle(IReadOnlyList<PawnId> selection, WorldSnapshot snapshot, List<Intent> into)
        {
            int next = -1;
            for (int i = 0; i < selection.Count && next < 0; i++)
                if (OrderModel.IsColonist(snapshot, selection[i])) next = Next(Of(snapshot, selection[i]));
            if (next < 0) return;

            for (int i = 0; i < selection.Count; i++)
            {
                PawnId pawn = selection[i];
                if (!OrderModel.IsColonist(snapshot, pawn) || Of(snapshot, pawn) == next) continue;
                into.Add(new Intent(IntentKind.SetHostilityResponse, default, pawn.Value, next));
            }
        }
    }
}
