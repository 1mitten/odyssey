#nullable enable
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The shape and timing of one stroke: two poses and the three numbers that say how the
    /// figure travels between them.
    ///
    /// <para><b>Why this is a value and not a set of constants.</b> Every one of these used to be
    /// <c>const</c> on <see cref="WorkSwing"/>, which meant there could only ever be one stroke in
    /// the build. That was correct while felling was the only work there was, and it is the single
    /// thing that had to change before a second kind of work could look like anything — an axe and
    /// a pick do not move alike, and no amount of swapping the prop hides it.</para>
    ///
    /// <para><see cref="WorkSwing"/> is still three angles and nothing else. What moved here is
    /// the <em>curve</em>: which two poses the stroke runs between, how long it takes, and where in
    /// it the raise ends and the blow lands.</para>
    /// </summary>
    public readonly struct WorkStroke
    {
        /// <summary>The pose with the tool at the top of its arc.</summary>
        public readonly WorkSwing Raised;

        /// <summary>The pose at the moment the head is in the work.</summary>
        public readonly WorkSwing Struck;

        /// <summary>How long one stroke takes, in seconds.</summary>
        public readonly float StrokeSeconds;

        /// <summary>Where in the stroke the tool stops rising and starts to fall.</summary>
        public readonly float RaiseEnds;

        /// <summary>
        /// Where in the stroke the head reaches the work. The dwell runs from here to the end.
        ///
        /// Not only the shape of the pose: it is the instant a chip should fly, and something
        /// outside has to be able to ask when that was.
        /// </summary>
        public readonly float StrikeEnds;

        public WorkStroke(WorkSwing raised, WorkSwing struck,
            float strokeSeconds, float raiseEnds, float strikeEnds)
        {
            Raised = raised;
            Struck = struck;
            StrokeSeconds = strokeSeconds;
            RaiseEnds = raiseEnds;
            StrikeEnds = strikeEnds;
        }

        /// <summary>
        /// How much longer or shorter than the nominal one colonist's stroke is, either way.
        ///
        /// Shared by every stroke, because it is a fact about people working near each other and
        /// not about the tool: two workers who set to at the same moment are visibly out of step
        /// within three or four blows, and nobody looks hurried.
        /// </summary>
        public const float StrokeSpread = 0.18f;

        /// <summary>
        /// The felling stroke, and the one set of angles in the project that was actually settled
        /// against photographs rather than reasoned about (<c>06-rendering-and-camera.md</c> §6a).
        ///
        /// The range is wide because it starts from an idle in which the arm hangs at the side.
        /// The forearm is what the tool follows, and that is shoulder plus elbow, which is why the
        /// struck shoulder looks smaller than a drawing of the pose would suggest. The spine's
        /// signs run opposite to the arm's: a spine stands up where an arm hangs down, so here
        /// positive folds forward into the blow.
        /// </summary>
        public static readonly WorkStroke Axe = new WorkStroke(
            raised: new WorkSwing(-158f, -74f, -12f),
            struck: new WorkSwing(-55f, -20f, 20f),
            // Ten seconds of felling is about nine swings: an unhurried rhythm rather than a man
            // attacking the tree.
            strokeSeconds: 1.15f,
            raiseEnds: 0.62f,
            strikeEnds: 0.78f);

        /// <summary>
        /// The mining stroke. **Every number here is a proposal and none of them is settled.**
        ///
        /// <para>§6a is explicit that the axe's six angles could not be confirmed by reasoning and
        /// had to come off <c>SwingCheck</c> photographs. These have exactly the same standing: a
        /// pick goes over the crown and down the midline rather than past a shoulder and across
        /// the body, its head is heavier, and its arc is shorter — so it wants less tilt, a
        /// shorter period and less dwell. That is the argument, and an argument is not a
        /// measurement. Shoot the contact sheet before believing any of it.</para>
        /// </summary>
        public static readonly WorkStroke Pick = new WorkStroke(
            // The pick does not come as far back as an axe: the weight of the head does the work
            // rather than the length of the arc.
            raised: new WorkSwing(-140f, -68f, -10f),
            struck: new WorkSwing(-52f, -16f, 22f),
            // Faster cadence, so mining reads as persistent where felling reads as unhurried.
            strokeSeconds: 0.85f,
            raiseEnds: 0.58f,
            // Less dwell. A pick bites and rebounds where an axe buries itself and rests; without
            // a recoil in the curve the shortest honest thing is to spend less time at the bottom.
            strikeEnds: 0.84f);

        /// <summary>
        /// The builder's stroke. **Every number is a proposal**, on the same footing as the pick's.
        ///
        /// <para>The owner's brief was "like chopping rather than like mining", and that settles
        /// the one thing that most distinguishes the three: where the arc lives. An axe comes past
        /// a shoulder and across the body; a pick goes over the crown and down the midline; a
        /// hammer is the axe's plane with a good deal less of it.</para>
        ///
        /// <para>What makes it its own stroke rather than a fast axe is the <em>haft</em>. The
        /// hammer is 0.63 m against the axe's 0.74, and a short haft is swung from the elbow where
        /// a long one is swung from the shoulder. So the shoulder comes back much less (−128°
        /// against −158°) while the elbow cocks <em>harder</em> (−86° against −74°): the tool is
        /// brought back beside the ear rather than behind the back. Get that the wrong way round —
        /// a short tool on a long arc — and the figure reads as swinging a hammer it wishes were
        /// an axe, which is exactly what copying <see cref="Axe"/> and shortening the period
        /// would produce.</para>
        ///
        /// <para>Faster and flatter besides: 0.7 s a blow, because driving a frame together is a
        /// quick repeated tap and not a woodcutter's rhythm, and <see cref="StrikeEnds"/> at 0.88
        /// because a hammer rebounds off what it hits harder than a pick does and far harder than
        /// an axe, which buries itself and rests. Until a stroke can recoil, the only honest way to
        /// say "it bounces" is to spend almost no time at the bottom.</para>
        /// </summary>
        public static readonly WorkStroke Hammer = new WorkStroke(
            raised: new WorkSwing(-128f, -86f, -8f),
            struck: new WorkSwing(-58f, -14f, 16f),
            strokeSeconds: 0.7f,
            raiseEnds: 0.6f,
            strikeEnds: 0.88f);

        /// <summary>The pose at the moment the head is in the work. Any phase in the dwell agrees.</summary>
        public WorkSwing AtStrike => At(0.9f);

        /// <summary>Where in the stroke a clock running at the nominal rate is, 0 to 1.</summary>
        public float Phase(float seconds) => Wrap(seconds / StrokeSeconds);

        /// <summary>
        /// Where in the stroke a particular figure is, 0 to 1.
        ///
        /// <paramref name="offset"/> lengthens the stroke rather than shifting it, which is the
        /// difference between work that begins and work that snaps on: every stroke starts at
        /// nought, so the pose eases in towards the one place in it nearest to standing still, and
        /// two workers drift apart over the following strokes rather than beginning out of step.
        /// </summary>
        public float Phase(float seconds, float offset) => Wrap(seconds / PeriodFor(offset));

        /// <summary>How long one stroke takes for a figure with this offset, in seconds.</summary>
        public float PeriodFor(float offset) =>
            StrokeSeconds * (1f + StrokeSpread * (Mathf.Repeat(offset, 1f) - 0.5f));

        /// <summary>
        /// Whether the blow landed between two phases — whether the stroke crossed
        /// <see cref="StrikeEnds"/> going forwards.
        ///
        /// Three questions, not one: the ordinary crossing; the frame where the phase wraps, which
        /// must not fire twice for one blow; and a frame long enough to step over the whole strike,
        /// which must still fire, because a dropped frame is not a reason for the chips to go
        /// missing.
        /// </summary>
        public bool Lands(float previous, float current)
        {
            if (current < previous) return previous < StrikeEnds;
            return previous < StrikeEnds && current >= StrikeEnds;
        }

        /// <summary>
        /// How far through the stroke the tool is: 0 raised, 1 in the work.
        ///
        /// Three unequal parts, and the inequality is the entire point. The raise is long and
        /// eased at both ends, because lifting a tool is deliberate. The strike is short and
        /// accelerating, because a falling head accelerates. The dwell at the bottom is the beat
        /// in which nothing happens — take it out and the motion reads as a metronome rather than
        /// as work, which is exactly what a plain sine wave gives you.
        /// </summary>
        /// <summary>
        /// Where the working hand is on the haft at this point in the stroke, given the two ends
        /// of its slide: <paramref name="raised"/> at the top and <paramref name="struck"/> at the
        /// blow.
        ///
        /// <para>It is <see cref="Stroke"/> and nothing else, which is the whole reason the slide
        /// costs so little. That curve is already the tool's own progress — long eased raise, short
        /// accelerating fall, dwell at the bottom — so a hand carried along it slides up
        /// deliberately, snaps down as the head accelerates, and is still at the butt through the
        /// dwell with the blade in the wood. A separate curve would have to be kept in step with
        /// this one by hand, and the frame it drifted on would be the frame the blow lands.</para>
        /// </summary>
        public float GripAt(float phase, float raised, float struck) =>
            Mathf.Lerp(raised, struck, Stroke(phase));

        public float Stroke(float phase)
        {
            if (phase < RaiseEnds)
            {
                float t = phase / RaiseEnds;
                return 1f - t * t * (3f - 2f * t);
            }

            if (phase < StrikeEnds)
            {
                float t = (phase - RaiseEnds) / (StrikeEnds - RaiseEnds);
                return t * t;
            }

            return 1f;
        }

        /// <summary>The pose at a point in the stroke.</summary>
        public WorkSwing At(float phase)
        {
            float stroke = Stroke(phase);
            return new WorkSwing(
                Mathf.Lerp(Raised.Shoulder, Struck.Shoulder, stroke),
                Mathf.Lerp(Raised.Elbow, Struck.Elbow, stroke),
                Mathf.Lerp(Raised.Spine, Struck.Spine, stroke));
        }

        static float Wrap(float phase)
        {
            phase %= 1f;
            return phase < 0f ? phase + 1f : phase;
        }
    }

    /// <summary>
    /// Everything about how one kind of work looks: the stroke, the tool, the debris, where the
    /// head is aimed and the handful of numbers that fit the prop to the fist.
    ///
    /// <para><b>The instruction this obeys</b> (owner, 2026-09-16): use composition, so effects
    /// and poses are reused rather than copied. There is one director, one swing, one chip system
    /// and one fitting pipeline; a style is a bundle of numbers that tells them what to be. Adding
    /// a third kind of work — building, deconstructing, butchering — should be a new static here
    /// and a row in <see cref="ForJob"/>, and nothing else.</para>
    ///
    /// <para><b>Tilt, grip, roll and spacing used to live on the director</b>, which made them
    /// properties of the whole colony rather than of the work. That was invisible while there was
    /// one kind of work and wrong the moment there were two: a pick and an axe are not held alike
    /// and do not swing in the same plane.</para>
    /// </summary>
    public readonly struct WorkStyle
    {
        public readonly WorkStroke Stroke;

        /// <summary>The catalogue id of the tool. Null on a clone without the packs, and then no tool.</summary>
        public readonly string ToolModule;

        /// <summary>What flies on contact.</summary>
        public readonly ChipRecipe Chips;

        /// <summary>
        /// How far in from the centre of the work cell, towards the worker, the head is aimed.
        ///
        /// <para><b>This is the number the design note called <c>WorkAim</c>, and it is the real
        /// bug that note was written to catch.</b> Felling aims 0.15 m past the centre of the cell,
        /// which is right for a trunk about six-tenths of a metre through — roughly the middle of
        /// the wood. A rock <em>cell</em> is 2.5 m through. Aim 0.15 m past its centre and the pick
        /// finishes 1.1 m inside solid stone, with the chips spawning in there with it: the head
        /// vanishes and the burst is never seen. <c>MinimumStandOff</c> does not save it, because
        /// the figure is solved well outside that floor and still has its pick buried.</para>
        ///
        /// <para>Expressed as a distance from the centre rather than as a face, because that one
        /// number covers both cases and stays inside the cell from every approach angle — even
        /// along the diagonal, where the boundary is 1.77 m out. A face point would have to pick an
        /// axis and would be wrong on the diagonal.</para>
        /// </summary>
        public readonly float AimFromCentre;

        /// <summary>Degrees the swing plane is tilted out of the figure's own right-hand axis.</summary>
        public readonly float Tilt;

        /// <summary>Where along the haft the fist closes, as a fraction from the butt.</summary>
        public readonly float GripFraction;

        /// <summary>Degrees the head is rolled about the haft. The escape hatch for <c>BitAxis</c>.</summary>
        public readonly float BladeRoll;

        /// <summary>Degrees the whole tool is turned about the figure's up axis.</summary>
        public readonly float BladeYaw;

        /// <summary>
        /// Where the <em>off</em> hand takes hold, as a fraction of the haft from the butt.
        ///
        /// <para><b>An absolute place on the haft, and it used to be a gap.</b> It was the distance
        /// the off hand sat <em>above</em> the working one, which put the two fists in the wrong
        /// order: the off hand up the haft and the working hand below it. A woodcutter holds an axe
        /// the other way round, and the owner's account of the technique is unambiguous — the
        /// non-dominant hand goes at the very butt and stays there, because it is the pivot the
        /// whole swing turns about. So this is now a place and not a gap, and the number that
        /// varies is the working hand's, which slides.</para>
        ///
        /// <para>Not quite zero. The butt of a haft is the end of the wood, and a fist wrapped
        /// round the end of a stick has the stick's end somewhere inside it.</para>
        /// </summary>
        public readonly float ButtFraction;

        /// <summary>
        /// Where the working hand starts the stroke, as a fraction of the haft from the butt, at
        /// the top of the raise.
        ///
        /// <para><b>The hands slide, and that is what the grip was missing.</b> Both fists were
        /// pinned to the haft for the whole stroke, which is the one thing a woodcutter's hands
        /// never do (owner, 2026-09-16): the dominant hand starts up near the head where it can
        /// carry the weight of the tool, and slides down the haft to meet the other at the butt as
        /// the blow falls. That slide is where the head speed comes from, and it is also why the
        /// off arm could reach at all — with the working hand high, the butt end swings back
        /// towards the body, which is exactly where the other hand is waiting.</para>
        ///
        /// <para>The observed fault it fixes is "the left arm is too far away". It was: with both
        /// hands pinned low, the butt of a raised axe is out at the end of an extended right arm
        /// and the left shoulder is half a body away from it. No solver reaches that, and
        /// <c>MeasuredGripOverreach</c> exists to say so in centimetres.</para>
        /// </summary>
        public readonly float SlideFraction;

        /// <summary>
        /// How far back out of the work, towards the worker, the debris is thrown from, in metres.
        ///
        /// <para>Chips spawn at the head, and the head is deliberately <em>inside</em> the thing
        /// being struck — that is what <see cref="AimFromCentre"/> is for. For a tree that is a
        /// few centimetres into a trunk and the burst still reads. For rock the head finishes just
        /// inside the face of an opaque block, so the pieces are born inside solid stone and the
        /// first part of their flight is invisible; what reaches the eye is a thin spray appearing
        /// out of nowhere a moment later. Backing the spawn out to the face fixes it, and the
        /// distance is per style because only the style knows how deep it struck.</para>
        /// </summary>
        public readonly float ChipStandOff;

        /// <summary>
        /// Degrees the whole stroke is aimed downward when the work is below the worker's feet.
        ///
        /// <para><b>The design note said no vertical aim would ever be wanted, and it was wrong
        /// about mining.</b> Its reasoning held for felling — a tree and the colonist cutting it
        /// stand on the same floor — and it assumed a miner would too. A miner does not. Cutting a
        /// cell out of the layer below means standing on its rim or on top of it, and in both the
        /// rock's top face is at the worker's own feet, a whole cell below the height the blade
        /// travels at. Drawn level, the pick sweeps through empty air a metre and a third above the
        /// stone it is supposedly breaking, which is what the owner saw.</para>
        ///
        /// <para>45° is derived, not dialled: the edge lands about 1.34 m above the feet and about
        /// 1.73 m in front, and the chain it hangs off pivots around the base of the spine roughly
        /// a metre up. Bringing a point 1.76 m out and 0.34 m above that pivot down to a metre
        /// below it is a rotation of some 46°. <c>DescribeTools</c> prints the measured result, so
        /// the number is checkable rather than argued.</para>
        ///
        /// <para>Zero for felling, which is the same as not having it.</para>
        /// </summary>
        public readonly float Dip;

        /// <summary>
        /// Degrees the whole stroke is aimed <em>upward</em> when the work is above the worker.
        ///
        /// <para>The mirror of <see cref="Dip"/>, and it exists for the same reason: a miner can
        /// cut the rock over its head or the overhang beside it — a pick goes overhead, and
        /// undercutting a face is ordinary mining — so the stroke has to point there. Drawn level,
        /// the pick swings at waist height under a ceiling three metres up.</para>
        ///
        /// <para><b>It is not as large as the dip, and it cannot be.</b> Bringing the edge down to
        /// the feet is a rotation the arm can make; bringing it to the bottom face of the cell
        /// above is not — that face is three metres up and the reach is about 1.76 m from a pivot
        /// a metre off the ground, so no angle gets there. What this asks for instead is the
        /// highest the figure can honestly put the head, which reads as a colonist working a
        /// ceiling at full stretch. <c>MeasuredRaisedBladeHeight</c> is what it actually achieves.</para>
        ///
        /// <para>Negative because it runs the same arithmetic as the dip through
        /// <see cref="WorkSwing.Dipped"/>: positive folds the back forward and carries the arm
        /// down, so negative straightens up and lifts it.</para>
        /// </summary>
        public readonly float Raise;

        public WorkStyle(WorkStroke stroke, string toolModule, ChipRecipe chips, float aimFromCentre,
            float tilt, float gripFraction, float bladeRoll, float bladeYaw, float buttFraction,
            float slideFraction,
            float chipStandOff = 0f, float dip = 0f, float raise = 0f)
        {
            ChipStandOff = chipStandOff;
            Dip = dip;
            Raise = raise;
            Stroke = stroke;
            ToolModule = toolModule;
            Chips = chips;
            AimFromCentre = aimFromCentre;
            Tilt = tilt;
            GripFraction = gripFraction;
            BladeRoll = bladeRoll;
            BladeYaw = bladeYaw;
            ButtFraction = buttFraction;
            SlideFraction = slideFraction;
        }

        /// <summary>Felling: the settled one. Every number came off a contact sheet.</summary>
        public static readonly WorkStyle Felling = new WorkStyle(
            WorkStroke.Axe, ModuleIds.ToolAxe, ChipRecipe.Wood,
            aimFromCentre: 0.15f,
            // Nothing: an axe bites a few centimetres into a trunk and the chips already read.
            //
            // The off hand at the butt and the working hand sliding 0.62 -> 0.21 down to meet it.
            // Two constraints decide the pair and they pull against each other. The technique says
            // the hands finish together; the meshes say two fists closer than about 9 cm are one
            // fist, which is what the older 0.11 spacing drew and what the owner saw as one arm
            // passing through the other (2026-09-16). 0.05 and 0.21 of a 0.74 m haft leave 0.12 m
            // between the palms, which is a hand's breadth — as together as two hands get.
            tilt: -30f, gripFraction: 0.21f, bladeRoll: 0f, bladeYaw: 0f, buttFraction: 0.05f,
            slideFraction: 0.62f,
            chipStandOff: 0f);

        /// <summary>
        /// Mining. **Proposed, not settled** — see <see cref="WorkStroke.Pick"/>.
        ///
        /// <para><see cref="BladeRoll"/> is the one to distrust most. <c>BitAxis</c> picks the
        /// fatter of the two axes crossing the haft and signs it towards the fat side, which is
        /// genuinely ambiguous on a pick: the head sticks out <em>both</em> ways, point one side
        /// and adze the other, so the sign is decided by whichever end the modeller happened to
        /// make heavier. That is not a fact anybody chose. 180° is the likeliest answer and the
        /// contact sheet is the only thing that can say.</para>
        /// </summary>
        public static readonly WorkStyle Mining = new WorkStyle(
            WorkStroke.Pick, ModuleIds.ToolPickaxe, ChipRecipe.Stone,
            // Just inside the near face of a 2.5 m cell, rather than 1.1 m into the rock.
            aimFromCentre: CellMetrics.SizeXZ * 0.5f - 0.12f,
            // A pick goes over the crown and down the midline, so much less tilt than an axe.
            // A pick slides less than an axe: the head is heavy and is never let go of.
            tilt: -8f, gripFraction: 0.21f, bladeRoll: 180f, bladeYaw: 0f, buttFraction: 0.05f,
            slideFraction: 0.44f,
            // Out past the face and a little clear of it, so the lumps are seen leaving the rock
            // rather than appearing in mid-air once they have already cleared it.
            // 12 cm back out to the face the head went in through, and 10 cm clear of it.
            chipStandOff: 0.22f,
            // Bent over the hole when the rock is below, and at full stretch when it is above.
            dip: 45f, raise: -55f);

        /// <summary>
        /// Building. **Proposed, and with less standing than even mining had**, because mining at
        /// least had a job to be photographed doing and this has none.
        ///
        /// <para><b>Nothing in the simulation builds anything.</b> There is no build pipeline, no
        /// <c>JobHandle.Build</c>, no construction designation that anything acts on — so
        /// <b>Reached in play since the build pipeline landed</b> — <see cref="IndexForJob"/> maps
        /// <c>JobHandle.Build</c> here, and a colonist raising a wall swings it. The paragraph below
        /// is kept because it is the record of why the style existed before anything could use it,
        /// and of the claim it was written to test.
        ///
        /// <para>It exists because the owner asked for the motion (2026-09-16)
        /// and because a hammer is the cheapest possible test of the claim this file makes: that a
        /// third kind of work should cost "a new static here and a row in
        /// <see cref="IndexForJob"/>, and nothing else". It cost a static, a recipe and a
        /// catalogue row. The claim holds.</para>
        ///
        /// <para><c>PawnFigureDirector.StyleOverride</c> is still the harness's way in, and is what
        /// <c>SwingCheck</c> photographs the stroke with; it is no longer the only way in.</para>
        ///
        /// <para><b>Two-handed, on the owner's "akin to chopping".</b> A framing hammer swung at a
        /// wall with both fists is the axe's motion with a shorter tool, and it reuses every part
        /// of the fitting path unchanged. A one-handed hammer — the other hand steadying a nail —
        /// is a different thing and would be the first tool in the project to need a
        /// <c>TwoHanded</c> flag, because <c>TwoBoneIk</c> currently puts the off hand on the haft
        /// unconditionally. §10 of <c>13-gestures.md</c> keeps that question.</para>
        ///
        /// <para>Aimed at the near face like mining and not at the cell centre like felling: a
        /// wall under construction fills its cell, so a stroke aimed 0.15 m past the middle of it
        /// finishes a metre inside the timber. That is the <see cref="AimFromCentre"/> bug, and it
        /// would have been made a second time here by copying the wrong one of the two.</para>
        /// </summary>
        public static readonly WorkStyle Building = new WorkStyle(
            WorkStroke.Hammer, ModuleIds.ToolHammer, ChipRecipe.Timber,
            aimFromCentre: CellMetrics.SizeXZ * 0.5f - 0.12f,
            // Between the axe's -30 and the pick's -8: across the body, but a short haft cannot
            // travel as far round as a long one without the elbow leaving the plane.
            // A 0.63 m haft has little to slide along, and a hammer is swung from the elbow.
            tilt: -20f, gripFraction: 0.24f, bladeRoll: 0f, bladeYaw: 0f, buttFraction: 0.06f,
            slideFraction: 0.40f,
            // Less than mining's 0.22: a timber frame is open work rather than an opaque block, so
            // the head does not disappear into it and the burst needs backing out much less far.
            chipStandOff: 0.1f,
            // A builder works the joists under its feet and the plate over its head, exactly as a
            // miner works the layer below and the ceiling above, so both aims are wanted.
            dip: 45f, raise: -55f);

        /// <summary>How many styles there are. Sizes the per-figure tool table.</summary>
        public const int Count = 3;

        public const int FellingIndex = 0;
        public const int MiningIndex = 1;
        public const int BuildingIndex = 2;

        /// <summary>The styles, by index. Mutable so a contact sheet can tune one and re-fit.</summary>
        public static readonly WorkStyle[] All = { Felling, Mining, Building };

        /// <summary>
        /// Which style a job is worked in, from the job def the snapshot already publishes.
        ///
        /// <para><b>Option A of the design note's three, and it is free.</b> No contract change, no
        /// new virtual on the driver, no simulation work at all — exactly as
        /// <c>JobLabels.IconKeys</c> is already a table parallel to <c>JobIndex</c>. It also puts
        /// the art vocabulary on the side of the seam the architecture already puts it: a stroke is
        /// the same kind of thing as a word, and <c>PawnView.JobDef</c>'s own comment says
        /// presentation is where an index becomes words.</para>
        ///
        /// <para>The observation that would overturn it is a single job index needing two different
        /// strokes. None exists today. If one ever does, it becomes a byte in the view and a
        /// virtual on the driver, and nothing written here is wasted.</para>
        /// </summary>
        public static int IndexForJob(int jobDef) =>
            jobDef == JobHandle.Mine ? MiningIndex
            : jobDef == JobHandle.Build ? BuildingIndex
            : FellingIndex;

        /// <summary>The same, resolved. Anything that is not mining swings an axe.</summary>
        public static WorkStyle ForJob(int jobDef) => All[IndexForJob(jobDef)];

        /// <summary>A copy with one fitting number changed, for tuning off a contact sheet.</summary>
        public WorkStyle With(float? tilt = null, float? gripFraction = null,
            float? bladeRoll = null, float? bladeYaw = null, float? buttFraction = null,
            float? slideFraction = null) =>
            new WorkStyle(Stroke, ToolModule, Chips, AimFromCentre,
                tilt ?? Tilt, gripFraction ?? GripFraction, bladeRoll ?? BladeRoll,
                bladeYaw ?? BladeYaw, buttFraction ?? ButtFraction, slideFraction ?? SlideFraction,
                ChipStandOff, Dip, Raise);
    }
}
