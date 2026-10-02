using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Run'lar arası kalıcı ilerleme: görevle açılmış başlangıç içerikleri, görev ilerlemesi, son başlangıç seçimi.
// Ayarlar (GameSettings · PlayerPrefs) ve run içi kilitler (UnlockManager, her run sıfırlanır) bundan ayrıdır.
// Dosya: persistentDataPath/meta.json (sürümlü, sabit içerik id'leri).
// - Yazım: önce meta.json.tmp, sonra yerine koyma; önceki sürüm meta.json.bak olarak kalır.
// - Okuma: dosya bozuksa silinmez, meta.corrupt-<zaman>.json adıyla saklanır; .bak denenir, o da yoksa varsayılan.
// - Bilinmeyen id'ler yok sayılır ama silinmez (ileride eklenen içerik için).
// - Batch mod (otomatik testler): açıkça klasör verilmedikçe diske dokunmaz; bellekte varsayılanla çalışır.
public static class MetaSave
{
    public const int CurrentVersion = 1;
    public const string FileName = "meta.json";
    public enum LoadResult { NotLoaded, Fresh, Loaded, FromBackup, CorruptReset, MemoryOnly }

    private static MetaSaveData data;
    private static string directory;
    private static bool memoryOnly, readOnly;
    public static bool HasPendingChanges { get; private set; }
    private static float nextRetry;

    public static bool FlushPending() => !HasPendingChanges || Save();

    public static void RetryPending()
    {
        if (!HasPendingChanges || DiskDisabled || readOnly || Time.realtimeSinceStartup < nextRetry) return;
        nextRetry = Time.realtimeSinceStartup + 5f;
        Save();
    }

