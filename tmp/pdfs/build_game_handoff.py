from pathlib import Path
import re, ast, struct, hashlib, json, subprocess, textwrap
from xml.sax.saxutils import escape
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle, KeepTogether
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.lib.pagesizes import A4
from reportlab.graphics.shapes import Drawing, Rect, String, Line, Polygon

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/pdf'
OUT.mkdir(parents=True,exist_ok=True)
FONT=Path('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/share/fonts/DejaVuSans.ttf')
pdfmetrics.registerFont(TTFont('DVS',str(FONT)))
pdfmetrics.registerFontFamily('DVS',normal='DVS',bold='DVS',italic='DVS',boldItalic='DVS')

def font_notices():
 b=FONT.read_bytes(); n=struct.unpack_from('>H',b,4)[0]
 off=next(struct.unpack_from('>I',b,12+i*16+8)[0] for i in range(n) if b[12+i*16:16+i*16]==b'name')
 _,count,s=struct.unpack_from('>HHH',b,off); vals=[]
 for i in range(count):
  p,e,l,k,z,o=struct.unpack_from('>HHHHHH',b,off+6+i*12)
  if k in (0,13,14) and p==3:
   t=b[off+s+o:off+s+o+z].decode('utf-16-be')
   if t not in vals: vals.append(t)
 return '\n\n'.join(vals)

notice=font_notices()
(OUT/'DejaVuSans-LICENSE.txt').write_text(notice,encoding='utf-8')

def scalar(v):
 v=v.strip()
 if v.startswith('"'):
  try:return ast.literal_eval(v)
  except:return v.strip('"')
 return v

def field(txt,key,default=''):
 m=re.search(r'^  '+re.escape(key)+r':[ \t]*(.*)$',txt,re.M)
 return scalar(m.group(1)) if m else default

guidmap={}
for p in (ROOT/'Assets').rglob('*.meta'):
 try:
  m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
  if m:guidmap[m.group(1)]=Path(str(p)[:-5])
 except (OSError,UnicodeError):pass

def deref(txt,key):
 v=field(txt,key); m=re.search(r'guid: (\w+)',v)
 return guidmap.get(m.group(1)) if m else None

pool=ROOT/'Assets/ScriptableObjects/BossRewards/GecOyunV1/BossRewardPool_GecOyunV1.asset'
pt=pool.read_text(encoding='utf-8-sig')
reward_entries=[]; stage=''
for line in pt.splitlines():
 m=re.match(r'  - id: (\w+)',line)
 if m:stage={'erken':'Erken','orta':'Orta','guclu':'Güçlü'}.get(m[1],m[1])
 m=re.search(r'    - reward: .*guid: (\w+)',line)
 if m:
  p=guidmap[m[1]]; t=p.read_text(encoding='utf-8-sig')
  reward_entries.append((stage,p,t))
assert len(reward_entries)==32,len(reward_entries)

enumpath=ROOT/'Assets/Scripts/Enums/StatType.cs'
enums=re.findall(r'^\s*(\w+)\s*(?:,|$)',enumpath.read_text(encoding='utf-8-sig'),re.M)
enums=[x for x in enums if x not in ('public','StatType')]
reward_details=[]
for stage,p,t in reward_entries:
 effect=deref(t,'effect'); req=deref(t,'requires')
 entry={'stage':stage,'name':field(t,'displayName'),'id':field(t,'id'),'asset':p.relative_to(ROOT).as_posix(),
        'maxStacks':field(t,'maxStacks'),'weight':field(t,'weight'),'exclusiveGroup':field(t,'exclusiveGroup'),
        'note':field(t,'note'),'effectAsset':effect.relative_to(ROOT).as_posix() if effect else None,
        'requires':field(req.read_text(encoding='utf-8-sig'),'displayName') if req else None}
 entry['serializedFields']={key:scalar(val) for key,val in re.findall(r'^  (\w+):[ \t]*(.*)$',t,re.M) if not key.startswith('m_')}
 entry['modifiersPerStack']=[{'stat':enums[int(s)],'target':['All','Player','Planter','Round','Grid','Mutation'][int(tar)],'operation':['Flat','AddPercent','MorePercent','Set'][int(op)],'value':float(v)} for s,tar,op,v in re.findall(r'- statType: (\d+)\s+target: (\d+)\s+operation: (\d+)\s+value: ([\d.eE+-]+)',t)]
 reward_details.append(entry)

