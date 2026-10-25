"""İstilacı Filiz kişiliği — toon VFX çalışması (24 Ekim 2026). Özgün geometri; dış model/doku/eklenti yok.
  blender -b --factory-startup -P Tools/Art/SproutBurst/build_sprout_burst.py
Kullanıcı: Filiz, Diken'in toprak patlamasıyla aynı göründü; her kaprisin kendi kişiliği olmalı. Filiz = sinsi, yayılan, canlı:
turkuaz + pembe, kıvrımlı ve yuvarlak şekiller (Diken'in sivri / ateşli dilinin tersi).
24 kare/sn, 84 kare:
  1–12  hazırlık: tümsek kalp atışı gibi kabarır; yerde sarmaşık biçimli çatlaklar dışa doğru uzar.
  12–34 çıkış: dört dal kamçı gibi fırlayıp ucundan kıvrılır; gonca dönerek yükselir; 25'te "pof" — yapraklar açılır,
        pembe-krem spor bulutu ve süzülen spor taneleri; dallar kıvrılıp toprağa geri çekilir.
  36–48 yayılma: yerde sürünen bir dal komşu hücreye uzanır (yol boyu yaprakçıklar), ucunda küçük filiz açılır.
  52–60 vuruş: turkuaz özsu sıçraması (yuvarlak splat + damlalar) ve kopan yaprak parçaları; filiz darbeden uzağa eğilir.
  64–84 kırılma: filiz solar ve devrilir, yapraklar dökülür, spor bulutu; dal ve küçük filiz de solar.
Diken çalışmasının toon yardımcıları (malzemeler, tarla, kart) aynı süreçte kullanılır; Random yalnız sabit tohumla.
"""
import bpy, bmesh, math, random
from pathlib import Path
from mathutils import Vector, Euler

HERE = Path(__file__).resolve().parent
THORN = HERE.parent / 'ThornBurst' / 'build_thorn_burst.py'
_src = THORN.read_text(encoding='utf-8')
exec(compile(_src[:_src.index('# ---------------------------------------------------------------- nesneler')], str(THORN), 'exec'))

ROOT = HERE.parents[2]
OUT = ROOT / 'Docs' / 'SproutBurst'
FRAMES = OUT / 'frames'; FRAMES.mkdir(parents=True, exist_ok=True)
GAME = OUT / 'game_frames'; GAME.mkdir(parents=True, exist_ok=True)
N = 84
s.frame_start, s.frame_end = 1, N

TEAL, TEAL_D, TEAL_DD = srgb('#3ED6C8'), srgb('#1E9A90'), srgb('#13645E')
PINK, PINK_D, PINK_L = srgb('#F25C9A'), srgb('#B8346E'), srgb('#FFC2DC')
SAP, SAP_L = srgb('#5FE3B0'), srgb('#C8FFE6')
LEAF_L = srgb('#7FEBDD')

# ---------------------------------------------------------------- yardımcılar
def tube_segment(name, a, b, ra, rb, light, dark, parent=None, sides=6, ink=.018):
    """a → b konik parça; nesne kökeni a (ölçek a'dan büyür)."""
    t = (b - a).normalized()
    ref = Vector((0, 0, 1)) if abs(t.z) < .95 else Vector((1, 0, 0))
    u = t.cross(ref).normalized(); w = t.cross(u).normalized()
    bm = bmesh.new()
    ring_a = [bm.verts.new((u * math.cos(k * 2 * math.pi / sides) + w * math.sin(k * 2 * math.pi / sides)) * ra) for k in range(sides)]
    ring_b = [bm.verts.new(b - a + (u * math.cos(k * 2 * math.pi / sides) + w * math.sin(k * 2 * math.pi / sides)) * rb) for k in range(sides)]
    for k in range(sides):
        j = (k + 1) % sides
        bm.faces.new((ring_a[k], ring_a[j], ring_b[j], ring_b[k]))
    bm.faces.new(list(reversed(ring_a))); bm.faces.new(ring_b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm, LIT, ink=ink)
    o.location = a
    if parent: o.parent = parent
    paint(o, lambda p: light if p.normal.z > .15 else dark)
    return o


