"""Aşılı Kabuk kişiliği — toon VFX çalışması (25 Ekim 2026). Özgün geometri; dış model/doku/eklenti yok.
  blender -b --factory-startup -P Tools/Art/ShellBurst/build_shell_burst.py
Kullanıcı: her kaprisin kendi kişiliği olmalı. Diken saldırgan / patlayıcı, Filiz sinsi / yayılan; Kabuk = AĞIR, KALIN, İNATÇI.
Dil: kalın köşeli kabuk plakaları, çatlak çizgileri; gövde koyu kabuk kahvesi, bölge rengi (A amber, B mor) damarlarda ve
çekirdekte. Mavi yok (mavi = elektrik).
24 kare/sn, 146 kare:
  1–22   hazırlık: iki düğüm hücresinde toprak plaka plaka çatlar (altından bölge rengi sızar, plakalar titrer); düğümden kendi
         bölgesinin hücrelerine "aşı damarları" uzanır, uçlarında saksı kenarını saran kelepçe; damarlarda ışık boncuğu koşar.
  22–40  çıkış (ağır): düğüm toprağı iterek titreye titreye kalkar, plakaları devirir, az aşar ve "TOK" diye oturur
         (A 34, B 36): yere toz halkası + mürekkep şok halkası.
  52–68  A'ya üç vuruş: her vuruşta kabukta yeni çatlak (çekirdek ışığı sızar), sert kabuk kıymıkları, kısa "tink" kıvılcımı,
         düğüm ağır ağır sallanır (sıçrama / beyaz flaş yok). Üçüncüden sonra kırılmak üzereymiş gibi titrer.
  76     dördüncü vuruş = kırılma: kabuk beş plakaya ayrılıp ceviz gibi açılır, tepe topuzu fırlar, amber çekirdek yükselip patlar;
         A bölgesinde yer dalgası, damarlardan amber boncuklar hücrelere koşar → her hücre serbest kalır (kıvılcım + halka),
         damarlar söner.
  78–100 B: düğüm sarsılıp toprağa çekilir; damarlardan mor boncuklar hücrelere koşar → her hücrede kabuk yaka kalkar
         (plakalar kenardan kalkıp içe kapanır, toprağı kabuk plakaları örter, ek yerlerinden mor sızar).
  118–146 "20 sn sonra": kabuklar çatlar, plakalar dışa devrilip düşer, kıymık + toz; damarlar söner.
Diken çalışmasının toon yardımcıları (malzemeler, tarla, kart) aynı süreçte kullanılır; Random yalnız sabit tohumla.
"""
import bpy, bmesh, math, random
from pathlib import Path
from mathutils import Vector, Euler, Quaternion

HERE = Path(__file__).resolve().parent
THORN = HERE.parent / 'ThornBurst' / 'build_thorn_burst.py'
_src = THORN.read_text(encoding='utf-8')
exec(compile(_src[:_src.index('# ---------------------------------------------------------------- nesneler')], str(THORN), 'exec'))

ROOT = HERE.parents[2]
OUT = ROOT / 'Docs' / 'ShellBurst'
SHOTS = {'yakin': OUT / 'frames_yakin', 'genis': OUT / 'frames_genis', 'oyun': OUT / 'frames_oyun'}
for p in SHOTS.values(): p.mkdir(parents=True, exist_ok=True)
N = 146
s.frame_start, s.frame_end = 1, N

BARK, BARK_D, BARK_L, BARK_E = srgb('#6E4A33'), srgb('#4A3122'), srgb('#8C6142'), srgb('#D3A774')
STONE_L, STONE_S = srgb('#E9E1D3'), srgb('#ACA08F')
ZC = {'A': dict(main=srgb('#FFB723'), light=srgb('#FFE7A3'), dark=srgb('#B86A12')),
      'B': dict(main=srgb('#AA5FEB'), light=srgb('#E6CCFF'), dark=srgb('#5E2A7E'))}

# ---------------------------------------------------------------- yerleşim (oyundaki gibi 3×2 bölgeler, düğüm ortada)
GZ = .22
ZONES = {'A': [(x, y) for y in (-3, -2) for x in (-2, -1, 0)], 'B': [(x, y) for y in (1, 2) for x in (0, 1, 2)]}
NODE = {'A': (-1, -3), 'B': (1, 1)}
EM = {'A': 22, 'B': 24}                 # çıkış başlangıcı
TOK = {z: EM[z] + 12 for z in EM}       # oturma ("TOK")
HITS, BRK, DEC, OPEN = [52, 60, 68], 76, 78, 118
FRONT = math.atan2(-1.0, 1.0)           # düğümden kameraya bakan yatay yön
UP = Vector((0, 0, 1))
HIT_DIR = SR.copy(); HIT_DIR.z = 0; HIT_DIR.normalize()   # imleç ekranın solundan vurur → düğüm sağa sallanır


def cpos(c):
    return Vector((c[0] * 2.0, c[1] * 2.0, 0))


for z in NODE:   # düğüm hücreleri boş: düğüm toprağın içinden çıkar
    x, y = NODE[z]
    for o in [o for o in bpy.data.objects if o.name.startswith(f'plant {x},{y},')]: bpy.data.objects.remove(o)


# ---------------------------------------------------------------- yardımcılar
def keyq(o, f, q):
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = q; o.keyframe_insert('rotation_quaternion', frame=f)


def pulse(x, w):
    return math.sin(math.pi * x / w) if 0 < x < w else 0.0


def ease_in(x):
    x = min(1.0, max(0.0, x)); return x * x * x


def faces_obj(name, build, mat, ink=0.0, parent=None):
    """build(bm) -> her yüzün rengi listesi (oluşturma sırasıyla)."""
    bm = bmesh.new(); colors = build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm, mat, ink=ink, parent=parent)
    paint(o, lambda p: colors[p.index])
    return o


def ellip(th, ph, rx, rz, cz, bump=0.0):
    r = 1 + bump
    return Vector((rx * r * math.sin(th) * math.cos(ph), rx * r * math.sin(th) * math.sin(ph), cz + rz * r * math.cos(th)))


def ellip_normal(p, rx, rz, cz):
    return Vector((p.x / rx ** 2, p.y / rx ** 2, (p.z - cz) / rz ** 2)).normalized()


def lobe_bump(i_frac, j_frac, lobe):
    """Plakanın şişkinliği: ortada en çok, ek yerlerinde ve tepede / tabanda az (sarımsak dişi gibi lob)."""
    return lobe * max(0.0, math.sin(math.pi * j_frac)) ** .8 * math.sin(math.pi * i_frac * .9 + .15)