base=(ROOT/'tmp/pdfs/game_handoff_20261010.md').read_text(encoding='utf-8')
appendix=[]
for idx,start in enumerate(range(0,32,6)):
 parts=[f'# EK A{idx+1} / Bağlı boss ödülleri',
 'Bu envanter GecOyunV1 havuzunun GUID bağlantılarından otomatik okunmuştur. Stack, ağırlık, ön koşul ve grup bilgileri anlık asset değerleridir. Ağırlık kesin çıkma yüzdesi değildir. Kart notları ham asset metnidir; eski round ifadeleri için 13. bölümdeki aşama kuralı esas alınır.']
 for entry in reward_details[start:start+6]:
  parts.append(f"## {entry['name']} / {entry['stage']}")
  detail=f"Kimlik: {entry['id']}. En çok stack: {entry['maxStacks']}. Temel ağırlık: {entry['weight']}."
  if entry['requires']:detail+=f" Ön koşul: {entry['requires']}."
  if entry['exclusiveGroup']:detail+=f" Dışlama grubu: {entry['exclusiveGroup']}."
  parts.append(detail)
  if entry['note']:parts.append(str(entry['note']).replace('\n',' '))
  parts.append('Asset: '+entry['asset'])
 appendix.append('\n\n'.join(parts))

effect_rows=[]
for stage,p,t in reward_entries:
 effect=deref(t,'effect')
 if not effect:continue
 et=effect.read_text(encoding='utf-8-sig')
 lines=[]
 for key,val in re.findall(r'^  (\w+): (.*)$',et,re.M):
  if key.startswith('m_') or key in ('id','displayName'):continue
  lines.append(f'{key}={scalar(val)}')
 effect_rows.append((field(t,'displayName'),'; '.join(lines)))
for i in range(0,len(effect_rows),7):
 parts=['# EK B / Yeni ödüllerin veri parametreleri',
 'İsimler C# / asset alan adlarıdır; bunlar ham ayarlardır. Kodun profil kapıları ve güncel Hilal fare rotası bu alanlara ek anlam verir. Bir alanın dosyada durması hâlâ kullanıldığı anlamına gelmeyebilir.']
 for name,data in effect_rows[i:i+7]:parts.extend(['## '+name,data])
 appendix.append('\n\n'.join(parts))

legacy_rows=[]
for entry in reward_details:
 if entry['effectAsset']:continue
 desc=[]
 for mod in entry['modifiersPerStack']:
  desc.append(f"{mod['stat']} / {mod['target']}: {mod['operation']} {mod['value']:g}")
 fields=entry['serializedFields']
 for key in ('directDamageMultiplier','rareDirectMultiplier','behaviorDamageMultiplier'):
  if fields.get(key,'1')!='1':desc.append(key+'='+fields[key])
 if fields.get('levelChoiceDelta','0')!='0':desc.append('levelChoiceDelta='+fields['levelChoiceDelta'])
 if fields.get('echo','0')!='0':
  desc.extend(k+'='+fields.get(k,'0') for k in ('echo','echoDelay','echoDamage','echoRadius','echoReach'))
 if fields.get('rhythmHarvests','0')!='0':
  desc.extend(k+'='+fields[k] for k in ('rhythmHarvests','rhythmDamage','rhythmRadius'))
 if fields.get('chain','0')!='0':desc.append('2 ek nesil; şans/hasar 0.75, 0.5; bütçe 32; kare başı 8')
 legacy_rows.append((entry['name'],'; '.join(desc)))
for i in range(0,len(legacy_rows),10):
 parts=['# EK B / Temel ödüllerin sayısal etkileri','Her satır tek stack değerini gösterir. MorePercent 0.25, ilgili gruba ×1,25 anlamına gelir; Flat puan ekler. Buradaki teknik alan adları, ana bölümdeki sahiplik kurallarıyla okunmalıdır.','| Ödül | Bağlı asset etkisi |','|---|---|']
 parts.extend('| '+name+' | '+desc+' |' for name,desc in legacy_rows[i:i+10])
 appendix.append('\n\n'.join(parts[:2])+'\n\n'+'\n'.join(parts[2:]))

