"""OFL derivatives for a multilingual heading preview. Original fonts stay untouched."""
from pathlib import Path
import sys,json,hashlib
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/.font-deps'))
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.varLib.instancer import instantiateVariableFont
OUT=ROOT/'Assets/Art/UI/ComicToon/Fonts'

def rename(font,family,postscript):
    names={1:family,2:'Regular',3:postscript+'-1.0',4:family,6:postscript,16:family,17:'Regular'}
    for record in font['name'].names:
        if record.nameID in names: record.string=names[record.nameID].encode(record.getEncoding())

font=TTFont(ROOT/'Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf')
glyf=font['glyf']; cmap=font.getBestCmap()
def add(name,glyph,advance):
    glyf[name]=glyph
    order=font.getGlyphOrder()
    if name not in order:font.setGlyphOrder(order+[name])
    font['hmtx'][name]=(advance,0)
def accent(name,points):
    p=TTGlyphPen(None);p.moveTo(points[0])
    for xy in points[1:]:p.lineTo(xy)
    p.closePath();add(name,p.glyph(),0)
accent('inkBreve',[(-120,65),(-80,-45),(45,-60),(125,45),(55,65),(15,15),(-30,20),(-55,80)])
accent('inkDot',[(-45,0),(42,8),(55,90),(-30,83)])
accent('inkCedilla',[(5,0),(-32,-60),(30,-65),(28,-100),(-55,-103),(-65,-150),(62,-145),(98,-105),(87,-30),(28,-25),(45,0)])
for code,base,mark,below in [(0x11e,'G','inkBreve',False),(0x11f,'g','inkBreve',False),(0x130,'I','inkDot',False),(0x15e,'S','inkCedilla',True),(0x15f,'s','inkCedilla',True)]:
    b=glyf[cmap[ord(base)]];b.recalcBounds(glyf);a=glyf[mark];a.recalcBounds(glyf)
    x=round((b.xMin+b.xMax-a.xMin-a.xMax)/2)
    y=b.yMin-4 if below else b.yMax+35-a.yMin
    p=TTGlyphPen(glyf);p.addComponent(cmap[ord(base)],(1,0,0,1,0,0));p.addComponent(mark,(1,0,0,1,x,y))
    name='ink'+str(code);add(name,p.glyph(),font['hmtx'][cmap[ord(base)]][0])
    for table in font['cmap'].tables:
        if table.isUnicode():table.cmap[code]=name
p=TTGlyphPen(None)
for points in [[(150,0),(150,680),(245,680),(245,115),(370,150),(465,290),(550,245),(455,70),(305,0)],[(55,370),(55,445),(370,555),(370,480)],[(55,235),(55,310),(370,420),(370,345)]]:
    p.moveTo(points[0])
    for point in points[1:]:p.lineTo(point)
    p.closePath()
add('inkLira',p.glyph(),600)
for table in font['cmap'].tables:
    if table.isUnicode():table.cmap[0x20ba]='inkLira'
rename(font,'Harvest Panel TR','HarvestPanelTR-Regular')
font.save(OUT/'HarvestPanelTR-Regular.ttf')

source=ROOT/'Tools/FontSources/NotoSansSC-Variable.ttf'
fallback=instantiateVariableFont(TTFont(source),{'wght':800},inplace=True)
rename(fallback,'Harvest CJK Support','HarvestCJKSupport-ExtraBold')
fallback.save(OUT/'HarvestCJKSupport-ExtraBold.ttf')
primary=TTFont(OUT/'HarvestPanelTR-Regular.ttf').getBestCmap()
secondary=TTFont(OUT/'HarvestCJKSupport-ExtraBold.ttf').getBestCmap()
checks={
 'tr':'ABCÇDEFGĞHIİJKLMNOÖPQRSŞTUÜVWXYZabcçdefgğhıijklmnoöpqrsştuüvwxyz ÖNİZLEME RUN’I BAŞLAT',
 'en':'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz START RUN PLANTER SHOP SKILL SHOP',
 'ru':'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя НАЧАТЬ ЗАБЕГ МАГАЗИН',
 'zh-Hans':'开始游戏种植商店技能商店预览准备收获金币铁石头暂停设置返回确认取消继续语言简体中文',
 'symbols':'0123456789 !?.,:;()[]{}+-=*/%&@#_$€£₺…—–‘’“”←↑→↓×÷'}
report={}
for lang,text in checks.items():
    missing=[f'U+{ord(c):04X}' for c in text if ord(c) not in primary and ord(c) not in secondary]
    assert not missing,(lang,missing)
    report[lang]={'tested':len(set(text)),'missing':missing}
report['primary_characters']=len(primary)
report['fallback_characters']=len(secondary)
report['noto_source_sha256']=hashlib.sha256(source.read_bytes()).hexdigest()
dest=ROOT/'Docs/UI-Button-Preview/coverage.json'
dest.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
