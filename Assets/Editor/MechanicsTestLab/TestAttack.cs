using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    // Saldırı ve üretim kontrolü. Saldırının kendisi oyunun yoludur (PlayerController → AttackInRadius): kritik, gerçek hedefler,
    // hasat, zincir kökü, Hasat Ritmi, ödül tetikleri ve davranışlar aynı koddan geçer. Burada yalnız zamanlayıcının kimde olduğu
    // (otomatik / elle) ve nişanın nereden geldiği (fare / sabit konum) seçilir.
    public static class TestAttack
    {
        private static PlayerController cached;
        public static PlayerController Player => cached != null ? cached : cached = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        public static bool AutoAttack
        {
            get => PlayerController.EditorAutoAttack;
            set => PlayerController.EditorAutoAttack = value;
        }

        // Dolu: sabit nişan (imleç de orada durur). null: fareyle normal oyun kontrolü.
        public static Vector3? Aim
        {
            get => PlayerController.EditorAim;
            set
            {
                PlayerController.EditorAim = value;
                IgnorePointerOverUi(value.HasValue);
            }
        }

        // Tarla olayı hedefi "imleç arayüzün üstündeyken" vurulmaz; bu kural fareyle nişan içindir. Nişan sabitken ya da tek
        // saldırı panelden verilirken fare nişan değildir (panelin üstündedir): oyunun bu amaçla açık bıraktığı soru
        // (FieldEventDirector.PointerOverUi) o sırada "hayır" döner. Fareyle normal kontrolde oyunun kendi sorusu geri gelir.
        private static System.Func<bool> pointerQuestion;
        private static bool NotOverUi() => false;

        private static void IgnorePointerOverUi(bool ignore)
        {
            if (ignore)
            {
                if (pointerQuestion == null) pointerQuestion = FieldEventDirector.PointerOverUi;
                FieldEventDirector.PointerOverUi = NotOverUi;
            }
            else if (pointerQuestion != null)
            {
                FieldEventDirector.PointerOverUi = pointerQuestion;
                pointerQuestion = null;
            }
        }

        public static bool ProductionPaused
        {
            get => PlantSpawner.EditorPauseProduction;
            set => PlantSpawner.EditorPauseProduction = value;
        }

        public static bool RoundTimerFrozen
        {
            get => RoundManager.EditorFreezeTimer;
            set => RoundManager.EditorFreezeTimer = value;
        }

        public static Vector3 WorldOf(GridPosition cell) => FieldCells.World(FieldCells.Grid, cell);

        public static void AimAt(GridPosition cell) => Aim = WorldOf(cell);
        public static void UseMouse() => Aim = null;

        public static float Cooldown => Player != null ? Player.EditorAttackCooldown : 0f;
        public static float Interval => Player != null ? Player.EditorAttackInterval : 0f;

        // Tek gerçek doğrudan saldırı. Bekleme dolmadıysa ya da duraklama sürüyorsa saldırı yapılmaz, nedeni döner.
        public static Result AttackOnce(Vector3 position)
        {
            PlayerController player = Player;
            if (player == null) return Result.Fail("PlayerController yok.");
            bool mouseAim = !Aim.HasValue;
            if (mouseAim) IgnorePointerOverUi(true);
            bool attacked;
            string refusal;
            try { attacked = player.EditorAttackNow(position, out refusal); }
            finally { if (mouseAim) IgnorePointerOverUi(false); }
            if (!attacked) return Result.Fail("Saldırı yapılmadı: " + refusal + ".");
            return Result.Success($"Doğrudan saldırı: ({position.x:0.##}, {position.z:0.##}) · gerçek temas yarıçapı {player.LastAttackRadius:0.##}.");
        }

        // Ayrı test komutu: beklemeyi doldurur.
        public static Result ResetCooldown()
        {
            if (Player == null) return Result.Fail("PlayerController yok.");
            Player.EditorResetAttackCooldown();
            return Result.Success("Saldırı beklemesi dolduruldu (test komutu).");
        }

        // Son saldırının gerçek hedef hücreleri (yalnız test göstergesi; oyunun hedeflemesiyle aynı sorgu).
        public static void TargetCells(Vector3 center, float radius, List<GridObject> into)
        {
            into.Clear();
            FieldCells.Grid?.GetGridObjectsInRadius(center, radius, into);
        }
    }

    // Görsel test kontrolleri. Hepsi yalnız gösterimdir: hasar, hedef ve zamanlama değişmez.
    public static class TestVisuals
    {
        private static bool damageNumbersSaved, damageNumbersOriginal;

        // Oyunun ayarı bellekte değişir (PlayerPrefs'e yazılmaz) ve oturum bitince eski değerine döner.
        public static bool DamageNumbers
        {
            get => GameSettings.DamageNumbers;
            set
            {
                if (!damageNumbersSaved) { damageNumbersSaved = true; damageNumbersOriginal = GameSettings.DamageNumbers; }
                GameSettings.DamageNumbers = value;
            }
        }

        // Tarla olayı bölgelerinin kesin alan çizgisi (oyunun mevcut test görünümü) — varsayılan kapalı.
        public static bool EventOutlines
        {
            get => LateGamePresenter.DebugOutlines;
            set => LateGamePresenter.DebugOutlines = value;
        }

        // Scene görünümünde saldırı dairesi ve gerçek hedef hücreleri — varsayılan kapalı. Oyunun VFX'inin yerine geçmez.
        public static bool TargetOverlay;

        public static void Restore()
        {
            if (damageNumbersSaved) GameSettings.DamageNumbers = damageNumbersOriginal;
            damageNumbersSaved = false;
            LateGamePresenter.DebugOutlines = false;
            TargetOverlay = false;
        }
    }
}