sourcefiles=[
 'AGENTS.md','ProjectSettings/ProjectVersion.txt','Packages/manifest.json',
 'Assets/Resources/RunProfileSelection.asset',
 'Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset',
 'Assets/ScriptableObjects/Balance/AlphaDengeV1/RunBalance_AlphaDengeV1.asset',
 'Assets/ScriptableObjects/FieldEvents/GecOyunV1/FieldEventPlan_GecOyunV1.asset',
 'Assets/Scripts/Managers/StatManager.cs','Assets/Scripts/Stats/StatCalculator.cs',
 'Assets/Scripts/Stats/BasePowerCardRules.cs','Assets/Scripts/Stats/NumericSafety.cs',
 'Assets/Scripts/Managers/ProgressionManager.cs','Assets/Scripts/Managers/HarvestChain.cs',
 'Assets/Scripts/Managers/PlacementManager.cs','Assets/Scripts/Plants/GroundCell.cs',
 'Assets/Scripts/Managers/RoundManager.cs','Assets/Scripts/Managers/GameSettings.cs',
 'Assets/Scripts/ScriptableObjects/TileModifierSO.cs',
 'Assets/Scripts/Managers/BossRewardManager.cs','Assets/Scripts/Managers/RunCalendar.cs',
 'Assets/Scripts/UI/CardUI/CardSelectionUI.cs','Assets/Scripts/Player/PlayerController.cs',
 'Docs/FullGame-PlaytestFix-20261006.md','Docs/Bolum3-8-GecOyunOlaylariVeBossOdulleri.md',
 'Docs/Bolum3-8-IcerikBaglama.md','Docs/CrescentFamily/CursorRoute.md',
 'Docs/CrescentFamily/README.md','Docs/WanderingBounty/README.md','Docs/ThornSiege/README.md',
 'Docs/AntennaFamily/README.md','Docs/SimpleResonance.md','Docs/FinalSkillTree.md',
 'Docs/DemoScene.md','Docs/UI-Art-Direction.md',
 'Docs/Bolum3-3-BaslangicSecimiVeGorevler.md']
for p in (ROOT/'Assets/Scripts/RunEffects').rglob('*.cs'):sourcefiles.append(p.relative_to(ROOT).as_posix())
for p in (ROOT/'Assets/Scripts/FieldEvents').rglob('*.cs'):sourcefiles.append(p.relative_to(ROOT).as_posix())
sourcefiles.append(pool.relative_to(ROOT).as_posix())
for stage,p,t in reward_entries:
 sourcefiles.append(p.relative_to(ROOT).as_posix())
 effect=deref(t,'effect')
 if effect:sourcefiles.append(effect.relative_to(ROOT).as_posix())
sourcefiles=sorted(set(x for x in sourcefiles if (ROOT/x).is_file()))
manifest={'snapshotDate':'2026-10-10','scope':'GameScene / Run50_GecOyunV1','humanPlayThisTask':False,'testsRunThisTask':False,
 'gitHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
 'warning':'Working tree contains uncommitted work; HEAD alone is not this snapshot.',
 'files':[{'path':x,'sha256':hashlib.sha256((ROOT/x).read_bytes()).hexdigest()} for x in sourcefiles]}
