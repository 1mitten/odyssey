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

        /// <summary>How far up the haft from the fist the off hand sits, as a fraction.</summary>
        public readonly float OffHandSpacing;

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

        public WorkStyle(WorkStroke stroke, string toolModule, ChipRecipe chips, float aimFromCentre,
            float tilt, float gripFraction, float bladeRoll, float bladeYaw, float offHandSpacing,
            float chipStandOff = 0f, float dip = 0f)
        {
            ChipStandOff = chipStandOff;
            Dip = dip;
            Stroke = stroke;
            ToolModule = toolModule;
            Chips = chips;
            AimFromCentre = aimFromCentre;
            Tilt = tilt;
            GripFraction = gripFraction;
            BladeRoll = bladeRoll;
            BladeYaw = bladeYaw;
            OffHandSpacing = offHandSpacing;
        }

        /// <summary>Felling: the settled one. Every number came off a contact sheet.</summary>
        public static readonly WorkStyle Felling = new WorkStyle(
            WorkStroke.Axe, ModuleIds.ToolAxe, ChipRecipe.Wood,
            aimFromCentre: 0.15f,
            // Nothing: an axe bites a few centimetres into a trunk and the chips already read.
            tilt: -30f, gripFraction: 0.16f, bladeRoll: 0f, bladeYaw: 0f, offHandSpacing: 0.11f,
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
            tilt: -8f, gripFraction: 0.16f, bladeRoll: 180f, bladeYaw: 0f, offHandSpacing: 0.11f,
            // Out past the face and a little clear of it, so the lumps are seen leaving the rock
            // rather than appearing in mid-air once they have already cleared it.
            // 12 cm back out to the face the head went in through, and 10 cm clear of it.
            chipStandOff: 0.22f,
            // Bent over the hole when the rock is below. See Dip.
            dip: 45f);

        /// <summary>How many styles there are. Sizes the per-figure tool table.</summary>
        public const int Count = 2;

        public const int FellingIndex = 0;
        public const int MiningIndex = 1;

        /// <summary>The styles, by index. Mutable so a contact sheet can tune one and re-fit.</summary>
        public static readonly WorkStyle[] All = { Felling, Mining };

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
            jobDef == JobHandle.Mine ? MiningIndex : FellingIndex;

        /// <summary>The same, resolved. Anything that is not mining swings an axe.</summary>
        public static WorkStyle ForJob(int jobDef) => All[IndexForJob(jobDef)];

        /// <summary>A copy with one fitting number changed, for tuning off a contact sheet.</summary>
        public WorkStyle With(float? tilt = null, float? gripFraction = null,
            float? bladeRoll = null, float? bladeYaw = null, float? offHandSpacing = null) =>
            new WorkStyle(Stroke, ToolModule, Chips, AimFromCentre,
                tilt ?? Tilt, gripFraction ?? GripFraction, bladeRoll ?? BladeRoll,
                bladeYaw ?? BladeYaw, offHandSpacing ?? OffHandSpacing, ChipStandOff, Dip);
    }
}
