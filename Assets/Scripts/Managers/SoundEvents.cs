using System;
using UnityEngine;

// Gameplay/UI code requests a sound without holding a manager or AudioSource.
public static class SoundEvents
{
    public readonly struct EffectRequest
    {
        public readonly AudioClip Clip;
        public readonly float Volume, Pitch;
        public readonly bool Hit;
        public EffectRequest(AudioClip clip, float volume, float pitch, bool hit)
        { Clip = clip; Volume = volume; Pitch = pitch; Hit = hit; }
    }

    public static event Action<EffectRequest> EffectRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => EffectRequested = null;

    public static void PlayEffect(AudioClip clip, float volume = 1f, float pitch = 1f, bool hit = false)
    {
        if (clip == null || volume <= 0f) return;
        SoundManager.Ensure();
        EffectRequested?.Invoke(new EffectRequest(clip, volume, pitch, hit));
    }
}
