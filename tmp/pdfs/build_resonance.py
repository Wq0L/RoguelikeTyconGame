from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.colors import HexColor, Color
from pathlib import Path
import math

OUT=Path('output/pdf/Rezონans-Tasarim-Taslagi.pdf'.replace('ონ','on'))
pdfmetrics.registerFont(TTFont('Body','C:/Windows/Fonts/arial.ttf'))
pdfmetrics.registerFont(TTFont('Bold','C:/Windows/Fonts/arialbd.ttf'))
W,H=595.28,841.89
c=canvas.Canvas(str(OUT),pagesize=(W,H))
c.setTitle('Rezonans - Saksının Altındaki Güç | Tasarım Taslağı')
c.setAuthor('ClickerGame - Tasarım taslağı')
ink=HexColor('#202534'); purple=HexColor('#7655B4'); green=HexColor('#087963')
style=ParagraphStyle('body',fontName='Body',fontSize=10.2,leading=14,textColor=ink)
small=ParagraphStyle('small',parent=style,fontSize=9,leading=12)
bold=ParagraphStyle('bold',parent=style,fontName='Bold',fontSize=12,leading=15)
page=0
def p(txt,x,y,width=499,st=style):
    ob=Paragraph(txt,st); _,h=ob.wrap(width,1000); ob.drawOn(c,x,y-h); return y-h
def start(kicker,title,sub):
    global page
    page+=1
    c.setFillColor(HexColor('#F6F2E9'));c.rect(0,0,W,H,fill=1,stroke=0)
    c.setFillColor(ink);c.rect(0,H-15,W,15,fill=1,stroke=0)
    p(kicker.upper(),42,800,511,ParagraphStyle('k',parent=small,textColor=purple,fontName='Bold'))
    p(title,42,776,511,ParagraphStyle('title',parent=bold,fontSize=25,leading=29))
    p(sub,42,735,511,small)
    c.setStrokeColor(ink);c.line(42,52,553,52)
    p('CLICKERGAME  /  TASARIM TASLAĞI v1  /  21.09.2026',42,40,470,ParagraphStyle('f',parent=small,fontSize=8))
    p(f'{page:02}',528,40,30,small)
def end(): c.showPage()
def box(y,title,text,accent=purple,height=87):
    c.setFillColor(HexColor('#DCD5C9'));c.roundRect(45,y-height-3,511,height,8,fill=1,stroke=0)
    c.setFillColor(HexColor('#FFFDF7'));c.setStrokeColor(ink);c.setLineWidth(1.4);c.roundRect(42,y-height,511,height,8,fill=1,stroke=1)
    c.setFillColor(accent);c.rect(42,y-height+9,5,height-18,fill=1,stroke=0)
    yy=p(title,56,y-10,481,bold)
    yy=p(text,56,yy-5,481,small)
    assert yy>=y-height+7,(page,title,yy,y-height)
    return y-height-11
def note(y,text): return p(text,44,y,507,small)

start('Tasarım yönü','Saksının altındaki güç','Kolay kurulan setler fayda verir. Zor kurulan setler oyunun kurallarını değiştirir.')
y=box(686,'01  /  Toplama zorluğu ödülün ölçeğini belirler','2 tile: küçük destek. 3 tile: yeni bir tetikleme döngüsü. 4 tile: güçlü bir oyun tarzı. Tam 6 tile: ekranda hissedilen, turu taşıyabilen büyük ödül.',height=90)
y=box(y,'02  /  Yüzdeden çok davranış','Zor tariflerin hedefi yalnızca +%hasar değildir: patlama şimşeği çağırır, şimşek yankılanır, zincirin sonu yeni bir hasat fırsatı yaratır. Güç oyuncunun görebildiği olaylarla anlatılır.',height=90)
y=box(y,'03  /  Saksı tarifi taşır','Gerekli tile’lar aynı saksının kapladığı alanda bulunur. Sıralama ve komşuluk şartı yoktur. 2x3 saksı en fazla 6 tile taşır. Aynı tile tarifte iki farklı tür yerine geçmez.',height=90)
y=box(y,'04  /  Rarity ikinci bir şans duvarı olmaz','İlk sürümde tarifler Common / Rare / Epic / Legendary şartı aramaz. Tile rarity’si kendi temel bonusunu etkiler. Zaten zor olan altılı dizilimin önüne bir de Legendary şartı koymayız.',height=90)
y=box(y,'05  /  Bu belge uygulama değildir','Mevcut dosyalarda 8 tile ailesi ve 3 tek-tür rezonans var. Yeni davranışlar, karışık tarifler ve aşağıdaki tüm yeni denge sayıları öneridir. Oyun kodu, sahne ve asset’ler değiştirilmedi.',height=90)
note(y-2,'Kapsam: 8 mevcut aile + 2 yeni davranış önerisi, 8 tek-tür seti, 22 karışık tarif, tetikleme kuralları ve test planı. Önceki fikirler bu zorluk ölçeğine göre yeniden dengelendi; eski ve yeni sayılar birlikte kullanılmamalı.')
end()