(OUT/'Kaynak-Anlik-Goruntu-20261010.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'Boss-Odul-Envanteri-20261010.json').write_text(json.dumps(reward_details,ensure_ascii=False,indent=2),encoding='utf-8')

appendix.append('''# EK C / Tile aileleri, can ve seçenekler

| Tile türü | Ana rol |
|---|---|
| Fertile / Bereketli | Üretim aralığını azaltma; taban sonrası nadirlik dönüşümü. |
| Water / Su | XP yönü. |
| Crystal / Kristal | Bitki nadirliği. |
| Energy / Enerji | Harvest Score yönü. |
| Damage / Hasar | Saksıdaki bitkilere direct hasar. |
| Duplicate | Hasat kaynaklarını çoğaltma şansı. |
| Explosive / Patlama | Normal patlama davranışı şansı. |
| Tornado / Kasırga | Hareketli kasırga davranışı şansı. |
| Boomerang / Bumerang | Gidiş-dönüş davranışı şansı. |
| Electric / Elektrik | Çapraz elektrik davranışı şansı. |

Tile yükseltmesi her seviyede başlangıç zar değerinin %25'i kadar ilave etki verir; Sv 3 başlangıç etkisinin 1,75 katıdır. Bu işlem üç kez ×1,25 çarpma değildir. Kartın gösterdiği zar sonucu uygulamaya aktarılır; yerleşimde bağımsız ikinci zar atıp görünen etkiyi değiştirmemelidir.

Aktif can eğrisinin Common referans noktaları: R1=20, R5=25, R10=31, R15=44, R20=67, R25=99, R30=146, R35=212, R40=306, R45=440, R50=627. Rarity çarpanları 1 / 1,5 / 2 / 2,5 / 3. Asıl bitki sonucu plant verisi ve ortak can hesaplayıcısından gelir; bu referans tabloyu her prefab'ın mutlak canı diye kopyalama.

Güncel süre tablolu profilde 65 saniye, eski 60 saniye tavanına takılmaz ve ek tempo hızına çevrilmez. Süre stat düğümleri bu profilin sabit tablosunu uzatmaz. Eski profillerdeki süre/tempo kuralları ayrıca korunabilir.

Oyuncu ayarları çözünürlük, pencere modu, VSync, FPS sınırı, kalite, arka plan efektleri; ana/müzik/efekt sesleri ve odak dışı sessizlik; sarsıntı, kamera hareketi, hasar sayıları, koordinatlar, nadirlik auraları, round sonu yavaşlama ve ekran parlamalarını içerir. Ayarlar PlayerPrefs'te saklanır; meta açılım kaydıyla aynı dosya değildir.

Elektriğin zamana bağlı şarj deneyi kaynakta kalmış olabilir ama emekli edilmiştir. Güncel profil KillChance yolunu kullanır; ElectricChargeManager dosyasının varlığından aktif şarj mekaniği çıkarma.
''')

appendix.append('''# EK C / Kaynağa nasıl geri dönülür?

Bu PDF'de dosya adları okunabilirlik için çoğunlukla kısa yazılmıştır. Tam proje göreli yollar ve SHA-256 değerleri Kaynak-Anlik-Goruntu-20261010.json dosyasındadır. Aynı klasördeki Boss-Odul-Envanteri-20261010.json, 32 ödülün okunabilir makine envanteridir.

## Öncelik sırası

1. Çalışan profilin bağlı asset'i ve güncel C# uygulaması.
2. Güncel aile/revizyon belgesi: özellikle CrescentFamily/CursorRoute.md ve WanderingBounty/README.md.
3. Tarihli bölüm teslimi ve test çıktısı.
4. Eski tasarım belgesi ve sohbet fikri.

Eski belgeyi kaynak listesinden silmek gerekmez; geçerlilik alanı belirtilmelidir. Örneğin CrescentFamily/README.md, 9 Ekim tam satır davranışını tarihsel olarak saklarken üstte 10 Ekim fare rotasına yönlendirir. FinalSkillTree'nin eski maliyetleri profil override'ı okunmadan güncel fiyat diye sunulamaz.

## Değişiklik yapmadan önce cevaplanacak sorular

- İlgili kural GameScene'e mi, DemoScene'e mi, bütün profillere mi ait?
- Bu olay isabet mi, hasat mı, direct mi, davranış mı, ödül saldırısı mı?
- Hangi saksının statı hesaplanıyor; efektin kendi hasarı tekrar çarpılıyor mu?
- Sonraki frame/round/run'a kalan hangi kuyruk, referans ve görsel var?
- Test gerçek oyunu mu oynuyor, yoksa skor/XP'yi elle vererek akışı mı doğruluyor?
- Aynı seed karşılaştırmasında RNG tüketimi değişti mi?

## Belge paketi

PDF okunabilir ana belgedir. Aynı adlı Markdown, AI araçları için metin sürümüdür. JSON dosyaları kaynak/ödül kimliklerini taşır. Bu dosyalar yeni oyun sistemi oluşturmaz; belge üretimi sırasında oyun kodu değiştirilmemiştir.

Font: yerel araç paketindeki DejaVu Sans. Türkçe gömülü yazı tipi olarak kullanıldı; oyun fontu veya bağımlılığı eklenmedi. Bitstream Vera / Arev izin metni aşağıdadır ve ayrıca DejaVuSans-LICENSE.txt olarak sağlanır. Ücretsiz ticari kullanım; font tek başına satılamaz; telif/izin bildirimi korunur. Oyun içi/jenerik kredi şartı yoktur.
''')

licenseparas=re.split(r'\n\s*\n',notice)
appendix.append('# EK D / Gömülü font lisans bildirimi\n\n'+ '\n\n'.join(p.replace('\n',' ') for p in licenseparas))
full=base+'\n\n---PAGE---\n\n'+'\n\n---PAGE---\n\n'.join(appendix)
full=full.replace('—','-').replace('–','-')
mdout=OUT/'Oyun-Sistem-Rehberi-20261010.md'
mdout.write_text(full,encoding='utf-8')

INK=colors.HexColor('#20283A'); TEAL=colors.HexColor('#147B80'); GOLD=colors.HexColor('#F5BD58'); MUTED=colors.HexColor('#526477')
styles={
 'body':ParagraphStyle('body',fontName='DVS',fontSize=9.25,leading=13.8,textColor=INK,spaceAfter=8),
 'h1':ParagraphStyle('h1',fontName='DVS',fontSize=21,leading=26,textColor=INK,spaceAfter=17),
 'h2':ParagraphStyle('h2',fontName='DVS',fontSize=12,leading=16,textColor=TEAL,spaceBefore=6,spaceAfter=7,keepWithNext=True),
 'small':ParagraphStyle('small',fontName='DVS',fontSize=7.3,leading=10.5,textColor=MUTED,spaceAfter=7,wordWrap='CJK'),
 'cell':ParagraphStyle('cell',fontName='DVS',fontSize=8.15,leading=11.6,textColor=INK,spaceAfter=0),
 'headcell':ParagraphStyle('headcell',fontName='DVS',fontSize=8.2,leading=11.6,textColor=colors.white,spaceAfter=0),
 'license':ParagraphStyle('license',fontName='DVS',fontSize=7.4,leading=10,textColor=INK,spaceAfter=5),
}

def rich(s):
 s=escape(s)
 s=re.sub(r'\*\*(.+?)\*\*',r'<font color="#147B80">\1</font>',s)
 return s

def table(rows):
 n=len(rows[0]); widths={2:[135,356],3:[113,170,208],4:[111,90,145,145]}.get(n,[491/n]*n)
 data=[[Paragraph(rich(v),styles['headcell' if i==0 else 'cell']) for v in row] for i,row in enumerate(rows)]
 t=Table(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
 t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),TEAL),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.HexColor('#EFF5F5'),colors.HexColor('#F8FAFA')]),('VALIGN',(0,0),(-1,-1),'TOP'),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('LINEBELOW',(0,0),(-1,0),1,TEAL)]))
 return t

