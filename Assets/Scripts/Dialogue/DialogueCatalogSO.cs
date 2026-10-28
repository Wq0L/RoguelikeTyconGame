using System;
using UnityEngine;

// Diyalog / tutorial satır kataloğu (25 Ekim 2026, plan: Docs/DiyalogPlani). Tutorial ve hikâye aynı altyapıyı kullanır.
// Her kayıt bir "konuşma"dır: hangi anda (tetik), hangi koşulda, kaç kez, hangi kutuda, hangi satırlarla.
// Metinler burada değil, anahtarla metin tablosundadır (Resources/Dialogue/dialogue_text.csv, DialogueText).
// Kimlik (id) kayıtta "görüldü" olarak tutulur: yayınlandıktan sonra değiştirilmez.
public enum DialogueTrigger
{
    None,
    SelectionOpened,     // ana menüde çiftçi / tırpan seçimi açıldı
    RunSetupOpened,      // oyun sahnesinde hazırlık ekranı
    PlacingEntered,      // saksı yerleştirme modu
    RoundStarted,        // bir round başladı
    RoundIdle,           // round içinde kutu boşaldı (bir önceki satırın ardından)
    LevelUp,             // kart seçimi açıldı
    FieldPreview,        // round sonu tarla önizlemesi açıldı (Tab; RoundPreviewUI). Sınama önizlemesi değildir.
    FieldEventPrepared,  // kapris hazırlanıyor
    RunEnded,            // run bitti (neden: RunOutcome)
    // Faz 2. Kayıtta sayı olarak durur: yeni tetikler hep sona eklenir.
    PeriodLastRound,     // Adak döneminin son round'u başladı ve Adak henüz dolmadı
    PeriodClosed,        // Adak dönemi tuttu (round sonu ekranı)
    BossRound,           // bir Sınama round'u başladı
}

// Öncelik bu sırayla: ilk karşılaşmanın gerekli öğretimi > hikâye > adlandırma > tepki.
public enum DialogueCategory { Reaction, Naming, Story, Guide }

public enum DialogueSize { Big, Small, Planet }

public enum DialogueFrequency { Once, Repeat }

[Serializable]
public sealed class DialogueLine
{
    public string textKey;
    [Tooltip("Eri'nin yüz karesi (GreenhouseMascotFrames, 0–15). ŞU AN KULLANILMIYOR: konuşma kutusunda Eri sabit (28 Ekim kullanıcı kararı); ileride ifade çizimleri gelirse.")]
    [Range(0, 15)] public int face = 8;
}

[Serializable]
public sealed class DialogueEntry
{
    public string id;
    public DialogueCategory category = DialogueCategory.Guide;
    public DialogueTrigger trigger;
    public DialogueSize size = DialogueSize.Big;
    public DialogueFrequency frequency = DialogueFrequency.Once;
    [Tooltip("Tekrar eden konuşmanın en kısa aralığı (sn, gerçek zaman).")]
    public float cooldownSeconds = 120f;
    [Range(0, 3)] public int minStage;
    [Range(0, 3)] public int maxStage = 3;
    [Tooltip("Bu konuşmadan önce görülmüş olması gereken konuşma (boş: yok).")]
    public string requiresSeen = "";
    [Tooltip("Yalnız RunEnded için: hangi bitiş (None: hepsi).")]
    public RunOutcome outcome = RunOutcome.None;
    [Tooltip("Aynı kategoride çakışınca büyük olan önce.")]
    public int priority;
    public DialogueLine[] lines = Array.Empty<DialogueLine>();
}

[CreateAssetMenu(menuName = "ClickerGame/Dialogue/Catalog", fileName = "DialogueCatalog")]
public sealed class DialogueCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Dialogue/DialogueCatalog";
    public DialogueEntry[] entries = Array.Empty<DialogueEntry>();
}