start('Tile sözlüğü','10 aile, belirgin roller','İlk sekiz aile projede mevcut. Son iki aile yeni öneridir; henüz eklenmiş değildir.')
rows=[('Fertile / Verimli','Üretim aralığını azaltır; yeniden büyüme ve üretim döngülerinin temeli.'),('Water / Su','XP kazancını artırır; davranışlarla kazanılan XP’yi uzmanlaştırır.'),('Crystal / Kristal','Bitki rarity dağılımını etkiler; değerli bitki odaklı tariflerin anahtarı.'),('Damage / Odak','Bu saksının bitkilerinin aldığı doğrudan hasarı artırır.'),('Energy / Enerji','Önerilen rol: davranış hasarını beslemek. Mevcut planter HarvestDamage bağlantısı ayrıca doğrulanıp tamamlanmalı.'),('Explosive / Patlama','Hasat sırasında alan hasarı üretir; mevcut doğrudan kaynak kısıtı korunuyor.'),('Duplicate / İkiz','Mevcut tetiklemede kaynak, XP ve skor ikiye katlanır.'),('Tornado / Hortum','Hareket eden hasar davranışı. Mevcut sistemde eşzamanlı hortum sınırı bulunur.')]
y=684
for name,txt in rows:
    y=p('<b>'+name+'</b> - '+txt,46,y,503,small)-10
y-=2
y=box(y,'YENİ  /  Yankı','Her 4 doğrudan hasatta 1 Yankı yükü kazan. En fazla 1 yük sakla. Sonraki Patlama, Hortum veya Şimşek oluşumunu 0,35 sn sonra %40 güçle tekrar et; yük tüketilir. Tek başına, sonraki doğrudan vuruşu %25 hasarla tekrarlar.',green,100)
y=box(y,'YENİ  /  Şimşek','Her 5 doğrudan hasatta, en fazla 3 canlı bitkiye sırasıyla %60 / %45 / %30 H hasarı ver. Her sıçrama önceki hedeften en fazla 2 hücre uzağa gider; aynı zincirde aynı bitki ikinci kez seçilmez.',green,90)
note(y,'H = tetikleme anındaki oyuncu temel hasat hasarı. Yankılanan hortum: %40 hasar, aynı süre. Yeni davranışların ilk sürümü sayaçlıdır; tarif toplandıktan sonra ayrıca düşük bir proc şansı beklenmez.')
end()

start('Tek-tür setleri','Aynı aileyi toplamanın ödülü','Bunlar mevcut kuralların yerine değerlendirilecek denge önerileridir; üzerine ek bonus değildir.')
y=685
for title,txt in [
('Verimli','2 tile: üretim aralığı x0,95. 3 tile: x0,85. 4 tile: her 8 doğrudan hasatta bir boş hücre hemen üretir.'),
('Su','2 tile: XP x1,10. 3 tile: XP x1,25. 4 tile: her 10. hasadın XP’si x5 olur.'),
('Kristal','2 tile: rarity statına +3 puan. 3 tile: +7 puan. 4 tile: her 8. üretim mevcut Rare veya üstü havuzdan seçilir.'),
('Odak','2 tile: doğrudan hasar x1,10. 3 tile: x1,30. 4 tile: her 6. doğrudan vuruş x3 hasar verir.'),
('Enerji','2 tile: davranış hasarı x1,10. 3 tile: x1,25. 4 tile: her 5. birincil hasar davranışı x2 güçle oluşur.'),
('Patlama','2 tile: patlama hasarı x1,10. 3 tile: x1,25. 4 tile: her 4. birincil patlamadan 0,25 sn sonra %50 güçte ikinci patlama gelir.'),
('İkiz','2 tile: başarılı ikizin ekstra kaynak kopyasına +%5. 3 tile: +%15. 4 tile: her 8. doğrudan hasatta kaynak kopyası garanti; normal proc ile üçüncü kopya oluşmaz.'),
('Hortum','2 tile: süre x1,10. 3 tile: x1,20. 4 tile: her 3. birincil hortum %50 daha geniştir; hedef başına vuruş sıklığı aynı kalır.')]:
    y=box(y,title,txt,height=65)
