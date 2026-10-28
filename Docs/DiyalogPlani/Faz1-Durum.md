# Diyalog altyapısı — Faz 1 durumu (26 Ekim 2026)

Plan: `Diyalog-Tutorial-Plani-20261025.pdf` (sürüm 2). Faz 1 = altyapı + birkaç gerçek deneme satırı.

## Yapıldı ve izole kopyada test edildi (DialogueVerification 45/45)

- **Kalıcı kayıt** (`MetaSave` sürüm 2): görülen konuşmalar, hikâye aşaması, kilometre taşları, bekleyen hikâye, run sayaçları,
  giriş videosu. API: `DialogueProgress`.
  - Sürüm 1 kayıt açılıyor, alanlar boş başlıyor. Daha yeni sürüm okunuyor ama üzerine yazılmıyor.
  - Satır görülünce diske hemen yazılmıyor. Aşama yalnız run sonunda artıyor ve hemen yazılıyor.
- **Satır kataloğu** (`Resources/Dialogue/DialogueCatalog.asset`, Inspector'dan düzenlenir) ve metin tablosu
  (`Resources/Dialogue/dialogue_text.csv`, ileride çeviri tablosu biçimi).
- **Yönetici** (`DialogueDirector`):
  - Olay tabanlı; görülmemiş konuşma ilk uygun anda çıkar, run sayısına bakılmaz.
  - Öğretim hikâyeden önce gelir; hikâye kaybolmaz, sakin ana ertelenir.
  - Tek bekleyen satır var; geçersizleşen bekleyen satır gösterilmez ve "görülmedi" kalır.
  - Run sonu nedenine göre satır seçer.
- **Konuşma kutusu** (`DialogueBox`): büyük ve küçük.
  - Ekran görüntüleri: `Faz1_BuyukKutu.png`, `Faz1_KucukKutu.png`.
  - **Büyük kutu:** yalnız kutuya tık, Boşluk ya da Enter ile ilerler; "Atla" bütün konuşmayı geçer.
  - **Küçük kutu:** tıklamayla ilerlemez. Yazı bitince 6 sn sonra ya da Boşluk ile kapanır. Arayüz ışını yok, yani kapris vuruşları etkilenmez.
  - **Seçili düğme:** kutu açıkken Boşluk/Enter başka bir seçili düğmeye basmıyor.
  - **Saat:** menü zamanında (timeScale 0) yazı akıyor; duraklatma bayrağında duruyor.
  - **Bellek:** karede 0 bayt.
- **Gezegendeki Eri:** round başı/sonu kendi konuşmaları kaldırıldı. Kısa tepkiler (`React`) ve "konuşuyor" zıplaması eklendi.
  Kareler `MascotFrames` ile portreyle paylaşılıyor.
- **Mechanics Test Lab koruması:** lab oturumunda diyalog kendiliğinden konuşmuyor (`DialogueDirector.LabSession`, `TestSession` açar/kapatır).
  İleride lab araçları `LabEnabled` ile açacak.
- **Eksik iki olay eklendi:** `StartSelectionPanelUI.Opened` ve `RoundPreviewUI.Opened`.

## Deneme satırları (taslak; asıl metin Faz 2'de birlikte yazılacak)

| Kimlik | An | Kutu |
|---|---|---|
| intro.select | seçim ekranı | büyük, 3 satır |
| tut.hit | round başı | küçük |
| tut.adak | round içinde, tut.hit'ten sonra | küçük |
| end.quota | Adak tutmadan biten run | büyük, 2 satır |
| react.setup | hazırlık ekranı | gezegen tepkisi, "Hmm…" |

## Başka testler

- RoundPreviewVerification 47/47.
- StartLoadoutVerification 88/88. Kayıt sürümü beklentisi 1 → güncel sürüm olarak güncellendi.
- MechanicsTestLabVerification: Artçı bölümünde duruyor.
  - **Bu hata Faz 1'den önce de vardı.** 24 Ekim commit'inde (`a24ab7a5`) Artçı dalgalı zamanlamaya geçmiş, lab testi eski beklentide kalmış.
  - Artçı'ya kadar 119 kontrol geçti; oturumun MechanicsTestScene'den açılması bunlardan biri. Artçı'dan sonraki adımlar bu yüzden çalışmadı ve doğrulanmadı.
  - Lab test dosyası değiştirilmedi.

## Doğrulanmadı / yapılmadı

- **Play'de görülmedi.** Gerçek menü → seçim ekranı → Eri'nin ilk konuşması akışı testte durum taklidiyle sınandı, gerçek menü sahnesinde değil.
- **Gezegendeki Eri'nin tepki balonu ve zıplaması** çalıştırılarak görülmedi; otomatik testlerde gezegen yok.
- **Sonraki fazlar:**
  - T2/T3 ve T6–T10 satırları (Faz 2).
  - Kilometre taşı bağlantıları: ilk Sınama, R25, tam oyun zaferi (Faz 3).
  - Ayarlar: metin hızı, aç/kapa, rehberi sıfırla (Faz 4).
  - Giriş videosu (Faz 5).
  - Lab araçları (Faz 6).
  - Yazı sesi.
