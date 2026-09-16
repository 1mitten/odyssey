#nullable enable
using System;
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Throws a few chips of something out of a cut when a tool lands: wood off an axe today,
    /// stone off a pick when mining arrives.
    ///
    /// A director and not a subsystem, and the distinction is the one the architecture insists on
    /// (`01-architecture.md` §3a): this is presentation coordinating presentation. No chip is a
    /// simulation object, none is in a cell, none is in the save and none is in the state hash —
    /// the simulation has never heard of them, exactly as it has never heard of the tufts of grass
    /// the mesher strews over the ground. What they are for is that a blow with nothing coming off
    /// it reads as a colonist waving an axe near a tree.
    ///
    /// **One system for the whole colony, not one per colonist.** The particles are simulated in
    /// world space, so a single emitter can throw a burst from wherever the blade happened to be;
    /// a system per figure would multiply the draw calls by the number of woodcutters for no gain
    /// whatever. It is owned by <see cref="PawnFigureDirector"/> because that is the only thing
    /// that knows the instant a blow lands, and it is disposed with it.
    ///
    /// **It is warmed on construction**, which matters more than the size of it suggests: the
    /// first time a particle material is drawn, the shader variant is compiled and the system's
    /// buffers are allocated, and unwarmed that lands on the exact frame the first axe hits — the
    /// one frame in the whole sequence anybody is looking at. See <see cref="Warm"/>.
    ///
    /// **What it takes to use it for something else — mining, say.** Nothing here. Write a preset
    /// in <see cref="ChipRecipe"/> (<see cref="ChipRecipe.Stone"/> is already sitting there
    /// waiting) and call <see cref="Throw"/> with it from whichever director knows the instant the
    /// tool lands. Colour, size, speed, lifetime, count and spread are all per particle, so the
    /// system, the material and the draw call are shared however many materials the colony works.
    /// The genuinely hard part is not the debris, it is knowing *when* — for felling that is
    /// <see cref="WorkSwing.Lands"/>, a pure function of the stroke phase with its own tests, and
    /// it is the pattern worth copying rather than the particle setup.
    /// </summary>
    public sealed class ChipDirector : IDisposable
    {
        /// <summary>How many pieces have been thrown. Diagnostic; a harness can check a blow landed.</summary>
        public int ChipsThrown { get; private set; }

        /// <summary>True once the material and the system exist and have been stepped at least once.</summary>
        public bool Warmed { get; private set; }

        /// <summary>
        /// Whether the chips are falling. False holds every piece of debris exactly where it is.
        ///
        /// <para>Chips are simulated in world space by Unity's own particle update, which knows
        /// nothing about the simulation clock, so a burst thrown on the frame before the player
        /// pressed space went on arcing to the ground after everything that threw it had stopped.
        /// A simulation speed of zero is Unity's own way of saying "hold": positions, velocities
        /// and ages are all kept, and putting it back to one continues the flight rather than
        /// restarting it or dropping the particles.</para>
        ///
        /// <para>It is deliberately not <c>Pause()</c>: this system is emitted into while it is
        /// stopped, and its play state is load-bearing for that. See <see cref="Configure"/>.</para>
        /// </summary>
        public bool Running
        {
            get => _running;
            set
            {
                if (_running == value) return;
                _running = value;
                if (_system == null) return;
                ParticleSystem.MainModule main = _system.main;
                main.simulationSpeed = value ? 1f : 0f;
            }
        }

        bool _running = true;

        readonly GameObject? _object;
        readonly ParticleSystem? _system;
        readonly System.Random _random = new System.Random(20260916);

        /// <summary>
        /// Build the emitter under <paramref name="parent"/>, on <paramref name="layer"/> so the
        /// slice camera sees it exactly as it sees the figures.
        ///
        /// A clone with no usable unlit shader gets no emitter and no chips, and everything else
        /// goes on working — the same bargain every other piece of presentation art makes.
        /// </summary>
        public ChipDirector(Transform? parent, int layer)
        {
            Material? material = BuildMaterial();
            if (material == null) return;

            _object = new GameObject("Chips");
            _object.transform.SetParent(parent, worldPositionStays: false);
            _object.layer = layer;

            _system = _object.AddComponent<ParticleSystem>();
            Configure(_system, material);
            Warm();
        }

        /// <summary>An unlit material, or null where the pipeline has no such shader.</summary>
        static Material? BuildMaterial()
        {
            Shader? shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;

            return new Material(shader) { name = "Odyssey/Chip" };
        }

        static void Configure(ParticleSystem system, Material material)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.gravityModifier = 1.8f;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;      // every chip is emitted with a velocity of its own
            main.startSize = 0.05f;

            // Nothing emits on its own: a blow landing is the only thing that throws a chip, and
            // it says so by calling Throw.
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// Draw the thing once, out of sight, before anybody needs it.
        ///
        /// The first draw of a particle material compiles its shader variant and allocates the
        /// system's vertex buffers. Left to happen naturally that lands on the frame the first axe
        /// hits a tree, which is the one frame in the sequence the player is watching — a hitch
        /// precisely where the effect is supposed to be. So a handful of chips are thrown at
        /// construction, far below the board where no camera can see them, and cleared again.
        ///
        /// This is a warm and not a guarantee: it makes the pipeline instantiate the material and
        /// walk the emit path, and on most drivers it compiles the variant, but a pipeline that
        /// defers compilation until the material is actually visible will still pay for it once.
        /// Doing more than this means a shader variant collection, which is a build-time job and
        /// not worth it for one unlit material.
        /// </summary>
        public void Warm()
        {
            if (_system == null || Warmed) return;

            var parameters = new ParticleSystem.EmitParams
            {
                position = new Vector3(0f, -10_000f, 0f),
                velocity = Vector3.zero,
                startLifetime = 0.01f,
                startSize = 0.001f,
                startColor = new Color(0f, 0f, 0f, 0f),
                applyShapeToPosition = false,
            };

            _system.Emit(parameters, 8);
            // Step the system so the emit path, the buffers and the renderer are all exercised
            // now rather than on the first blow. Then take them straight back out again.
            _system.Simulate(0.02f, withChildren: true, restart: false);
            _system.Clear(true);
            Warmed = true;
        }

        /// <summary>
        /// Throw a burst of <paramref name="recipe"/> from <paramref name="at"/>, outward along
        /// <paramref name="outOfTheCut"/>.
        ///
        /// The direction wants to be roughly back towards whoever swung and a little upward, which
        /// is where debris goes when an edge bites across the grain. It is normalised here rather
        /// than by the caller, and a direction of nothing at all becomes straight up, because a
        /// zero vector would put every piece in one place and leave it hanging there.
        ///
        /// The recipe is a parameter and not a field because one director serves the whole colony:
        /// a woodcutter and a miner can be throwing different debris on the same frame, from the
        /// same system, in the same draw call.
        /// </summary>
        public void Throw(in ChipRecipe recipe, Vector3 at, Vector3 outOfTheCut)
        {
            if (_system == null || !recipe.IsSomething) return;

            Vector3 out0 = outOfTheCut.sqrMagnitude > 1e-6f ? outOfTheCut.normalized : Vector3.up;
            out0 = (out0 + Vector3.up * 0.55f).normalized;

            var parameters = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < recipe.Count; i++)
            {
                Vector3 direction = Quaternion.Euler(
                    Range(-recipe.Spread, recipe.Spread),
                    Range(-recipe.Spread, recipe.Spread), 0f) * out0;

                parameters.position = at;
                parameters.velocity = direction * Range(recipe.Speed.x, recipe.Speed.y);
                parameters.startLifetime = Range(recipe.Life.x, recipe.Life.y);
                parameters.startSize = Range(recipe.Size.x, recipe.Size.y);
                parameters.startColor = recipe.Colours[_random.Next(recipe.Colours.Length)];
                _system.Emit(parameters, 1);
            }

            ChipsThrown += recipe.Count;
        }

        /// <summary>
        /// Step the chips by hand.
        ///
        /// Needed only where there is no player loop — an editor tool photographing a stroke — and
        /// harmless everywhere else, since a system that is already being simulated by Unity is
        /// not simulated twice: the tools call this, the game does not.
        /// </summary>
        public void Evaluate(float deltaTime)
        {
            if (_system == null || deltaTime <= 0f) return;
            _system.Simulate(deltaTime, withChildren: true, restart: false);
        }

        public void Dispose()
        {
            if (_object == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_object);
            else UnityEngine.Object.DestroyImmediate(_object);
        }

        float Range(float low, float high) => low + (float)_random.NextDouble() * (high - low);
    }
}
