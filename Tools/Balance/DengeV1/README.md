# Denge V1 veri sahipliği

Esas düzenleme kaynağı `denge_v1_params.py` dosyasıdır. Asset'ler ve özet, `make_denge_v1.py` çıktısıdır.

- Üretimden önce `python make_denge_v1.py --check` mevcut çıktıların son kayıtlı hash'lerle aynı olduğunu doğrular; dosya yazmaz.
- `python make_denge_v1.py` önce aynı kontrolü yapar, sonra üretir. Inspector'da değişmiş, silinmiş veya çıktı dizinine eklenmiş dosya varsa yazmadan durur.
- Inspector denemesi kalıcı tutulacaksa farkı önce parametre dosyasına taşı. Parametre ve asset değişikliğini birlikte incelemeden manifest'i yenileme. Manifest'i körlemesine güncellemek korumayı etkisizleştirir.
- `generated_manifest.json` başarılı üretim sonunda yenilenir. Normal değişikliklerde elle düzenlenmez.
- Kontrol yalnız veri sahipliğini korur; üretici tüm dosyaları atomik bir işlemle yazmaz. Üretim yarıda kesilirse dosya farklarını inceleyerek kurtar.

Regresyon: `python test_generation_guard.py`. Kullanıcının gerçek asset'lerini değiştirmeden geçici dizinlerde çalışır.

104 düğüm farklı ağaç kutularıdır; 221 kademe bu kutulardaki toplam satın alma adedidir.
