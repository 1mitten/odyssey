#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One roster card's content (A2 in the panel catalogue): identity, mood, current activity,
    /// and which layer they are on. The layer is not decoration — on a layered map the roster
    /// bar is the player's primary answer to "where is everyone".
    /// </summary>
    public struct RosterCard
    {
        public PawnId Id;
        /// <summary>
        /// The seed this colonist was rolled from — the other half of who they are.
        ///
        /// <para><b>An id alone does not identify a person across games</b>, and the roster bar is
        /// the one place that matters, because its cards are slots that re-read themselves only
        /// when the colonist in them changes. Every colony numbers its pawns from one, so slot 0
        /// holds <c>PawnId(1)</c> in every game there has ever been; load a different colony and
        /// the id has not changed while everything derived from the seed — the name, the face, the
        /// age, the occupation — has. Published here so the decision is made against the model
        /// rather than by each view reaching back into the frame for it.</para>
        ///
        /// <para>The owner met it twice. First as blank portraits on start and load, fixed for the
        /// picture alone with a generation counter; then as <i>"the colonist info card and the
        /// roster top bar names don't match up"</i> (2026-09-18), which is the same fault wearing
        /// the name and the face instead. The inspect pane reads afresh every time, which is why it
        /// is always the one telling the truth and why this keeps looking like a roster problem.</para>
        /// </summary>
        public uint Seed;
        public string Name;
        public int Mood;        // 0..1000, the simulation's own scale, as food and rest are

        /// <summary>The <see cref="MoodBand"/> the simulation published for her (design 43 §5a).</summary>
        public int MoodBand;
        public int Food;        // 0..1000
        public int Rest;        // 0..1000
        public int JobDef;      // JobHandle value, -1 idle
        public int Layer;       // Cell.Y
        public bool Selected;

        /// <summary>
        /// The health bar's fill, 0 to 1000 (design 33 §9f): hit points over the pool, clamped, as
        /// the bar over her head reads them. <b>−1 when the frame publishes no pool</b>, which is a
        /// frame from before combat or one built by hand: the card then draws the empty track and
        /// no fill, rather than claiming a colonist it knows nothing about is whole or dying.
        /// Nought while downed, whatever the hit points below nought say.
        /// </summary>
        public int Health;

        /// <summary>The bar's ink: <see cref="CombatFeedbackModel.HealthBarColour"/>, red while downed.</summary>
        public HudColour HealthInk;

        /// <summary>Down and not dead (<see cref="PawnView.IsDowned"/>): the card says so over an empty red bar.</summary>
        public bool Downed;

        /// <summary>The word over the bar: <c>ui.status.downed</c> while <see cref="Downed"/>, else empty.</summary>
        public string HealthWord;
    }

    /// <summary>
    /// Builds the roster from a published frame. Rows are written into a reused list, so a
    /// refresh costs no allocation once the colony stops growing — the interface's steady-state
    /// rule, applied from the first version rather than retrofitted.
    ///
    /// Supports pagination across pages of cards and persistent player-directed slot ordering.
    /// </summary>
    public sealed class RosterModel
    {
        public readonly List<RosterCard> Cards = new List<RosterCard>();
        readonly List<PawnId> _customOrder = new List<PawnId>();

        public IReadOnlyList<PawnId> CustomOrder => _customOrder;

        public int Page { get; private set; }
        public int PageCapacity { get; set; } = int.MaxValue;
        public int TotalCount => _customOrder.Count;
        public int PageCount => PageCapacity <= 0 || TotalCount <= 0 ? 1 : Math.Max(1, (TotalCount + PageCapacity - 1) / PageCapacity);

        public void SetPage(int page)
        {
            int maxPage = Math.Max(0, PageCount - 1);
            Page = Math.Max(0, Math.Min(page, maxPage));
        }

        public bool EnsurePageFor(PawnId pawnId)
        {
            if (!pawnId.IsValid) return false;
            int idx = _customOrder.IndexOf(pawnId);
            if (idx < 0) return false;
            int targetPage = PageCapacity > 0 ? idx / PageCapacity : 0;
            SetPage(targetPage);
            return true;
        }

        public bool Swap(PawnId a, PawnId b)
        {
            if (a == b || !a.IsValid || !b.IsValid) return false;
            int idxA = _customOrder.IndexOf(a);
            int idxB = _customOrder.IndexOf(b);
            if (idxA < 0 || idxB < 0) return false;
            _customOrder[idxA] = b;
            _customOrder[idxB] = a;
            return true;
        }

        public bool SwapIndices(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= _customOrder.Count || indexB < 0 || indexB >= _customOrder.Count || indexA == indexB)
                return false;
            PawnId tmp = _customOrder[indexA];
            _customOrder[indexA] = _customOrder[indexB];
            _customOrder[indexB] = tmp;
            return true;
        }

        public void LoadOrder(IEnumerable<PawnId> order, int page = 0)
        {
            _customOrder.Clear();
            foreach (PawnId id in order)
            {
                if (id.IsValid && !_customOrder.Contains(id))
                    _customOrder.Add(id);
            }
            SetPage(page);
        }

        public void Refresh(WorldSnapshot snapshot, PawnId selected, int capacity = int.MaxValue) =>
            Refresh(snapshot, selected.IsValid ? new[] { selected } : Array.Empty<PawnId>(), capacity);

        /// <summary>
        /// Marks a card selected for every member of a multi-selection, so the roster bar answers
        /// a drag box on the world the same frame the brackets do.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId> selected, int capacity = int.MaxValue)
        {
            PageCapacity = Math.Max(1, capacity);

            // Reconcile custom order: remove dead/despawned pawns, append new pawns.
            for (int i = _customOrder.Count - 1; i >= 0; i--)
            {
                if (!snapshot.TryGetPawn(_customOrder[i], out _))
                    _customOrder.RemoveAt(i);
            }

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                // The roster is the colony's people (design 29 §2): an animal has no card, no
                // name and no slot to be dragged into, so it never enters the order at all. Nor
                // has a bandit, which is a person and not ours (design 33 §5): asked of the
                // flags, never of the kind.
                if (!pawns[i].IsColonist) continue;
                PawnId id = pawns[i].Id;
                if (!_customOrder.Contains(id))
                    _customOrder.Add(id);
            }

            // Clamp active page
            SetPage(Page);

            Cards.Clear();
            int start = Page * PageCapacity;
            int end = Math.Min(start + PageCapacity, _customOrder.Count);

            for (int i = start; i < end; i++)
            {
                PawnId id = _customOrder[i];
                if (!snapshot.TryGetPawn(id, out PawnView pawn)) continue;

                bool isSelected = false;
                for (int s = 0; s < selected.Count; s++)
                {
                    if (selected[s] == id) { isSelected = true; break; }
                }

                // Read once and used twice: the name is a function of it, and the view keys the
                // slot on it. Two readings of one thing is how the bar and the card came to
                // disagree in the first place.
                uint seed = ColonistNames.RollSeedOf(snapshot, pawn.Id);
                Health(snapshot, pawn, out int health, out HudColour healthInk, out bool downed);

                Cards.Add(new RosterCard
                {
                    Id = pawn.Id,
                    Seed = seed,
                    Name = ColonistNames.Of(seed, pawn.Id),
                    Mood = pawn.Mood,
                    MoodBand = MoodBands.Of(snapshot, pawn.Id),
                    Food = pawn.Food,
                    Rest = pawn.Rest,
                    JobDef = pawn.JobDef,
                    Layer = pawn.Cell.Y,
                    Selected = isSelected,
                    Health = health,
                    HealthInk = healthInk,
                    Downed = downed,
                    HealthWord = downed ? Registry.Label(CombatFeedbackModel.DownedKey) : string.Empty,
                });
            }
        }

        /// <summary>
        /// A card's health bar (design 33 §9f, owner: <i>"Their health needs to be also displayed
        /// on their colony stats as it appears above them"</i>). <b>Always drawn</b>, where the bar
        /// over the head is drawn only while <c>odyssey.pawn.hp</c> is published, so it reads the
        /// same two aspects with the Health tab's rule for the gap between them: the pool,
        /// <c>odyssey.pawn.hp.max</c>, is published for every person always, and <b>a pool with no
        /// hit points beside it is a whole colonist</b> (design 33 §5d). Nothing here is derived
        /// that the simulation already says.
        ///
        /// <para>The ink is <see cref="CombatFeedbackModel.HealthBarColour"/>, the one owner of the
        /// bar's colours, so the card and the bar over her head change colour on the same hit.
        /// Downed is the flag's, not a reading of the hit points: an empty bar in the red, whatever
        /// is left of the −50 % a downed pawn may sink to before it dies.</para>
        ///
        /// <para>Two O(1) aspect lookups and a flag test per card per refresh; no allocation.</para>
        /// </summary>
        public static void Health(WorldSnapshot snapshot, in PawnView pawn, out int perMille, out HudColour ink, out bool downed)
        {
            downed = pawn.IsDowned;
            if (downed)
            {
                perMille = 0;
                ink = CombatFeedbackModel.HealthBad;
                return;
            }

            if (!snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.HpMaxKey, out int max) || max <= 0)
            {
                perMille = -1;
                ink = CombatFeedbackModel.HealthGood;
                return;
            }

            int hp = snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.HpKey, out int published) ? published : max;
            int shown = hp < 0 ? 0 : hp > max ? max : hp;
            perMille = (int)((long)shown * 1000 / max);
            ink = CombatFeedbackModel.HealthBarColour(shown, max);
        }
    }

    /// <summary>
    /// The mood band's words, read from the band <b>the simulation publishes</b> (design 43 §5a).
    ///
    /// <para><b>This class used to hold two thresholds of its own</b>, 600 and 350, the second a
    /// copy of the simulation's break threshold under a comment saying the threshold "is not
    /// published". So a colonist at the resting target of 500 — fed, rested, nothing on her mind —
    /// read <i>strained</i> for the whole game, and the owner asked for that to stop. The lines
    /// move with traits now, which only the simulation knows, so the band is its answer and this
    /// class keeps no number at all.</para>
    /// </summary>
    public static class MoodBands
    {
        /// <summary>
        /// The band published for <paramref name="pawn"/>, or <see cref="MoodBand.Content"/> when the
        /// frame carries none — a frame built by hand, or one for a pawn whose mood does not move.
        /// Content rather than a guess from the mood: guessing is the copied threshold again.
        /// </summary>
        public static int Of(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawnAspect(pawn, MindAspectNames.BandKey, out int band)
                && band >= 0 && band < MoodBand.Count
                ? band
                : MoodBand.Content;

        /// <summary>The registry key that names a band.</summary>
        public static string KeyOf(int band) =>
            band == MoodBand.Content ? "ui.mood.content"
            : band == MoodBand.Strained ? "ui.mood.strained"
            : band == MoodBand.Broken ? "ui.mood.broken"
            : "ui.mood.breaking";

        static string[]? _words;

        /// <summary>
        /// The band as a word inside a sentence ("mood content"): the registry's label in lower
        /// case, built once, so a pane refreshing fifteen times a second allocates nothing.
        /// </summary>
        public static string Word(int band)
        {
            if (_words == null)
            {
                var words = new string[MoodBand.Count];
                for (int b = 0; b < words.Length; b++)
                    words[b] = Registry.Label(KeyOf(b)).ToLowerInvariant();
                _words = words;
            }
            return band >= 0 && band < MoodBand.Count ? _words[band] : _words[MoodBand.Content];
        }
    }
}