note(y,'Yalnızca erişilen en yüksek tek-tür kademe aktiftir; 4 tile, 2 ve 3 tile etkilerini ayrıca almaz. Yankı ve Şimşek için başlangıçta tek-tür seti yok; güçleri karışık tariflerde açılır.')
end()

def recipes(kicker,title,sub,items,foot):
    start(kicker,title,sub);y=686
    for name,recipe,effect in items:
        y=box(y,name,'<b>'+recipe+'</b><br/>'+effect,height=88)
    note(y,foot);end()

recipes('Kolay / 2 tile','Küçük ama hissedilir','1x2 ve daha büyük saksılarda; ana oyun döngüsüne sade destek.',[
('01  Can Suyu','1 Verimli + 1 Su','Üretim aralığı x0,95; bu saksının hasat XP’si x1,10.'),
('02  Seçkin Filiz','1 Verimli + 1 Kristal','Rarity statına +3 puan. Her 10. doğrudan hasat, aynı hücrenin sonraki üretim aralığını %15 azaltır.'),
('03  Ustalık','1 Su + 1 Odak','Bu saksıdaki doğrudan hasatların XP’si +%15. Alan hasarıyla yapılan hasatlar bu bonustan yararlanmaz.'),
('04  Yüklü Patlama','1 Enerji + 1 Patlama','Bu saksıdan çıkan birincil patlamalar +%15 hasar verir.'),
('05  Rüzgârla Filizlenme','1 Verimli + 1 Hortum','Saksının en az bir hortumu aktifken üretim aralığı x0,93. Birden fazla hortum bonusu büyütmez.'),
('06  İkiz Ders','1 Su + 1 İkiz','İkiz tetiklenmeyen hasatlarda XP +%15; tetiklenen hasatta normal ikiz ödülü uygulanır.')],
'Bu seviyede zincir başlatan patlama yağmuru veya garantili değerli hasat yok. Bir çiftin tesadüfen oluşması güzel bir küçük kazanım olmalı.')

recipes('Orta / 3 tile','Davranışlar konuşmaya başlar','1x3 veya daha büyük saksı; her tarif tek bir anlaşılır döngü kurar.',[
('07  Patlamadan Doğuş','2 Verimli + 1 Patlama','Her 3. birincil patlama, bu saksıda en uzun süredir bekleyen boş hücrenin kalan üretim süresini %50 azaltır.'),
('08  Prizma Yükü','2 Kristal + 1 Enerji','Her 3 Rare+ hasat, sonraki hasar davranışına +%75 güç yükler. En fazla 1 yük; davranış oluşunca tüketilir.'),
('09  Hasat Döngüsü','1 Verimli + 1 Odak + 1 İkiz','Doğrudan hasatta ikiz tetiklenirse aynı hücrenin sonraki üretim aralığı %40 azalır. Hücre başına tek yük.'),
('10  Yankılı Fitil','1 Yankı + 1 Enerji + 1 Patlama','Her 4. birincil patlama, 0,35 sn sonra %60 güçle bir kez tekrarlanır. Bu tarif tekrarı Yankı yükü tüketmez.'),
('11  Bilgelik Akımı','1 Şimşek + 1 Su + 1 Odak','Şimşek öldürmelerinin XP’si +%40. Her 3 şimşek öldürmesi, sıradaki temel Şimşek sayacına +1 ekler.'),
('12  Mücevher Rüzgârı','1 Kristal + 1 İkiz + 1 Hortum','Bu saksının hortumunun öldürdüğü Rare+ bitkiler +%40 kaynak verir; XP ve skor bu ek ödülden etkilenmez.')],
'Rare+ = Rare, Epic veya Legendary bitki. “Birincil”, bir tekrardan veya başka bir tarifin alt olayından doğmamış davranıştır. Sayaçlar saksıya aittir.')