def leaf_mesh(name, length, width, cup, light, dark, parent=None, ink=.014):
    """Yerel +Y boyunca uzanan sivri uçlu yaprak, hafif çanak (+Z). Orta damar + iki kenar şeridi."""
    bm = bmesh.new()
    n = 8
    mid = [bm.verts.new(Vector((0, length * i / n, cup * .5 * math.sin(math.pi * i / n)))) for i in range(n + 1)]
    def edge(sign):
        out = [mid[0]]
        for i in range(1, n):
            tt = i / n
            wdt = width * math.sin(math.pi * tt) ** .8 * (1 - .25 * tt)
            out.append(bm.verts.new(Vector((sign * wdt, length * tt, cup * math.sin(math.pi * tt)))))
        out.append(mid[n])
        return out
    for e, flip in ((edge(-1), False), (edge(1), True)):
        for i in range(n):
            quad = [mid[i], e[i], e[i + 1], mid[i + 1]]
            uniq = []
            for v in quad:
                if v not in uniq: uniq.append(v)
            if len(uniq) >= 3: bm.faces.new(list(reversed(uniq)) if flip else uniq)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm, LIT, ink=ink)
    if parent: o.parent = parent
    paint(o, lambda p: light if abs(p.normal.z) > .3 else dark)
    return o


def round_outline(radius, bumps, depth, seed, squash=1.0, count=40):
    r = random.Random(seed); ph = r.uniform(0, 6.28)
    def f(scale):
        return [(math.cos(2 * math.pi * i / count) * radius * scale * (1 + depth * math.sin(bumps * 2 * math.pi * i / count + ph)),
                 math.sin(2 * math.pi * i / count) * radius * scale * squash * (1 + depth * math.sin(bumps * 2 * math.pi * i / count + ph))) for i in range(count)]
    return f


def spore_puff(name, seed, size):
    """Yumuşak spor bulutu: az kabarcıklı yuvarlak; mürekkep, pembe gölge, krem-pembe üst."""
    return card(name, round_outline(size, 5, .1, seed, .85), [(1.12, INK, 0.0), (1.0, PINK_L, .02), (.7, CREAM, .04)])


def dot(name, size, color):
    """Polen kıvılcımı: dört köşeli küçük elmas, ince mürekkep (boncuk gibi okunmasın)."""
    def diamond(scale):
        return [(0, size * 1.5 * scale), (size * .6 * scale, 0), (0, -size * 1.5 * scale), (-size * .6 * scale, 0)]
    return card(name, diamond, [(1.3, INK, 0.0), (1.0, color, .02)])


def splat_outline(seed):
    r = random.Random(seed)
    lobes = [(r.uniform(0, 6.28), r.uniform(.25, .55), r.uniform(.25, .4)) for _ in range(6)]
    def f(scale):
        pts = []
        for i in range(48):
            a = 2 * math.pi * i / 48
            rr = .32
            for ang, length, wdt in lobes:
                d = abs((a - ang + math.pi) % (2 * math.pi) - math.pi)
                rr += length * .5 * max(0.0, 1 - d / wdt)
            pts.append((math.cos(a) * rr * scale, math.sin(a) * rr * scale))
        return pts
    return f


def ribbon(name, pts, width, z, color, parent=None):
    bm = bmesh.new()
    left, right = [], []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]); t.z = 0; t.normalize()
        nrm = Vector((-t.y, t.x, 0))
        wdt = width * (1 - .7 * i / (len(pts) - 1))
        left.append(bm.verts.new(Vector((p.x, p.y, z)) + nrm * wdt)); right.append(bm.verts.new(Vector((p.x, p.y, z)) - nrm * wdt))
    for i in range(len(pts) - 1): bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
    o = obj_from_bm(name, bm, UNLIT); paint(o, lambda p: color)
    if parent: o.parent = parent
    return o


# ---------------------------------------------------------------- yerleşim
NEIGHBOR = max([Vector((2, 0, 0)), Vector((-2, 0, 0)), Vector((0, 2, 0)), Vector((0, -2, 0))], key=lambda v: v.dot(SR))
OUTWARD = NEIGHBOR.normalized()

MOUND = mound()

