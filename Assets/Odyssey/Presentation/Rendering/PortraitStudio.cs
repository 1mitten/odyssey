#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Photographs a colonist once and keeps the picture
    /// (<c>docs/design/20-avatars.md</c> §10).
    ///
    /// <para><b>Why this exists.</b> The drawn avatar matched a colonist's palette and invented
    /// their person: it threw away <see cref="ColonistAppearance.Look"/>, which is *which of the
    /// sixty-one characters this is*, so a colonist in a helmet was given a ponytail. The owner's
    /// report was that the colonists look nothing like their profile picture, and they were right.
    /// This renders the actual character.</para>
    ///
    /// <para><b>Why it is allowed, given that §4.5 refuses portraits.</b> It refuses *fifty live
    /// render-textured portraits at 15 Hz while the world draws*, and names a cached atlas as the
    /// graduation path. Nothing here is live: a portrait is rendered the first time an appearance
    /// is asked for and never again.</para>
    ///
    /// <para><b>The cache is keyed on the appearance, not on the pawn</b>, which is the whole
    /// performance argument. Two colonists who genuinely look alike share one picture, a colonist
    /// who leaves and comes back asks for one that already exists, and a colony of twenty-six with
    /// a dozen distinct appearances is twelve renders for the session.</para>
    ///
    /// <para><b>One <see cref="RenderTexture"/> exists for the whole game.</b> It is reused for
    /// every portrait and read back into a small <see cref="Texture2D"/> per appearance — 128
    /// square, so 64 kB each. That is what makes <see cref="LiveRenderTextures"/> a number a test
    /// can pin at one rather than at the size of the colony.</para>
    /// </summary>
    public sealed class PortraitStudio : IDisposable
    {
        /// <summary>
        /// The side of a portrait, in pixels. One size, displayed at 26, 30 and 64 — a picture
        /// downscaled by the layout is free, and three sizes would be three renders and three
        /// textures for one person.
        /// </summary>
        public const int Size = 128;

        /// <summary>How far under the board the rig sits, in metres. Far outside any world.</summary>
        const float Underworld = -5000f;

        readonly ModuleCatalogue? _catalogue;
        readonly Dictionary<ColonistAppearance, Texture2D?> _cache =
            new Dictionary<ColonistAppearance, Texture2D?>();

        GameObject? _rig;
        Camera? _camera;
        Light? _light;
        RenderTexture? _target;
        GameObject? _subject;
        int _subjectLook = -1;

        public PortraitStudio(ModuleCatalogue? catalogue, ColonistMaterials? materials)
        {
            _catalogue = catalogue;
            Materials = materials;
        }

        /// <summary>
        /// How a colonist is painted. The same object the figures hold where there is a colony, so
        /// a portrait and the person walking around cannot be painted differently.
        /// </summary>
        public ColonistMaterials? Materials { get; set; }

        /// <summary>
        /// Who everybody is. Set to the game's own book when there is a colony, so an override —
        /// and the pinned cast the two development switches set — reaches a portrait too.
        /// </summary>
        public ColonistAppearanceBook? Appearances { get; set; }

        /// <summary>How many distinct appearances have been photographed.</summary>
        public int Portraits => _cache.Count;

        /// <summary>
        /// Render textures alive right now. <b>One, once anything has been photographed, and never
        /// more</b> — the picture the plan's own leak test was asking for.
        /// </summary>
        public int LiveRenderTextures => _target != null ? 1 : 0;

        /// <summary>
        /// Whether a portrait can be taken at all.
        ///
        /// <para><b>It asks whether any row resolved to a prefab, not whether there are rows</b>,
        /// and the difference is the whole of it. The catalogue is a committed asset whose prefab
        /// references point into the gitignored <c>Assets/Synty</c>: on a checkout without the
        /// packs it loads perfectly and every single reference is null. Counting rows therefore
        /// answers "yes" on exactly the machine that cannot take a photograph — which is what the
        /// self-hosted runner found, by running the portrait tests instead of ignoring them and
        /// then discovering there was nobody to photograph.</para>
        /// </summary>
        public bool Available
        {
            get
            {
                List<ModuleEntry> rows = Rows;
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].prefab != null) return true;

                return false;
            }
        }

        /// <summary>
        /// The colonist family, found once. <see cref="ModuleCatalogue.FindFamily"/> walks the
        /// whole catalogue and builds a list, and this is asked on every portrait.
        /// </summary>
        List<ModuleEntry> Rows =>
            _rows ??= _catalogue == null
                ? EmptyRows
                : _catalogue.FindFamily(ModuleIds.ColonistBase);

        List<ModuleEntry>? _rows;

        static readonly List<ModuleEntry> EmptyRows = new List<ModuleEntry>();

        /// <summary>
        /// The portrait of the colonist this seed and id describe, or null where one cannot be
        /// taken — no catalogue, no prefab for their look, or no graphics device.
        /// </summary>
        public Texture2D? For(uint rollSeed, PawnId id)
        {
            ColonistAppearanceBook book = Appearances ??= new ColonistAppearanceBook(0u, Rows.Count);
            return For(book.For(id.Value, rollSeed));
        }

        /// <summary>The portrait of a colonist in the published frame.</summary>
        public Texture2D? For(WorldSnapshot snapshot, PawnId id) =>
            For(ColonistNames.RollSeedOf(snapshot, id), id);

        /// <summary>
        /// The portrait of one appearance. **A miss is cached too**: a look with no prefab is a
        /// miss on every frame otherwise, and the answer cannot change without the catalogue
        /// changing.
        /// </summary>
        public Texture2D? For(in ColonistAppearance appearance)
        {
            if (_cache.TryGetValue(appearance, out Texture2D? cached)) return cached;

            Texture2D? taken = Shoot(appearance);
            _cache[appearance] = taken;
            return taken;
        }

        /// <summary>
        /// Forget every picture and release the rig. The session's teardown, and the way an
        /// appearance panel would ask for everybody to be photographed again.
        /// </summary>
        /// <summary>
        /// Bumped every time the pictures are thrown away, so anything holding one can tell that
        /// what it holds has been destroyed.
        ///
        /// <para><b>The roster bar needed this and its absence was a reported bug</b> (2026-09-18:
        /// the pictures gone on start and on load, present on the colonist card). A card re-reads
        /// itself when the colonist in its slot changes — and a new colony reuses the same small
        /// pawn ids, so on a load nothing about the slot changed while every texture under it had
        /// just been destroyed. An id cannot answer "does this picture still exist"; a generation
        /// can, and it costs one integer.</para>
        /// </summary>
        public int Generation { get; private set; }

        public void Clear()
        {
            foreach (KeyValuePair<ColonistAppearance, Texture2D?> row in _cache)
                if (row.Value != null) UnityEngine.Object.Destroy(row.Value);

            _cache.Clear();
            Generation++;
        }

        public void Dispose()
        {
            Clear();

            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.Destroy(_target);
                _target = null;
            }

            if (_rig != null)
            {
                UnityEngine.Object.Destroy(_rig);
                _rig = null;
                _camera = null;
                _light = null;
                _subject = null;
                _subjectLook = -1;
            }
        }

        Texture2D? Shoot(in ColonistAppearance appearance)
        {
            List<ModuleEntry> rows = Rows;
            if ((uint)appearance.Look >= (uint)rows.Count) return null;

            ModuleEntry row = rows[appearance.Look];
            if (row.prefab == null) return null;

            EnsureRig();
            if (_camera == null || _target == null) return null;

            GameObject? subject = Subject(row, appearance.Look);
            if (subject == null) return null;

            Paint(subject, row, appearance);
            if (!Frame(subject, row)) return null;

            // Everything in the rig is off except for the length of this call, which is
            // synchronous. That is what keeps a portrait's own light out of the world's frame
            // without a spare layer or a rendering-layer mask — a directional light is global.
            //
            // **And the same door swings both ways**, which is the fault this file shipped with:
            // the world's own light, ambient and fog are global too, so a portrait was lit by
            // whatever time of day it happened to be taken at — and then cached for the session
            // (§10.7). The studio therefore takes the environment over for the same instant it
            // takes the light, and hands it straight back.
            subject.SetActive(true);
            Environs world = TakeOverEnvironment();
            if (_light != null)
            {
                // Re-asserted rather than trusted. The light is set up once in EnsureRig and
                // nothing here should be able to move it — but it is a directional light in a
                // live scene, and one thing did: see OdysseyBootstrap.FindKeyLight. Three writes
                // per portrait is a cheap way of never having to wonder again.
                _light.transform.rotation = KeyLightAim;
                _light.color = Color.white;
                _light.intensity = KeyLightIntensity;
                _light.enabled = true;
            }

            RenderTexture previous = RenderTexture.active;
            try
            {
                _camera.Render();

                RenderTexture.active = _target;
                var picture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                picture.Apply();
                return picture;
            }
            finally
            {
                RenderTexture.active = previous;
                if (_light != null) _light.enabled = false;
                Restore(world);
                subject.SetActive(false);
            }
        }

        /// <summary>
        /// The global rendering state a portrait must not be at the mercy of, saved so it can be
        /// put back. See <see cref="TakeOverEnvironment"/>.
        /// </summary>
        readonly struct Environs
        {
            public Environs(AmbientMode mode, SphericalHarmonicsL2 probe, bool fog,
                float reflections, Light[] suns)
            {
                Mode = mode;
                Probe = probe;
                Fog = fog;
                Reflections = reflections;
                Suns = suns;
            }

            /// <summary>
            /// The ambient light itself, as the renderer actually reads it.
            ///
            /// <para><b>The probe and not the three <c>RenderSettings.ambient*</c> colours</b>,
            /// which is the difference between this fix working and appearing to. Writing the
            /// colours asks Unity to re-integrate the probe, and it does that on its own schedule
            /// — <see cref="DaylightDirector.ProbeUpdateHours"/> exists precisely because
            /// <c>DynamicGI.UpdateEnvironment</c> is too dear to call often. So the first version
            /// of this set a flat grey ambient, rendered against the world's *stale* probe, and
            /// measured a portrait that still tracked the clock. The probe is a direct write and
            /// takes effect on the next draw.</para>
            /// </summary>
            public readonly SphericalHarmonicsL2 Probe;

            /// <summary>Which ambient setting the renderer was reading before we took over.</summary>
            public readonly AmbientMode Mode;

            public readonly bool Fog;

            /// <summary>The skybox reflection's strength. The last thing the sky reaches us by.</summary>
            public readonly float Reflections;

            /// <summary>The scene's own directional lights, switched off and to be switched on.</summary>
            public readonly Light[] Suns;
        }

        /// <summary>
        /// Where the key light points: the way the camera looks, tilted down and a little to one
        /// side. See <see cref="EnsureRig"/> for what the first version of this got wrong.
        /// </summary>
        static readonly Quaternion KeyLightAim = Quaternion.Euler(24f, -22f, 0f);

        /// <summary>How hard the key light is driven.</summary>
        const float KeyLightIntensity = 1.6f;

        /// <summary>
        /// The studio's own ambient: one flat grey, the same at midnight as at noon.
        ///
        /// <para><b>Chosen against the daylight reference rather than picked.</b> The cast
        /// photographed at noon under the world's own light measured a mean luminance of about 80
        /// of 255 over the lit pixels, and that is what the owner has been looking at and has not
        /// complained of. This is the value that reproduces it with the world switched off
        /// (<c>docs/design/20-avatars.md</c> §10.7).</para>
        /// </summary>
        static readonly Color StudioAmbient = new Color(0.62f, 0.62f, 0.66f);

        /// <summary>
        /// How many of the scene's own directional lights the last shot had to switch off.
        /// Diagnostic: zero at night, one in the day, and anything else is worth looking at.
        /// </summary>
        public int SunsHidden { get; private set; }

        static readonly List<Light> Scratch = new List<Light>();

        /// <summary>
        /// Take the global environment over for the length of one synchronous render: a fixed
        /// ambient probe, no fog, no skybox reflection, and every other directional light off.
        ///
        /// <para><b>Every <em>other</em> directional light, found each time rather than cached.</b>
        /// A portrait is taken once per appearance and never again, so the scan is rare; caching
        /// it would mean holding references to lights a session teardown has destroyed, which is
        /// the more expensive mistake. <see cref="Light.cullingMask"/> would be the cheaper answer
        /// and is not available to us: URP ignores it on whichever directional light it picks as
        /// the main one, which is exactly the light in the way.</para>
        /// </summary>
        Environs TakeOverEnvironment()
        {
            Scratch.Clear();
            // FindObjectsOfTypeAll rather than FindObjectsByType, because the ordinary find skips
            // hidden objects and a light that lights the subject counts however it was made. The
            // two filters below are what that costs: the all-objects find also returns lights
            // that belong to prefabs on disk, which light nothing.
            foreach (Light light in Resources.FindObjectsOfTypeAll<Light>())
            {
                if (light == null || ReferenceEquals(light, _light)) continue;
                if (light.type != LightType.Directional) continue;
                if (!light.gameObject.scene.IsValid()) continue;
                if (!light.isActiveAndEnabled) continue;
                light.enabled = false;
                Scratch.Add(light);
            }

            SunsHidden = Scratch.Count;

            var saved = new Environs(
                RenderSettings.ambientMode,
                RenderSettings.ambientProbe,
                RenderSettings.fog,
                RenderSettings.reflectionIntensity,
                Scratch.ToArray());

            var flat = new SphericalHarmonicsL2();
            flat.AddAmbientLight(StudioAmbient);
            // Custom is the one mode in which the renderer reads the probe we just wrote. Under
            // Trilight — which is what the daylight cycle sets — Unity re-derives the probe from
            // its own three colours and the write is silently thrown away, which is exactly what
            // the first attempt at this measured.
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = flat;
            RenderSettings.fog = false;
            // A fixed ambient is not the whole of the sky's reach: the default reflection probe
            // is the skybox, and the skybox is the time of day.
            RenderSettings.reflectionIntensity = 0f;
            return saved;
        }

        /// <summary>Put back what <see cref="TakeOverEnvironment"/> took.</summary>
        static void Restore(in Environs saved)
        {
            RenderSettings.reflectionIntensity = saved.Reflections;
            RenderSettings.fog = saved.Fog;
            RenderSettings.ambientProbe = saved.Probe;
            RenderSettings.ambientMode = saved.Mode;

            for (int i = 0; i < saved.Suns.Length; i++)
                if (saved.Suns[i] != null) saved.Suns[i].enabled = true;
        }

        void EnsureRig()
        {
            if (_rig != null) return;

            _rig = new GameObject("Odyssey portrait studio")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _rig.transform.position = new Vector3(0f, Underworld, 0f);

            var cameraObject = new GameObject("camera");
            cameraObject.transform.SetParent(_rig.transform, worldPositionStays: false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;                       // rendered by hand, never by the loop
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 6f;
            _camera.clearFlags = CameraClearFlags.SolidColor;

            // Transparent, so the tile behind the portrait is still the colonist's garment colour
            // and a portrait sits in the same box the drawn avatar does.
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            var lightObject = new GameObject("key light");
            lightObject.transform.SetParent(_rig.transform, worldPositionStays: false);
            // Pointing the same way the camera looks, tilted down and a little to one side. The
            // first version was Euler(28, 200, 0), which is a light aimed at the back of their
            // heads — measured on Logs/portraits.png, where the whole cast came out dim and flat.
            lightObject.transform.rotation = KeyLightAim;
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = KeyLightIntensity;
            _light.shadows = LightShadows.None;
            _light.enabled = false;

            _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "Odyssey portrait",
                antiAliasing = 4,
            };
            _camera.targetTexture = _target;
        }

        /// <summary>
        /// The body being photographed. One instance is kept and swapped only when the look
        /// changes, because the common case is a colony of one or two families of body and
        /// instantiating a Synty character is not free.
        /// </summary>
        GameObject? Subject(ModuleEntry row, int look)
        {
            if (_subject != null && _subjectLook == look) return _subject;

            if (_subject != null)
            {
                UnityEngine.Object.Destroy(_subject);
                _subject = null;
                _subjectLook = -1;
            }

            GameObject instance = UnityEngine.Object.Instantiate(row.prefab!, _rig!.transform);
            instance.name = "subject";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 165f, 0f);
            instance.transform.localScale = row.scale == Vector3.zero ? Vector3.one : row.scale;

            // No animator and no PlayableGraph: a portrait does not walk. It also means a look
            // whose gaits are missing — which LooksFrom drops outright — can still be photographed.
            var animator = instance.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            // The same bare head the figure director gives, or a colonist's portrait and the
            // colonist would be wearing different things.
            ColonistAttachments.BareTheHead(instance);

            // Two empty slots on the head bone, for the hair and the beard this look will be
            // dressed in. Made with the subject and not per portrait, because the subject is kept
            // and reused across every colonist wearing this body
            // (docs/design/29-modular-colonists.md, MC5).
            //
            // GetBoneTransform on a disabled Animator is fine and is already relied on below to
            // aim the camera: what is disabled is the animation update, not the avatar.
            _hairMesh = null;
            _hairRenderer = null;
            _beardMesh = null;
            _beardRenderer = null;
            if (animator != null && animator.isHuman)
            {
                Transform? head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    ColonistAttachments.MakeSlot(head, "Hair", instance.layer,
                        out MeshFilter hf, out MeshRenderer hr);
                    ColonistAttachments.MakeSlot(head, "Beard", instance.layer,
                        out MeshFilter bf, out MeshRenderer br);
                    _hairMesh = hf;
                    _hairRenderer = hr;
                    _beardMesh = bf;
                    _beardRenderer = br;
                }
            }

            instance.SetActive(false);
            _subject = instance;
            _subjectLook = look;
            return instance;
        }

        MeshFilter? _hairMesh;
        MeshRenderer? _hairRenderer;
        MeshFilter? _beardMesh;
        MeshRenderer? _beardRenderer;

        ColonistAttachments? _attachments;

        /// <summary>
        /// The hair and beards. Shared with the figure director through one owner, because a
        /// colonist photographed for their roster card and the same colonist walking around the
        /// board must not be dressed by two different answers.
        /// </summary>
        ColonistAttachments Attachments => _attachments ??= new ColonistAttachments(_catalogue);

        void Paint(GameObject subject, ModuleEntry row, in ColonistAppearance appearance)
        {
            if (Materials == null) return;

            AppearanceCells cells = row.appearance;
            AppearanceCells? usable = cells.Any ? cells : null;

            ColonistAttachments.Wear(_hairMesh, _hairRenderer,
                Attachments.Hair(appearance.HairPiece), Materials, appearance);
            ColonistAttachments.Wear(_beardMesh, _beardRenderer,
                Attachments.Beard(appearance.BeardPiece), Materials, appearance);

            var skins = subject.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < skins.Length; i++)
            {
                SkinnedMeshRenderer skin = skins[i];
                if (skin == null) continue;

                // sharedMaterial rather than material: the second one instantiates a copy per
                // renderer and the studio would leak one for every portrait it ever took.
                Material? art = skin.sharedMaterial;
                Material? painted = Materials.For(art, usable, appearance);
                if (painted != null) skin.sharedMaterial = painted;
            }
        }

        /// <summary>
        /// Point the camera at the head. False when the body has no renderers to aim at, which is
        /// what a prefab that failed to resolve its art looks like.
        /// </summary>
        bool Frame(GameObject subject, ModuleEntry row)
        {
            if (_camera == null) return false;

            var renderers = subject.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) return false;

            // The bounds have to be taken with the object live, because a disabled renderer
            // reports nothing. It goes straight back off.
            bool wasActive = subject.activeSelf;
            subject.SetActive(true);

            Bounds body = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) body.Encapsulate(renderers[i].bounds);

            subject.SetActive(wasActive);

            if (body.size.y <= 0.001f) return false;

            // **The head is found by the rig, not by the silhouette**, and that is not a
            // refinement. The first version framed on `body.max.y`, which is the top of whatever
            // the character is wearing — so a top hat, a tall helmet or a spike of hair pushed the
            // crop down and cut the face off at the chin. Measured on Logs/portraits.png: of
            // twenty-four, the hat-wearers were the ones framed wrongly, and every bare head was
            // fine, which is the signature of exactly this fault.
            //
            // Every pack character is a valid Mecanim humanoid (`docs/research/e-02`), so the head
            // bone is there to be asked for. `enabled` is false on the animator and does not need
            // to be true for this — the avatar's bone map is static data.
            Vector3 anchor;
            var animator = subject.GetComponent<Animator>();
            Transform? head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;

            float stature = body.size.y;
            if (head != null)
            {
                // The bone sits at the base of the skull, so the framing rises a little off it.
                anchor = head.position + Vector3.up * (stature * 0.035f);
            }
            else
            {
                anchor = new Vector3(body.center.x, body.max.y - stature * 0.12f, body.center.z);
            }

            // Sized off the body rather than off the head, so every colonist is photographed at
            // the same scale and a tall one does not come out further away.
            _camera.orthographicSize = stature * 0.135f;
            _camera.transform.position = new Vector3(anchor.x, anchor.y, body.center.z - 3f);
            _camera.transform.rotation = Quaternion.identity;
            return true;
        }
    }
}
