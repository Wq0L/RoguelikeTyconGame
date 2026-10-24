using UnityEngine;

public enum GameSound { PlaceItem, PlantDamage, InvalidPlacement, CancelPlacement }

public sealed partial class SoundManager
{
    [System.Serializable]
    public sealed class Cue
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
        [Range(0.5f, 2f)] public float pitch = 1f;
    }

    [Header("Placement and Plant Sounds")]
    [SerializeField] private Cue placeItem = new Cue();
    [SerializeField] private Cue plantDamage = new Cue { volume = 0.3f };
    [SerializeField] private Cue invalidPlacement = new Cue();
    [SerializeField] private Cue cancelPlacement = new Cue();
    private int lastPlantHitFrame = -1;
    private bool criticalPlantHitPlayed;

    public void Play(GameSound sound, bool critical = false)
    {
        var cue = sound switch
        {
            GameSound.PlaceItem => placeItem,
            GameSound.PlantDamage => plantDamage,
            GameSound.InvalidPlacement => invalidPlacement,
            GameSound.CancelPlacement => cancelPlacement,
            _ => null
        };
        if (cue == null || cue.clip == null) return;
        bool hit = sound == GameSound.PlantDamage;
        if (hit)
        {
            if (lastPlantHitFrame != Time.frameCount)
            { lastPlantHitFrame = Time.frameCount; criticalPlantHitPlayed = false; }
            else if (!critical || criticalPlantHitPlayed) return;
            criticalPlantHitPlayed |= critical;
        }
        PlayEffect(cue.clip, cue.volume, cue.pitch, hit);
    }
}