def loop_diagram():
 d=Drawing(491,88)
 labels=[('ÜRETİM','Bitki / can'),('HASAT','Fare + davranış'),('KAZANÇ','Kaynak / XP / skor'),('KURULUM','Kart / saksı / ödül')]
 for i,(a,b) in enumerate(labels):
  x=i*124
  d.add(Rect(x,23,116,55,rx=7,ry=7,fillColor=TEAL if i<3 else INK,strokeColor=None))
  d.add(String(x+58,57,a,fontName='DVS',fontSize=9,fillColor=colors.white,textAnchor='middle'))
  d.add(String(x+58,39,b,fontName='DVS',fontSize=7.5,fillColor=colors.white,textAnchor='middle'))
  if i<3:d.add(Line(x+117,50,x+122,50,strokeColor=GOLD,strokeWidth=2))
 d.add(Line(429,14,57,14,strokeColor=TEAL,strokeWidth=1.5))
 d.add(String(246,1,'Yerleşim ve seçimler bir sonraki hasadın biçimini değiştirir.',fontName='DVS',fontSize=7.5,fillColor=MUTED,textAnchor='middle'))
 return d

class BookDoc(SimpleDocTemplate):
 def beforeDocument(self):
  self.sections=[]
 def afterFlowable(self,f):
  if isinstance(f,Paragraph) and f.style.name=='h1':
   key='section'+str(len(self.sections));title=f.getPlainText()
   self.canv.bookmarkPage(key)
   self.canv.addOutlineEntry(title,key,0,False)
   self.sections.append({'title':title,'page':self.page})
   if title not in ('OYUNUN GÜNCEL SİSTEM REHBERİ','İçindekiler'):
    self.notify('TOCEntry',(0,title,self.page,key))

