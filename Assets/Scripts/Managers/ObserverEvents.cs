using System;
using System.Collections.Generic;
using UnityEngine;

// Yalnız gözlem (ölçüm / test) için yayınlanan olaylar (Bölüm 3.7.6.2): PlantHealth.AnyDamaged / AnyHarvested, HarvestChain.Traced,
// BehaviorEchoes.Traced. Oynanış bunlara bağlı değildir; bir gözlemcinin istisnası hasarı, ölümü, havuza dönüşü ya da zincir
// kuyruğunu yarıda bırakmamalıdır.
// - Dinleyici listesi abone olunca / ayrılınca yeni bir dizi olarak kurulur; yayın o diziyi gezer (yayında allocation yok; yayın
//   sırasında abone olan ya da ayrılan, o anki yayını bozmaz).
// - Her dinleyici ayrı çağrılır: biri istisna atarsa yakalanır, raporlanır (dinleyici başına bir kez LogException, sonrası sayılır)
//   ve sıradaki dinleyiciler ile çağıran oynanış kodu sürer.
// - Oynanış olayları (ör. görev sayacı: PlantHealth.Harvested) bu yoldan geçmez: onların hatası gerçek bir oynanış hatasıdır ve
//   yutulmaz.
public static class ObserverEvents
{
    private static readonly HashSet<Delegate> reported = new();

    // Bir gözlemcinin attığı ve yakalanan istisna sayısı (oyun başlarken sıfırlanır).
    public static int Failures { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetReports()
    {
        reported.Clear();
        Failures = 0;
    }

    public static void Add<T>(ref T[] list, T handler) where T : Delegate
    {
        if (handler == null) return;
        var next = new T[list.Length + 1];
        Array.Copy(list, next, list.Length);
        next[list.Length] = handler;
        list = next;
    }

    // C# olay kuralı: aynı dinleyici birden çok eklendiyse sondan bir tanesi çıkar.
    public static void Remove<T>(ref T[] list, T handler) where T : Delegate
    {
        if (handler == null) return;
        int index = Array.LastIndexOf(list, handler);
        if (index < 0) return;
        var next = new T[list.Length - 1];
        Array.Copy(list, 0, next, 0, index);
        Array.Copy(list, index + 1, next, index, list.Length - index - 1);
        list = next;
    }

    public static void Raise<T>(Action<T>[] list, T a)
    {
        for (int i = 0; i < list.Length; i++)
        {
            try { list[i](a); }
            catch (Exception e) { Report(list[i], e); }
        }
    }

    public static void Raise<T1, T2, T3>(Action<T1, T2, T3>[] list, T1 a, T2 b, T3 c)
    {
        for (int i = 0; i < list.Length; i++)
        {
            try { list[i](a, b, c); }
            catch (Exception e) { Report(list[i], e); }
        }
    }

    public static void Raise<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5>[] list, T1 a, T2 b, T3 c, T4 d, T5 e)
    {
        for (int i = 0; i < list.Length; i++)
        {
            try { list[i](a, b, c, d, e); }
            catch (Exception ex) { Report(list[i], ex); }
        }
    }

    private static void Report(Delegate handler, Exception e)
    {
        Failures++;
        if (!reported.Add(handler)) return;
        Debug.LogError($"Gözlem olayı dinleyicisi istisna attı: {handler.Method.DeclaringType?.Name}.{handler.Method.Name}. Oynanış sürdü; " +
                       "bu dinleyicinin sonraki istisnaları yalnız sayılır.");
        Debug.LogException(e);
    }
}