def shell_plate(name, ph0, ph1, rx, rz, cz, th0, th1, inner, lobe=.1, nph=6, nth=10, parent=None, ink=.022, tone=BARK):
    """Elipsoit kabuğun bir dilimi (kalın, lob lob şişkin plaka). Nesne kökü: plakanın alt kenarının ortası (menteşe)."""
    phm = (ph0 + ph1) / 2
    hinge = ellip(th1, phm, rx, rz, cz)

    def build(bm):
        cols = []
        O = [[None] * (nph + 1) for _ in range(nth + 1)]; I = [[None] * (nph + 1) for _ in range(nth + 1)]
        for i in range(nth + 1):
            th = th0 + (th1 - th0) * i / nth
            for j in range(nph + 1):
                ph = ph0 + (ph1 - ph0) * j / nph
                O[i][j] = bm.verts.new(ellip(th, ph, rx, rz, cz, lobe_bump(i / nth, j / nph, lobe)) - hinge)
                I[i][j] = bm.verts.new(ellip(th, ph, rx * inner, rz * inner, cz) - hinge)
        for i in range(nth):
            for j in range(nph):
                edge = j == 0 or j == nph - 1
                bm.faces.new((O[i][j], O[i + 1][j], O[i + 1][j + 1], O[i][j + 1])); cols.append(BARK_D if edge else BARK_L if i < 2 else tone)
                bm.faces.new((I[i][j + 1], I[i + 1][j + 1], I[i + 1][j], I[i][j])); cols.append(BARK_D)
        for i in range(nth):   # yan kenarlar koyu: aralık gölge gibi okunur, içinden çekirdek ışığı görünür
            bm.faces.new((O[i][0], I[i][0], I[i + 1][0], O[i + 1][0])); cols.append(BARK_D)
            bm.faces.new((O[i + 1][nph], I[i + 1][nph], I[i][nph], O[i][nph])); cols.append(BARK_D)
        for j in range(nph):   # üst ve alt kenar
            bm.faces.new((O[0][j + 1], I[0][j + 1], I[0][j], O[0][j])); cols.append(BARK_E)
            bm.faces.new((O[nth][j], I[nth][j], I[nth][j + 1], O[nth][j + 1])); cols.append(BARK_E)
        return cols
    o = faces_obj(name, build, LIT, ink=ink, parent=parent)
    o.location = hinge
    return o, hinge, Vector((math.cos(phm), math.sin(phm), 0))


def surface_ribbon(name, pts, normals, width, color, parent, origin, lift=0.0):
    def build(bm):
        L, R = [], []
        n = len(pts)
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
            side = normals[i].cross(t).normalized()
            w = width * (1 - .75 * abs(2 * i / (n - 1) - 1))
            q = p - origin + normals[i] * lift
            L.append(bm.verts.new(q + side * w)); R.append(bm.verts.new(q - side * w))
        cols = []
        for i in range(n - 1):
            bm.faces.new((L[i], R[i], R[i + 1], L[i + 1])); cols.append(color)
        return cols
    return faces_obj(name, build, UNLIT, parent=parent)


def tube_segment(name, a, b, ra, rb, light, dark, sides=6, ink=.016):
    """a → b konik parça; nesne kökü a (ölçek a'dan büyür)."""
    t = (b - a).normalized()
    ref = Vector((0, 0, 1)) if abs(t.z) < .95 else Vector((1, 0, 0))
    u = t.cross(ref).normalized(); w = t.cross(u).normalized()

    def build(bm):
        ra_ = [bm.verts.new((u * math.cos(k * 2 * math.pi / sides) + w * math.sin(k * 2 * math.pi / sides)) * ra) for k in range(sides)]
        rb_ = [bm.verts.new(b - a + (u * math.cos(k * 2 * math.pi / sides) + w * math.sin(k * 2 * math.pi / sides)) * rb) for k in range(sides)]
        cols = []
        for k in range(sides):
            j = (k + 1) % sides
            bm.faces.new((ra_[k], ra_[j], rb_[j], rb_[k])); cols.append(None)
        bm.faces.new(list(reversed(ra_))); cols.append(None); bm.faces.new(rb_); cols.append(None)
        return cols
    bm = bmesh.new(); build(bm); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm, LIT, ink=ink); o.location = a
    paint(o, lambda p: light if p.normal.z > .15 else dark)
    return o


def sweep(name, pts, radius, light, dark, sides=6, ink=.016):
    def build(bm):
        rings = []
        n = len(pts)
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
            ref = Vector((0, 0, 1)) if abs(t.z) < .95 else Vector((1, 0, 0))
            u = t.cross(ref).normalized(); w = t.cross(u).normalized()
            r = radius * (1 - .45 * abs(2 * i / (n - 1) - 1))
            rings.append([bm.verts.new(p + (u * math.cos(k * 2 * math.pi / sides) + w * math.sin(k * 2 * math.pi / sides)) * r) for k in range(sides)])
        for i in range(n - 1):
            for k in range(sides):
                j = (k + 1) % sides
                bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
        bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
        return [None] * len(bm.faces)
    bm = bmesh.new(); build(bm); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm, LIT, ink=ink)
    paint(o, lambda p: light if p.normal.z > .15 else dark)
    return o


def ring_mesh(name, r_in, r_out, color, n=48):
    def build(bm):
        a = [bm.verts.new((math.cos(2 * math.pi * i / n) * r_in, math.sin(2 * math.pi * i / n) * r_in, 0)) for i in range(n)]
        b = [bm.verts.new((math.cos(2 * math.pi * i / n) * r_out, math.sin(2 * math.pi * i / n) * r_out, 0)) for i in range(n)]
        for i in range(n): bm.faces.new((a[i], b[i], b[(i + 1) % n], a[(i + 1) % n]))
        return [color] * n
    return faces_obj(name, build, UNLIT)


def disc(name, radius, color, n=32):
    def build(bm):
        bm.faces.new([bm.verts.new((math.cos(2 * math.pi * i / n) * radius, math.sin(2 * math.pi * i / n) * radius, 0)) for i in range(n)])
        return [color]
    return faces_obj(name, build, UNLIT)


def slab(name, poly, thick, top, side, ink=.016):
    def build(bm):
        tv = [bm.verts.new((x, y, thick)) for x, y in poly]; bv = [bm.verts.new((x, y, 0)) for x, y in poly]
        n = len(poly); cols = []
        bm.faces.new(tv); cols.append(top); bm.faces.new(list(reversed(bv))); cols.append(side)
        for i in range(n):
            bm.faces.new((bv[i], bv[(i + 1) % n], tv[(i + 1) % n], tv[i])); cols.append(side)
        return cols
    return faces_obj(name, build, LIT, ink=ink)


