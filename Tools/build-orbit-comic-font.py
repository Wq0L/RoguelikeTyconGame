"""Build the review-only Orbit Ink TR face from the project's OFL comic source.
Install fonttools into Tools/.font-deps. Does not replace existing Unity fonts.
"""
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools/.font-deps'))
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.recordingPen import DecomposingRecordingPen

folder = ROOT / 'Assets/Art/UI/ComicToon/Fonts'
font = TTFont(folder / 'HarvestComicTR-Regular.ttf')
cmap = font.getBestCmap()
glyphset = font.getGlyphSet()
new_glyphs = {}
metrics = {}
# Change complete glyphs, including their accents, as one piece. No per-frame randomness.
for index, name in enumerate(font.getGlyphOrder()):
    advance, bearing = font['hmtx'][name]
    angle = ((index * 17) % 7 - 3) * .007
    width = .96 + ((index * 11) % 5 - 2) * .007
    pen = TTGlyphPen(None)
    drawing = DecomposingRecordingPen(glyphset)
    glyphset[name].draw(drawing)
    drawing.replay(TransformPen(pen, (width, 0, angle, 1, 8, 0)))
    new_glyphs[name] = pen.glyph()
    metrics[name] = (round(advance * width) + 12, round(bearing * width) + 8)
for name, glyph in new_glyphs.items():
    font['glyf'][name] = glyph
    font['hmtx'][name] = metrics[name]

def symbol(code, name, polygons, advance=650):
    if code in cmap:
        return
    pen=TTGlyphPen(None)
    for points in polygons:
        pen.moveTo(points[0])
        for point in points[1:]: pen.lineTo(point)
        pen.closePath()
    font['glyf'][name]=pen.glyph()
    order=font.getGlyphOrder()
    if name not in order: order=order+[name]
    font.setGlyphOrder(order)
    font['hmtx'][name]=(advance,30)
    for table in font['cmap'].tables:
        if table.isUnicode(): table.cmap[code]=name
    cmap[code]=name

# UI symbols absent from the original, authored as solid chunky strokes.
symbol(0x20BA,'orbitLira',[
    [(175,0),(175,720),(285,720),(285,125),(390,150),(470,225),(515,340),(615,300),(565,160),(465,55),(320,0)],
    [(65,400),(65,480),(425,600),(425,520)],
    [(65,255),(65,335),(425,455),(425,375)]])
arrow=[(35,305),(380,305),(270,195),(350,115),(610,365),(350,615),(270,535),(380,425),(35,425)]
symbol(0x2192,'orbitRight',[arrow])
symbol(0x2190,'orbitLeft',[[(650-x,y) for x,y in reversed(arrow)]])
symbol(0x2191,'orbitUp',[[(y,650-x) for x,y in arrow]])
symbol(0x2193,'orbitDown',[[(650-y,x) for x,y in arrow]])

names={1:'Orbit Ink TR',2:'Regular',3:'OrbitInkTR-Regular-1.0',4:'Orbit Ink TR Regular',6:'OrbitInkTR-Regular',16:'Orbit Ink TR',17:'Regular'}
for record in font['name'].names:
    if record.nameID in names: record.string=names[record.nameID].encode(record.getEncoding())
font['head'].fontRevision=1.0
out=folder/'OrbitInkTR-Regular.ttf'
font.save(out)
check=TTFont(out)
required='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÇçĞğİıÖöŞşÜüÂâÎîÛû0123456789 !?.,:;\'"()[]{}+-=*/%&@#_$€£₺…—–‘’“”←↑→↓×÷'
missing=[f'U+{ord(c):04X}' for c in required if ord(c) not in check.getBestCmap()]
assert not missing, missing
for glyph in check['glyf'].glyphs.values():
    glyph.expand(check['glyf'])
assert check['name'].getDebugName(1)=='Orbit Ink TR'
print(f'PASS: {len(check.getBestCmap())} mapped characters; Turkish/English, digits, punctuation, currencies and arrows.')