    public static LoadResult LastLoad { get; private set; }
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        directory = null;
        memoryOnly = readOnly = false;
        LastLoad = LoadResult.NotLoaded;
        Changed = null;
        HasPendingChanges = false;
        nextRetry = 0f;
    }

    // Testler: kullanıcının kaydı yerine bu klasör. null: varsayılan konum.
    public static void UseDirectory(string path)
    {
        directory = path;
        memoryOnly = false;
        data = null;
    }

    public static void UseMemoryOnly()
    {
        memoryOnly = true;
        data = null;
    }

    public static string FilePath => Path.Combine(directory ?? Application.persistentDataPath, FileName);
    public static string BackupPath => FilePath + ".bak";
    private static bool DiskDisabled => memoryOnly || (directory == null && Application.isBatchMode);

    public static MetaSaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static void Load()
    {
        HasPendingChanges = false;
        nextRetry = 0f;
        readOnly = false;
        if (DiskDisabled) { data = new MetaSaveData(); LastLoad = LoadResult.MemoryOnly; return; }
        string path = FilePath, backup = BackupPath;
        bool mainExists = File.Exists(path);
        if (!mainExists && !File.Exists(backup)) { data = new MetaSaveData(); LastLoad = LoadResult.Fresh; return; }
        if (mainExists && TryRead(path, out MetaSaveData loaded)) { data = loaded; LastLoad = LoadResult.Loaded; return; }
        if (mainExists) Quarantine(path);
        if (TryRead(backup, out loaded))
        {
            data = loaded;
            LastLoad = LoadResult.FromBackup;
            Save(); // ana dosyayı yedekten geri kur
            return;
        }
        data = new MetaSaveData();
        LastLoad = LoadResult.CorruptReset;
        Debug.LogWarning("Kayıt okunamadı; varsayılanla başlandı. Bozuk dosya korundu: " + Path.GetDirectoryName(path));
    }

    private static bool TryRead(string path, out MetaSaveData result)
    {
        result = null;
        try
        {
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return false;
            result = JsonUtility.FromJson<MetaSaveData>(text);
            if (result == null || result.version < 1) return false;
            Normalize(result);
            // Daha yeni bir sürümün kaydı: okunur ama üzerine yazılmaz (bilinmeyen alanlar kaybolmasın).
            readOnly = result.version > CurrentVersion;
            return true;
        }
        catch (Exception)
        {
            result = null;
            return false;
        }
    }

    private static void Normalize(MetaSaveData d)
    {
        d.farmer ??= "";
        d.scythe ??= "";
        d.unlocked ??= new List<string>();
        d.unlocked.RemoveAll(string.IsNullOrEmpty);
        d.quests ??= new List<QuestRecord>();
        d.quests.RemoveAll(q => q == null || string.IsNullOrEmpty(q.id));
    }

    private static void Quarantine(string path)
    {
        try { File.Move(path, Path.Combine(Path.GetDirectoryName(path), $"meta.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json")); }
        catch (Exception e) { Debug.LogWarning("Bozuk kayıt taşınamadı: " + e.Message); }
    }

    public static bool Save()
    {
        if (data == null || DiskDisabled || readOnly) return false;
        HasPendingChanges = true;
        nextRetry = Time.realtimeSinceStartup + 5f;
        string path = FilePath, temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (!File.Exists(path)) File.Move(temp, path);
            else
            {
                try { File.Replace(temp, path, BackupPath, true); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(path, BackupPath, true);
                    File.Delete(path);
                    File.Move(temp, path);
                }
            }
            HasPendingChanges = false;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Kayıt yazılamadı: " + e.Message);
            return false;
        }
    }

    // ---------------- içerik ----------------
    public static bool IsUnlocked(StartOptionSO option) => option != null && (option.OpenAtStart || Data.unlocked.Contains(option.id));

    public static void SetSelection(FarmerSO farmer, ScytheSO scythe)
    {
        if (farmer == null || scythe == null) return;
        if (Data.farmer == farmer.id && Data.scythe == scythe.id) return;
        Data.farmer = farmer.id;
        Data.scythe = scythe.id;
        Save();
        Changed?.Invoke();
    }

    // ---------------- görev ----------------
    private static QuestRecord Record(QuestSO quest, bool create)
    {
        foreach (QuestRecord r in Data.quests) if (r.id == quest.id) return r;
        if (!create) return null;
        var record = new QuestRecord { id = quest.id };
        Data.quests.Add(record);
        return record;
    }

    public static int BestProgress(QuestSO quest) => quest != null ? Record(quest, false)?.best ?? 0 : 0;
    public static bool IsQuestDone(QuestSO quest) => quest != null && (Record(quest, false)?.done ?? false);

    // Tek-run ilerlemesi: kayıtta yalnız en iyi run tutulur; yalnız artınca yazılır.
    public static bool ReportProgress(QuestSO quest, int value)
    {
        if (quest == null || string.IsNullOrEmpty(quest.id)) return false;
        QuestRecord record = Record(quest, true);
        if (value <= record.best) return false;
        record.best = value;
        if (!HasPendingChanges) nextRetry = Time.realtimeSinceStartup + 5f;
        HasPendingChanges = true;
        Changed?.Invoke();
        return true;
    }

    // Tamamlama tek seferdir: içerik kalıcı açılır, geri alınmaz.
    public static bool CompleteQuest(QuestSO quest, IEnumerable<StartOptionSO> unlocks)
    {
        if (quest == null || string.IsNullOrEmpty(quest.id)) return false;
        QuestRecord record = Record(quest, true);
        if (record.done) return false;
        record.done = true;
        record.best = Mathf.Max(record.best, quest.target);
        foreach (StartOptionSO option in unlocks)
            if (option != null && !string.IsNullOrEmpty(option.id) && !Data.unlocked.Contains(option.id)) Data.unlocked.Add(option.id);
        Save();
        Changed?.Invoke();
        return true;
    }
}

[Serializable]
public sealed class MetaSaveData
{
    public int version = MetaSave.CurrentVersion;
    public string farmer = "";
    public string scythe = "";
    public List<string> unlocked = new();
    public List<QuestRecord> quests = new();
}

[Serializable]
public sealed class QuestRecord
{
    public string id;
    public int best;
    public bool done;
}