def dust(name, seed, size):
    """Toz bulutu (Kabuk: gri-bej taş tozu): mürekkep kenar, gölge diski, kaydırılmış açık disk."""
    root = empty(name)
    outline = cloud_outline(seed)
    for j, (scale, col, fwd, dy) in enumerate([(1.12, INK, 0.0, 0.0), (1.0, STONE_S, .02, 0.0), (.86, STONE_L, .04, .1)]):
        bm = bmesh.new()
        off = -VD * fwd + SUP * (dy * size)
        c = bm.verts.new(off)
        ring = [bm.verts.new(off + SR * x + SUP * y) for (x, y) in outline(size * scale)]
        for i in range(len(ring)): bm.faces.new((c, ring[i], ring[(i + 1) % len(ring)]))
        o = obj_from_bm(f'{name} {j}', bm, UNLIT); paint(o, lambda p, col=col: col); o.parent = root
    return root


def spark(name, size, color):
    """Serbest kalma kıvılcımı: dört köşeli elmas (Filiz poleni gibi ince mürekkep)."""
    def diamond(scale):
        return [(0, size * 1.6 * scale), (size * .6 * scale, 0), (0, -size * 1.6 * scale), (-size * .6 * scale, 0)]
    return card(name, diamond, [(1.35, INK, 0.0), (1.0, color, .02)])


def tink(name, zone):
    """Sert vuruş kıvılcımı: altı düzensiz kırık cam ışını (Diken'in on iki dişli kırmızı yıldızından ayrı)."""
    rays = [(80, 1.0), (140, .55), (200, .8), (255, .45), (320, .95), (20, .5)]
    def outline(scale):
        pts = []
        for deg, length in rays:
            a = math.radians(deg)
            pts.append((math.cos(a) * .45 * length * scale, math.sin(a) * .45 * length * scale))
            a2 = a + math.radians(30)
            pts.append((math.cos(a2) * .09 * scale, math.sin(a2) * .09 * scale))
        return pts
    return card(name, outline, [(1.2, INK, 0.0), (1.0, ZC[zone]['main'], .02), (.55, CREAM, .04)])


def chip(name, size, seed):
    r = random.Random(seed)
    def build(bm):
        bmesh.ops.create_cube(bm, size=1)
        for v in bm.verts: v.co = Vector((v.co.x * size * 1.4, v.co.y * size * r.uniform(.7, 1.1), v.co.z * size * .45))
        for v in bm.verts: v.co.x += r.uniform(-.2, .2) * size
        return [BARK if i % 3 else BARK_E for i in range(len(bm.faces))]
    return faces_obj(name, build, LIT, ink=.014)


# ---------------------------------------------------------------- düğüm (zırh tohumu)
NODE_SIZE = {'A': (.6, .7, .38), 'B': (.52, .8, .44)}   # rx, rz, cz: A geniş ve tıknaz, B uzun
NODE_LOBE, NODE_TH0, NODE_TH1 = .1, math.radians(13), math.radians(116)
PLATES = 5
GAP = math.radians(4)


def node_crack_points(center, seed, th_a, th_b, reach=.11):
    r = random.Random(seed)
    pts = []
    for i in range(7):
        t = i / 6
        th = th_a + (th_b - th_a) * t
        ph = center + (reach if i % 2 else -reach) * (.4 + .6 * r.random()) * (1 - .5 * t)
        pts.append((th, ph))
    return pts


class Node:
    def __init__(self, z):
        self.z = z
        col = ZC[z]
        self.rx, self.rz, self.cz = NODE_SIZE[z]
        rx, rz, cz = self.rx, self.rz, self.cz
        self.root = empty(f'Düğüm {z}', cpos(NODE[z]) + Vector((0, 0, GZ)))
        self.plates = []
        half = math.pi / PLATES
        for k in range(PLATES):
            c = FRONT + k * 2 * half
            o, hinge, d = shell_plate(f'{z} plaka {k}', c - half + GAP, c + half - GAP, rx, rz, cz, NODE_TH0, NODE_TH1, .8,
                                      lobe=NODE_LOBE, parent=self.root)
            self.plates.append((o, hinge, d, c))
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1)
        for v in bm.verts: v.co = Vector((v.co.x * rx * .76, v.co.y * rx * .76, v.co.z * rz * .78 + cz))
        self.core = obj_from_bm(f'{z} çekirdek', bm, UNLIT, ink=.02, parent=self.root)
        paint(self.core, lambda p: col['light'] if p.normal.z > .35 else col['main'])
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=.14, radius2=.035, depth=.26)
        for v in bm.verts: v.co.z += .13
        self.knob = obj_from_bm(f'{z} topuz', bm, LIT, ink=.02, parent=self.root)
        paint(self.knob, lambda p: BARK_E if p.normal.z > .5 else BARK_D)
        self.knob_rest = Vector((0, 0, cz + rz * math.cos(NODE_TH0) - .03))
        self.knob.location = self.knob_rest
        # Bölge motifi (A üçgen, B baklava): ön plakaya gömülü, bölge renginde.
        front, hinge, _, _ = self.plates[0]
        th, ph = math.radians(62), FRONT
        p = self.surface(th, ph, FRONT, .03)
        n = ellip_normal(p, rx, rz, cz)
        upv = (UP - n * n.dot(UP)).normalized(); rt = upv.cross(n).normalized()
        shape = [(0, .17), (.16, -.11), (-.16, -.11)] if z == 'A' else [(0, .19), (.13, 0), (0, -.19), (-.13, 0)]
        for j, (scale, cc, fwd) in enumerate([(1.4, INK, 0.0), (1.0, col['main'], .012), (.45, col['light'], .022)]):
            def build(bm, scale=scale, cc=cc, fwd=fwd):
                base = p - hinge + n * fwd
                bm.faces.new([bm.verts.new(base + rt * x * scale + upv * y * scale) for x, y in shape])
                return [cc]
            faces_obj(f'{z} motif {j}', build, UNLIT, parent=front)
        # Çatlak aşamaları (vuruş 1–3): plaka yüzeyinde mürekkep + içinden bölge ışığı.
        self.cracks = []
        if True:   # iki bölgede de (oyunda iki düğüm de vurulabilir); çalışmada yalnız A'nınki görünür
            hit_plate = min(range(PLATES), key=lambda k: abs((self.plates[k][3] - math.atan2(-HIT_DIR.y, -HIT_DIR.x) + math.pi) % (2 * math.pi) - math.pi))
            stages = [(1, hit_plate, 3, .45, 1.5), (2, 0, 8, .3, 1.35), (3, hit_plate, 14, 1.0, 1.85), (3, (hit_plate + 1) % PLATES, 19, .35, 1.3), (3, 0, 23, 1.15, 1.9)]
            for stage, k, seed, tha, thb in stages:
                o, hinge, _, c = self.plates[k]
                tp = node_crack_points(c, seed, tha, thb)
                pts = [self.surface(th, ph, c, .012) for th, ph in tp]
                nrm = [ellip_normal(q, rx, rz, cz) for q in pts]
                a = surface_ribbon(f'{z} çatlak {stage}.{k} m', pts, nrm, .085, INK, o, hinge, lift=.006)
                b = surface_ribbon(f'{z} çatlak {stage}.{k} ışık', pts, nrm, .036, col['main'], o, hinge, lift=.016)
                self.cracks.append((stage, a, b))

    def surface(self, th, ph, center, extra=0.0):
        """Lob plakanın dış yüzeyindeki nokta (motif ve çatlaklar yüzeye otursun)."""
        half = math.pi / PLATES - GAP
        jf = min(1.0, max(0.0, (ph - (center - half)) / (2 * half)))
        i_f = (th - NODE_TH0) / (NODE_TH1 - NODE_TH0)
        return ellip(th, ph, self.rx, self.rz, self.cz, lobe_bump(i_f, jf, NODE_LOBE) + extra)


