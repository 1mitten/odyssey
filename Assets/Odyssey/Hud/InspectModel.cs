#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    public enum InspectSubject
    {
        None,
        Colonist,
        Item,
        Cell,

        /// <summary>
        /// A corpse (design 33 §1: "clickable as Corpse of X"), by <see cref="InspectModel.Corpse"/>.
        /// Appended with the combat contracts step; lane C fills its pane.
        /// </summary>
        Corpse,
    }

    /// <summary>
    /// One pane tab (B1): its name, whether it is live, and the reason shown when it is not.
    /// Tabs whose systems do not exist yet are present and disabled, so the shape of the game is
    /// visible from the first version — the panel catalogue's guarantee, made concrete.
    /// </summary>
    /// <summary>
    /// One kind of thing on a built store: what it is, how much, and in how many of its bays.
    ///
    /// <para>A struct in a pooled list rather than a string built per frame, for the pane's usual
    /// reason — this is rebuilt fifteen times a second while a store is selected.</para>
    /// </summary>
    public struct StoreContentRow
    {
        /// <summary>The registry key, for the icon beside the name.</summary>
        public string IconKey;

        /// <summary>The commodity's name, from the registry and never written here.</summary>
        public string Name;

        /// <summary>Units of it, summed across however many bays hold it.</summary>
        public int Units;

        /// <summary>How many of the store's bays it occupies.</summary>
        public int Stacks;
    }

    public struct InspectTab
    {
        public string Name;
        public bool Enabled;
        public string Reason;
    }

    /// <summary>
    /// One command-grid slot (A10). Every command a colonist will eventually offer is visible
    /// now, greyed, with the honest reason it is not available — never hidden, per the
    /// catalogue's "disabled with a reason" rule.
    /// </summary>
    public struct InspectCommand
    {
        public string IconKey;
        public string Label;
        public bool Enabled;
        public string Reason;
    }

    /// <summary>
    /// One line of the Skills tab: what it is called, what the colonist's standing in it is, and
    /// — where there is no simulation behind it yet — why there is no number.
    ///
    /// <para>The level is not a percentage and is not stored as one: it is the simulation's own
    /// 0 to 20, derived there from the experience ladder and published as a byte. Passion is the
    /// simulation's own three values, and is the field that decides who you put on a job, so it
    /// is on the row rather than in a tooltip (owner, 2026-09-17).</para>
    /// </summary>
    public struct SkillRow
    {
        public string IconKey;
        public string Name;

        /// <summary>False when nothing in the simulation trains it; <see cref="Reason"/> says why.</summary>
        public bool Live;

        public int Level;

        /// <summary>0 none, 1 minor, 2 major.</summary>
        public int Passion;

        /// <summary>Experience in thousandths of a point. The absolute total, which nothing draws
        /// today: the bar is drawn from <see cref="Progress"/>, because the ladder that turns one
        /// into the other is simulation content.</summary>
        public int Experience;

        /// <summary>
        /// How far this skill stands towards its next level, per mille — the bar (SK2).
        ///
        /// <para><b>Derived by the simulation, not here.</b> The denominator is
        /// <c>SkillDef.experienceToAdvance</c>, tuning content that a mod is meant to be able to
        /// override; a copy of it in this assembly would be a second source of truth for it. So
        /// this arrives already divided, the way <see cref="Level"/> does.</para>
        ///
        /// <para>Full at the top level, where there is no next one to be part of the way
        /// towards.</para>
        /// </summary>
        public int Progress;

        public string Reason;
        public string Note;
    }

    /// <summary>
    /// One line of the tile readout: what the fact is called, and what it reads. See
    /// <see cref="InspectModel.CellRows"/> for why the facts are rows rather than a sentence.
    /// </summary>
    public struct InspectRow
    {
        public string Name;
        public string Value;

        /// <summary>
        /// The colour the value is drawn in, or null to leave the row's own colour alone.
        ///
        /// <para>Set only where the value carries a judgement the colour is part of — a quality
        /// tier — so that <c>HudTheme.Quality</c> is the one place a tier's colour is decided and
        /// every surface that names one agrees. Null is the ordinary case and means the shell
        /// touches nothing.</para>
        /// </summary>
        public HudColour? Tint;

        /// <summary>
        /// What the row says when hovered, or null for none. The registry's description for a
        /// thought or a trait (design 51), so correcting the wiki corrects the tooltip.
        /// </summary>
        public string? Tooltip;
    }

    /// <summary>
    /// Assembles the inspect pane (A9) for the current selection: which subject, which header,
    /// which tabs, which commands. Pure function of the selection handle and the published
    /// frame; holds no reference to a simulation object, which is the whole contract.
    ///
    /// The tombstone case is specified behaviour, not an accident: a colonist who is gone from
    /// the frame keeps the pane open with last-known values greyed and every command disabled,
    /// rather than closing under the cursor (design 09 §2.3). The slice has no death yet, but
    /// the pane behaves correctly the day it arrives.
    /// </summary>
    public sealed class InspectModel
    {
        public InspectSubject Subject;
        public PawnId Pawn;
        public ThingId Thing;

        /// <summary>The corpse on the pane, as a <see cref="CorpseView.Id"/>, or 0 (design 33 §5f).</summary>
        public int Corpse;

        // ---- what the pane's shell builds (design 33 §5f) ------------------------------------
        //
        // The shell (HudShell.Inspect, Presentation) used to decide its shape from the subject
        // and IsAnimal, which left every new kind of pawn — the bandit, the corpse — falling to
        // the colonist's defaults in a file the interface lane does not own. It reads these four
        // answers instead, and they live here, in the fast tier, where lane C can change them and
        // test them. Their values as the contracts step left them reproduce the pane exactly as it
        // was; the bandit and the corpse are lane C's to answer.

        /// <summary>
        /// The pane's avatar slot shows this pawn's own face (and portrait) rather than a keyed
        /// badge. A person's: a colonist's, and a bandit's own portrait with the helmet on (design
        /// 42 §2 — "they need to be their own character"). An animal wears its kind's badge.
        /// </summary>
        public bool ShowsFace => Subject == InspectSubject.Colonist && !IsAnimal;

        /// <summary>
        /// The needs, the skills, the Health tab's values and the mood in the state line are
        /// synced. A colonist's only. <b>A bandit has none of them</b> (design 33 §5c: no needs;
        /// its skills are not the player's to read and its health is the bar over its head), so
        /// its pane is the animal's shape — kind, activity, where.
        /// </summary>
        public bool ShowsColonistBody => Subject == InspectSubject.Colonist && !IsAnimal && !IsHostile;

        /// <summary>
        /// The tab strip and the fixed-height tab box are built. A colonist's, and an animal's —
        /// whose box is built from an empty tab list, the pane as it was (design 33 §5i) and kept
        /// rather than changed without a decision. A bandit's is not: its pane has no tabs to
        /// hold, and an empty box would be the Health tab's place with nothing in it.
        /// </summary>
        public bool ShowsTabBox => Subject == InspectSubject.Colonist && !IsHostile;

        /// <summary>The badge the avatar slot shows when <see cref="ShowsFace"/> is false.</summary>
        public string AvatarKey =>
            Subject == InspectSubject.Item ? ItemIconKey
            : Subject == InspectSubject.Cell ? CellIconKey
            : IsAnimal || IsHostile || Subject == InspectSubject.Corpse ? KindIconKey
            : PawnKindLabels.Colonist;

        /// <summary>The corpse's registry key: its badge, and the first word of its title.</summary>
        public const string CorpseKey = "ui.pawn.corpse";

        /// <summary>Last-known values of a colonist who has left the frame, shown greyed.</summary>
        public bool Tombstoned;

        // ---- the Health tab (design 33 §1, §5d; lane C) ---------------------------------------

        /// <summary>
        /// The Health tab's rows under the bar: the colonist's condition (unhurt, hurt, stunned,
        /// downed) and what is in her hand. Rebuilt only when one of them changes.
        /// </summary>
        public readonly List<InspectRow> HealthRows = new List<InspectRow>();

        /// <summary>
        /// "73 / 100": hit points left out of the pool, in whole points, up to the next whole point
        /// so a colonist on her feet never reads nought, and nought for anybody below it. Empty
        /// when no pool is published (a frame from before combat).
        /// </summary>
        public string HealthValue = string.Empty;

        /// <summary>The bar's fill, 0 to 1000: hit points over the pool, clamped.</summary>
        public int HealthPerMille;

        /// <summary>The bar's ink, from <see cref="CombatFeedbackModel.HealthBarColour"/>.</summary>
        public HudColour HealthInk = CombatFeedbackModel.HealthGood;

        /// <summary>The Health tab's row labels, by key.</summary>
        public const string HealthKey = "ui.combat.health", ConditionKey = "ui.combat.condition",
            WeaponKey = "ui.combat.weapon";

        // ---- the Thoughts tab (design 51 §5b) ------------------------------------------------

        /// <summary>
        /// The line over the list: her mood and the target it is drifting to, in points out of a
        /// hundred ("Mood 50 · Heading for 55"). Empty when the frame publishes no target.
        /// </summary>
        public string ThoughtHeading = string.Empty;

        /// <summary>
        /// What is on her mind, one row each, grouped <i>Now</i> (the situational offsets) then
        /// <i>Memories</i>, worst first within each and a memory's ties by the soonest to lapse —
        /// the tab exists to answer "why is she breaking", so the answer is the first row. A group
        /// with nothing in it has no heading. Capped at <see cref="HudLayout.ThoughtRows"/> with the
        /// remainder counted on the last row, so the pane stays one height.
        /// </summary>
        public readonly List<InspectRow> ThoughtRows = new List<InspectRow>();

        /// <summary>
        /// Who she is (design 51 §5f): one row per trait, the name then what it does, tinted by
        /// <see cref="TraitSummary.Tint"/> and described by the registry. Drawn on the Needs tab
        /// under the bars, in the slack the fixed body leaves there. Empty for a colonist from
        /// before traits, and the pane then says nothing about them.
        /// </summary>
        public readonly List<InspectRow> TraitRows = new List<InspectRow>();

        long _traitsSignature = long.MinValue;
        long _thoughtsSignature = long.MinValue;
        readonly List<(MindCatalogue.Source Source, int Value, int Left, int Count)> _nowScratch =
            new List<(MindCatalogue.Source, int, int, int)>();
        readonly List<(MindCatalogue.Source Source, int Value, int Left, int Count)> _memoryScratch =
            new List<(MindCatalogue.Source, int, int, int)>();

        // ---- header
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public string Position = string.Empty;
        public int Layer = -1;

        /// <summary>
        /// The one line a building site has to say for itself: what it is waiting for, or how much
        /// longer it will take. Empty when the selected cell has no site on it.
        /// </summary>
        public string Site = string.Empty;

        /// <summary>The registry key for what is being built, so the pane can draw its icon.</summary>
        public string SiteIconKey = string.Empty;

        /// <summary>Units of material delivered and wanted, for a bar. Both 0 when there is no site.</summary>
        public int SiteDelivered;
        public int SiteCost;

        /// <summary>How far through the work, 0 to 1. Separate from the material, per <see cref="SiteView"/>.</summary>
        public float SiteProgress;

        // ---- item body
        /// <summary>How many are in the selected pile. The ledger counts these, never the piles.</summary>
        public int Stack;

        /// <summary>The pile's own icon key, so a pile of wood stops drawing a meal.</summary>
        public string ItemIconKey = "ui.res.meal";

        // ---- cell body
        /// <summary>
        /// The tile's facts, one row each in a fixed order: any order standing on it, the work of
        /// it, what crossing it costs, the floor, what it bears. A row is a label and a value
        /// because the pane prints them in two columns — the same fact is always in the same
        /// place, which a joined line never gave (owner, 2026-09-17). Empty when the world has
        /// not answered the question yet, and the pane says nothing rather than pretending.
        /// </summary>
        public readonly List<InspectRow> CellRows = new List<InspectRow>();

        /// <summary>The tile's own icon key, so the pane's avatar is the thing that was clicked.</summary>
        public string CellIconKey = "ui.overlay.zones";

        /// <summary>
        /// The selected pawn is an animal (design 29 §2, §8): the pane says its species, what it
        /// is doing and where it is, and nothing a person has — no portrait, no needs, no tabs,
        /// no commands. Set from the view's flags on every refresh.
        /// </summary>
        public bool IsAnimal;

        /// <summary>
        /// The selected pawn is hostile — a bandit (design 33 §1). A person, and not ours: its
        /// pane is the animal's shape (kind, activity, where) with the kind's badge, and it has no
        /// needs, skills, Health tab or Draft button. Set from the view's flags on every refresh.
        /// </summary>
        public bool IsHostile;

        // Which pawn IsAnimal and IsHostile were last read for. A pawn that leaves the frame keeps
        // the shape it had while it was in it, rather than falling to a colonist's tombstone.
        PawnId _shapeFor;

        /// <summary>The species' registry key, for the badge an animal shows where a person shows a face.</summary>
        public string KindIconKey = PawnKindLabels.Colonist;

        // ---- colonist body, the Needs tab
        /// <summary>
        /// The activity line. For an animal and for any pawn without <see cref="ShowsColonistBody"/>
        /// it is the whole of the line under the name; for a corpse it is the line the corpse's
        /// pane says there (lane C's words, set in <see cref="Refresh"/>).
        /// </summary>
        public string Job = "idle";
        public string JobIconKey = "ui.status.idle";
        public int Food;
        public int Rest;      // 0..1000, the simulation's scale
        public int Mood;      // 0..1000, like Food and Rest

        /// <summary>
        /// The <see cref="Odyssey.Sim.Contracts.MoodBand"/> the simulation published for her (design
        /// 44 §5a), which is what the pane names her mood by. Never derived here from
        /// <see cref="Mood"/>: the lines are hers and move with traits.
        /// </summary>
        public int Band;

        /// <summary>
        /// The band as a word inside a sentence, "content" to "breaking down" — and in a break, which
        /// one: "breaking down (tantrum)" (design 51 §5c). Rebuilt only when the band or the break
        /// changes, so a standing pane allocates nothing.
        /// </summary>
        public string MoodWord => _moodWord ?? MoodBands.Word(Band);

        /// <summary>The <see cref="BreakHandle"/> she is in, or -1 when she is in none.</summary>
        public int BreakKind = -1;

        string? _moodWord;
        int _moodWordBand = -1, _moodWordBreak = -2;

        // ---- no selection: the colony summary
        public int ColonySize;
        public readonly List<int> JobCounts = new List<int>();

        public readonly List<InspectTab> Tabs = new List<InspectTab>();

        /// <summary>
        /// What a built store is holding, one row per kind, biggest first. Empty for everything
        /// else — a painted zone's contents are on the board and are read off the board.
        /// </summary>
        public readonly List<StoreContentRow> StoreContents = new List<StoreContentRow>();

        /// <summary>Whether the store under the pane is one the colony built, rather than painted.</summary>
        public bool IsBuiltStore { get; private set; }

        /// <summary>
        /// Everything the Holding rows are drawn from, in one int.
        ///
        /// <para>The contents change as haulers arrive, which the filter's signature cannot see —
        /// so the group has a cheap one of its own and the pane still rebuilds nothing on a frame
        /// where nothing moved.</para>
        /// </summary>
        public int StoreContentsSignature
        {
            get
            {
                unchecked
                {
                    int signature = IsBuiltStore ? 17 : 0;
                    for (int i = 0; i < StoreContents.Count; i++)
                    {
                        signature = signature * 31 + StoreContents[i].IconKey.GetHashCode();
                        signature = signature * 31 + StoreContents[i].Units;
                        signature = signature * 31 + StoreContents[i].Stacks;
                    }
                    return signature * 31 + StoreSummary.GetHashCode();
                }
            }
        }

        /// <summary>How full a built store is, as the Holding header says it: "4 of 8 stacks".</summary>
        public string StoreSummary { get; private set; } = string.Empty;
        public readonly List<InspectCommand> Commands = new List<InspectCommand>();

        /// <summary>
        /// The Skills tab's rows: every skill the design names, whether or not the simulation can
        /// train it. Rebuilt in place on every refresh, so the pane allocates nothing per frame
        /// once the list has reached its length.
        /// </summary>
        public readonly List<SkillRow> Skills = new List<SkillRow>();

        /// <summary>
        /// Which tab the pane is showing, as an index into <see cref="Tabs"/>. Held on the model
        /// rather than in the view, because "which tab" survives a refresh and a reselection and
        /// the view is rebuilt from the model, never the other way round.
        /// </summary>
        public int ActiveTab;

        /// <summary>
        /// Show a tab, if it is one that can be shown. A disabled tab is not a no-op by accident:
        /// the catalogue's rule is that a dead control says why rather than doing nothing
        /// silently, and the reason is already on the chip's tooltip.
        /// </summary>
        public void ShowTab(int index)
        {
            if (index < 0 || index >= Tabs.Count || !Tabs[index].Enabled) return;
            ActiveTab = index;
        }

        /// <summary>The name of the tab being shown, or empty when nothing is selected.</summary>
        public string ActiveTabName =>
            ActiveTab >= 0 && ActiveTab < Tabs.Count ? Tabs[ActiveTab].Name : string.Empty;

        // Selection state is set by the pick resolver; the model never reads input itself.
        // Every subject but a corpse writes Title, Subtitle and Job itself, so each forgets which
        // corpse last wrote them: otherwise choosing that corpse again kept the other subject's
        // strings (review, 2026-09-23).
        public void SetColonist(PawnId id)
        {
            Subject = InspectSubject.Colonist;
            Pawn = id;
            Thing = ThingId.None;
            Corpse = 0;
            _corpseFor = 0;
        }

        public void SetItem(ThingId id)
        {
            Subject = InspectSubject.Item;
            Thing = id;
            Pawn = PawnId.None;
            Corpse = 0;
            _corpseFor = 0;
        }

        public void SetCell(CellRef cell)
        {
            Subject = InspectSubject.Cell;
            Pawn = PawnId.None;
            Thing = ThingId.None;
            Corpse = 0;
            _cell = cell;
            _corpseFor = 0;
        }

        /// <summary>A corpse, by <see cref="CorpseView.Id"/> (design 33 §5f).</summary>
        public void SetCorpse(int corpseId)
        {
            Subject = InspectSubject.Corpse;
            Pawn = PawnId.None;
            Thing = ThingId.None;
            Corpse = corpseId;
        }

        public void ClearSelection()
        {
            Subject = InspectSubject.None;
            Pawn = PawnId.None;
            Thing = ThingId.None;
            Corpse = 0;
            Tombstoned = false;
            _corpseFor = 0;
        }

        CellRef _cell;

        // The last cell Position was written for. The pane refreshes fifteen times a second and
        // "at 78, 59" is an interpolated string, so without this the model allocates one per
        // refresh for as long as anything is selected — which ADR 0003's flip condition F1
        // forbids, and which also defeats the view's own guard, since a fresh string instance
        // every time makes "has this changed" unanswerable by comparison.
        CellRef _positionFor;
        bool _positionWritten;

        // The three values the activity line is built from. Same argument as _positionFor above:
        // "Hauling · Wood × 8" is a composed string and the pane refreshes fifteen times a
        // second, so without this the model allocates one per refresh for as long as a colonist
        // is selected. A colonist walking a long haul is exactly the case that would do it, since
        // nothing about her changes for thirty seconds together.
        int _jobFor = int.MinValue, _carriedFor = int.MinValue, _stackFor;

        /// <summary>
        /// What this colonist is doing, and what she is carrying while she does it. Design 24 §8.
        ///
        /// <para>Two sparse aspect reads, and an empty-handed colonist pays for one of them: the
        /// absence of the row <em>is</em> the answer, so there is no sentinel to test.</para>
        /// </summary>
        void SetJob(WorldSnapshot snapshot, in PawnView pawn)
        {
            JobLabels.CarriedBy(snapshot, pawn.Id, out int carried, out int stack);

            if (_jobFor == pawn.JobDef && _carriedFor == carried && _stackFor == stack) return;

            _jobFor = pawn.JobDef;
            _carriedFor = carried;
            _stackFor = stack;
            Job = JobLabels.Carrying(pawn.JobDef, carried, stack);
        }

        /// <summary>
        /// "Pace 90% · in the rain", the line under the activity line (design 17 §5a), and what
        /// it is made of for its tooltip. Empty for anything the simulation published no pace for.
        /// </summary>
        public string Pace = string.Empty;

        /// <summary>The factors of <see cref="Pace"/> that are not the standard walk, joined.</summary>
        public string PaceTip = string.Empty;

        // What the two pace strings were last built from. The same argument as _positionFor: they
        // are composed, and the pane refreshes fifteen times a second.
        PaceModel.Factors _paceFor;
        bool _paceWritten;

        void SetPace(WorldSnapshot snapshot, PawnId id)
        {
            PaceModel.Factors factors = PaceModel.Of(snapshot, id);
            if (_paceWritten && factors.Equals(_paceFor)) return;
            _paceFor = factors;
            _paceWritten = true;
            Pace = factors.Published ? PaceModel.Line(factors) : string.Empty;
            PaceTip = factors.Published ? PaceModel.Tooltip(factors) : string.Empty;
        }

        void SetPosition(CellRef cell)
        {
            if (_positionWritten && _positionFor == cell) return;
            _positionFor = cell;
            _positionWritten = true;
            Position = $"at {cell.X}, {cell.Z}";
        }

        /// <summary>The carried half of the activity cache while an animal's line is in it: no carried def is this.</summary>
        const int AnimalActivity = int.MinValue + 1;

        // The corpse the header strings were last written for. A corpse never changes, so its
        // three strings are composed once per selection rather than fifteen times a second.
        int _corpseFor;

        /// <summary>
        /// What the corpse on the pane was, as its kind's registry key, and whether it was an
        /// animal — for the Almanac, which opens an animal's corpse on its Fauna entry. Empty and
        /// false for anything else.
        /// </summary>
        public string CorpseKindKey { get; private set; } = string.Empty;

        public bool CorpseWasAnimal { get; private set; }

        /// <summary>
        /// "Corpse of Wrenn" — "Corpse of a midden hog" — what it was, and when it died (design 33
        /// §1). A colonist is named as she was named alive: <see cref="ColonistNames.Of(uint, PawnId)"/>
        /// over the seed and id the corpse kept, which answers a player's own name first, so the
        /// roster she left and the body she left agree. A bandit is a person with a name too
        /// (design 42 §2), and keeps it; only an animal is called by its kind.
        /// </summary>
        void DescribeCorpse(in CorpseView corpse)
        {
            if (_corpseFor == corpse.Id) return;
            _corpseFor = corpse.Id;

            bool colonist = (corpse.Flags & (PawnFlags.Person | PawnFlags.Hostile)) == PawnFlags.Person;
            bool hostile = (corpse.Flags & PawnFlags.Hostile) != 0;
            CorpseKindKey = PawnKindLabels.IconKey(corpse.Kind);
            CorpseWasAnimal = (corpse.Flags & PawnFlags.Person) == 0;
            string of = !CorpseWasAnimal
                ? ColonistNames.Of(corpse.RollSeed, corpse.Pawn)
                : WithArticle(PawnKindLabels.Label(corpse.Kind).ToLowerInvariant());
            Title = Registry.Label(CorpseKey) + " of " + of;
            Subtitle = colonist ? ColonistWord : hostile ? HostileKindWord(corpse.Kind) : AnimalWord;
            Job = Registry.Label(DeadKey) + " · since " + GameClock.HourOfDay(corpse.Tick).ToString("00")
                + "h, day " + GameClock.DayOfMonth(corpse.Tick) + " of " + GameClock.MonthName(corpse.Tick);
        }

        const string DeadKey = "ui.combat.dead";

        // What a pawn is, under its name, on the living pane and the corpse's alike: the
        // registry's words lower-cased, once, so renaming a kind in icon-keys.csv renames it
        // here too (RegistryTests.TheInspectPaneWritesNoPawnKindItself).
        static readonly string ColonistWord = Registry.Label(PawnKindLabels.Colonist).ToLowerInvariant();
        static readonly string HostileWord = Registry.Label("ui.pawn.hostile").ToLowerInvariant();
        static readonly string AnimalWord = Registry.Label("ui.pawn.animal").ToLowerInvariant();
        static readonly string BanditWord = PawnKindLabels.Label(PawnKindLabels.Bandit).ToLowerInvariant();

        /// <summary>
        /// The word under a hostile person's name: "bandit" for the bandit (design 42 §2), the
        /// generic "hostile" for any hostile kind that comes after it and has no word of its own.
        /// </summary>
        static string HostileKindWord(int kind) => kind == PawnKindLabels.Bandit ? BanditWord : HostileWord;

        static string WithArticle(string noun) =>
            noun.Length > 0 && "aeiou".IndexOf(noun[0]) >= 0 ? "an " + noun : "a " + noun;

        // What the Health tab's strings were last built from, so a refresh that says the same
        // thing builds nothing — the pane refreshes fifteen times a second.
        int _healthHp = int.MinValue, _healthMax = int.MinValue, _healthWeapon = int.MinValue, _healthCondition = -1;

        /// <summary>
        /// Fill <see cref="TraitRows"/> from the slots the simulation published (design 51 §4d),
        /// rebuilding only when a slot changed — which, since traits never change, is once per
        /// colonist the pane is opened on.
        /// </summary>
        void RefreshTraits(WorldSnapshot snapshot, in PawnView pawn)
        {
            long signature = pawn.Id.Value;
            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
                signature = signature * 31 + (snapshot.TryGetPawnAspect(pawn.Id, MindAspectNames.TraitKey[slot], out int h) ? h + 1 : 0);
            if (signature == _traitsSignature) return;
            _traitsSignature = signature;

            TraitRows.Clear();
            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
            {
                if (!snapshot.TryGetPawnAspect(pawn.Id, MindAspectNames.TraitKey[slot], out int handle)) continue;
                TraitRows.Add(TraitSummary.Row(handle,
                    Read(snapshot, pawn.Id, MindAspectNames.TraitMoodKey[slot], 0),
                    Read(snapshot, pawn.Id, MindAspectNames.TraitNerveKey[slot], 0),
                    Read(snapshot, pawn.Id, MindAspectNames.TraitLearnKey[slot], 1_000),
                    Read(snapshot, pawn.Id, MindAspectNames.TraitWorkKey[slot], 1_000),
                    Read(snapshot, pawn.Id, MindAspectNames.TraitCannotKey[slot], 0)));
            }
        }

        static int Read(WorldSnapshot snapshot, PawnId pawn, AspectKey key, int otherwise) =>
            snapshot.TryGetPawnAspect(pawn, key, out int value) ? value : otherwise;

        /// <summary>
        /// Fill <see cref="ThoughtRows"/> and <see cref="ThoughtHeading"/> from what the simulation
        /// published (design 51 §4d). O(1) lookups, a dozen of them, for the one pawn on the pane;
        /// the rows are rebuilt only when a published value moved, so a standing pane allocates
        /// nothing.
        /// </summary>
        void RefreshThoughts(WorldSnapshot snapshot, in PawnView pawn)
        {
            _nowScratch.Clear();
            _memoryScratch.Clear();
            // Signed in at the resolution the heading draws, so a mood drifting a thousandth at a
            // time does not rebuild the rows on every refresh for a number that reads the same.
            long signature = MindCatalogue.PointsOf(pawn.Mood);
            bool targeted = snapshot.TryGetPawnAspect(pawn.Id, MindAspectNames.TargetKey, out int target);
            signature = signature * 31 + (targeted ? MindCatalogue.PointsOf(target) : -100_000);

            foreach (MindCatalogue.Source source in MindCatalogue.Situational)
            {
                if (!snapshot.TryGetPawnAspect(pawn.Id, source.Value, out int value) || value == 0) continue;
                _nowScratch.Add((source, value, 0, 1));
                signature = signature * 31 + value;
            }

            // A trait's permanent offset is situational: it is here now, for as long as she is
            // who she is (design 51 §4e). Named by the trait.
            for (int slot = 0; slot < TraitHandle.MaxPerPawn; slot++)
            {
                if (!snapshot.TryGetPawnAspect(pawn.Id, MindAspectNames.TraitMoodKey[slot], out int value) || value == 0) continue;
                snapshot.TryGetPawnAspect(pawn.Id, MindAspectNames.TraitKey[slot], out int handle);
                _nowScratch.Add((new MindCatalogue.Source(TraitSummary.Key(handle), default), value, 0, 1));
                signature = signature * 31 + value + handle;
            }

            foreach (MindCatalogue.Source source in MindCatalogue.Memories)
            {
                if (!snapshot.TryGetPawnAspect(pawn.Id, source.Value, out int value)) continue;
                snapshot.TryGetPawnAspect(pawn.Id, source.Left, out int left);
                if (!snapshot.TryGetPawnAspect(pawn.Id, source.Count, out int count)) count = 1;
                _memoryScratch.Add((source, value, left, count));
                // The time left is signed in at the resolution it is drawn at, so a pane open on a
                // memory does not rebuild every tick for a number that reads the same.
                signature = signature * 31 + value;
                signature = signature * 31 + MindCatalogue.LeftOf(left);
                signature = signature * 31 + count;
            }

            if (signature == _thoughtsSignature) return;
            _thoughtsSignature = signature;

            ThoughtHeading = targeted
                ? Registry.Label("ui.need.mood") + " " + MindCatalogue.Points(pawn.Mood).TrimStart('+')
                  + " · " + Registry.Label("ui.mind.target") + " " + MindCatalogue.Points(target).TrimStart('+')
                : string.Empty;

            // Worst first; a memory's tie goes to the one that lapses soonest.
            _nowScratch.Sort((a, b) => a.Value.CompareTo(b.Value));
            _memoryScratch.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.Left.CompareTo(b.Left));

            ThoughtRows.Clear();
            int budget = HudLayout.ThoughtRows;
            int wanted = (_nowScratch.Count > 0 ? _nowScratch.Count + 1 : 0)
                       + (_memoryScratch.Count > 0 ? _memoryScratch.Count + 1 : 0);
            if (wanted == 0)
            {
                ThoughtRows.Add(new InspectRow { Name = Registry.Label("ui.mind.nothing"), Value = string.Empty });
                return;
            }

            // One row is kept for the count of what did not fit, when anything does not; and when
            // both groups have something, the memories keep a heading and a row, so a long list of
            // needs never hides every memory behind "+n more".
            int room = wanted > budget ? budget - 1 : budget;
            int reserved = _memoryScratch.Count > 0 && _nowScratch.Count > 0 ? 2 : 0;
            int nowRoom = room - reserved;
            int shown = 0;
            AddGroup("ui.mind.now", _nowScratch, ref nowRoom, ref shown);
            room = nowRoom + reserved;
            AddGroup("ui.mind.memories", _memoryScratch, ref room, ref shown);
            int hidden = _nowScratch.Count + _memoryScratch.Count - shown;
            if (hidden > 0)
                ThoughtRows.Add(new InspectRow { Name = "+" + hidden + " " + Registry.Label("ui.mind.more"), Value = string.Empty });
        }

        void AddGroup(string headingKey,
            List<(MindCatalogue.Source Source, int Value, int Left, int Count)> rows, ref int room, ref int shown)
        {
            // A heading with no row under it says nothing, so a group needs room for both.
            if (rows.Count == 0 || room < 2) return;
            ThoughtRows.Add(new InspectRow
            {
                Name = Registry.Label(headingKey),
                Value = string.Empty,
                Tooltip = Registry.Describe(headingKey),
            });
            room--;
            for (int i = 0; i < rows.Count && room > 0; i++, room--, shown++)
            {
                (MindCatalogue.Source source, int value, int left, int count) = rows[i];
                string name = Registry.Label(source.Key);
                if (count > 1) name += " x" + count;
                string worth = MindCatalogue.Points(value);
                ThoughtRows.Add(new InspectRow
                {
                    Name = name,
                    Value = source.Memory
                        ? worth + "  " + MindCatalogue.Left(left) + " " + Registry.Label("ui.mind.left")
                        : worth,
                    Tint = value > 0 ? HudTheme.Good : value < 0 ? HudTheme.Bad : (HudColour?)null,
                    Tooltip = Registry.Describe(source.Key),
                });
            }
        }

        /// <summary>
        /// The Health tab (design 33 §1: HP, state, weapon). The pool is <c>odyssey.pawn.hp.max</c>,
        /// published for every person always; <b>no <c>odyssey.pawn.hp</c> beside it means whole</b>
        /// (design 33 §5d). The condition is the flags' — downed, then stunned — else hurt while
        /// below the pool, else unhurt. The weapon is <c>odyssey.pawn.weapon</c>'s item, else bare
        /// hands.
        ///
        /// <para>Three O(1) aspect lookups a refresh for the one pawn on the pane.</para>
        /// </summary>
        void RefreshHealth(WorldSnapshot snapshot, in PawnView pawn)
        {
            bool pooled = snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.HpMaxKey, out int max) && max > 0;
            if (!pooled) max = 0;
            int hp = snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.HpKey, out int published) ? published : max;
            int weaponDef = snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.WeaponKey, out int held) ? held : -1;
            int tier = snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.WeaponQualityKey, out int q) ? q : 0;
            // The tier rides the cache key: a better weapon of the same kind is a different line.
            int weapon = weaponDef < 0 ? -1 : weaponDef * 16 + tier;
            int condition = pawn.IsDowned ? 3 : pawn.IsStunned ? 2 : hp < max ? 1 : 0;

            if (hp == _healthHp && max == _healthMax && weapon == _healthWeapon && condition == _healthCondition)
                return;
            _healthHp = hp;
            _healthMax = max;
            _healthWeapon = weapon;
            _healthCondition = condition;

            int shown = hp < 0 ? 0 : hp > max ? max : hp;
            HealthValue = pooled ? WholePoints(hp) + " / " + WholePoints(max) : string.Empty;
            HealthPerMille = pooled ? (int)((long)shown * 1000 / max) : 0;
            HealthInk = CombatFeedbackModel.HealthBarColour(shown, max);

            HealthRows.Clear();
            HealthRows.Add(new InspectRow
            {
                Name = Registry.Label(ConditionKey),
                Value = Registry.Label(condition switch
                {
                    3 => CombatFeedbackModel.DownedKey,
                    2 => CombatFeedbackModel.StunnedKey,
                    1 => "ui.combat.hurt",
                    _ => "ui.combat.unhurt",
                }),
            });
            HealthRows.Add(new InspectRow
            {
                Name = Registry.Label(WeaponKey),
                Value = weaponDef >= 0 ? ItemLabels.Label(weaponDef, tier) : Registry.Label("ui.combat.barehands"),
            });
        }

        /// <summary>Thousandths to whole points, up to the next one: on her feet is never "0". Nought below it.</summary>
        static int WholePoints(int milli) => milli <= 0 ? 0 : (milli + 999) / 1000;

        /// <summary>
        /// Refill every field from the current frame. Called on the pane's cadence (15 Hz in the
        /// catalogue), and once more the moment the selection changes, so a click answers in the
        /// same frame it happened.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot)
        {
            Tabs.Clear();
            Commands.Clear();
            _bedUnderPane = false;
            _powerSwitchUnderPane = false;
            TileEdifice = 0;
            TileCellIndex = -1;
            _orderActionUnderPane = false;
            IsCampfire = false;
            IsHearth = false;
            IsStore = false;
            IsBuiltStore = false;
            StoreSummary = string.Empty;
            StoreContents.Clear();

            if (Subject == InspectSubject.Colonist)
            {
                bool present = snapshot.TryGetPawn(Pawn, out PawnView pawn);
                if (present)
                {
                    IsAnimal = PawnKindLabels.IsAnimal(pawn);
                    IsHostile = pawn.IsHostile;
                    _shapeFor = Pawn;
                }
                else if (_shapeFor != Pawn)
                {
                    // Never seen in a frame: the colonist's tombstone, as it always was.
                    IsAnimal = false;
                    IsHostile = false;
                }

                if (IsAnimal || IsHostile)
                {
                    // An animal (design 29 §8) or a bandit (design 33 §1): kind, activity,
                    // where. The colonist's tabs, commands and skills are not added, so the pane
                    // below the header is empty — and stays so when the pawn leaves the frame,
                    // which a fight now makes ordinary: it keeps its shape, greyed.
                    Skills.Clear();
                    if (!present)
                    {
                        Tombstoned = true;
                        return;
                    }
                    Tombstoned = false;
                    KindIconKey = PawnKindLabels.IconKey(pawn.Kind);
                    // A bandit is a person with a name of their own, dealt from the colonist
                    // pool by the same seed and id (design 42 §2: "they need to be their own
                    // character"); an animal is called by its kind.
                    Title = IsAnimal ? PawnKindLabels.Label(pawn.Kind) : ColonistNames.Of(snapshot, pawn.Id);
                    if (IsAnimal)
                    {
                        Subtitle = AnimalWord;
                        // Its own mark in the carried half of the cache, so a colonist's line and
                        // an animal's for the same job cannot be taken for each other; the stack
                        // half carries whether the rain has sent it for cover (design 43 §6a).
                        bool sheltering = PawnKindLabels.IsSheltering(snapshot, pawn.Id);
                        int shelterMark = sheltering ? 1 : 0;
                        string activity = PawnKindLabels.ActivityKey(pawn.JobDef, sheltering);
                        if (_jobFor != pawn.JobDef || _carriedFor != AnimalActivity || _stackFor != shelterMark)
                        {
                            _jobFor = pawn.JobDef;
                            _carriedFor = AnimalActivity;
                            _stackFor = shelterMark;
                            Job = Registry.Label(activity);
                        }
                        JobIconKey = activity;
                    }
                    else
                    {
                        // A bandit's job is a person's job — fighting, mostly — in a person's words,
                        // under the kind's word where the name would otherwise leave you guessing.
                        Subtitle = HostileKindWord(pawn.Kind);
                        SetJob(snapshot, pawn);
                        JobIconKey = JobLabels.IconKey(pawn.JobDef);
                    }
                    SetPosition(pawn.Cell);
                    Layer = pawn.Cell.Y;
                    return;
                }

                if (present)
                {
                    Tombstoned = false;
                    Title = ColonistNames.Of(snapshot, pawn.Id);
                    Subtitle = ColonistWord;
                    SetJob(snapshot, pawn);
                    JobIconKey = JobLabels.IconKey(pawn.JobDef);
                    SetPace(snapshot, pawn.Id);
                    Food = pawn.Food;
                    Rest = pawn.Rest;
                    Mood = pawn.Mood;
                    Band = MoodBands.Of(snapshot, pawn.Id);
                    BreakKind = Band == MoodBand.Broken
                        && snapshot.TryGetPawnAspect(pawn.Id, MindAspectNames.BreakKey, out int kind) ? kind : -1;
                    if (Band != _moodWordBand || BreakKind != _moodWordBreak)
                    {
                        _moodWordBand = Band;
                        _moodWordBreak = BreakKind;
                        _moodWord = BreakKind >= 0 && BreakKind < BreakHandle.Count
                            ? MoodBands.Word(Band) + " (" + Registry.Label("ui.break." + BreakHandle.Names[BreakKind]).ToLowerInvariant() + ")"
                            : MoodBands.Word(Band);
                    }
                    SetPosition(pawn.Cell);
                    Layer = pawn.Cell.Y;
                    RefreshHealth(snapshot, pawn);
                    RefreshTraits(snapshot, pawn);
                    RefreshThoughts(snapshot, pawn);
                }
                else
                {
                    // Gone from the frame. The pane stays open on last-known values, greyed by
                    // the view, with no way to command a subject that no longer exists.
                    Tombstoned = true;
                }

                AddColonistTabs();
                AddColonistCommands(!Tombstoned && OrderModel.IsDrafted(snapshot, Pawn), ResponseModel.Of(snapshot, Pawn));
                RefreshSkills(snapshot);
                return;
            }

            Tombstoned = false;
            IsAnimal = false;
            IsHostile = false;

            // A corpse (design 33 §1, §5f): "Corpse of X", what it was, when it died and where it
            // lies, with the corpse badge and no tabs or commands.
            if (Subject == InspectSubject.Corpse)
            {
                Skills.Clear();
                KindIconKey = CorpseKey;
                var corpses = snapshot.Corpses;
                for (int i = 0; i < corpses.Length; i++)
                {
                    if (corpses[i].Id != Corpse) continue;
                    DescribeCorpse(corpses[i]);
                    SetPosition(corpses[i].Cell);
                    Layer = corpses[i].Cell.Y;
                    return;
                }

                // Not in this frame (a load, or a later cleanup): the word alone, rather than the
                // last corpse's name.
                _corpseFor = 0;
                CorpseKindKey = string.Empty;
                CorpseWasAnimal = false;
                Title = Registry.Label(CorpseKey);
                Subtitle = string.Empty;
                Job = string.Empty;
                return;
            }

            if (Subject == InspectSubject.Item)
            {
                bool found = false;
                ThingView atThing = default;
                var things = snapshot.Things;
                for (int i = 0; i < things.Length; i++)
                {
                    if (things[i].Id != Thing) continue;
                    ThingView thing = things[i];

                    // **The count is the title, not a footnote.** It was on the state line under
                    // it and the owner read the pane twice without seeing it (2026-09-19: "when I
                    // click on wood I can't see how many is this pile"). A pile's size is the
                    // first thing asked of it, and the title is where the eye lands — so the
                    // headline is "Wood × 27" and the line below says where it is lying.
                    Title = thing.Stack > 1
                        ? ItemLabels.Label(thing.DefIndex) + " × " + thing.Stack
                        : ItemLabels.Label(thing.DefIndex, thing.Quality);
                    Subtitle = "item";
                    Stack = thing.Stack;
                    ItemIconKey = ItemLabels.IconKey(thing.DefIndex);
                    SetPosition(thing.Cell);
                    Layer = thing.Cell.Y;
                    atThing = thing;
                    found = true;
                    break;
                }
                if (!found) Subtitle = "item · no longer present";

                // A pile lying in a field carries the field's answer (owner, 2026-09-19: the
                // click area that mattered was the tile, and the pile ate it - the only clear
                // ground to click was between the plants). The query the picker asked was for
                // this very cell, so its detail is already in the frame.
                CellRows.Clear();
                if (found && snapshot.TryGetCellDetail(
                        snapshot.Size.Index(atThing.Cell), out CellDetail under))
                {
                    if (under.ZonePlant != byte.MaxValue)
                    {
                        string plant = Registry.Label(BuildLabels.PlantKey(under.ZonePlant));
                        string howMany = under.ZoneYield > 1 ? plant + " × " + under.ZoneYield : plant;
                        Row(0, "growing", under.CropGrowth == ushort.MaxValue
                            ? howMany + " — awaiting its seed"
                            : howMany + " — " + under.CropGrowth / 10 + "% grown");
                    }
                }
                return;
            }

            if (Subject == InspectSubject.Cell)
            {
                SetPosition(_cell);
                Layer = _cell.Y;
                if (DescribeSiteAt(snapshot, _cell))
                {
                    // A site leads and is the whole answer: the tile under a blueprint is the
                    // least interesting thing about the click — except the one thing a player
                    // does with an order they have changed their mind about, which is cancel it
                    // (owner, 2026-09-23). One row, in the cancel tool's red, taking the building
                    // order only: a line ordered through the same cell has a pane of its own.
                    CellRows.Clear();
                    _cellRowsFor = -1;
                    CellIconKey = "ui.overlay.zones";
                    _orderActionUnderPane = true;
                    OrderAction = IntentKind.CancelBuilding;
                    OrderActionA = 1;
                    Row(0, OrderActionRow, Registry.Label(PaletteTools.Cancel),
                        OrderActionTint(IntentKind.CancelBuilding, keep: false));
                    return;
                }

                Site = string.Empty;
                SiteIconKey = string.Empty;
                DescribeCellAt(snapshot, _cell);

                return;
            }

            // Nothing selected: the colony as a whole, which answers "is anything happening".
            Title = "The holding";
            Subtitle = "nothing selected";
            Position = string.Empty;
            _positionWritten = false;   // or a later selection on the same cell keeps the blank
            Layer = -1;
            ColonySize = snapshot.PawnCount;
            JobCounts.Clear();
            for (int job = 0; job < JobHandle.Count; job++) JobCounts.Add(0);
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
                if (pawns[i].JobDef >= 0 && pawns[i].JobDef < JobHandle.Count)
                    JobCounts[pawns[i].JobDef]++;
        }

        /// <summary>
        /// What is going up here, whether it has its material, and how much longer.
        ///
        /// <para><b>The three questions a player asks of a blueprint, and the pane could answer
        /// none of them</b> — a click on a site said "Ground · cell", exactly as a click on bare
        /// grass did, so the one thing on the board that is <i>about</i> a plan had nothing to say
        /// about it (owner, 2026-09-17).</para>
        ///
        /// <para><b>The material line leads when the material is missing</b>, because that is the
        /// actionable half: a site with no wood is not slow, it is stuck, and telling the player
        /// "0%" would describe the symptom rather than the cause. Once it is fed, the time left is
        /// what they want, so that is what the line becomes. It is the same rule <c>AlertModel</c>
        /// follows — lead with the actionable clause.</para>
        ///
        /// <para>The estimate is honest about being one: it is the work remaining at one colonist's
        /// pace, and two builders halve it while none makes it infinite. "about" is doing real work
        /// in that sentence.</para>
        /// </summary>
        bool DescribeSiteAt(WorldSnapshot snapshot, CellRef cell)
        {
            int index = snapshot.Size.Index(cell);
            var sites = snapshot.Sites;

            for (int i = 0; i < sites.Length; i++)
            {
                if (sites[i].CellIndex != index) continue;

                SiteView site = sites[i];
                string thing = BuildLabels.Building(site.Building);
                string stuff = BuildLabels.Stuff(site.Stuff);

                Title = thing.Length == 0 ? "Building site" : thing;
                Subtitle = stuff.Length == 0 ? "planned" : "planned · " + stuff;
                SiteIconKey = BuildLabels.BuildingKey(site.Building);
                SetSiteLine(site, stuff);
                SiteDelivered = site.Delivered;
                SiteCost = site.Cost;
                SiteProgress = site.Progress;
                return true;
            }

            SiteDelivered = 0;
            SiteCost = 0;
            SiteProgress = 0f;
            _siteDeliveredFor = -1;
            _siteSecondsFor = -1;
            return false;
        }

        // The last values Site was written for. Same argument as _positionFor: the pane refreshes
        // fifteen times a second and this is an interpolated string, so rebuilding it every time
        // would allocate for as long as a site is selected — ADR 0003 F1 — and would also defeat
        // the view's own guard, which asks "is this the same instance".
        //
        // The seconds reading is quantised to whole seconds before it is compared, so a countdown
        // rebuilds once a second rather than fifteen times.
        int _siteDeliveredFor = -1;
        int _siteSecondsFor = -1;
        bool _siteFramedFor;

        void SetSiteLine(SiteView site, string stuff)
        {
            int seconds = site.IsFrame ? (site.WorkTotal - site.WorkDone + 59) / 60 : -1;
            int delivered = site.Delivered * 1_000 + site.PartsDelivered;
            if (_siteFramedFor == site.IsFrame
                && _siteDeliveredFor == delivered
                && _siteSecondsFor == seconds) return;

            _siteFramedFor = site.IsFrame;
            _siteDeliveredFor = delivered;
            _siteSecondsFor = seconds;

            // The material leads while it is missing, because that is the actionable half: a site
            // with no wood is not slow, it is stuck.
            // A second payment follows the first (design 32 §14): the scrap metal a generator
            // still wants is said once its wood is in — "20 of 30 wood" while that is the stuck
            // half, then "4 of 20 scrap metal".
            string parts = site.PartsItem >= 0
                ? Registry.Label(ItemLabels.IconKey(site.PartsItem)).ToLowerInvariant()
                : string.Empty;
            Site = site.IsFrame
                ? "about " + Seconds(site.WorkTotal - site.WorkDone) + " left"
                : site.Delivered < site.Cost || site.PartsCost == 0
                    ? $"{site.Delivered} of {site.Cost} {stuff} delivered"
                    : $"{site.PartsDelivered} of {site.PartsCost} {parts} delivered";
        }

        /// <summary>
        /// Ticks as a rough wall-clock reading at speed 1, which is the only pace a player can
        /// judge a wait against. 60 ticks a second, per the composition root's own rate.
        /// </summary>
        static string Seconds(int ticks)
        {
            if (ticks <= 0) return "no time";
            int seconds = (ticks + 59) / 60;
            if (seconds < 60) return seconds + "s";
            return seconds / 60 + "m " + seconds % 60 + "s";
        }

        // ---- the cell readout --------------------------------------------------------------

        /// <summary>
        /// What the clicked cell is, when no site stands on it.
        ///
        /// <para><b>The title names the thing that was clicked, in the picker's own order of
        /// ownership:</b> the edifice standing in the cell above all (a tree is what a click on a
        /// tree means), then the built slab (a bridge is walked on, and the tile under the click
        /// is air), then the terrain itself. "Ground" is what a blank key leaves — the city's
        /// finished surfaces, which are not the played board's to name yet.</para>
        ///
        /// <para><b>No answer is a state, not a gap.</b> The row arrives one publish after the
        /// click and is withdrawn when the question is, so a pane may honestly show "Ground" for
        /// the frame between the two. What it must never do is invent a reading.</para>
        /// </summary>
        void DescribeCellAt(WorldSnapshot snapshot, CellRef cell)
        {
            if (!snapshot.TryGetCellDetail(snapshot.Size.Index(cell), out CellDetail detail))
            {
                Title = "Ground";
                Subtitle = "cell";
                CellIconKey = "ui.overlay.zones";
                if (_cellRowsFor >= 0)
                {
                    CellRows.Clear();
                    _cellRowsFor = -1;
                }
                return;
            }

            Subtitle = "cell";

            // **A click inside a store is about the store.** The pane titles the zone, says how big
            // it is, and offers the tile's own facts on a second tab — which is the whole of the
            // owner's report that a stockpile "reads as though it belongs to a single tile"
            // (design brief, 2026-09-21; docs/design/26-storage.md §9).
            //
            // The subject stays `Cell`, deliberately: everything below still describes the tile, a
            // zone has no identity a selection could hold on to across an edit, and the pick
            // resolver goes on answering in cells. What changes is what the pane leads with.
            // **Either kind of store**, since 2026-09-21: a shelf is a store the player built
            // rather than painted, and a pane that led with the tile for one and with the store for
            // the other would be the same report arriving a second time. The ordinal is one series
            // across both, so "Shelf 3" and "Stockpile 3" can never be the same store.
            IsStore = detail.StoreKind != CellDetail.StoreNone;
            // A painted zone's contents are on the board and are read off the board; only a built
            // one has an inside that has to be described. The Holding group hangs off this rather
            // than off the list being non-empty, so an empty shelf can say it is empty instead of
            // the section silently not existing.
            IsBuiltStore = detail.StoreKind == CellDetail.StoreShelf;
            if (IsStore)
            {
                bool built = detail.StoreKind == CellDetail.StoreShelf;
                StoreCells = detail.StorageCells;

                string storeKey = built ? PaletteTools.Shelf : PaletteTools.Stockpile;
                Title = $"{Registry.Label(storeKey)} {detail.StorageOrdinal}";
                CellIconKey = storeKey;

                // A zone's extent is its tiles; a shelf's is how full it is, because "1 tile" says
                // nothing at all about a thing that is always one tile.
                Subtitle = built
                    ? $"{detail.StoredStacks} of {detail.StoreSlots} stacks"
                    : StoreCells == 1 ? "1 tile" : $"{StoreCells} tiles";
                StoreSummary = built ? $"{detail.StoredStacks} of {detail.StoreSlots} stacks" : string.Empty;
                // Named from the registry, not written here. "Tile" is already the name of a floor
                // covering in `ui.arch.tool.tile`, so a literal would have been a second copy of a
                // name the wiki owns — which `RegistryTests` said, and the answer to that test is
                // never to reword.
                Tabs.Add(new InspectTab { Name = Registry.Label(TabStorage), Enabled = true, Reason = string.Empty });
                Tabs.Add(new InspectTab { Name = Registry.Label(TabTile), Enabled = true, Reason = string.Empty });
                if (ActiveTab < 0 || ActiveTab >= Tabs.Count) ActiveTab = 0;
            }

            // Outside the branch on purpose: it clears as well as fills, and a list left behind
            // by the last selection would be drawn over the next one. Every other field here is
            // assigned on every path for the same reason.
            FillStoreContents(snapshot, detail);

            string edifice = EdificeLabels.Title(detail.Edifice);
            string terrain = TerrainLabels.Label(detail.Terrain);

            // What the tile itself is called. Computed either way, because the Tile tab says it
            // even when the store's name is what the header carries.
            string tileTitle;
            string tileIcon;
            if (edifice.Length > 0)
            {
                tileTitle = edifice;
                tileIcon = EdificeLabels.IconKey(detail.Edifice);
            }
            else if (detail.FloorStuff != StuffHandle.None)
            {
                string stuff = BuildLabels.Stuff(detail.FloorStuff);
                tileTitle = stuff.Length == 0
                    ? "Built floor"
                    : char.ToUpperInvariant(stuff[0]) + stuff.Substring(1) + " floor";
                tileIcon = BuildLabels.StuffKey(detail.FloorStuff);
            }
            else if (terrain.Length > 0)
            {
                tileTitle = terrain;
                tileIcon = TerrainLabels.IconKey(detail.Terrain);
            }
            else
            {
                tileTitle = "Ground";
                tileIcon = "ui.overlay.zones";
            }

            // A cell holding nothing but a line — an order over open ground, or a laid line in the
            // air while the lines are shown — is about the line (design 32 §14): the ground under
            // it is what a click on the ground means, and this click landed on the line.
            if (edifice.Length == 0 && detail.FloorStuff == StuffHandle.None)
                foreach (ConduitView line in snapshot.Conduits)
                    if (line.CellIndex == detail.CellIndex)
                    {
                        tileTitle = Registry.Label(PaletteTools.Conduit);
                        tileIcon = PaletteTools.Conduit;
                        break;
                    }

            if (!IsStore)
            {
                Title = tileTitle;
                CellIconKey = tileIcon;
            }

            SetCellRows(snapshot, detail);
        }

        // The last cell the readout rows were written for, and everything they quote. The pane
        // refreshes fifteen times a second and the rows are strings, so they are rebuilt only
        // when something they say has moved — the same argument as _positionFor and
        // _siteSecondsFor, and the same flip condition F1 from ADR 0003. Order progress is
        // compared as the whole percent it prints, so a face being cut rebuilds the rows once
        // per percent rather than fifteen times a second.
        int _cellRowsFor = -1;
        int _cellRowsCost;
        int _cellRowsFloor;
        int _cellRowsEdifice;
        int _cellRowsSupport;
        int _cellRowsWork;
        int _cellRowsOrderKind;
        int _cellRowsOrderPercent;
        int _cellRowsQuality;
        int _cellRowsOwner;

        /// <summary>
        /// The store's rung, plus one, or 0 for no store — the two facts the storage row draws
        /// folded into the one integer this guard needs. Plus one because rung 0 is Last, a real
        /// answer, and a guard that could not tell it from "no store at all" would leave the row
        /// standing over a cell the player had just un-zoned.
        /// </summary>
        byte _cellRowsStoreKind;
        byte _cellRowsStoredStacks;
        byte _cellRowsStoredDef;
        int _cellRowsStoredUnits;
        int _cellRowsStoragePriority;

        static int StoragePriorityOf(CellDetail detail) =>
            detail.StoreKind != CellDetail.StoreNone ? detail.StoragePriority + 1 : 0;

        int _cellRowsZonePlant;
        int _cellRowsZoneYield;
        int _cellRowsCropGrowth;
        bool _cellRowsIndoors;
        int _cellRowsTemp;

        /// <summary>
        /// Whether the tile under the pane is a bed whose owner row can be pressed — the pane's
        /// first interactive fact, and the one thing a tile readout can do rather than only say
        /// (design 20 §8). Cleared every refresh and set only by <see cref="SetCellRows"/>, so a
        /// stale true cannot outlive the bed it described.
        /// </summary>
        public bool BedUnderPane => _bedUnderPane;

        bool _bedUnderPane;

        /// <summary>
        /// Whether the tile under the pane is a power building whose switch row can be pressed
        /// (design 32 §5) — the pane's second interactive fact, kept exactly as the bed's is:
        /// cleared every refresh, set only by <see cref="SetCellRows"/> and set <b>above</b> its
        /// early return, so the control cannot die on the second refresh and go on looking alive.
        /// </summary>
        public bool PowerSwitchUnderPane => _powerSwitchUnderPane;

        /// <summary>Whether the building under the pane is switched on; what a press on its switch row reverses.</summary>
        public bool PowerSwitchOn { get; private set; }

        bool _powerSwitchUnderPane;

        /// <summary>
        /// What stands in the tile under the pane, as an <see cref="EdificeHandle"/> value, or 0.
        /// Cleared every refresh and set by <see cref="SetCellRows"/> with the other tile flags, so
        /// the bill list (design 48 §5) is shown for exactly the station the answer is about.
        /// </summary>
        public int TileEdifice { get; private set; }

        /// <summary>The whole-world index of the tile the answer is about, or -1 before there is one.</summary>
        public int TileCellIndex { get; private set; } = -1;

        /// <summary>
        /// Whether the pane holds an order whose action row can be pressed: a building site's
        /// Cancel, or a line's — Cancel an order, Remove a laid line, Keep one marked to come up
        /// (design 32 §14; owner, 2026-09-23: "the same for any building blueprint that has been
        /// put down so it can be cancelled"). Kept as the switch's and the bed's flags are —
        /// cleared every refresh, set before any early return.
        /// </summary>
        public bool OrderActionUnderPane => _orderActionUnderPane;

        /// <summary>What a press on the order row sends, about <see cref="Cell"/>, with <see cref="OrderActionA"/>.</summary>
        public IntentKind OrderAction { get; private set; }

        /// <summary>The intent's <c>A</c>: 1 on a building site's cancel, which takes the building order only.</summary>
        public int OrderActionA { get; private set; }

        bool _orderActionUnderPane;

        /// <summary>The order row's key, which the shell compares against rather than against a word.</summary>
        public const string OrderActionRow = "order";

        /// <summary>
        /// The colour an order's action is drawn in: a cancel in the cancel tool's red, taking a
        /// line up in the remove tool's amber, and keeping a marked line in no colour of its own.
        /// <c>OrderColours</c> is the one owner of an order's hue, so the button and the tool the
        /// player would otherwise reach for are the same colour.
        /// </summary>
        public static HudColour? OrderActionTint(IntentKind action, bool keep) =>
            keep ? (HudColour?)null
            : action == IntentKind.RemoveConduit ? OrderColours.Hue(DesignateTool.RemoveConduit)
            : OrderColours.Hue(DesignateTool.Cancel);

        /// <summary>Everything the power rows quote, folded into one number for the rebuild guard.</summary>
        int _cellRowsPower;

        /// <summary>
        /// The pane holds a campfire (design 43 §6). A campfire's pane is wide, as a store's is: the
        /// hearth's button does not fit the 280 px tile column (owner, 2026-09-25). Cleared every
        /// refresh and set before the cell rows' early return, as the bed's and the switch's flags
        /// are, so neither answer can outlive the fire.
        /// </summary>
        public bool IsCampfire { get; private set; }

        /// <summary>The pane's campfire is the hearth: the header says so, on a line under the name.</summary>
        public bool IsHearth { get; private set; }

        /// <summary>The pane's campfire is not the hearth, and one button under the header makes it so.</summary>
        public bool OffersHearth => IsCampfire && !IsHearth;

        /// <summary>A store's pane and a campfire's are the full 560; every other tile's is the narrow column.</summary>
        public bool IsWide => IsStore || IsCampfire || IsStation;

        /// <summary>
        /// Something that takes bills stands in the tile (design 49): the pane is the bench width
        /// and carries the bill list. A campfire is one, so it is wide on both counts.
        /// </summary>
        public bool IsStation => BillsModel.IsStation(TileEdifice);

        /// <summary>The header line on the hearth, and the button on any other campfire.</summary>
        public const string HearthKey = "ui.home.hearth";
        public const string MakeHearthKey = "ui.command.sethearth";

        /// <summary>The switch row's key, which the shell compares against rather than against a word.</summary>
        public const string PowerSwitchRow = "switch";

        /// <summary>
        /// The power building and net at this cell, folded to one number for the guard — every
        /// field the rows below print, so a hopper going down a unit or a net going dark rebuilds
        /// the rows and nothing else does.
        /// </summary>
        static int PowerSignature(WorldSnapshot snapshot, int cell)
        {
            int sig = 17;
            if (snapshot.TryGetPowerDevice(cell, out PowerDeviceView d))
            {
                sig = sig * 31 + (d.On ? 1 : 2);
                sig = sig * 31 + (d.Powered ? 1 : 2);
                sig = sig * 31 + d.NetKey;
                sig = sig * 31 + d.LoadW;
                sig = sig * 31 + d.FuelMilli / 1_000;
                if (snapshot.TryGetPowerNet(d.NetKey, out PowerNetView net))
                    sig = sig * 31 + net.SupplyW * 7 + net.DemandW * 13 + (int)net.State;
            }
            foreach (ConduitView line in snapshot.Conduits)
                if (line.CellIndex == cell) sig = sig * 31 + (int)line.Kind * 3 + (int)line.State + 1_000;
            return sig;
        }

        /// <summary>
        /// The selected cell is inside a storage zone, so the pane is about the <b>store</b>: the
        /// title is the zone's, the subtitle is its extent, and there are two tabs with Storage
        /// first and the tile's own facts second.
        /// </summary>
        public bool IsStore { get; private set; }

        /// <summary>
        /// How many cells the store covers — the extent the pane's title line carries.
        ///
        /// <para>The only one of these the pane turned out to need. <c>StorePriority</c> and
        /// <c>StoreTileTitle</c> were written beside it for a header chip and a Tile-tab title
        /// that were never built, and were set on every refresh and read by nothing until they
        /// were taken out on 2026-09-21. A property whose doc comment describes a feature that
        /// does not exist is the most expensive kind of dead code: it reads as a contract.</para>
        /// </summary>
        public int StoreCells { get; private set; }

        /// <summary>The two tabs a store's pane carries, by registry key. The shell compares against these rather than against words.</summary>
        public const string TabStorage = "ui.tab.storage";

        public const string TabTile = "ui.tab.tile";

        /// <summary>
        /// The cell the pane is describing, for whoever must name it back to the world — the
        /// owner picker's pick is an intent about this cell.
        /// </summary>
        public CellRef Cell => _cell;

        /// <summary>
        /// The tile's facts, one row each, in a fixed order so a fact is always in the same
        /// place: any order standing on the cell first — the actionable clause leads, the same
        /// rule <c>AlertModel</c> and the site line follow — then the work of it, what crossing
        /// it costs, the floor, and what it bears.
        /// </summary>
        void SetCellRows(WorldSnapshot snapshot, in CellDetail detail)
        {
            byte progress = 0, kind = 0;
            bool ordered = false;
            var orders = snapshot.Orders;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].CellIndex != detail.CellIndex) continue;
                ordered = true;
                kind = orders[i].Kind;
                progress = orders[i].Progress;
                break;
            }
            int orderPercent = (progress * 100 + 127) / 255;

            // **Set before the early return, not inside the rebuild.** This is a fact about the
            // cell the pane is holding, not about whether the rows happened to change — and it was
            // written as the latter, which armed the affordance for exactly one frame and then
            // killed it. `Refresh` clears it every time; `SetCellRows` returns here whenever
            // nothing has moved; so the second refresh after a bed was selected cleared the flag,
            // took this return, and never set it again. The row went on reading "Assign…" for ever
            // over a control that was dead, and the owner reported being unable to assign a bed
            // three times across two sessions before a test could say why (BedOwnerPickerTests).
            // **And it asks what the thing is, not only whether it has a tier.** A quality above
            // nought used to be a good enough proxy for "this is a bed" because a bed was the only
            // thing that took one. The shelf takes none, so it does not trip this — but the next
            // piece of quality-bearing furniture would, and the row it grew would open the *bed*
            // picker over it. Three characters against a report.
            _bedUnderPane = detail.EdificeQuality > 0 && detail.Edifice == EdificeHandle.Bed;
            TileEdifice = detail.Edifice;
            TileCellIndex = detail.CellIndex;
            if (snapshot.TryGetPowerDevice(detail.CellIndex, out PowerDeviceView switchable))
            {
                _powerSwitchUnderPane = true;
                PowerSwitchOn = switchable.On;
            }
            int powerSignature = PowerSignature(snapshot, detail.CellIndex);
            foreach (ConduitView held in snapshot.Conduits)
            {
                if (held.CellIndex != detail.CellIndex) continue;
                _orderActionUnderPane = true;
                OrderAction = held.Kind == ConduitKind.Built ? IntentKind.RemoveConduit : IntentKind.CancelConduit;
                OrderActionA = 0;
                break;
            }
            // Set beside the bed's flag and **above** the early return below, for the reason that
            // whole paragraph exists: a flag cleared every refresh and set only after the return
            // is a control that dies on the second refresh and goes on looking alive.
            // The hearth (design 43 §3f, §6) the same way: header facts, not rows, so they are not
            // in the rows' guard; the shell's rebuild signature carries them instead.
            IsCampfire = detail.Edifice == EdificeHandle.Campfire;
            IsHearth = IsCampfire && snapshot.HearthCell == detail.CellIndex;

            if (_cellRowsFor == detail.CellIndex
                && _cellRowsCost == detail.MoveCostPerMille
                && _cellRowsFloor == detail.FloorStuff
                && _cellRowsEdifice == detail.Edifice
                && _cellRowsSupport == detail.Support
                && _cellRowsWork == detail.WorkToClear
                && _cellRowsOrderKind == (ordered ? kind : 0)
                && _cellRowsOrderPercent == (ordered ? orderPercent : 0)
                && _cellRowsQuality == detail.EdificeQuality
                && _cellRowsOwner == detail.EdificeOwner
                && _cellRowsZonePlant == detail.ZonePlant
                && _cellRowsCropGrowth == detail.CropGrowth
                && _cellRowsZoneYield == detail.ZoneYield
                && _cellRowsStoragePriority == StoragePriorityOf(detail)
                && _cellRowsStoreKind == detail.StoreKind
                && _cellRowsStoredStacks == detail.StoredStacks
                && _cellRowsStoredUnits == detail.StoredUnits
                && _cellRowsStoredDef == detail.StoredDef
                && _cellRowsIndoors == detail.IsIndoors
                && _cellRowsTemp == detail.AmbientTempC
                && _cellRowsPower == powerSignature) return;

            _cellRowsFor = detail.CellIndex;
            _cellRowsCost = detail.MoveCostPerMille;
            _cellRowsFloor = detail.FloorStuff;
            _cellRowsEdifice = detail.Edifice;
            _cellRowsSupport = detail.Support;
            _cellRowsWork = detail.WorkToClear;
            _cellRowsOrderKind = ordered ? kind : 0;
            _cellRowsOrderPercent = ordered ? orderPercent : 0;
            _cellRowsQuality = detail.EdificeQuality;
            _cellRowsOwner = detail.EdificeOwner;
            _cellRowsZonePlant = detail.ZonePlant;
            _cellRowsCropGrowth = detail.CropGrowth;
            _cellRowsZoneYield = detail.ZoneYield;
            _cellRowsStoragePriority = StoragePriorityOf(detail);
            _cellRowsStoreKind = detail.StoreKind;
            _cellRowsStoredStacks = detail.StoredStacks;
            _cellRowsStoredUnits = detail.StoredUnits;
            _cellRowsStoredDef = detail.StoredDef;
            _cellRowsIndoors = detail.IsIndoors;
            _cellRowsTemp = detail.AmbientTempC;
            _cellRowsPower = powerSignature;

            // Written in place, like the skills list: the count is a handful and changes rarely,
            // so the list never churns while a tile is held.
            int n = 0;
            if (ordered)
                Row(n++, OrderVerb(kind), orderPercent + "% done");
            else if (detail.WorkToClear > 0)
                Row(n++, "minable", "about " + Seconds(detail.WorkToClear) + " of work");

            // A bed's own two facts, beside what it is (design 20 §8): how well it was made, and
            // whose it is. The owner row is the pane's first interactive row — the shell turns a
            // press on it into the assign popover — so it is said even where nobody owns the bed
            // yet, because "give this to somebody" is the actionable clause and the actionable
            // clause leads.
            if (detail.EdificeQuality > 0)
            {
                Row(n++, "quality", QualityLabels.Label(detail.EdificeQuality),
                    HudTheme.Quality(detail.EdificeQuality));

                // "Assign…" rather than an em dash for a bed nobody owns. The row has been
                // pickable since it was written and nothing said so: it looked exactly like the
                // rows above and below it, which are facts, and the owner could not find the
                // feature at all (2026-09-17: "I couldn't work out how to assign a colonist to a
                // bed"). A control has to say it is one, and the word is the cheapest way to.
                Row(n++, "owner", detail.EdificeOwner > 0
                    ? ColonistNames.Of(snapshot, new PawnId(detail.EdificeOwner))
                    : "Assign…");
            }

            // A power building's own facts (design 32 §10), beside what it is: what it is doing —
            // the actionable clause, so it leads — its hopper, its net's balance, and the switch,
            // which the shell turns a press on into the switch intent, as it does the bed's owner.
            if (snapshot.TryGetPowerDevice(detail.CellIndex, out PowerDeviceView device))
            {
                bool netKnown = snapshot.TryGetPowerNet(device.NetKey, out PowerNetView net);
                HudColour? tint = !device.On || device.NetKey < 0 ? HudTheme.TextMeta
                    : device.Role == PowerRole.Generator && device.BurnsFuel && device.FuelMilli <= 0 ? HudTheme.Bad
                    : netKnown ? PowerLabels.Colour(net.State) : (HudColour?)null;
                Row(n++, "power", PowerLabels.Status(device, netKnown, net), tint);
                if (device.BurnsFuel)
                    Row(n++, "fuel", PowerLabels.Fuel(device),
                        device.FuelMilli * 2 < device.FuelCapacityMilli ? HudTheme.Warn : (HudColour?)null);
                if (netKnown)
                    Row(n++, "net", PowerLabels.Balance(net) + " — " + PowerLabels.State(net.State),
                        PowerLabels.Colour(net.State));
                Row(n++, PowerSwitchRow, Registry.Label(device.On ? "ui.command.switchoff" : "ui.command.switchon"));
            }

            // A line in the cell, when it is drawn: laid (and on what), ordered, or marked to come
            // up. Silent while the lines are hidden — a hidden thing the pane announced would be
            // the pane contradicting the picture.
            foreach (ConduitView line in snapshot.Conduits)
            {
                if (line.CellIndex != detail.CellIndex) continue;
                string lineSays = line.Kind == ConduitKind.Ordered ? "ordered"
                    : line.Kind == ConduitKind.Marked ? "marked to come up"
                    : "laid — " + PowerLabels.State(line.State);
                if (line.Kind == ConduitKind.Built && snapshot.TryGetPowerNet(line.NetKey, out PowerNetView lineNet))
                    lineSays += ", " + PowerLabels.Balance(lineNet);
                Row(n++, "conduit", lineSays, line.Kind == ConduitKind.Built ? PowerLabels.Colour(line.State) : (HudColour?)null);
                // The line's own action, which the shell turns a press on into an intent: cancel
                // an order, take a laid line up, or keep one that is marked (design 32 §14).
                bool keep = line.Kind == ConduitKind.Marked;
                Row(n++, OrderActionRow, line.Kind == ConduitKind.Ordered ? Registry.Label(PaletteTools.Cancel)
                    : keep ? "Keep it"
                    : Registry.Label(PaletteTools.Unwire),
                    OrderActionTint(line.Kind == ConduitKind.Built ? IntentKind.RemoveConduit : IntentKind.CancelConduit, keep));
                break;
            }

            // A field's own answer, beside the ground's (owner, 2026-09-18 - clicking a growing
            // zone should say what is growing there). What the zone grows and how far the
            // standing crop has come; the changing percentage is why the pane is worth holding
            // open over a field. Changing the crop from here is deliberately not offered: one
            // crop exists, and the species chooser is the inspect pane's recorded hook for the
            // day a second crop gives it something to choose (design 22 §8).
            if (detail.ZonePlant != byte.MaxValue)
            {
                string plant = Registry.Label(BuildLabels.PlantKey(detail.ZonePlant));
                string howMany = detail.ZoneYield > 1 ? plant + " × " + detail.ZoneYield : plant;
                Row(n++, "growing", detail.CropGrowth == ushort.MaxValue
                    ? howMany + " — awaiting its seed"
                    : howMany + " — " + detail.CropGrowth / 10 + "% grown");
            }
            // The store's own answer, beside the field's: what the colony has set this ground
            // aside for, and how much it cares. Its rung leads, because the rung is the thing a
            // player changes and the thing that decides where the next armful goes; the size
            // follows it, because a zone has no name until storage groups arrive (S2) and "how
            // big" is the only other thing that tells two of them apart.
            if (detail.StoreKind != CellDetail.StoreNone)
                // A fact, not a control: the settings are a tab of their own since 2026-09-21, so
                // this row says which rung the store is on and nothing opens from it. Two ways in
                // to one panel is the one a player finds by accident.
                Row(n++, "storage",
                    Registry.Label(StorageSettingsModel.PriorityKeys[detail.StoragePriority]));

            // What a built store is actually holding, which a painted one has no equivalent of:
            // its cells are the board and what is on them is read off the board.
            //
            // **Counted in stacks, not against a unit total.** "400 of 600" was the recorded form
            // and it has no honest denominator: 600 is eight times wood's stack limit, and a shelf
            // full of meals — which stack to twenty — would read "160 of 600" and look nearly
            // empty. Stacks is the thing a shelf actually meters, and the `× n` form carries the
            // amount beside it (docs/design/24-pile-reading.md §2).
            if (detail.StoreKind == CellDetail.StoreShelf)
            {
                string holding;
                if (detail.StoredStacks == 0)
                    holding = "empty — " + detail.StoreSlots + " stacks free";
                else
                {
                    // **Never a bare number.** This said `46 — 3 of 8 stacks` for a store holding
                    // more than one kind, because the single-kind byte is 255 there and the amount
                    // was printed on its own: a quantity of nothing named, which reads as a bug
                    // whichever way you take it. Nor is it summed and labelled, because 400 wood
                    // and 20 meals are not 420 of anything. The kinds themselves are listed on the
                    // Storage tab now, so this line only has to say that there is more than one.
                    //
                    // **It falls back rather than trusting the scan.** `StoreContents` is filled
                    // from the published things, and a caller that hands over a detail row with no
                    // store cell on it — every test that builds one by hand — would otherwise be
                    // told this shelf holds "0 kinds", which is a worse lie than the one being
                    // fixed.
                    string what = detail.StoredDef != 255
                        ? Registry.Label(ItemLabels.IconKey(detail.StoredDef)) + " × " + detail.StoredUnits
                        : StoreContents.Count > 1 ? StoreContents.Count + " kinds" : "mixed";
                    holding = what + " — " + detail.StoredStacks + " of " + detail.StoreSlots + " stacks";
                }

                Row(n++, "holding", holding);
            }

            if (detail.IsIndoors)
                Row(n++, "environment", "indoors");

            // How warm it is here, beside whether it is indoors: the room's air where the cell
            // is in a room, the outdoor curve where it is not — the same number the colonists
            // are feeling on the needs cadence and the crops on the growth one (design 28 §8).
            // Centi-degrees to one decimal, signed, because −12.5 °C and 12.5 °C are different
            // decisions and the pane exists to make the decision obvious. Silent only for a
            // detail that was never told, which in the game never happens.
            if (detail.AmbientTempC != int.MinValue)
                Row(n++, "temperature", TemperatureLabels.Describe(detail.AmbientTempC),
                    HudTheme.Temperature(detail.AmbientTempC));

            Row(n++, "walk speed", detail.MoveCostPerMille == 0
                ? "cannot walk"
                : (100_000 + detail.MoveCostPerMille / 2) / detail.MoveCostPerMille + "%");

            // The floor is said once: as the title when that is what was clicked, as a row when
            // something above it — a tree, an order — is the headline instead.
            bool floorIsTitle = EdificeLabels.Title(detail.Edifice).Length == 0
                && detail.FloorStuff != StuffHandle.None;
            if (detail.FloorStuff != StuffHandle.None && !floorIsTitle)
            {
                string stuff = BuildLabels.Stuff(detail.FloorStuff);
                Row(n++, "floor", stuff.Length == 0 ? "built" : stuff);
            }

            // Support is a solid's own fact — what the column can still bear — and is shown
            // where there is something to dig, which is where a collapse is a question.
            if (detail.WorkToClear > 0)
                Row(n++, "support", detail.Support.ToString());

            while (CellRows.Count > n) CellRows.RemoveAt(CellRows.Count - 1);
        }

        /// <summary>
        /// What is in the store under the pane, gathered by kind, biggest first.
        ///
        /// <para><b>Off the things, not off a second channel.</b> A contained thing is published
        /// in <see cref="WorldSnapshot.Things"/> at its store's own cell carrying its container id
        /// — the decision <c>ThingView.Container</c> records — so the list a player reads and the
        /// totals the Build palette and the stores panel count are the same rows. A contents
        /// channel of its own would be the fifth place to look and the one that drifts.</para>
        ///
        /// <para>Scanned rather than indexed because a store holds at most a handful of kinds and
        /// this runs only while one is selected. Sorted by amount so the thing there is most of is
        /// the thing read first, and ties broken by name so the list does not shuffle under the
        /// pointer as stacks come and go.</para>
        /// </summary>
        void FillStoreContents(WorldSnapshot snapshot, in CellDetail detail)
        {
            StoreContents.Clear();
            if (detail.StoreKind != CellDetail.StoreShelf || detail.StoreCellIndex < 0) return;

            GridSize size = snapshot.Size;
            var things = snapshot.Things;

            for (int i = 0; i < things.Length; i++)
            {
                if (things[i].Container == 0) continue;
                if (size.Index(things[i].Cell) != detail.StoreCellIndex) continue;

                int def = things[i].DefIndex;
                int at = -1;
                for (int row = 0; row < StoreContents.Count; row++)
                    if (StoreContents[row].IconKey == ItemLabels.IconKey(def)) { at = row; break; }

                if (at < 0)
                {
                    StoreContents.Add(new StoreContentRow
                    {
                        IconKey = ItemLabels.IconKey(def),
                        Name = Registry.Label(ItemLabels.IconKey(def)),
                        Units = things[i].Stack,
                        Stacks = 1,
                    });
                    continue;
                }

                StoreContentRow had = StoreContents[at];
                had.Units += things[i].Stack;
                had.Stacks++;
                StoreContents[at] = had;
            }

            StoreContents.Sort(static (a, b) =>
            {
                int byAmount = b.Units.CompareTo(a.Units);
                return byAmount != 0 ? byAmount : string.CompareOrdinal(a.Name, b.Name);
            });
        }

        void Row(int index, string name, string value, HudColour? tint = null)
        {
            while (CellRows.Count <= index) CellRows.Add(new InspectRow());
            InspectRow row = CellRows[index];
            row.Name = name;
            row.Value = value;
            row.Tint = tint;
            CellRows[index] = row;
        }

        /// <summary>
        /// The verb for an order standing on the selected cell. The numbers are
        /// <c>DesignationKind</c>'s — Mine 1, Deconstruct 2, Fell 3 — restated here because the
        /// enum lives in the simulation and <c>OrderView.Kind</c> carries only its value. Fell
        /// reads as chopping, the game's own word for it since the palette stopped saying
        /// "Harvest".
        /// </summary>
        static string OrderVerb(byte kind) => kind switch
        {
            1 => "mining",
            2 => "deconstructing",
            3 => "chopping",
            4 => "picking",
            _ => "working",
        };

        /// <summary>
        /// Fill the Skills tab from the frame.
        ///
        /// <para>The list is written in place rather than cleared and refilled, because it is one
        /// row per skill for as long as a colonist is selected and the pane refreshes fifteen
        /// times a second: clearing a <see cref="List{T}"/> of structs and re-adding does not
        /// allocate, but rebuilding it does churn, and the count never changes. So the rows are
        /// laid down once at the catalogue's length and overwritten after that.</para>
        ///
        /// <para>A tombstoned colonist keeps its last-known rows, like every other field on this
        /// pane: the frame no longer carries the pawn, so the loop below finds nothing for it and
        /// leaves what was there.</para>
        /// </summary>
        void RefreshSkills(WorldSnapshot snapshot)
        {
            // In reading order, not declaration order (owner, 2026-09-17): alphabetical by the word
            // on screen, laid down so the two-column grid reads down the left and then down the
            // right. The same order feeds a candidate's card on the setup page, which is the point
            // — the two grids are looked at side by side and must not disagree.
            IReadOnlyList<SkillCatalogue.Entry> order = SkillCatalogue.ReadingOrder;

            while (Skills.Count < order.Count) Skills.Add(default);

            for (int i = 0; i < order.Count; i++)
            {
                SkillCatalogue.Entry entry = order[i];
                SkillRow row = Skills[i];
                row.IconKey = entry.Key;
                row.Name = Registry.Label(entry.Key);
                row.Live = entry.Live;
                row.Reason = entry.Reason;
                row.Note = entry.Note;
                if (!entry.Live)
                {
                    row.Level = 0;
                    row.Passion = 0;
                    row.Experience = 0;
                    row.Progress = 0;
                }
                Skills[i] = row;
            }

            if (Tombstoned) return;

            // One walk of the published aspects rather than three lookups per row, which is what
            // TryGetPawnAspect's own remarks recommend for a reader that wants every aspect of a
            // pawn: the lookup is a scan, so calling it thirty-nine times would be thirty-nine
            // scans of the same span.
            var published = snapshot.PawnAspects;
            for (int i = 0; i < published.Length; i++)
            {
                PawnAspect aspect = published[i];
                if (aspect.Pawn != Pawn) continue;
                for (int r = 0; r < order.Count; r++)
                {
                    SkillCatalogue.Entry entry = order[r];
                    if (!entry.Live) continue;

                    SkillRow row = Skills[r];
                    if (aspect.Key == entry.Level) row.Level = aspect.Value;
                    else if (aspect.Key == entry.Passion) row.Passion = aspect.Value;
                    else if (aspect.Key == entry.Experience) row.Experience = aspect.Value;
                    else if (aspect.Key == entry.Progress) row.Progress = aspect.Value;
                    else continue;
                    Skills[r] = row;
                }
            }
        }

        void AddColonistTabs()
        {
            Tabs.Add(new InspectTab { Name = "Needs", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Skills", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Gear", Enabled = false, Reason = "equipment arrives with the inventory" });
            // Live since design 51 §5b: what is on her mind, and why she is where she is.
            Tabs.Add(new InspectTab { Name = "Thoughts", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Social", Enabled = false, Reason = "M6" });
            // Live from the combat contracts step (design 33 §5): a colonist can be hurt now. The
            // tab's body is lane C's (HudShell.Combat.cs, CombatFeedbackModel) and is empty until
            // it is written.
            Tabs.Add(new InspectTab { Name = "Health", Enabled = true, Reason = string.Empty });
            Tabs.Add(new InspectTab { Name = "Log", Enabled = false, Reason = "M6" });
        }

        /// <summary>
        /// The draft's key names, one per face of the one button (design 33 §2f). Public so the
        /// shell that draws the button can tell it is the one that does something.
        /// </summary>
        public const string DraftKey = "ui.command.draft", UndraftKey = "ui.command.undraft";

        /// <summary>Whether the colonist on the pane is drafted: which face the Draft button shows.</summary>
        public bool Drafted { get; private set; }

        /// <summary>
        /// The colonist's response to danger (<see cref="ResponseModel"/>, design 33 §18e): which
        /// face the response button shows. A colonist gone from the frame reads as the default,
        /// with the button off.
        /// </summary>
        public int Response { get; private set; }

        void AddColonistCommands(bool drafted, int response)
        {
            Drafted = drafted;
            Response = response;
            Commands.Add(new InspectCommand
            {
                IconKey = "ui.command.inspect", Label = "Inspect",
                Enabled = false, Reason = "the record view arrives with the log (M6)",
            });
            Commands.Add(new InspectCommand
            {
                IconKey = "ui.command.prioritise", Label = "Prioritise",
                Enabled = false, Reason = "job priorities arrive with the work grid (M7)",
            });
            // Live since the draft (design 33 §2f). One button with two faces, as the reference
            // has it: it says what pressing it will do, and a tombstoned colonist has nothing to
            // command.
            string key = drafted ? UndraftKey : DraftKey;
            Commands.Add(new InspectCommand
            {
                IconKey = key, Label = Registry.Label(key),
                Enabled = !Tombstoned,
                Reason = drafted ? "give back to the work list (T)" : "take direct control: right-click to move (T)",
            });
            // Her response to danger (design 33 §18e), beside Draft. A setting rather than an
            // action, so the face is the response she has, and a press moves it round the three.
            string respond = ResponseModel.KeyOf(response);
            Commands.Add(new InspectCommand
            {
                IconKey = respond, Label = Registry.Label(respond),
                Enabled = !Tombstoned,
                Reason = ResponseModel.Describe(response),
            });
        }
    }
}
