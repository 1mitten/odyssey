#nullable enable
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// The inspect pane's host for the bill list (design 48 §5, design 49). The list itself is
    /// <see cref="BillList"/>, a control of its own so that every station that takes bills — the
    /// electric cooker, the campfire, a crafting bench later — draws the same one; this only builds
    /// it under a tile's header and refreshes its model from the frame.
    /// </summary>
    public sealed partial class HudShell
    {
        readonly BillsModel _bills = new BillsModel();

        BillList? _billList;

        /// <summary>
        /// Build the list under a tile's header, once per subject. Hidden for anything that is not
        /// a station, and shown by <see cref="SyncBills"/> the moment the answer says one stands
        /// there. It runs to the pane's edges: its strips are bands across the pane, not boxes in
        /// its padding.
        /// </summary>
        void BuildBills(VisualElement into)
        {
            _billList = new BillList(_bills, SendBill);
            _billList.style.marginLeft = -HudLayout.Pad;
            _billList.style.marginRight = -HudLayout.Pad;
            _billList.style.marginTop = HudLayout.Pad;
            _billList.style.marginBottom = 6;
            _billList.style.display = DisplayStyle.None;
            into.Add(_billList);
        }

        /// <summary>Bring the list to what the frame says. The pane refreshes fifteen times a second; the list rewrites words only when its model moves.</summary>
        void SyncBills(WorldSnapshot frame)
        {
            if (_billList == null) return;
            _bills.Refresh(frame, _inspect.Cell, _inspect.TileCellIndex, _inspect.TileEdifice);
            _billList.Sync();
        }

        /// <summary>An intent like every command, applied at once while paused (design 48 §5).</summary>
        void SendBill(Intent intent) => _boot?.World?.Intents.Submit(intent);
    }
}