# Sarmaşık çatlaklar (yerde, tümseğin çevresinde): 5 kol, her biri dalgalı; kol başına iki katman (mürekkep + koyu turkuaz).
r0 = random.Random(41)
CRACKS = []
for k in range(5):
    a0 = k * 2 * math.pi / 5 + r0.uniform(-.3, .3)
    pts = []
    for i in range(9):
        t = i / 8
        a = a0 + .55 * math.sin(t * 5 + k)
        pts.append(GROUND + Vector((math.cos(a), math.sin(a), 0)) * (.42 + .58 * t) + Vector((0, 0, .01)))   # tümseğin dışında görünür
    root = empty(f'Çatlak kol {k}', GROUND)
    ribbon(f'Çatlak {k} m', [p - GROUND for p in pts], .075, .005, INK, root)
    ribbon(f'Çatlak {k} t', [p - GROUND for p in pts], .042, .008, TEAL, root)
    CRACKS.append(root)


def tendril_points(dir2, length, curl, n=9):
    up = Vector((0, 0, 1)); pts = [Vector()]
    for i in range(n):
        s_ = (i + .5) / n
        th = s_ * s_ * curl
        pts.append(pts[-1] + (dir2 * math.sin(th) + up * math.cos(th)) * length / n)
    return pts


TENDRILS = []
for k in range(4):
    a = k * math.pi / 2 + .5
    d = Vector((math.cos(a), math.sin(a), 0))
    pts = tendril_points(d, r0.uniform(1.25, 1.5), r0.uniform(3.1, 3.6))
    base = GROUND + d * .12 - Vector((0, 0, .02))
    segs = []
    for i in range(len(pts) - 1):
        ra = .1 * (1 - .8 * i / (len(pts) - 1)); rb = .1 * (1 - .8 * (i + 1) / (len(pts) - 1))
        segs.append(tube_segment(f'Dal {k}.{i}', base + pts[i], base + pts[i + 1], ra, rb, TEAL, TEAL_D))
    TENDRILS.append((segs, base, d))

# Ana filiz (oyundaki SproutMain'in yer tutucusu: turkuaz sap, pembe gonca, dört yaprak).
MAIN = empty('Ana filiz', GROUND)
STEM = tube_segment('Sap', Vector(), Vector((0, 0, .85)), .1, .075, TEAL, TEAL_D, parent=MAIN)
STEM.location = Vector()
bmh = bmesh.new(); bmesh.ops.create_icosphere(bmh, subdivisions=2, radius=.22)
for v in bmh.verts: v.co.z *= 1.35
HEAD = obj_from_bm('Gonca', bmh, LIT, ink=.025, parent=MAIN); HEAD.location = Vector((0, 0, 1.0))
paint(HEAD, lambda p: PINK if p.normal.z > -.2 else PINK_D)
LEAVES = []
for k in range(4):
    hinge = empty(f'Yaprak menteşe {k}'); hinge.parent = MAIN; hinge.location = Vector((0, 0, .76))
    leaf_mesh(f'Yaprak {k}', .8, .27, .12, LEAF_L, TEAL_D, parent=hinge)
    LEAVES.append((hinge, k * math.pi / 2 + .4))

PUFFS = [spore_puff(f'Spor bulutu {k}', 60 + k, .17 + .03 * (k % 3)) for k in range(7)]
r1 = random.Random(9)
DOTS = [(dot(f'Spor tanesi {k}', .035 + .012 * (k % 3), PINK_L if k % 2 else CREAM),
         Vector((math.cos(k * 2.4), math.sin(k * 2.4), 0)), r1.uniform(.4, .9), r1.uniform(.8, 1.4), r1.uniform(0, 6.28)) for k in range(12)]

# Yayılma dalı: tümsekten komşu hücreye yerde sürünür (saksı kenarlarının üstünden kavis).
SIDE = Vector((-OUTWARD.y, OUTWARD.x, 0))
VINE_PTS = []
for i in range(13):
    t = i / 12
    VINE_PTS.append(GROUND + OUTWARD * (.25 + 1.75 * t) + SIDE * (.18 * math.sin(t * math.pi * 2.2)) + Vector((0, 0, .02 + .1 * math.sin(math.pi * t))))
VINE = [tube_segment(f'Sürünen dal {i}', VINE_PTS[i], VINE_PTS[i + 1], .08 * (1 - .4 * i / 12), .08 * (1 - .4 * (i + 1) / 12), TEAL, TEAL_D) for i in range(12)]
VINE_LEAVES = []
for k, i in enumerate((4, 8)):
    h = empty(f'Dal yaprağı {k}', VINE_PTS[i]); h.rotation_euler = (math.radians(-20), 0, math.atan2(SIDE.y, SIDE.x) * (1 if k else -1) - math.pi / 2)
    leaf_mesh(f'Dal yaprak {k}', .45, .15, .05, LEAF_L, TEAL_D, parent=h)
    VINE_LEAVES.append(h)
