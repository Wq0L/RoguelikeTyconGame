from pathlib import Path
import json, re, math
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Flowable
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.pagesizes import A4

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/pdf/ClickerGame_Oyuncu_Rehberi.pdf'
OUT.parent.mkdir(parents=True,exist_ok=True)
pdfmetrics.registerFont(TTFont('Arial','C:/Windows/Fonts/arial.ttf'))
pdfmetrics.registerFont(TTFont('ArialB','C:/Windows/Fonts/arialbd.ttf'))
pdfmetrics.registerFontFamily('Arial',normal='Arial',bold='ArialB',italic='Arial',boldItalic='ArialB')
navy=colors.HexColor('#142D38'); teal=colors.HexColor('#137D80'); gold=colors.HexColor('#D49A38'); ink=colors.HexColor('#263E47'); pale=colors.HexColor('#EDF5F4')
styles={
 'body':ParagraphStyle('body',fontName='Arial',fontSize=10.3,leading=15.4,textColor=ink,spaceAfter=10),
 'h':ParagraphStyle('h',fontName='ArialB',fontSize=24,leading=29,textColor=navy,spaceAfter=17),
 'sub':ParagraphStyle('sub',fontName='ArialB',fontSize=13,leading=17,textColor=teal,spaceBefore=10,spaceAfter=7),
 'small':ParagraphStyle('small',fontName='Arial',fontSize=8.4,leading=11.8,textColor=ink,spaceAfter=7),
 'cell':ParagraphStyle('cell',fontName='Arial',fontSize=8.7,leading=12,textColor=ink),
 'th':ParagraphStyle('th',fontName='ArialB',fontSize=8.7,leading=12,textColor=colors.white),
 'kicker':ParagraphStyle('kicker',fontName='ArialB',fontSize=10,leading=14,textColor=teal,spaceAfter=12),
 'title':ParagraphStyle('title',fontName='ArialB',fontSize=39,leading=45,textColor=navy,spaceAfter=21),
}
story=[]
def p(t,style='body'): story.append(Paragraph(t,styles[style]))
def sub(t): p(t,'sub')
def page(n,title,lead=None):
 if story: story.append(PageBreak())
 p(f'OYUNCU REHBERİ  /  {n}','kicker'); p(title,'h')
 if lead:p(lead)
def table(headers,rows,widths=None):
 data=[[Paragraph(str(x),styles['th']) for x in headers]]+[[Paragraph(str(x),styles['cell']) for x in row] for row in rows]
 t=Table(data,colWidths=widths or [499/len(headers)]*len(headers),repeatRows=1,hAlign='LEFT')
 t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),navy),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,pale]),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),9),('RIGHTPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8),('LINEBELOW',(0,0),(-1,0),1,teal)]))
 story.append(t);story.append(Spacer(1,12))
def note(title,text):
 t=Table([[Paragraph('<b>'+title+'</b><br/>'+text,styles['body'])]],colWidths=[499]);t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,-1),pale),('BOX',(0,0),(-1,-1),0.6,teal),('LEFTPADDING',(0,0),(-1,-1),13),('RIGHTPADDING',(0,0),(-1,-1),13),('TOPPADDING',(0,0),(-1,-1),12),('BOTTOMPADDING',(0,0),(-1,-1),5)]));story.append(t);story.append(Spacer(1,12))