recipes('Zor / 4 tile','Bir saksı, bir oyun tarzı','2x2 veya 2x3 saksı. Güç artışı belirgin; olaylar birbirini doğurmaya başlar.',[
('13  Fırtına Reaktörü','1 Enerji + 1 Patlama + 1 Hortum + 1 Şimşek','Her birincil hortum ilk isabetinde 1 patlama (%100 H) üretir. Patlamanın ilk öldürmesi, 3 hedefli Şimşek başlatır (%60 / %45 / %30 H).'),
('14  Çatallanan Yankı','2 Şimşek + 1 Yankı + 1 Enerji','Her 3. birincil Şimşek, %100 / %75 / %50 / %35 H ile 4 hedefe gider. 0,35 sn sonra aynı rota %50 güçle tekrarlanır.'),
('15  Kusursuz Hasat','1 Odak + 1 Kristal + 1 İkiz + 1 Yankı','Rare+ bitkiyi doğrudan tek vuruşla hasat et: kaynak ödülü toplam x3 olur; XP ve skor x1. Normal İkiz bu ödüle ayrıca eklenmez.'),
('16  Kök Motoru','2 Verimli + 1 Patlama + 1 İkiz','Her 3 birincil patlama öldürmesi, en eski boş hücreyi anında üretir. O bitkinin sonraki kaynak ödülü toplam x2; normal İkiz ile x3 olmaz.'),
('17  Fırtına Akademisi','2 Su + 1 Hortum + 1 Şimşek','Hortum ve Şimşek öldürmelerinde XP x2. Her 8 böyle öldürmede, saksının bir sonraki doğrudan hasadı XP x5 verir; bir yük saklanır.'),
('18  Kristal Maden','2 Kristal + 1 Patlama + 1 Yankı','Rare+ doğrudan hasat, %150 H kristal patlaması üretir; 0,35 sn sonra %75 H ile tekrarlar. Bu iki olayın öldürmeleri +%50 kaynak verir.')],
'Bu tarifler temel davranış kaynak kısıtına istisna getirir. İstisna yalnızca yazılı adımlar içindir; her patlama kendiliğinden yeni patlama başlatmaz.')

start('Çok zor / tam 6 tile','Jackpot: turu değiştiren setler','Yalnızca 2x3 saksı. Büyük ödül, nadir oluşan tam tarifin karşılığıdır.')
y=686
for name,recipe,txt in [
('19  KIYAMET MAKİNESİ','2 Enerji + 2 Patlama + 1 Şimşek + 1 Yankı','Her 8 doğrudan hasatta bir Felaket başlat: %300 H patlama; en fazla 6 farklı hedefe %120 H Şimşek; vurulan her hedefte %100 H mini patlama; tüm dizinin %50 güçlü Yankısı. Tek Felakette en fazla 28 hasar olayı. Mini patlamalar 1 hücre yarıçaplıdır.'),
('20  YAŞAM TAŞMASI','3 Verimli + 1 Su + 1 İkiz + 1 Yankı','Her 12 doğrudan hasatta 8 sn Bereket: üretim aralığı x0,20; bu saksıda hasat kaynağı toplam x3, XP x2. Başlangıçta en fazla 6 boş hücre hemen üretir. Bereket sırasında sayaç ilerlemez; bitince 0’dan başlar. Dolu hücreler ezilmez.'),
('21  ALTIN ÇAĞ','3 Kristal + 2 İkiz + 1 Yankı','Her 8 Rare+ doğrudan hasatta, sonraki 3 üretim mevcut Epic+ havuzdan seçilir. Bu 3 bitkinin kaynak ödülü toplam x5, XP x2 olur. Havuzda Epic+ yoksa mevcut en yüksek rarity kullanılır. Kuyruk en fazla 3; kuyruk doluyken yeni sayaç işlemez.'),
('22  GÖĞÜ YIRTAN FIRTINA','2 Hortum + 2 Şimşek + 1 Enerji + 1 Yankı','Her 10 doğrudan hasatta 6 sn Süper Hortum çağır: normal hortumun x3 hasarı ve x1,5 alanı. Saniyede 1 kez, en fazla 4 hedefe %100 H Şimşek yollar. Süre sonunda %400 H patlama yapar. Aktifken sayaç durur; yeni tetik süresini uzatmaz.')]:
    y=box(y,name,'<b>'+recipe+'</b><br/>'+txt,HexColor('#BA7C12'),height=122)
