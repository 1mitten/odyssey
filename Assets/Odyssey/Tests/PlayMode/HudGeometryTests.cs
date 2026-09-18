#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

                    // A hotkey cap is a legend, not a word. What is forbidden is an upper-case
                    // fragment of two or three letters standing in for a name.
                    //
                    // The colonist-initial exemption went on 2026-09-18 with the letter it
                    // excused: a roster card draws that colonist's own face now
                    // (docs/design/20-avatars.md), so there is no initial to let through. A rule
                    // that shrinks as the interface improves is the shape this one should have.
                    //
                    // The caps excuse themselves by their role rather than by this list naming
                    // every place one is drawn — HudText.Apply adds HudText.KeyCapClass to
                    // anything set in the Hotkey role. The list was three classes long and the
                    // Build palette's ESC hint would have been a fourth; a rule that has to be
                    // extended every time it is obeyed is a rule that eventually is not.
                    if (label.ClassListContains(HudText.KeyCapClass)) continue;
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
                    // Seed zero reads the pool from the top, so this still walks every name the
                    // pool holds — which is what the widest-name measurement is after (U40).
                    string name = ColonistNames.Of(0u, new PawnId(id));
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
        /// A popover raised from the command bar lands flush on the bar, carries a way out, and —
        /// unless it is the Build palette — sits over the button that raised it (owner,
        /// 2026-09-17).
        ///
        /// <para><b>Only the player loop can answer this.</b> Where a popover sits is decided from
        /// the laid-out bar — the reflow moves buttons as items go into Menu, and the interface
        /// scale moves them again — so the model can state the rule but not that the shell obeys
        /// it. What is measured here is the realised boxes: the popover's bottom against the bar's
        /// top, and its left against the button's.</para>
        ///
        /// <para><b>The Build palette is exempt from the second half, on purpose.</b> It is pinned
        /// into the bottom-left corner of the screen rather than anchored to its cap (owner, later
        /// the same day: <i>"it needs to pin/dock against the bottom and left for space — so up
        /// against the left screen border"</i>), because <c>PopoverLeft</c> puts it a few pixels of
        /// the bar's own padding short of the edge. It is exempted here rather than dropped from
        /// the loop, because every other clause — flush on the bar, on the screen at both ends, a
        /// close button in its top right — still applies to it and is worth keeping.</para>
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

                    if (name == "build")
                    {
                        Assert.That(box.xMin, Is.EqualTo(canvas.xMin).Within(0.5f),
                            "the Build palette is pinned to the left edge of the screen and is " +
                            $"{box.xMin - canvas.xMin:0.#} px off it");
                    }
                    else
                    {
                        float wanted = HudLayout.PopoverLeft(
                            button.xMin - canvas.xMin, box.width, canvas.width) + canvas.xMin;
                        Assert.That(box.xMin, Is.EqualTo(wanted).Within(0.5f),
                            $"the {name} popover is not over the button that raised it");
                    }

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
        /// Giving an order shuts whatever menu was up over the board, and gives the order.
        ///
        /// <para>Owner, 2026-09-18: <i>"if I'm in the build menu (or any other menu) and I click on
        /// an order — I expect that menu to be closed down and the … order would happen"</i>. The
        /// orders strip is the one control that is on screen whether or not anything else is, which
        /// is why it was taken out of the palette's header in the first place — so it is precisely
        /// the control a player reaches for from inside something else.</para>
        ///
        /// <para><b>Both halves are asserted, and the second is the one worth having.</b> A close
        /// that also swallowed the order would look right in a screenshot and be a worse bug than
        /// the overlap it replaced: the menu goes, and nothing the player asked for happens. So the
        /// tool is checked in the director afterwards, not just the panel's display.</para>
        ///
        /// <para>Driven with a real <c>ClickEvent</c> on the real button, the way
        /// <c>BedOwnerPickerTests</c> is — this cannot be a fast-tier test, because what is being
        /// proved is that a shell built by the composition root wires the two together.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AnOrderClosesWhateverMenuWasOpenAndStillHappens()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                // "the build menu (or any other menu)" — both bar popovers, one after the other.
                foreach (string menu in new[] { "build", "menu" })
                {
                    VisualElement? opener = ButtonFor(doc, menu);
                    Assert.That(opener, Is.Not.Null, $"the command bar has no {menu} button");
                    using (var open = ClickEvent.GetPooled())
                    {
                        open.target = opener;
                        opener!.SendEvent(open);
                    }
                    yield return Settle(doc);

                    VisualElement? panel = doc.rootVisualElement.Q(name: menu);
                    Assert.That(panel, Is.Not.Null, $"the shell built no {menu} popover");
                    Assert.That(panel!.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                        $"the {menu} popover did not open, so this proves nothing");

                    // Put the order down first if a previous pass left it held: the strip toggles,
                    // and a test that armed nothing would pass its "menu closed" half regardless.
                    boot.Directors!.Designate.Tool = DesignateTool.None;

                    VisualElement? order =
                        doc.rootVisualElement.Q(name: "action-" + PaletteTools.Fell);
                    Assert.That(order, Is.Not.Null, "the orders strip has no Chop button");
                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = order;
                        order!.SendEvent(click);
                    }
                    yield return Settle(doc);

                    Assert.That(panel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                        $"giving an order left the {menu} popover standing over the board");
                    Assert.That(boot.Directors!.Designate.Tool, Is.EqualTo(DesignateTool.Fell),
                        $"the {menu} popover closed but the order never happened");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Open the Build palette and hand back the panel.
        /// </summary>
        static IEnumerator OpenPalette(UIDocument doc)
        {
            VisualElement? button = ButtonFor(doc, "build");
            Assert.That(button, Is.Not.Null, "the command bar has no Build button");
            using (var click = ClickEvent.GetPooled())
            {
                click.target = button;
                button!.SendEvent(click);
            }
            yield return Settle(doc);
        }

        /// <summary>Put the palette into a given layout by clicking its switcher, the way a
        /// player would, rather than by writing to the director behind its back.</summary>
        static IEnumerator SwitchLayout(UIDocument doc, BuildPaletteLayout layout)
        {
            VisualElement? button = doc.rootVisualElement.Q(name: "layout-" + layout);
            Assert.That(button, Is.Not.Null, $"the switcher has no {layout} button");
            using (var click = ClickEvent.GetPooled())
            {
                click.target = button;
                button!.SendEvent(click);
            }
            yield return Settle(doc);
        }

        /// <summary>
        /// No row of the Build palette overflows, in any layout, at any of the three resolutions.
        ///
        /// <para><b>This is the specification's own acceptance criterion</b>, which it states as
        /// <c>scrollWidth &lt;= clientWidth</c> on every flex row — a browser's way of asking
        /// whether the contents fit the box. UI Toolkit has no such property, so the same question
        /// is asked of the laid-out children directly: no child may end beyond the right edge of
        /// the row that holds it. It is the criterion that matters most for Bar, whose whole
        /// premise is that two dense rows fit inside 1920 minus its margins, and which would
        /// otherwise fail by silently clipping the last material.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator NoRowOfTheBuildPaletteOverflowsInAnyLayoutAtAnyResolution()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);
                    yield return OpenPalette(doc);

                    float slack = SlackFor(doc.rootVisualElement.worldBound, resolution);

                    foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                    {
                        yield return SwitchLayout(doc, layout);

                        VisualElement? palette = doc.rootVisualElement.Q(name: "build");
                        Assert.That(palette, Is.Not.Null);

                        int rows = 0;
                        foreach (VisualElement row in Rows(palette!))
                        {
                            rows++;
                            Rect box = row.worldBound;
                            foreach (VisualElement child in row.Children())
                            {
                                if (child.resolvedStyle.display == DisplayStyle.None) continue;
                                Rect kid = child.worldBound;
                                Assert.That(kid.xMax, Is.LessThanOrEqualTo(box.xMax + slack),
                                    $"at {resolution.x}x{resolution.y} in {layout}, a child of " +
                                    $"'{ClassPath(row)}' ends at {kid.xMax:0.#} against a row " +
                                    $"ending at {box.xMax:0.#} — the row overflows");
                            }
                        }

                        Assert.That(rows, Is.GreaterThan(0),
                            $"{layout} built no rows at all, so this test measured nothing");

                        // And the panel itself fits the screen it is docked in.
                        Assert.That(palette!.worldBound.xMax,
                            Is.LessThanOrEqualTo(doc.rootVisualElement.worldBound.xMax + slack),
                            $"the {layout} palette runs off the right of a {resolution.x}px screen");
                    }
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>
        /// The palette's header is one row in every layout: BUILD and the breadcrumb on the left,
        /// the switcher, ESC and the X on the right of the same line (owner, 2026-09-17: <i>"shift
        /// the toggle view, esc and x onto the same row as the build text — this will tidy that
        /// up"</i>).
        ///
        /// <para>Asserted on the two halves' realised boxes rather than on the resolved flex
        /// direction, because the direction being a row and the halves still standing one above
        /// the other is a state this panel has actually been in: Rows stacked them with a
        /// <c>column</c> override, and the fault a later session would reintroduce is a margin or
        /// a width that wraps them, which no property reports. Two halves are on one row when
        /// their vertical spans overlap and the controls end at the right of the identity.</para>
        ///
        /// <para>It also checks the row still fits, at every category and in the narrowest layout,
        /// because one row is only tidy while nothing runs off the end of it — the breadcrumb
        /// ellipsises, and a category whose name pushed the X off the panel would be the fault
        /// this replaced.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheBuildHeaderIsOneRowInEveryLayout()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);
                    yield return OpenPalette(doc);

                    float slack = SlackFor(doc.rootVisualElement.worldBound, resolution);

                    foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                    {
                        yield return SwitchLayout(doc, layout);

                        VisualElement? palette = doc.rootVisualElement.Q(name: "build");
                        VisualElement? identity = palette!.Q(className: "bp__hdr-id");
                        VisualElement? controls = palette.Q(className: "bp__hdr-ctl");
                        VisualElement? header = palette.Q(className: "bp__hdr");
                        Assert.That(identity, Is.Not.Null, $"{layout} has no header identity");
                        Assert.That(controls, Is.Not.Null, $"{layout} has no header controls");

                        for (int category = 0; category < PaletteTools.Categories.Length; category++)
                        {
                            VisualElement? tile =
                                palette.Q(name: "cat-" + PaletteTools.Categories[category].key);
                            if (tile != null)
                            {
                                using (var click = ClickEvent.GetPooled())
                                {
                                    click.target = tile;
                                    tile.SendEvent(click);
                                }
                                yield return Settle(doc);
                            }

                            Rect id = identity!.worldBound;
                            Rect ctl = controls!.worldBound;
                            Rect box = header!.worldBound;

                            Assert.That(id.yMin, Is.LessThan(ctl.yMax - slack),
                                $"in {layout} at {resolution.x}x{resolution.y} the controls sit " +
                                $"below the BUILD line: identity {id}, controls {ctl}");
                            Assert.That(ctl.yMin, Is.LessThan(id.yMax - slack),
                                $"in {layout} at {resolution.x}x{resolution.y} the BUILD line sits " +
                                $"below the controls: identity {id}, controls {ctl}");
                            Assert.That(ctl.xMin, Is.GreaterThanOrEqualTo(id.xMax - slack),
                                $"in {layout} the controls start inside the identity half");
                            Assert.That(ctl.xMax, Is.LessThanOrEqualTo(box.xMax + slack),
                                $"in {layout} the header's controls run {ctl.xMax - box.xMax:0.#} " +
                                "px past the end of their own row");
                        }
                    }
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>Every flex row in the palette: the bands, the grids and the bar's two rows.</summary>
        static List<VisualElement> Rows(VisualElement palette)
        {
            var rows = new List<VisualElement>();
            foreach (string className in new[]
                     {
                         "bp__hdr", "bp__hdr-id", "bp__hdr-ctl", "bp__band", "bp__bar-row",
                         "bp__sub-grid", "bp__mat-grid", "bp__bar-subs", "bp__bar-mats",
                         "bp__mats-row",
                     })
                rows.AddRange(palette.Query(className: className).ToList());
            return rows;
        }

        /// <summary>An element's classes, for a failure message that names the row that
        /// overflowed rather than leaving it to be found by eye.</summary>
        static string ClassPath(VisualElement element)
        {
            var classes = new List<string>(element.GetClasses());
            return classes.Count > 0 ? string.Join(".", classes) : element.name;
        }

        /// <summary>
        /// Nothing in the palette draws the placeholder square.
        ///
        /// <para><b>The specification forbids it by name</b> — "no empty-checkbox placeholders
        /// anywhere in the palette" — and it is exactly the sort of rule that rots quietly: a
        /// category added without a shape draws an outlined box, which looks deliberate, sits
        /// beside six that are not, and reads as art nobody has got to yet. This walks every glyph
        /// the open panel contains and names the one that fell through.</para>
        ///
        /// <para>The material tier is included on purpose. Those are <c>IconBadge</c> slots on the
        /// game's own sprites, and an <c>IconBadge</c> whose key has no art falls back to the
        /// square — so this is also the test that fails if the wood or stone art is ever
        /// unloadable.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EveryTileInTheBuildPaletteDrawsSomething()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                yield return OpenPalette(doc);

                foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                {
                    yield return SwitchLayout(doc, layout);

                    VisualElement? palette = doc.rootVisualElement.Q(name: "build");
                    var glyphs = palette!.Query<HudGlyph>().ToList();
                    Assert.That(glyphs, Is.Not.Empty, $"{layout} drew no icons at all");

                    foreach (HudGlyph glyph in glyphs)
                    {
                        if (glyph is IconBadge badge)
                        {
                            Assert.That(IconArt.Has(badge.Key), Is.True,
                                $"in {layout}, the material slot '{badge.Key}' has no art and is " +
                                "drawing the placeholder square");
                            continue;
                        }

                        Assert.That(glyph.Kind, Is.Not.EqualTo(HudGlyphKind.Placeholder),
                            $"in {layout}, a tile in '{ClassPath(glyph.parent)}' is drawing the " +
                            "placeholder square — something on the palette has no shape");
                    }
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Every key the palette can put on screen has a shape of its own.
        ///
        /// <para>The general form of the test above, and cheaper: it does not need the panel open
        /// or a category selected, so it covers the twenty-six sub-types that only appear once
        /// their category is chosen. Between the two, no tile can reach a player as a square.</para>
        /// </summary>
        [Test]
        public void EveryPaletteKeyHasItsOwnShape()
        {
            foreach (string key in PaletteTools.IconKeys)
                Assert.That(PaletteGlyphs.Has(key), Is.True,
                    $"{key} is on the Build palette and has no drawn shape, so it would draw the " +
                    "placeholder square that the specification forbids");
        }

        /// <summary>
        /// Write a picture of each layout to <c>Logs/</c>.
        ///
        /// <para><b>Why a test takes photographs.</b> Everything else here asks whether the
        /// palette <i>fits</i> — nothing overflows, nothing overlaps, no tile falls back to the
        /// placeholder square, every label clears its contrast floor. None of that can say whether
        /// the shape meant to be a bench reads as a bench, and the palette's thirty-seven icons are
        /// hand-written vector paths, which is exactly where a mirrored axis hides: it still draws
        /// something, it still passes, and it still looks like nothing.</para>
        ///
        /// <para>The rig already renders the real HUD into a render texture at the real
        /// resolution, so the picture costs one <c>ReadPixels</c> and is of the actual panel rather
        /// than of a mock-up of it. It asserts only that it managed to write something: it is here
        /// to produce the artefact the owner judges, in the same spirit as the <c>*Check</c>
        /// harnesses, not to have an opinion about what is in it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLayoutSitsForItsPortrait()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                // Nothing else clears this texture. In the game a camera draws the world into the
                // frame before the panel goes over it; in the rig the panel is all there is, so
                // without this each portrait carries the last one underneath it — which is how the
                // first set came out with the previous layout showing around the edges of the
                // next. A picture with a ghost in it is worse than no picture: it invites a
                // diagnosis of a bug that is not there.
                doc.panelSettings.clearColor = true;
                doc.panelSettings.colorClearValue = new Color(0.06f, 0.08f, 0.09f, 1f);

                yield return Settle(doc);
                yield return OpenPalette(doc);

                Directory.CreateDirectory("Logs");

                foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                {
                    yield return SwitchLayout(doc, layout);

                    // One more frame after the switch, so the texture holds the panel that was
                    // just built rather than the one it replaced.
                    yield return null;

                    string path = $"Logs/palette-{layout.ToString().ToLowerInvariant()}.png";
                    Capture(doc, path);
                    Assert.That(File.Exists(path), Is.True, $"no picture was written for {layout}");
                    Debug.Log($"[HudGeometry] wrote {path}");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        static void Capture(UIDocument doc, string path)
        {
            RenderTexture? target = doc.panelSettings.targetTexture;
            if (target == null) return;

            RenderTexture previous = RenderTexture.active;
            var picture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(path, picture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(picture);
            }
        }

        /// <summary>
        /// The Build cap on the command bar says whether the player is in build mode, and stops
        /// saying it when they leave.
        ///
        /// <para><b>The owner's bug, stated as a test</b> (2026-09-17): <i>"when I come out of
        /// build mode by escaping etc, the build button still stays bold when it shouldn't and is
        /// confusing — it's an indicator to whether you are truly in build mode"</i>. The cap was
        /// drawn with a permanent accent fill, so the wash that was supposed to mean "open" was
        /// invisible under it and the button looked identical either way.</para>
        ///
        /// <para>It checks the resolved fill rather than only the class, because the class being
        /// set and the fill not following is exactly the shape this fault took the first time. And
        /// it arms the tool through the panel rather than through the director, because the whole
        /// question is whether the cap follows what the player did.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheBuildCapIsLitOnlyWhileAToolIsHeld()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                VisualElement? cap = ButtonFor(doc, "build");
                Assert.That(cap, Is.Not.Null, "the command bar has no Build cap");

                Color resting = cap!.resolvedStyle.backgroundColor;
                Assert.That(cap.ClassListContains("cmd--on"), Is.False,
                    "the Build cap is lit before anything has been opened or armed");

                // Opening the panel is not build mode. Nothing is in the player's hand yet, and a
                // click on the world would still select rather than place.
                yield return OpenPalette(doc);
                Assert.That(cap.ClassListContains("cmd--on"), Is.False,
                    "the Build cap lit merely because the panel was opened");

                VisualElement? wall = doc.rootVisualElement.Q(name: "sub-" + PaletteTools.Wall);
                Assert.That(wall, Is.Not.Null);
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = wall;
                    wall!.SendEvent(click);
                }
                yield return Settle(doc);

                Assert.That(cap.ClassListContains("cmd--on"), Is.True,
                    "the Build cap is dark with a wall in the player's hand");
                Color lit = cap.resolvedStyle.backgroundColor;
                Assert.That(lit, Is.Not.EqualTo(resting),
                    $"the Build cap draws {lit} both in and out of build mode, so it says nothing");
                Assert.That(lit.r, Is.EqualTo(HudTokens.Accent.r).Within(0.02f),
                    "the lit cap is not the accent fill");

                // What Escape does first: put the tool down. The panel stays open, and the cap
                // must go dark anyway — that is the owner's report.
                boot.Directors!.Designate.Tool = DesignateTool.None;
                yield return Settle(doc);

                Assert.That(cap.ClassListContains("cmd--on"), Is.False,
                    "the Build cap is still lit after the tool was put down — the owner's report");
                Assert.That(cap.resolvedStyle.backgroundColor, Is.EqualTo(resting),
                    "the Build cap did not go back to its resting fill");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The orders strip stands in the right-hand gutter, against the edge of the screen and
        /// directly under the depth rail, at every resolution the criteria name (owner,
        /// 2026-09-17).
        ///
        /// <para>Measured on the realised boxes rather than on the model, because the whole claim
        /// is about a position the model can only describe: the rail's height is the world's to
        /// decide, and "under it" is therefore a relationship between two boxes rather than a
        /// number anybody can write down. Nothing is opened first — that is the point of the
        /// strip.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheOrdersStripSitsInTheGutterUnderTheDepthRail()
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, resolution);
                try
                {
                    yield return Settle(doc);

                    Rect canvas = doc.rootVisualElement.worldBound;
                    float slack = SlackFor(canvas, resolution);

                    VisualElement? strip = doc.rootVisualElement.Q(name: "orders");
                    VisualElement? rail = doc.rootVisualElement.Q(name: "rail");
                    VisualElement? bar = doc.rootVisualElement.Q(name: "bar");
                    Assert.That(strip, Is.Not.Null, "the shell built no orders strip");
                    Assert.That(rail, Is.Not.Null, "the shell built no depth rail");
                    Assert.That(bar, Is.Not.Null, "the shell built no command bar");

                    Rect box = strip!.worldBound;
                    Rect railBox = rail!.worldBound;
                    Debug.Log($"[HudGeometry] orders at {resolution.x}x{resolution.y}: {box}, " +
                              $"rail {railBox}, canvas {canvas}");

                    Assert.That(box.xMax, Is.EqualTo(canvas.xMax).Within(slack + 0.5f),
                        $"the orders strip stops {canvas.xMax - box.xMax:0.#} px short of the right " +
                        $"edge at {resolution.x}x{resolution.y}");
                    Assert.That(box.xMax, Is.EqualTo(railBox.xMax).Within(slack + 0.5f),
                        "the orders strip and the depth rail are not in one gutter");
                    Assert.That(box.yMin, Is.GreaterThanOrEqualTo(railBox.yMax - slack),
                        $"the orders strip starts at {box.yMin:0.#}, above the rail's bottom at " +
                        $"{railBox.yMax:0.#} — it is over the depth control rather than under it");
                    Assert.That(box.yMax, Is.LessThanOrEqualTo(bar!.worldBound.yMin + slack),
                        $"the orders strip runs {box.yMax - bar.worldBound.yMin:0.#} px into the " +
                        "command bar");
                }
                finally
                {
                    Object.Destroy(root);
                }
            }
        }

        /// <summary>
        /// Every order has a button in the strip, and pressing one arms it with nothing else open.
        ///
        /// <para>That last clause is the reason the strip exists (owner, 2026-09-17: <i>"this
        /// enables us to quickly give orders without having to click the build button"</i>), so it
        /// is asserted rather than assumed: the palette is never opened in this test, and the tool
        /// still ends up in the player's hand. Pressing the same button again puts it down, which
        /// is the toggle every tool in this game obeys.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator PressingAnOrderArmsItWithoutOpeningTheBuildPalette()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                VisualElement? palette = doc.rootVisualElement.Q(name: "build");
                Assert.That(palette, Is.Not.Null, "the shell built no Build palette");
                Assert.That(palette!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "the Build palette is open before the test has touched anything");

                foreach (string key in PaletteTools.Pinned)
                {
                    VisualElement? button = doc.rootVisualElement.Q(name: "action-" + key);
                    Assert.That(button, Is.Not.Null, $"the orders strip has no button for {key}");
                    Assert.That(button!.ClassListContains("ord__btn"), Is.True,
                        $"{key} is drawn somewhere other than the orders strip");

                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = button;
                        button.SendEvent(click);
                    }
                    yield return Settle(doc);

                    Assert.That(PaletteTools.TryGet(key, out PaletteTool tool), Is.True,
                        $"{key} is in the strip and is not a live tool");
                    Assert.That(tool.IsArmed(boot.Directors!.Designate), Is.True,
                        $"pressing {key} in the orders strip armed nothing");
                    Assert.That(palette.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                        $"pressing {key} opened the Build palette, which is the trip the strip " +
                        "exists to save");
                    Assert.That(button.ClassListContains("ord__btn--on"), Is.True,
                        $"the {key} button does not say it is held");

                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = button;
                        button.SendEvent(click);
                    }
                    yield return Settle(doc);

                    Assert.That(tool.IsArmed(boot.Directors!.Designate), Is.False,
                        $"pressing {key} a second time did not put it down");
                    Assert.That(button.ClassListContains("ord__btn--on"), Is.False,
                        $"the {key} button is still lit with nothing in the player's hand");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// The armed banner wears the held order's colour, in a thick border, one word, just above
        /// the command bar (owner, 2026-09-17).
        ///
        /// <para>Every clause of that is checked, because each is a separate way for it to be
        /// wrong: the colour comes from the same token the strip's button is painted with, so the
        /// two cannot disagree about what mode a colour means; the width is the token rather than
        /// the hairline it was; the second line is gone, so the banner holds exactly one label;
        /// and it sits within a gap of the bar rather than floating in the middle of the board.
        /// The colour is read off the <i>resolved</i> border rather than off a class, which is the
        /// shape the Build cap's fault took — the class set and the fill not following.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheArmedBannerWearsTheHeldOrdersColour()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);

                float slack = SlackFor(doc.rootVisualElement.worldBound, Resolutions[1]);

                VisualElement? banner = doc.rootVisualElement.Q(name: "armed");
                Assert.That(banner, Is.Not.Null, "the shell built no armed banner");
                Assert.That(banner!.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "the armed banner is showing with nothing in the player's hand");

                // Every order, so a hue that was written for one and not the others fails here
                // rather than in a playtest of whichever one nobody tried.
                foreach (string key in PaletteTools.Pinned)
                {
                    VisualElement? button = doc.rootVisualElement.Q(name: "action-" + key);
                    Assert.That(button, Is.Not.Null, $"the orders strip has no button for {key}");
                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = button;
                        button!.SendEvent(click);
                    }
                    yield return Settle(doc);

                    Assert.That(banner.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                        $"holding {key} says nothing over the board");

                    Color want = HudTokens.Convert(HudTheme.PinnedActionHue(key)!.Value);
                    Color edge = banner.resolvedStyle.borderTopColor;
                    Debug.Log($"[HudGeometry] armed banner for {key}: border {edge}, want {want}");
                    Assert.That(edge.r, Is.EqualTo(want.r).Within(0.02f),
                        $"the banner's border is not {key}'s colour");
                    Assert.That(edge.g, Is.EqualTo(want.g).Within(0.02f),
                        $"the banner's border is not {key}'s colour");
                    Assert.That(edge.b, Is.EqualTo(want.b).Within(0.02f),
                        $"the banner's border is not {key}'s colour");

                    Assert.That(banner.resolvedStyle.borderTopWidth,
                        Is.EqualTo((float)HudTheme.ArmedBorderWidth).Within(1f),
                        "the banner's border is not the thick one the owner asked for");

                    int labels = banner.Query<Label>().ToList().Count;
                    Assert.That(labels, Is.EqualTo(1),
                        "the armed banner says more than what is held — the second line came off " +
                        "on 2026-09-17");

                    Label word = banner.Query<Label>().First();
                    Assert.That(word.text, Is.EqualTo(Registry.Label(key)),
                        $"the banner calls {key} something the registry does not");

                    // The floor: every order draws at least the width of the longest of them, so
                    // the banner does not resize under the eye as the mode changes. Reported as
                    // well as asserted, because whether the four come out *equal* depends on
                    // whether the modelled advance covers the real face, and the number is worth
                    // having in the log rather than inferring from a pass.
                    float width = banner.worldBound.width;
                    Debug.Log($"[HudGeometry] armed banner for {key}: \"{word.text}\" " +
                              $"{width:0.#} px against a floor of {HudLayout.ArmedWidth:0.#}");
                    Assert.That(width, Is.GreaterThanOrEqualTo(HudLayout.ArmedWidth - slack),
                        $"the banner for {key} is narrower than the longest order");

                    // Down by the controls it is about, not adrift in the middle of the board.
                    VisualElement? bar = doc.rootVisualElement.Q(name: "bar");
                    float gap = bar!.worldBound.yMin - banner.worldBound.yMax;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(0f),
                        $"the armed banner runs {-gap:0.#} px into the command bar");
                    Assert.That(gap, Is.LessThanOrEqualTo(HudLayout.Gap * 2f),
                        $"the armed banner floats {gap:0.#} px above the command bar");

                    // Put it down again, so the next order starts from nothing held.
                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = button;
                        button.SendEvent(click);
                    }
                    yield return Settle(doc);
                }

                Assert.That(banner.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "the armed banner is still showing after the last order was put down");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Opening Build puts away whatever was being inspected, and the palette then sits in the
        /// bottom-left corner: hard against the left edge and on the command bar (owner,
        /// 2026-09-17).
        /// </summary>
        [UnityTest]
        public IEnumerator TheBuildPaletteDocksIntoTheBottomLeftCorner()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                yield return OpenPalette(doc);

                float slack = SlackFor(doc.rootVisualElement.worldBound, Resolutions[1]);

                foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                {
                    yield return SwitchLayout(doc, layout);

                    Rect panel = doc.rootVisualElement.Q(name: "build")!.worldBound;
                    Rect screen = doc.rootVisualElement.worldBound;

                    Assert.That(panel.xMin, Is.EqualTo(screen.xMin).Within(slack),
                        $"the {layout} palette starts at {panel.xMin:0.#} rather than against the " +
                        "left edge of the screen");

                    VisualElement? bar = doc.rootVisualElement.Q(className: "commandbar");
                    Assert.That(bar, Is.Not.Null);
                    Assert.That(panel.yMax, Is.EqualTo(bar!.worldBound.yMin).Within(slack + 2f),
                        $"the {layout} palette ends at {panel.yMax:0.#} against a command bar " +
                        $"starting at {bar.worldBound.yMin:0.#}, so it is not sitting on the bar");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Rows holds its height too, whatever category is open (owner, 2026-09-17: <i>"the height
        /// needs to stay fixed — ie as tall as the structure menu/selection goes so it can
        /// accommodate all of the menus"</i>).
        ///
        /// <para>Rail's promise was a property of that layout; this is the same promise made of the
        /// default, and for a plainer reason — the panel is docked on the command bar and grows
        /// upward, so a category with fewer sub-types than the last one does not shrink neatly, it
        /// drops the whole control down the screen while the player is aiming at it.</para>
        ///
        /// <para>Unlike Rail's four-column grid, where the row count is arithmetic on the number of
        /// tools, Rows wraps its sub-types by how wide their <i>words</i> are. That is a fact about
        /// the text engine, so the reserved height is a measured constant rather than a derived
        /// one, and this test prints every category's band on every run so that the constant can be
        /// re-derived from the output rather than guessed at again.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheRowsLayoutKeepsItsHeightWhateverCategoryIsOpen()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                yield return OpenPalette(doc);
                yield return SwitchLayout(doc, BuildPaletteLayout.Rows);

                float first = -1f;
                for (int i = 0; i < PaletteTools.Categories.Length; i++)
                {
                    VisualElement? tile =
                        doc.rootVisualElement.Q(name: "cat-" + PaletteTools.Categories[i].key);
                    Assert.That(tile, Is.Not.Null, $"Rows has no {Registry.Label(PaletteTools.Categories[i].key)} tile");
                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = tile;
                        tile!.SendEvent(click);
                    }
                    yield return Settle(doc);

                    VisualElement panel = doc.rootVisualElement.Q(name: "build")!;
                    float height = panel.worldBound.height;
                    if (first < 0f) first = height;

                    Debug.Log($"[HudGeometry] rows {Registry.Label(PaletteTools.Categories[i].key)}: " +
                              $"panel {height:0.#}, " +
                              $"subs {panel.Q(className: "bp__subs")?.worldBound.height ?? -1:0.#}, " +
                              $"mats {panel.Q(className: "bp__mats-row")?.worldBound.height ?? -1:0.#}");

                    Assert.That(height, Is.EqualTo(first).Within(1f),
                        $"the Rows palette stands {height:0.#} px on " +
                        $"{Registry.Label(PaletteTools.Categories[i].key)} against {first:0.#} on the first " +
                        "category, so opening a category moves the whole control up or down the " +
                        "screen while the player is aiming at it");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Rail's whole argument: its height does not change when the category does, so nothing
        /// below it reflows.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRailLayoutKeepsItsHeightWhateverCategoryIsOpen()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                yield return OpenPalette(doc);
                yield return SwitchLayout(doc, BuildPaletteLayout.Rail);

                float first = -1f;
                for (int i = 0; i < PaletteTools.Categories.Length; i++)
                {
                    VisualElement? tile =
                        doc.rootVisualElement.Q(name: "cat-" + PaletteTools.Categories[i].key);
                    Assert.That(tile, Is.Not.Null, $"the rail has no {Registry.Label(PaletteTools.Categories[i].key)} row");
                    using (var click = ClickEvent.GetPooled())
                    {
                        click.target = tile;
                        tile!.SendEvent(click);
                    }
                    yield return Settle(doc);

                    VisualElement panel = doc.rootVisualElement.Q(name: "build")!;
                    float height = panel.worldBound.height;
                    if (first < 0f) first = height;

                    // Printed every run, because "it stands the same height" is the whole claim
                    // and a failure that only says which two disagree costs a second run to find
                    // out what the shape of the disagreement is.
                    Debug.Log($"[HudGeometry] rail {Registry.Label(PaletteTools.Categories[i].key)}: " +
                              $"panel {height:0.#}, " +
                              $"subs {panel.Q(className: "bp__sub-grid")?.worldBound.height ?? -1:0.#}, " +
                              $"mats {panel.Q(className: "bp__mat-grid")?.worldBound.height ?? -1:0.#}, " +
                              $"pane {panel.Q(className: "bp__pane")?.worldBound.height ?? -1:0.#}");

                    Assert.That(height, Is.EqualTo(first).Within(1f),
                        $"the Rail palette stands {height:0.#} px on " +
                        $"{Registry.Label(PaletteTools.Categories[i].key)} against {first:0.#} on the first " +
                        "category, so opening a category reflows everything under the panel");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Switching layout in the running game keeps the selection, which the fast tier proves
        /// about the model and this proves about the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingLayoutOnScreenKeepsWhatWasArmed()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                yield return OpenPalette(doc);

                VisualElement? wall = doc.rootVisualElement.Q(name: "sub-" + PaletteTools.Wall);
                Assert.That(wall, Is.Not.Null, "the palette did not open on Structure");
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = wall;
                    wall!.SendEvent(click);
                }
                yield return Settle(doc);

                foreach (BuildPaletteLayout layout in BuildPaletteModel.Layouts)
                {
                    yield return SwitchLayout(doc, layout);

                    VisualElement? armed = doc.rootVisualElement.Q(name: "sub-" + PaletteTools.Wall);
                    Assert.That(armed, Is.Not.Null, $"{layout} does not draw the armed sub-type");
                    Assert.That(armed!.ClassListContains("bp__tile--on"), Is.True,
                        $"the wall stopped being lit after switching to {layout}");
                }
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// While one of the four orders is held, the open palette says which mode it is in — in
        /// that order's own colour, with that order's own icon (owner, 2026-09-17).
        ///
        /// <para>The order is picked up from the strip in the right-hand gutter, which is where
        /// the four buttons live since they came off the palette's header. That the panel still
        /// wears the colour is the point of the test: the two are joined by the model rather than
        /// by being in the same box, and moving the buttons out is exactly the change that could
        /// have broken it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingAnOrderColoursTheOpenPalette()
        {
            GameObject root = Build(out OdysseyBootstrap boot, out UIDocument doc, Resolutions[1]);
            try
            {
                yield return Settle(doc);
                yield return OpenPalette(doc);

                VisualElement? palette = doc.rootVisualElement.Q(name: "build");
                Color resting = palette!.resolvedStyle.borderTopColor;

                VisualElement? cancel =
                    doc.rootVisualElement.Q(name: "action-" + PaletteTools.Cancel);
                Assert.That(cancel, Is.Not.Null, "the orders strip has no Cancel button");
                using (var click = ClickEvent.GetPooled())
                {
                    click.target = cancel;
                    cancel!.SendEvent(click);
                }
                yield return Settle(doc);

                Color expected = HudTokens.Convert(HudTheme.PinnedActionHue(PaletteTools.Cancel)!.Value);
                Assert.That(palette.resolvedStyle.borderTopColor, Is.Not.EqualTo(resting),
                    "holding Cancel left the panel looking exactly as it did while building");
                Assert.That(palette.resolvedStyle.borderTopColor.r, Is.EqualTo(expected.r).Within(0.02f),
                    "the panel's edge is not Cancel's colour");

                Label? crumb = palette.Q<Label>(className: "bp__crumb");
                Assert.That(crumb!.text, Is.EqualTo(Registry.Label(PaletteTools.Cancel)),
                    "the header still describes a build that is not about to happen");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        /// <summary>
        /// Every window carries an X, which is the owner's rule stated as a test rather than as a
        /// convention each call site has to remember.
        ///
        /// <para><b>Two windows are exempt, for two different reasons, and both reasons are
        /// asserted rather than allowed silently.</b></para>
        ///
        /// <para><c>start</c> (U38) has nothing behind it to close <i>to</i>: it exists precisely
        /// when no session is built, so an X on it could only either do nothing or quit the game
        /// while wearing the glyph that means "dismiss this". A control that does nothing is worse
        /// than no control, and one that quits under a dismiss glyph is worse still — so it has
        /// none, and the way out is the Quit row that says so in words and asks twice. The
        /// assertion is that it really has no X, so the exemption cannot quietly cover one.</para>
        ///
        /// <para><c>setup</c> (2026-09-18) became a window when the page took the in-game menus'
        /// own fill rather than inventing a colour, and it does have somewhere to go: the Back row
        /// in its footer. That is a <i>better</i> way out than an X for this screen — it says in
        /// words where it goes — and an X beside it would be two controls for one action. The
        /// assertion here is on the alternative: the exemption holds only while a
        /// <c>setup__back</c> row is actually on the page, so deleting Back fails this test rather
        /// than leaving a window nobody can leave.</para>
        ///
        /// <para>Every <i>other</i> window still has to carry one, which is what naming the two
        /// buys over loosening the rule: a third window without an X fails here.</para>
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

                // The window, and what it has instead of an X — null where there is nothing behind
                // it to go back to at all.
                var exempt = new (string Window, string? Instead)[]
                {
                    ("start", null),
                    ("setup", "setup__back"),
                };
                int exemptSeen = 0;

                foreach (VisualElement window in windows)
                {
                    Assert.That(window.ClassListContains("panel"), Is.True,
                        $"the '{window.name}' window is not a panel, so it does not carry the fill");

                    int listed = -1;
                    for (int i = 0; i < exempt.Length; i++)
                        if (exempt[i].Window == window.name) listed = i;

                    if (listed >= 0)
                    {
                        exemptSeen++;
                        Assert.That(window.Q(className: "panel__close"), Is.Null,
                            $"the '{window.name}' window is listed as carrying no X, yet it has " +
                            "one — one of the two is wrong");

                        // An exemption is only honest while the thing it was granted for is there.
                        string? instead = exempt[listed].Instead;
                        if (instead != null)
                            Assert.That(window.Q(className: instead), Is.Not.Null,
                                $"the '{window.name}' window is exempt from the X because it has a " +
                                $"'{instead}' row instead, and that row is not on the page — so it " +
                                "is a window with no way out at all");

                        continue;
                    }

                    Assert.That(window.Q(className: "panel__close"), Is.Not.Null,
                        $"the '{window.name}' window has no close button");
                }

                // Each exemption has to still apply to something, or it is a hole left open for a
                // window that quietly stopped being built.
                Assert.That(exemptSeen, Is.EqualTo(exempt.Length),
                    "a window named as exempt from the close-button rule was not on screen at all");
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
            // Explicitly, not by default: since U38 pressing Play lands on the start screen, and
            // what this rig is asserting is that a session exists.
            boot.buildOnPlay = true;
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