NODES = {z: Node(z) for z in ('A', 'B')}


# ---------------------------------------------------------------- doğum yeri: çatlayan toprak plakaları + altından sızan ışık
class Birth:
    def __init__(self, z):
        self.z = z
        c0 = cpos(NODE[z]) + Vector((0, 0, GZ - .004))
        r = random.Random(31 if z == 'A' else 37)
        self.glow = disc(f'{z} doğum ışığı', .64, ZC[z]['main']); self.glow.location = c0 + Vector((0, 0, .002))
        self.slabs = []
        center = [(math.cos(a) * .17, math.sin(a) * .17) for a in [i * math.pi / 3 + .2 for i in range(6)]]
        o = slab(f'{z} orta plaka', center, .05, SOIL_A, SOIL_D); o.location = c0
        self.center = o
        for k in range(6):
            a0 = k * math.pi / 3 + r.uniform(-.12, .12) + .2; a1 = (k + 1) * math.pi / 3 + .2 - .07
            am = (a0 + a1) / 2
            ri, ro = .22, .6 + r.uniform(-.03, .03)
            pts = [Vector((math.cos(a0) * ri, math.sin(a0) * ri, 0)), Vector((math.cos(a1) * ri, math.sin(a1) * ri, 0)),
                   Vector((math.cos(a1) * ro, math.sin(a1) * ro, 0)), Vector((math.cos(am) * (ro + .03), math.sin(am) * (ro + .03), 0)),
                   Vector((math.cos(a0) * ro, math.sin(a0) * ro, 0))]
            hinge = Vector((math.cos(am) * ro, math.sin(am) * ro, 0))
            cen = sum(pts, Vector()) / len(pts)
            pts = [cen + (p - cen) * .82 for p in pts]   # aralık: altından bölge ışığı sızar
            o = slab(f'{z} plaka {k}', [((p - hinge).x, (p - hinge).y) for p in pts], .05, SOIL_A if k % 2 else SOIL_B, SOIL_D)
            o.location = c0 + hinge
            self.slabs.append((o, c0 + hinge, Vector((math.cos(am), math.sin(am), 0))))
        self.c0 = c0


BIRTH = {z: Birth(z) for z in ('A', 'B')}


# ---------------------------------------------------------------- aşı damarları + kelepçeler
class Vein:
    def __init__(self, z, cell, idx):
        self.z, self.cell = z, cell
        a = cpos(NODE[z]); b = cpos(cell)
        d = (b - a); L = d.length; d.normalize()
        side = Vector((-d.y, d.x, 0))
        start = a + d * .55; end = b - d * .78
        self.pts = []
        n = 9
        for i in range(n + 1):
            t = i / n
            p = start + (end - start) * t + side * (.1 * math.sin(t * math.pi * 2 + idx)) + Vector((0, 0, .3 + .2 * math.sin(math.pi * t)))
            self.pts.append(p)
        self.segs = []
        for i in range(n):
            seg = tube_segment(f'{z} damar {cell} {i}', self.pts[i], self.pts[i + 1], .075 * (1 - .3 * i / n), .075 * (1 - .3 * (i + 1) / n), BARK, BARK_D)
            # damarın sırtında bölge ışığı (ince şerit; parçanın çocuğu, onunla büyür)
            def build(bm, a=self.pts[i], b=self.pts[i + 1]):
                t = (b - a).normalized(); sd = Vector((-t.y, t.x, 0)).normalized() * .026
                q0 = Vector((0, 0, .072)); q1 = b - a + Vector((0, 0, .066))
                bm.faces.new((bm.verts.new(q0 + sd), bm.verts.new(q0 - sd), bm.verts.new(q1 - sd), bm.verts.new(q1 + sd)))
                return [ZC[z]['main']]
            faces_obj(f'{z} damar ışığı {cell} {i}', build, UNLIT, parent=seg)
            self.segs.append(seg)
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=.11)
        self.bead = obj_from_bm(f'{z} boncuk {cell}', bm, UNLIT, ink=.02); paint(self.bead, lambda p: ZC[z]['light'])
        # kelepçe: damarın vardığı yanda saksı kenarını saran kabuk yayı
        ang = math.atan2(-d.y, -d.x)
        arc = [b + Vector((math.cos(ang + u) * .8, math.sin(ang + u) * .8, .27)) for u in [math.radians(-55 + 110 * k / 8) for k in range(9)]]
        self.clamp = empty(f'{z} kelepçe kökü {cell}', b + Vector((math.cos(ang) * .8, math.sin(ang) * .8, .27)))
        self.clamp_rest = self.clamp.location.copy(); self.clamp_out = Vector((math.cos(ang), math.sin(ang), 0))
        o = sweep(f'{z} kelepçe {cell}', [p - self.clamp.location for p in arc], .08, BARK_L, BARK_D)
        o.parent = self.clamp
        def build(bm):
            pts = [p - self.clamp.location + Vector((0, 0, .078)) for p in arc]
            L_, R_ = [], []
            for i, p in enumerate(pts):
                t = (pts[min(i + 1, 8)] - pts[max(i - 1, 0)]).normalized(); sd = Vector((-t.y, t.x, 0)) * .03
                L_.append(bm.verts.new(p + sd)); R_.append(bm.verts.new(p - sd))
            for i in range(8): bm.faces.new((L_[i], R_[i], R_[i + 1], L_[i + 1]))
            return [ZC[z]['main']] * 8
        faces_obj(f'{z} kelepçe ışığı {cell}', build, UNLIT, parent=self.clamp)
        self.length = sum((self.pts[i + 1] - self.pts[i]).length for i in range(n))

    def point(self, u):
        u = min(1.0, max(0.0, u)) * (len(self.pts) - 1)
        i = min(int(u), len(self.pts) - 2)
        return self.pts[i].lerp(self.pts[i + 1], u - i)


VEINS = {z: [Vein(z, c, i) for i, c in enumerate([c for c in ZONES[z] if c != NODE[z]])] for z in ('A', 'B')}


def travel(v):
    return max(5, int(round(v.length / .32)))


