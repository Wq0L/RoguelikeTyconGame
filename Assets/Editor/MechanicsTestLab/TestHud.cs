using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechanicsTestLab
{
    // Test oturumunda oyun HUD'unu gizleme / gösterme. YALNIZ çizim kapanır: HUD nesnesine iç içe bir Canvas eklenir ve o Canvas
    // kapatılır. GameObject'ler etkin kalır, HUD script'leri ve yöneticiler çalışmaya devam eder, dünya VFX'ine ve gerçek olaylara
    // dokunulmaz. Oyun arayüzü yeniden tasarlanmaz. Oturum bitince (ya da "göster" denince) eklenen Canvas kaldırılır.
    public static class TestHud
    {
        public enum Part { Quota, FieldEvent, Counters }

        public static readonly (Part part, string label, string detail)[] Parts =
        {
            (Part.Quota, "Kota ve boss ödülleri listesi", "QuotaHUD: kota, boss satırı, ödül listesi, Hasat Ritmi ve seçim satırı"),
            (Part.FieldEvent, "Tarla olayı açıklaması", "FieldEventHud: olay adı / süre plakası ve A-B açıklaması (dünyadaki bölge işaretleri kalır)"),
            (Part.Counters, "Round / XP / kaynak göstergeleri", "RoundUI, XpUI, kaynak sayaçları, LevelWorkStatusUI. Kota kartı Round kutusunun çocuğudur: bu gizlenince kota kartı da görünmez."),
        };

        private sealed class Hidden
        {
            public Part Part;
            public GameObject Target;
            public Canvas Canvas;
            public bool Added, WasEnabled;
        }

        private static readonly List<Hidden> hidden = new();
        private static readonly bool[] wanted = new bool[3];
        private static readonly List<Component> found = new();

        public static string LastReport { get; private set; } = "";

        public static bool IsHidden(Part part) => wanted[(int)part];

        public static Result SetHidden(Part part, bool hide)
        {
            wanted[(int)part] = hide;
            Show(part);
            if (!hide) return Result.Success($"HUD gösteriliyor: {Label(part)}.");
            return Hide(part);
        }

        private static string Label(Part part) => Parts[(int)part].label;

        private static void Collect<T>() where T : Component =>
            found.AddRange(Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        private static void Targets(Part part)
        {
            found.Clear();
            switch (part)
            {
                case Part.Quota: Collect<QuotaHUD>(); break;
                case Part.FieldEvent: Collect<FieldEventHud>(); break;
                default: Collect<RoundUI>(); Collect<XpUI>(); Collect<GoldUI>(); Collect<LevelWorkStatusUI>(); break;
            }
        }

        private static Result Hide(Part part)
        {
            Targets(part);
            int done = 0;
            var skipped = new List<string>();
            foreach (Component component in found)
            {
                GameObject target = component.gameObject;
                if (hidden.Exists(h => h.Target == target)) { done++; continue; }
                // İç içe Canvas yalnız RectTransform'lu arayüz nesnesine eklenir (düz Transform'u RectTransform'a çevirmemek için).
                if (!(target.transform is RectTransform)) { skipped.Add(component.GetType().Name + " (arayüz nesnesi değil)"); continue; }
                Canvas canvas = target.GetComponent<Canvas>();
                if (canvas != null && canvas.isRootCanvas) { skipped.Add(component.GetType().Name + " (kök Canvas'ın üstünde: bütün arayüzü kapatırdı)"); continue; }
                bool added = canvas == null;
                if (added) canvas = target.AddComponent<Canvas>();
                hidden.Add(new Hidden { Part = part, Target = target, Canvas = canvas, Added = added, WasEnabled = !added && canvas.enabled });
                canvas.enabled = false;
                done++;
            }
            LastReport = $"{Label(part)}: {done} öğe gizlendi" + (skipped.Count > 0 ? " · gizlenemeyen: " + string.Join(", ", skipped) : "");
            return done > 0 || skipped.Count == 0
                ? Result.Success("HUD gizlendi (yalnız çizim; script'ler ve yöneticiler çalışıyor) — " + LastReport)
                : Result.Fail("HUD gizlenemedi — " + LastReport);
        }

        private static void Show(Part part)
        {
            for (int i = hidden.Count - 1; i >= 0; i--)
            {
                Hidden entry = hidden[i];
                if (entry.Part != part) continue;
                hidden.RemoveAt(i);
                if (entry.Canvas == null) continue;
                // Eklenen Canvas kare sonunda kalkar (Unity bileşeni o ana kadar tutar); HUD bir sonraki karede yeniden çizilir.
                if (entry.Added) Object.Destroy(entry.Canvas);
                else entry.Canvas.enabled = entry.WasEnabled;
            }
        }

        // Yeni sahne: yok olan HUD nesnelerinin kaydı düşer, sahne değişiminde yaşayan nesnelerinki korunur (yoksa eklenen Canvas
        // sahipsiz kalır ve "göster" onu geri alamaz); istenen gizleme yeni nesnelere yeniden uygulanır.
        public static void OnSceneRebuilt()
        {
            hidden.RemoveAll(entry => entry.Target == null || entry.Canvas == null);
            foreach ((Part part, string _, string _) in Parts)
                if (wanted[(int)part]) Hide(part);
        }

        // Oturum sonu: her şey eski hâline döner.
        public static void Restore()
        {
            foreach ((Part part, string _, string _) in Parts) Show(part);
            hidden.Clear();
            Array.Clear(wanted, 0, wanted.Length);
            LastReport = "";
        }

        public static int HiddenCount => hidden.Count;

        // Gizli öğelerin dökümü (hata ayıklama ve doğrulama için).
        public static string Describe()
        {
            var text = new System.Text.StringBuilder();
            foreach (Hidden entry in hidden)
                text.Append($"[{entry.Part} · {(entry.Target != null ? entry.Target.name : "yok")} · {(entry.Added ? "eklenen Canvas" : "mevcut Canvas")} · " +
                            $"{(entry.Canvas == null ? "Canvas yok" : entry.Canvas.enabled ? "açık" : "kapalı")}] ");
            return text.Length > 0 ? text.ToString() : "gizli öğe yok";
        }
    }
}
