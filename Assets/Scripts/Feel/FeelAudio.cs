using UnityEngine;

public enum FeelSound
{
    Tick,
    Thump,
    Whoosh,
    Chime,
    Shimmer
}

// Feel anları için küçük prosedürel sesler. Asset gerektirmez; ilk kullanımda bir kez üretilir.
// İleride gerçek ses dosyalarıyla değiştirmek için Override() kullanılabilir.
// AudioSource gerçek zamanda çalar: timeScale 0 (round sonu, shop) iken de duyulur.
// UnityEngine.Random kullanılmaz: oyunun RNG dizisi etkilenmez.
public static class FeelAudio
{
    private const int SampleRate = 44100;
    private const int VoiceCount = 8;

    public static float MasterVolume = 0.7f;

    private static readonly AudioClip[] clips = new AudioClip[5];
    private static readonly AudioClip[] overrides = new AudioClip[5];
    private static AudioSource[] voices;
    private static GameObject host;
    private static int nextVoice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        System.Array.Clear(clips, 0, clips.Length);
        System.Array.Clear(overrides, 0, overrides.Length);
        voices = null;
        host = null;
        nextVoice = 0;
        MasterVolume = 0.7f;
    }

    public static void Override(FeelSound sound, AudioClip clip) => overrides[(int)sound] = clip;

    public static void Play(FeelSound sound, float volume = 1f, float pitch = 1f)
    {
        if (volume <= 0f || Application.isBatchMode) return;
        EnsureHost();
        AudioClip clip = overrides[(int)sound] != null ? overrides[(int)sound] : GetClip(sound);
        AudioSource voice = voices[nextVoice++ % VoiceCount];
        voice.pitch = pitch;
        voice.PlayOneShot(clip, volume * MasterVolume * GameSettings.EffectsVolume);
    }

    private static void EnsureHost()
    {
        if (host != null) return;
        host = new GameObject("Feel Audio");
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }
    }

    private static AudioClip GetClip(FeelSound sound)
    {
        int index = (int)sound;
        if (clips[index] != null) return clips[index];
        clips[index] = sound switch
        {
            FeelSound.Tick => Build("Feel Tick", 0.035f, Tick),
            FeelSound.Thump => Build("Feel Thump", 0.2f, Thump),
            FeelSound.Whoosh => BuildWhoosh(0.36f),
            FeelSound.Chime => Build("Feel Chime", 0.75f, Chime),
            _ => Build("Feel Shimmer", 0.65f, Shimmer)
        };
        return clips[index];
    }

    private delegate float Wave(float t);

    private static AudioClip Build(string name, float seconds, Wave wave)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[count];
        for (int i = 0; i < count; i++) data[i] = wave(i / (float)SampleRate);
        return Finish(name, data);
    }

    private static AudioClip Finish(string name, float[] data)
    {
        float peak = 0.0001f;
        foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
        float gain = 0.8f / peak;
        // Kısa fade-in/out: tıkırtı (click) olmasın.
        int fade = Mathf.Min(64, data.Length / 4);
        for (int i = 0; i < data.Length; i++)
        {
            float edge = Mathf.Min(1f, Mathf.Min(i, data.Length - 1 - i) / (float)fade);
            data[i] *= gain * edge;
        }
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Sine(float frequency, float t) => Mathf.Sin(2f * Mathf.PI * frequency * t);

    private static float Tick(float t) =>
        (Sine(1850f, t) + 0.35f * Sine(3700f, t)) * Mathf.Exp(-t * 160f);

    private static float Thump(float t)
    {
        // 150 Hz'den 55 Hz'e düşen sinüs: yere "oturma" hissi.
        float phase = 2f * Mathf.PI * (55f * t + 95f * (1f - Mathf.Exp(-t * 18f)) / 18f);
        return Mathf.Sin(phase) * Mathf.Exp(-t * 20f);
    }

    private static readonly float[] ChimeNotes = { 1046.5f, 1318.5f, 1568f }; // Do-Mi-Sol arpej

    private static float Chime(float t)
    {
        float value = 0f;
        for (int n = 0; n < ChimeNotes.Length; n++)
        {
            float start = n * 0.085f;
            if (t < start) continue;
            float local = t - start;
            value += (Sine(ChimeNotes[n], local) + 0.3f * Sine(ChimeNotes[n] * 2f, local)) * Mathf.Exp(-local * 7f);
        }
        return value;
    }

    private static float Shimmer(float t)
    {
        float tremolo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 17f * t);
        float body = Sine(2093f, t) + Sine(2637f, t) * 0.8f + Sine(3136f, t) * 0.6f + Sine(3951f, t) * 0.4f;
        float attack = Mathf.Clamp01(t / 0.015f);
        return body * tremolo * attack * Mathf.Exp(-t * 5f);
    }

    private static AudioClip BuildWhoosh(float seconds)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[count];
        var noise = new System.Random(1337);
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            // Filtre önce açılıp sonra kapanır: "vuuş".
            float cutoff = Mathf.Lerp(0.02f, 0.22f, Mathf.Sin(t * Mathf.PI));
            float white = (float)noise.NextDouble() * 2f - 1f;
            low += (white - low) * cutoff;
            data[i] = low * Mathf.Sin(t * Mathf.PI);
        }
        return Finish("Feel Whoosh", data);
    }
}
