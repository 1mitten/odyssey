#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Odyssey.Tests.PlayMode
{
    /// <summary>
    /// The interface acceptance criteria, measured on the screen rather than in the model.
    ///
    /// <para><b>Why both this and <c>HudLayoutTests</c>.</b> The fast tier proves that
    /// <see cref="HudLayout"/>'s arithmetic does not overlap and does not exceed the coverage
    /// ceiling, in under a tenth of a second and with no engine. What it cannot prove is that the
    /// shell builds the screen the model describes — a stylesheet that quietly places the alerts
    /// panel somewhere else, or a padding that makes a panel taller than the model says, would
    /// leave every fast-tier assertion passing about a screen nobody is looking at. This test
    /// lays the real HUD out in a real panel at three resolutions and measures the boxes UI
    /// Toolkit actually produced.</para>
    ///
    /// <para><b>What "the model contains reality" means.</b> Each realised box has to sit inside
    /// its modelled box, allowing a pixel of rounding. Containment rather than equality, because a
    /// conservative model is safe in both directions that matter — a panel smaller than modelled
    /// cannot overlap a neighbour the model cleared, and cannot push coverage above a ceiling the
    /// model already met. A panel <i>larger</i> than modelled is the failure, and it is the one
    /// this catches.</para>
    /// </summary>
    public class HudGeometryTests
    {
        const string PanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string StylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";
        const string UiFontPath = "Assets/Odyssey/Presentation/Ui/Fonts/ArchivoNarrow.ttf";
        const string MonoFontPath = "Assets/Odyssey/Presentation/Ui/Fonts/IBMPlexMono-Medium.ttf";

        /// <summary>The three the acceptance criteria name.</summary>
        static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
        };

        /// <summary>
        /// How far a realised box may sit outside its modelled one.
        ///
        /// <para><b>It has to depend on the panel scale, and that is a fact about UI Toolkit
        /// rather than a fudge.</b> The layout is rounded to the <i>physical</i> pixel grid, and a
        /// panel scaled to fit a 1280-pixel window against a 1920-pixel canvas has one physical
        /// pixel to every 1.5 reference pixels. So a command item declared 38 reference pixels
        /// tall occupies 25.3 physical pixels, rounds to 26, and measures back as <b>39</b>. Every
        /// element in a column can gain one such step, which is why this is two of them rather
        /// than one.</para>
        /// </summary>
        static float SlackFor(Rect canvas, Vector2Int resolution) =>
            Mathf.Max(1.5f, 2f * canvas.width / resolution.x);

        [UnityTest]
        public IEnumerator NoTwoPanelsOverlapAtAnyOfTheThreeResolutions()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    float slack = SlackFor(doc.rootVisualElement.worldBound, resolution);
                    Dictionary<string, Rect> boxes = Regions(doc);
                    var names = new List<string>(boxes.Keys);

                    for (int i = 0; i < names.Count; i++)
                    for (int j = i + 1; j < names.Count; j++)
                    {
                        Rect a = boxes[names[i]];
                        Rect b = boxes[names[j]];
                        Rect shared = Intersection(a, b);

                        Assert.That(shared.width <= slack || shared.height <= slack, Is.True,
                            $"at {resolution.x}x{resolution.y} the '{names[i]}' panel {a} overlaps " +
                            $"the '{names[j]}' panel {b} by {shared.width:0.#} x {shared.height:0.#} px");
                    }
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        [UnityTest]
        public IEnumerator TheHudCoversUnderTheCeilingWithNothingSelected()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    Rect canvas = doc.rootVisualElement.worldBound;
                    float area = 0f;
                    foreach (KeyValuePair<string, Rect> region in Regions(doc))
                        area += region.Value.width * region.Value.height;

                    float coverage = area / (canvas.width * canvas.height);
                    Debug.Log($"[HudGeometry] {resolution.x}x{resolution.y} " +
                              $"(logical {canvas.width:0}x{canvas.height:0}): {coverage:P1} covered");

                    Assert.That(coverage, Is.LessThanOrEqualTo(HudLayout.CoverageCeiling),
                        $"the HUD covers {coverage:P1} of a {resolution.x}x{resolution.y} screen " +
                        $"with nothing selected, over the {HudLayout.CoverageCeiling:P0} ceiling");
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>
        /// The realised boxes sit inside the modelled ones. This is the assertion that keeps the
        /// fast tier honest: without it, <c>HudLayoutTests</c> proves things about a model that
        /// nothing forces the shell to obey.
        /// </summary>
        [UnityTest]
        public IEnumerator TheModelDescribesTheScreenTheShellActuallyBuilds()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                Rect canvas = doc.rootVisualElement.worldBound;
                float slack = SlackFor(canvas, Resolutions[1]);
                Dictionary<string, Rect> realised = Regions(doc);

                var content = new HudContent(
                    colonists: doc.rootVisualElement.Query(className: "card").ToList().Count,
                    storeRows: Visible(doc, "stores__row"),
                    alerts: Visible(doc, "alert"),
                    layers: doc.rootVisualElement.Query(className: "ruler__tick").ToList().Count,
                    needRows: 0);

                Dictionary<HudRegion, HudRect> model = HudLayout.Solve(canvas.width, canvas.height, content);

                // The inspect pane is deliberately not in this list: with nothing selected there
                // is no pane, which is what TheInspectPaneIsAbsentUntilSomethingIsSelected holds.
                (string Name, HudRegion Region)[] pairs =
                {
                    ("stores", HudRegion.Stores),
                    ("clock", HudRegion.Clock),
                    ("rail", HudRegion.DepthRail),
                };

                foreach ((string name, HudRegion region) in pairs)
                {
                    Assert.That(realised, Does.ContainKey(name), $"the shell built no '{name}' panel");
                    Rect box = realised[name];
                    HudRect want = model[region];

                    Debug.Log($"[HudGeometry] {name}: realised {box}, modelled " +
                              $"({want.X:0.#}, {want.Y:0.#}) {want.Width:0.#} x {want.Height:0.#}");

                    Assert.That(box.xMin, Is.GreaterThanOrEqualTo(want.X - slack), $"{name} starts left of the model");
                    Assert.That(box.yMin, Is.GreaterThanOrEqualTo(want.Y - slack), $"{name} starts above the model");
                    Assert.That(box.xMax, Is.LessThanOrEqualTo(want.Right + slack), $"{name} runs right of the model");
                    Assert.That(box.yMax, Is.LessThanOrEqualTo(want.Bottom + slack),
                        $"{name} is {box.yMax - want.Bottom:0.#} px taller than the model allows for, " +
                        "so the coverage and overlap arithmetic in the fast tier is describing a " +
                        "different screen from the one the shell builds. " +
                        Describe(doc.rootVisualElement.Q(name: name)!));
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator TheCommandBarNeverRunsOffTheEdgeAndAlwaysShowsItsHotkeys()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    Rect canvas = doc.rootVisualElement.worldBound;
                    float slack = SlackFor(canvas, resolution);
                    VisualElement? bar = doc.rootVisualElement.Q(name: "bar");
                    Assert.That(bar, Is.Not.Null, "the shell built no command bar");

                    // The bar spans the screen (owner, 2026-09-17). What "nothing is cut off at
                    // the right edge" reduces to is therefore about the items, not the bar: the
                    // bar itself is meant to reach both edges, and the loop below is what holds
                    // the items to it.
                    Rect box = bar!.worldBound;
                    Assert.That(box.xMin, Is.EqualTo(canvas.xMin).Within(slack + 0.5f),
                        $"the bar starts {box.xMin - canvas.xMin:0.#} px in from the left at " +
                        $"{resolution.x}x{resolution.y}");
                    Assert.That(box.xMax, Is.EqualTo(canvas.xMax).Within(slack + 0.5f),
                        $"the bar ends {canvas.xMax - box.xMax:0.#} px short of the right edge at " +
                        $"{resolution.x}x{resolution.y}");

                    // The bar may not wrap. One row, whatever fits; the rest go into Menu, which
                    // is what lets the inspect pane above it assume a height for it.
                    Debug.Log($"[HudGeometry] bar at {resolution.x}x{resolution.y}: {box}, {Describe(bar)}");
                    Assert.That(box.height, Is.LessThanOrEqualTo(HudCommands.BarHeight + HudLayout.BarFrame + slack),
                        $"the bar is {box.height:0.#} px tall against a modelled " +
                        $"{HudCommands.BarHeight + HudLayout.BarFrame}, so it has wrapped to a " +
                        $"second row. {Describe(bar)}");

                    // Every item that is on the row shows a hotkey cap with something in it, and
                    // none of them runs off the end of the bar.
                    int caps = 0;
                    foreach (VisualElement item in doc.rootVisualElement.Query(className: "cmd").ToList())
                    {
                        if (item.resolvedStyle.display == DisplayStyle.None) continue;
                        Label? key = item.Q<Label>(className: "cmd__key");
                        Assert.That(key, Is.Not.Null, "a command-bar item has no hotkey cap");
                        Assert.That(key!.text, Is.Not.Empty, "a command-bar item shows a blank hotkey");
                        Assert.That(item.worldBound.xMax, Is.LessThanOrEqualTo(box.xMax + slack + 0.5f),
                            $"a command item runs to {item.worldBound.xMax:0.#} past the bar's " +
                            $"{box.xMax:0.#} at {resolution.x}x{resolution.y}, so it is cut off");
                        caps++;
                    }
                    Assert.That(caps, Is.GreaterThan(1),
                        $"only {caps} command items are on the bar at {resolution.x}x{resolution.y}");
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>
        /// Turning the interface scale up makes the HUD bigger, end to end.
        ///
        /// <para>The fast tier proves the arithmetic and that no two panels overlap at any rung.
        /// What it cannot prove is that the rung reaches the panel at all — that the director's
        /// event is subscribed, that the shell writes the reference resolution, and above all that
        /// it writes it to <b>its own copy</b> of the settings asset rather than to the committed
        /// one.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TurningTheInterfaceScaleUpMakesTheHudBigger()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                Assert.That(boot.Directors, Is.Not.Null, "the bootstrap never built its directors");

                PanelSettings before = doc.panelSettings;
                Rect wide = doc.rootVisualElement.worldBound;
                Rect storesBefore = doc.rootVisualElement.Q(name: "stores")!.worldBound;

                boot.Directors!.Settings.SetUiScale(150);
                for (int i = 0; i < 6; i++) yield return null;

                Assert.That(doc.panelSettings.referenceResolution.x,
                    Is.EqualTo(HudLayout.ReferenceFor(150).Width),
                    "the scale never reached the panel");
                Assert.That(doc.rootVisualElement.worldBound.width, Is.LessThan(wide.width),
                    "a larger scale is a smaller canvas, which is what makes everything on it bigger");

                // The panel is the same asset instance throughout — the shell takes its copy once,
                // before the tree is built, so changing the scale never swaps the panel under a
                // live HUD and never writes to the asset on disk.
                Assert.That(doc.panelSettings, Is.SameAs(before),
                    "the shell swapped its panel settings mid-session");

                // The stores panel is a fixed 288 reference pixels wide, so at 150% it occupies
                // half again as much of the screen. Measured as a fraction of the canvas, because
                // worldBound is in canvas units and those are what just changed.
                Rect narrow = doc.rootVisualElement.worldBound;
                Rect storesAfter = doc.rootVisualElement.Q(name: "stores")!.worldBound;
                float was = storesBefore.width / wide.width;
                float now = storesAfter.width / narrow.width;
                Debug.Log($"[HudGeometry] stores is {was:P1} of the canvas at 100% and {now:P1} at 150%");
                Assert.That(now, Is.GreaterThan(was * 1.3f),
                    "the HUD did not actually grow relative to the screen");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The audio faders seat unity at the centre of their track — lowering towards
        /// silence to the left of it, boosting to the ceiling to the right — and a drag
        /// reaches the bus it names.
        ///
        /// <para>The fast tier proves the span, the seating arithmetic and the event. What it
        /// cannot prove is that the shell built faders at all, or that the loop closes:
        /// track position to director to event and back onto the thumb and the readout. The
        /// drag is offered the way the engine would deliver it — as the slider's own value —
        /// because <c>MouseHarness</c> cannot press a button in a batch run (its docs say
        /// why); the pointer half of a drag is Unity's own control, and it is the wiring
        /// around it that is ours to break.</para>
        ///
        /// <para>The slider's value is track position (−1 to +1), not dB, so that unity can
        /// sit at the exact centre — see <c>SettingsDirector.TrackOf</c> for the
        /// mapping.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AVolumeSliderDragsItsBusAndThePanelAgrees()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                Assert.That(boot.Directors, Is.Not.Null, "the bootstrap never built its directors");
                SettingsDirector settings = boot.Directors!.Settings;

                settings.SetOpen(true);
                settings.SetTab(SettingsTab.Audio);
                yield return null;

                List<Slider> faders = doc.rootVisualElement.Query<Slider>(className: "settings__fader").ToList();
                Assert.That(faders.Count, Is.EqualTo(SettingsDirector.Buses.Length),
                    "one fader per bus, or a bus has no lever");

                // The first two are the master's and the music's: the buses are drawn in order.
                Slider master = faders[0];
                Slider music = faders[1];
                Assert.That(master.direction, Is.EqualTo(SliderDirection.Horizontal),
                    "the owner asked for faders that drag left to right, on 2026-09-17");

                // The default is seated at the centre of the track: the owner's second ask,
                // later the same day. The notch is the mark that makes the centre findable.
                Assert.That((master.lowValue + master.highValue) / 2f, Is.EqualTo(0f).Within(0.001f),
                    "the track's centre is 0, or unity cannot sit at it");
                Assert.That(master.value, Is.EqualTo(0f).Within(0.001f),
                    "the fader does not start at the centre — at unity");
                Assert.That(master.Q(className: "settings__fader-notch"), Is.Not.Null,
                    "the fader has no centre mark to find unity by");

                Label readout = ReadoutOf(master);
                Assert.That(readout.text, Is.EqualTo("0 dB"), "a fader starts at unity");

                // Drag left, to half the attenuation side of the track: −40 dB.
                master.value = -0.5f;
                Assert.That(settings.BusDb(SettingsBus.Master), Is.EqualTo(-40),
                    "the drag never reached the bus it names");
                Assert.That(master.value, Is.EqualTo(-0.5f).Within(0.001f),
                    "the thumb was not seated at the director's whole dB");
                Assert.That(readout.text, Is.EqualTo("-40 dB"),
                    "the readout does not say where the thumb rests");

                // Drag right, onto the boost side: +6 dB, said with its plus.
                master.value = 0.5f;
                Assert.That(settings.BusDb(SettingsBus.Master), Is.EqualTo(SettingsDirector.BoostDb / 2),
                    "the boost side of the centre does not boost");
                Assert.That(readout.text, Is.EqualTo("+6 dB"),
                    "a boost that does not say its plus reads as a level, not a lift");

                Assert.That(music.value, Is.EqualTo(0f).Within(0.001f),
                    "dragging one fader moved another bus's");

                // Both ends, and past them: the track clamps, and the readout says the word
                // at the floor.
                master.value = 1f;
                Assert.That(settings.BusDb(SettingsBus.Master), Is.EqualTo(SettingsDirector.BoostDb));
                Assert.That(readout.text, Is.EqualTo("+12 dB"));
                master.value = -400f;
                Assert.That(settings.BusDb(SettingsBus.Master), Is.EqualTo(SettingsDirector.SilenceDb));
                Assert.That(readout.text, Is.EqualTo("Mute"));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>The readout beside one fader: the label row above it in the Audio
        /// section.</summary>
        static Label ReadoutOf(VisualElement fader)
        {
            VisualElement? row = null;
            foreach (VisualElement child in fader.parent.Children())
            {
                if (child == fader) break;
                row = child;
            }
            return row!.Q<Label>(className: "settings__value")!;
        }

        /// <summary>
        /// The one acceptance criterion that is about words rather than boxes: no three-letter
        /// placeholder strings anywhere on the screen. MEA, WOO and SCR were the old icon
        /// stand-in, and they read as truncated data rather than as a deliberate gap.
        /// </summary>
        [UnityTest]
        public IEnumerator NoLabelIsAThreeLetterPlaceholder()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                foreach (Label label in doc.rootVisualElement.Query<Label>().ToList())
                {
                    string text = label.text;
                    if (string.IsNullOrEmpty(text)) continue;

                    // A hotkey cap is a legend, not a word, and a colonist's initial is one
                    // letter. What is forbidden is an upper-case fragment of two or three letters
                    // standing in for a name.
                    if (label.ClassListContains("cmd__key") || label.ClassListContains("menu__key")) continue;
                    if (label.ClassListContains("card__initial")) continue;
                    if (label.ClassListContains("rail__hint")) continue;

                    bool shout = text.Length is 2 or 3 && text == text.ToUpperInvariant() &&
                                 System.Array.TrueForAll(text.ToCharArray(), char.IsLetter);

                    Assert.That(shout, Is.False,
                        $"'{text}' is a two-or-three-letter capitalised fragment on a " +
                        $"{string.Join(".", label.GetClasses())} label, which is the placeholder " +
                        "idiom the acceptance criteria strike out");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The activity line on a roster card leads with a picture and still holds its word
        /// (owner, 2026-09-16).
        ///
        /// <para><b>What could go wrong is silent.</b> The icon takes 17 px off a 132 px card, and
        /// a word that no longer fits does not report itself — UI Toolkit simply lays it past the
        /// card's edge, where the card's own rounded frame hides the tail. So this measures the
        /// word the text engine would draw against the room the row actually gave it, on the real
        /// face at the real size, rather than trusting the arithmetic in the sheet's comment.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheActivityLineLeadsWithAnIconAndStillHoldsItsWord()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                var rows = doc.rootVisualElement.Query(className: "card__jobrow").ToList();
                Assert.That(rows, Is.Not.Empty, "no card carries an activity line");

                foreach (VisualElement row in rows)
                {
                    var icon = row.Q<IconBadge>();
                    var word = row.Q<Label>(className: "card__job");
                    Assert.That(icon, Is.Not.Null, "an activity line with no icon in it");
                    Assert.That(word, Is.Not.Null, "an activity line with no word in it");

                    Rect iconBox = icon!.worldBound;
                    Rect wordBox = word!.worldBound;
                    Rect card = row.parent.worldBound;

                    Assert.That(iconBox.xMax, Is.LessThanOrEqualTo(wordBox.xMin + 0.01f),
                        $"the icon for '{word.text}' is not to the left of the word");
                    Assert.That(iconBox.xMin, Is.GreaterThanOrEqualTo(card.xMin - 0.01f),
                        "the icon starts outside its own card");

                    float drawn = word.MeasureTextSize(
                        word.text, 0f, VisualElement.MeasureMode.Undefined,
                        0f, VisualElement.MeasureMode.Undefined).x;

                    Debug.Log($"[HudGeometry] activity '{word.text}': icon {iconBox.width:0.#} px, " +
                              $"word {drawn:0.#} px drawn in {wordBox.width:0.#} px of room");

                    Assert.That(wordBox.width, Is.GreaterThanOrEqualTo(drawn - 0.01f),
                        $"'{word.text}' needs {drawn:0.#} px and the icon left it " +
                        $"{wordBox.width:0.#}, so the activity is being clipped by the card edge");
                    Assert.That(wordBox.xMax, Is.LessThanOrEqualTo(card.xMax + 0.01f),
                        $"'{word.text}' runs off the right of its card");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A card is as wide as its two rows need and no wider (owner, 2026-09-17: the need bars
        /// came off so the strip could carry many more colonists).
        ///
        /// <para><b>Why the text engine and not arithmetic.</b> A card's width is decided by two
        /// strings — the longest name the pool can deal and the longest word in <c>ui.status</c> —
        /// and neither can be predicted from its character count in a narrow face. This asks what
        /// they really draw, at the real size, and bounds the constant on both sides: too narrow
        /// and a name is cut short, too wide and every card on the bar is paying for space no
        /// string uses. The stores panel's own width test is the same idea and found 288 px was
        /// never measured.</para>
        ///
        /// <para>Names are measured off the pool rather than off the colony, because a five-pawn
        /// scenario never reaches the eighth name, let alone the cycle suffix that a colony of
        /// fifty will.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheCardIsWideEnoughForItsRowsAndNoWider()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                VisualElement? card = doc.rootVisualElement.Q(className: "card");
                Assert.That(card, Is.Not.Null, "the shell built no roster card");

                var nameLabel = card!.Q<Label>(className: "card__name");
                var jobLabel = card.Q<Label>(className: "card__job");
                Assert.That(nameLabel, Is.Not.Null);
                Assert.That(jobLabel, Is.Not.Null);

                // Twelve cycles of the eight-name pool, which is a colony of ninety-six. The
                // interesting names are not the pool's own — they are the ones carrying the
                // cycle number the model appends once the pool is exhausted, and a two-digit
                // suffix is wider than a one-digit one. Stopping at the pool, or at one cycle,
                // would size the card for a colony that never grows.
                float widestName = 0f;
                string longestName = string.Empty;
                for (int id = 1; id <= 96; id++)
                {
                    string name = ColonistNames.Of(new PawnId(id));
                    float w = Draws(nameLabel!, name);
                    if (w <= widestName) continue;
                    widestName = w;
                    longestName = name;
                }

                float widestJob = 0f;
                string longestJob = string.Empty;
                foreach (string key in JobLabels.IconKeys)
                {
                    float w = Draws(jobLabel!, Registry.Label(key));
                    if (w <= widestJob) continue;
                    widestJob = w;
                    longestJob = Registry.Label(key);
                }

                float nameRow = 2 * HudLayout.CardPad + HudLayout.CardAvatar +
                                HudLayout.CardAvatarGap + widestName;
                float jobRow = 2 * HudLayout.CardPad + IconBadge.RowSize +
                               HudLayout.CardIconGap + widestJob;
                float needed = Mathf.Max(nameRow, jobRow);

                Debug.Log($"[HudGeometry] card rows: name '{longestName}' {widestName:0.#} px " +
                          $"-> {nameRow:0.#}, activity '{longestJob}' {widestJob:0.#} px " +
                          $"-> {jobRow:0.#}; card is {HudLayout.CardWidth}");

                Assert.That(HudLayout.CardWidth, Is.GreaterThanOrEqualTo(needed),
                    $"a card is {HudLayout.CardWidth} px and its widest row needs {needed:0.#} " +
                    $"('{longestName}' / '{longestJob}'), so something is being cut short");

                // Headroom, not comfort: a name is allowed to grow a little before the constant
                // has to be revisited. More than this and the strip is carrying fewer colonists
                // than it could for no reason anybody chose.
                Assert.That(HudLayout.CardWidth, Is.LessThanOrEqualTo(needed + 16f),
                    $"a card is {HudLayout.CardWidth} px where {needed:0.#} would do, and the " +
                    "strip is the densest region on the screen");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The Skills tab builds, switches and draws real art (owner, 2026-09-17).
        ///
        /// <para><b>What only the player loop can answer.</b> The fast tier proves the model
        /// lists thirteen skills and numbers the two the simulation backs. It cannot say whether
        /// the tab strip is clickable, whether the two bodies swap, or whether a single key of
        /// the owner's sheet resolves to a texture rather than falling back to the placeholder
        /// square — which is the failure ADR 0007 calls out as invisible, because a key with no
        /// art is not an error and is not logged.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheSkillsTabSwitchesAndDrawsTheOwnersArt()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                var world = boot.World;
                Assert.That(world, Is.Not.Null, "no world to select a colonist in");
                Assert.That(world!.Views.Current.PawnCount, Is.GreaterThan(0), "no colonists");

                PawnId first = world.Views.Current.Pawns[0].Id;
                boot.Directors!.ChooseColonist(first, world.Views.Current);

                yield return Settle(doc);

                var tabs = doc.rootVisualElement.Query<Label>(className: "tab").ToList();
                Label? skillsTab = tabs.Find(t => t.text == "Skills");
                Assert.That(skillsTab, Is.Not.Null, "the pane built no Skills tab");

                VisualElement? grid = doc.rootVisualElement.Q(className: "skills");
                Assert.That(grid, Is.Not.Null, "the pane built no skills grid");
                Assert.That(grid!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "the pane must open on Needs, not on Skills");

                using (var press = PointerDownEvent.GetPooled())
                {
                    press.target = skillsTab;
                    skillsTab!.SendEvent(press);
                }
                yield return Settle(doc);

                Assert.That(grid.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                    "clicking the Skills tab did not show the skills");
                VisualElement? needs = doc.rootVisualElement.Q(className: "needs");
                Assert.That(needs!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "two tab bodies are on screen at once");

                var rows = grid.Query(className: "skill").ToList();
                Assert.That(rows.Count, Is.EqualTo(SkillCatalogue.All.Length),
                    "the tab does not list every skill the design names");

                int drawn = 0;
                foreach (VisualElement row in rows)
                {
                    var badge = row.Q<IconBadge>();
                    Assert.That(badge, Is.Not.Null, "a skill row with no icon slot");
                    if (IconArt.Has(badge!.Key)) drawn++;
                }

                Debug.Log($"[HudGeometry] skills: {rows.Count} rows, {drawn} drawing real art");
                Assert.That(drawn, Is.EqualTo(SkillCatalogue.All.Length - 1),
                    "every skill but social is cut from the owner's sheet; a shortfall means a " +
                    "key resolved to nothing and quietly drew its placeholder square");

                // And the pane still clears the command bar with its tallest body showing.
                Rect pane = doc.rootVisualElement.Q(name: "inspect")!.worldBound;
                Rect bar = doc.rootVisualElement.Q(name: "bar")!.worldBound;
                Assert.That(pane.yMax, Is.LessThanOrEqualTo(bar.yMin + 0.01f),
                    $"the skills pane ends at {pane.yMax:0.#} and the command bar starts at " +
                    $"{bar.yMin:0.#}");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// A popover raised from the command bar lands on the button that raised it, flush on the
        /// bar, and carries a way out (owner, 2026-09-17).
        ///
        /// <para><b>Only the player loop can answer this.</b> Where a popover sits is decided from
        /// the laid-out bar — the reflow moves buttons as items go into Menu, and the interface
        /// scale moves them again — so the model can state the rule but not that the shell obeys
        /// it. What is measured here is the realised boxes: the popover's bottom against the
        /// bar's top, and its left against the button's.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EveryBarPopoverOpensOverItsOwnButtonAndFlushWithTheBar()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                Rect bar = doc.rootVisualElement.Q(name: "bar")!.worldBound;
                Rect canvas = doc.rootVisualElement.worldBound;

                Assert.That(bar.xMin, Is.EqualTo(canvas.xMin).Within(0.5f), "the bar is inset on the left");
                Assert.That(bar.xMax, Is.EqualTo(canvas.xMax).Within(0.5f), "the bar is inset on the right");
                Assert.That(bar.yMax, Is.EqualTo(canvas.yMax).Within(0.5f),
                    "the bar is not on the bottom edge of the screen");

                foreach (string name in new[] { "build", "menu" })
                {
                    VisualElement? item = ButtonFor(doc, name);
                    Assert.That(item, Is.Not.Null, $"no command-bar button raises the {name} popover");

                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = item;
                        item!.SendEvent(click);
                    }
                    yield return Settle(doc);

                    VisualElement? popover = doc.rootVisualElement.Q(name: name);
                    Assert.That(popover, Is.Not.Null, $"the shell built no {name} popover");
                    Assert.That(popover!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                        $"clicking the button did not open the {name} popover");

                    Rect box = popover.worldBound;
                    Rect button = item.worldBound;

                    Debug.Log($"[HudGeometry] {name} popover: {box}, button at {button.xMin:0.#}, " +
                              $"bar top {bar.yMin:0.#}");

                    Assert.That(box.yMax, Is.EqualTo(bar.yMin).Within(0.5f),
                        $"the {name} popover sits {box.yMax - bar.yMin:0.#} px from the bar " +
                        "rather than flush on it");

                    float wanted = HudLayout.PopoverLeft(
                        button.xMin - canvas.xMin, box.width, canvas.width) + canvas.xMin;
                    Assert.That(box.xMin, Is.EqualTo(wanted).Within(0.5f),
                        $"the {name} popover is not over the button that raised it");
                    Assert.That(box.xMin, Is.GreaterThanOrEqualTo(canvas.xMin - 0.5f),
                        $"the {name} popover hangs off the left of the screen");
                    Assert.That(box.xMax, Is.LessThanOrEqualTo(canvas.xMax + 0.5f),
                        $"the {name} popover hangs off the right of the screen");

                    // And a way out that is not the keyboard.
                    VisualElement? close = popover.Q(className: "panel__close");
                    Assert.That(close, Is.Not.Null, $"the {name} popover has no close button");
                    Assert.That(close!.worldBound.xMax, Is.GreaterThan(box.center.x),
                        $"the {name} popover's close button is not in its top right");

                    using (var shut = ClickEvent.GetPooled())
                    {
                        shut.target = close;
                        close.SendEvent(shut);
                    }
                    yield return Settle(doc);

                    Assert.That(popover.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                        $"the {name} popover's X did not close it");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The Build palette spends its height on the group the player is reaching into (owner,
        /// 2026-09-17: the first group "is using too much vertical space").
        ///
        /// <para><b>What the fast tier cannot say.</b> How many rows ten category chips wrap to is
        /// a fact about the text engine and the panel's width, not about the model — so the cap is
        /// stated in <see cref="HudLayout.BuildCatHeight"/> and measured here. The assertion that
        /// matters is the comparison: the tools group must get at least as much height as the
        /// categories, which is what "more room for the second group" reduces to.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheBuildPaletteGivesItsHeightToTheToolsRatherThanTheCategories()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                VisualElement? button = ButtonFor(doc, "build");
                Assert.That(button, Is.Not.Null);
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = button;
                    button!.SendEvent(click);
                }
                yield return Settle(doc);

                VisualElement? palette = doc.rootVisualElement.Q(name: "build");
                VisualElement? cats = palette!.Q(className: "build__scroll");
                VisualElement? tools = palette.Q(className: "build__tools");
                Assert.That(cats, Is.Not.Null, "the palette built no category group");
                Assert.That(tools, Is.Not.Null, "the palette built no tools group");

                // A chip has to be the height the model counts rows in, or the cap is a count of
                // something else.
                VisualElement? chip = cats!.Q(className: "chip");
                Assert.That(chip, Is.Not.Null, "the palette built no category chips");
                Assert.That(chip!.worldBound.height, Is.EqualTo((float)HudLayout.BuildChip).Within(0.5f),
                    $"a chip is {chip.worldBound.height:0.#} px against a modelled {HudLayout.BuildChip}");

                float catHeight = cats.worldBound.height;
                float toolHeight = tools!.worldBound.height;
                Debug.Log($"[HudGeometry] build palette: categories {catHeight:0.#} px, " +
                          $"tools {toolHeight:0.#} px, panel {palette.worldBound.height:0.#}");

                Assert.That(catHeight, Is.LessThanOrEqualTo(HudLayout.BuildCatHeight + 0.5f),
                    $"the category group stands {catHeight:0.#} px against a cap of " +
                    $"{HudLayout.BuildCatHeight}, so it is still taking the panel's height");
                Assert.That(catHeight / HudLayout.BuildChipRow,
                    Is.LessThanOrEqualTo(HudLayout.BuildCatRows + 0.01f),
                    $"the categories are wrapping to more than {HudLayout.BuildCatRows} rows");

                // The second group has something in it the moment the palette opens, and none of
                // it is cut off by the panel it sits in. A tools group that only fills once the
                // player has guessed the chips above are clickable is a panel needing explanation.
                var toolChips = tools.Query(className: "chip").ToList();
                Assert.That(toolChips, Is.Not.Empty,
                    "the palette opened on no category, so the second group is empty");
                foreach (VisualElement toolChip in toolChips)
                    Assert.That(toolChip.worldBound.yMax,
                        Is.LessThanOrEqualTo(palette.worldBound.yMax + 0.5f),
                        "a tool chip is cut off by the bottom of the palette");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Every window carries an X, which is the owner's rule stated as a test rather than as a
        /// convention each call site has to remember.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryWindowHasAWayOutThatIsNotTheKeyboard()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                var windows = doc.rootVisualElement.Query(className: "window").ToList();
                Assert.That(windows.Count, Is.GreaterThanOrEqualTo(3),
                    "the Build palette, the Menu popover and the settings panel are all windows");

                foreach (VisualElement window in windows)
                {
                    Assert.That(window.Q(className: "panel__close"), Is.Not.Null,
                        $"the '{window.name}' window has no close button");
                    Assert.That(window.ClassListContains("panel"), Is.True,
                        $"the '{window.name}' window is not a panel, so it does not carry the fill");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>The bar button that raises a named popover.</summary>
        static VisualElement? ButtonFor(UIDocument doc, string popover)
        {
            string label = popover == "build" ? "Build" : "Menu";
            foreach (VisualElement item in doc.rootVisualElement.Query(className: "cmd").ToList())
            {
                var text = item.Q<Label>(className: "cmd__label");
                if (text != null && text.text == label) return item;
            }
            return null;
        }

        /// <summary>What a string really draws in a label's own face and size.</summary>
        static float Draws(Label label, string text) =>
            label.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined,
                                  0f, VisualElement.MeasureMode.Undefined).x;

        /// <summary>
        /// Nothing selected, no pane (owner, 2026-09-16). This is the state the HUD spends most
        /// of its life in, so it is the one worth a test of its own: the pane must be out of the
        /// tree rather than merely transparent, or it still takes clicks and still counts towards
        /// coverage.
        /// </summary>
        [UnityTest]
        public IEnumerator TheInspectPaneIsAbsentUntilSomethingIsSelected()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                VisualElement? pane = doc.rootVisualElement.Q(name: "inspect");
                Assert.That(pane, Is.Not.Null, "the shell built no inspect pane at all");
                Assert.That(pane!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "with nothing selected the inspect pane is still on screen");

                Assert.That(Regions(doc), Does.Not.ContainKey("inspect"),
                    "a hidden pane is still being counted as covered screen");

                Rect canvas = doc.rootVisualElement.worldBound;
                var content = new HudContent(
                    colonists: doc.rootVisualElement.Query(className: "card").ToList().Count,
                    storeRows: Visible(doc, "stores__row"),
                    alerts: Visible(doc, "alert"),
                    layers: doc.rootVisualElement.Query(className: "ruler__tick").ToList().Count,
                    needRows: 0);

                HudRect modelled = HudLayout.Solve(canvas.width, canvas.height, content)[HudRegion.Inspect];
                Assert.That(modelled.Height, Is.EqualTo(0f),
                    "the model still reserves a strip for a pane the shell does not build");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The stores panel is as wide as its widest row needs and no wider.
        ///
        /// <para><b>Why this is measured rather than chosen.</b> The width is a constant in
        /// <see cref="HudLayout"/> and a matching literal in the stylesheet, and the only way to
        /// know what it should be is to ask the text engine how wide the longest commodity name
        /// actually draws in the face and size the HUD uses. The panel was 288 px for rows whose
        /// longest label is six characters, which is the owner's report. This logs the figure it
        /// needs, so the next person to add a commodity with a long name gets a number rather
        /// than an opinion.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheStoresPanelIsWideEnoughForItsRowsAndNoWider()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                VisualElement stores = doc.rootVisualElement.Q(name: "stores")!;
                float needed = 0f;
                string widest = string.Empty;

                foreach (VisualElement line in Lines(stores))
                {
                    float width = NaturalWidth(line);
                    if (width <= needed) continue;
                    needed = width;
                    widest = string.Join(" / ", Texts(line));
                }

                // The panel's own frame and padding, which no row carries.
                float chrome = 2f * (HudTheme.BorderWidth + HudLayout.Pad);
                needed += chrome;

                Debug.Log($"[HudGeometry] stores needs {needed:0.#} px for '{widest}' " +
                          $"(chrome {chrome:0.#}); the model says {HudLayout.StoresWidth}");

                Assert.That(HudLayout.StoresWidth, Is.GreaterThanOrEqualTo(needed),
                    $"the stores panel is {HudLayout.StoresWidth} px and its widest line " +
                    $"('{widest}') needs {needed:0.#}, so something is clipped or wrapped");

                Assert.That(HudLayout.StoresWidth, Is.LessThanOrEqualTo(needed + 48f),
                    $"the stores panel is {HudLayout.StoresWidth} px for a widest line of " +
                    $"{needed:0.#} px, which is more empty column than the owner asked for");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>The panel's header and its visible rows: everything that decides its width.</summary>
        static IEnumerable<VisualElement> Lines(VisualElement stores)
        {
            foreach (VisualElement line in stores.Query(className: "panel__hdr").ToList())
                yield return line;
            foreach (VisualElement line in stores.Query(className: "stores__row").ToList())
                if (line.resolvedStyle.display != DisplayStyle.None)
                    yield return line;
        }

        static IEnumerable<string> Texts(VisualElement line)
        {
            foreach (Label label in line.Query<Label>().ToList())
                if (!string.IsNullOrEmpty(label.text))
                    yield return label.text;
        }

        /// <summary>
        /// How wide a row wants to be, which is not how wide it is: a label with
        /// <c>flex-grow: 1</c> measures back as whatever space the row gave it, so the laid-out
        /// width says nothing about the text. Each child is asked for its own natural width
        /// instead — the text engine's answer for a label, the resolved box for anything else —
        /// and the row's padding and each child's margins are added on top.
        /// </summary>
        static float NaturalWidth(VisualElement line)
        {
            float total = line.resolvedStyle.paddingLeft + line.resolvedStyle.paddingRight;

            foreach (VisualElement child in line.Children())
            {
                if (child.resolvedStyle.display == DisplayStyle.None) continue;

                float own = child is Label label && !string.IsNullOrEmpty(label.text)
                    ? label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined,
                                            0f, VisualElement.MeasureMode.Undefined).x
                    : child.resolvedStyle.width;

                // A nested row — the header's right-hand group — carries its own children.
                if (child.childCount > 0 && child is not Label) own = Mathf.Max(own, NaturalWidth(child));

                total += own + child.resolvedStyle.marginLeft + child.resolvedStyle.marginRight;
            }

            return total;
        }

        // ---------------------------------------------------------------- fixture

        /// <summary>Every framed region's box, keyed by the name the shell gives it. Regions that
        /// are hidden — the alerts panel with nothing to say, the Build palette, the menu, the
        /// settings panel — are left out, because a hidden panel covers nothing.</summary>
        static Dictionary<string, Rect> Regions(UIDocument doc)
        {
            var boxes = new Dictionary<string, Rect>();
            foreach (VisualElement region in doc.rootVisualElement.Query(className: "region").ToList())
            {
                if (region.resolvedStyle.display == DisplayStyle.None) continue;
                boxes[region.name] = region.worldBound;
            }

            // The colonist strip is not a framed panel — each card is its own — so it is measured
            // as the union of the cards, which is what HudLayout models.
            Rect? strip = null;
            foreach (VisualElement card in doc.rootVisualElement.Query(className: "card").ToList())
                strip = strip == null ? card.worldBound : Union(strip.Value, card.worldBound);
            if (strip != null) boxes["strip"] = strip.Value;

            VisualElement? bar = doc.rootVisualElement.Q(name: "bar");
            if (bar != null) boxes["bar"] = bar.worldBound;

            return boxes;
        }

        /// <summary>
        /// Every visible child's classes, height, top and vertical margins. A panel two pixels
        /// taller than the model is not something anybody can diagnose from the total, and a
        /// second run of the PlayMode gate to find out costs a minute and a half.
        /// </summary>
        static string Describe(VisualElement element)
        {
            var parts = new List<string>();
            foreach (VisualElement child in element.Children())
            {
                if (child.resolvedStyle.display == DisplayStyle.None) continue;
                Rect box = child.worldBound;
                parts.Add($"[{string.Join(".", child.GetClasses())} h{box.height:0.##} " +
                          $"y{box.yMin:0.##} m{child.resolvedStyle.marginTop:0.##}/" +
                          $"{child.resolvedStyle.marginBottom:0.##}]");
            }
            return string.Join(" ", parts);
        }

        static int Visible(UIDocument doc, string className)
        {
            int count = 0;
            foreach (VisualElement element in doc.rootVisualElement.Query(className: className).ToList())
                if (element.resolvedStyle.display != DisplayStyle.None) count++;
            return count;
        }

        static Rect Intersection(Rect a, Rect b) => Rect.MinMaxRect(
            Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin),
            Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(
            Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        /// <summary>
        /// Real seconds, not frame counts: the shell primes on the first frame a world exists, and
        /// the cadence buckets after that are wall-clock, which in this harness advances by well
        /// under a millisecond a frame.
        /// </summary>
        static IEnumerator Settle(UIDocument doc)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            for (int i = 0; i < 6; i++) yield return null;
        }

        /// <summary>
        /// The play scene's HUD stack over a small world, rendered into a texture of the size
        /// under test. The panel scales with the screen, so the <i>logical</i> canvas it lays out
        /// against depends on the aspect it is given — which is exactly the thing being measured,
        /// and is why the resolution is applied to the panel rather than to the game window.
        /// </summary>
        static GameObject Build(out OdysseyBootstrap boot, out UIDocument doc, Vector2Int resolution)
        {
            var root = new GameObject("HudGeometry");

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            var rig = cameraObject.AddComponent<SliceCameraRig>();

            var bootObject = new GameObject("Bootstrap");
            bootObject.transform.SetParent(root.transform, false);
            bootObject.SetActive(false);
            boot = bootObject.AddComponent<OdysseyBootstrap>();
            boot.sizeX = 60;
            boot.sizeZ = 60;
            boot.seed = 1;
            boot.barrenMap = true;
            boot.grassScatter = 0;
            boot.cameraRig = rig;
            bootObject.AddComponent<SelectionPresenter>();

            doc = bootObject.AddComponent<UIDocument>();
            PanelSettings? settings = null;
#if UNITY_EDITOR
            settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
#endif
            settings = settings != null ? Object.Instantiate(settings) : ScriptableObject.CreateInstance<PanelSettings>();
            if (settings.themeStyleSheet == null)
                settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.targetTexture = new RenderTexture(resolution.x, resolution.y, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            doc.panelSettings = settings;

            var shell = bootObject.AddComponent<HudShell>();
#if UNITY_EDITOR
            shell.hudStyles = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(StylesPath);
            shell.uiFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
            shell.monoFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(MonoFontPath);
#endif
            bootObject.SetActive(true);
            return root;
        }
    }
}
