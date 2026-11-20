# Steam mağaza sayfası çevirileri

**Tarih:** 13 Kasım 2026. Uzun metin ("Bu oyun hakkında") ve kısa açıklama, 12 dil.

Oyun terimleri sözlüğü: [`../Terimler.csv`](../Terimler.csv). **Oyun çevrilirken önce bu sözlüğe bakılır**; mağaza ile oyun aynı kelimeleri kullanmalı.

## Diller

| Dosya | Steam dil alanı | Kısa açıklama | Ana dili konuşan kontrolü |
|---|---|---|---|
| [en.md](en.md) | English (KAYNAK) | 273 / 300 | geliştirici |
| [tr.md](tr.md) | Turkish | 252 / 300 | geliştirici |
| [fr.md](fr.md) | French | 273 / 300 | yapılmadı |
| [it.md](it.md) | Italian | 268 / 300 | yapılmadı |
| [de.md](de.md) | German | 275 / 300 | yapılmadı |
| [es-ES.md](es-ES.md) | Spanish - Spain | 266 / 300 | yapılmadı |
| [pt-BR.md](pt-BR.md) | Portuguese - Brazil | 290 / 300 | yapılmadı |
| [zh-Hans.md](zh-Hans.md) | Simplified Chinese | 109 / 300 | yapılmadı |
| [zh-Hant.md](zh-Hant.md) | Traditional Chinese | 109 / 300 | yapılmadı |
| [ja.md](ja.md) | Japanese | 137 / 300 | yapılmadı |
| [ko.md](ko.md) | Korean | 158 / 300 | yapılmadı |
| [ru.md](ru.md) | Russian | 289 / 300 | yapılmadı |

Doldurulmayanlar: Spanish - Latin America, Portuguese - Portugal.

## Steam kuralları

- **Biçim BBCode'dur**, Markdown değil. `**kalın**` çalışmaz. Başlık için `[h2]…[/h2]`, kalın yazı için `[b]…[/b]` kullanılır. Dosyalardaki metinler `[h2]` ile hazırlanmıştır.
- **GIF satırı** ("[GIF BURAYA GELECEK …]") yayından önce Steam'e yüklenen GIF'in etiketiyle değiştirilir.
- **Kısa açıklama en fazla 300 karakterdir.** Değişiklikten sonra yeniden sayılmalı.
- **Desteklenen diller tablosu dürüst olmalı.** Oyunda şu an yalnız İngilizce ve Türkçe var. Mağaza sayfasını başka dile çevirmek serbest, ama oyuna eklenmeyen dil tabloda işaretlenmez. Çinli oyuncular "Çince yok" durumuna özellikle sert tepki verir.
- Dil seçiminde **bayrak ve ülke adı kullanılmaz**; dil kendi adıyla yazılır (简体中文, 繁體中文, Русский…).

## Çeviri kuralları (kullanıcı kararı, 13 Kasım 2026)

- **AI kokmamalı.** Kelimesi kelimesine değil, o dili konuşan bir oyuncunun yazacağı gibi yazılır.
- **Esprili, doğal ton korunur.** Espri Eri'ye ve oyuna yönelir, insanlara ya da kültürlere değil.
- Her dilde **en fazla 1–2 gerçek, temaya uyan deyim** (hasat, doğa, açlık) kullanılır. Uydurma atasözü yok.
- **Küfür, argo ve bölgeye göre kaba anlam kazanan kelime yok.** Örnek: İspanyolcada "coger" yerine cosechar / recoger.
- **Din, siyaset, millî sembol ve kalıp yargı yok.** Adak / sunu kelimelerinin dinî çağrışımı her dilde ayrıca değerlendirilir.
- **Oyuncunun cinsiyetini varsayan çekim yok.**

## Çalışma yöntemi (iki AI)

1. İngilizce kaynak netleşir.
2. AI-1 çevirir (aşağıdaki çevirmen komutu).
3. AI-2 denetler (aşağıdaki denetçi komutu).
4. Anlaşmazlıklar tabloya dökülür, karar geliştiricinin.
5. Yayından önce mümkünse ana dili o dil olan bir oyuncu okur. AI'lar aynı hatayı birlikte onaylayabilir.

### Çevirmen komutu

```text
Steam mağaza sayfası metnini İngilizceden [DİL] diline çeviriyorsun.
Oyun: ERIGONE, roguelite hasat/tarla oyunu. Ton: samimi, esprili.
Kurallar:
- Terimler.csv'deki karşılıkları aynen kullan.
- ERIGONE ve Eri çevrilmez. Eri dişildir.
- Kelimesi kelimesine çevirme; o dili ana dili olarak konuşan biri nasıl yazarsa öyle yaz.
- O kültürde gerçekten kullanılan en fazla 2 ifade kullanabilirsin (tercihen hasat/doğa/açlık temalı).
  Her birini [KÜLTÜREL] diye işaretle; harfi harfine geri çevirisini ve anlamını yaz. Atasözü uydurma.
- Din, siyaset, millî sembol, kalıp yargı, argo, küfür ve cinsiyet varsayan çekim yok.
- Steam BBCode etiketlerini ([h2], [b]) bozma. Kısa açıklama 300 karakteri geçmesin.
```

### Denetçi komutu

```text
Aşağıda İngilizce kaynak ve [DİL] çevirisi var. Çeviriyi düzeltme, denetle:
1. Anlam kaybı ya da eklenmiş anlam var mı?
2. Terimler.csv'ye uyuyor mu?
3. Ana dili [DİL] olan birine doğal mı, yoksa çeviri kokuyor mu?
4. [KÜLTÜREL] ifadeler o dilde gerçekten var mı, doğru anlamda mı kullanılmış, saygılı mı?
5. Uzunluk sınırı ve Steam etiketleri doğru mu?
6. Küfür, argo, bölgeye göre kaba anlam kazanan kelime, din / siyaset / milliyet çağrışımı, kalıp yargı ya da oyuncunun cinsiyetini varsayan çekim var mı?
   Esprili ve doğal ton korunmuş mu? Espriler oyuna ve Eri'ye mi yönelik?
Her sorun için şunları yaz: cümle, sorun, öneri, güven düzeyin (yüksek/orta/düşük).
Sorun yoksa "sorun yok" de; düzeltme uydurma.
```

## Açık kararlar (özet; ayrıntılar dil dosyalarında)

- **en:** "Eri is getting hungry" → "hungry again"? "neighbour" → "neighbor"? Uzun metinde "it" vardı, dosyada "she" olarak saklandı; Steam'de de düzeltilmeli.
- **tr:** dönem / aşama; "hasadı" / "hasat alanını"; deneme / run. Ayrıca oyun içinde "Adak'ım" → "Adağım", "Sınama'yı" → "Sınamayı".
- **ru:** "ты всё расставил" eril çekim; nötr öneri dosyada.
- **es-ES:** son gönderimde eski sürüm geldi; dosyada onaylı sürüm var, Steam'deki hâli kontrol edilmeli.
- **ja:** Eri / エリ; もう / また腹ぺこ.
