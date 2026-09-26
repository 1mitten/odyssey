#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The New game page's Story block (design 59 §12, Claude Design's mockups 25a–25c, 25h): the
    /// three storytellers as cards, the difficulty ladder, and the Custom block, in the right-hand
    /// column the skills cap left empty. The page stays one step.
    ///
    /// <para><b>All three cards are always shown</b>, so they can be compared: a choice is made
    /// between things you can see at once. <b>The Custom block is always drawn</b>, dimmed and
    /// unpressable on any rung but Custom, so picking Custom moves nothing on the page and only
    /// lights what is already there.</para>
    ///
    /// <para><b>Compact</b> is decided by the page's own laid-out width against
    /// <see cref="HudLayout.SetupCompactBelow"/>, never by the screen: the HUD's canvas is 1920 x
    /// 1080 at 100% interface scale whatever the window, so the mockup's 1280 x 720 is what the
    /// page sees at 150% or on a narrow aspect. There the cards lose their blurbs (the chosen one's
    /// shows once under them), the strips are 28 high, the ladder is 4 + 3 and the Custom levers
    /// show their figures without tracks.</para>
    ///
    /// <para>Every press goes to <see cref="MenuDirector"/>, whose <see cref="StoryChoice"/> is the
    /// one rule for what a press does; this file only draws what it says.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        sealed class StoryCardView
        {
            public VisualElement Root = null!;
            public PathGlyph Emblem = null!;
            public Label Name = null!;
            public Label Blurb = null!;
            public RhythmStrip Strip = null!;
        }

        sealed class StoryLeverView
        {
            public VisualElement Root = null!;
            public Slider? Fader;
            public VisualElement? Fill;
            public Label Value = null!;
            public SwitchView? Switch;
        }

        readonly List<StoryCardView> _storyCards = new();
        readonly List<Label> _storyRungs = new();
        readonly Dictionary<string, StoryLeverView> _storyLevers = new();
        Label _storyBlurbCompact = null!;
        VisualElement _storyCustom = null!;
        VisualElement _storyLeverGrid = null!;
        Label _storyCustomNote = null!;
        bool _setupCompact;

        /// <summary>The Story block, the page's third column.</summary>
        VisualElement BuildStoryBlock()
        {
            var block = new VisualElement();
            block.AddToClassList("story");

            block.Add(StoryHeading(StoryCatalogue.StorytellerHeadingKey));

            var cards = new VisualElement();
            cards.AddToClassList("story__cards");
            block.Add(cards);
            for (int i = 0; i < StoryCatalogue.Tellers.Count; i++)
            {
                int index = i;
                StoryCatalogue.Teller teller = StoryCatalogue.Tellers[i];

                var card = new VisualElement();
                card.AddToClassList("story__card");
                card.AddToClassList("settings__row");
                if (i > 0) card.AddToClassList("story__card--after");
                card.tooltip = teller.Blurb;

                var head = new VisualElement();
                head.AddToClassList("story__head");
                // The portrait tile: the placeholder emblem until the commissioned art arrives,
                // looked up by the portrait key so the art replaces it with no layout change.
                var portrait = new VisualElement();
                portrait.AddToClassList("story__portrait");
                portrait.tooltip = Registry.Label(teller.PortraitKey);
                var emblem = new PathGlyph(teller.Emblem, HudLayout.StoryEmblem, HudTokens.TextMeta);
                portrait.Add(emblem);
                head.Add(portrait);
                Label name = HudText.Make(teller.Label, HudTextRole.Name, ussClass: "story__name");
                head.Add(name);
                card.Add(head);

                Label blurb = HudText.Make(teller.Blurb, HudTextRole.Body, ussClass: "story__blurb");
                card.Add(blurb);

                var strip = new RhythmStrip(teller.Marks, HudTokens.TextMeta);
                strip.AddToClassList("story__strip");
                card.Add(strip);

                card.RegisterCallback<ClickEvent>(_ => _menu.ChooseStoryteller(index));
                cards.Add(card);
                _storyCards.Add(new StoryCardView
                {
                    Root = card, Emblem = emblem, Name = name, Blurb = blurb, Strip = strip,
                });
            }

            _storyBlurbCompact = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "story__blurb-compact");
            block.Add(_storyBlurbCompact);

            VisualElement difficulty = StoryHeading(StoryCatalogue.DifficultyHeadingKey);
            difficulty.AddToClassList("story__heading--gap");
            block.Add(difficulty);

            var ladder = new VisualElement();
            ladder.AddToClassList("story__ladder");
            block.Add(ladder);
            for (int r = 0; r < StoryCatalogue.Rungs.Count; r++)
            {
                int rung = r;
                Label seg = HudText.Make(StoryCatalogue.Rungs[r].Label, HudTextRole.Row, ussClass: "story__rung");
                seg.RegisterCallback<ClickEvent>(_ => _menu.ChooseRung(rung));
                ladder.Add(seg);
                _storyRungs.Add(seg);
            }

            // Custom: always drawn, so nothing moves when it lights (25b).
            _storyCustom = new VisualElement();
            _storyCustom.AddToClassList("story__custom");
            _storyCustom.Add(HudText.Make(StoryCatalogue.RungAt(StoryCatalogue.CustomRung).Label,
                HudTextRole.PanelLabel, ussClass: "story__custom-label"));
            _storyCustomNote = HudText.Make(string.Empty, HudTextRole.Meta, ussClass: "story__custom-note");
            _storyCustom.Add(_storyCustomNote);

            _storyLeverGrid = new VisualElement();
            _storyLeverGrid.AddToClassList("story__levers");
            _storyCustom.Add(_storyLeverGrid);
            StoryLever(StoryCatalogue.ThreatScaleKey, StoryCatalogue.ThreatMin, StoryCatalogue.ThreatMax,
                v => _menu.SetThreat(v), left: true);
            StorySwitchLever(StoryCatalogue.BigThreatsKey, left: false);
            StoryLever(StoryCatalogue.AdaptationKey, StoryCatalogue.AdaptationMin, StoryCatalogue.AdaptationMax,
                v => _menu.SetAdaptation(v), left: true);
            StoryLever(StoryCatalogue.GraceKey, StoryCatalogue.GraceMin, StoryCatalogue.GraceMax,
                v => _menu.SetGrace(v), left: false);
            block.Add(_storyCustom);

            return block;
        }

        /// <summary>The standard heading: 11/600 upper and tracked, then a rule filling the width.</summary>
        static VisualElement StoryHeading(string key)
        {
            var heading = new VisualElement();
            heading.AddToClassList("story__heading");
            heading.Add(HudText.Make(Registry.Label(key), HudTextRole.PanelLabel, ussClass: "story__heading-label"));
            var rule = new VisualElement();
            rule.AddToClassList("story__heading-rule");
            heading.Add(rule);
            return heading;
        }

        VisualElement StoryLeverRow(string key, bool left)
        {
            var row = new VisualElement();
            row.AddToClassList("story__lever");
            row.AddToClassList(left ? "story__lever--left" : "story__lever--right");
            row.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "story__lever-label"));
            _storyLeverGrid.Add(row);
            return row;
        }

        void StoryLever(string key, int min, int max, System.Action<int> set, bool left)
        {
            VisualElement row = StoryLeverRow(key, left);
            var fader = new Slider(min, max, SliderDirection.Horizontal);
            fader.AddToClassList("settings__fader");
            fader.AddToClassList("story__fader");
            VisualElement tracker = fader.Q(className: "unity-base-slider__tracker") ?? fader;
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("sw__fill");
            tracker.Add(fill);
            fader.RegisterValueChangedCallback(evt => set(Mathf.RoundToInt(evt.newValue)));
            row.Add(fader);
            Label value = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, ussClass: "story__figure");
            row.Add(value);
            _storyLevers[key] = new StoryLeverView { Root = row, Fader = fader, Fill = fill, Value = value };
        }

        void StorySwitchLever(string key, bool left)
        {
            VisualElement row = StoryLeverRow(key, left);
            SwitchView view = Switch();
            view.Control.AddToClassList("story__switch");
            row.Add(view.Control);
            row.RegisterCallback<ClickEvent>(_ => _menu.SetBigThreats(!_menu.Story.BigThreats));
            _storyLevers[key] = new StoryLeverView { Root = row, Value = view.Word, Switch = view };
        }

        /// <summary>Draw the choice: which card and rung are lit, and what Custom's levers read.</summary>
        void RefreshStory()
        {
            if (_storyCustom == null) return;
            StoryChoice choice = _menu.Story;

            Color accent = HudTokens.Accent, meta = HudTokens.TextMeta;
            for (int i = 0; i < _storyCards.Count; i++)
            {
                StoryCardView card = _storyCards[i];
                bool on = i == choice.Teller;
                card.Root.EnableInClassList("story__card--on", on);
                card.Emblem.Tint = on ? accent : meta;
                card.Strip.Tint = on ? accent : meta;
                card.Name.style.color = on ? accent : HudTokens.TextPrimary;
            }
            HudText.Set(_storyBlurbCompact, choice.HasTeller ? StoryCatalogue.TellerAt(choice.Teller).Blurb : string.Empty,
                HudTextRole.Body);

            for (int r = 0; r < _storyRungs.Count; r++)
            {
                bool on = r == choice.Rung;
                _storyRungs[r].EnableInClassList("story__rung--on", on);
                _storyRungs[r].EnableInClassList("story__rung--after-on", r > 0 && r - 1 == choice.Rung);
                _storyRungs[r].style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
            }

            bool live = choice.IsCustom;
            _storyCustom.EnableInClassList("story__custom--dim", !live);
            _storyLeverGrid.SetEnabled(live);
            HudText.Set(_storyCustomNote, StoryCatalogue.CustomNote(choice.Rung), HudTextRole.Meta);

            SetStoryLever(StoryCatalogue.ThreatScaleKey, choice.ThreatPercent, StoryCatalogue.ThreatMin,
                StoryCatalogue.ThreatMax, StoryCatalogue.Percent(choice.ThreatPercent));
            SetStoryLever(StoryCatalogue.AdaptationKey, choice.AdaptationPercent, StoryCatalogue.AdaptationMin,
                StoryCatalogue.AdaptationMax, StoryCatalogue.Percent(choice.AdaptationPercent));
            SetStoryLever(StoryCatalogue.GraceKey, choice.GraceHundredths, StoryCatalogue.GraceMin,
                StoryCatalogue.GraceMax, StoryCatalogue.Stretch(choice.GraceHundredths));
            if (_storyLevers.TryGetValue(StoryCatalogue.BigThreatsKey, out StoryLeverView? big) && big.Switch != null)
            {
                big.Switch.Control.EnableInClassList("sw__switch--on", choice.BigThreats);
                big.Switch.Word.text = choice.BigThreats ? "On" : "Off";
            }
        }

        void SetStoryLever(string key, int at, int min, int max, string text)
        {
            if (!_storyLevers.TryGetValue(key, out StoryLeverView? view) || view.Fader == null) return;
            if (!Mathf.Approximately(view.Fader.value, at)) view.Fader.SetValueWithoutNotify(at);
            if (view.Fill != null)
                view.Fill.style.width = Length.Percent(max > min ? (at - min) * 100f / (max - min) : 0f);
            HudText.Set(view.Value, text, HudTextRole.Meta);
        }

        /// <summary>
        /// The page measured itself: go compact below <see cref="HudLayout.SetupCompactBelow"/> of
        /// content width, and come back above it. The card names step down from 19/600 to 14/500,
        /// which is type and so is set here rather than in the sheet (the sheet sets no type).
        /// </summary>
        void OnSetupGeometry(GeometryChangedEvent evt)
        {
            bool compact = _setupPage.contentRect.width < HudLayout.SetupCompactBelow;
            if (compact == _setupCompact) return;
            _setupCompact = compact;
            _setupPage.EnableInClassList("setup--compact", compact);
            foreach (StoryCardView card in _storyCards)
            {
                HudTextRole role = compact ? HudTextRole.Row : HudTextRole.Name;
                HudText.Apply(card.Name, role);
                HudText.Set(card.Name, card.Name.text, role);
            }
        }
    }
}