note(y,'Jackpot ve normal İkiz kaynak çarpanları üst üste çarpılmaz. Süper Hortum mevcut global hortum bütçesinde 1 yer kullanır; yer yoksa en eski normal hortumun yerini alır. Böylece güç büyük kalır, nesne sayısı kontrolsüz büyümez.')
end()

start('Zorluk ve seçim','Neden altılı ödül uçuk?','Tarif uzunluğu tek başına yetmez: havuz, kilitler, alan ve oyuncunun seçim hakkı birlikte ölçülür.')
y=box(686,'Örnek olasılık modeli - gerçek oyun oranı değildir','10 aile eşit olasılıklı, tile’lar bağımsız, saksı tamamen dolu ve dizilim sırası önemsiz varsayılsın. Belirli bir çift 1+1: %2. Belirli üçlü 1+1+1: %0,6. Belirli dörtlü 1+1+1+1: %0,24. Belirli altılı 2+2+1+1: %0,018 (yaklaşık 5.556’da 1).',height=106)
y=box(y,'Gerçek oyunda rastgele atama ayrıca zorlaştırır','Mevcut kart akışı bonusu rastgele uygun boş hücreye atıyor. Oyuncunun kart seçimi, açılmış aileler, saksı yerleştirme ve birden fazla aday alan gerçek olasılığı değiştirir. Yukarıdaki oranlar bir run’da tarif tamamlama ihtimali değildir.',height=95)
y=box(y,'Jackpot yalnızca teoride kalmamalı','Öneri: tur sonlarında run başına 2 kez, mevcut iki modifier’ın hücrelerini takas etme hakkı. Kilitli hücre hedeflenemez. Bu yeni bir özellik önerisidir; mevcut sistemde var sayılmıyor. Hak sayısı gerçek tamamlanma verisiyle ayarlanır.',height=95)
y=box(y,'Bir saksı için bir ana tarif','En yüksek tile sayısı isteyen karışık tarif otomatik önerilir. Aynı zorlukta birden fazla tarif varsa oyuncu tur arası birini seçer. En fazla 1 karışık + 1 tek-tür seti aktif. Karışık tarifin kullandığı aileler aynı anda tek-tür rezonansı vermez; temel tile bonusları kalır.',height=106)
y=box(y,'Testte hedeflenen karşılaşma oranı','İlk ölçüm hedefi, uygun saksısı ve gerekli aileleri açılmış run’lar için: en az bir çift %70-90; üçlü %30-50; dörtlü %10-20; altılı %1-3. Bunlar tahmin değil ürün hedefi; havuz ve takas imkânıyla ulaşılabilirliği ölçülecek.',height=95)
note(y,'Tarifleri yapay şekilde aşırı nadirleştirip ödülü artırmak tek başına iyi tasarım değildir. Oyuncu “bir tile daha bulsam çalışacak” durumunu okuyabilmeli ve bunun peşinden gidebilmelidir.')
end()

