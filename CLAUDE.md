# Claude proje talimatları

Her işe başlamadan proje kökündeki `AGENTS.md` dosyasını oku ve içindeki tüm
proje kurallarını uygula. Ortak kuralların ana kaynağı `AGENTS.md` dosyasıdır;
lisans bildirimi, yerelleştirme, görsel tasarım ve editörde düzenleme/önizleme
kuralları Claude için de geçerlidir.

UI ve görsel yerleşim işlerinde özellikle:

- Görünüm Play'e basmadan editörde incelenebilir ve düzenlenebilir olmalıdır.
- Kalıcı tasarım sahne/prefab veya serileştirilmiş tasarım verisinde tutulmalı;
  runtime kodu editörde kaydedilmiş tasarımı sabit değerlerle ezmemelidir.
- Dinamik arayüzler için aynı prefab ve yerleşim hesabını kullanan editör
  önizlemesi veya oluşturma/güncelleme aracı sağlanmalıdır.
- MCP düzenlemeleri Edit Mode'da doğru sahne/prefab'a kaydedilmelidir.
  Yalnız Play Mode'da görünen sonuç tamamlanmış kalıcı düzenleme sayılmaz.
- Editör ve oyun davranışı ayrı doğrulanmalı; önizleme oyun ilerlemesini
  değiştirmemeli, tekrar çalıştırma nesneleri çoğaltmamalıdır.

Ayrıntılar ve kapsam için `AGENTS.md` içindeki
"Zorunlu editörde düzenleme ve önizleme kuralı" bölümünü esas al.