def footer(c,doc):
 w,h=A4
 c.setFillColor(TEAL);c.rect(0,h-10,w,10,stroke=0,fill=1)
 c.setFont('DVS',7);c.setFillColor(MUTED)
 c.drawString(52,h-34,'CLICKERGAME  /  SİSTEM REFERANSI  /  10.10.2026')
 c.setStrokeColor(colors.HexColor('#D7E0E4'));c.line(52,42,w-52,42)
 c.drawString(52,28,'GameScene • Run50_GecOyunV1 • Kod / veri anlık görüntüsü')
 c.drawRightString(w-52,28,str(doc.page))

pdfout=OUT/'Oyun-Sistem-Rehberi-20261010.pdf'
doc=BookDoc(str(pdfout),pagesize=A4,rightMargin=52,leftMargin=52,topMargin=57,bottomMargin=57,
 title='ClickerGame - Güncel Oyun Sistem Rehberi',author='Proje belgeleri ve kodundan hazırlanan AI devir rehberi',subject='GameScene / Run50_GecOyunV1 - 10 Ekim 2026',pageCompression=1)
doc.sections=[]
story=[]
chapters=full.split('---PAGE---')
for ci,chapter in enumerate(chapters):
 if ci:story.append(PageBreak())
 if ci==1:
  story.append(Paragraph('İçindekiler',styles['h1']))
  toc=TableOfContents()
  toc.levelStyles=[ParagraphStyle('toc',fontName='DVS',fontSize=8.4,leading=11.2,textColor=INK,spaceBefore=2.8,leftIndent=0,firstLineIndent=0)]
  toc.tableStyle=TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('TOPPADDING',(0,0),(-1,-1),0),('BOTTOMPADDING',(0,0),(-1,-1),0),('LEFTPADDING',(0,0),(-1,-1),0),('RIGHTPADDING',(0,0),(-1,-1),0)])
  story.extend([toc,PageBreak()])
 lines=chapter.strip().splitlines();i=0;license_section=chapter.strip().startswith('# EK D')
 while i<len(lines):
  line=lines[i].strip()
  if not line:i+=1;continue
  if line.startswith('|'):
   rows=[]
   while i<len(lines) and lines[i].strip().startswith('|'):
    cells=[v.strip() for v in lines[i].strip().strip('|').split('|')]
    if not all(re.fullmatch(r'[-: ]+',v) for v in cells):rows.append(cells)
    i+=1
   story.extend([table(rows),Spacer(1,12)]);continue
  if line.startswith('# '):
   story.append(Paragraph(rich(line[2:]),styles['h1']));i+=1;continue
  if line.startswith('## '):
   story.append(Paragraph(rich(line[3:]),styles['h2']));i+=1;continue
  paragraph=[line];i+=1
  if not re.match(r'^(- |\d+\. )',line):
   while i<len(lines) and lines[i].strip() and not lines[i].startswith(('#','|')):
    paragraph.append(lines[i].strip());i+=1
  text=' '.join(paragraph)
  style=styles['license' if license_section else 'small' if text.startswith(('Kaynak:','Asset:')) else 'body']
  story.append(Paragraph(rich(text),style))
 if ci==0:story.extend([Spacer(1,14),loop_diagram()])
doc.multiBuild(story,onFirstPage=footer,onLaterPages=footer)
(OUT/'Belge-Bolum-Dizini.json').write_text(json.dumps(doc.sections,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'pdf':str(pdfout),'chapters':len(chapters),'section_pages':doc.sections,'source_count':len(sourcefiles)},ensure_ascii=False,indent=2))
