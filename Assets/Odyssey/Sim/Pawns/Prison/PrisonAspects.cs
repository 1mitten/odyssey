using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What the prison publishes about a pawn (design 59 §11), as sparse pawn aspects: a pawn the
    /// colony has nothing to say about publishes none. The interface reads the same names from
    /// <c>Odyssey.Hud.PrisonAspectNames</c>, which cannot see this assembly.
    /// </summary>
    public static class PrisonAspects
    {
        /// <summary>1 on a downed pawn the colony means to hold for whom no prison bed is free. The alert reads it.</summary>
        public const string NoBedName = "odyssey.pawn.prison.nobed";

        /// <summary>1 on a person marked for capture and not yet taken.</summary>
        public const string CaptureMarkName = "odyssey.pawn.prison.capture";

        /// <summary>A held prisoner's <see cref="PrisonMode"/>, as a number.</summary>
        public const string ModeName = "odyssey.pawn.prison.mode";

        /// <summary>How far talked round, in thousandths of a full bar.</summary>
        public const string WillingName = "odyssey.pawn.prison.willing";

        /// <summary>Game hours until she joins at the best warden's pace; -1 when nobody will talk to her. Recruit mode only.</summary>
        public const string HoursName = "odyssey.pawn.prison.hours";

        /// <summary>Why she is being talked round slowly: <see cref="RecruitBlockers"/> as a number. Recruit mode only.</summary>
        public const string BlockersName = "odyssey.pawn.prison.blockers";

        /// <summary>1 while her bed is a shackle bed.</summary>
        public const string ShackledName = "odyssey.pawn.prison.shackled";

        /// <summary>Her chance of breaking out in a day, in parts per million (design 59 §9a).</summary>
        public const string EscapeName = "odyssey.pawn.prison.escape";

        /// <summary>Why her escape risk is what it is: <see cref="EscapeReasons"/> as a number.</summary>
        public const string EscapeWhyName = "odyssey.pawn.prison.escapewhy";

        /// <summary>
        /// Present on a downed prisoner out of her prison bed, who is to be carried back to it: the
        /// one case Capture is offered on somebody already held (design 59 §16 H3). Asked of
        /// <see cref="CaptureRules.WantsCapture"/>, so the menu and the order cannot disagree.
        /// </summary>
        public const string StrayName = "odyssey.pawn.prison.stray";

        public static readonly AspectKey NoBed = AspectKey.Of(NoBedName);
        public static readonly AspectKey Stray = AspectKey.Of(StrayName);
        public static readonly AspectKey Escape = AspectKey.Of(EscapeName);
        public static readonly AspectKey EscapeWhy = AspectKey.Of(EscapeWhyName);
        public static readonly AspectKey CaptureMark = AspectKey.Of(CaptureMarkName);
        public static readonly AspectKey Mode = AspectKey.Of(ModeName);
        public static readonly AspectKey Willing = AspectKey.Of(WillingName);
        public static readonly AspectKey Hours = AspectKey.Of(HoursName);
        public static readonly AspectKey Blockers = AspectKey.Of(BlockersName);
        public static readonly AspectKey Shackled = AspectKey.Of(ShackledName);

        /// <summary>
        /// What every prisoner's rows share within one publish (design 59 §16 #7): how many the
        /// colony holds and who its best warden is. Each walks every pawn, and each was asked again
        /// for every prisoner — fifty prisoners among four hundred pawns was some 80,000 steps a
        /// publish. Found on first need and kept for the rest of the publish; start one per publish.
        /// </summary>
        public struct Shared
        {
            int _held;
            Pawn? _warden;
            bool _heldKnown, _wardenKnown;

            public int Held(PawnContext ctx)
            {
                if (!_heldKnown) { _held = EscapeRisk.Held(ctx); _heldKnown = true; }
                return _held;
            }

            public Pawn? Warden(PawnContext ctx)
            {
                if (!_wardenKnown) { _warden = Recruitment.BestWarden(ctx); _wardenKnown = true; }
                return _warden;
            }
        }

        /// <summary>Everything the prison says about one pawn, into the publish.</summary>
        public static void Publish(SnapshotWriter writer, Pawn pawn, PawnContext ctx)
        {
            var shared = new Shared();
            Publish(writer, pawn, ctx, ref shared);
        }

        /// <summary><see cref="Publish(SnapshotWriter, Pawn, PawnContext)"/> within one publish's <see cref="Shared"/>.</summary>
        public static void Publish(SnapshotWriter writer, Pawn pawn, PawnContext ctx, ref Shared shared)
        {
            if (pawn.Prison != null && pawn.Prison.CaptureMark) writer.AddPawnAspect(pawn.Id, CaptureMark, 1);
            if (pawn.Custody == PawnCustody.Prisoner)
            {
                // The prisoner's pane (design 59 §11b), every number from the owner that runs it.
                // Asked of custody, never of the record: a freshly taken prisoner's record is empty,
                // and a load drops an empty one.
                PrisonMode mode = pawn.Prison?.Mode ?? PrisonMode.Hold;
                writer.AddPawnAspect(pawn.Id, Mode, (int)mode);
                writer.AddPawnAspect(pawn.Id, Willing, (pawn.Prison?.Willingness ?? 0) / 1_000);
                if (PrisonerTrees.IsShackled(pawn, ctx)) writer.AddPawnAspect(pawn.Id, Shackled, 1);
                // The risk the hourly roll uses, from the same call (design 59 §9b).
                EscapeOdds odds = EscapeRisk.Odds(pawn, ctx, shared.Held(ctx));
                writer.AddPawnAspect(pawn.Id, Escape, odds.PerDayPpm);
                writer.AddPawnAspect(pawn.Id, EscapeWhy, (int)odds.Reasons);
                if (mode == PrisonMode.Recruit)
                {
                    Pawn? warden = shared.Warden(ctx);
                    writer.AddPawnAspect(pawn.Id, Hours, Recruitment.HoursToJoin(pawn, ctx, warden));
                    writer.AddPawnAspect(pawn.Id, Blockers, (int)Recruitment.Factors(pawn, warden, ctx).Blockers);
                }
            }
            if (pawn.Downed && CaptureRules.WantsCapture(pawn, ctx))
            {
                if (pawn.IsPrisoner) writer.AddPawnAspect(pawn.Id, Stray, 1);
                if (CaptureRules.BedFor(pawn, pawn, ctx) < 0) writer.AddPawnAspect(pawn.Id, NoBed, 1);
            }
        }
    }
}
