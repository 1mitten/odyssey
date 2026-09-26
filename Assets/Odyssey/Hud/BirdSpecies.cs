#nullable enable

namespace Odyssey.Hud
{
    /// <summary>The birds of the sky (design 50). Presentation only: nothing simulated knows them.</summary>
    public enum BirdKind : byte
    {
        Rook = 0,
        Buzzard = 1,
    }

    /// <summary>
    /// <b>What one kind of bird is</b> (design 50 §1–§2): its size, its colours, how its wings beat
    /// and how fast it flies. The shape is built from this by <see cref="BirdShape"/>, the flap is
    /// the shader's, and the flock rules are <see cref="BirdSky"/>'s.
    ///
    /// <para>Not a Def and not in the wiki, on purpose. A bird is never named on screen, is never
    /// saved and is never simulated, so it is drawing rather than content. If a bird ever gains a
    /// name a player reads, it moves to the Defs and the registry with it.</para>
    ///
    /// <para><b>Every number here was invented for the sketch and is the owner's to judge</b>, not a
    /// measurement. The span is the real bird's. The drawn size grows with zoom (<see cref="BirdScale"/>).</para>
    /// </summary>
    public sealed class BirdSpecies
    {
        public BirdSpecies(BirdKind kind, float span, float length, uint body, uint wing, uint under, uint beak,
            float beatsPerSecond, float amplitude, float glideShare, float cruiseSpeed, float maxSpeed,
            bool fingeredTips, float wingChord)
        {
            Kind = kind;
            Span = span;
            Length = length;
            Body = body;
            Wing = wing;
            Under = under;
            Beak = beak;
            BeatsPerSecond = beatsPerSecond;
            Amplitude = amplitude;
            GlideShare = glideShare;
            CruiseSpeed = cruiseSpeed;
            MaxSpeed = maxSpeed;
            FingeredTips = fingeredTips;
            WingChord = wingChord;
        }

        public BirdKind Kind { get; }

        /// <summary>Wingspan in metres, tip to tip, at life size.</summary>
        public float Span { get; }

        /// <summary>Beak to tail in metres.</summary>
        public float Length { get; }

        /// <summary>The four colours as 0xRRGGBB, authored in sRGB like every palette in the project.</summary>
        public uint Body { get; }
        public uint Wing { get; }
        public uint Under { get; }
        public uint Beak { get; }

        /// <summary>Wingbeats a second of game time while flapping.</summary>
        public float BeatsPerSecond { get; }

        /// <summary>How far a wingtip travels up and down, as a share of the span.</summary>
        public float Amplitude { get; }

        /// <summary>The share of level flight spent gliding rather than beating.</summary>
        public float GlideShare { get; }

        /// <summary>Metres a second the flock's leader travels.</summary>
        public float CruiseSpeed { get; }

        /// <summary>The fastest a bird of this kind is steered, metres a second.</summary>
        public float MaxSpeed { get; }

        /// <summary>Whether the wingtip splits into the fingered primaries a buzzard soars on.</summary>
        public bool FingeredTips { get; }

        /// <summary>The chord of the wingtip, as a share of the span: broad for a soaring bird.</summary>
        public float WingChord { get; }

        public static readonly BirdSpecies Rook = new BirdSpecies(BirdKind.Rook,
            span: 0.90f, length: 0.46f,
            body: 0x23242C, wing: 0x2B2E3A, under: 0x1B1C22, beak: 0x8D8F93,
            beatsPerSecond: 3.2f, amplitude: 0.28f, glideShare: 0.35f,
            cruiseSpeed: 9f, maxSpeed: 12f, fingeredTips: false, wingChord: 0.07f);

        public static readonly BirdSpecies Buzzard = new BirdSpecies(BirdKind.Buzzard,
            span: 1.25f, length: 0.55f,
            body: 0x6A4A32, wing: 0x5D402B, under: 0xCDB690, beak: 0x3A3530,
            beatsPerSecond: 2.2f, amplitude: 0.22f, glideShare: 0.9f,
            cruiseSpeed: 8f, maxSpeed: 10f, fingeredTips: true, wingChord: 0.13f);

        /// <summary>Every species, indexed by <see cref="BirdKind"/>.</summary>
        public static readonly BirdSpecies[] All = { Rook, Buzzard };

        public static BirdSpecies Of(BirdKind kind) => All[(int)kind];
    }
}
