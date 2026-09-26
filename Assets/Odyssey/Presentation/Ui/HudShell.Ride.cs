#nullable enable
using Odyssey.Hud;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Presentation.Ui
{
    /// <summary>
    /// Riding along with a colonist (design 57 §6): while a ride runs the whole in-game interface
    /// is put away and one strip along the bottom says who is being watched, what she is doing,
    /// how hurt she is, how fast time is running and how to leave. The owner, 2026-09-26: a minimal
    /// strip.
    ///
    /// <para><b>The in-game interface is hidden by its container</b>, the same one the start screen
    /// hides (<c>_worldUi</c>): half its regions set their own inline display as they come and go,
    /// and hiding the parent is the only switch none of them can argue with. The alerts go on being
    /// watched underneath — their chimes still sound — and the strip shows the first of them.</para>
    /// </summary>
    public sealed partial class HudShell
    {
        VisualElement _rideStrip = null!;
        Label _rideName = null!;
        Label _rideJob = null!;
        VisualElement _rideHealth = null!;
        VisualElement _rideHealthFill = null!;
        Label _rideHealthValue = null!;
        Label _rideSpeed = null!;
        Label _rideAlert = null!;

        /// <summary>Whether the strip is the interface on screen now, as last applied.</summary>
        bool _rideUiShown;

        /// <summary>The registry's word for each game speed, 0 to 3.</summary>
        static readonly string[] SpeedKeys = { "ui.speed.pause", "ui.speed.play", "ui.speed.fast", "ui.speed.ultra" };

        void BuildRide()
        {
            _rideStrip = new VisualElement { name = "ride", pickingMode = PickingMode.Ignore };
            _rideStrip.AddToClassList("ride");
            _rideStrip.style.display = DisplayStyle.None;

            var who = new VisualElement { pickingMode = PickingMode.Ignore };
            who.AddToClassList("ride__who");
            _rideName = HudText.Make(string.Empty, HudTextRole.Name, ussClass: "ride__name");
            _rideJob = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "ride__job");
            who.Add(_rideName);
            who.Add(_rideJob);
            _rideStrip.Add(who);

            var health = new VisualElement { pickingMode = PickingMode.Ignore };
            health.AddToClassList("ride__health");
            _rideHealth = new VisualElement { pickingMode = PickingMode.Ignore };
            _rideHealth.AddToClassList("ride__bar");
            _rideHealthFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _rideHealthFill.AddToClassList("ride__fill");
            _rideHealth.Add(_rideHealthFill);
            _rideHealthValue = HudText.Make(string.Empty, HudTextRole.Meta, numeric: true, ussClass: "ride__value");
            health.Add(_rideHealth);
            health.Add(_rideHealthValue);
            _rideStrip.Add(health);

            _rideSpeed = HudText.Make(string.Empty, HudTextRole.Row, ussClass: "ride__speed");
            _rideStrip.Add(_rideSpeed);

            var leave = new VisualElement { pickingMode = PickingMode.Ignore };
            leave.AddToClassList("ride__leave");
            // Escape is read directly and never rebound (HotkeyDirector: it is not in the closed
            // set), so the cap is written rather than looked up.
            leave.Add(HudText.Make("Esc", HudTextRole.Hotkey, ussClass: "ride__cap"));
            leave.Add(HudText.Make(Registry.Label("ui.command.leaveride"), HudTextRole.Meta));
            _rideStrip.Add(leave);

            _rideAlert = HudText.Make(string.Empty, HudTextRole.Body, ussClass: "ride__alert");
            _rideAlert.style.display = DisplayStyle.None;
            _rideStrip.Add(_rideAlert);

            _hud.Add(_rideStrip);
        }

        /// <summary>
        /// Every frame a world runs: put the in-game interface away when a ride begins and back when
        /// it ends. Cheap on the frames nothing changes: one comparison.
        /// </summary>
        void SyncRideUi()
        {
            bool riding = _directors?.Ride.Riding == true;
            if (riding == _rideUiShown) return;
            _rideUiShown = riding;
            _rideStrip.style.display = riding ? DisplayStyle.Flex : DisplayStyle.None;
            _worldUi.style.display = riding ? DisplayStyle.None : DisplayStyle.Flex;
            // A menu raised at the pointer names things on a board nobody is looking at now.
            if (riding) CloseContextMenu();
            if (riding) RefreshRide();
        }

        /// <summary>A session came or went: whatever a ride had on screen goes, and the next frame decides afresh.</summary>
        void ResetRideUi()
        {
            _rideUiShown = false;
            if (_rideStrip != null) _rideStrip.style.display = DisplayStyle.None;
        }

        /// <summary>The strip's words, on the fast cadence the inspect pane refreshes on.</summary>
        void RefreshRide()
        {
            var world = _boot?.World;
            if (!_rideUiShown || world == null || _directors == null) return;

            RideDirector ride = _directors.Ride;
            ride.RefreshStrip(world.Views.Current);
            InspectModel her = ride.Strip;

            _rideName.text = her.Title;
            _rideJob.text = string.IsNullOrEmpty(her.HealthCondition) ? her.Job : her.Job + "  ·  " + her.HealthCondition;
            _rideStrip.EnableInClassList("ride--lost", her.Tombstoned);

            bool hasHealth = !string.IsNullOrEmpty(her.HealthValue);
            _rideHealth.style.display = hasHealth ? DisplayStyle.Flex : DisplayStyle.None;
            _rideHealthValue.text = her.HealthValue;
            _rideHealthFill.style.width = Length.Percent(Mathf.Clamp(her.HealthPerMille, 0, 1000) / 10f);
            _rideHealthFill.style.backgroundColor = HudTokens.Convert(her.HealthInk);

            int speed = Mathf.Clamp(world.GameSpeed, 0, SpeedKeys.Length - 1);
            _rideSpeed.text = Registry.Label(SpeedKeys[speed]);

            bool alert = _alerts.Rows.Count > 0;
            _rideAlert.style.display = alert ? DisplayStyle.Flex : DisplayStyle.None;
            if (alert) _rideAlert.text = _alerts.Rows[0].Lead;
        }
    }
}
