#nullable enable
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the fight's interface (design 33 §1, §5) — the Health tab's body,
    /// the corpse pane, the Spawn tab's marauder and weapon rows. <b>Lane C's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>): the one Presentation file lane C writes, so the
    /// interface lane never edits a file the drawing lane owns.
    ///
    /// <para><b>A seam from the contracts step.</b> <c>HudShell.Inspect</c> calls the four methods
    /// below — build the tab body into the colonist's fixed-height box, forget it when the pane is
    /// rebuilt, show or hide it with the tab strip, and sync its values fifteen times a second —
    /// and each does nothing yet, so the Health tab is live and empty. The words and numbers come
    /// from <c>Odyssey.Hud.CombatFeedbackModel</c>, which is lane C's too and runs in the fast
    /// tier; this file only draws its answers.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        /// <summary>The Health tab's body, built once per subject into the pane's tab box.</summary>
        VisualElement? _healthBody;

        /// <summary>Build the Health tab's body into <paramref name="tabBody"/>. Empty until lane C writes it.</summary>
        void BuildHealthTab(VisualElement tabBody)
        {
            _healthBody = new VisualElement();
            _healthBody.style.display = DisplayStyle.None;
            tabBody.Add(_healthBody);
        }

        /// <summary>The pane is being rebuilt for another subject: drop what was built.</summary>
        void ForgetHealthTab() => _healthBody = null;

        /// <summary>Show the body while the Health tab is the active one, hide it otherwise.</summary>
        void ShowHealthTab(bool shown)
        {
            if (_healthBody != null)
                _healthBody.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Update the values in place. Nothing to update until lane C writes the body.</summary>
        void SyncHealthTab()
        {
        }
    }
}
