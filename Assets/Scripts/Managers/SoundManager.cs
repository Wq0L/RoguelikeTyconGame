using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public enum Sound
    {
        BackgroundMusic,
        SoundEffect,
        
    }

    [System.Serializable]
    public class SoundAudioClip
    {
        public Sound sound;
        public AudioClip audioClip;
    }

    private static SoundManager instance;
    public SoundAudioClip[] soundAudioClips;
    private static Dictionary<Sound, float> soundTimerDictionary;

    public static void Initialize()
    {
        soundTimerDictionary = new Dictionary<Sound, float>();
        //burda tüm sesler için timer ekleyebiliriz, böylece aynı anda birden fazla çalmasını engelleyebiliriz
    }


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void PlaySound(Sound sound)
    {
        GameObject soundGameObject = new GameObject("Sound");
        AudioSource audioSource = soundGameObject.AddComponent<AudioSource>();
        audioSource.PlayOneShot(GetAudioClip(sound));
    }

    private static bool CanPlaySound(Sound sound)
    {
        // switch (sound)
        // {
        //     case Sound.SoundEffect:
        //         if (!soundTimerDictionary.ContainsKey(sound))
        //         {
        //             soundTimerDictionary[sound] = 0f;
        //             return true;
        //         }
        //         else
        //         {
        //             float lastTimePlayed = soundTimerDictionary[sound];
        //             if (Time.time - lastTimePlayed > 0.2f) // Cooldown of 0.2 seconds for sound effects
        //             {
        //                 soundTimerDictionary[sound] = Time.time;
        //                 return true;
        //             }
        //         }
        //         break;
        //         default:
        //             return true;
        // }
        return false;
    }

    private static AudioClip GetAudioClip(Sound sound)
    {
        foreach (var soundAudioClip in instance.soundAudioClips)
        {
            if (soundAudioClip.sound == sound)
            {
                return soundAudioClip.audioClip;
            }
        }
        Debug.LogWarning("Audio clip not found for sound: " + sound);
        return null;
    }

    // public static void StopAllSounds()
    // {
    //     AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
    //     foreach (AudioSource audioSource in audioSources)
    //     {
    //         audioSource.Stop();
    //     }
    // }

}
