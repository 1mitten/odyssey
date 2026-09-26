#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// <b>The ambient birds, drawn</b> (design 50): rooks and a buzzard over the board, one instanced
    /// call a species.
    ///
    /// <para>Owns the sky (<see cref="BirdSky"/>, engine-free and fast-tier tested), the perch source
    /// (<see cref="BirdPerches"/>), a mesh a species built from <see cref="BirdShape"/>, and a material
    /// a species on <c>Odyssey/Bird</c>. The bootstrap builds it with the session, calls
    /// <see cref="Sync"/> once a frame under its own frame section, and disposes it with the session.</para>
    ///
    /// <para><b>Nothing here reaches a cell, a save or the hash.</b> Birds cannot be clicked:
    /// the picker walks the grid, and a bird is not in it.</para>
    ///
    /// <para><b>Game time, not ticks.</b> The sky steps on the frame's seconds times the game speed,
    /// so a paused world holds every bird where it is and speed 3 flies three times as fast, as the
    /// rain does. It is not stepped per tick, because a bird at ten metres a second moved once a tick
    /// judders on a display faster than the tick rate.</para>
    /// </summary>
    public sealed class BirdDirector : IDisposable
    {
        static readonly int StateId = Shader.PropertyToID("_BirdState");
        static readonly int ClockId = Shader.PropertyToID("_BirdClock");
        static readonly int BeatsId = Shader.PropertyToID("_BirdBeats");
        static readonly int AmplitudeId = Shader.PropertyToID("_BirdAmplitude");

        /// <summary>The wing clock wraps every game hour: a whole number of beats for both species, so the seam is invisible.</summary>
        public const float ClockWrapSeconds = 3600f;

        /// <summary>The most colonists and animals the scare test reads in one frame.</summary>
        public const int MaxWalkers = 512;

        readonly WorldRenderModel _model;
        readonly BirdPerches _perches;
        readonly BirdSky _sky;
        readonly Mesh[] _meshes;
        readonly Material?[] _materials;
        readonly MaterialPropertyBlock[] _props;
        readonly Matrix4x4[][] _matrices;
        readonly List<Vector4>[] _states;
        readonly float[] _walkers = new float[MaxWalkers * 3];
        readonly int[] _counts;
        float _clock;

        public BirdDirector(WorldRenderModel model)
        {
            _model = model;
            _perches = new BirdPerches(model);
            float width = model.Size.SizeX * CellMetrics.SizeXZ, depth = model.Size.SizeZ * CellMetrics.SizeXZ;
            _sky = new BirdSky(width, depth, _perches, SeedFor(model.Size));

            int kinds = BirdSpecies.All.Length;
            _meshes = new Mesh[kinds];
            _materials = new Material?[kinds];
            _props = new MaterialPropertyBlock[kinds];
            _matrices = new Matrix4x4[kinds][];
            _states = new List<Vector4>[kinds];
            _counts = new int[kinds];

            Shader? shader = Shader.Find("Odyssey/Bird");
            if (shader == null)
                Debug.LogWarning("Odyssey/Bird shader not found; the birds will not be drawn.");

            for (int k = 0; k < kinds; k++)
            {
                BirdSpecies species = BirdSpecies.All[k];
                _meshes[k] = BuildMesh(species);
                _props[k] = new MaterialPropertyBlock();
                _matrices[k] = new Matrix4x4[BirdSky.MaxBirds];
                _states[k] = new List<Vector4>(BirdSky.MaxBirds);
                if (shader == null) continue;

                var material = new Material(shader)
                {
                    name = "Odyssey/Bird/" + species.Kind,
                    hideFlags = HideFlags.DontSave,
                    enableInstancing = true,
                };
                material.SetFloat(BeatsId, species.BeatsPerSecond);
                material.SetFloat(AmplitudeId, species.Amplitude);
                _materials[k] = material;
            }
        }

        /// <summary>The flock model, for the tests and the developer overlay.</summary>
        public BirdSky Sky => _sky;

        public BirdPerches Perches => _perches;

        public bool Available => _materials[0] != null;

        /// <summary>
        /// Off, the sky is neither stepped nor drawn and costs nothing. The control the frame-time
        /// arm times against (design 50 §8), and the seam a settings switch would use.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Whether a bird casts a shadow. On in the game; the frame-time arm turns it off to price it.</summary>
        public bool CastShadows { get; set; } = true;

        /// <summary>Birds submitted last frame.</summary>
        public int LastDrawn { get; private set; }

        /// <summary>Instanced calls submitted last frame: one a species with anybody in it.</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>The drawn size last frame, as a multiple of life size (<see cref="BirdScale"/>).</summary>
        public float LastScale { get; private set; } = 1f;

        /// <summary>
        /// One frame. <paramref name="gameSeconds"/> is the frame's seconds times the game speed
        /// (zero while paused). <paramref name="tick"/> gives the hour. The slice's two answers
        /// decide what is drawn: nothing below the surface, and no perch above the highest drawn layer.
        /// </summary>
        public void Sync(float gameSeconds, long tick, in WeatherView weather, ReadOnlySpan<PawnView> pawns,
            float cameraDistance, bool underground, int highestVisibleLayer)
        {
            if (!Enabled)
            {
                LastDrawn = 0;
                LastDrawCalls = 0;
                return;
            }

            _perches.HighestVisibleLayer = highestVisibleLayer;

            int walkers = Mathf.Min(pawns.Length, MaxWalkers);
            for (int i = 0; i < walkers; i++)
            {
                Vector3 at = CellMetrics.FloorCentre(pawns[i].Cell);
                _walkers[i * 3] = at.x;
                _walkers[i * 3 + 1] = at.y;
                _walkers[i * 3 + 2] = at.z;
            }

            var conditions = new BirdConditions(Daylight.HourOf(tick), weather.Kind, weather.RainPerMille / 1000f);
            _sky.Step(gameSeconds, conditions, new ReadOnlySpan<float>(_walkers, 0, walkers * 3));

            if (gameSeconds > 0f) _clock = Mathf.Repeat(_clock + gameSeconds, ClockWrapSeconds);
            Draw(cameraDistance, underground);
        }

        void Draw(float cameraDistance, bool underground)
        {
            LastDrawn = 0;
            LastDrawCalls = 0;
            LastScale = BirdScale.For(cameraDistance);
            if (underground || !Available) return;

            // Nothing allocated in here: the matrices, the states and the counts are all reused.
            int[] counts = _counts;
            for (int k = 0; k < _states.Length; k++)
            {
                _states[k].Clear();
                counts[k] = 0;
            }

            ReadOnlySpan<Bird> birds = _sky.Birds;
            for (int i = 0; i < birds.Length; i++)
            {
                Bird b = birds[i];
                if (b.State == BirdState.Away) continue;
                int k = (int)b.Kind;
                float size = LastScale * BirdSpecies.All[k].Span;
                // Unity's Euler turns Z, then X, then Y: roll, then pitch, then heading. Its X turns
                // the nose down and its Z lifts the right wing, so both are negated.
                Quaternion turn = Quaternion.Euler(-b.Pitch * Mathf.Rad2Deg, b.Yaw * Mathf.Rad2Deg, -b.Bank * Mathf.Rad2Deg);
                _matrices[k][counts[k]] = Matrix4x4.TRS(new Vector3(b.X, b.Y, b.Z), turn, new Vector3(size, size, size));
                _states[k].Add(new Vector4(b.Phase, b.Flap, b.Fold, 0f));
                counts[k]++;
            }

            float width = _model.Size.SizeX * CellMetrics.SizeXZ, depth = _model.Size.SizeZ * CellMetrics.SizeXZ;
            float height = _model.Size.SizeY * CellMetrics.SizeY;
            // The sky over the board and the margin a leaving flock crosses: every bird is inside it.
            var bounds = new Bounds(new Vector3(width * 0.5f, height * 0.5f + 40f, depth * 0.5f),
                new Vector3(width + 2f * BirdSky.ExitMargin + 40f, height + 120f, depth + 2f * BirdSky.ExitMargin + 40f));

            for (int k = 0; k < _states.Length; k++)
            {
                Material? material = _materials[k];
                if (material == null || counts[k] == 0) continue;
                material.SetFloat(ClockId, _clock);

                // Padded to the cap so the block's array is one length for ever: a block fixes an
                // array's length the first time it is set (ChunkRenderer.WritePadded).
                List<Vector4> states = _states[k];
                while (states.Count < BirdSky.MaxBirds) states.Add(default);
                _props[k].SetVectorArray(StateId, states);

                var parameters = new RenderParams(material)
                {
                    worldBounds = bounds,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = false,
                    matProps = _props[k],
                };
                Graphics.RenderMeshInstanced(parameters, _meshes[k], 0, _matrices[k], counts[k]);
                LastDrawn += counts[k];
                LastDrawCalls++;
            }
        }

        /// <summary>
        /// A species' mesh from <see cref="BirdShape"/>: unit wingspan, colours in the vertices, the
        /// wing weight in uv.x. Colours are authored in sRGB and converted here, because a vertex
        /// colour, unlike a colour property, is handed to the shader untouched.
        /// </summary>
        public static Mesh BuildMesh(BirdSpecies species)
        {
            BirdShape.Build(species, out float[] positions, out float[] weights, out BirdColour[] slots, out int[] triangles);
            int n = weights.Length;
            var vertices = new Vector3[n];
            var colours = new Color[n];
            var uv = new Vector2[n];
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int v = 0; v < n; v++)
            {
                vertices[v] = new Vector3(positions[v * 3], positions[v * 3 + 1], positions[v * 3 + 2]);
                uint rgb = BirdShape.ColourOf(species, slots[v]);
                var srgb = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
                colours[v] = linear ? srgb.linear : srgb;
                uv[v] = new Vector2(weights[v], 0f);
            }

            var mesh = new Mesh { name = "Odyssey/Bird/" + species.Kind, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.colors = colours;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The same sky for the same board size, so two screenshots of one board agree.</summary>
        static uint SeedFor(GridSize size) =>
            0xB1D5u ^ (uint)(size.SizeX * 73856093) ^ (uint)(size.SizeZ * 19349663) ^ (uint)(size.SizeY * 83492791);

        public void Dispose()
        {
            foreach (Material? material in _materials) Destroy(material);
            foreach (Mesh mesh in _meshes) Destroy(mesh);
        }

        static void Destroy(UnityEngine.Object? o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
