#nullable enable
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    // The hit-chance readout at the pointer (design 53 §8b): one line beside the cursor while a
    // drafted colonist with a gun is selected and a hostile is under the pointer. The words are
    // ShotReadout's and the numbers the simulation's; this only puts them beside the cursor.
    public sealed partial class HudShell
    {
        VisualElement? _shotReadout;
        Label? _shotReadoutText;

        /// <summary>How far from the pointer the readout sits, in panel pixels, so it never covers what it describes.</summary>
        const float ShotReadoutOffset = 18f;

        /// <summary>
        /// Show <paramref name="text"/> beside the pointer at <paramref name="screenPosition"/> (the
        /// mouse's own, bottom-left origin), or put the readout away with null or empty text.
        /// Never pickable, so it cannot take the hover it is answering.
        /// </summary>
        public void SetShotReadout(string? text, Vector2 screenPosition, HudColour? ink = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (_shotReadout != null) _shotReadout.style.display = DisplayStyle.None;
                return;
            }
            if (_hud == null || _hud.panel == null) return;

            if (_shotReadout == null)
            {
                _shotReadout = Panel("shotreadout", "shotreadout");
                _shotReadout.pickingMode = PickingMode.Ignore;
                _shotReadout.style.position = Position.Absolute;
                _shotReadoutText = new Label { pickingMode = PickingMode.Ignore };
                _shotReadoutText.AddToClassList("shotreadout__text");
                _shotReadout.Add(_shotReadoutText);
                _worldUi.Add(_shotReadout);
            }

            if (_shotReadoutText != null)
            {
                HudText.Set(_shotReadoutText, text!, HudTextRole.Row);
                // The chance to hit, judged on StatInks.HitChance (design 59); neutral with none.
                _shotReadoutText.style.color = ink.HasValue
                    ? new StyleColor(HudTokens.Convert(ink.Value))
                    : new StyleColor(StyleKeyword.Null);
            }
            Vector2 at = ToPanel(screenPosition);
            _shotReadout.style.left = at.x + ShotReadoutOffset;
            _shotReadout.style.top = at.y + ShotReadoutOffset;
            _shotReadout.style.display = DisplayStyle.Flex;
            _shotReadout.BringToFront();
        }
    }
}