# ---------------------------------------------------------------- B: kabuk yaka (kapanan hücre)
class Collar:
    def __init__(self, cell, zone='B'):
        self.cell, self.zone = cell, zone
        c0 = cpos(cell)
        self.root = empty(f'Yaka {cell}', c0 + Vector((0, 0, .17)))
        self.plates = []
        r = random.Random(cell[0] * 7 + cell[1] * 13 + 50)
        for k in range(6):
            ph0 = k * math.pi / 3 + math.radians(2.5) + .3; ph1 = (k + 1) * math.pi / 3 - math.radians(2.5) + .3
            phm = (ph0 + ph1) / 2
            R0, H = .8, .55 + r.uniform(-.04, .06)
            hinge = Vector((math.cos(phm) * R0, math.sin(phm) * R0, 0))

            def build(bm, ph0=ph0, ph1=ph1, H=H, hinge=hinge, seed=k):
                rr = random.Random(seed * 3 + cell[0])
                cols = []; nph, nv = 5, 6
                O = [[None] * (nph + 1) for _ in range(nv + 1)]; I = [[None] * (nph + 1) for _ in range(nv + 1)]
                tops = [1 + .14 * (rr.random() - .3) for _ in range(nph + 1)]
                for i in range(nv + 1):
                    v = i / nv
                    for j in range(nph + 1):
                        ph = ph0 + (ph1 - ph0) * j / nph
                        h = H * v * (tops[j] if i == nv else 1)
                        rad = R0 - .22 * v * v + .03 * abs(math.sin(i * 1.6))   # içe eğik, sırtlı
                        O[i][j] = bm.verts.new(Vector((math.cos(ph) * (rad + .045), math.sin(ph) * (rad + .045), h)) - hinge)
                        I[i][j] = bm.verts.new(Vector((math.cos(ph) * (rad - .045), math.sin(ph) * (rad - .045), h)) - hinge)
                for i in range(nv):
                    for j in range(nph):
                        bm.faces.new((O[i][j], O[i][j + 1], O[i + 1][j + 1], O[i + 1][j])); cols.append(BARK_D if i % 3 == 1 else BARK)
                        bm.faces.new((I[i + 1][j], I[i + 1][j + 1], I[i][j + 1], I[i][j])); cols.append(BARK_D)
                for i in range(nv):
                    bm.faces.new((O[i + 1][0], I[i + 1][0], I[i][0], O[i][0])); cols.append(BARK_E)
                    bm.faces.new((O[i][nph], I[i][nph], I[i + 1][nph], O[i + 1][nph])); cols.append(BARK_E)
                for j in range(nph):
                    bm.faces.new((O[nv][j], O[nv][j + 1], I[nv][j + 1], I[nv][j])); cols.append(BARK_E)
                return cols
            o = faces_obj(f'Yaka {cell} plaka {k}', build, LIT, ink=.02, parent=self.root)
            o.location = hinge
            d = Vector((math.cos(phm), math.sin(phm), 0))
            # açılışta çatlak (öndeki plakalarda)
            crack = None
            if d.dot(Vector((1, -1, 0)).normalized()) > .2:
                pts = [hinge + Vector((0, 0, .08)) + d * .05 + Vector((-d.y, d.x, 0)) * (.06 * (1 if i % 2 else -1)) + Vector((0, 0, H * .85 * i / 5)) - d * (.22 * (i / 5) ** 2) for i in range(6)]
                nrm = [d] * len(pts)
                crack = (surface_ribbon(f'Yaka {cell} çatlak {k} m', pts, nrm, .045, INK, o, hinge, lift=.05),
                         surface_ribbon(f'Yaka {cell} çatlak {k} ışık', pts, nrm, .02, ZC[zone]['main'], o, hinge, lift=.06))
            self.plates.append((o, d, crack))
        # içeride sızan mor ışık bandı (plaka aralıklarından görünür)
        def band(bm):
            n = 36; a = []; b = []
            for i in range(n):
                ang = 2 * math.pi * i / n
                a.append(bm.verts.new((math.cos(ang) * .72, math.sin(ang) * .72, .02))); b.append(bm.verts.new((math.cos(ang) * .64, math.sin(ang) * .64, .38)))
            for i in range(n): bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
            return [ZC[zone]['main']] * n
        self.band = faces_obj(f'Yaka {cell} ışık', band, UNLIT, parent=self.root)
        # toprağı örten kabuk plakaları (doğum plakalarının kabuk tonlu kardeşi)
        self.crust = []
        for k in range(5):
            a0 = k * 2 * math.pi / 5 + .4; a1 = (k + 1) * 2 * math.pi / 5 + .4 - .09
            am = (a0 + a1) / 2
            pts = [(0, 0), (math.cos(a0) * .58, math.sin(a0) * .58), (math.cos(am) * .63, math.sin(am) * .63), (math.cos(a1) * .58, math.sin(a1) * .58)]
            cen = Vector((sum(p[0] for p in pts) / 4, sum(p[1] for p in pts) / 4, 0))
            o = slab(f'Yaka {cell} kabuk {k}', [(x - cen.x, y - cen.y) for x, y in pts], .045, BARK_L, BARK_D)
            o.parent = self.root; o.location = cen + Vector((0, 0, .05))
            self.crust.append((o, cen))
        self.flakes = [chip(f'Yaka {cell} kıymık {k}', .07, cell[0] * 5 + k) for k in range(3)]
        self.dusts = [dust(f'Yaka {cell} toz {k}', 300 + cell[0] * 3 + cell[1] + k, .2) for k in range(2)]


COLLARS = {c: Collar(c) for c in ZONES['B']}


# ---------------------------------------------------------------- efekt parçaları
TOK_DUST = {z: [dust(f'{z} oturma tozu {k}', 120 + k + (0 if z == 'A' else 20), .16 + .03 * (k % 3)) for k in range(8)] for z in NODE}
SHOCK = {z: ring_mesh(f'{z} şok halkası', .92, 1.0, INK) for z in NODE}
TINKS = [tink(f'Tink {k}', 'A') for k in range(4)]
CHIPS = [chip(f'Kıymık {k}', .12, 70 + k) for k in range(9)]
CORE_SPARKS = [spark(f'Çekirdek kıvılcımı {k}', .07, ZC['A']['light'] if k % 2 else CREAM) for k in range(8)]
FREE = {v.cell: [spark(f'Serbest {v.cell} {k}', .06, ZC['A']['light'] if k % 2 else CREAM) for k in range(4)] for v in VEINS['A']}
B_SINK_DUST = [dust(f'B çekilme tozu {k}', 200 + k, .2) for k in range(5)]

# ---------------------------------------------------------------- zamanlamalar
ARRIVE = {}
for z in ('A', 'B'):
    for i, v in enumerate(VEINS[z]):
        start = (BRK + 6) if z == 'A' else (DEC + 6)
        ARRIVE[(z, v.cell)] = (start + i * .0, start + travel(v))
