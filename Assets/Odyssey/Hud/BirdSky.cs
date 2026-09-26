#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// A place a bird can sit (design 50 §6): the top of a drawn tree crown or a roof, in metres.
    /// </summary>
    public readonly struct BirdPerch
    {
        public BirdPerch(float x, float y, float z, float radius, int key)
        {
            X = x;
            Y = y;
            Z = z;
            Radius = radius;
            Key = key;
        }

        /// <summary>The middle of the top, in world metres.</summary>
        public readonly float X, Y, Z;

        /// <summary>How far from the middle a bird may sit: a crown's reach, or half a roof cell.</summary>
        public readonly float Radius;

        /// <summary>Which column it is, so whoever answered can say later whether it still holds.</summary>
        public readonly int Key;
    }

    /// <summary>
    /// <b>The seam between the flock and the world.</b> The game answers it from the render mirror
    /// (<c>BirdPerches</c>), and the tests answer it from a fake. Nothing else about the board
    /// reaches the birds.
    /// </summary>
    public interface IBirdPerches
    {
        /// <summary>
        /// Up to <c>into.Length</c> perches within <paramref name="radius"/> metres of (x, z), nearest
        /// first. Returns how many were written.
        /// </summary>
        int Near(float x, float z, float radius, BirdPerch[] into);

        /// <summary>The height in metres of the highest drawn thing over (x, z): ground, roof or crown.</summary>
        float Ceiling(float x, float z);

        /// <summary>Whether a perch is still there and still drawn (a felled tree, a roof cut away by the slice).</summary>
        bool Holds(in BirdPerch perch);
    }

    /// <summary>The sky the birds are flying in this step.</summary>
    public readonly struct BirdConditions
    {
        public BirdConditions(float hour, WeatherKind weather, float rain)
        {
            Hour = hour;
            Weather = weather;
            Rain = rain;
        }

        /// <summary>The hour of the day, 0 to 24, continuous.</summary>
        public readonly float Hour;

        public readonly WeatherKind Weather;

        /// <summary>How hard it is raining, 0 to 1.</summary>
        public readonly float Rain;

        public static BirdConditions ClearNoon => new BirdConditions(12f, WeatherKind.Clear, 0f);
    }

    public enum BirdState : byte
    {
        /// <summary>In the air with its flock.</summary>
        Flying = 0,

        /// <summary>In the air, making for its own perch.</summary>
        Landing = 1,

        /// <summary>Sitting on a crown or a roof, wings folded.</summary>
        Perched = 2,

        /// <summary>Off the board. Not drawn.</summary>
        Away = 3,
    }

    /// <summary>
    /// One bird as the drawing reads it. Position and velocity in world metres; yaw, pitch and bank in
    /// radians (yaw 0 is +Z and turns towards +X, pitch is nose up, bank is the right wing down);
    /// phase, flap and fold for the shader.
    /// </summary>
    public struct Bird
    {
        public float X, Y, Z;
        public float VX, VY, VZ;
        public float Yaw, Pitch, Bank;

        /// <summary>Where in its wingbeat this bird is, so a flock does not flap in step.</summary>
        public float Phase;

        /// <summary>How hard it is beating, 0 (gliding) to about 1.</summary>
        public float Flap;

        /// <summary>How far its wings are folded, 0 in flight to 1 on a perch.</summary>
        public float Fold;

        public BirdState State;
        public BirdKind Kind;
        public int Flock;

        // The flock's own bookkeeping, public so a test can read it.
        public float SlotX, SlotY, SlotZ;
        public float PerchX, PerchY, PerchZ, PerchYaw;
        public BirdPerch Perch;
        public float GlideSeed;
    }

    public enum FlockMode : byte
    {
        Cruising = 0,
        Landing = 1,
        Perched = 2,
        Scattered = 3,
        Leaving = 4,
        Away = 5,
    }

    /// <summary>
    /// A flock is a <b>leader point</b>, not a bird (design 50 §5): a centre drifting across the board,
    /// with the leader circling it. The birds steer at the leader plus their own slot.
    /// </summary>
    public sealed class BirdFlock
    {
        public BirdKind Kind;
        public int First, Count;
        public FlockMode Mode;

        /// <summary>Down for the night at the rookery, not just resting.</summary>
        public bool Roosting;

        public float Timer;
        public float CentreX, CentreZ, WayX, WayZ, ExitX, ExitZ;
        public float Angle, Radius, Turn, Altitude, Seed;
        public float LeaderX, LeaderY, LeaderZ;
        public float Ceiling, CeilingTimer, HoldTimer;
    }

    /// <summary>
    /// <b>Every bird in the sky, stepped on game time</b> (design 50 §5). Engine-free so the fast tier
    /// runs it; <c>BirdDirector</c> feeds it the hour, the weather and who is walking where, and draws
    /// what it leaves.
    ///
    /// <para><b>Presentation only.</b> Nothing here is saved or hashed, and no simulated system asks
    /// about a bird. It is seeded so that the same board gives the same sky twice (a screenshot tool
    /// wants that), which is all the determinism it needs.</para>
    ///
    /// <para><b>Cost is bounded by construction.</b> Separation is within a flock of at most 14, so the
    /// quadratic term is 14 squared per flock and never the sky squared. The whole sky is at most
    /// <see cref="MaxBirds"/>. A perch search runs only when a flock comes down.</para>
    /// </summary>
    public sealed class BirdSky
    {
        /// <summary>The most birds any board can have, whatever its size.</summary>
        public const int MaxBirds = 80;

        /// <summary>A walker this close, horizontally, puts a perched flock up.</summary>
        public const float ScareRadius = 8f;

        /// <summary>…if it is also within this many metres vertically (a colonist a storey down does not).</summary>
        public const float ScareHeight = 10f;

        /// <summary>A step is split to this many seconds, and never more than <see cref="MaxSteps"/> a frame.</summary>
        public const float StepSeconds = 0.1f;

        public const int MaxSteps = 5;

        /// <summary>Rooks go to roost at this hour and come out at <see cref="RiseHour"/>.</summary>
        public const float RoostHour = 19f, RiseHour = 6f;

        /// <summary>The buzzard is out between these hours, in clear or cloudy weather.</summary>
        public const float BuzzardOutHour = 8f, BuzzardInHour = 17.5f;

        /// <summary>How far beyond the board's edge a leaving flock goes before it counts as away.</summary>
        public const float ExitMargin = 40f;

        /// <summary>How far from a flock's centre it looks for somewhere to land.</summary>
        public const float LandingSearch = 30f;

        const float Tau = 6.2831853f;

        readonly IBirdPerches _perches;
        readonly float _width, _depth;
        readonly Bird[] _birds;
        readonly List<BirdFlock> _flocks = new List<BirdFlock>();
        readonly BirdPerch[] _found = new BirdPerch[8];
        readonly List<(float X0, float Z0, float X1, float Z1)> _changes = new List<(float, float, float, float)>();
        readonly int _count;
        uint _rng;
        bool _settled;
        bool _rookerySearched;
        bool _hasRookery;
        float _rookeryX, _rookeryZ;

        /// <summary>
        /// A sky over a board <paramref name="widthMetres"/> by <paramref name="depthMetres"/>, with its
        /// flocks placed and cruising. The first <see cref="Step"/> settles them for the hour it is
        /// (a colony loaded at night starts with its rooks already at roost).
        /// </summary>
        public BirdSky(float widthMetres, float depthMetres, IBirdPerches perches, uint seed)
        {
            _perches = perches;
            _width = Math.Max(1f, widthMetres);
            _depth = Math.Max(1f, depthMetres);
            _rng = seed == 0 ? 0x9E3779B9u : seed;
            _birds = new Bird[MaxBirds];

            FlocksFor(_width, _depth, out int rookFlocks, out int buzzards);
            int next = 0;
            for (int i = 0; i < rookFlocks; i++)
                next = AddFlock(BirdKind.Rook, 7 + (int)(Next() * 8f), next);
            for (int i = 0; i < buzzards; i++)
                next = AddFlock(BirdKind.Buzzard, 1, next);
            _count = next;
        }

        /// <summary>
        /// How many rook flocks and buzzards a board carries: two flocks and one buzzard on Standard
        /// (300 m square), scaled by area and held to one to five flocks and one to three buzzards.
        /// </summary>
        public static void FlocksFor(float widthMetres, float depthMetres, out int rookFlocks, out int buzzards)
        {
            float ratio = widthMetres * depthMetres / (300f * 300f);
            rookFlocks = Clamp((int)Math.Round(2f * ratio), 1, 5);
            buzzards = Clamp((int)Math.Round(ratio), 1, 3);
        }

        public ReadOnlySpan<Bird> Birds => new ReadOnlySpan<Bird>(_birds, 0, _count);

        public IReadOnlyList<BirdFlock> Flocks => _flocks;

        public int Count => _count;

        /// <summary>Game seconds this sky has run.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Whether a rookery has been found on this board, and where.</summary>
        public bool HasRookery => _hasRookery;
        public float RookeryX => _rookeryX;
        public float RookeryZ => _rookeryZ;

        /// <summary>How many birds are in <paramref name="state"/>.</summary>
        public int CountIn(BirdState state)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
                if (_birds[i].State == state) n++;
            return n;
        }

        /// <summary>Rooks are out between <see cref="RiseHour"/> and <see cref="RoostHour"/>.</summary>
        public static bool RookDay(float hour) => hour >= RiseHour && hour < RoostHour;

        /// <summary>The buzzard is out by day, in clear or cloudy weather.</summary>
        public static bool BuzzardOut(in BirdConditions sky) =>
            sky.Hour >= BuzzardOutHour && sky.Hour < BuzzardInHour &&
            (sky.Weather == WeatherKind.Clear || sky.Weather == WeatherKind.Cloudy);

        /// <summary>
        /// Part of the board changed between (x0, z0) and (x1, z1), in metres: a tree came down, a roof
        /// went up or in. Birds sitting on it go up on the next step.
        /// </summary>
        public void Disturb(float x0, float z0, float x1, float z1) => _changes.Add((x0, z0, x1, z1));

        /// <summary>
        /// Advance the sky by <paramref name="seconds"/> of game time. <paramref name="walkers"/> is
        /// every colonist and animal as x, y, z triples in metres. Zero seconds (paused) moves
        /// nothing and forgets nothing.
        /// </summary>
        public void Step(float seconds, in BirdConditions sky, ReadOnlySpan<float> walkers)
        {
            if (!(seconds > 0f)) return;

            if (!_settled)
            {
                Settle(sky);
                _settled = true;
            }

            int steps = Math.Min(MaxSteps, Math.Max(1, (int)Math.Ceiling(seconds / StepSeconds)));
            float dt = Math.Min(StepSeconds, seconds / steps);
            for (int s = 0; s < steps; s++)
                StepOnce(dt, sky, walkers);
            _changes.Clear();
        }

        void StepOnce(float dt, in BirdConditions sky, ReadOnlySpan<float> walkers)
        {
            Elapsed += dt;
            if (RookDay(sky.Hour)) _rookerySearched = false;

            for (int f = 0; f < _flocks.Count; f++)
                UpdateFlock(_flocks[f], dt, sky, walkers);

            for (int f = 0; f < _flocks.Count; f++)
            {
                BirdFlock flock = _flocks[f];
                for (int i = flock.First; i < flock.First + flock.Count; i++)
                    UpdateBird(i, flock, dt);
                if (flock.Mode == FlockMode.Landing && AllPerched(flock))
                {
                    flock.Mode = FlockMode.Perched;
                    flock.HoldTimer = 0.5f;
                    flock.Timer = Range(12f, 35f) * (1f + 2f * Clamp01(sky.Rain));
                }
            }
        }

        // ---------------------------------------------------------------- flocks

        int AddFlock(BirdKind kind, int size, int first)
        {
            size = Math.Min(size, MaxBirds - first);
            if (size <= 0) return first;
            bool buzzard = kind == BirdKind.Buzzard;
            var flock = new BirdFlock
            {
                Kind = kind,
                First = first,
                Count = size,
                Mode = FlockMode.Cruising,
                CentreX = Range(0.2f, 0.8f) * _width,
                CentreZ = Range(0.2f, 0.8f) * _depth,
                Angle = Next() * Tau,
                Radius = buzzard ? Range(25f, 35f) : Range(18f, 28f),
                Turn = Next() < 0.5f ? -1f : 1f,
                Altitude = buzzard ? Range(28f, 38f) : Range(9f, 14f),
                Seed = Next() * Tau,
                Timer = Range(5f, 25f),
            };
            PickWaypoint(flock);
            flock.Ceiling = _perches.Ceiling(flock.CentreX, flock.CentreZ);
            PlaceLeader(flock);
            flock.LeaderY = flock.Ceiling + flock.Altitude;
            _flocks.Add(flock);

            float spread = 2f + size * 0.35f;
            for (int k = 0; k < size; k++)
            {
                ref Bird b = ref _birds[first + k];
                b.Kind = kind;
                b.Flock = _flocks.Count - 1;
                b.SlotX = buzzard ? 0f : Range(-spread, spread);
                b.SlotY = buzzard ? 0f : Range(-1.5f, 1.5f);
                b.SlotZ = buzzard ? 0f : Range(-spread, spread);
                b.Phase = Next() * Tau;
                b.GlideSeed = Next();
                b.X = flock.LeaderX + b.SlotX;
                b.Y = flock.LeaderY + b.SlotY;
                b.Z = flock.LeaderZ + b.SlotZ;
                b.State = BirdState.Flying;
                b.Flap = 0.8f;
            }
            return first + size;
        }

        /// <summary>Put every flock where the hour and the weather say it should already be.</summary>
        void Settle(in BirdConditions sky)
        {
            foreach (BirdFlock flock in _flocks)
            {
                bool rook = flock.Kind == BirdKind.Rook;
                bool allowed = rook ? RookDay(sky.Hour) : BuzzardOut(sky);
                if (!allowed)
                {
                    if (rook && TryRoost(flock)) SnapToPerches(flock);
                    else GoAway(flock);
                }
                else if (rook && sky.Weather == WeatherKind.Storm && TryLand(flock))
                {
                    SnapToPerches(flock);
                }
            }
        }

        void UpdateFlock(BirdFlock flock, float dt, in BirdConditions sky, ReadOnlySpan<float> walkers)
        {
            bool rook = flock.Kind == BirdKind.Rook;
            bool allowed = rook ? RookDay(sky.Hour) : BuzzardOut(sky);
            bool storm = sky.Weather == WeatherKind.Storm;
            bool grounded = flock.Mode == FlockMode.Perched || flock.Mode == FlockMode.Landing;

            if (grounded)
            {
                if (ChangedUnder(flock, out float cx, out float cz)) Scatter(flock, cx, cz);
                else if (Scared(flock, walkers, out float wx, out float wz)) Scatter(flock, wx, wz);
                else if (flock.Mode == FlockMode.Perched)
                {
                    flock.HoldTimer -= dt;
                    if (flock.HoldTimer <= 0f)
                    {
                        flock.HoldTimer = 0.5f;
                        if (LostPerch(flock, out float lx, out float lz)) Scatter(flock, lx, lz);
                    }
                }
            }

            switch (flock.Mode)
            {
                case FlockMode.Cruising:
                    if (!allowed)
                    {
                        if (!rook || !TryRoost(flock)) BeginLeaving(flock);
                        break;
                    }
                    if (rook && storm)
                    {
                        if (!TryLand(flock)) Advance(flock, dt);
                        break;
                    }
                    flock.Timer -= dt * (1f + Clamp01(sky.Rain));
                    if (rook && flock.Timer <= 0f && !TryLand(flock)) flock.Timer = Range(8f, 15f);
                    Advance(flock, dt);
                    break;

                case FlockMode.Landing:
                    Advance(flock, dt);
                    break;

                case FlockMode.Perched:
                    if (flock.Roosting)
                    {
                        if (allowed && !storm) TakeOff(flock);
                    }
                    else if (!allowed)
                    {
                        // Dusk found them resting somewhere else: up, and off to the rookery.
                        if (!TryRoost(flock)) BeginLeaving(flock);
                    }
                    else if (!(rook && storm))
                    {
                        flock.Timer -= dt;
                        if (flock.Timer <= 0f) TakeOff(flock);
                    }
                    break;

                case FlockMode.Scattered:
                    flock.Timer -= dt;
                    Advance(flock, dt);
                    if (flock.Timer <= 0f)
                    {
                        flock.Mode = FlockMode.Cruising;
                        flock.Timer = Range(6f, 12f);
                    }
                    break;

                case FlockMode.Leaving:
                    if (allowed && !(rook && storm))
                    {
                        flock.Mode = FlockMode.Cruising;
                        flock.Timer = Range(10f, 30f);
                        PickWaypoint(flock);
                        break;
                    }
                    Advance(flock, dt);
                    if (AllOutside(flock)) GoAway(flock);
                    break;

                case FlockMode.Away:
                    if (allowed && !(rook && storm)) Return(flock);
                    break;
            }
        }

        /// <summary>Move the flock's centre towards its waypoint, or its exit, and the leader round it.</summary>
        void Advance(BirdFlock flock, float dt)
        {
            BirdSpecies species = BirdSpecies.Of(flock.Kind);
            bool leaving = flock.Mode == FlockMode.Leaving;
            float tx = leaving ? flock.ExitX : flock.WayX, tz = leaving ? flock.ExitZ : flock.WayZ;
            float drift = leaving ? species.CruiseSpeed * 0.8f : flock.Kind == BirdKind.Buzzard ? 1.2f : 2.5f;
            float dx = tx - flock.CentreX, dz = tz - flock.CentreZ;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d > 0.01f)
            {
                float move = Math.Min(d, drift * dt);
                flock.CentreX += dx / d * move;
                flock.CentreZ += dz / d * move;
            }
            if (!leaving && d < 10f) PickWaypoint(flock);

            flock.Angle += flock.Turn * species.CruiseSpeed / Math.Max(flock.Radius, 1f) * dt;
            if (leaving) flock.Radius = Math.Max(0f, flock.Radius - 5f * dt);
            PlaceLeader(flock);

            flock.CeilingTimer -= dt;
            if (flock.CeilingTimer <= 0f)
            {
                flock.CeilingTimer = 0.5f;
                flock.Ceiling = Math.Max(_perches.Ceiling(flock.LeaderX, flock.LeaderZ),
                    _perches.Ceiling(flock.CentreX, flock.CentreZ));
            }
            float wanted = flock.Ceiling + flock.Altitude + 2f * (float)Math.Sin(Elapsed * 0.3f + flock.Seed);
            flock.LeaderY += (wanted - flock.LeaderY) * Math.Min(1f, dt * 0.5f);
        }

        void PlaceLeader(BirdFlock flock)
        {
            flock.LeaderX = flock.CentreX + (float)Math.Sin(flock.Angle) * flock.Radius;
            flock.LeaderZ = flock.CentreZ + (float)Math.Cos(flock.Angle) * flock.Radius;
        }

        void PickWaypoint(BirdFlock flock)
        {
            float margin = Math.Min(15f, Math.Min(_width, _depth) * 0.25f);
            flock.WayX = Range(margin, _width - margin);
            flock.WayZ = Range(margin, _depth - margin);
        }

        /// <summary>Come down near the flock's centre. False when there is nowhere to sit.</summary>
        bool TryLand(BirdFlock flock)
        {
            int found = _perches.Near(flock.CentreX, flock.CentreZ, LandingSearch, _found);
            if (found <= 0) return false;
            AssignPerches(flock, Math.Min(found, 3));
            flock.Roosting = false;
            flock.Mode = FlockMode.Landing;
            return true;
        }

        /// <summary>Go to the rookery for the night. False when this board has none.</summary>
        bool TryRoost(BirdFlock flock)
        {
            if (!_rookerySearched) FindRookery();
            if (!_hasRookery) return false;
            int found = _perches.Near(_rookeryX, _rookeryZ, 25f, _found);
            if (found <= 0) return false;
            AssignPerches(flock, found);
            flock.Roosting = true;
            flock.Mode = FlockMode.Landing;
            flock.CentreX = _rookeryX;
            flock.CentreZ = _rookeryZ;
            return true;
        }

        /// <summary>
        /// Choose the stand the rooks roost in: the first perch found near a rook flock, else near
        /// the middle of the board. Chosen once a night, so a felled rookery is replaced by the next.
        /// </summary>
        void FindRookery()
        {
            _rookerySearched = true;
            _hasRookery = false;
            foreach (BirdFlock flock in _flocks)
            {
                if (flock.Kind != BirdKind.Rook) continue;
                if (TryRookeryNear(flock.CentreX, flock.CentreZ)) return;
            }
            TryRookeryNear(_width * 0.5f, _depth * 0.5f);
        }

        bool TryRookeryNear(float x, float z)
        {
            int found = _perches.Near(x, z, 80f, _found);
            if (found <= 0) return false;
            _hasRookery = true;
            _rookeryX = _found[0].X;
            _rookeryZ = _found[0].Z;
            return true;
        }

        /// <summary>Deal the flock's birds round the first <paramref name="perches"/> perches found.</summary>
        void AssignPerches(BirdFlock flock, int perches)
        {
            for (int k = 0; k < flock.Count; k++)
            {
                ref Bird b = ref _birds[flock.First + k];
                BirdPerch perch = _found[k % perches];
                float angle = Next() * Tau;
                float r = (float)Math.Sqrt(Next()) * perch.Radius * 0.55f;
                b.Perch = perch;
                b.PerchX = perch.X + (float)Math.Cos(angle) * r;
                b.PerchZ = perch.Z + (float)Math.Sin(angle) * r;
                // A crown is domed: the further out a bird sits, the lower it sits.
                b.PerchY = perch.Y - 0.35f * (perch.Radius > 0.01f ? r / perch.Radius : 0f);
                b.PerchYaw = Next() * Tau;
                if (b.State == BirdState.Perched) b.VY = 3f;
                if (b.State != BirdState.Away) b.State = BirdState.Landing;
            }
        }

        /// <summary>Put every bird straight onto its perch: for a sky that starts at night.</summary>
        void SnapToPerches(BirdFlock flock)
        {
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                b.X = b.PerchX;
                b.Y = b.PerchY;
                b.Z = b.PerchZ;
                b.VX = b.VY = b.VZ = 0f;
                b.Yaw = b.PerchYaw;
                b.Fold = 1f;
                b.Flap = 0f;
                b.State = BirdState.Perched;
            }
            flock.Mode = FlockMode.Perched;
            flock.HoldTimer = 0.5f;
            flock.Timer = Range(12f, 35f);
        }

        void TakeOff(BirdFlock flock)
        {
            flock.Mode = FlockMode.Cruising;
            flock.Roosting = false;
            flock.Timer = Range(20f, 60f);
            float sx = 0f, sz = 0f;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                sx += b.X;
                sz += b.Z;
                b.State = BirdState.Flying;
                b.VY = Range(3f, 5f);
                b.VX = Range(-2f, 2f);
                b.VZ = Range(-2f, 2f);
            }
            flock.CentreX = sx / flock.Count;
            flock.CentreZ = sz / flock.Count;
            flock.LeaderY = Math.Max(flock.LeaderY, _birds[flock.First].Y + 4f);
            PickWaypoint(flock);
        }

        /// <summary>
        /// Everybody up at once, away from what startled them, climbing for three seconds before they
        /// rejoin the flock in flight.
        /// </summary>
        void Scatter(BirdFlock flock, float fromX, float fromZ)
        {
            float sx = 0f, sz = 0f;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                sx += b.X;
                sz += b.Z;
                float dx = b.X - fromX, dz = b.Z - fromZ;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 0.1f)
                {
                    float a = Next() * Tau;
                    dx = (float)Math.Cos(a);
                    dz = (float)Math.Sin(a);
                    d = 1f;
                }
                b.VX = dx / d * 6f + Range(-1.5f, 1.5f);
                b.VZ = dz / d * 6f + Range(-1.5f, 1.5f);
                b.VY = Range(5f, 7f);
                b.State = BirdState.Flying;
                b.Flap = 1f;
            }
            flock.Mode = FlockMode.Scattered;
            flock.Timer = 3f;
            flock.CentreX = sx / flock.Count;
            flock.CentreZ = sz / flock.Count;
            flock.LeaderY = Math.Max(flock.LeaderY, _birds[flock.First].Y + 6f);
            PickWaypoint(flock);
        }

        void BeginLeaving(BirdFlock flock)
        {
            flock.Mode = FlockMode.Leaving;
            flock.Roosting = false;
            // Out by the nearest edge.
            float toWest = flock.CentreX, toEast = _width - flock.CentreX;
            float toSouth = flock.CentreZ, toNorth = _depth - flock.CentreZ;
            float nearest = Math.Min(Math.Min(toWest, toEast), Math.Min(toSouth, toNorth));
            flock.ExitX = flock.CentreX;
            flock.ExitZ = flock.CentreZ;
            if (nearest == toWest) flock.ExitX = -ExitMargin;
            else if (nearest == toEast) flock.ExitX = _width + ExitMargin;
            else if (nearest == toSouth) flock.ExitZ = -ExitMargin;
            else flock.ExitZ = _depth + ExitMargin;

            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                if (b.State == BirdState.Perched) b.VY = 3f;
                if (b.State != BirdState.Away) b.State = BirdState.Flying;
            }
        }

        void GoAway(BirdFlock flock)
        {
            flock.Mode = FlockMode.Away;
            flock.Roosting = false;
            if (flock.ExitX == 0f && flock.ExitZ == 0f)
            {
                flock.ExitX = -ExitMargin;
                flock.ExitZ = flock.CentreZ;
            }
            for (int i = flock.First; i < flock.First + flock.Count; i++)
                _birds[i].State = BirdState.Away;
        }

        /// <summary>Back over the board from where the flock left, making for somewhere inside.</summary>
        void Return(BirdFlock flock)
        {
            flock.Mode = FlockMode.Cruising;
            flock.Timer = Range(20f, 45f);
            flock.CentreX = flock.ExitX;
            flock.CentreZ = flock.ExitZ;
            flock.Radius = flock.Kind == BirdKind.Buzzard ? Range(25f, 35f) : Range(18f, 28f);
            PickWaypoint(flock);
            PlaceLeader(flock);
            flock.Ceiling = _perches.Ceiling(Clamp(flock.WayX, 0f, _width), Clamp(flock.WayZ, 0f, _depth));
            flock.LeaderY = flock.Ceiling + flock.Altitude;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                b.X = flock.LeaderX + b.SlotX;
                b.Y = flock.LeaderY + b.SlotY;
                b.Z = flock.LeaderZ + b.SlotZ;
                float dx = flock.WayX - b.X, dz = flock.WayZ - b.Z;
                float d = Math.Max(0.1f, (float)Math.Sqrt(dx * dx + dz * dz));
                float speed = BirdSpecies.Of(flock.Kind).CruiseSpeed;
                b.VX = dx / d * speed;
                b.VZ = dz / d * speed;
                b.VY = 0f;
                b.Fold = 0f;
                b.State = BirdState.Flying;
            }
        }

        bool AllPerched(BirdFlock flock)
        {
            for (int i = flock.First; i < flock.First + flock.Count; i++)
                if (_birds[i].State != BirdState.Perched) return false;
            return true;
        }

        bool AllOutside(BirdFlock flock)
        {
            const float margin = 10f;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                bool inside = b.X > -margin && b.X < _width + margin && b.Z > -margin && b.Z < _depth + margin;
                if (inside) return false;
            }
            return true;
        }

        bool ChangedUnder(BirdFlock flock, out float x, out float z)
        {
            x = z = 0f;
            if (_changes.Count == 0) return false;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                if (b.State != BirdState.Perched && b.State != BirdState.Landing) continue;
                foreach (var change in _changes)
                {
                    // A cell's width of slack: a crown reaches past its own column.
                    if (b.PerchX < change.X0 - 2.5f || b.PerchX > change.X1 + 2.5f) continue;
                    if (b.PerchZ < change.Z0 - 2.5f || b.PerchZ > change.Z1 + 2.5f) continue;
                    x = b.PerchX;
                    z = b.PerchZ;
                    return true;
                }
            }
            return false;
        }

        bool Scared(BirdFlock flock, ReadOnlySpan<float> walkers, out float x, out float z)
        {
            x = z = 0f;
            const float r2 = ScareRadius * ScareRadius;
            for (int w = 0; w + 2 < walkers.Length; w += 3)
            {
                float wx = walkers[w], wy = walkers[w + 1], wz = walkers[w + 2];
                for (int i = flock.First; i < flock.First + flock.Count; i++)
                {
                    ref Bird b = ref _birds[i];
                    if (b.State != BirdState.Perched) continue;
                    float dx = wx - b.X, dz = wz - b.Z;
                    if (dx * dx + dz * dz > r2 || Math.Abs(wy - b.Y) > ScareHeight) continue;
                    x = wx;
                    z = wz;
                    return true;
                }
            }
            return false;
        }

        bool LostPerch(BirdFlock flock, out float x, out float z)
        {
            x = z = 0f;
            for (int i = flock.First; i < flock.First + flock.Count; i++)
            {
                ref Bird b = ref _birds[i];
                if (b.State != BirdState.Perched || _perches.Holds(b.Perch)) continue;
                x = b.PerchX;
                z = b.PerchZ;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- birds

        void UpdateBird(int index, BirdFlock flock, float dt)
        {
            ref Bird b = ref _birds[index];
            if (b.State == BirdState.Away) return;
            BirdSpecies species = BirdSpecies.Of(b.Kind);

            if (b.State == BirdState.Perched)
            {
                b.X = b.PerchX;
                b.Y = b.PerchY;
                b.Z = b.PerchZ;
                b.VX = b.VY = b.VZ = 0f;
                b.Yaw = b.PerchYaw;
                b.Pitch = 0f;
                b.Bank = 0f;
                b.Flap = 0f;
                b.Fold = Math.Min(1f, b.Fold + dt * 2.5f);
                return;
            }

            bool arriving = flock.Mode == FlockMode.Landing && b.State == BirdState.Landing;
            if (!arriving && b.State == BirdState.Landing) b.State = BirdState.Flying;

            float tx, ty, tz;
            if (arriving)
            {
                tx = b.PerchX;
                ty = b.PerchY;
                tz = b.PerchZ;
            }
            else if (flock.Mode == FlockMode.Scattered && flock.Timer > 1.8f)
            {
                // Still bolting: away along the line they left on, and up.
                tx = b.X + b.VX;
                ty = b.Y + 3f;
                tz = b.Z + b.VZ;
            }
            else
            {
                tx = flock.LeaderX + b.SlotX;
                ty = flock.LeaderY + b.SlotY;
                tz = flock.LeaderZ + b.SlotZ;
            }

            float dx = tx - b.X, dy = ty - b.Y, dz = tz - b.Z;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (arriving && dist < 0.35f)
            {
                b.State = BirdState.Perched;
                b.X = b.PerchX;
                b.Y = b.PerchY;
                b.Z = b.PerchZ;
                b.VX = b.VY = b.VZ = 0f;
                return;
            }

            float want = arriving
                ? Clamp(dist * 0.9f, 1.5f, species.MaxSpeed)
                : Math.Min(species.MaxSpeed, Math.Max(species.CruiseSpeed * 0.6f, dist * 1.2f));
            float inv = dist > 0.0001f ? 1f / dist : 0f;
            float steer = arriving ? 3f : flock.Mode == FlockMode.Scattered ? 1.2f : 2f;
            float ax = (dx * inv * want - b.VX) * steer;
            float ay = (dy * inv * want - b.VY) * steer;
            float az = (dz * inv * want - b.VZ) * steer;

            // Keep apart from the rest of this flock only: n squared within a flock, never the sky's.
            if (!(arriving && dist < 3f))
            {
                for (int j = flock.First; j < flock.First + flock.Count; j++)
                {
                    if (j == index) continue;
                    ref Bird o = ref _birds[j];
                    if (o.State == BirdState.Perched || o.State == BirdState.Away) continue;
                    float ex = b.X - o.X, ey = b.Y - o.Y, ez = b.Z - o.Z;
                    float e2 = ex * ex + ey * ey + ez * ez;
                    if (e2 >= 4f || e2 < 0.0001f) continue;
                    ax += ex / e2 * 3f;
                    ay += ey / e2 * 3f;
                    az += ez / e2 * 3f;
                }
            }

            b.VX += ax * dt;
            b.VY += ay * dt;
            b.VZ += az * dt;

            // A bird in the air keeps its airspeed; only one coming in to sit may slow right down.
            float speed = (float)Math.Sqrt(b.VX * b.VX + b.VY * b.VY + b.VZ * b.VZ);
            if (!arriving && speed < 3f && speed > 0.0001f)
            {
                float lift = 3f / speed;
                b.VX *= lift;
                b.VY *= lift;
                b.VZ *= lift;
            }

            b.X += b.VX * dt;
            b.Y += b.VY * dt;
            b.Z += b.VZ * dt;

            if (!arriving && b.Y < flock.Ceiling + 2f)
            {
                b.Y = flock.Ceiling + 2f;
                if (b.VY < 0f) b.VY = 0f;
            }

            // Attitude: face along the flight, bank into the turn, nose with the climb.
            float horizontal = (float)Math.Sqrt(b.VX * b.VX + b.VZ * b.VZ);
            float bankWanted = 0f;
            if (horizontal > 0.3f)
            {
                float yaw = (float)Math.Atan2(b.VX, b.VZ);
                float turn = Wrap(yaw - b.Yaw);
                b.Yaw = Wrap(b.Yaw + turn * Math.Min(1f, dt * 5f));
                bankWanted = Clamp(turn * 1.2f, -0.8f, 0.8f);
            }
            b.Bank += (bankWanted - b.Bank) * Math.Min(1f, dt * 3f);
            float pitch = (float)Math.Atan2(b.VY, Math.Max(horizontal, 0.5f)) * 0.6f;
            b.Pitch += (pitch - b.Pitch) * Math.Min(1f, dt * 3f);

            // Beat hard climbing, bolting or coming in; otherwise glide some of the time by kind.
            bool working = b.VY > 0.8f || flock.Mode == FlockMode.Scattered || (arriving && dist < 6f);
            float wave = 0.5f + 0.5f * (float)Math.Sin(Elapsed * 0.35f + b.GlideSeed * Tau);
            float flap = working ? 1f : wave < species.GlideShare ? 0.05f : 0.85f;
            b.Flap += (flap - b.Flap) * Math.Min(1f, dt * 3f);
            b.Fold = Math.Max(0f, b.Fold - dt * 3f);
        }

        // ---------------------------------------------------------------- arithmetic

        float Next()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 16777216f;
        }

        float Range(float min, float max) => min + (max - min) * Next();

        static float Wrap(float angle)
        {
            while (angle > Math.PI) angle -= Tau;
            while (angle < -Math.PI) angle += Tau;
            return angle;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
