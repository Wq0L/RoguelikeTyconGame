using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace MechanicsTestLab
{
    // Test oturumunun zaman kontrolü. Ayrı bir saat YOKTUR: yalnız oyunun kendi saatinin hızı (Time.timeScale) değişir; artçı,
    // yankı, zincir, kasırga, bumerang, tarla olayı ve ödül sayaçlarının hepsi Time.deltaTime okuduğu için birlikte yavaşlar,
    // durur ve adım atar.
    // - Hız yalnız aktif round'da uygulanır. Menü ekranlarının (kart, ödül, mağaza) duraklatması ve round sonu yavaşlaması oyuna aittir.
    // - Tek adım: duraklatılmışken tam bir kare, sabit süreyle (StepSeconds) ilerler; kare bitince yeniden durur.
    // - Kare sonu kancası oyun döngüsüne (PostLateUpdate) Editor tarafından eklenir; oturum bitince kaldırılır ve zaman geri yüklenir.
    public static class TestTime
    {
        private struct FrameEndMarker { }

        public static readonly float[] Speeds = { 0.1f, 0.25f, 0.5f, 1f };
        public static bool Paused { get; private set; }
        public static float Speed { get; private set; } = 1f;
        public static float StepSeconds = 1f / 60f;
        public static int StepsDone { get; private set; }
        public static bool Stepping => stepFrame >= 0;

        private static int stepFrame = -1;
        private static float captureBeforeStep;
        private static bool installed;
        // Oyun döngüsünden her kare sonunda (kayıt ve sayaç izleme için).
        public static event System.Action FrameEnded;

        public static bool InRound => GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;

        public static void Install()
        {
            if (installed) return;
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem[] phases = root.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != typeof(PostLateUpdate)) continue;
                var list = new List<PlayerLoopSystem>(phases[i].subSystemList ?? new PlayerLoopSystem[0]);
                list.Add(new PlayerLoopSystem { type = typeof(FrameEndMarker), updateDelegate = FrameEnd });
                phases[i].subSystemList = list.ToArray();
                root.subSystemList = phases;
                PlayerLoop.SetPlayerLoop(root);
                installed = true;
                return;
            }
        }

        // Oturum sonu: kanca kalkar, zaman ve test hızı varsayılana döner.
        public static void Restore()
        {
            if (installed)
            {
                PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
                PlayerLoopSystem[] phases = root.subSystemList;
                for (int i = 0; i < phases.Length; i++)
                {
                    if (phases[i].type != typeof(PostLateUpdate) || phases[i].subSystemList == null) continue;
                    var list = new List<PlayerLoopSystem>(phases[i].subSystemList);
                    list.RemoveAll(s => s.type == typeof(FrameEndMarker));
                    phases[i].subSystemList = list.ToArray();
                }
                root.subSystemList = phases;
                PlayerLoop.SetPlayerLoop(root);
                installed = false;
            }
            if (stepFrame >= 0) Time.captureDeltaTime = captureBeforeStep;
            stepFrame = -1;
            Paused = false;
            Speed = 1f;
            StepsDone = 0;
            Time.timeScale = 1f;
        }

        public static void SetPaused(bool paused)
        {
            Paused = paused;
            Apply();
        }

        public static void SetSpeed(float speed)
        {
            Speed = Mathf.Clamp(speed, 0.05f, 1f);
            Apply();
        }

        // Duraklatılmışken tek kare ilerletir. Bir sonraki oyun karesi tam StepSeconds sürer; kare bitince yeniden durur.
        public static Result Step()
        {
            if (!InRound) return Result.Fail("Tek adım yalnız aktif round'da atılır.");
            if (!Paused) return Result.Fail("Tek adım için önce simülasyonu duraklat.");
            if (Stepping) return Result.Fail("Önceki adım henüz işlenmedi.");
            captureBeforeStep = Time.captureDeltaTime;
            Time.captureDeltaTime = Mathf.Clamp(StepSeconds, 0.001f, 1f);
            Time.timeScale = 1f;
            stepFrame = Time.frameCount + 1;
            return Result.Success($"Tek adım: {StepSeconds * 1000f:0.#} ms oyun süresi.");
        }

        private static void Apply()
        {
            if (!TestSession.Active || Stepping || !InRound) return;
            RoundManager rounds = RoundManager.Instance;
            // Round'un son anındaki yavaşlama oyuna aittir; o sırada test hızı araya girmez.
            if (rounds != null && rounds.EndSlowdownProgress > 0f) return;
            float wanted = Paused ? 0f : Speed;
            if (!Mathf.Approximately(Time.timeScale, wanted)) Time.timeScale = wanted;
        }

        private static void FrameEnd()
        {
            if (!TestSession.Active) return;
            if (stepFrame >= 0)
            {
                if (Time.frameCount >= stepFrame)
                {
                    stepFrame = -1;
                    Time.captureDeltaTime = captureBeforeStep;
                    Time.timeScale = 0f;
                    StepsDone++;
                }
            }
            else Apply();   // oyun round'a girerken hızı 1'e çeker; test hızı her kare sonunda yeniden uygulanır
            try { FrameEnded?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }
}
