#nullable enable
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Audio;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// <see cref="HudShell"/>: the draw's machine on the setup page (design 41 §5, §6; unit CD5).
    ///
    /// <para><b>Gamble is built into the page that exists, not beside it.</b> Everything the page
    /// already has stays where it is; Gamble adds a Creation switch, turns each skill's figure into a
    /// reel window, frames the detail pane with marquee bulbs, and swaps Keep and Reroll for one
    /// Pull / Stop button. In Standard every one of those is hidden and the page is what it was.</para>
    ///
    /// <para><b>The machine never decides anything.</b> <see cref="ColonistSelect.Pull"/> draws the
    /// colonist at the press; this works out which symbol each reel lands on and hands the
    /// <see cref="ReelMachine"/> the targets. What a player sees land is what the colony gets —
    /// <c>StartScreenTests</c> checks it end to end.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        readonly ReelMachine _machine = new ReelMachine();

        /// <summary>The slot the machine is revealing, or has just revealed; -1 when it is at rest.</summary>
        int _machineSlot = -1;

        VisualElement _modeSwitch = null!;
        VisualElement _modeStandard = null!;
        VisualElement _modeGamble = null!;
        Label _drawHint = null!;
        VisualElement _detailRecord = null!;
        VisualElement _drawIdentity = null!;
        ReelWindow _drawPortrait = null!;
        ReelWindow _drawName = null!;
        ReelWindow _drawPace = null!;
        Label _drawVerdict = null!;
        VisualElement _drawTraitRow = null!;
        readonly ReelWindow[] _drawTraits = new ReelWindow[HudLayout.TraitWindows];
        VisualElement _drawAction = null!;
        HudGlyph _drawActionIcon = null!;
        Label _drawActionLabel = null!;
        Label _drawActionCap = null!;
        readonly List<VisualElement> _drawBulbs = new List<VisualElement>();
        readonly List<BulbLight> _drawBulbLit = new List<BulbLight>();
        readonly List<ReelWindow?> _drawSkillReels = new List<ReelWindow?>();
        readonly List<HudGlyph?> _drawSkillFlames = new List<HudGlyph?>();
        readonly List<HudGlyph?> _drawSkillFlameOutlines = new List<HudGlyph?>();
        readonly List<VisualElement> _cardBlanks = new List<VisualElement>();
        readonly List<VisualElement> _cardLocks = new List<VisualElement>();

        // ---- the pull being shown: what each reel carries and where it lands ----------------------

        /// <summary>What one reel of the current pull is.</summary>
        enum ReelKind { Face, Name, Pace, Skill, Trait }

        sealed class Plan
        {
            public int Slot = -1;
            public uint Seed;
            public PawnId Id;
            public readonly List<ReelKind> Kind = new List<ReelKind>();
            /// <summary>For a skill reel, the detail grid's row; for a trait reel, which window.</summary>
            public readonly List<int> Index = new List<int>();
            public readonly List<string[]> Texts = new List<string[]>();
            public readonly List<int> Target = new List<int>();
            public readonly List<bool> Tease = new List<bool>();
            public readonly List<ReelWindow.Tone> Landed = new List<ReelWindow.Tone>();
            public uint[] FaceSeeds = System.Array.Empty<uint>();
            public Verdict Verdict;
            public readonly List<int> Passion = new List<int>();
        }

        Plan? _plan;

        /// <summary>The machine's sounds (CD6), or null where the menu has no voice for them.</summary>
        DrawSounds? _drawSound;

        /// <summary>A reel's figures, built once: "0" to "20".</summary>
        static readonly string[] LevelTexts = BuildNumbers(0, 20);

        /// <summary>The pace reel: 85 to 115 per cent.</summary>
        static readonly string[] PaceTexts = BuildNumbers(PaceLow, PaceHigh);

        const int PaceLow = 85, PaceHigh = 115;

        /// <summary>The trait reels: an empty "--" then every trait by name.</summary>
        static string[]? _traitTexts;

        const string Incapable = "--";

        /// <summary>How many names and faces spin past before the real one lands.</summary>
        const int IdentitySymbols = 20;

        static string[] BuildNumbers(int from, int to)
        {
            var texts = new string[to - from + 1];
            for (int i = 0; i < texts.Length; i++) texts[i] = (from + i).ToString("0");
            return texts;
        }

        static string[] TraitTexts()
        {
            if (_traitTexts != null) return _traitTexts;
            var texts = new string[TraitHandle.Count + 1];
            texts[0] = Incapable;
            for (int t = 0; t < TraitHandle.Count; t++) texts[t + 1] = Registry.Label(TraitHandle.Keys[t]);
            return _traitTexts = texts;
        }

        // ---- building ------------------------------------------------------------------------------

        /// <summary>The Creation switch, beside Board size (design 41 §5.1): two segments.</summary>
        VisualElement BuildModeSwitch()
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("setup__field");
            wrap.AddToClassList("setup__mode");
            wrap.Add(HudText.Make(Registry.Label("ui.newgame.mode"), HudTextRole.PanelLabel,
                ussClass: "startscreen__seedcap"));

            _modeSwitch = new VisualElement();
            _modeSwitch.AddToClassList("draw__switch");
            _modeStandard = ModeSegment(ColonistSelect.StandardKey, CreationMode.Standard);
            _modeGamble = ModeSegment(ColonistSelect.GambleKey, CreationMode.Gamble);
            _modeSwitch.Add(_modeStandard);
            _modeSwitch.Add(_modeGamble);
            wrap.Add(_modeSwitch);
            return wrap;
        }

        VisualElement ModeSegment(string key, CreationMode mode)
        {
            var segment = new VisualElement();
            segment.AddToClassList("draw__segment");
            segment.Add(HudText.Make(Registry.Label(key), HudTextRole.Row, ussClass: "draw__segment-label"));
            segment.tooltip = Registry.Describe(key).Length > 0 ? Registry.Describe(key) : Registry.Label(key);
            segment.RegisterCallback<ClickEvent>(_ =>
            {
                ColonistSelect? select = _menu.Colonists;
                if (select == null || _machine.Phase == MachinePhase.Spinning || _machine.Phase == MachinePhase.Stopping) return;
                EndRename();
                select.SetMode(mode, SeedEntry.Draw);
            });
            return segment;
        }

        /// <summary>
        /// Everything Gamble adds to the colonist screen, built once and hidden in Standard. Called
        /// after <see cref="BuildColonistScreen"/> has made the cards and the detail pane.
        /// </summary>
        void BuildMachine(VisualElement record)
        {
            _detailRecord = record;

            // The cards: a blank face frame and a LOCKED tag each, hidden until Gamble needs them.
            // Appended, never inserted: RefreshColonists and the rename box find a card's lines as
            // its second child, and a frame put in front would shift them.
            for (int slot = 0; slot < ColonistSelect.Slots; slot++)
            {
                VisualElement card = _colonistCards[slot];
                var blank = new VisualElement { pickingMode = PickingMode.Ignore };
                blank.AddToClassList("colonist__blank");
                blank.style.display = DisplayStyle.None;
                card.Add(blank);
                _cardBlanks.Add(blank);

                var tag = new VisualElement { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("draw__lock");
                tag.Add(new HudGlyph(HudGlyphKind.Lock, 11f, HudTokens.Accent));
                tag.Add(HudText.Make(Registry.Label("ui.draw.locked"), HudTextRole.PanelLabel, ussClass: "draw__lock-label"));
                tag.style.display = DisplayStyle.None;
                card.Add(tag);
                _cardLocks.Add(tag);
            }

            _drawHint = HudText.Make(Registry.Label("ui.draw.hint"), HudTextRole.Body, ussClass: "draw__hint");
            _drawHint.style.display = DisplayStyle.None;
            _colonistCards.Add(_drawHint);

            // The marquee: two rails of bulbs along the frame's top and bottom.
            for (int rail = 0; rail < 2; rail++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("draw__rail");
                line.AddToClassList(rail == 0 ? "draw__rail--top" : "draw__rail--bottom");
                for (int b = 0; b < HudLayout.BulbsPerRail; b++)
                {
                    var bulb = new VisualElement { pickingMode = PickingMode.Ignore };
                    bulb.AddToClassList("draw__bulb");
                    line.Add(bulb);
                    _drawBulbs.Add(bulb);
                    _drawBulbLit.Add((BulbLight)(-1));
                }

                line.style.display = DisplayStyle.None;
                _colonistDetail.Add(line);
            }

            // The identity row: the portrait window, the name window, the pace, and the verdict.
            _drawIdentity = new VisualElement();
            _drawIdentity.AddToClassList("draw__identity");
            _drawPortrait = new ReelWindow(HudLayout.PortraitWindow, HudLayout.PortraitWindow, HudLayout.PortraitWindow,
                HudTextRole.Name, numeric: false, faces: true, modifier: "draw__window--portrait");
            _drawName = new ReelWindow(HudLayout.NameWindowWidth, HudLayout.IdentityHeight, HudLayout.NamePitch,
                HudTextRole.Name, numeric: false, modifier: "draw__window--name");
            var pace = new VisualElement();
            pace.AddToClassList("draw__pace");
            pace.Add(HudText.Make(Registry.Label("ui.draw.reel.pace"), HudTextRole.PanelLabel, ussClass: "draw__pace-label"));
            _drawPace = new ReelWindow(HudLayout.ReelWindowWidth, HudLayout.ReelWindowHeight, HudLayout.ReelPitch,
                HudTextRole.Row, numeric: true);
            pace.Add(_drawPace);
            pace.tooltip = Registry.Describe("ui.draw.reel.pace").Length > 0
                ? Registry.Describe("ui.draw.reel.pace") : Registry.Label("ui.draw.reel.pace");
            var spacer = new VisualElement();
            spacer.AddToClassList("draw__spacer");
            _drawVerdict = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "draw__verdict");
            _drawIdentity.Add(_drawPortrait);
            _drawIdentity.Add(_drawName);
            _drawIdentity.Add(pace);
            _drawIdentity.Add(spacer);
            _drawIdentity.Add(_drawVerdict);
            _drawIdentity.style.display = DisplayStyle.None;
            _colonistDetail.Insert(_colonistDetail.IndexOf(_detailRecord) + 1, _drawIdentity);

            // A window and a flame on every skill line, beside the figure they replace.
            IReadOnlyList<SkillCatalogue.Entry> order = SkillCatalogue.ReadingOrder;
            for (int i = 0; i < _detailSkillViews.Count; i++)
            {
                SkillLineView view = _detailSkillViews[i];
                var reel = new ReelWindow(HudLayout.ReelWindowWidth, HudLayout.ReelWindowHeight, HudLayout.ReelPitch,
                    HudTextRole.Name, numeric: true, modifier: "draw__window--skill");
                reel.style.display = DisplayStyle.None;
                view.Root.Insert(view.Root.IndexOf(view.Value), reel);
                _drawSkillReels.Add(reel);

                var flames = new VisualElement { pickingMode = PickingMode.Ignore };
                flames.AddToClassList("draw__flame");
                var major = new HudGlyph(HudGlyphKind.Flame, HudLayout.FlameHeight, HudTokens.Warn);
                var minor = new HudGlyph(HudGlyphKind.FlameOutline, HudLayout.FlameHeight, HudTokens.Warn, 0.8f);
                major.style.display = DisplayStyle.None;
                minor.style.display = DisplayStyle.None;
                flames.Add(major);
                flames.Add(minor);
                flames.style.display = DisplayStyle.None;
                view.Root.Insert(view.Root.IndexOf(view.Passion), flames);
                _drawSkillFlames.Add(major);
                _drawSkillFlameOutlines.Add(minor);
            }

            // The trait row: three windows, then the one button.
            _drawTraitRow = new VisualElement();
            _drawTraitRow.AddToClassList("draw__traits");
            for (int t = 0; t < HudLayout.TraitWindows; t++)
            {
                _drawTraits[t] = new ReelWindow(HudLayout.TraitWindowWidth, HudLayout.TraitWindowHeight, HudLayout.TraitPitch,
                    HudTextRole.Row, numeric: false, modifier: "draw__window--trait");
                _drawTraitRow.Add(_drawTraits[t]);
            }

            var gap = new VisualElement();
            gap.AddToClassList("draw__spacer");
            _drawTraitRow.Add(gap);

            _drawAction = new VisualElement();
            _drawAction.AddToClassList("draw__action");
            _drawActionIcon = new HudGlyph(HudGlyphKind.Lever, 18f, HudTokens.OnAccent);
            _drawActionLabel = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "draw__action-label");
            _drawActionCap = HudText.Make(Registry.Label("ui.draw.key"), HudTextRole.Hotkey, numeric: true,
                ussClass: "draw__action-cap");
            _drawAction.Add(_drawActionIcon);
            _drawAction.Add(_drawActionLabel);
            _drawAction.Add(_drawActionCap);
            _drawAction.RegisterCallback<ClickEvent>(_ => DrawAction());
            _drawTraitRow.Add(_drawAction);
            _drawTraitRow.style.display = DisplayStyle.None;
            _colonistDetail.Add(_drawTraitRow);

            _machine.ReelLanded += OnReelLanded;
            _machine.Finished += OnMachineFinished;
            _drawSound = new DrawSounds(_boot!.audioCatalogue, _boot.transform, _boot.gameObject.layer);
        }

        // ---- the button and the key ----------------------------------------------------------------

        /// <summary>
        /// Pull, Stop, or Next colonist, by where the machine is (design 41 §5.3). The button and
        /// Space both come here, so the two can never mean different things.
        /// </summary>
        void DrawAction()
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null || select.Mode != CreationMode.Gamble) return;

            switch (_machine.Phase)
            {
                case MachinePhase.Spinning:
                    if (_plan != null) _machine.Stop(_plan.Target, _plan.Tease, _plan.Verdict);
                    PaintMachine();
                    return;

                case MachinePhase.Stopping:
                    return;

                case MachinePhase.Landed:
                    NextColonist(select);
                    return;
            }

            int slot = select.Selected;
            if (select.StateOf(slot) == SlotState.Landed)
            {
                NextColonist(select);
                return;
            }

            if (select.StateOf(slot) != SlotState.Unpulled) return;
            EndRename();
            if (!select.Pull(slot, SeedEntry.Draw)) return;

            _plan = PlanFor(select.Cards[slot], slot);
            _machineSlot = slot;
            _machine.Start(Counts(_plan), select.Cards[slot].Seed ^ 0x5EEDu);
            _drawSound?.Spinning(true);
            RefreshDraw();
            PaintMachine();
        }

        /// <summary>
        /// Press the machine's button, exactly as a click or Space does: Pull, Stop or Next
        /// colonist. Public so a PlayMode test can drive the control a player drives — a batch run
        /// cannot press a button (CLAUDE.md, "nothing tests that a click reaches the game").
        /// </summary>
        public void PressDrawAction() => DrawAction();

        /// <summary>Where the machine stands, for a test waiting on it.</summary>
        public MachinePhase DrawPhase => _machine.Phase;

        void NextColonist(ColonistSelect select)
        {
            int next = select.NextUnpulled;
            if (next < 0) return;
            _drawSound?.Spinning(false);
            _machine.Reset();
            _machineSlot = -1;
            _plan = null;
            select.Select(next);
        }

        static List<int> Counts(Plan plan)
        {
            var counts = new List<int>(plan.Texts.Count);
            for (int i = 0; i < plan.Texts.Count; i++) counts.Add(plan.Texts[i].Length);
            return counts;
        }

        void OnReelLanded(int reel)
        {
            // CD6's clunk and ding go here. The machine runs silent until the clips exist.
            _drawSound?.Landed(_plan != null && reel < _plan.Tease.Count && _plan.Tease[reel]);
        }

        void OnMachineFinished(Verdict verdict)
        {
            _drawSound?.Finished(verdict);
            if (_machineSlot >= 0) _menu.Colonists?.Land(_machineSlot);
        }

        /// <summary>Space pulls and stops while the page shows a gamble — never while a name is being typed.</summary>
        void PollDrawKey()
        {
            Keyboard? keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame) return;
            if (Hotkeys().Typing) return;
            DrawAction();
        }

        // ---- the plan: what each reel carries --------------------------------------------------------

        /// <summary>
        /// Work out every reel of a pull from the colonist it reveals: the symbols it spins through,
        /// the one it lands on, whether it teases, and the tone it lands in. Reading order is the
        /// cascade's order: the portrait, the name, the pace, the left column of skills top to
        /// bottom, the right column, then the traits.
        /// </summary>
        Plan PlanFor(in Candidate who, int slot)
        {
            var plan = new Plan { Slot = slot, Seed = who.Seed, Id = ColonistDraw.IdForSlot(slot) };
            var rng = new System.Random(unchecked((int)who.Seed));

            // The portrait: the real face is symbol 0, nineteen strangers around it.
            plan.FaceSeeds = new uint[IdentitySymbols];
            plan.FaceSeeds[0] = who.Seed;
            var names = new string[IdentitySymbols];
            names[0] = who.Name;
            for (int k = 1; k < IdentitySymbols; k++)
            {
                plan.FaceSeeds[k] = unchecked((uint)rng.Next() ^ 0xA5A5u);
                names[k] = ColonistNames.Rolled(plan.FaceSeeds[k], plan.Id);
            }

            Add(plan, ReelKind.Face, 0, names, 0, false, ReelWindow.Tone.Face);
            Add(plan, ReelKind.Name, 0, names, 0, false, ReelWindow.Tone.Face);

            int pace = Mathf.Clamp(Mathf.RoundToInt(who.PacePerMille / 10f), PaceLow, PaceHigh);
            Add(plan, ReelKind.Pace, 0, PaceTexts, pace - PaceLow, false, ReelWindow.Tone.Face);

            // The verdict needs the budget skills and the traits' worths; the worth is content, so
            // it is read here where the simulation is visible, not carried on the card.
            var levels = new List<int>();
            var passions = new List<int>();
            var worths = new List<int>();
            PawnContent content = ContentPack.Pawns();
            for (int t = 0; t < who.Traits.Count; t++)
            {
                int handle = who.Traits[t];
                worths.Add((uint)handle < (uint)content.Traits.Length ? content.Traits[handle].worth : 0);
            }

            for (int i = 0; i < who.Skills.Count; i++)
                if (who.Skills[i].Live) { levels.Add(who.Skills[i].Level); passions.Add(who.Skills[i].Passion); }
            plan.Verdict = DrawVerdict.Of(levels, passions, worths);

            // Skills in the grid's own reading order: the grid is a wrapping row two wide, so the
            // left column is the even rows and the right the odd.
            for (int column = 0; column < HudLayout.SetupSkillColumns; column++)
            for (int i = column; i < who.Skills.Count && i < _detailSkillViews.Count; i += HudLayout.SetupSkillColumns)
            {
                SkillRow row = who.Skills[i];
                if (!row.Live) continue;
                int level = Mathf.Clamp(row.Level, 0, LevelTexts.Length - 1);
                bool hot = DrawVerdict.IsHotSkill(level);
                ReelWindow.Tone tone = hot ? ReelWindow.Tone.Hot
                    : plan.Verdict == Verdict.Dud && level == 0 ? ReelWindow.Tone.Cold
                    : ReelWindow.Tone.Face;
                Add(plan, ReelKind.Skill, i, LevelTexts, level, hot, tone);
                plan.Passion.Add(row.Passion);
            }

            string[] traits = TraitTexts();
            for (int t = 0; t < HudLayout.TraitWindows; t++)
            {
                int handle = t < who.Traits.Count ? who.Traits[t] : TraitHandle.None;
                int worth = t < worths.Count ? worths[t] : 0;
                bool star = DrawVerdict.IsStar(worth), flaw = DrawVerdict.IsFlaw(worth);
                Add(plan, ReelKind.Trait, t, traits, handle + 1, star || flaw,
                    star ? ReelWindow.Tone.Hot : flaw ? ReelWindow.Tone.Cold : ReelWindow.Tone.Face);
            }

            return plan;
        }

        static void Add(Plan plan, ReelKind kind, int index, string[] texts, int target, bool tease, ReelWindow.Tone tone)
        {
            plan.Kind.Add(kind);
            plan.Index.Add(index);
            plan.Texts.Add(texts);
            plan.Target.Add(target);
            plan.Tease.Add(tease);
            plan.Landed.Add(tone);
        }

        // ---- painting ------------------------------------------------------------------------------

        /// <summary>
        /// Step the machine and paint it. Called every frame from <c>Update</c> while the setup page
        /// shows a gamble — the menu's one per-frame driver, since the HUD's own refresh returns
        /// when there is no world.
        /// </summary>
        void StepDraw(float seconds)
        {
            if (_setupPage == null || _setupPage.style.display == DisplayStyle.None) return;
            ColonistSelect? select = _menu.Colonists;
            if (select == null || select.Mode != CreationMode.Gamble) return;

            PollDrawKey();
            if (_machine.Phase == MachinePhase.Idle) return;
            _machine.Step(seconds);
            PaintMachine();
        }

        /// <summary>Paint every reel, the bulbs, the verdict, the button and the frame from the machine.</summary>
        void PaintMachine()
        {
            Plan? plan = _plan;
            if (plan == null || _machine.Count != plan.Kind.Count) return;

            for (int r = 0; r < plan.Kind.Count; r++)
            {
                ReelWindow window = WindowFor(plan, r);
                ReelPhase phase = _machine.ReelPhaseOf(r);
                string[] texts = plan.Texts[r];
                if (phase == ReelPhase.Landed)
                {
                    float settle = (float)_machine.FractionOf(r);
                    PaintLanded(plan, r, window, settle, (float)_machine.HotAmount(r));
                    continue;
                }

                bool teasing = phase == ReelPhase.Teasing;
                float fraction = (float)_machine.FractionOf(r);
                if (plan.Kind[r] == ReelKind.Face)
                {
                    window.SetSpinningFaces(ColonistFace.Of(plan.FaceSeeds[_machine.SymbolAt(r, 1)], plan.Id),
                        ColonistFace.Of(plan.FaceSeeds[_machine.SymbolAt(r, 0)], plan.Id),
                        ColonistFace.Of(plan.FaceSeeds[_machine.SymbolAt(r, -1)], plan.Id), fraction, teasing);
                }
                else
                {
                    window.SetSpinning(texts[_machine.SymbolAt(r, 1)], texts[_machine.SymbolAt(r, 0)],
                        texts[_machine.SymbolAt(r, -1)], fraction, teasing);
                }

                window.SetTone(teasing ? ReelWindow.Tone.Tease : ReelWindow.Tone.Face, 0f);
                window.Bars(teasing, (float)_machine.TeaseProgress(r));
                if (plan.Kind[r] == ReelKind.Skill) ShowFlame(plan.Index[r], 0);
            }

            PaintBulbs();
            PaintVerdict(_machine.Phase, _machine.Verdict);
            PaintAction();
            PaintMachineCard(plan.Slot);
        }

        void PaintLanded(Plan plan, int r, ReelWindow window, float settle, float amount)
        {
            if (plan.Kind[r] == ReelKind.Face)
            {
                PawnId id = plan.Id;
                window.SetLandedFace(ColonistFace.Of(plan.Seed, id), _boot!.Portraits.For(plan.Seed, id), settle);
            }
            else
            {
                window.SetLanded(plan.Texts[r][plan.Target[r]], settle);
            }

            ReelWindow.Tone tone = plan.Landed[r];
            window.SetTone(tone, tone == ReelWindow.Tone.Face ? 0f : amount);
            if (plan.Kind[r] == ReelKind.Skill)
            {
                int skillReel = 0;
                for (int i = 0; i < r; i++) if (plan.Kind[i] == ReelKind.Skill) skillReel++;
                ShowFlame(plan.Index[r], plan.Passion[skillReel]);
            }
        }

        ReelWindow WindowFor(Plan plan, int reel) => plan.Kind[reel] switch
        {
            ReelKind.Face => _drawPortrait,
            ReelKind.Name => _drawName,
            ReelKind.Pace => _drawPace,
            ReelKind.Skill => _drawSkillReels[plan.Index[reel]]!,
            _ => _drawTraits[plan.Index[reel]],
        };

        void ShowFlame(int row, int passion)
        {
            if (row < 0 || row >= _drawSkillFlames.Count) return;
            _drawSkillFlames[row]!.style.display = passion >= 2 ? DisplayStyle.Flex : DisplayStyle.None;
            _drawSkillFlameOutlines[row]!.style.display = passion == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void PaintBulbs(Verdict? settled = null)
        {
            int perRail = HudLayout.BulbsPerRail;
            for (int i = 0; i < _drawBulbs.Count; i++)
            {
                int along = i % perRail;
                BulbLight light = settled switch
                {
                    null => _machine.Bulb(along, perRail),
                    Verdict.Jackpot => BulbLight.Gold,
                    Verdict.Dud => along < ReelMachine.DudKeptDim ? BulbLight.Dim : BulbLight.Off,
                    Verdict.Plain => BulbLight.Lit,
                    _ => BulbLight.Dim,
                };
                if (_drawBulbLit[i] == light) continue;
                _drawBulbLit[i] = light;
                _drawBulbs[i].style.backgroundColor = light switch
                {
                    BulbLight.Gold => (StyleColor)HudTokens.MachineStar,
                    BulbLight.Lit => (StyleColor)HudTokens.Warn,
                    BulbLight.Dim => (StyleColor)new Color(HudTokens.Warn.r, HudTokens.Warn.g, HudTokens.Warn.b, 0.28f),
                    _ => (StyleColor)new Color(1f, 1f, 1f, 0.08f),
                };
            }
        }

        void PaintVerdict(MachinePhase phase, Verdict verdict)
        {
            string key = phase == MachinePhase.Spinning || phase == MachinePhase.Stopping ? "ui.draw.spinning"
                : verdict == Verdict.Jackpot ? "ui.draw.jackpot"
                : verdict == Verdict.Dud ? "ui.draw.dud"
                : string.Empty;
            string text = key.Length == 0 ? string.Empty : Registry.Label(key);
            if (!ReferenceEquals(_drawVerdict.userData, text))
            {
                _drawVerdict.userData = text;
                HudText.Set(_drawVerdict, text, HudTextRole.Row);
            }

            _drawVerdict.style.display = text.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _drawVerdict.EnableInClassList("draw__verdict--spinning", key == "ui.draw.spinning");
            _drawVerdict.EnableInClassList("draw__verdict--jackpot", key == "ui.draw.jackpot");
            _drawVerdict.EnableInClassList("draw__verdict--dud", key == "ui.draw.dud");
            _colonistDetail.EnableInClassList("setup__detail--jackpot", key == "ui.draw.jackpot");
        }

        /// <summary>The button: Pull, Stop, Stopping n / N, Next colonist, All pulled.</summary>
        void PaintAction()
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null) return;

            string key;
            HudGlyphKind icon;
            string mode;
            string cap = Registry.Label("ui.draw.key");
            switch (_machine.Phase)
            {
                case MachinePhase.Spinning:
                    key = "ui.draw.stop"; icon = HudGlyphKind.Stop; mode = "stop";
                    break;
                case MachinePhase.Stopping:
                    key = "ui.draw.stopping"; icon = HudGlyphKind.Stop; mode = "stopping";
                    cap = CascadeCounter();
                    break;
                default:
                    SlotState state = select.StateOf(select.Selected);
                    if (state == SlotState.Unpulled)
                    {
                        key = "ui.draw.pull"; icon = HudGlyphKind.Lever; mode = "pull";
                    }
                    else if (select.NextUnpulled >= 0)
                    {
                        key = "ui.draw.next"; icon = HudGlyphKind.ArrowRight; mode = "next";
                    }
                    else
                    {
                        key = "ui.draw.allpulled"; icon = HudGlyphKind.Check; mode = "done";
                    }
                    break;
            }

            string label = Registry.Label(key);
            if (!ReferenceEquals(_drawActionLabel.userData, label))
            {
                _drawActionLabel.userData = label;
                HudText.Set(_drawActionLabel, label, HudTextRole.Name);
            }

            if (!ReferenceEquals(_drawActionCap.userData, cap))
            {
                _drawActionCap.userData = cap;
                HudText.Set(_drawActionCap, cap, HudTextRole.Hotkey);
            }

            _drawActionIcon.Kind = icon;
            _drawActionIcon.Tint = mode == "pull" || mode == "stop" ? HudTokens.OnAccent
                : mode == "stopping" ? HudTokens.Warn
                : mode == "next" ? HudTokens.Accent
                : HudTokens.TextDim;
            foreach (string m in ActionModes) _drawAction.EnableInClassList("draw__action--" + m, m == mode);
        }

        static readonly string[] ActionModes = { "pull", "stop", "stopping", "next", "done" };

        /// <summary>"4 / 11": how far the cascade has got. Built from a cache, so the frame allocates nothing.</summary>
        string CascadeCounter()
        {
            int landed = _machine.LandedCount, total = _machine.Count;
            int key = landed * 100 + total;
            if (!_counterCache.TryGetValue(key, out string? text))
            {
                text = landed.ToString("0") + " / " + total.ToString("0");
                _counterCache[key] = text;
            }

            return text;
        }

        readonly Dictionary<int, string> _counterCache = new Dictionary<int, string>();

        /// <summary>The card being revealed: "Colonist 2" over "Spinning" or "Stopping".</summary>
        void PaintMachineCard(int slot)
        {
            if (slot < 0 || slot >= _colonistCards.childCount) return;
            ColonistSelect? select = _menu.Colonists;
            if (select == null || select.StateOf(slot) != SlotState.Spinning) return;
            VisualElement lines = CardLines(slot);
            string status = Registry.Label(_machine.Phase == MachinePhase.Stopping ? "ui.draw.stopping" : "ui.draw.spinning");
            if (!ReferenceEquals(lines[1].userData, status))
            {
                lines[1].userData = status;
                HudText.Set((Label)lines[1], status, HudTextRole.Row);
            }
        }

        /// <summary>The card's line stack: its face (or blank) comes first, so find it by class.</summary>
        VisualElement CardLines(int slot) => _colonistCards[slot].Q(className: "colonist__lines")!;

        // ---- the static half: what the page shows when nothing is spinning -------------------------

        /// <summary>
        /// Everything about the page that depends on the mode and the slots rather than on a frame:
        /// which of Standard's and Gamble's parts are shown, the cards' states, and — when the
        /// machine is not revealing the selected slot — its windows at rest or landed. Called from
        /// <see cref="RefreshColonists"/>, which runs on every change the select screen raises.
        /// </summary>
        void RefreshDraw()
        {
            ColonistSelect? select = _menu.Colonists;
            if (select == null || _drawIdentity == null) return;
            bool gamble = select.Mode == CreationMode.Gamble;

            _modeStandard.EnableInClassList("draw__segment--on", !gamble);
            _modeGamble.EnableInClassList("draw__segment--on", gamble);
            _modeSwitch.EnableInClassList("draw__switch--locked", !select.CanChangeMode);
            _modeSwitch.tooltip = select.CanChangeMode ? string.Empty
                : Registry.Label("ui.draw.modelocked") + ". " + Registry.Describe("ui.draw.modelocked");

            Show(_colonistKeep, !gamble);
            Show(_colonistReroll, !gamble);
            Show(_drawHint, gamble);
            Show(_detailRecord, !gamble);
            Show(_drawIdentity, gamble);
            Show(_drawTraitRow, gamble);
            Show(_detailTraits, !gamble);
            _colonistDetail.EnableInClassList("setup__detail--machine", gamble);
            foreach (VisualElement rail in _colonistDetail.Query(className: "draw__rail").ToList()) Show(rail, gamble);

            for (int i = 0; i < _detailSkillViews.Count; i++)
            {
                SkillLineView view = _detailSkillViews[i];
                view.Root.EnableInClassList("skill--reel", gamble);
                Show(view.Value, !gamble);
                Show(view.Passion, !gamble);
                Show(_drawSkillReels[i]!, gamble);
                Show(_drawSkillFlames[i]!.parent, gamble);
            }

            _startCommit.EnableInClassList("settings__row--off", !_menu.Seed.Usable || !select.CanStart);
            _startCommit.EnableInClassList("setup__commit--waiting", !select.CanStart);

            for (int slot = 0; slot < ColonistSelect.Slots && slot < _colonistCards.childCount; slot++)
                RefreshDrawCard(select, slot, gamble);

            if (!gamble)
            {
                _drawSound?.Spinning(false);
                _machine.Reset();
                _machineSlot = -1;
                _plan = null;
                return;
            }

            // A slot the machine is revealing is painted by the machine, frame by frame.
            if (_machineSlot == select.Selected && _machine.Phase != MachinePhase.Idle)
            {
                PaintMachine();
                return;
            }

            // Selecting another card while one has landed puts the machine back at rest.
            if (_machine.Phase == MachinePhase.Landed && _machineSlot != select.Selected)
            {
                _machine.Reset();
                _machineSlot = -1;
            }

            SlotState state = select.StateOf(select.Selected);
            if (state == SlotState.Landed)
                PaintLandedStatically(select.Cards[select.Selected], select.Selected, select.DisplayName(select.Selected));
            else PaintResting();
            PaintAction();
        }

        void RefreshDrawCard(ColonistSelect select, int slot, bool gamble)
        {
            VisualElement card = _colonistCards[slot];
            SlotState state = select.StateOf(slot);
            bool faceDown = gamble && (state == SlotState.Unpulled || state == SlotState.Spinning);
            bool active = gamble && select.Selected == slot && state != SlotState.Landed;

            Show(_cardBlanks[slot], faceDown);
            // Hidden but still laid out, so the lines keep their place beside where the face goes
            // and the blank frame sits exactly over it.
            _colonistFaces[slot].style.visibility = faceDown ? Visibility.Hidden : Visibility.Visible;
            Show(_cardLocks[slot], gamble && state == SlotState.Landed);
            card.EnableInClassList("colonist--unpulled", gamble && state == SlotState.Unpulled && !active);
            card.EnableInClassList("colonist--active", active);
            if (gamble) card.EnableInClassList("row--armed", false);

            if (!faceDown) return;
            VisualElement lines = CardLines(slot);
            string title = Registry.Label("ui.draw.colonist").Replace("{n}", (slot + 1).ToString("0"));
            HudText.Set((Label)lines[0], active || state == SlotState.Spinning ? title : Registry.Label("ui.draw.notpulled"),
                HudTextRole.Name);
            string status = state == SlotState.Spinning
                ? Registry.Label(_machine.Phase == MachinePhase.Stopping ? "ui.draw.stopping" : "ui.draw.spinning")
                : active ? Registry.Label("ui.draw.ready") : string.Empty;
            lines[1].userData = status;
            HudText.Set((Label)lines[1], status, HudTextRole.Row);
            card.tooltip = string.Empty;
        }

        /// <summary>Before a pull: every window at rest, the bulbs dim, no verdict.</summary>
        void PaintResting()
        {
            _drawPortrait.SetResting(string.Empty);
            _drawName.SetResting(Registry.Label("ui.draw.notpulled"));
            _drawPace.SetResting(Incapable);
            for (int i = 0; i < _drawSkillReels.Count; i++)
            {
                _drawSkillReels[i]!.SetResting(Incapable);
                ShowFlame(i, 0);
            }

            foreach (ReelWindow trait in _drawTraits) trait.SetResting(Incapable);
            PaintBulbs(Verdict.None);
            PaintVerdict(MachinePhase.Idle, Verdict.None);
        }

        /// <summary>A card pulled earlier and shown again: every window landed, no animation.</summary>
        void PaintLandedStatically(in Candidate who, int slot, string called)
        {
            Plan plan = PlanFor(who, slot);
            for (int r = 0; r < plan.Kind.Count; r++) PaintLanded(plan, r, WindowFor(plan, r), 0f, 1f);
            // A name typed over a landed card is what the page calls them everywhere.
            _drawName.SetLanded(called, 0f);
            // The rows nothing simulates show a dash in Gamble whatever is pulled.
            for (int i = 0; i < _drawSkillReels.Count && i < who.Skills.Count; i++)
                if (!who.Skills[i].Live) _drawSkillReels[i]!.SetResting(Incapable);
            PaintBulbs(plan.Verdict);
            PaintVerdict(MachinePhase.Landed, plan.Verdict);
        }

        static void Show(VisualElement element, bool on) =>
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