start('Davranış davranışı doğurur','Zincir güçlü, sonucu anlaşılır','Etkiyi kısmak yerine her tarifin kaç adım üreteceğini açıkça tanımlıyoruz.')
y=box(686,'Örnek: Kıyamet Makinesi / H = 100','Başlangıç patlaması 300 hasar. Şimşek 6 hedefin her birine 120. Altı mini patlamanın her biri 100. Yankı bunları sırasıyla 150 / 60 / 50 ile tekrarlar. Hedef yoksa o adım atlanır; altı hedef varmış gibi ödül üretilmez.',height=98)
y=box(y,'Kaynak saksı ile hedef saksıyı ayır','Zincirin hasarı ve tarif sayacı kaynak saksıya aittir. Bitkinin temel kaynak / XP / skor ödülü kendi saksısına aittir. Bir tarif “bu olayın öldürmeleri” diyorsa sadece o olayın ek ödülünü uygular. Aynı ölüm iki ödül çağrısı üretmez.',height=95)
y=box(y,'Yankı kendini yankılamaz','Her olay kaynak saksı, kök olay ve zincir adımı taşır. Yankı kopyası tekrar Yankı yükü kazanmaz. Temel davranışların sayaçları doğrudan hasatla dolar. Tarif açıkça saymıyorsa alt olaylar sayaç ilerletmez.',height=95)
y=box(y,'Başka tarifle birleşme kuralı','Olayı oluşturan tarif kendi yazılı zincirini tamamlar. Alt olaylar başka bir karışık tarifi tetiklemez. Temel Yankı yükü, tarifin zaten yankılanan olayı üzerinde tüketilmez. Böylece belgede yazan bir tekrar sessizce üç tekrara dönüşmez.',height=95)
y=box(y,'Tur geçişi ve saksı taşıma','Sayaçlar yalnızca Round durumunda ilerler. Tur arası aynı saksının aynı tarifi koruması sayacı korur; taşıma yeni yük vermez. Tarif değişirse o tarifin sayaç / bekleyen yükleri sıfırlanır. Yeni run her şeyi sıfırlar. Süreli etkiler oyun durduğunda durur.',height=95)
note(y,'Görsel sunum: yeşil üretim darbesi, altın ödül patlaması, mor Yankı halkası, camgöbeği Şimşek. Rezonans adı oyuna dönüldükten sonra saksının üstünde comic hasar yazısı gibi görünür. Her alt olayda yeniden büyük başlık gösterilmez.')
end()

start('Uygulama öncesi kontrol','Önce küçük bir oynanabilir set','Belge bir tasarım önerisidir. Aşağıdaki çalışmalar ayrıca uygulama gerektirir.')
y=box(686,'1  /  Mevcut davranışları sağlamlaştır','Energy’nin planter HarvestDamage etkisi ile Player HarvestDamage okuyan Patlama / Hortum akışını aynı tasarımda bağla. Explosive Common’ın mevcut %100 değeri, diğer kademelerden güçlü oluşu açısından incelenmeli. Tornado rarity / şans asset tutarlılığı doğrulanmalı.',height=100)
y=box(y,'2  /  İlk deneme kapsamı','Önce Yankı ve Şimşek temel davranışları; ardından Can Suyu, Patlamadan Doğuş, Fırtına Reaktörü ve Kıyamet Makinesi. Böylece 2 / 3 / 4 / 6 tile güç sıçramaları aynı testte karşılaştırılır. Diğer tarifler ikinci dalgaya kalır.',height=95)
y=box(y,'3  /  Dengeyi ölç','Her run için uygun saksı sayısı, tamamlanan tarif, tamamlanma turu, zincir başına olay sayısı, hasar, kaynak ve XP kaydet. Altılı set, kurulabildiğinde benzer temel düzene göre yaklaşık 3-6 kat kısa süreli üretim / hasar etkisi hedefler; sürekli x6 garanti edilmez.',height=95)
y=box(y,'4  /  Anlamlı doğrulama','Aynı bitkinin iki kez ödül vermemesi; son hedefin ölmesi; boş saksı; dolu hortum bütçesi; tur sırasında duraklatma; tarif değiştirme; save/load sayaçları; yüksek hızda olay bütçesi. 28 olaylık jackpot görsel havuzla çalışmalı, her olay için nesne yaratıp silmemeli.',height=95)
y=p('<b>İncelenen proje kaynakları</b>',46,y-3,503,bold)-9
for s in ['Assets/Scripts/ScriptableObjects/TileModifierSO.cs - mevcut 8 aile ve davranış enum’ları.', 'Assets/Resources/ResonanceRules.asset - Odak 2/3/4 tile: x2/x4/x8; Su 2/3: x2/x10; Verimli 2/3: üretim aralığı x0,75/x0,50.', 'Assets/Scripts/Managers/ResonanceManager.cs - aynı tür sayımı ve en yüksek kademe; mevcut değerlendirme karışık tarif çalıştırmıyor.', 'Önceki proje incelemesi: PlanterBrain, PlantResource, ProgressionManager ve GridModifiers asset’leri; enerji, ödül sahipliği ve rastgele atama notlarının dayanağı.']:
    y=p(s,46,y,503,small)-9
note(y-4,'Karar özeti: kolay set küçük fayda; zor set yeni davranış; tam altılı set gösterişli güç patlaması. Sayılar playtest başlangıcıdır, mevcut oyunun doğrulanmış dengesi değildir.')
end()
c.save()
print(OUT.resolve())
