#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The dream's grade (<c>docs/design/56-wake-up.md</c> §4): warm, faded and glowing, with heavy
    /// soft edges, laid over the golden hour at the wake's <c>Haze</c> and gone when the player is
    /// awake. The owner chose "soft and warm" (2026-09-26).
    ///
    /// <para><b>The values are targets, not offsets.</b> URP blends a volume's overridden
    /// parameters from whatever the lower-priority volumes left towards its own by its weight, so at
    /// weight 0 this is exactly the golden hour and at weight 1 exactly the table below — nothing
    /// here needs to know what the golden hour's own numbers are.</para>
    ///
    /// <para><b>No new pass.</b> Colour adjustments, white balance, bloom and vignette all run in
    /// the uber post pass the golden hour already pays for; this only moves their numbers. Built in
    /// memory, like <see cref="OvercastVolume"/>, so it can never dirty an asset, and destroyed
    /// whole by <see cref="Dispose"/>.</para>
    /// </summary>
    public sealed class WakeVolume : IDisposable
    {
        /// <summary>Above the storm's grey (50), so a wake in a storm is still a warm dream.</summary>
        public const float Priority = 60f;

        readonly GameObject _host;
        readonly Volume _volume;
        readonly VolumeProfile _profile;

        public WakeVolume(Transform? parent)
        {
            _host = new GameObject("WakeVolume") { hideFlags = HideFlags.DontSave };
            if (parent != null) _host.transform.SetParent(parent, worldPositionStays: false);

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;

            // Only the parameters that move are overridden. Add(overrides: true) would override
            // every one, and URP switches a parameter that cannot blend (bloom's dirt, its
            // downscale, the vignette's shape) the moment the weight is above nought — so the
            // golden hour's own choices would be swapped for URP's defaults on the first frame.
            ColorAdjustments colour = _profile.Add<ColorAdjustments>();
            colour.postExposure.Override(0.55f);
            colour.saturation.Override(-30f);
            colour.contrast.Override(-18f);
            colour.colorFilter.Override(new Color(1.0f, 0.90f, 0.78f));

            WhiteBalance balance = _profile.Add<WhiteBalance>();
            balance.temperature.Override(22f);

            Bloom bloom = _profile.Add<Bloom>();
            bloom.intensity.Override(2.4f);
            bloom.threshold.Override(0.6f);
            bloom.scatter.Override(0.8f);

            Vignette vignette = _profile.Add<Vignette>();
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.8f);
            vignette.color.Override(new Color(0.16f, 0.09f, 0.04f));

            _volume = _host.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = Priority;
            _volume.sharedProfile = _profile;
            _volume.weight = 0f;
        }

        /// <summary>The wake's haze, 0 to 1, as the volume's weight.</summary>
        public float Haze
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