SMALL = empty('Küçük filiz', GROUND + NEIGHBOR)
tube_segment('Küçük sap', Vector(), Vector((0, 0, .38)), .06, .045, TEAL, TEAL_D, parent=SMALL).location = Vector()
for k in range(2):
    h = empty(f'Küçük yaprak menteşe {k}'); h.parent = SMALL; h.location = Vector((0, 0, .34)); h.rotation_euler = (math.radians(-15), 0, k * math.pi + .6)
    leaf_mesh(f'Küçük yaprak {k}', .3, .11, .04, LEAF_L, TEAL_D, parent=h)
SMALL_PUFFS = [spore_puff(f'Küçük pof {k}', 80 + k, .12) for k in range(4)]
SMALL.scale = (1.4, 1.4, 1.4)

# Vuruş: özsu splat + damlalar + kopan yapraklar.
SPLAT = card('Özsu', splat_outline(5), [(1.15, INK, 0.0), (1.0, SAP, .02), (.55, SAP_L, .04)])
DROPS = []
for k in range(7):
    bmd = bmesh.new(); bmesh.ops.create_icosphere(bmd, subdivisions=1, radius=.05 + .015 * (k % 3))
    o = obj_from_bm(f'Damla {k}', bmd, LIT, ink=.012); paint(o, lambda p: SAP if p.normal.z > 0 else TEAL_D)
    DROPS.append((o, Vector((math.cos(k * .9 - 1.2), math.sin(k * .9 - 1.2), 0))))
SHREDS = []
for k in range(3):
    h = empty(f'Yaprak parçası {k}'); leaf_mesh(f'Parça {k}', .22, .08, .03, LEAF_L, TEAL_D, parent=h); SHREDS.append(h)
BREAK_PUFFS = [spore_puff(f'Kırılma bulutu {k}', 100 + k, .19 + .04 * (k % 2)) for k in range(7)]

HIT_FROM = -SR   # imleç ekranın solundan
HIT_AT = GROUND + Vector((0, 0, .95))

# ---------------------------------------------------------------- kare kare
def pulse(x, w):
    return math.sin(math.pi * x / w) if 0 < x < w else 0.0


def fountain(puffs, f, start, life, center, reach, spread=70.0, rise=.35, peak=1.0):
    """Puflar merkezden küçük doğar, yukarı-dışa yelpaze gibi açılarak büyür, hızla söner (ekran düzleminde)."""
    n = len(puffs)
    for k, o in enumerate(puffs):
        u = (f - start - .4 * (k % 3)) / life
        if u < 0 or u > 1: key(o, f, loc=center, scale=HIDE); continue
        ang = math.radians(-spread + 2 * spread * k / max(1, n - 1))
        d = SUP * math.cos(ang) + SR * math.sin(ang)
        go = ease_out(u / .55)
        vary = (1.0, .68, .86, .6, .95, .72, .8)[k % 7]   # boy çeşitliliği: aynı çiçek sırası gibi okunmasın
        size = (.2 + .8 * ease_out(u / .35)) * (1 - smooth((u - .45) / .55)) * peak * vary
        key(o, f, loc=center + d * reach * go * (.75 + .25 * (k % 2)) + Vector((0, 0, rise * u)), scale=max(HIDE, size))


