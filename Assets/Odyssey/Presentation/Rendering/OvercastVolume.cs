#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The grey day's second lever: a global post-processing volume that drains colour and
    /// contrast, weighted by <b>gloom</b> — the storm's term — laid over whatever grade the scene
    /// already has. Ordinary rain keeps its colour and leaves it at weight 0 (owner, 2026-09-25).
    ///
    /// <para><b>Prototype (claude/rain-look).</b> <see cref="Overcast"/> moves the light; this moves
    /// the picture. The light alone could not do it: the Meadow ambient is so heavy that dimming the
    /// sun left a sunny-looking meadow with softer shadows, and saturation is not a property of a
    /// light. No extra pass is paid — colour adjustments run in the uber post pass the golden hour
    /// already uses — and at weight 0 the volume contributes nothing.</para>
    ///
    /// <para>The profile is built in memory, never saved: nothing here can dirty an asset.</para>
    /// </summary>
    public sealed class OvercastVolume : IDisposable
    {
        readonly GameObject _host;
        readonly Volume _volume;
        readonly VolumeProfile _profile;

        /// <summary>Saturation at full cover, in the colour adjustment's own −100…100 scale.</summary>
        public static float SaturationAtFullCover { get; set; } = -38f;

        /// <summary>Contrast at full cover, same scale.</summary>
        public static float ContrastAtFullCover { get; set; } = -14f;

        /// <summary>Exposure at full cover, in stops.</summary>
        public static float ExposureAtFullCover { get; set; } = -0.25f;

        public OvercastVolume(Transform parent)
        {
            _host = new GameObject("OvercastVolume") { hideFlags = HideFlags.DontSave };
            _host.transform.SetParent(parent, worldPositionStays: false);

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            ColorAdjustments colour = _profile.Add<ColorAdjustments>(overrides: true);
            colour.saturation.value = SaturationAtFullCover;
            colour.contrast.value = ContrastAtFullCover;
            colour.postExposure.value = ExposureAtFullCover;
            colour.colorFilter.value = new Color(0.94f, 0.97f, 1.0f);

            _volume = _host.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50f;
            _volume.sharedProfile = _profile;
            _volume.weight = 0f;
        }

        /// <summary>Gloom, 0 to 1, as the volume's weight.</summary>
        public float Cover
        {
            get => _volume.weight;
            set => _volume.weight = Mathf.Clamp01(value);
        }

        public void Dispose()
        {
            Destroy(_host);
            Destroy(_profile);
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
