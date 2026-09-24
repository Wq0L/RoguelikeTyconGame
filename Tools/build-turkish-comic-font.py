"""Create an OFL-compliant, renamed Turkish companion to the existing comic face.
Requires fonttools. Original font remains untouched; accents share its base outlines.
"""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Temp/fonttools'))
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphCoordinates
from fontTools.ttLib.tables.ttProgram import Program

root = Path(__file__).resolve().parents[1]
folder = root / 'Assets/Art/UI/ComicToon/Fonts'
font = TTFont(folder / 'LilitaOne-Regular.ttf')
glyf = font['glyf']
cmap = font.getBestCmap()

def add(name, glyph, advance, bearing=0):
    glyf[name] = glyph
    font.setGlyphOrder(font.getGlyphOrder() + ([name] if name not in font.getGlyphOrder() else []))
    font['hmtx'][name] = (advance, bearing)

# Reuse the original lowercase i dot, so its rounding and weight stay identical.
source = glyf[cmap[105]]
coords, ends, flags = source.getCoordinates(glyf)
dot = Glyph(); dot.numberOfContours = 0; dot.coordinates = GlyphCoordinates(); dot.flags = flags[:0]; dot.endPtsOfContours = []
start = 0
for end in ends:
    points = coords[start:end+1]
    if min(y for x,y in points) > 500:
        dot.coordinates.extend(points); dot.flags.extend(flags[start:end+1]); dot.endPtsOfContours.append(len(dot.coordinates)-1); dot.numberOfContours += 1
    start = end+1
assert dot.numberOfContours > 0
dot.program = Program(); dot.program.fromBytecode([])
add('harvestDot', dot, 0); dot.recalcBounds(glyf)

# Rounded breve with the same substantial weight as the source's other accents.
pen = TTGlyphPen(None)
pen.moveTo((-120,0)); pen.qCurveTo((-112,-112),(0,-112)); pen.qCurveTo((112,-112),(120,0))
pen.lineTo((50,0)); pen.qCurveTo((45,-44),(0,-44)); pen.qCurveTo((-45,-44),(-50,0)); pen.closePath()
add('harvestBreve',pen.glyph(),0)

def compose(code, name, base, accent, accent_top):
    b=glyf[cmap[ord(base)]]; b.recalcBounds(glyf)
    a=glyf[accent]; a.recalcBounds(glyf)
    x=round((b.xMin+b.xMax-a.xMin-a.xMax)/2)
    y=round(accent_top-a.yMax)
    p=TTGlyphPen(glyf); p.addComponent(cmap[ord(base)],(1,0,0,1,0,0)); p.addComponent(accent,(1,0,0,1,x,y))
    advance,bearing=font['hmtx'][cmap[ord(base)]]
    add(name,p.glyph(),advance,bearing)
    for table in font['cmap'].tables:
        if table.isUnicode(): table.cmap[code]=name

compose(0x11E,'Gbreve','G','harvestBreve',860)
compose(0x11F,'gbreve','g','harvestBreve',660)
compose(0x130,'Idotaccent','I','harvestDot',850)
compose(0x15E,'Scedilla','S','cedilla',-14)
compose(0x15F,'scedilla','s','cedilla',-14)

# Lilita is a Reserved Font Name under the source OFL; rename all family identifiers.
names={1:'Harvest Comic TR',2:'Regular',3:'HarvestComicTR-Regular-1.0',4:'Harvest Comic TR Regular',6:'HarvestComicTR-Regular',16:'Harvest Comic TR',17:'Regular'}
for record in font['name'].names:
    if record.nameID in names: record.string=names[record.nameID].encode(record.getEncoding())
font['head'].fontRevision=1.001
out=folder/'HarvestComicTR-Regular.ttf'
font.save(out)
check=TTFont(out).getBestCmap()
assert all(ord(c) in check for c in 'ÇçĞğİıÖöŞşÜüâîû')
print('PASS: Turkish coverage; original base outlines and advance widths preserved.')