CLOSE_AT = {c: ARRIVE[('B', c)][1] for c in ZONES['B'] if c != NODE['B']}
CLOSE_AT[NODE['B']] = DEC + 15
OPEN_AT = {c: OPEN + 2 * (abs(c[0] - NODE['B'][0]) + abs(c[1] - NODE['B'][1])) for c in ZONES['B']}

# ---------------------------------------------------------------- kare kare
HIDE_Q = Quaternion()


def node_frame(nd, f):
    z = nd.z
    t = f - EM[z]
    tok = TOK[z]
    if t < 0:
        key(nd.root, f, loc=cpos(NODE[z]) + Vector((0, 0, GZ - 1.3)), scale=HIDE); keyq(nd.root, f, HIDE_Q)
        return
    loc = cpos(NODE[z]) + Vector((0, 0, GZ))
    sc = Vector((1, 1, 1)); q = Quaternion()
    if f < tok:
        u = t / 10
        lift = -1.25 * (1 - smooth(u)) if t <= 10 else .12 * pulse(t - 10, 2.2)
        loc = loc + Vector((0, 0, lift + .025 * math.sin(t * 3.1)))
        q = Euler((math.radians(3 * math.sin(t * 2.3)), math.radians(3 * math.cos(t * 1.9)), 0)).to_quaternion()
    else:
        k = f - tok
        sc = {0: Vector((1.14, 1.14, .82)), 1: Vector((.95, .95, 1.06)), 2: Vector((1.02, 1.02, .98))}.get(k, Vector((1, 1, 1)))
        breath = .015 * math.sin((f - tok) * .25)
        sc = Vector((sc.x, sc.y, sc.z + breath))
    # vuruşlar (yalnız A): ağır sallanma, tabandan
    if z == 'A':
        ax = UP.cross(HIT_DIR).normalized()
        ang = 0.0
        for h in HITS + [BRK]:
            th_ = f - h
            if 0 <= th_ < 30 and f < BRK: ang += math.radians(14) * math.exp(-th_ * .26) * math.cos(th_ * .62)
        if HITS[-1] + 3 <= f < BRK:   # kırılmak üzere: kısa sert titreşim
            ang += math.radians(1.6 * math.sin(f * 2.9))
            loc = loc + Vector((.012 * math.sin(f * 3.7), 0, 0))
        for h in HITS:
            if 0 <= f - h < 2: sc = Vector((1.06, 1.06, .93))
        q = Quaternion(ax, ang) @ q
    if z == 'B' and f >= DEC:
        k = f - DEC
        if k < 5: q = Euler((math.radians(4 * math.sin(k * 3.3)), math.radians(4 * math.cos(k * 2.9)), 0)).to_quaternion()
        sink = ease_in((k - 4) / 11)
        loc = loc + Vector((0, 0, -1.3 * sink))
        if sink >= .999: sc = Vector((HIDE, HIDE, HIDE))
    key(nd.root, f, loc=loc, scale=sc); keyq(nd.root, f, q)
    # plakalar: kırılmada ceviz gibi açılır
    for k, (o, hinge, d, c) in enumerate(nd.plates):
        if z == 'A' and f >= BRK:
            u = (f - BRK - .6 * k) / 6
            a = math.radians(102 + 8 * (k % 2)) * ease_out(u) if u > 0 else 0.0
            hop = .18 * pulse(f - BRK - .6 * k, 7)
            sink = smooth((f - BRK - 18) / 10)
            key(o, f, loc=hinge + d * (.12 * ease_out(u)) + Vector((0, 0, hop - .35 * sink)), scale=HIDE if sink >= .99 else 1)
            keyq(o, f, Quaternion(UP.cross(d).normalized(), a))
        else:
            key(o, f, loc=hinge, scale=1); keyq(o, f, Quaternion())
    for stage, a, b in nd.cracks:
        on = z == 'A' and f >= HITS[stage - 1]
        sc_ = HIDE if not on else (1.25 if f - HITS[stage - 1] < 2 else 1.0)
        key(a, f, scale=sc_); key(b, f, scale=sc_)
    # çekirdek: hasarla şişer (ek yerlerinden daha çok ışık), kırılmada yükselir ve patlar
    dmg = sum(1 for h in HITS if f >= h) if z == 'A' else 0
    cs = 1 + .045 * dmg
    cl = Vector()
    if z == 'A' and f >= BRK:
        k = f - BRK
        cs = 1.25 * smooth(k / 4) + cs * (1 - smooth(k / 4)) if k < 8 else max(HIDE, 1.25 * (1 - (k - 8) / 3))
        cl = Vector((0, 0, .35 * smooth((k - 4) / 4)))
        if k >= 11: cs = HIDE
    key(nd.core, f, loc=cl, scale=cs)
    if z == 'A' and f >= BRK:
        k = f - BRK
        key(nd.knob, f, loc=nd.knob_rest + Vector((-.25 * k / 10, .1 * k / 10, 1.6 * (k / 10) - 1.9 * (k / 10) ** 2)),
            rot=(k * .5, k * .3, 0), scale=HIDE if k > 12 else 1)
    else:
        key(nd.knob, f, loc=nd.knob_rest, rot=(0, 0, 0), scale=1)


