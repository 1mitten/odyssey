#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The floating words of a fight, on screen (design 33 §1): one label per live
    /// <see cref="CombatFloaters.Floater"/>, placed over its point in the world each frame and
    /// faded with it. Words are the HUD's, in the HUD's shipped faces (<see cref="HudText"/>), so
    /// what a word says and the glyphs it needs are covered by the same registry and font tests as
    /// every other label.
    ///
    /// <para><b>Beneath the HUD, not in it.</b> The layer is the first child of the document's
    /// root, so every panel draws over a word that drifts under it, and it never takes a click
    /// (<see cref="PickingMode.Ignore"/>). It is added by the composition root rather than by
    /// <c>HudShell</c>, whose tree is laid out for panels; this is a picture of the world.</para>
    ///
    /// <para><b>Pooled.</b> Labels are made as the brawl needs them, up to
    /// <see cref="CombatFloaters.Capacity"/>, and hidden rather than removed.</para>
    /// </summary>
    public sealed class CombatFloaterView
    {
        readonly VisualElement _layer;
        readonly List<Label> _labels = new List<Label>();

        public CombatFloaterView(VisualElement root)
        {
            _layer = new VisualElement { name = "combat-floaters", pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0;
            _layer.style.top = 0;
            _layer.style.right = 0;
            _layer.style.bottom = 0;
            root.Insert(0, _layer);
        }

        /// <summary>How many labels are showing on the last draw. Diagnostic.</summary>
        public int Showing { get; private set; }

        public void Draw(CombatFloaters floaters, Camera? camera)
        {
            Showing = 0;
            IPanel? panel = _layer.panel;
            IReadOnlyList<CombatFloaters.Floater> alive = floaters.Alive;

            for (int i = 0; i < alive.Count && camera != null && panel != null; i++)
            {
                CombatFloaters.Floater floater = alive[i];
                Vector3 at = floater.At;
                if (Vector3.Dot(at - camera.transform.position, camera.transform.forward) <= 0f) continue;

                Vector2 point = RuntimePanelUtils.CameraTransformWorldToPanel(panel, at, camera);
                Label label = LabelAt(Showing++);
                HudText.Set(label, floater.Text, HudTextRole.Name);
                Color ink = HudTokens.Convert(floater.Ink);
                ink.a *= floater.Alpha;
                label.style.color = ink;
                label.style.left = point.x;
                label.style.top = point.y;
                label.style.display = DisplayStyle.Flex;
            }

            for (int i = Showing; i < _labels.Count; i++) _labels[i].style.display = DisplayStyle.None;
        }

        Label LabelAt(int index)
        {
            while (_labels.Count <= index)
            {
                Label label = HudText.Make(string.Empty, HudTextRole.Name, numeric: false);
                label.pickingMode = PickingMode.Ignore;
                label.style.position = Position.Absolute;
                // Centred on its point: the word stands over the head it came off.
                label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                _layer.Add(label);
                _labels.Add(label);
            }
            return _labels[index];
        }

        public void Dispose() => _layer.RemoveFromHierarchy();
    }
}
