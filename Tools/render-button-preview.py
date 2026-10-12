"""Compose an offline SVG review with the actual font outlines, not generated lettering."""
from pathlib import Path
import sys,json,base64,re,math
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/.font-deps'))
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
font=TTFont(ROOT/'Assets/Art/UI/ComicToon/Fonts/HarvestPanelTR-Regular.ttf')
glyphs=font.getGlyphSet(); cmap=font.getBestCmap(); upem=font['head'].unitsPerEm
support=TTFont(ROOT/'Assets/Art/UI/ComicToon/Fonts/HarvestCJKSupport-ExtraBold.ttf')
support_cmap=support.getBestCmap();support_glyphs=support.getGlyphSet()
folder=ROOT/'Docs/UI-Button-Preview'
positions=[(.09,.044,.26,.14),(.368,.038,.282,.155),(.662,.045,.27,.14),(.805,None,.18,.12)]
def outlines(text,x,y,size,center=True):
    selected=[(font,glyphs,cmap) if ord(c) in cmap else (support,support_glyphs,support_cmap) for c in text]
    width=sum(f['hmtx'][m[ord(c)]][0]*size/f['head'].unitsPerEm for c,(f,g,m) in zip(text,selected))
    cursor=x-width/2 if center else x
    paths=[]
    for index,(char,(f,g,m)) in enumerate(zip(text,selected)):
        scale=size/f['head'].unitsPerEm
        pen=SVGPathPen(g);g[m[ord(char)]].draw(pen)
        seed=(ord(char)*17+index*31)%101
        cjk=0x3400<=ord(char)<=0x9fff
        angle=((seed%9)-4)*(.22 if cjk else .65)
        offset=((seed%5)-2)*size*(.007 if cjk else .018)
        height=1+((seed%7)-3)*(.004 if cjk else .012)
        paths.append(f'<path d="{pen.getCommands()}" transform="translate({cursor} {y+offset}) rotate({angle}) scale({scale} {-scale*height})"/>')
        cursor+=f['hmtx'][m[ord(char)]][0]*scale
    return ''.join(paths)
def replace_label(match):
    attrs,text=match.groups()
    size=float(re.search(r'font-size="([\d.]+)"',attrs)[1]);x=float(re.search(r'x="([\d.]+)"',attrs)[1]);y=float(re.search(r'y="([\d.]+)"',attrs)[1])
    # Stroke before scaling to font units; vector-effect keeps it at button scale.
    paths=outlines(text,x,y,size).replace('/>',' vector-effect="non-scaling-stroke"/>')
    return '<g fill="#fffdf2" stroke="#100b18" stroke-width="5" stroke-linejoin="round" paint-order="stroke fill">'+paths+'</g>'
image=base64.b64encode((folder/'reference.png').read_bytes()).decode()
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1288" height="1130" viewBox="0 0 1288 1130">',
 '<rect width="1288" height="1130" fill="#171020"/>',
 f'<image width="1288" height="726" href="data:image/png;base64,{image}"/>']
for control,pos in zip(json.loads((folder/'controls.json').read_text(encoding='utf-8')),positions):
    left,bottom,width,height=pos
    top=.167 if bottom is None else 1-bottom-height
    button=re.sub(r'<text([^>]*)>(.*?)</text>',replace_label,control['svg'])
    button=button.replace('<svg ',f'<svg x="{left*1288}" y="{top*726}" width="{width*1288}" height="{height*726}" ',1)
    svg.append(button)
svg.append('<g fill="#f6edce">'+outlines('HARVEST PANEL — COMIC LETTERING',30,778,32,False)+outlines('ÖNİZLEME · RUN’I BAŞLAT · İŞÇİ · IĞDIR · ÇĞİÖŞÜ',30,828,29,False)+outlines('START RUN · PLANTER SHOP · SKILL SHOP',30,875,29,False)+outlines('НАЧАТЬ ЗАБЕГ · МАГАЗИН · Ёё Йй Жж Яя',30,922,27,False)+outlines('开始游戏 · 种植商店 · 技能商店 · 预览',30,975,30,False)+outlines('0123456789 · ₺ € $ % → ← · çğışöü',30,1026,28,False)+'</g>')
svg.append('<text x="30" y="1090" fill="#b9adc7" font-family="sans-serif" font-size="18">Harf bazında sabit varyasyon · Basitleştirilmiş Çince · Onay taslağı</text></svg>')
(folder/'preview.svg').write_text(''.join(svg),encoding='utf-8')
print('PASS: multilingual preview uses actual font outlines; all sample glyphs resolved.')