for f in range(1, N + 1):
    # tümsek: kalp atışı (iki kabarma); 12–15 çöker
    beat = max(pulse((f - 1) % 6, 2.5), .6 * pulse((f - 1) % 6 - 2.5, 2))
    if f < 12: key(MOUND, f, loc=GROUND, scale=(1 + .08 * beat, 1 + .08 * beat, 1 + .3 * beat + .02 * f))
    else:
        u = (f - 12) / 3
        key(MOUND, f, loc=GROUND - Vector((0, 0, .15 * min(u, 1))), scale=HIDE if u >= 1 else (1 - .4 * u, 1 - .4 * u, max(HIDE, 1 - u)))
    # çatlaklar: dışa uzar (2–11), sonra söner (26–31)
    for k, c in enumerate(CRACKS):
        grow = min(1.0, max(0.0, (f - 2 - k * .6) / 6))
        fade = 1 - smooth((f - 26) / 5)
        sc = grow * fade
        key(c, f, scale=HIDE if sc <= .01 else (sc, sc, 1))
    # dallar: 12'den itibaren parça parça kamçılanır, salınır, 28–34 uçtan geri çekilir
    for k, (segs, base, d) in enumerate(TENDRILS):
        grown = (f - 12 - k * .7) * 2.2
        retract = (f - 28 - k * .5) * 2
        sway = math.radians(8 * math.sin((f - 12) * .9 + k)) if f >= 15 else 0
        for i, seg in enumerate(segs):
            vis = grown > i and retract < len(segs) - i
            pop = 1.15 if vis and grown - i < 1.5 else 1.0
            key(seg, f, scale=HIDE if not vis else pop)
    # ana filiz: 14–24 dönerek yükselir, 21 aşar, 24 oturur
    t = f - 14
    if t < 0: key(MAIN, f, loc=GROUND - Vector((0, 0, 1.2)), scale=HIDE, rot=(0, 0, -2 * math.pi))
    else:
        rise = ease_out(t / 7)
        over = .18 * pulse(t - 5, 5)
        spin = -2 * math.pi * (1 - ease_out(t / 9))
        squash = 1 - .2 * pulse(t - 9, 3)
        hit = f - 52
        tilt = math.radians(18) * (math.exp(-hit * .35) * math.cos(hit * .9)) if 0 <= hit < 12 else 0
        brk = f - 64
        droop = math.radians(75) * smooth(brk / 9) if brk >= 0 else 0
        sink = smooth((brk - 8) / 10) if brk >= 0 else 0
        ax = HIT_FROM.cross(Vector((0, 0, 1))).normalized()
        rot = Euler((ax.x * (tilt + droop), ax.y * (tilt + droop), spin))
        sc = (1 / squash ** .5, 1 / squash ** .5, squash) if hit < 0 or hit > 3 else (1.12, 1.12, .8)
        key(MAIN, f, loc=GROUND + Vector((0, 0, -1.2 * (1 - rise) + over - .9 * sink)), scale=HIDE if sink >= .99 else sc, rot=rot)
    pre = f - 21
    swell = .85 + .2 * smooth(pre / 3) if f < 25 else 1.0
    jitter = math.radians(4 * math.sin(f * 2.3)) if 21 <= f < 25 else 0
    popf = f - 25
    hs = (1.15, 1.15, .82) if popf == 0 else (.95, .95, 1.12) if popf == 1 else (1, 1, 1)
    key(HEAD, f, scale=(swell * hs[0], swell * hs[1], swell * hs[2]), rot=(jitter, -jitter, 0))
    # yapraklar: kapalı (yukarı sarılı) → 25'te açılır, aşar; kırılmada kopar
    for k, (hinge, yaw) in enumerate(LEAVES):
        o = f - 25
        if o < 0: pitch = math.radians(74 + (3 * math.sin(f * 2.1 + k) if f >= 21 else 0))   # tomurcuk; pof öncesi titrer
        else:
            pitch = math.radians(74 - 84 * ease_out(o / 2)) if o < 2 else math.radians(-10 + 24 * smooth((o - 2) / 4))
            pitch += math.radians(3 * math.sin(o * .5 + k)) if o > 6 else 0   # açık taç hafif nefes alır
        brk = f - 64
        if brk >= 0:
            pitch = math.radians(-20 - 60 * smooth(brk / 6))
        key(hinge, f, rot=(pitch, 0, yaw), scale=HIDE if brk > 9 else 1)
    # spor bulutu "pof" (25–33) ve spor taneleri (25–40)
    center = MAIN.location + Vector((0, 0, 1.05))   # gonca (bu karede MAIN konumu yazıldı)
    fountain(PUFFS, f, 26, 7, center, .75, spread=75, rise=.3)
    for k, (dt, d, sp, up, ph) in enumerate(DOTS):
        u = (f - 26 - .5 * (k % 3)) / 14
        vis = 0 <= u <= 1
        pos = center + d * sp * ease_out(u / .4) * .8 + Vector((0, 0, .2 + up * .7 * u)) + SR * (.1 * math.sin(u * 8 + ph))
        key(dt, f, loc=pos, rot=(0, 0, 0), scale=HIDE if not vis else max(HIDE, 1 - smooth((u - .55) / .45)))
    # yayılma dalı: 36–44 parça parça, yaprakçıklar 39/42, küçük filiz 45–48; kırılmada solar
    wilt = smooth((f - 68) / 10)
    for i, seg in enumerate(VINE):
        vis = f >= 36 + i * .7
        pop = 1.2 if vis and f - (36 + i * .7) < 1 else 1.0
        key(seg, f, scale=HIDE if not vis or wilt >= .99 else (pop * (1 - wilt * .6), pop * (1 - wilt * .6), pop * (1 - wilt)))
    for k, h in enumerate(VINE_LEAVES):
        u = f - (39 + 3 * k)
        key(h, f, scale=HIDE if u < 0 or wilt >= .99 else (1.25 if u < 1 else 1) * (1 - wilt))
    u = f - 45
    sm = 1.4 * (0 if u < 0 else (min(1.25, .5 + .5 * u) if u < 2 else 1 + .08 * pulse(u - 2, 3)))
    key(SMALL, f, scale=HIDE if sm <= 0 or wilt >= .99 else (sm, sm, sm * (1 - wilt)))
    fountain(SMALL_PUFFS, f, 46, 6, GROUND + NEIGHBOR + Vector((0, 0, .55)), .4, spread=65, rise=.2)
    # vuruş: splat (52–55), damlalar (52–60), yaprak parçaları (52–62)
    h = f - 52
    key(SPLAT, f, loc=HIT_AT - HIT_FROM * .15, scale=HIDE if h < 0 or h > 3 else {0: .55, 1: .8, 2: .7, 3: .4}[h])
    for k, (o, d) in enumerate(DROPS):
        u = h / 8
        vis = 0 <= h <= 8
        dd = (d + (-HIT_FROM) * 1.2).normalized()
        pos = HIT_AT + dd * (.9 * u) + Vector((0, 0, .5 * u - .9 * u * u))
        key(o, f, loc=pos, scale=HIDE if not vis else max(HIDE, 1 - h / 9))
    for k, sh in enumerate(SHREDS):
        u = h / 10
        vis = 0 <= h <= 10
        dd = (-HIT_FROM + Vector((0, 0, 1)) * (.6 + .3 * k) + SIDE * (k - 1) * .5).normalized()
        pos = HIT_AT + dd * .9 * ease_out(u) + Vector((0, 0, -.6 * u * u))
        key(sh, f, loc=pos, rot=(u * 6 + k, u * 4, u * 9 + k), scale=HIDE if not vis else 1 - .5 * u)
    # kırılma: spor bulutu (64–74)
    fountain(BREAK_PUFFS, f, 67, 8, GROUND + Vector((0, 0, .7)), .8, spread=85, rise=.35)