class Loop(Flowable):
 def __init__(self):Flowable.__init__(self);self.width=499;self.height=191
 def draw(self):
  c=self.canv
  labels=[('SAKSI','Üretim alanını kur'),('HASAT','Fareyle alanı yönet'),('YATIRIM','Ağaç + kart + zemin'),('ÖDÜL','Kaynak + XP + skor')]
  for i,(a,b) in enumerate(labels):
   x=(i%2)*259;y=108-(i//2)*96;c.setFillColor(navy if i==0 else pale);c.roundRect(x,y,240,77,10,fill=1,stroke=0);c.setFillColor(colors.white if i==0 else teal);c.setFont('ArialB',13);c.drawString(x+15,y+48,a);c.setFont('Arial',10);c.drawString(x+15,y+25,b)
  c.setStrokeColor(gold);c.setLineWidth(2)
  for x,y,dx,dy in [(242,146,13,0),(379,104,0,-13),(255,50,-13,0),(120,93,0,13)]:
   c.line(x,y,x+dx,y+dy);ex=x+dx;ey=y+dy;c.line(ex,ey,ex-dx*.35+dy*.25,ey-dy*.35-dx*.25);c.line(ex,ey,ex-dx*.35-dy*.25,ey-dy*.35+dx*.25)

p('CLICKERGAME  /  MEVCUT SÜRÜM','kicker');story.append(Spacer(1,20))
p('Hasattan<br/>güçlü bir bahçeye','title')
p('Oyuncu için sistemler, bağlantılar ve oynanış rehberi','sub')
p('Saksılarını yerleştir. Üretimi hasada dönüştür. Kartlarla zemini güçlendir; aynı saksıda biriken etkilerle rezonans aç. Her tur, bir sonraki turda nasıl büyüyeceğine dair yeni bir karar verir.')
story.append(Loop());story.append(Spacer(1,17))
note('Oyunun çekiciliği','Basit fare kontrolünün altında yerleşim, üretim, hasar ve şans kararları birlikte çalışır. Tek bir yeni kart, aynı saksıda bir eşiği tamamlayarak küçük bir bonusu büyük bir güç sıçramasına çevirebilir.')
p('19 Eylül 2026 • Yerel proje dosyalarına göre hazırlanmıştır. Bu rehber mevcut kod, oyun sahnesi ve veri dosyalarını esas alır; canlı oyun testi veya nihai denge onayı değildir.','small')
p('Okuma yolu: 2-3 başlangıç • 4-5 üretim ve ödüller • 6-9 kartlar ve davranışlar • 10-14 yetenek ağacı • 15-17 ilerleme, strateji ve sürüm notları','small')

page('01','Bir koşu nasıl ilerler?','Hedef, süreli turlar boyunca bahçeni geliştirip mümkün olduğunca yüksek Hasat Skoru toplamaktır. Mevcut oyun sahnesinde bir koşu 130 tur sürer.')
table(['Aşama','Ne yaparsın?','Sonraki sisteme etkisi'],[
 ['Hazırlık','İlk turdan önce mağazada saksı ve yetenek alırsın.','Üretim kapasiten ve ilk hasat gücün oluşur.'],
 ['Tur','Fareyi bitkilerin üzerinde gezdirirsin; alan saldırısı otomatik çalışır.','Ölen bitki kaynak, XP ve skor verir.'],
 ['Seviye','XP eşiğini geçince seviye yükselir.','Her seviye için tur sonuna bir kart seçimi eklenir.'],
 ['Kart seçimi','Sunulan kartlardan birini seçer veya hakkın varsa atlarsın.','Kart rastgele uygun zemine yerleşir; saksı bonusu ve rezonans değişebilir.'],
 ['Tur arası','Haritayı inceler, mağazada yatırım yaparsın.','Sonraki tur daha güçlü üretim ve hasatla başlar.'],
 ['Koşu sonu','130. turun süresi biter.','Son skor gösterilir; bu turdan kalan kart seçimleri açılmaz.']],[80,207,212])
sub('Zaman hangi anlarda akar?')
p('Turun taban süresi 30 saniyedir. Süre yatırımlarıyla en fazla 90 saniyeye çıkar ve yeni tur başında yeniden hesaplanır. Kart, mağaza, yerleştirme ve tur sonu sırasında oyun zamanı durur; karar vermek için acele etmen gerekmez.')
note('Üç ayrı ilerleme hattı','Kaynaklar satın alma içindir. XP seviye ve kart seçimi üretir. Hasat Skoru koşunun sonucudur. XP kazanmak, doğrudan yetenek puanı veya harcanabilir para vermez.')

page('02','Kontroller ve ilk hamleler')
table(['Eylem','Kontrol / sonuç'],[
 ['Hasat alanını yönlendir','Fareyi zeminde hareket ettir. Daire içindeki bitkiler belirli aralıklarla otomatik hasar alır; sürekli tıklaman gerekmez.'],
 ['Saksı yerleştir','Mağazadan satın al; uygun zeminde sol tıkla. Kaplanan hücrelerin tamamı açık ve başka saksıdan boş olmalıdır.'],
 ['Saksıyı döndür','Yerleştirme sırasında R: her basışta 90 derece.'],
 ['Yerleştirmeyi iptal et','Esc: mağazaya dönersin. Mevcut sürümde bedelin yalnızca yarısı iade edilir.'],
 ['Saksı sat','Mağazada satış modunu açıp saksıya sol tıkla. Bedelin yarısı döner; üzerindeki bitkiler ödülsüz kaldırılır.'],
 ['Bilgi oku','Kartta gösterilen bonusu, yetenek bilgi kutusunu ve hücrenin saksı rezonanslarını incele.'],
 ['Görüntü ayarları','Seçenekler menüsünde çözünürlük ve tam ekran ayarlanır.']],[125,374])
sub('İlk turun pratik sırası')
p('<b>1.</b> Açık 3×3 alanına sığacak saksıları satın al ve yerleştir.<br/><b>2.</b> Hasar, saldırı aralığı veya süre yatırımlarından yararlan.<br/><b>3.</b> Turu başlat; dolu hücreleri hasat dairenin içinde tut.<br/><b>4.</b> Tur sonundaki kartlardan sonra zemini yeniden incele.<br/><b>5.</b> Kazandığın kaynakla üretim, hasat veya genişleme ihtiyacını karşıla.')
note('Bu kopyanın başlangıç bakiyesi','Mevcut kod başlangıçta 80.000 Altın, 80.000 Demir ve 80.000 Taş verir. Bu yüksek bakiye pek çok yatırımı erkenden denemeyi mümkün kılar. Eski notlardaki 80 Altın başlangıcı bu kopyanın davranışı değildir.')

page('03','Saksı ve zemin birlikte çalışır','Saksı, kapladığı bütün bonuslu hücreleri ortak bir havuz gibi kullanır. Bir hücrenin katkısı yalnızca o hücredeki bitkiye değil, aynı saksının bitkilerine uygulanır.')
table(['Boyut','Satın alma bedeli','Erişim','Rolü'],[
 ['1×1','20 Altın','Başlangıçta açık','Esnek yerleşim; tek hücreyle rezonans açamaz.'],
 ['1×2','40 Altın','Başlangıçta açık','İki aynı aile hücresiyle ilk rezonans eşiği.'],
 ['1×3','90 Altın','P2 kilidi','Üçlü XP/üretim ve üçlü Odak eşiği.'],
 ['2×2','120 Demir','P5 kilidi','Dört hücre; en yüksek Odak eşiğine ulaşabilir.'],
 ['2×3','250 Taş','P8 kilidi','Altı hücre; birden fazla aileyi birleştirme alanı.']],[48,93,89,269])
sub('Üretim nasıl yenilenir?')
p('Mevcut saksı verilerinde taban üretim aralığı 5 saniyedir. Bir üretim noktası üzerinde yaşayan bitki varsa yenisini üretmez. Hasat edilince sayaç sıfırlanır, sonraki bitki için bekleme başlar. İlk üretim zamanları rastgele dağıtılır. Üretim süresi bonuslarla kısalır; alt sınır 0,1 saniyedir.')
sub('Büyük saksı neden farklıdır?')
p('Daha fazla hücre, daha fazla üretim yeri ve aynı saksı içinde daha yüksek rezonans ihtimali sağlar. Fakat boyut tek başına her açıdan üstünlük demek değildir: saksıların bitki dağılımları da farklıdır. Örneğin 1×3 saksının temel nadir bitki dağılımı, 2×2 saksıdan daha yüksektir.')
note('Zemin, saksıdan bağımsız kalır','Saksı satıldığında hücre bonusları silinmez. Aynı bonuslu alanın üzerine daha büyük bir saksı kurarak önceden ayrı duran etkileri tek saksıda toplayabilirsin. Satış, bitki hasadı sayılmaz.')

page('04','Bitkiler, kaynaklar ve nadirlik')
table(['Bitki','Nadirlik','Taban kaynak','Taban skor'],[
 ['Çimen / Grass','Common','2 Altın','1'],['Pancar, Mısır','Uncommon','4 Altın','3'],
 ['Havuç, Marul, Çilek','Rare','1 Demir','10'],['Biber, Patates, Domates','Epic','1 Taş','30'],
 ['Üzüm','Legendary','30 Altın','100'],['Ananas','Legendary','10 Demir','100'],['Balkabağı','Legendary','5 Taş','100']],[160,88,147,104])
p('Bütün mevcut bitkiler bonuslardan önce 20 XP verir. Kaynak miktarı ilgili Altın/Demir/Taş çarpanından; XP ise saksının XP çarpanından etkilenir. Sonuçlar tam sayıya yuvarlanır. Çifte Hasat tutarsa kaynak, XP ve skor ikiye katlanır.','small')
sub('Saksıya göre temel nadirlik dağılımı')
table(['Saksı','Common','Uncommon','Rare','Epic','Legendary'],[
 ['1×1','%60','%25','%12','%3','%0'],['1×2','%78','%18','%4','%0','%0'],['1×3','%35','%30','%22','%10','%3'],['2×2','%55','%27','%13','%4','%1'],['2×3','%40','%25','%20','%12','%3']],[69,86,86,86,86,86])
p('Bunlar nadirlik bonusu yokken yaklaşık oranlardır. Crystal ve Nadir Filizler, Common dışındaki mevcut ağırlıkları büyütür; yüksek nadirliklere daha fazla ağırlık verir. Bir bitkinin başlangıç ağırlığı 0 ise bonus onu havuza eklemez.','small')
note('Nadirlik, kaynak erişimini değiştirir','1×2 saksı temel havuzunda Taş veren bitki yoktur. Taş ekonomisi için uygun üretim havuzuna geçmek gerekir. Crystal üzerindeki “+15” değeri, “Legendary gelme ihtimali doğrudan %15” anlamına gelmez.')

page('05','XP ve kart seçiminin kuralları')
sub('Seviye eşiği büyür')
p('İlk seviye için 100 XP gerekir. Sonraki eşikler her seviyede ×1,2 artar: 100, 120, 144, 172,8... Bir ödül birkaç eşiği birden aşabilir; artan XP kaybolmaz. Seviye artışı anında olur, kart seçimleri tur sonuna birikir.')
sub('Kart seçmek zemini seçmek değildir')
p('Her seçimde üç kart sunulur. Aile, nadirlik ve sayısal bonus ayrı belirlenir. Kartın üstünde gördüğün değer, seçtiğinde aynen zemine aktarılır. Hedef, <b>kilidi açık ve henüz bonusu olmayan rastgele bir hücredir</b>; üzerinde saksı olması gerekmez. Oyuncu hücreyi elle seçmez ve dolu bonusun üzerine yeni kart yazılmaz.')
sub('Kart şansı neyi değiştirir?')
p('Kart Sezgisi, kartın Rare/Epic/Legendary olma ağırlığını artırır. Crystal ise bitki üretimindeki nadirliği etkiler. Bu iki şans sistemi ayrı çalışır. Aile ağırlıkları da eşit değildir; kilitli kartlar seçime katılmaz.')
table(['Atlama','İşleyiş'],[
 ['Hak','Her tur başında 1 hak; İlk Vazgeçiş ve İkinci Vazgeçiş ile toplam 3 olabilir.'],
 ['Bedel','O seviye için kart seçimi tüketilir; kart yenileme değildir.'],
 ['Ödül','Sunulan en yüksek nadirliğe göre taban: Common 2, Rare 4, Epic 7, Legendary 10.'],
 ['Hesap','Taban × (1 + tur × 0,1), sonra yuvarlama. Ödül Altın, Demir veya Taştan rastgele biridir.']],[85,414])
note('Uygun hücre kalmadığında','Mevcut sürümde kart seçimi yine tüketilir ancak zemine bonus eklenmez. Açık alandaki boş bonus yerlerini takip et; alan genişlemesi yeni kart yerleri de açar. Örnek: 10. turda Legendary içeren seçimi atlamak 20 adet rastgele kaynak verir.')

page('06','Sekiz kart ailesi','Kart ailelerini şu soruyla değerlendir: daha çok bitki mi, daha hızlı hasat mı, daha fazla XP mi, yoksa ek hasat olayı mı istiyorum?')
table(['Aile','İşlev ve bağlantı','Common / Rare / Epic / Legendary'],[
 ['Fertile<br/>Verimli','Üretim bekleme süresini çarparak azaltır. Boşalan üretim noktası daha çabuk dolar.','%10-15 / %18-25 / %28-38 / %40-55 süre azalması'],
 ['Water<br/>Su','Saksının XP kazancını artırır; daha fazla seviye ve kart getirir.','+%15-25 / +%30-45 / +%50-70 / +%80-120 XP'],
 ['Crystal<br/>Kristal','Mevcut nadir bitki ağırlıklarını artırır; kaynak ve skor dağılımını değiştirir.','+10-15 / +18-28 / +30-45 / +50-70 nadirlik bonusu'],
 ['Damage<br/>Odak','Saksının bitkilerine yaptığın doğrudan hasarı artırır.','+%5-10 / +%10-15 / +%15-20 / +%20-30 hasar'],
 ['Explosive<br/>Patlayıcı','Doğrudan hasatla saksı dışındaki komşu bitkilere hasar verme şansı. P3 ile açılır.','%15-25 / %30-40 / %45-60 / %70-90 tetikleme'],
 ['Duplicate<br/>Çifte Hasat','Hasat ödülünü iki kat sayma şansı. P6 ile açılır.','%15-25 / %30-40 / %45-60 / %70-90 tetikleme'],
 ['Tornado<br/>Hortum','Doğrudan hasattan sonra gezinen hortum çıkarma denemesi.','Mevcut bütün dosyalarda %100; aktif hortum sınırı geçerlidir.'],
 ['Energy<br/>Enerji','Kart havuzunda bulunur; mevcut bağlantısıyla hasara veya skora etkin katkı sağlamaz.','Görünen değerine dayanarak güç artışı bekleme.']],[69,256,174])
p('Yüzde aralıkları tek karta aittir. Patlayıcı, Çifte Hasat ve Hortum şansları saksıda toplanır ve %100 ile sınırlanır. Water ve Odak yüzdeleri normal bonus hesabında toplanır; Fertile süre çarpanları birbiriyle çarpılır.','small')

page('07','Rezonans: asıl güç sıçraması','Aynı saksının kapladığı hücrelerde aynı aileden yeterince bonus bulununca ek çarpan açılır. Hücrelerin bitişik olması veya özel bir desen oluşturması gerekmez; aynı saksıya ait olmaları yeterlidir.')
table(['Aile','2 hücre','3 hücre','4 ve üzeri'],[
 ['Odak','×2 doğrudan hasar','×4 doğrudan hasar','×8 doğrudan hasar'],['Water','×2 XP','×10 XP','×10 XP'],['Fertile','×0,75 üretim süresi','×0,5 üretim süresi','×0,5 üretim süresi']],[112,129,129,129])
p('Aynı ailede yalnızca ulaşılan en yüksek eşik çalışır. Üç Water, ×2 ile ×10’u birlikte vermez; ek rezonans ×10 olur. Farklı ailelerin rezonansları aynı saksıda birlikte çalışabilir. Kartların nadirliği farklı olsa da aynı aile sayımına katılır.')
sub('Örnek 1 / Üç Odak')
p('Oyuncu hasarın 100, aynı saksıda üç Odak bonusun +%10 olsun. Normal saksı çarpanı 1 + 0,1 + 0,1 + 0,1 = 1,3. Üçlü rezonansla 1,3 × 4 = 5,2. Böylece nominal doğrudan hasar <b>520</b> olur. Kritik ve rastgele vuruş farkı ayrıca uygulanır. Başka saksıda bu bonuslar yoksa oradaki temel vuruş hâlâ 100’dür.')
sub('Örnek 2 / Üç Water')
p('Her biri +%20 XP veren üç Water: (1 + 0,2 + 0,2 + 0,2) × 10 = <b>×16 XP</b>. Taban 20 XP’lik bitki 320 XP verir. Çifte Hasat da tutarsa 640 XP olur. Örnek diğer XP yatırımlarını içermez.')
sub('Örnek 3 / Üç Fertile')
p('Üç kart da üretim süresini %10 kısaltsın: 5 × 0,9 × 0,9 × 0,9 × 0,5 = <b>1,8225 saniye</b>. Bu süre yalnızca boşalan üretim noktasının beklemesidir; yaşayan bitkiyi kendiliğinden hasat etmez.')
note('Yerleşimin değeri','Altı hücreli bir 2×3 saksı, uygun bonuslar denk gelmişse üç Water + üç Fertile taşıyabilir. Böylece XP ve üretim rezonanslarını birlikte kullanır. Kart konumları rastgele olduğundan bu, garanti bir seçim dizisi değildir.')

page('08','Patlama, hortum ve çifte ödül')
sub('Patlayıcı: komşu saksılara uzanan hasat')
p('Bir bitkiyi doğrudan saldırıyla hasat ettiğinde, kendi saksısının patlama şansı denenir. Tutarsa saksının kapladığı alanın <b>dört yöndeki dış komşularında</b> bulunan bitkiler hasar alır. Köşegenler ve aynı saksının içindeki bitkiler bu patlamanın hedefi değildir. Aynı komşu hücre bir patlamada yalnızca bir kez vurulur.')
p('Patlama hasarı oyuncunun güncel Hasat Hasarına eşittir. Odak/saksı hasar çarpanı uygulanmaz; ayrıca kritik veya ±%15 vuruş farkı hesaplanmaz. Yakın saksılar arası yerleşim bu yüzden patlama için değerlidir.')
sub('Hortum: açık hücreler boyunca dolaşır')
p('Doğrudan hasatta hortum denemesi yapılır. Mevcut kart değeri %100 olsa da aynı anda <b>en fazla 4 hortum</b> bulunabilir. Hortum açık hücrelerde dört yönden rastgele birine ilerler; mümkünse hemen geldiği yere dönmez. En fazla 8 adım atar ve girdiği hücredeki bitkiye vurur.')
p('Her vuruş, çıkış anındaki oyuncu Hasat Hasarının yarısıdır; en az 1 hasar verir. Odak ve kritik uygulanmaz. Tur bitince aktif hortumlar temizlenir. Başlangıç hücresine anında vurmak yerine komşu hücreye hareketle başlar.')
sub('Çifte Hasat: bitki değil, ödül çoğalır')
p('Bitki hasat edildiğinde saksının Çifte Hasat şansı denenir. Başarılıysa kaynak miktarı, XP ve skor ikiye katlanır. Yeni bitki oluşturmaz. Patlama veya hortumla hasat edilen bitkinin ödülünde de çalışabilir.')
note('Zincirin sınırı','Patlama ve hortumla ölen bitkiler kaynak, XP ve skor verir; ancak yeni patlama veya hortum tetiklemez. Aynı doğrudan hasat hem patlama hem hortum başlatabilir. Çifte Hasat tetikleme sayısını ikiye katlamaz.')

page('09','Yetenek ağacı nasıl okunur?','Ağaçta 131 düğüm ve toplam 283 satın alma kademesi vardır. Yetenekleri XP ile değil, düğümün istediği kaynakla satın alırsın.')
table(['Kol','Oyuncuya sağladığı şey','Başka hangi sistemi besler?'],[
 ['H / Hasat','Düz hasar, yüzde hasar, bağımsız çarpanlar, kritik.','Bitkileri daha hızlı boşaltır; patlama ve hortumun taban hasarını da büyütür.'],
 ['Z / Zaman ve kontrol','Tur süresi, saldırı aralığı, alan, kart atlama ve kart şansı.','Tur başına fırsat sayısı ve kart seçimi kalitesi.'],
 ['P / Alan ve kilitler','Daha geniş grid, yeni saksılar ve özel kart havuzları.','Üretim kapasitesi, bonus alanı ve rezonans kombinasyonları.'],
 ['E / Ekonomi','Kaynak, üretim, XP ve nadir bitki yatırımları.','Sonraki alımlar, yeni kartlar ve kaynak çeşitliliği.']],[91,191,217])
sub('Bağlantı kuralı')
p('Normal bir rota dört düğümden oluşur: <b>A → B → C → D</b>. Kademe sayıları sırasıyla 3, 2, 2, 2’dir. Rotanın içindeki sonraki düğüm için öncekini bitirirsin. Başka bir rotaya geçiş çoğunlukla kaynak rotanın A düğümünü tamamlamayı ister; bütün rotayı bitirmek şart değildir. İki önkoşul yazıyorsa ikisi de gerekir.')
p('Bir düğümde sonraki kademe eski kademenin yerine geçer. Örneğin aynı düğüm önce +1, sonra +2 veriyorsa toplam +3 olmaz; düğümün güncel katkısı +2’dir. Yan yana çizilmiş iki düğüm, tek başına birbirini açmaz.')
note('Ağaç ve hücre bonusu farklı kapsamda','Ağaçtaki oyuncu bonusları genel saldırına; saksı hedefli ekonomi bonusları bütün saksılara uygulanır. Kart ve rezonans ise ilgili saksıyla sınırlıdır. Bu ayrım, genel yatırımın yerel bir rezonansla birlikte büyümesini sağlar.')

# Compact full route connectivity, sourced from the current manifest.
data=json.loads((ROOT/'Docs/FinalSkillTree.json').read_text(encoding='utf-8-sig'))['nodes']
byid={n['id']:n for n in data}
effect_names={0:'Hasat hasarı',1:'Alan yarıçapı',2:'Saldırı aralığı',3:'Kritik şansı',4:'Kritik çarpanı',5:'Üretim aralığı',6:'Nadirlik bonusu',7:'Altın',8:'Demir',9:'Taş',10:'XP',11:'Skor*',12:'Kart şansı',13:'Grid boyutu',14:'Tur süresi',19:'Atlama hakkı'}
def req(n):return ' + '.join(r['id']+' ('+str(r['level'])+'. kademe)' for r in n['prerequisites']) or 'Başlangıçtan açık'
def effect_route(nodes):
 vals={}
 for n in nodes:
  for e in n['tiers'][-1]['effects']:
   key=(e['statType'],e['operation']);v=e['value']
   vals[key]=(vals.get(key,1)*(1+v) if e['operation']==2 else vals.get(key,0)+v)
 out=[]
 for (s,op),v in vals.items():
  if op==2: val='×'+f'{v:.3g}'
  elif op==1:val='+'+f'{100*v:.4g}'+'%'
  elif s==3:val='+'+f'{100*v:.4g}'+' yüzde puan'
  elif s==14:val='+'+f'{v:g}'+' sn'
  else:val='+'+f'{v:.4g}'
  out.append(effect_names.get(s,str(s))+' '+val)
 return '; '.join(out)
for prefix,title,idx in [('H','Hasat rotalarının bağlantıları','10'),('Z','Zaman ve kontrol rotaları','11'),('E','Ekonomi rotalarının bağlantıları','12')]:
 page(idx,title)
 rows=[]
 for n in data:
  if n['id'].startswith(prefix) and (n['id'].endswith('-A') or '-' not in n['id']):
   base=n['id'].split('-')[0]; nodes=[x for x in data if x['id'].split('-')[0]==base]
   rows.append([base+' / '+escape(n['name'].removesuffix(' - 1')),req(n),effect_route(nodes)])
 table(['Rota','Başlama önkoşulu','Rota tamamen alınırsa'],rows,[160,160,179])
 p('Kodlar bu rehberde bağlantıları kısa göstermek içindir. Oyun içinde düğüm adını takip edebilirsin. A düğümünden sonraki B/C/D bağlantıları yetenek ağacı bölümündeki ortak kurala uyar. Etkiler yalnızca bu rotanın katkısını gösterir; önkoşul rotaların gücü bu sütuna dahil değildir.','small')
 if prefix=='H':p('H10 ve H11 tek düğümdür; sırasıyla ×8 ve ×9 bağımsız hasar çarpanı verir. Tam hasar yatırımları nominal taban hasarı 1.296.000 seviyesine çıkarabilir; kritik ve yerel Odak ayrıca uygulanır.','small')
 if prefix=='Z':p('Saldırı aralığının küçülmesi daha sık vuruş demektir. Oyuncu kontrolündeki fiili alt sınır 0,1 saniyedir. Süre rotaları toplam +60 saniye sağlar; tur süresi 90 saniyede sınırlanır.','small')
 if prefix=='E':note('Skor rotası için mevcut sürüm notu','E11 / Hasat Rekoru, veride saksı hedefli skor bonusu verir. Mevcut skor hesabı oyuncu hedefli bonusu okuduğundan bu yatırımın vaat ettiği artış hesaplamaya yansımaz. Diğer ekonomi rotalarıyla karıştırma.')

page('13','Genişleme ve açılma haritası')
table(['Düğüm','Önkoşul','Bedel ve sonuç'],[
 ['P1 / Grid Genişleme I','Başlangıçtan açık','1. kademe: 140 Altın → 5×5.<br/>2. kademe: 280 Demir → 7×7.'],
 ['P2 / 1×3 Saksı','P1, 1. kademe','100 Altın → 1×3 saksı satın alma kilidi.'],
 ['P3 / Patlayıcı Kartlar','P2 tamamlanmış','80 Demir → Explosive kartları havuza katılır.'],
 ['P5 / 2×2 Saksı','P2 + P1, 2. kademe','160 Demir → 2×2 saksı satın alma kilidi.'],
 ['P6 / Çifte Hasat Kartları','P5 tamamlanmış','240 Demir → Duplicate kartları havuza katılır.'],
 ['P7 / Grid Genişleme II','P1, 2. kademe','1. kademe: 600 Taş → 9×9.<br/>2. kademe: 1.950 Taş → 11×11.'],
 ['P8 / 2×3 Saksı','P5 + P7, 1. kademe','350 Taş → 2×3 saksı satın alma kilidi.']],[148,141,210])
p('Başlangıç alanı 3×3, yani 9 hücredir. Genişleme merkezden kare biçiminde açılır: 25, 49, 81 ve en son 121 hücre. Saksı kilidini satın almak ücretsiz saksı vermez; ardından mağazada ayrıca saksı bedelini ödersin.')
sub('Büyümenin iki yönlü etkisi')
p('Yeni alan daha fazla saksı ve yeni bonus hücreleri sağlar. Buna karşılık kartların rastgele düşebileceği hücre sayısı da artar. Dolayısıyla alan açmak, istediğin saksıya hemen yeni kart geleceğini garanti etmez. Kart sonrasında yerleşimi gözden geçirmek bu yüzden önemlidir.')
note('Somut bağlantı örneği','P1 ile 5×5 aç → P2 ile 1×3 erişimi kazan → mağazadan 1×3 al → aynı saksının altında üç Water birikirse XP ×10 rezonansı aç → daha çok seviye ve kart kazan. Her ok farklı bir sistemin diğerine katkısını gösterir.')

page('14','Hasar ve artan zorluk')
p('Oyuncunun güncel başlangıç değerleri: 1 hasat hasarı, 1 birim alan yarıçapı, 3 saniye saldırı aralığı, %30 kritik şansı ve ×2 kritik çarpanı. Her hedef için normal hasar ±%15 değişir, tam sayıya yuvarlanır ve kritik ayrı denenir. Dairenin içindeki uygun bitkiler aynı saldırıda vurulabilir.')
sub('Bitkiler turla güçlenir')
table(['Doğduğu tur','Common can','Legendary can'],[
 ['1','5','15'],['10','15','45'],['25','35','105'],['45','80','240'],['65','180','540'],['80','1.500','4.500'],['95','9.000','27.000'],['110','45.000','135.000'],['120','100.000','300.000'],['130','180.000','540.000']],[125,187,187])
p('Ara turlarda can geometrik olarak artar. Nadirlik çarpanları Common ×1, Uncommon ×1,2, Rare ×1,6, Epic ×2 ve Legendary ×3’tür. Can doğum anında belirlenir; yaşayan bitki sonraki turda kendiliğinden iyileşmez veya yeni turun canına yükselmez. Can eğrisi oyuncunun hasarına göre uyarlanmaz.','small')
note('Verim, iki beklemenin toplamıdır','Bir noktanın döngüsü kabaca “bitkiyi hasat etme süresi + yeniden üretim süresi”dir. Bitkiler uzun süre yaşıyorsa hasar, kritik, saldırı aralığı veya alan yatırımı değerlidir. Saksılar çoğunlukla boşsa üretim yatırımı daha doğrudan fayda sağlar.')

page('15','Sistemleri birlikte kullanmak')
table(['Gözlemin','Yapabileceğin yatırım','Neden işe yarar?'],[
 ['Bitkiler birikiyor.','Hasar + saldırı aralığı + uygun Odak saksısı.','Üretim noktalarını daha hızlı boşaltırsın.'],
 ['Hızla temizliyor, sonra bekliyorsun.','Fertile + üretim rotaları + yeni saksı.','Hasat edilecek bitki sayısı yükselir.'],
 ['Daha çok kart istiyorsun.','Water + XP rotaları + üçlü Water hedefi.','Aynı hasattan daha fazla XP ve seviye.'],
 ['Demir veya Taş üretimi zayıf.','Uygun saksı havuzu + Crystal + ilgili gelir rotası.','Önce kaynak üreten bitkiyi erişilebilir kılar, sonra gelirini büyütürsün.'],
 ['Bonuslar ayrı saksılara dağılmış.','Zemini incele; satış ve büyük saksı yerleşimini değerlendir.','Bonusları aynı saksıda toplayıp rezonans açabilirsin; yarım iade maliyetini hesaba kat.'],
 ['Komşu saksılarda çok bitki var.','Patlayıcı taşıyan saksıyı doğrudan hasat et.','Dış komşulara ek hasar ulaşabilir.'],
 ['Kart uygulanacak yer kalmadı.','Alan aç veya hakkın varsa kartı atla.','Yeni bonus yeri veya anlık kaynak kazanırsın.']],[137,186,176])
sub('Oyunun keyfi hangi kararlarda?')
p('<b>Görünür büyüme:</b> dar bir bahçeden çok saksılı düzene geçmek.<br/><b>Beklenmedik kombinasyon:</b> rastgele gelen kartın bir rezonans eşiğini tamamlaması.<br/><b>Ritim:</b> kısa hasat bölümlerinin ardından durup plan yapabilmek.<br/><b>Güç hissi:</b> hasar yazıları, kritik vuruşlar, rezonans açılışı ve dolaşan hortumlarla yatırımın karşılığını görmek.')
p('Rezonans bildirimi kart seçimleri bittikten sonra gösterilebilir. Bonus hesaplaması o anda zaten uygulanmıştır; gösterim tur süresinden tüketmez. Bu kısa sunum, hangi saksının yeni bir eşiğe ulaştığını fark etmeyi kolaylaştırır.','small')

page('16','Mevcut sürümü doğru okumak','Bu rehberdeki sayılar 19 Eylül 2026 tarihli yerel proje durumuna aittir. Oyun geliştikçe fiyatlar, denge ve bazı bağlantılar değişebilir.')
table(['Konu','Şu anki davranış'],[
 ['Başlangıç ekonomisi','Her kaynaktan 80.000; eski tasarım notlarındaki düşük başlangıç ekonomisiyle aynı deneyim değildir.'],
 ['Energy kartı','Adı skor çağrıştırıyor; verisi saksı hasar alanına yazıyor. Mevcut doğrudan saldırı ve skor hesabı bu değeri kullanmıyor.'],
 ['Hasat Rekoru','Saksı hedefli bonus ile oyuncu hedefli skor hesabı uyuşmuyor; beklenen skor artışı devreye girmiyor.'],
 ['Hortum nadirliği','Mevcut kart dosyalarının hepsi aynı %100 şansı verir. “Tornado-Rare” adlı dosyanın nadirlik alanı da Common kayıtlıdır.'],
 ['Patlama zinciri','Eski notlarda geçen zincirleme patlama güncel hasar kurallarında kapalıdır. Yalnızca doğrudan hasat davranış tetikler.'],
 ['Yenileme ve kalıcılık','Oyuncuya açık kart değeri yenileme, koşu dışı kalıcı gelişim veya çevrimdışı üretim bu rehberde mevcut özellik olarak sunulmaz.'],
 ['Denge','Yüksek başlangıç bakiyesi ve güçlü son hasar çarpanları nedeniyle öneriler kesin bir zorluk eğrisi veya en iyi rota iddiası taşımaz.']],[122,377])
sub('Rehberin dayandığı proje kaynakları')
p('Oyun akışı ve kontroller: GameScene, RoundManager, PlayerController, PlacementManager.<br/>Üretim ve ödüller: PlanterBrain, PlantSpawner, PlantResource, HarvestScoreManager.<br/>Kart ve bağlantılar: CardSelectionUI, ProgressionManager, DamageTypeRules, ResonanceRules.<br/>Sayısal veriler: Planters, Plants, GridModifiers, CoreStat, XP ve PlantHealthScaling dosyaları.<br/>Yetenek ağacı: FinalSkillTree verileri, SkillTreeManager ve GridUnlockManager. Eski tasarım notları güncel kodla çeliştiğinde kod esas alınmıştır.','small')
p('Terimler: koşu = 130 turluk oyun; tur = süreli hasat bölümü; hücre/tile = zemin karesi; kart = hücre bonusu seçimi; rezonans = aynı saksıdaki aile sayısından doğan ek çarpan.','small')

def footer(c,doc):
 w,h=A4;c.setStrokeColor(teal);c.setLineWidth(.5);c.line(48,43,w-48,43);c.setFillColor(ink);c.setFont('Arial',8);c.drawString(48,29,'CLICKERGAME  •  Oyuncu rehberi  •  19.09.2026');c.drawRightString(w-48,29,str(doc.page))
doc=SimpleDocTemplate(str(OUT),pagesize=A4,rightMargin=48,leftMargin=48,topMargin=43,bottomMargin=58,title='ClickerGame - Oyuncu Rehberi',author='ClickerGame',subject='Mevcut oyun sistemleri, oynanış ve sistem bağlantıları')
doc.build(story,onFirstPage=footer,onLaterPages=footer)
print(OUT)
