#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>How loudly an alert is drawn, and which of the palette's signal colours it takes.</summary>
    public enum AlertSeverity
    {
        /// <summary>Something worth knowing. Info blue, an "i" in a circle.</summary>
        Notice,

        /// <summary>Something that will become a problem. Warn amber, a triangle.</summary>
        Warning,

        /// <summary>Something that is a problem now. Bad red, a triangle.</summary>
        Danger,
    }

    /// <summary>
    /// One line of the alerts panel, already split the way the spec asks it to be drawn:
    /// <b>the actionable clause first, the detail trailing in dimmer ink</b> — "No stockpile —
    /// <i>salvage lies where it fell</i>". A player scanning the panel should be able to read only
    /// the leads and know what to do.
    /// </summary>
    public readonly struct AlertRow
    {
        /// <summary>The symbolic key naming the condition, for the registry and later for art.</summary>
        public readonly string Key;

        /// <summary>The highlighted target name (e.g. "Wrenn" or "Colony").</summary>
        public readonly string TargetName;

        /// <summary>The message trailing the target (e.g. " is close to breaking").</summary>
        public readonly string TargetSuffix;

        /// <summary>Optional prefix before the target, if any.</summary>
        public readonly string TargetPrefix;

        /// <summary>The complete actionable lead string.</summary>
        public readonly string Lead;

        /// <summary>The qualifying detail, dimmed.</summary>
        public readonly string Detail;

        public readonly AlertSeverity Severity;

        /// <summary>How many subjects the alert covers, for a test and for a tooltip.</summary>
        public readonly int Count;

        /// <summary>The target pawn if this alert relates to a colonist, or None.</summary>
        public readonly PawnId Pawn;

        /// <summary>The target cell if this alert relates to a map location, or None.</summary>
        public readonly CellRef? Cell;

        /// <summary>Stable token identifying this alert for dismissal.</summary>
        public readonly int DismissKey;

        public AlertRow(
            string key,
            string targetName,
            string targetSuffix,
            AlertSeverity severity,
            int count = 1,
            PawnId pawn = default,
            CellRef? cell = null,
            string targetPrefix = "",
            string detail = "",
            bool dismissByKey = false,
            int identity = 0)
        {
            Key = key;
            TargetName = targetName;
            TargetSuffix = targetSuffix;
            TargetPrefix = targetPrefix;
            Lead = string.IsNullOrEmpty(targetPrefix) ? targetName + targetSuffix : targetPrefix + targetName + targetSuffix;
            Detail = detail;
            Severity = severity;
            Count = count;
            Pawn = pawn;
            Cell = cell;
            // A row whose cell follows something moving — a raid's centre — is dismissed by its key,
            // or a dismiss would last only until the band took a step; and by the identity of what
            // it is about (the band's id), or dismissing one raid would dismiss the next.
            DismissKey = dismissByKey
                ? ComputeDismissKey(key, new PawnId(identity), null)
                : ComputeDismissKey(key, pawn, cell);
        }

        public AlertRow(string key, string lead, string detail, AlertSeverity severity, int count)
        {
            Key = key;
            TargetName = lead;
            TargetSuffix = string.Empty;
            TargetPrefix = string.Empty;
            Lead = lead;
            Detail = detail;
            Severity = severity;
            Count = count;
            Pawn = default;
            Cell = null;
            DismissKey = ComputeDismissKey(key, default, null);
        }

        public static int ComputeDismissKey(string key, PawnId pawn, CellRef? cell)
        {
            unchecked
            {
                int hash = (key != null ? key.GetHashCode() : 0) * 397;
                hash = (hash * 397) ^ pawn.Value;
                if (cell.HasValue)
                {
                    hash = (hash * 397) ^ cell.Value.X;
                    hash = (hash * 397) ^ (cell.Value.Y << 10);
                    hash = (hash * 397) ^ (cell.Value.Z << 20);
                }
                return hash;
            }
        }
    }

    /// <summary>
    /// What the alerts panel says, read off the published frame and nothing else.
    /// </summary>
    public sealed class AlertModel
    {
        /// <summary>Food at or under which a colonist counts as starving, in thousandths.</summary>
        public const int StarveAt = 120;

        /// <summary>Food a colonist must climb back to before the starving alert can clear.</summary>
        public const int StarveClearAt = 300;

        /// <summary>Mood at or under which a colonist counts as close to breaking.</summary>
        public const int BreakAt = MoodBands.Strained;

        /// <summary>Mood a colonist must climb back to before the breaking alert can clear.</summary>
        public const int BreakClearAt = MoodBands.Content;

        /// <summary>Seconds a colony must be idle before the panel says so.</summary>
        public const double IdleSustain = 3.0;

        public const string StarveKey = "ui.alert.starvation";
        public const string BreakKey = "ui.alert.mentalbreak";
        public const string IdleKey = "ui.alert.idle";
        public const string StoreStuckKey = "ui.alert.storagestuck";

        /// <summary>A net has gone dark with something on it wanting power (design 32 §10).</summary>
        public const string PowerLossKey = "ui.alert.powerloss";

        /// <summary>A switched-on generator is empty and its net wants power.</summary>
        public const string NoFuelKey = "ui.alert.nofuel";

        /// <summary>
        /// A downed colonist lying where she fell with no free bed to be carried to (design 33
        /// §11d): the owner's "leave her, and say why". Read off the simulation's
        /// <c>odyssey.pawn.rescue.nobed</c>, one row a colonist, a click on it goes to her.
        /// </summary>
        public const string NoRescueBedKey = "ui.alert.norescuebed";

        /// <summary>
        /// Somebody the colony means to hold is lying downed with no free prison bed to be carried
        /// to (design 58 §7): a capture waiting, or a prisoner brought down outside her cell. Read off
        /// the simulation's <c>odyssey.pawn.prison.nobed</c>; one row a pawn, a click goes to her.
        /// </summary>
        public const string NoPrisonBedKey = "ui.alert.noprisonbed";

        /// <summary>
        /// Somebody is kept home and there is no hearth, so home does not exist and keeps nobody
        /// (design 43 §3f, §4d). Only while it matters: with nobody kept home, no hearth is simply
        /// a colony that has not built a fire.
        /// </summary>
        public const string NoHearthKey = "ui.alert.nohearth";

        /// <summary>A deconstruct order stands on the hearth: home goes when it comes down (§3f's warning).</summary>
        public const string HearthDownKey = "ui.alert.hearthdown";

        /// <summary>
        /// Seconds a store must be marked for removal and still full before the panel says so.
        ///
        /// <para>Longer than the idle latch because the ordinary case looks identical for a while:
        /// a shelf ordered taken apart is full until a hauler has walked to it, and telling the
        /// player it is stuck while somebody is on their way to empty it would be crying wolf. Ten
        /// seconds is long enough for a colonist to cross a room and short enough that a player who
        /// walks away and comes back finds the reason waiting.</para>
        /// </summary>
        public const double StoreStuckSustain = 10.0;

        /// <summary>
        /// A colonist with an injury nobody has tended who is waiting on a doctor (design 43 §11,
        /// §15): Danger while it bleeds, with the hours the bleed leaves her; Warning while she lies
        /// downed with it. One row a colonist; a click goes to her. A colonist on her feet with a
        /// bruise is not news: the doctor's round leaves her to bed rest (design 37 §4).
        /// </summary>
        public const string InjuredKey = "ui.alert.injured";

        /// <summary>
        /// Somebody needs tending and there are no medical supplies anywhere on the board (design 43
        /// §5, design 37): the doctor dresses the wound bare, for less heal and a worse tend.
        /// </summary>
        public const string NoMedicineKey = "ui.alert.nomedicine";

        /// <summary>
        /// A raid is assaulting (design 55 §7): Danger, the standing raiders counted, a click going to
        /// the middle of them. Raised from the assault, not the arrival — a band gathering at the edge
        /// is the Events row's news, and the alert is the condition to act on. Its chime is the assault
        /// horn, through the override the sound library has carried since 2026-09-19.
        /// </summary>
        public const string RaidKey = "ui.alert.raid";

        /// <summary>
        /// The order kind a deconstruction is published as (<c>DesignationKind.Deconstruct</c>, 2).
        /// Restated here as <c>InspectModel.OrderVerb</c> and <c>OrderColours.ToolOf</c> restate it,
        /// because this assembly cannot see the simulation's enum.
        /// </summary>
        public const byte DeconstructOrderKind = 2;

        /// <summary>Every key this panel can put on screen, for the registry test.</summary>
        public static readonly string[] IconKeys = { StarveKey, BreakKey, IdleKey, StoreStuckKey, PowerLossKey, NoFuelKey, NoRescueBedKey, NoHearthKey, HearthDownKey, InjuredKey, NoMedicineKey, RaidKey, NoPrisonBedKey };

        public readonly List<AlertRow> Rows = new List<AlertRow>();

        readonly HashSet<int> _starving = new HashSet<int>();
        readonly HashSet<int> _breaking = new HashSet<int>();
        readonly HashSet<int> _dismissed = new HashSet<int>();

        double _idleSince = -1.0;

        /// <summary>When the first store that is being emptied and is not empty was seen.</summary>
        double _storeStuckSince = -1.0;
        bool _wasStoreStuck;

        int _wasDark = -1;
        int _wasShortW = -1;
        int _wasDry = -1;
        int _wasStarving = -1;
        int _wasBreaking = -1;
        bool _wasIdle;
        int _wasColony = -1;
        int _wasNoBed;
        int _wasNoPrisonBed;
        long _wasNoPrisonBedIds;
        // What the two hearth rows say, not only whether they are up: the count kept home while
        // there is no hearth (0 when the row is down), and the cell ordered down (-1 when none).
        int _wasNoHearth;
        int _wasHearthDown = -1;
        int _hearthDownDismissKey;
        long _wasNoBedIds;
        int _wasInjured = -1;
        long _wasInjuredIds;
        int _wasNoMedicine = -1;
        int _wasRaid = -1;
        int _wasRaidId;

        /// <summary>The dismiss key of the band the Raid row is about, so it can be forgotten when that band stops.</summary>
        int _raidDismiss;
        int _latchVersion;
        int _wasLatchVersion = -1;
        int _dismissVersion;
        int _wasDismissVersion = -1;

        /// <summary>
        /// Has this pawn an injury nobody has tended? Read off the body's sparse aspects: the
        /// records counted against the tended. <paramref name="hours"/> is the bleed's, or nought.
        /// </summary>
        static bool Untended(WorldSnapshot snapshot, in PawnView pawn, out int hours)
        {
            hours = 0;
            if (!snapshot.TryGetPawnAspect(pawn.Id, HealthAspectNames.InjuriesKey, out int injuries) || injuries <= 0) return false;
            snapshot.TryGetPawnAspect(pawn.Id, HealthAspectNames.TendedKey, out int tended);
            if (tended >= injuries) return false;
            snapshot.TryGetPawnAspect(pawn.Id, HealthAspectNames.BleedHoursKey, out hours);
            return hours > 0 || pawn.IsDowned;
        }

        /// <summary>Dismiss an active alert until its condition clears and re-occurs.</summary>
        public void Dismiss(int dismissKey)
        {
            if (_dismissed.Add(dismissKey))
            {
                _dismissVersion++;
                for (int i = 0; i < Rows.Count; i++)
                {
                    if (Rows[i].DismissKey == dismissKey)
                    {
                        Rows.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>Dismiss all currently visible alerts.</summary>
        public void DismissAll()
        {
            if (Rows.Count == 0) return;
            for (int i = 0; i < Rows.Count; i++)
                _dismissed.Add(Rows[i].DismissKey);
            Rows.Clear();
            _dismissVersion++;
        }

        public bool IsDismissed(int dismissKey) => _dismissed.Contains(dismissKey);

        /// <summary>
        /// Recompute from a frame. <paramref name="seconds"/> is wall-clock, not ticks.
        /// </summary>
        public void Refresh(WorldSnapshot snapshot, double seconds)
        {
            int starving = 0;
            int breaking = 0;
            int idle = 0;
            int noBed = 0;
            long noBedIds = 0;
            int injured = 0;
            long injuredIds = 0;
            int keptHome = 0;
            int noPrisonBed = 0;
            long noPrisonBedIds = 0;

            // Colonists only (design 33 §5d): these are the colony's alerts, and a bandit or an
            // animal is neither hungry on the colony's account nor part of whether it is idle. A
            // bandit's needs never move at all (design 33 §5c). Read from the flags, which a view
            // built without them derives from the kind as it always did.
            int colonists = 0;
            PawnId onlyColonist = default;

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                // Not a colonist's alert, so before the colonists-only gate: the pawn waiting for a
                // prison bed is a bandit, or a prisoner (design 58 §7).
                if (!pawn.IsColonist && snapshot.TryGetPawnAspect(pawn.Id, PrisonAspectNames.NoBedKey, out _))
                {
                    noPrisonBed++;
                    noPrisonBedIds = noPrisonBedIds * 31 + pawn.Id.Value;
                }
                if (!pawn.IsColonist) continue;
                colonists++;
                onlyColonist = pawn.Id;
                int id = pawn.Id.Value;

                if (Latch(_starving, id, pawn.Food, StarveAt, StarveClearAt, ref _latchVersion))
                {
                    starving++;
                }
                else
                {
                    _dismissed.Remove(AlertRow.ComputeDismissKey(StarveKey, pawn.Id, default));
                }

                if (Latch(_breaking, id, pawn.Mood, BreakAt, BreakClearAt, ref _latchVersion))
                {
                    breaking++;
                }
                else
                {
                    _dismissed.Remove(AlertRow.ComputeDismissKey(BreakKey, pawn.Id, default));
                }

                if (pawn.JobDef < 0) idle++;

                if (snapshot.TryGetPawnAspect(pawn.Id, AreaAspectNames.AreaKey, out int area) && area != 0) keptHome++;

                if (snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.RescueNoBedKey, out _))
                {
                    noBed++;
                    noBedIds = noBedIds * 31 + id;
                }
                else _dismissed.Remove(AlertRow.ComputeDismissKey(NoRescueBedKey, pawn.Id, default));

                // Untended injuries (design 43 §11), read off the body's sparse aspects: a colonist
                // nobody has hurt publishes none and costs one lookup. The bleed's hours are in the
                // hash so a row that counts down is rewritten when its number moves.
                if (Untended(snapshot, pawn, out int hours))
                {
                    injured++;
                    injuredIds = injuredIds * 31 + id * 1_000 + hours;
                }
                else _dismissed.Remove(AlertRow.ComputeDismissKey(InjuredKey, pawn.Id, default));
            }

            // No medical supplies anywhere on the board while somebody needs them (design 43 §5, design 37).
            int noMedicine = 0;
            if (injured > 0)
            {
                noMedicine = 1;
                System.ReadOnlySpan<ThingView> things = snapshot.Things;
                for (int i = 0; i < things.Length; i++)
                    if (things[i].DefIndex == ItemHandle.MedicalSupplies) { noMedicine = 0; break; }
            }
            if (noMedicine == 0) _dismissed.Remove(AlertRow.ComputeDismissKey(NoMedicineKey, default, default));

            Forget(_starving, snapshot, ref _latchVersion);
            Forget(_breaking, snapshot, ref _latchVersion);

            if (idle > 0 && idle == colonists)
            {
                if (_idleSince < 0.0) _idleSince = seconds;
            }
            else
            {
                _idleSince = -1.0;
                _dismissed.Remove(AlertRow.ComputeDismissKey(IdleKey, default, default));
                for (int i = 0; i < pawns.Length; i++)
                    _dismissed.Remove(AlertRow.ComputeDismissKey(IdleKey, pawns[i].Id, default));
            }

            bool idleStands = _idleSince >= 0.0 && seconds - _idleSince >= IdleSustain;

            // A store that has been told to come apart and still has something in it. The
            // simulation publishes the state and this decides when it is news, exactly as it does
            // for an idle colony: a wall-clock rule has no business inside a fixed-tick tick.
            int stuck = 0;
            System.ReadOnlySpan<StorageUnitView> stores = snapshot.StorageUnits;
            for (int i = 0; i < stores.Length; i++)
                if (stores[i].Emptying && stores[i].Stacks > 0) stuck++;

            if (stuck > 0)
            {
                if (_storeStuckSince < 0.0) _storeStuckSince = seconds;
            }
            else _storeStuckSince = -1.0;

            bool storeStuck = _storeStuckSince >= 0.0 && seconds - _storeStuckSince >= StoreStuckSustain;

            // Power (design 32 §10). No sustain on either: a dark net is a fact on the frame it
            // happens, and the whole-net rule means it does not flicker — demand counts what is
            // switched on, not what is powered, so a dark net stays dark until something changes.
            int dark = 0, shortW = 0, dry = 0;
            int darkCell = -1, dryCell = -1;
            System.ReadOnlySpan<PowerNetView> nets = snapshot.PowerNets;
            for (int i = 0; i < nets.Length; i++)
            {
                if (nets[i].State != PowerNetState.Dark) continue;
                dark++;
                shortW += nets[i].DemandW - nets[i].SupplyW;
            }
            System.ReadOnlySpan<PowerDeviceView> devices = snapshot.PowerDevices;
            for (int i = 0; i < devices.Length; i++)
            {
                PowerDeviceView d = devices[i];
                if (darkCell < 0 && d.Role == PowerRole.Consumer && d.On && !d.Powered
                    && snapshot.TryGetPowerNet(d.NetKey, out PowerNetView dn) && dn.State == PowerNetState.Dark)
                    darkCell = d.HeadCell;
                if (d.Role != PowerRole.Generator || !d.On || !d.BurnsFuel || d.FuelMilli > 0) continue;
                if (!snapshot.TryGetPowerNet(d.NetKey, out PowerNetView n) || n.DemandW <= 0) continue;
                dry++;
                if (dryCell < 0) dryCell = d.HeadCell;
            }

            // The hearth (design 43 §3f): missing while somebody is kept home, or ordered down.
            int hearth = snapshot.HearthCell;
            bool noHearth = hearth < 0 && keptHome > 0;
            bool hearthDown = false;
            if (hearth >= 0)
            {
                System.ReadOnlySpan<OrderView> orders = snapshot.Orders;
                for (int i = 0; i < orders.Length; i++)
                    if (orders[i].CellIndex == hearth && orders[i].Kind == DeconstructOrderKind) { hearthDown = true; break; }
            }

            // A raid assaulting (design 55 §7): the standing raiders of every assaulting band, and the
            // first band's middle and mix for the row. The middle is coarsened to eight cells in the
            // signature, so the row's jump follows the band without a rebuild on every step.
            int raidStanding = 0, raidMix = -1, raidId = 0;
            CellRef raidCentre = default;
            System.ReadOnlySpan<RaidView> raids = snapshot.Raids;
            for (int i = 0; i < raids.Length; i++)
            {
                if (raids[i].Phase != RaidPhase.Assaulting || raids[i].Standing <= 0) continue;
                raidStanding += raids[i].Standing;
                if (raidMix >= 0) continue;
                raidMix = raids[i].Mix;
                raidCentre = raids[i].Centre;
                raidId = raids[i].Id;
            }
            int raidSignature = raidMix < 0 ? -1 : raidStanding * 1_000_000 + raidCentre.X / 8 * 1_000 + raidCentre.Z / 8;

            if (raidSignature == _wasRaid && raidId == _wasRaidId &&
                starving == _wasStarving && breaking == _wasBreaking &&
                (noHearth ? keptHome : 0) == _wasNoHearth && (hearthDown ? hearth : -1) == _wasHearthDown &&
                dark == _wasDark && shortW == _wasShortW && dry == _wasDry &&
                idleStands == _wasIdle && storeStuck == _wasStoreStuck && colonists == _wasColony &&
                noBed == _wasNoBed && noBedIds == _wasNoBedIds &&
                noPrisonBed == _wasNoPrisonBed && noPrisonBedIds == _wasNoPrisonBedIds &&
                injured == _wasInjured && injuredIds == _wasInjuredIds && noMedicine == _wasNoMedicine &&
                _latchVersion == _wasLatchVersion && _dismissVersion == _wasDismissVersion)
                return;

            _wasDark = dark;
            _wasShortW = shortW;
            _wasDry = dry;
            _wasStarving = starving;
            _wasBreaking = breaking;
            _wasIdle = idleStands;
            _wasStoreStuck = storeStuck;
            _wasColony = colonists;
            _wasNoBed = noBed;
            _wasNoHearth = noHearth ? keptHome : 0;
            _wasHearthDown = hearthDown ? hearth : -1;
            _wasNoBedIds = noBedIds;
            _wasNoPrisonBed = noPrisonBed;
            _wasNoPrisonBedIds = noPrisonBedIds;
            _wasInjured = injured;
            _wasInjuredIds = injuredIds;
            _wasNoMedicine = noMedicine;
            _wasLatchVersion = _latchVersion;
            _wasDismissVersion = _dismissVersion;
            _wasRaid = raidSignature;
            _wasRaidId = raidId;
            Rows.Clear();

            // Worst of all first: a band in the colony.
            // Dismissed by the band, not the key alone: a raid that follows another in the same
            // refresh is news, and its assault horn with it.
            int raidDismiss = AlertRow.ComputeDismissKey(RaidKey, new PawnId(raidId), null);
            if (raidMix >= 0 && raidDismiss != _raidDismiss)
            {
                _dismissed.Remove(_raidDismiss);
                _raidDismiss = raidDismiss;
            }
            if (raidMix >= 0)
            {
                if (!_dismissed.Contains(raidDismiss))
                    Rows.Add(new AlertRow(
                        RaidKey,
                        Registry.Label(RaidKey),
                        raidStanding == 1 ? string.Empty : " × " + raidStanding,
                        AlertSeverity.Danger,
                        count: raidStanding,
                        cell: raidCentre,
                        detail: RaidMixLabels.Label(raidMix),
                        dismissByKey: true,
                        identity: raidId));
            }
            else
            {
                _dismissed.Remove(_raidDismiss);
                _raidDismiss = 0;
            }

            // Worst first: Danger (starving), then Warning (breaking), then Notice (idle).
            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (_starving.Contains(pawn.Id.Value))
                {
                    int dismissKey = AlertRow.ComputeDismissKey(StarveKey, pawn.Id, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        string name = ColonistNames.Of(snapshot, pawn.Id);
                        Rows.Add(new AlertRow(
                            StarveKey,
                            name,
                            " is starving",
                            AlertSeverity.Danger,
                            count: 1,
                            pawn: pawn.Id));
                    }
                }
            }

            for (int i = 0; i < pawns.Length; i++)
            {
                PawnView pawn = pawns[i];
                if (_breaking.Contains(pawn.Id.Value))
                {
                    int dismissKey = AlertRow.ComputeDismissKey(BreakKey, pawn.Id, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        string name = ColonistNames.Of(snapshot, pawn.Id);
                        Rows.Add(new AlertRow(
                            BreakKey,
                            name,
                            " is close to breaking",
                            AlertSeverity.Warning,
                            count: 1,
                            pawn: pawn.Id));
                    }
                }
            }

            // Down with nowhere to be carried: Danger, because she lies where she fell until a bed
            // frees, and a click goes to her (design 33 §11d).
            for (int i = 0; i < pawns.Length && noBed > 0; i++)
            {
                PawnView pawn = pawns[i];
                if (!pawn.IsColonist || !snapshot.TryGetPawnAspect(pawn.Id, CombatAspectNames.RescueNoBedKey, out _)) continue;
                int dismissKey = AlertRow.ComputeDismissKey(NoRescueBedKey, pawn.Id, default);
                if (_dismissed.Contains(dismissKey)) continue;
                Rows.Add(new AlertRow(
                    NoRescueBedKey,
                    ColonistNames.Of(snapshot, pawn.Id),
                    " is down with no bed to be carried to",
                    AlertSeverity.Danger,
                    count: 1,
                    pawn: pawn.Id));
            }

            // Down, meant to be held, and no prison bed free (design 58 §7): Warning, because she is
            // not dying of it, and a click goes to her.
            for (int i = 0; i < pawns.Length && noPrisonBed > 0; i++)
            {
                PawnView pawn = pawns[i];
                if (pawn.IsColonist || !snapshot.TryGetPawnAspect(pawn.Id, PrisonAspectNames.NoBedKey, out _)) continue;
                int dismissKey = AlertRow.ComputeDismissKey(NoPrisonBedKey, pawn.Id, default);
                if (_dismissed.Contains(dismissKey)) continue;
                Rows.Add(new AlertRow(
                    NoPrisonBedKey,
                    ColonistNames.Of(snapshot, pawn.Id),
                    " is waiting for a free prison bed",
                    AlertSeverity.Warning,
                    count: 1,
                    pawn: pawn.Id));
            }

            // Untended injuries (design 43 §11): Danger while bleeding, with how long it leaves her,
            // else Warning. A downed colonist already has the rescue's row or the downed flag; this
            // is the one that says a doctor is owed.
            for (int i = 0; i < pawns.Length && injured > 0; i++)
            {
                PawnView pawn = pawns[i];
                if (!pawn.IsColonist || !Untended(snapshot, pawn, out int hours)) continue;
                int dismissKey = AlertRow.ComputeDismissKey(InjuredKey, pawn.Id, default);
                if (_dismissed.Contains(dismissKey)) continue;
                Rows.Add(new AlertRow(
                    InjuredKey,
                    ColonistNames.Of(snapshot, pawn.Id),
                    hours > 0 ? " is bleeding: " + hours + " h " + Registry.Label("ui.health.todeath") : " needs tending",
                    hours > 0 ? AlertSeverity.Danger : AlertSeverity.Warning,
                    count: 1,
                    pawn: pawn.Id));
            }

            if (noMedicine > 0 && !_dismissed.Contains(AlertRow.ComputeDismissKey(NoMedicineKey, default, default)))
                Rows.Add(new AlertRow(
                    NoMedicineKey,
                    Registry.Label(NoMedicineKey),
                    ": the doctor will dress wounds bare",
                    AlertSeverity.Notice,
                    count: 1));

            // A dark net is Danger: whatever it was keeping warm is going cold now. The cell is a
            // consumer on it, so a click on the row goes to what has stopped, which is where the
            // player's eyes want to be.
            if (dark > 0)
            {
                CellRef? at = darkCell >= 0 ? snapshot.Size.FromIndex(darkCell) : (CellRef?)null;
                int dismissKey = AlertRow.ComputeDismissKey(PowerLossKey, default, null);
                if (!_dismissed.Contains(dismissKey))
                    Rows.Add(new AlertRow(
                        PowerLossKey,
                        Registry.Label(PowerLossKey),
                        dark == 1 ? string.Empty : " × " + dark,
                        AlertSeverity.Danger,
                        count: dark,
                        cell: at,
                        detail: PowerLabels.Watts(shortW) + " short"));
            }
            else _dismissed.Remove(AlertRow.ComputeDismissKey(PowerLossKey, default, null));

            if (dry > 0)
            {
                CellRef? at = dryCell >= 0 ? snapshot.Size.FromIndex(dryCell) : (CellRef?)null;
                int dismissKey = AlertRow.ComputeDismissKey(NoFuelKey, default, null);
                if (!_dismissed.Contains(dismissKey))
                    Rows.Add(new AlertRow(
                        NoFuelKey,
                        Registry.Label(NoFuelKey),
                        dry == 1 ? string.Empty : " × " + dry,
                        AlertSeverity.Warning,
                        count: dry,
                        cell: at));
            }
            else _dismissed.Remove(AlertRow.ComputeDismissKey(NoFuelKey, default, null));

            // The hearth ordered down is Warning: nothing has happened yet, and a click goes to it.
            // The row's dismissal key is made from the cell it points at, so the check is too.
            if (hearthDown)
            {
                CellRef at = snapshot.Size.FromIndex(hearth);
                int key = AlertRow.ComputeDismissKey(HearthDownKey, default, at);
                // The hearth moved to another campfire that is also marked: a dismissal of the
                // old one's warning is not a dismissal of this one's.
                if (_hearthDownDismissKey != 0 && _hearthDownDismissKey != key) _dismissed.Remove(_hearthDownDismissKey);
                _hearthDownDismissKey = key;
                if (!_dismissed.Contains(_hearthDownDismissKey))
                    Rows.Add(new AlertRow(
                        HearthDownKey,
                        Registry.Label(HearthDownKey),
                        string.Empty,
                        AlertSeverity.Warning,
                        count: 1,
                        cell: at));
            }
            else if (_hearthDownDismissKey != 0)
            {
                _dismissed.Remove(_hearthDownDismissKey);
                _hearthDownDismissKey = 0;
            }

            // No hearth with somebody kept home is Warning: home does not exist, so she is kept
            // nowhere. There is no cell to go to; the fix is building or marking a campfire.
            if (noHearth)
            {
                int dismissKey = AlertRow.ComputeDismissKey(NoHearthKey, default, null);
                if (!_dismissed.Contains(dismissKey))
                    Rows.Add(new AlertRow(
                        NoHearthKey,
                        Registry.Label(NoHearthKey),
                        string.Empty,
                        AlertSeverity.Warning,
                        count: keptHome,
                        cell: null));
            }
            else _dismissed.Remove(AlertRow.ComputeDismissKey(NoHearthKey, default, null));

            if (storeStuck)
                Rows.Add(new AlertRow(
                    StoreStuckKey,
                    Registry.Label(StoreStuckKey),
                    stuck == 1 ? " — nowhere to put what is in it" : " — " + stuck + " of them",
                    AlertSeverity.Warning,
                    count: stuck));

            if (idleStands)
            {
                if (colonists == 1)
                {
                    int dismissKey = AlertRow.ComputeDismissKey(IdleKey, onlyColonist, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        string name = ColonistNames.Of(snapshot, onlyColonist);
                        Rows.Add(new AlertRow(
                            IdleKey,
                            name,
                            " is idle",
                            AlertSeverity.Notice,
                            count: 1,
                            pawn: onlyColonist));
                    }
                }
                else
                {
                    int dismissKey = AlertRow.ComputeDismissKey(IdleKey, default, default);
                    if (!_dismissed.Contains(dismissKey))
                    {
                        Rows.Add(new AlertRow(
                            IdleKey,
                            "Colony",
                            " is idle",
                            AlertSeverity.Notice,
                            count: idle));
                    }
                }
            }
        }

        static bool Latch(HashSet<int> raised, int id, int value, int at, int clearAt, ref int version)
        {
            bool already = raised.Contains(id);
            if (value <= at)
            {
                if (!already)
                {
                    raised.Add(id);
                    version++;
                }
                return true;
            }
            if (already && value < clearAt) return true;
            if (already)
            {
                raised.Remove(id);
                version++;
            }
            return false;
        }

        static void Forget(HashSet<int> raised, WorldSnapshot snapshot, ref int version)
        {
            if (raised.Count == 0) return;
            List<int>? gone = null;
            foreach (int id in raised)
                if (!snapshot.TryGetPawn(new PawnId(id), out _))
                    (gone ??= new List<int>()).Add(id);
            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++)
            {
                raised.Remove(gone[i]);
                version++;
            }
        }
    }
}