def birth_frame(b, f):
    z = b.z
    t = f - EM[z]
    tok = TOK[z]
    node_top = NODES[z].root.location.z + NODES[z].cz + NODES[z].rz   # bu karede yazılmış düğüm konumu
    # ışık diski: hazırlıkta büyür ve ağır ağır nabız atar, çıkışta söner
    g = smooth((f - 2) / 12) * (1 + .06 * math.sin(f * .55)) * (1 - smooth((t - 8) / 5))
    key(b.glow, f, loc=b.c0 + Vector((0, 0, .002)), scale=HIDE if g < .01 else (g, g, 1))
    amp = math.radians(1 + 5 * smooth((f - 4) / 18))
    for k, (o, at, d) in enumerate(b.slabs):
        rattle = Euler((amp * math.sin(f * 2.7 + k), amp * math.cos(f * 3.1 + 2 * k), 0)).to_quaternion() if t < 12 else Quaternion()
        lift = .012 + .05 * smooth((f - 4) / 18)
        tilt = math.radians(65) * smooth(t / 9) if 0 <= t < 12 else 0.0
        if t >= 12:   # TOK: plakalar dışa devrilip düşer, sonra toprağa çekilir
            tilt = math.radians(65 + 45 * ease_out((t - 12) / 3))
        sink = smooth((t - 20) / 10)
        loc = at + d * (.14 * smooth(t / 9) + (.18 * ease_out((t - 12) / 4) if t >= 12 else 0)) + Vector((0, 0, lift - .25 * sink))
        key(o, f, loc=loc, scale=HIDE if sink >= .99 else 1)
        keyq(o, f, Quaternion(UP.cross(d).normalized(), tilt) @ rattle)
    # orta plaka: düğümün tepesinde kalkar, TOK'ta yana kayıp düşer
    o = b.center
    if t < 7:
        key(o, f, loc=b.c0 + Vector((0, 0, .01 + .03 * smooth((f - 4) / 18))), rot=(amp * math.sin(f * 2.2), amp * math.cos(f * 2.5), 0), scale=1)
    elif t < 12:
        key(o, f, loc=Vector((b.c0.x, b.c0.y, max(b.c0.z, node_top - .05))), rot=(0, math.radians(8 * (t - 7)), 0), scale=1)
    else:
        u = (t - 12) / 6
        side = SR * -.8 + Vector((1, -1, 0)).normalized() * .4
        sink = smooth((t - 22) / 8)
        key(o, f, loc=b.c0 + side * ease_out(u) + Vector((0, 0, (1.15 * (1 - u) if u < 1 else 0) + .02 - .25 * sink)),
            rot=(math.radians(150) * ease_out(u), math.radians(40) * u, 0), scale=HIDE if sink >= .99 else 1)
    # TOK tozu ve şok halkası
    for k, cl in enumerate(TOK_DUST[z]):
        u = (t - 12 - .3 * (k % 3)) / 10
        a = k * 2 * math.pi / 8 + .3
        dd = Vector((math.cos(a), math.sin(a), 0))
        size = (.35 + .65 * ease_out(u / .3)) * (1 - smooth((u - .45) / .55))
        key(cl, f, loc=b.c0 + dd * (.55 + .55 * ease_out(u)) + Vector((0, 0, .12 + .12 * u)), scale=HIDE if u < 0 or u > 1 else max(HIDE, size))
    u = (t - 12) / 5
    key(SHOCK[z], f, loc=b.c0 + Vector((0, 0, .01)), scale=HIDE if u < 0 or u > 1 else (.6 + 1.1 * ease_out(u), .6 + 1.1 * ease_out(u), 1))


def vein_frame(v, i, f):
    z = v.z
    n = len(v.segs)
    a_start, a_end = ARRIVE[(z, v.cell)]
    for k, seg in enumerate(v.segs):
        grown = f >= 4 + .6 * i + k * .9
        pop = 1.25 if grown and f - (4 + .6 * i + k * .9) < 1 else 1.0
        sc = pop if grown else HIDE
        if z == 'A' and f >= BRK:   # serbest kalma: boncuk geçtikten sonra söner
            pass_k = a_start + (a_end - a_start) * (k + .5) / n
            w = smooth((f - pass_k - 3) / 5)
            sc = HIDE if w >= .99 else (1 - .5 * w, 1 - .5 * w, 1 - w)
        if z == 'B' and f >= OPEN:
            w = smooth((f - OPEN_AT[v.cell] - 8 - (n - 1 - k) * .5) / 5)
            sc = HIDE if w >= .99 else (1 - .5 * w, 1 - .5 * w, 1 - w)
        key(seg, f, scale=sc)
    # kelepçe: damar ulaşınca kenara oturur
    ga = 4 + .6 * i + n * .9
    cs = 0 if f < ga else (1.3 if f - ga < 1 else 1.0)
    loc, q = v.clamp_rest, Quaternion()
    if z == 'A' and f >= a_end:   # serbest: kelepçe kopar, dışa savrulup düşer, söner
        u = (f - a_end) / 9
        loc = v.clamp_rest + v.clamp_out * (.35 * ease_out(u)) + Vector((0, 0, .55 * u - .7 * u * u))
        q = Quaternion(UP.cross(v.clamp_out).normalized(), math.radians(80) * ease_out(u))
        cs = max(0.0, 1 - smooth((u - .55) / .45))
    if z == 'B' and f >= CLOSE_AT[v.cell]: cs = max(0.0, 1 - smooth((f - CLOSE_AT[v.cell]) / 3))   # yaka kelepçeyi yutar
    key(v.clamp, f, loc=loc, scale=HIDE if cs < .01 else cs); keyq(v.clamp, f, q)
    # boncuk: hazırlıkta bağlantıyı gösterir; kararda (A serbest / B kapanış) hücreye koşar
    pos, show = None, False
    pu = (f - (13 + .7 * i)) / 7
    if 0 <= pu <= 1: pos, show = v.point(pu), True
    if a_start <= f <= a_end:
        pos, show = v.point((f - a_start) / (a_end - a_start)), True
    key(v.bead, f, loc=pos if pos is not None else v.pts[0], scale=(1.0 if show else HIDE))


def free_frame(cell, f):
    sparks = FREE[cell]
    at = cpos(cell) + Vector((0, 0, GZ))
    arr = ARRIVE[('A', cell)][1]
    for k, sp in enumerate(sparks):
        u = (f - arr - .5 * k) / 12
        a = k * math.pi / 2 + .6
        d = Vector((math.cos(a), math.sin(a), 0))
        key(sp, f, loc=at + d * .45 * ease_out(u) + Vector((0, 0, .3 + .9 * u)), scale=HIDE if u < 0 or u > 1 else max(HIDE, 1 - smooth((u - .5) / .5)))


