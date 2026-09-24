#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>How a left press on a roster card ended (<see cref="RosterSweep.Release"/>).</summary>
    public enum RosterSweepEnd
    {
        /// <summary>No sweep was under way.</summary>
        None,

        /// <summary>
        /// The press never covered another card: a click. Without Shift it is the roster's old
        /// click — choose her, put the slice on her layer and take the camera to her.
        /// </summary>
        Clicked,

        /// <summary>The press was dragged across other cards: a multi-selection, and no jump.</summary>
        Swept,
    }

    /// <summary>
    /// <b>Dragging across the roster's cards selects every colonist passed over</b> (design 33 §20;
    /// owner, 2026-09-24: <i>"when I'm in default mode and I want to select many colonists, I
    /// should be drag the across their roster profile and select them all"</i>) — the roster's
    /// answer to the box the world already draws, and the catalogue's A2 "drag-select a range"
    /// (<c>10-ui-panel-catalogue.md</c>).
    ///
    /// <para><b>The rule.</b> A left press on a card starts a sweep. It covers the cards from the
    /// one pressed to the one under the pointer <b>in slot order, inclusive</b> — a range, as the
    /// world's box is an area: dragged back, it lets go of the cards it no longer spans, and a
    /// quick flick that never lands on the cards in between still covers them, since the strip is
    /// always one row (<c>HudLayout.StripRowsAllowed</c>).</para>
    ///
    /// <list type="bullet">
    /// <item><b>Without Shift</b> the covered cards <i>are</i> the selection, the pressed one first
    /// so the inspect pane shows whom the drag began on.</item>
    /// <item><b>With Shift</b> they are added to the selection held at the press, which is kept in
    /// its order ahead of them.</item>
    /// <item><b>A press that never covers another card is a click</b>: without Shift, that one
    /// colonist; with Shift, she is toggled in or out, the strip's and the world's Shift-click
    /// (<see cref="SelectionDirector.Toggle"/>). Once a sweep has covered a second card it is a
    /// sweep for good, so dragging back on to the pressed card leaves the covered one selected
    /// rather than toggling her out.</item>
    /// </list>
    ///
    /// <para><b>Only the page the press was on.</b> The page's cards are copied at the press, and
    /// a card not among them is not covered; dragging off the end of the strip does not page.
    /// <b>Right-drag is not a sweep</b>: it still reorders the cards (roster paging's slot swap),
    /// and never reaches here.</para>
    ///
    /// <para>Unity-free so the fast tier owns the rule (<c>RosterSweepTests</c>): the view only
    /// reports a press, the card under the pointer and the release, and writes
    /// <see cref="Selection"/> to the director whenever <see cref="Over"/> says it moved.</para>
    /// </summary>
    public sealed class RosterSweep
    {
        readonly List<PawnId> _page = new List<PawnId>();
        readonly List<PawnId> _base = new List<PawnId>();
        readonly List<PawnId> _selection = new List<PawnId>();
        int _pressed = -1, _over = -1;
        bool _shift;

        /// <summary>A left press is held and started on a card.</summary>
        public bool Active { get; private set; }

        /// <summary>The sweep has covered a card other than the one pressed, so it is not a click.</summary>
        public bool Dragged { get; private set; }

        /// <summary>The card the press began on, or none.</summary>
        public PawnId Pressed => _pressed >= 0 ? _page[_pressed] : PawnId.None;

        /// <summary>Whether Shift was held at the press, which decides the whole sweep.</summary>
        public bool Additive => _shift;

        /// <summary>The selection the sweep asks for now, in order: primary first.</summary>
        public IReadOnlyList<PawnId> Selection => _selection;

        /// <summary>
        /// A left press on <paramref name="card"/>, on a page showing <paramref name="page"/> in slot
        /// order, with <paramref name="selection"/> already held. False, and nothing started, for a
        /// card that is not on the page.
        /// </summary>
        public bool Press(IReadOnlyList<PawnId> page, PawnId card, bool shift, IReadOnlyList<PawnId> selection)
        {
            Active = false;
            Dragged = false;
            _page.Clear();
            for (int i = 0; i < page.Count; i++) _page.Add(page[i]);
            _pressed = _page.IndexOf(card);
            _over = _pressed;
            if (_pressed < 0 || !card.IsValid)
            {
                _pressed = -1;
                _selection.Clear();
                return false;
            }

            _shift = shift;
            _base.Clear();
            if (shift)
                for (int i = 0; i < selection.Count; i++) _base.Add(selection[i]);

            Active = true;
            Compose();
            return true;
        }

        /// <summary>
        /// The pointer is over <paramref name="card"/>. True when that moved the covered range —
        /// the one time the view has a selection to write. A card off the pressed page, or any
        /// card while no sweep is held, changes nothing.
        /// </summary>
        public bool Over(PawnId card)
        {
            if (!Active) return false;
            int index = _page.IndexOf(card);
            if (index < 0 || index == _over) return false;
            _over = index;
            if (index != _pressed) Dragged = true;
            Compose();
            return true;
        }

        /// <summary>The button came up, wherever the pointer was: what the press turned out to be.</summary>
        public RosterSweepEnd Release()
        {
            if (!Active) return RosterSweepEnd.None;
            Active = false;
            return Dragged ? RosterSweepEnd.Swept : RosterSweepEnd.Clicked;
        }

        /// <summary>Drop the sweep without an outcome: a new session, the strip rebuilt under it.</summary>
        public void Cancel()
        {
            Active = false;
            Dragged = false;
        }

        void Compose()
        {
            _selection.Clear();
            PawnId pressed = _page[_pressed];

            if (!Dragged)
            {
                if (!_shift)
                {
                    _selection.Add(pressed);
                    return;
                }

                // Shift-click: toggled, in or out.
                bool had = false;
                for (int i = 0; i < _base.Count; i++)
                {
                    if (_base[i] == pressed) { had = true; continue; }
                    _selection.Add(_base[i]);
                }
                if (!had) _selection.Add(pressed);
                return;
            }

            for (int i = 0; i < _base.Count; i++) Add(_base[i]);
            Add(pressed);
            int from = _pressed < _over ? _pressed : _over;
            int to = _pressed < _over ? _over : _pressed;
            for (int i = from; i <= to; i++) Add(_page[i]);
        }

        void Add(PawnId pawn)
        {
            if (!pawn.IsValid || _selection.Contains(pawn)) return;
            _selection.Add(pawn);
        }
    }
}
