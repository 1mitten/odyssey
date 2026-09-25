#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>One colonist, as the Assign tab lists her (design 43 §6).</summary>
    public struct AssignRow
    {
        public PawnId Id;
        public uint Seed;
        public string Name;
        public int Area;
        public int Response;
        public bool Selected;

        public string AreaKey => AssignModel.AreaKeyOf(Area);
        public string ResponseKey => ResponseModel.KeyOf(Response);

        /// <summary>
        /// Drawn in the warning ink (the spec's "cautious" state): a colonist kept home or told to
        /// flee is one who will not be where a player expects when the raid comes.
        /// </summary>
        public bool AreaCautious => Area == AssignModel.Home;
        public bool ResponseCautious => Response == ResponseModel.Flee;
    }

    /// <summary>
    /// The Assign tab's rows (design 43 §6): every colonist in the roster's order, with where she
    /// may work and what she does about danger, twelve to a page. Unity-free, so the fast tier owns
    /// the rule; the shell hears a click and carries the intent this returns to the world.
    ///
    /// <para><b>The roster's order, never a sort of its own</b>, as the Work tab takes it: the
    /// player already arranged the colony once, on the strip.</para>
    ///
    /// <para><b>The area numbers are the simulation's <c>PawnArea</c></b>, which this assembly
    /// cannot see; <c>AssignModelTests</c> and <c>PawnAreaTests</c> hold the two sides to the same
    /// two.</para>
    /// </summary>
    public sealed class AssignModel
    {
        /// <summary>The two areas, as the simulation numbers them.</summary>
        public const int Anywhere = 0, Home = 1, AreaCount = 2;

        /// <summary>The current page of colonists.</summary>
        public readonly List<AssignRow> Rows = new List<AssignRow>();

        readonly List<PawnId> _all = new List<PawnId>();

        public int Page { get; private set; }

        public int TotalCount => _all.Count;

        public int PageCount => TotalCount <= 0 ? 1 : (TotalCount + AssignLayout.RowsPerPage - 1) / AssignLayout.RowsPerPage;

        /// <summary>Whether the pager shows: only past a page.</summary>
        public bool Paged => PageCount > 1;

        /// <summary>No hearth, so no home, and a colonist set to Home is kept nowhere (design 43 §4d).</summary>
        public bool NoHearth { get; private set; }

        public void SetPage(int page) => Page = Math.Max(0, Math.Min(page, PageCount - 1));

        /// <summary>Bring the page that holds this colonist up. False if she is not listed.</summary>
        public bool EnsurePageFor(PawnId id)
        {
            int at = _all.IndexOf(id);
            if (at < 0) return false;
            SetPage(at / AssignLayout.RowsPerPage);
            return true;
        }

        public void Refresh(WorldSnapshot snapshot, IReadOnlyList<PawnId>? order, IReadOnlyList<PawnId>? selected)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            NoHearth = snapshot.HearthCell < 0;

            // Colonists only, by the flags: the roster hands over only colonists already, and this
            // keeps a bandit or an animal in a stale order from growing a row the simulation would
            // refuse to set.
            _all.Clear();
            if (order != null)
                for (int i = 0; i < order.Count; i++)
                    if (snapshot.TryGetPawn(order[i], out PawnView listed) && listed.IsColonist) _all.Add(order[i]);

            SetPage(Page);
            Rows.Clear();
            int start = Page * AssignLayout.RowsPerPage;
            int end = Math.Min(TotalCount, start + AssignLayout.RowsPerPage);
            for (int i = start; i < end; i++)
            {
                PawnId id = _all[i];
                uint seed = ColonistNames.RollSeedOf(snapshot, id);
                Rows.Add(new AssignRow
                {
                    Id = id,
                    Seed = seed,
                    Name = ColonistNames.Of(seed, id),
                    Area = AreaOf(snapshot, id),
                    Response = ResponseModel.Of(snapshot, id),
                    Selected = Contains(selected, id),
                });
            }
        }

        /// <summary>The colonist's area off the frame: Home while the sparse aspect says so, Anywhere otherwise.</summary>
        public static int AreaOf(WorldSnapshot snapshot, PawnId pawn) =>
            snapshot.TryGetPawnAspect(pawn, AreaAspectNames.AreaKey, out int area) && area == Home ? Home : Anywhere;

        /// <summary>The key that names an area on the cell.</summary>
        public static string AreaKeyOf(int area) => area == Home ? AssignDirector.HomeKey : AssignDirector.AnywhereKey;

        /// <summary>The one after <paramref name="area"/>, round the two.</summary>
        public static int NextArea(int area) => (area + 1) % AreaCount;

        /// <summary>A press on a row's Area cell: the next area, for that colonist alone.</summary>
        public static bool TryCycleArea(WorldSnapshot snapshot, PawnId pawn, out Intent intent)
        {
            intent = default;
            if (!OrderModel.IsColonist(snapshot, pawn)) return false;
            intent = new Intent(IntentKind.SetPawnArea, default, pawn.Value, NextArea(AreaOf(snapshot, pawn)));
            return true;
        }

        /// <summary>A press on a row's Response cell: the next response, for that colonist alone.</summary>
        public static bool TryCycleResponse(WorldSnapshot snapshot, PawnId pawn, out Intent intent)
        {
            intent = default;
            if (!OrderModel.IsColonist(snapshot, pawn)) return false;
            intent = new Intent(IntentKind.SetHostilityResponse, default, pawn.Value,
                ResponseModel.Next(ResponseModel.Of(snapshot, pawn)));
            return true;
        }

        static bool Contains(IReadOnlyList<PawnId>? list, PawnId id)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i].Value == id.Value) return true;
            return false;
        }
    }
}