def collar_frame(cl, f):
    ca = CLOSE_AT[cl.cell]; oa = OPEN_AT[cl.cell]
    shake = 0.0
    if oa <= f < oa + 6: shake = math.radians(3 * math.sin((f - oa) * 3.4))
    keyq(cl.root, f, Euler((shake, -shake, 0)).to_quaternion())
    for k, (o, d, crack) in enumerate(cl.plates):
        u = (f - ca - .5 * k) / 4
        ax = UP.cross(d).normalized()
        if u < 0:
            key(o, f, scale=HIDE); keyq(o, f, Quaternion(ax, math.radians(100)))
        elif f < oa + 6:
            # kalkıp içe kapanır: 100° (yatık) → -14° aşma → -6° (içe eğik), "tak"
            a = 100 - 114 * ease_out(u) if u < 1 else -14 + 8 * smooth((u - 1) / 1.5)
            key(o, f, scale=min(1.0, .3 + .7 * ease_out(u / .6))); keyq(o, f, Quaternion(ax, math.radians(a)))
        else:
            v = (f - oa - 6 - .4 * k) / 5
            a = -6 + 126 * ease_out(v) if v > 0 else -6
            sink = smooth((f - oa - 16) / 8)
            keyq(o, f, Quaternion(ax, math.radians(a)))
            key(o, f, scale=HIDE if sink >= .99 else (1 - .5 * sink, 1 - .5 * sink, 1 - sink))
        if crack:
            on = oa <= f < oa + 14
            for c in crack: key(c, f, scale=1 if on else HIDE)
    # ışık bandı ve toprak kabuğu
    up = smooth((f - ca - 1) / 3) * (1 - smooth((f - oa - 6) / 3))
    key(cl.band, f, scale=HIDE if up < .01 else (1, 1, up))
    for k, (o, cen) in enumerate(cl.crust):
        u = (f - ca - 2 - .4 * k) / 3
        if u < 0: key(o, f, scale=HIDE, loc=cen + Vector((0, 0, .05))); continue
        if f < oa + 6:
            key(o, f, scale=(1.2 if u < 1 else 1.0), loc=cen + Vector((0, 0, .05)), rot=(0, 0, 0))
        else:
            v = (f - oa - 6 - .5 * k) / 8
            dd = cen.normalized() if cen.length > 1e-4 else Vector((1, 0, 0))
            key(o, f, loc=cen + dd * .25 * ease_out(v) + Vector((0, 0, .05 + .35 * pulse(v * 8, 6))), rot=(v * 3 + k, v * 2, 0),
                scale=HIDE if v >= 1 else max(HIDE, 1 - smooth((v - .5) / .5)))
    # kırılmada kıymık + toz
    for k, ch in enumerate(cl.flakes):
        v = (f - oa - 6 - .5 * k) / 10
        a = k * 2.1 + .5
        dd = Vector((math.cos(a), math.sin(a), 0))
        key(ch, f, loc=cl.root.location + dd * .7 * ease_out(v) + Vector((0, 0, .4 + .9 * v - 1.2 * v * v)), rot=(v * 7 + k, v * 5, v * 3),
            scale=HIDE if v < 0 or v > 1 else 1)
    for k, ds in enumerate(cl.dusts):
        v = (f - oa - 6 - k) / 9
        a = k * math.pi + .8
        key(ds, f, loc=cl.root.location + Vector((math.cos(a), math.sin(a), 0)) * (.5 + .3 * v) + Vector((0, 0, .25 + .2 * v)),
            scale=HIDE if v < 0 or v > 1 else max(HIDE, (.4 + .6 * ease_out(v / .3)) * (1 - smooth((v - .4) / .6))))


def hit_frame(f):
    nd = NODES['A']
    impact = cpos(NODE['A']) + Vector((0, 0, GZ + .55)) - HIT_DIR * .5
    for j, h in enumerate(HITS + [BRK]):
        k = f - h
        size = {0: .8, 1: 1.3, 2: .65}.get(k, 0) * (1.35 if h == BRK else 1)
        key(TINKS[j], f, loc=impact - VD * .4, scale=HIDE if size == 0 else size)
    for j, ch in enumerate(CHIPS):
        h = HITS[j // 3]
        u = (f - h) / 11
        side = Vector((-HIT_DIR.y, HIT_DIR.x, 0)) * ((j % 3) - 1) * .7
        dd = (-HIT_DIR * .9 + side).normalized()
        key(ch, f, loc=impact + dd * 1.1 * ease_out(u) + Vector((0, 0, .7 * u - 1.0 * u * u)), rot=(u * 8 + j, u * 5, u * 6 + j),
            scale=HIDE if u < 0 or u > 1 else 1)
    # kırılma: çekirdek kıvılcımları + yer dalgası
    center = cpos(NODE['A']) + Vector((0, 0, GZ + nd.cz + .35))
    for k, sp in enumerate(CORE_SPARKS):
        u = (f - BRK - 8 - .3 * (k % 3)) / 12
        a = k * 2 * math.pi / 8 + .2
        d = SR * math.cos(a) + SUP * math.sin(a)
        key(sp, f, loc=center + d * (.4 + .9 * ease_out(u)) + Vector((0, 0, .3 * u)), scale=HIDE if u < 0 or u > 1 else max(HIDE, 1 - smooth((u - .5) / .5)))
    # B çekilirken toz
    for k, ds in enumerate(B_SINK_DUST):
        v = (f - DEC - 6 - .5 * k) / 10
        a = k * 2 * math.pi / 5 + .4
        key(ds, f, loc=cpos(NODE['B']) + Vector((0, 0, GZ + .15)) + Vector((math.cos(a), math.sin(a), 0)) * (.5 + .4 * v),
            scale=HIDE if v < 0 or v > 1 else max(HIDE, (.4 + .6 * ease_out(v / .3)) * (1 - smooth((v - .4) / .6))))


for f in range(1, N + 1):
    for nd in NODES.values(): node_frame(nd, f)
    for b in BIRTH.values(): birth_frame(b, f)
    for z in VEINS:
        for i, v in enumerate(VEINS[z]): vein_frame(v, i, f)
    for c in FREE: free_frame(c, f)
    for cl in COLLARS.values(): collar_frame(cl, f)
    hit_frame(f)

for o in bpy.data.objects:
    ad = o.animation_data
    if ad and ad.action:
        for fc in getattr(ad.action, 'fcurves', []):
            for kp in fc.keyframe_points: kp.interpolation = 'CONSTANT'

# ---------------------------------------------------------------- ışık, kamera, render
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); s.collection.objects.link(sun)
sun.rotation_euler = (math.radians(38), math.radians(-18), math.radians(-40)); sun.data.energy = 3
s.world = bpy.data.worlds.new('Uzay'); s.world.use_nodes = True
s.world.node_tree.nodes['Background'].inputs[0].default_value = (*lin(SPACE), 1)
cam = bpy.data.objects.new('Kamera', bpy.data.cameras.new('Kamera')); s.collection.objects.link(cam)
cam.rotation_euler = tuple(math.radians(a) for a in CAM_ROT)
cam.data.type = 'ORTHO'; cam.data.clip_end = 100
s.camera = cam
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 960, 600, 100
s.render.image_settings.file_format = 'PNG'
MID = (cpos(NODE['A']) + cpos(NODE['B'])) * .5
SETUPS = {
    'yakin': (cpos(NODE['A']) + Vector((.4, .4, .9)), 6.2),
    'genis': (MID + SR * 1.4 + Vector((0, 0, .4)), 16.0),
    'oyun': (MID + SR * 1.4 + Vector((0, 0, .4)), 22 * 1920 / 1080 / 2),   # oyun kamerası mesafesi (1080p'de aynı piksel boyu)
}
import sys
only = [a for a in sys.argv if a.startswith('shot=')]
rng = [a for a in sys.argv if a.startswith('fr=')]   # hızlı deneme: fr=34-40
if rng: s.frame_start, s.frame_end = (int(x) for x in rng[0][3:].split('-'))
for name, (focus, ortho) in SETUPS.items():
    if only and only[0] != 'shot=' + name: continue
    cam.location = focus + cam.rotation_euler.to_matrix() @ Vector((0, 0, 30))
    cam.data.ortho_scale = ortho
    s.render.filepath = str(SHOTS[name] / 'f_')
    bpy.ops.render.render(animation=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ShellBurst.blend'))
print('SHELL BURST DONE')
