#nullable enable
using Odyssey.Hud;
using UnityEngine;

namespace Odyssey.Presentation.Audio
{
    /// <summary>
    /// The draw's machine, heard (design 41 §6.6): a ticking loop while the reels turn, a clunk as
    /// each lands, a ding over a hot one, and a sting for a jackpot or a dud.
    ///
    /// <para><b>A voice of its own, for <see cref="MenuAmbience"/>'s reason.</b> The machine lives on
    /// the setup page, where there is no world and so no <see cref="AudioDirector"/>. Two sources —
    /// the loop and the one-shots — on one object, 2D, reading the same faders as everything else
    /// (Master and Effects) at the moment each sound starts.</para>
    ///
    /// <para><b>Silent until the clips exist</b>, which is the catalogue's own rule: an id with no
    /// row, or a row with no clips, plays nothing and says nothing. Sourcing is owed, not blocking.</para>
    /// </summary>
    public sealed class DrawSounds : System.IDisposable
    {
        readonly AudioCatalogue? _catalogue;
        readonly GameObject? _root;
        readonly AudioSource? _shots;
        readonly AudioSource? _loop;

        public DrawSounds(AudioCatalogue? catalogue, Transform? parent, int layer)
        {
            _catalogue = catalogue;
            if (catalogue == null) return;

            _root = new GameObject("Odyssey Draw Machine");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.layer = layer;
            _shots = Source(_root);
            _loop = Source(_root);
            _loop.loop = true;
        }

        static AudioSource Source(GameObject root)
        {
            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 64;
            return source;
        }

        /// <summary>Whether any clip for the machine is present — false on a clone with none.</summary>
        public bool HasClips => Def(SoundIds.DrawClunk) != null || Def(SoundIds.DrawSpin) != null;

        /// <summary>The loop: on while anything spins, off when the last reel lands.</summary>
        public void Spinning(bool on)
        {
            if (_loop == null) return;
            if (!on)
            {
                if (_loop.isPlaying) _loop.Stop();
                return;
            }

            if (_loop.isPlaying) return;
            AudioCatalogue.SoundDef? def = Def(SoundIds.DrawSpin);
            AudioClip? clip = Pick(def);
            if (def == null || clip == null) return;
            _loop.clip = clip;
            _loop.volume = def.Volume * Gain();
            _loop.Play();
        }

        /// <summary>One reel has landed, hot or not.</summary>
        public void Landed(bool hot)
        {
            Shot(SoundIds.DrawClunk);
            if (hot) Shot(SoundIds.DrawHot);
        }

        /// <summary>The last reel has landed: stop the loop, and sting the verdict.</summary>
        public void Finished(Verdict verdict)
        {
            Spinning(false);
            if (verdict == Verdict.Jackpot) Shot(SoundIds.DrawStar);
            else if (verdict == Verdict.Dud) Shot(SoundIds.DrawDud);
        }

        void Shot(string id)
        {
            if (_shots == null) return;
            AudioCatalogue.SoundDef? def = Def(id);
            AudioClip? clip = Pick(def);
            if (def == null || clip == null) return;
            float variance = 1f + Random.Range(-def.VolumeVariance, def.VolumeVariance);
            _shots.pitch = 1f + Random.Range(-def.PitchVariance, def.PitchVariance);
            _shots.PlayOneShot(clip, def.Volume * variance * Gain());
        }

        AudioCatalogue.SoundDef? Def(string id)
        {
            if (_catalogue == null) return null;
            var sounds = _catalogue.Sounds;
            for (int i = 0; i < sounds.Count; i++)
                if (string.Equals(sounds[i].Id, id, System.StringComparison.Ordinal)) return sounds[i];
            return null;
        }

        static AudioClip? Pick(AudioCatalogue.SoundDef? def) =>
            def == null || def.Clips.Length == 0 ? null : def.Clips[Random.Range(0, def.Clips.Length)];

        static float Gain()
        {
            AudioSettingsStore faders = AudioSettingsStore.Load();
            return AudioMath.DbToLinear(AudioMath.StackDb(faders.Db(SoundBus.Master), faders.Db(SoundBus.Effects)));
        }

        public void Dispose()
        {
            Spinning(false);
            if (_root != null) Object.Destroy(_root);
        }
    }
}