# Dalların salınımı: dal parçalarını tabandan yatırarak (her dal kendi kökünde döner).
for k, (segs, base, d) in enumerate(TENDRILS):
    pivot = empty(f'Dal kökü {k}', base)
    for seg in segs:
        seg.parent = pivot; seg.location = seg.location - base
    axis = Vector((-d.y, d.x, 0))
    for f in range(12, 36):
        ang = math.radians(10 * math.sin((f - 12) * .8 + k) * (1 - smooth((f - 26) / 8)))
        key(pivot, f, rot=(axis.x * ang, axis.y * ang, 0))

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
FOCUS = GROUND + NEIGHBOR * .35 + Vector((0, 0, .75))
cam.location = FOCUS + cam.rotation_euler.to_matrix() @ Vector((0, 0, 30))
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 5.4; cam.data.clip_end = 100
s.camera = cam
s.render.resolution_x, s.render.resolution_y, s.render.resolution_percentage = 960, 600, 100
s.render.image_settings.file_format = 'PNG'
s.render.filepath = str(FRAMES / 'f_')
bpy.ops.render.render(animation=True)
cam.data.ortho_scale = 22 * 1920 / 1080
s.render.resolution_x, s.render.resolution_y = 1920, 1080
s.render.filepath = str(GAME / 'g_')
bpy.ops.render.render(animation=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'SproutBurst.blend'))
print('SPROUT BURST DONE')
