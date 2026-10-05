"""Bölüm 3.7.8 (P8) ölçüm tabloları. Girdi: BalanceRunMeasurement çıktıları (BalanceRuns_<set>.csv; virgülle birden çok dosya).
Çıktı Markdown'dur. Hedef tabloları oynanışı değiştirmediği için aday tablolar hedefsiz oynanmış run'ların skorundan hesapla
değerlendirilir (kural: Docs/Bolum3-7-8/OlcumOncesi-Tanimlar.md).

  python analyze_alpha.py curve    <runs.csv[,…]>                     politika başına R10/20/30/40/50 ölçüleri
  python analyze_alpha.py periods  <runs.csv[,…]> [tablo]            dönem başına skor, pay ve sonuç (tablo: params | kural | eski)
  python analyze_alpha.py targets  <runs.csv[,…]> [kota payı boss payı]  kurala göre hedef tablosu (dayanak: Uyumsuz; varsayılan 0.65 0.60) ve her politikanın sonucu
  python analyze_alpha.py window   <runs.csv[,…]> [tablo]            üstünlük penceresi (run başına ve politika başına)
  python analyze_alpha.py lab      <runs.csv[,…]>                     zincir laboratuvarı (A ↔ B kolları, eşleştirilmiş)
  python analyze_alpha.py chain    <runs.csv[,…]>                     gerçek run'larda zincir hunisi ve katkısı
  python analyze_alpha.py pair     <runs.csv[,…]> <etiket A> <etiket B> [politika]   aynı seed'li iki kol (ör. zinciri alan ↔ almayan)
  python analyze_alpha.py duration <eski.csv[,…]> <yeni.csv[,…]>      süre değişiminin etkisi (aynı politika ve seed)
  python analyze_alpha.py runs     <runs.csv[,…]> [tablo]            run listesi (elenenler dahil)
  python analyze_alpha.py hp       <runs.csv[,…]>                     can eğrisi adayları: öldürme vuruş sayısı ve hız (kol = aday)
  python analyze_alpha.py load     <final.csv[,…]>                    level, seçim sayısı ve menü yükü senaryoları
  python analyze_alpha.py openings <runs.csv[,…]> [tablo]            ilk üç boss: açılış başına dönem ve boss round'u skoru
  python analyze_alpha.py growth   <runs.csv[,…]>                     build'in kendi gelişimi: hasat / sn ve skor / sn, R20 → R30 → R40 → R50
  python analyze_alpha.py chainpick <runs.csv[,…]>                    zincir kolları: "mevcut" kola göre eşleştirilmiş oran (karar kuralı)
  python analyze_alpha.py perf     <runs.csv[,…]>                     kare süresi, GC, zincir kuyruğu, atlanan görseller (batch)
  python analyze_alpha.py clock    <final.csv[,…]>                    süre sayaçları (aktif / gerçek aktif) ve bot menü kareleri
  python analyze_alpha.py xp       <runs.csv[,…]> <final.csv[,…]> <etiket XP'siz> <etiket XP'li>   XP yatırımı alt karşılaştırması

Bot ölçümüdür; insan davranışı ve his hakkında sonuç vermez.
"""
import csv
import io
import math
import os
import statistics as st
import sys
from collections import OrderedDict, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import alpha_denge_v1_params as P

DATES = [3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50]
PERIODS = [(1 if i == 0 else DATES[i - 1] + 1, DATES[i]) for i in range(15)]
CHECKPOINTS = (10, 20, 30, 40, 50)
WINDOW_PERIODS = [i for i, (a, b) in enumerate(PERIODS) if a >= 27 and b <= 46]   # 27–30 … 44–46
WINDOW_SHARE = 2.0
OLD_QUOTA = [27, 40, 88, 90, 104, 176, 180, 220, 400, 720, 1040, 2240, 2700, 3000, 4800]
OLD_BOSS = [5, 10, 16, 24, 33, 45, 57, 72, 100, 172, 276, 500, 680, 860, 1100]
QUOTA_SHARE_OF_ANCHOR, BOSS_SHARE_OF_ANCHOR = 0.65, 0.60
ANCHOR_POLICY = 'Uyumsuz'   # Docs/Bolum3-7-8/OlcumOncesi-Tanimlar.md, Ek 2 (ilk yazimda Dengeli idi)
# İlk üç boss tavanı (Ek 3): makul açılışların en düşük skoru (açılış seti, k378acilis) × 0,80.
OPENING_MIN_QUOTA, OPENING_MIN_BOSS, OPENING_CAP = [48, 92, 232], [12, 33, 48], 0.80


def seconds(round_, table=None):
    value = None
    for start, sec in (table or P.ROUND_DURATIONS):
        if start <= round_: value = sec
    return value


def period_seconds(i, table=None):
    a, b = PERIODS[i]
    return sum(seconds(r, table) for r in range(a, b + 1))


def read(paths):
    rows = []
    for path in paths.split(','):
        for row in csv.DictReader(io.open(path, encoding='utf-8')):
            row['_file'] = os.path.basename(path)
            rows.append(row)
    return rows


def num(v, default=float('nan')):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def med(values):
    values = [v for v in values if v is not None and not (isinstance(v, float) and math.isnan(v))]
    return st.median(values) if values else float('nan')


def f(v, digits=2):
    if v is None or (isinstance(v, float) and math.isnan(v)): return '–'
    if isinstance(v, float) and math.isinf(v): return '∞'
    s = ('%.' + str(digits) + 'f') % v
    return s.replace('.', ',')


def n0(v):
    if v is None or (isinstance(v, float) and math.isnan(v)): return '–'
    return ('{:,.0f}'.format(v)).replace(',', ' ')


class Run:
    def __init__(self, key):
        self.key = key            # (dosya, run no)
        self.policy = self.label = self.farmer = self.scythe = ''
        self.seed = 0
        self.rounds = {}          # round → satır
        self.outcome = ''
        self.end_round = 0

    @property
    def is_lab(self): return self.label.startswith('LAB')

    def score(self, r): return num(self.rounds[r]['score']) if r in self.rounds else None

    def period_score(self, i):
        a, b = PERIODS[i]
        if any(r not in self.rounds for r in range(a, b + 1)): return None
        return sum(num(self.rounds[r]['score']) for r in range(a, b + 1))

    def boss_score(self, i):
        return self.score(PERIODS[i][1])


def runs_of(rows, labs=False):
    """Gerçek run'lar (labs=False) ya da laboratuvar satırları. Anahtar: dosya + run no + etiket."""
    out = OrderedDict()
    for row in rows:
        is_lab = row['label'].startswith('LAB')
        if is_lab != labs: continue
        key = (row['_file'], row['run'], row['label'])
        run = out.get(key)
        if run is None:
            run = out[key] = Run(key)
            run.policy, run.label, run.seed, run.farmer, run.scythe = row['policy'], row['label'], int(row['seed']), row['farmer'], row['scythe']
        if row.get('kills') in (None, ''):
            run.outcome, run.end_round = row['outcome'], int(row['round'])
        elif row.get('outcome') == 'kesildi':
            pass
        else:
            run.rounds[int(row['round'])] = row
    return list(out.values())


def groups(runs):
    g = OrderedDict()
    for run in runs: g.setdefault((run.policy, run.label), []).append(run)
    return g


# ---------------------------------------------------------------- hedef tabloları
def nice(value):
    """HarvestQuota.Nice ile aynı: 100'ün altında 5'in katı, üstünde iki anlamlı basamak."""
    if value <= 5: return 5
    step = 5 if value < 100 else 10 ** (math.floor(math.log10(value)) - 1)
    return int(math.floor(value / step + 0.5) * step)


def rule_tables(runs):
    """Kural: kota = dayanak politikanın (Uyumsuz) dönem medyan skoru × 0,65; boss = boss round'u medyan skoru × 0,60.
    Saniye başına gereksinim log uzayında 1-2-1 süzgeçle yumuşatılır (uçlar aynen), sonra azalmayan hâle getirilir."""
    average = [r for r in runs if r.policy == ANCHOR_POLICY]
    if not average: raise SystemExit('Kural için %s run\'ı gerekli.' % ANCHOR_POLICY)

    def smooth(raw):
        logs = [math.log(max(v, 1e-9)) for v in raw]
        out = list(logs)
        for i in range(1, len(logs) - 1): out[i] = 0.25 * logs[i - 1] + 0.5 * logs[i] + 0.25 * logs[i + 1]
        values = [math.exp(v) for v in out]
        for i in range(1, len(values)): values[i] = max(values[i], values[i - 1])
        return values

    quota_rate = smooth([QUOTA_SHARE_OF_ANCHOR * med([r.period_score(i) for r in average]) / period_seconds(i) for i in range(15)])
    boss_rate = smooth([BOSS_SHARE_OF_ANCHOR * med([r.boss_score(i) for r in average]) / seconds(PERIODS[i][1]) for i in range(15)])
    quota = [quota_rate[i] * period_seconds(i) for i in range(15)]
    boss = [boss_rate[i] * seconds(PERIODS[i][1]) for i in range(15)]
    for i in range(3):
        quota[i] = min(quota[i], OPENING_CAP * OPENING_MIN_QUOTA[i])
        boss[i] = min(boss[i], OPENING_CAP * OPENING_MIN_BOSS[i])
    quota, boss = [nice(v) for v in quota], [nice(v) for v in boss]
    for i in range(3): assert quota[i] <= OPENING_MIN_QUOTA[i] and boss[i] <= OPENING_MIN_BOSS[i], 'yuvarlama en düşük açılış skorunu aştı'
    return quota, boss


def tables(name, runs):
    if name == 'eski': return OLD_QUOTA, OLD_BOSS
    if name == 'kural': return rule_tables(runs)
    return list(P.QUOTA_TARGETS), list(P.BOSS_TARGETS)


def evaluate(run, quota, boss):
    """Hedef tablosuyla run'ın sonucu: (ilk elendiği dönem ya da None, neden, kota payları, boss payları)."""
    q, b, failed, why = [], [], None, ''
    for i in range(15):
        ps, bs = run.period_score(i), run.boss_score(i)
        q.append(ps / quota[i] if ps is not None else None)
        b.append(bs / boss[i] if bs is not None and boss[i] > 0 else None)
        if failed is None and ps is not None:
            miss_q, miss_b = ps < quota[i], boss[i] > 0 and bs < boss[i]
            if miss_q or miss_b: failed, why = i, 'KOTA+BOSS' if miss_q and miss_b else 'KOTA' if miss_q else 'BOSS'
    return failed, why, q, b


def window(q, b, failed):
    """Art arda en az iki pencere döneminde iki pay da ≥ 2,0. Dönüş: (ilk dönem dizini ya da None, en uzun dizi)."""
    first, best, streak = None, 0, 0
    for i in WINDOW_PERIODS:
        ok = (failed is None or i < failed) and q[i] is not None and b[i] is not None and q[i] >= WINDOW_SHARE and b[i] >= WINDOW_SHARE
        streak = streak + 1 if ok else 0
        if streak >= 2 and first is None: first = i - 1
        best = max(best, streak)
    return first, best


# ---------------------------------------------------------------- tablolar
def col(run, r, name):
    return num(run.rounds[r][name]) if r in run.rounds and run.rounds[r].get(name, '') != '' else None


def share(run, r, part, whole='kills'):
    a, b = col(run, r, part), col(run, r, whole)
    return a / b if a is not None and b else None


def curve(rows):
    runs = runs_of(rows)
    print('Medyanlar (seed\'ler üzerinden). Hasat / sn ve skor / sn o round\'un süresine bölünmüştür. ÖV: doğrudan vuruşla ölen bitkinin aldığı '
          'doğrudan vuruş sayısı (Common / Rare / Epic). Davranış ve zincir: hasadın o kaynakla yapılan payı.\n')
    for (policy, label), group in groups(runs).items():
        print('**%s** · %s · %d run\n' % (policy, label, len(group)))
        print('| Round | Süre | Run | Hasat / sn | Skor / sn | Hasat / vuruş | Vuruş / hedef | ÖV C / R / E | Davranış | Zincir | Doluluk | Bekleme sn | Hasar × çarpan | Aralık sn | Yarıçap | Level |')
        print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
        for r in CHECKPOINTS:
            alive = [run for run in group if r in run.rounds]
            if not alive: continue
            m = lambda name: med([col(run, r, name) for run in alive])
            beh = med([1 - (col(run, r, 'direct') or 0) / col(run, r, 'kills') if col(run, r, 'kills') else None for run in alive])
            chain = med([share(run, r, 'chainKills') for run in alive])
            per_attack = med([col(run, r, 'kills') / col(run, r, 'attacks') if col(run, r, 'attacks') else None for run in alive])
            dmg = med([(col(run, r, 'damage') or 0) * (col(run, r, 'directMult') or 1) for run in alive])
            print('| R%d | %d sn | %d | %s | %s | %s | %s | %s / %s / %s | %% %s | %% %s | %s | %s | %s | %s | %s | %s |' % (
                r, seconds(r), len(alive), f(m('killsPerSec')), f(m('scorePerSec')), f(per_attack), f(m('hitsPerAttack')),
                f(m('htkCommon')), f(m('htkRare')), f(m('htkEpic')), f(100 * beh, 0), f(100 * chain, 0), f(m('fill')), f(m('plantWait'), 1),
                n0(dmg), f(m('interval')), f(m('radius')), n0(m('level'))))
        print()


def periods(rows, name='params'):
    runs = runs_of(rows)
    quota, boss = tables(name, runs)
    print('Hedef tablosu: **%s** · kota %s · boss %s\n' % (name, quota, boss))
    print('Dönem skoru ve pay medyanları (o döneme ulaşan run\'lar; elenme bu tabloya göre hesaplanır).\n')
    for (policy, label), group in groups(runs).items():
        results = [evaluate(run, quota, boss) for run in group]
        print('**%s** · %s · %d run\n' % (policy, label, len(group)))
        print('| Dönem | Aktif sn | Kota | Gereksinim / sn | Run | Skor (medyan) | Skor / sn | Kota payı (en düşük – medyan – en yüksek) | Boss hedefi | Boss payı (medyan) | Bu dönemde elenen |')
        print('|---|---|---|---|---|---|---|---|---|---|---|')
        for i, (a, b) in enumerate(PERIODS):
            alive = [(run, res) for run, res in zip(group, results) if (res[0] is None or res[0] >= i) and run.period_score(i) is not None]
            if not alive: continue
            scores = [run.period_score(i) for run, _ in alive]
            shares = [res[2][i] for _, res in alive]
            bshare = med([res[3][i] for _, res in alive])
            out = sum(1 for _, res in alive if res[0] == i)
            print('| R%d–%d | %d | %s | %s | %d | %s | %s | %s – %s – %s | %s | %s | %s |' % (
                a, b, period_seconds(i), n0(quota[i]), f(quota[i] / period_seconds(i)), len(alive), n0(med(scores)), f(med(scores) / period_seconds(i)),
                f(min(shares)), f(med(shares)), f(max(shares)), n0(boss[i]), f(bshare), out if out else ''))
        print()


def targets(rows):
    runs = runs_of(rows)
    quota, boss = rule_tables(runs)
    print('Kural: kota = %s\'nin dönem medyan skoru × %s, boss hedefi = %s\'nin boss round\'u medyan skoru × %s; saniye başına gereksinim log uzayında 1-2-1 '
          'süzgeçle yumuşatılır, azalmayan hâle getirilir, okunur sayıya yuvarlanır.\n' % (ANCHOR_POLICY, f(QUOTA_SHARE_OF_ANCHOR), ANCHOR_POLICY, f(BOSS_SHARE_OF_ANCHOR)))
    print('| Dönem | Aktif sn | Eski kota | Kural kota | Gereksinim / sn | Eski boss | Kural boss |')
    print('|---|---|---|---|---|---|---|')
    for i, (a, b) in enumerate(PERIODS):
        print('| R%d–%d | %d | %s | %s | %s | %s | %s |' % (a, b, period_seconds(i), n0(OLD_QUOTA[i]), n0(quota[i]), f(quota[i] / period_seconds(i)), n0(OLD_BOSS[i]), n0(boss[i])))
    print('\nQUOTA_TARGETS = %s\nBOSS_TARGETS = %s\n' % (quota, boss))
    summary(runs, quota, boss)


def summary(runs, quota, boss):
    print('| Politika | Kol | Run | Kazanan | Elenen (dönem · neden) | Pencere olan run | İlk pencere dönemi (medyan) | R27–46 kota payı medyanı | R47–50 kota payı |')
    print('|---|---|---|---|---|---|---|---|---|')
    for (policy, label), group in groups(runs).items():
        wins, fails, windows, firsts, mids, lasts = 0, [], 0, [], [], []
        for run in group:
            failed, why, q, b = evaluate(run, quota, boss)
            complete = run.period_score(14) is not None
            if failed is None and complete: wins += 1
            elif failed is not None: fails.append('R%d %s' % (PERIODS[failed][1], why))
            else: fails.append('R%d %s' % (run.end_round, run.outcome or 'bitmedi'))
            first, _ = window(q, b, failed)
            if first is not None: windows += 1; firsts.append(first)
            mids += [q[i] for i in WINDOW_PERIODS if q[i] is not None and (failed is None or i < failed)]
            if q[14] is not None and failed is None: lasts.append(q[14])
        first_text = 'R%d–%d' % PERIODS[int(med(firsts))] if firsts else '–'
        print('| %s | %s | %d | %d | %s | %d | %s | %s | %s |' % (policy, label, len(group), wins, ', '.join(fails) or '–', windows, first_text, f(med(mids)), f(med(lasts))))
    print()


def window_report(rows, name='params'):
    runs = runs_of(rows)
    quota, boss = tables(name, runs)
    print('Hedef tablosu: **%s**. Pencere: R27–46 arasındaki altı dönemden art arda en az ikisinde kota payı ≥ %s ve boss payı ≥ %s.\n' % (name, f(WINDOW_SHARE, 1), f(WINDOW_SHARE, 1)))
    summary(runs, quota, boss)
    print('| Politika | Kol | Seed | Sonuç | ' + ' | '.join('R%d–%d' % PERIODS[i] for i in WINDOW_PERIODS) + ' | R47–50 | Pencere |')
    print('|---|---|---|---|' + '---|' * (len(WINDOW_PERIODS) + 2))
    for run in runs:
        failed, why, q, b = evaluate(run, quota, boss)
        first, best = window(q, b, failed)
        cells = []
        for i in WINDOW_PERIODS + [14]:
            if q[i] is None or (failed is not None and i > failed): cells.append('·')
            else: cells.append('%s / %s' % (f(q[i], 1), f(b[i], 1)))
        result = 'kazandı' if failed is None and run.period_score(14) is not None else ('R%d %s' % (PERIODS[failed][1], why) if failed is not None else 'R%d %s' % (run.end_round, run.outcome))
        print('| %s | %s | %d | %s | %s | %s |' % (run.policy, run.label, run.seed, result, ' | '.join(cells),
                                                    ('R%d\'ten, %d dönem' % (PERIODS[first][0], best)) if first is not None else '–'))
    print('\nHücre: kota payı / boss payı.')


def run_list(rows, name='params'):
    runs = runs_of(rows)
    quota, boss = tables(name, runs)
    print('| # | Politika | Kol | Başlangıç | Seed | Sonuç | Toplam skor | Level | R10 | R20 | R30 | R40 | R50 skor / sn |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for n, run in enumerate(runs, 1):
        failed, why, q, b = evaluate(run, quota, boss)
        last = PERIODS[failed][1] if failed is not None else max(run.rounds) if run.rounds else 0
        total = sum(num(run.rounds[r]['score']) for r in run.rounds if r <= last)
        result = 'KAZANDI' if failed is None and run.period_score(14) is not None else ('%s @R%d' % (why, PERIODS[failed][1]) if failed is not None else '%s @R%d' % (run.outcome, run.end_round))
        cells = [f(col(run, r, 'scorePerSec')) if r in run.rounds and r <= last else '·' for r in CHECKPOINTS]
        level = col(run, last, 'level') if last in run.rounds else None
        print('| %d | %s | %s | %s + %s | %d | %s | %s | %s | %s |' % (n, run.policy, run.label, run.farmer, run.scythe, run.seed, result, n0(total), n0(level), ' | '.join(cells)))


# ---------------------------------------------------------------- zincir
LAB_COLS = ('kills', 'direct', 'normalBehaviorKills', 'chainKills', 'echoKills', 'score', 'hitsPerAttack', 'fill', 'plantWait', 'capacity')


def lab(rows):
    labs = runs_of(rows, labs=True)
    # anahtar: (politika, seed, round, tekrar) → {kol: satır}
    table = defaultdict(dict)
    for run in labs:
        text = run.label            # "LAB R30 A ödülsüz #1" / "LAB R30 B +zincir_hasat [mevcut] #1"
        round_ = int(text.split()[1][1:])
        repeat = text.rsplit('#', 1)[1]
        arm = 'A' if ' A ' in text else (text[text.index('[') + 1:text.index(']')] if '[' in text else 'B')
        row = run.rounds.get(round_)
        if row is not None: table[(run.policy, run.seed, round_, repeat)][arm] = row
    arms = []
    for cells in table.values():
        for arm in cells:
            if arm != 'A' and arm not in arms: arms.append(arm)
    by_round = sorted({k[2] for k in table})
    print('Laboratuvar: aynı kayıt (tarla, statlar, ödüller), aynı zar akışı; A = zincir yok, B = + Zincir Hasat. Oranlar eşleştirilmiş (aynı seed ve tekrar) B ÷ A; '
          'medyan ve en düşük – en yüksek. Normal run değildir.\n')
    for round_ in by_round:
        pairs = {k: v for k, v in table.items() if k[2] == round_ and 'A' in v}
        print('**R%d** · %d eşleşme (%d seed × tekrar)\n' % (round_, len(pairs), len({k[1] for k in pairs})))
        a = lambda name: med([num(v['A'][name]) for v in pairs.values()])
        print('A kolu medyanları: hasat %s (doğrudan %s · normal davranış %s · artçı %s) · vuruş başına hedef %s · doluluk %s · bekleme %s sn · üretim kapasitesi %s\n' % (
            n0(a('kills')), n0(a('direct')), n0(a('normalBehaviorKills')), n0(a('echoKills')), f(a('hitsPerAttack')), f(a('fill')), f(a('plantWait'), 1), n0(a('capacity'))))
        print('| Kol | Toplam hasat B ÷ A | Artış olan eşleşme | Skor B ÷ A | Doğrudan hasat B − A | Normal davranış hasadı B − A | Zincir hasadı (nesil 1 + 2) | Zincirin net payı | Doluluk A → B | Bekleme A → B sn |')
        print('|---|---|---|---|---|---|---|---|---|---|')
        for arm in arms:
            ps = [v for v in pairs.values() if arm in v]
            if not ps: continue
            ratio = [num(v[arm]['kills']) / num(v['A']['kills']) for v in ps]
            score = [num(v[arm]['score']) / num(v['A']['score']) for v in ps if num(v['A']['score']) > 0]
            d = lambda name: med([num(v[arm][name]) - num(v['A'][name]) for v in ps])
            chain = [num(v[arm]['chainKills1']) + num(v[arm]['chainKills2']) for v in ps]
            net = [(num(v[arm]['kills']) - num(v['A']['kills'])) / c if c else None for v, c in zip(ps, chain)]
            print('| %s | ×%s (%s – %s) | %d / %d | ×%s | %s | %s | %s | %% %s | %s → %s | %s → %s |' % (
                arm, f(med(ratio)), f(min(ratio)), f(max(ratio)), sum(1 for r in ratio if r > 1), len(ratio), f(med(score)), n0(d('direct')), n0(d('normalBehaviorKills')),
                n0(med(chain)), f(100 * med(net), 0), f(a('fill')), f(med([num(v[arm]['fill']) for v in ps])), f(a('plantWait'), 1), f(med([num(v[arm]['plantWait']) for v in ps]), 1)))
        print()
        funnel([v[arm] for arm in arms for v in pairs.values() if arm in v], arms, pairs)


def funnel(_, arms, pairs):
    print('| Kol | Nesil 1: deneme → tetik | Nesil 2: deneme → tetik | Tekrar engeli | Nesil sınırı | Kök bütçesi | Geçersiz kaynak | Round sonu | Artçı (zincir dışı) | Havuz bekleyen iş · kare | Kuyruk gecikmesi (kare / iş) | Zincir vuruşu | Yeni hücreye | Vuruş başına hasat | Öldürmeden önceki can | Sıradaki saldırı erişirdi | …ve öldürürdü |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for arm in arms:
        rows = [v[arm] for v in pairs.values() if arm in v]
        if not rows: continue
        s = lambda name: sum(num(r[name], 0) for r in rows)
        fired = s('chFired1') + s('chFired2')
        kills = s('chainKills1') + s('chainKills2')
        print('| %s | %s → %s (%% %s) | %s → %s (%% %s) | %s | %s | %s | %s | %s | %s | %s · %s | %s | %s | %% %s | %s | %% %s | %% %s | %% %s |' % (
            arm, n0(s('chAttempts1')), n0(s('chTriggers1')), f(100 * s('chTriggers1') / max(1, s('chAttempts1')), 0),
            n0(s('chAttempts2')), n0(s('chTriggers2')), f(100 * s('chTriggers2') / max(1, s('chAttempts2')), 0),
            n0(s('chRejRepeat')), n0(s('chRejGeneration')), n0(s('chRejBudget')), n0(s('chRejInvalid')), n0(s('chRejRoundEnd')), n0(s('chRejExcluded')),
            n0(s('chPoolWaitJobs')), n0(s('chPoolWaitFrames')), f(s('chDelayFrames') / max(1, fired), 1), n0(s('chainHits')),
            f(100 * s('chainHitsNew') / max(1, s('chainHits')), 0), f(kills / max(1, s('chainHits'))),
            f(100 * med([num(r['chainKillHp']) for r in rows]), 0), f(100 * s('ghostReached') / max(1, s('ghostResolved')), 1), f(100 * s('ghostLethal') / max(1, s('ghostResolved')), 1)))
    print('\nSayılar eşleşmelerin toplamıdır. "Sıradaki saldırı erişirdi": zincirin öldürdüğü bitkiler yerinde dursaydı botun seçeceği sıradaki doğrudan saldırının alanına girenler.\n')


def chain(rows):
    runs = [r for r in runs_of(rows) if any(num(row.get('chainKills'), 0) > 0 for row in r.rounds.values())]
    print('Zinciri alan gerçek run\'lar (ödül alındıktan sonraki round\'lar toplamı).\n')
    print('| Politika | Kol | Seed | Zincir round\'u | Hasat | Zincir payı | Nesil 1 tetik oranı | Nesil 2 tetik oranı | Tekrar engeli | Nesil sınırı | Havuz bekleyen iş | Yeni hücreye | Sıradaki saldırı erişirdi | Doluluk | Bekleme sn |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for run in runs:
        rs = [row for r, row in sorted(run.rounds.items()) if num(row.get('chAttempts1'), 0) + num(row.get('chainKills'), 0) > 0]
        if not rs: continue
        s = lambda name: sum(num(r[name], 0) for r in rs)
        first = min(int(r['round']) for r in rs)
        print('| %s | %s | %d | R%d | %s | %% %s | %% %s | %% %s | %s | %s | %s | %% %s | %% %s | %s | %s |' % (
            run.policy, run.label, run.seed, first, n0(s('kills')), f(100 * s('chainKills') / max(1, s('kills')), 0),
            f(100 * s('chTriggers1') / max(1, s('chAttempts1')), 0), f(100 * s('chTriggers2') / max(1, s('chAttempts2')), 0), n0(s('chRejRepeat')), n0(s('chRejGeneration')),
            n0(s('chPoolWaitJobs')), f(100 * s('chainHitsNew') / max(1, s('chainHits')), 0), f(100 * s('ghostReached') / max(1, s('ghostResolved')), 1),
            f(med([num(r['fill']) for r in rs])), f(med([num(r['plantWait']) for r in rs]), 1)))


def pair(rows, label_a, label_b, policy=None):
    runs = [r for r in runs_of(rows) if policy is None or r.policy == policy]
    a = {(r.policy, r.seed): r for r in runs if r.label == label_a}
    b = {(r.policy, r.seed): r for r in runs if r.label == label_b}
    keys = [k for k in a if k in b]
    print('Eşleştirilmiş kollar (aynı politika ve seed): **%s** ↔ **%s** · %d çift. Oran B ÷ A, medyan (en düşük – en yüksek).\n' % (label_a, label_b, len(keys)))
    print('| Aralık | Çift | Hasat B ÷ A | Skor B ÷ A | B\'nin önde olduğu çift | Hasat / sn A → B | Doluluk A → B | Bekleme A → B sn |')
    print('|---|---|---|---|---|---|---|---|')
    for name, lo, hi in (('R1–22', 1, 22), ('R23–30', 23, 30), ('R31–40', 31, 40), ('R41–50', 41, 50), ('R23–50', 23, 50), ('R1–50', 1, 50)):
        kr, sr, ka, kb, fa, fb, wa, wb = [], [], [], [], [], [], [], []
        for k in keys:
            rounds = [r for r in range(lo, hi + 1) if r in a[k].rounds and r in b[k].rounds]
            if len(rounds) < hi - lo + 1: continue
            tot = lambda run, name: sum(num(run.rounds[r][name]) for r in rounds)
            sec = sum(seconds(r) for r in rounds)
            kr.append(tot(b[k], 'kills') / tot(a[k], 'kills')); sr.append(tot(b[k], 'score') / tot(a[k], 'score'))
            ka.append(tot(a[k], 'kills') / sec); kb.append(tot(b[k], 'kills') / sec)
            fa.append(med([num(a[k].rounds[r]['fill']) for r in rounds])); fb.append(med([num(b[k].rounds[r]['fill']) for r in rounds]))
            wa.append(med([num(a[k].rounds[r]['plantWait']) for r in rounds])); wb.append(med([num(b[k].rounds[r]['plantWait']) for r in rounds]))
        if not kr: continue
        print('| %s | %d | ×%s (%s – %s) | ×%s (%s – %s) | %d / %d | %s → %s | %s → %s | %s → %s |' % (
            name, len(kr), f(med(kr)), f(min(kr)), f(max(kr)), f(med(sr)), f(min(sr)), f(max(sr)), sum(1 for v in sr if v > 1), len(sr),
            f(med(ka)), f(med(kb)), f(med(fa)), f(med(fb)), f(med(wa), 1), f(med(wb), 1)))


def duration(old_rows, new_rows):
    old, new = runs_of(old_rows), runs_of(new_rows)
    old_by = {(r.policy, r.seed): r for r in old}
    pairs = [(old_by[(r.policy, r.seed)], r) for r in new if (r.policy, r.seed) in old_by]
    print('Aynı politika ve seed; eski = sabit 45 sn (Run50_XPV1, Bölüm 3.7.7 ölçümü), yeni = süre tablosu (başka hiçbir değer değişmedi). '
          '%d çift. Oran yeni ÷ eski, medyan.\n' % len(pairs))
    for policy in OrderedDict((r.policy, None) for _, r in pairs):
        ps = [(o, n) for o, n in pairs if n.policy == policy]
        print('**%s** · %d çift\n' % (policy, len(ps)))
        print('| Round | Süre eski → yeni | Hasat / round | Hasat / sn | Skor / round | Skor / sn | Altın geliri | XP | Level eski → yeni | Hasar eski → yeni | Common ÖV eski → yeni | Doluluk eski → yeni |')
        print('|---|---|---|---|---|---|---|---|---|---|---|---|')
        for r in (5, 10, 15, 20, 25, 30, 35, 40, 45, 50):
            both = [(o, n) for o, n in ps if r in o.rounds and r in n.rounds]
            if not both: continue
            ratio = lambda name: med([num(n.rounds[r][name]) / num(o.rounds[r][name]) for o, n in both if num(o.rounds[r][name]) > 0])
            rate = lambda name: med([(num(n.rounds[r][name]) / seconds(r)) / (num(o.rounds[r][name]) / 45.0) for o, n in both if num(o.rounds[r][name]) > 0])
            mo = lambda name: med([num(o.rounds[r][name]) for o, n in both]); mn = lambda name: med([num(n.rounds[r][name]) for o, n in both])
            print('| R%d | 45 → %d | ×%s | ×%s | ×%s | ×%s | ×%s | ×%s | %s → %s | %s → %s | %s → %s | %s → %s |' % (
                r, seconds(r), f(ratio('kills')), f(rate('kills')), f(ratio('score')), f(rate('score')), f(ratio('gold')), f(ratio('xp')),
                n0(mo('level')), n0(mn('level')), n0(mo('damage')), n0(mn('damage')), f(mo('htkCommon')), f(mn('htkCommon')), f(mo('fill')), f(mn('fill'))))
        print()


def hp(rows):
    runs = runs_of(rows)
    print("Öldürme vuruş sayısı (ÖV): doğrudan vuruşla ölen bitkinin aldığı doğrudan vuruş sayısı, medyan (seed'ler). Kol = can eğrisi adayı.\n")
    print('| Politika | Aday | Round | Run | ÖV Common | ÖV Uncommon | ÖV Rare | ÖV Epic | ÖV Legendary | Hasat / sn | Skor / sn | Davranış payı | Doluluk | Bekleme sn | Hasar × çarpan | Level | Temel güç kartı |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for (policy, label), group in sorted(groups(runs).items(), key=lambda kv: (kv[0][0], kv[0][1])):
        for r in (20, 30, 40, 50):
            alive = [run for run in group if r in run.rounds]
            if not alive: continue
            m = lambda name: med([col(run, r, name) for run in alive])
            beh = med([1 - (col(run, r, 'direct') or 0) / col(run, r, 'kills') if col(run, r, 'kills') else None for run in alive])
            dmg = med([(col(run, r, 'damage') or 0) * (col(run, r, 'directMult') or 1) for run in alive])
            print('| %s | %s | R%d | %d | %s | %s | %s | %s | %s | %s | %s | %% %s | %s | %s | %s | %s | %s |' % (
                policy, label, r, len(alive), f(m('htkCommon')), f(m('htkUncommon')), f(m('htkRare')), f(m('htkEpic')), f(m('htkLegendary')),
                f(m('killsPerSec')), f(m('scorePerSec')), f(100 * beh, 0), f(m('fill')), f(m('plantWait'), 1), n0(dmg), n0(m('level')), n0(m('cardBase'))))


def load(rows):
    """Seçim sayısı ve menü yükü. Süreler insan ölçümü DEĞİLDİR: seçim sayısı × varsayılan saniye."""
    g = OrderedDict()
    for row in rows: g.setdefault((row['policy'], row['label']), []).append(row)
    print('Menü süreleri insan ölçümü değildir: kart seçimi × 2 / 4 / 6 sn. Aktif oyun 51 dk 40 sn (3 100 sn).\n')
    print('| Politika | Kol | Run | R50 sonuna ulaşan | Level (medyan) | Seçim hakkı (en az – medyan – en çok) | Bir round sonunda en çok seçim | Temel güç kartı | 2 sn / kart | 4 sn / kart | 6 sn / kart | Level bekleyişi (kare) |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|')
    for (policy, label), rs in g.items():
        done = [r for r in rs if int(r['round']) >= 50]
        use = done or rs
        ch = [num(r['choices']) for r in use]
        m = lambda name: med([num(r[name]) for r in use])
        mins = lambda s: f(med(ch) * s / 60.0, 0) + ' dk'
        print('| %s | %s | %d | %d | %s | %s – %s – %s | %s | %s | %s | %s | %s | %s |' % (
            policy, label, len(rs), len(done), n0(m('level')), n0(min(ch)), n0(med(ch)), n0(max(ch)), n0(max(num(r['maxRoundChoices']) for r in use)),
            n0(m('cardBase')), mins(2), mins(4), mins(6), n0(max(num(r['maxWaitFrames']) for r in use))))


def openings(rows, name='params'):
    runs = runs_of(rows)
    quota, boss = tables(name, runs) if name != 'yok' else (None, None)
    print('İlk üç dönem. Hücre: dönem skoru / boss round\'u skoru; en düşük – medyan – en yüksek (3 seed).\n')
    head = '| Açılış | R1–3 dönemi | R3 boss | R4–6 dönemi | R6 boss | R7–10 dönemi | R10 boss |'
    print(head + (' Üç boss\'u da geçen seed |' if quota else ''))
    print('|---|---|---|---|---|---|---|' + ('---|' if quota else ''))
    for (policy, label), group in groups(runs).items():
        cells, passed = [], 0
        for i in range(3):
            ps = [run.period_score(i) for run in group if run.period_score(i) is not None]
            bs = [run.boss_score(i) for run in group if run.boss_score(i) is not None]
            cells.append('%s – %s – %s' % (n0(min(ps)), n0(med(ps)), n0(max(ps))) if ps else '–')
            cells.append('%s – %s – %s' % (n0(min(bs)), n0(med(bs)), n0(max(bs))) if bs else '–')
        if quota:
            for run in group:
                ok = all(run.period_score(i) is not None and run.period_score(i) >= quota[i] and run.boss_score(i) >= boss[i] for i in range(3))
                passed += 1 if ok else 0
        print('| %s%s | %s |%s' % (label, '' if policy == 'Dengeli' else ' (%s)' % policy, ' | '.join(cells), ' %d / %d |' % (passed, len(group)) if quota else ''))
    if quota: print('\nHedefler (%s): kota %s · boss %s' % (name, quota[:3], boss[:3]))


def growth(rows):
    runs = runs_of(rows)
    print('Build\'in kendi gelişimi (medyanlar): hasat / sn ve skor / sn, ve bir önceki sütuna oranı.\n')
    print('| Politika | Kol | Run | Hasat / sn R20 | R30 | R40 | R50 | R30 → R40 | R40 → R50 | Skor / sn R20 | R30 | R40 | R50 | R30 → R40 | R40 → R50 | Davranış vuruşu başına hasat R30 / R40 / R50 |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for (policy, label), group in groups(runs).items():
        k = {r: med([col(run, r, 'killsPerSec') for run in group if r in run.rounds]) for r in (20, 30, 40, 50)}
        sc = {r: med([col(run, r, 'scorePerSec') for run in group if r in run.rounds]) for r in (20, 30, 40, 50)}
        def hit(r):
            v = [col(run, r, 'normalBehaviorKills') / col(run, r, 'behaviorHits') for run in group if r in run.rounds and col(run, r, 'behaviorHits')]
            return f(100 * med(v), 0) if v else '–'
        ratio = lambda d, a, b: ('×' + f(d[b] / d[a])) if d[a] and d[a] == d[a] and d[b] == d[b] else '–'
        print('| %s | %s | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %% %s / %s / %s |' % (
            policy, label, len(group), f(k[20]), f(k[30]), f(k[40]), f(k[50]), ratio(k, 30, 40), ratio(k, 40, 50),
            f(sc[20], 0), f(sc[30], 0), f(sc[40], 0), f(sc[50], 0), ratio(sc, 30, 40), ratio(sc, 40, 50), hit(30), hit(40), hit(50)))


def chainpick(rows):
    labs = runs_of(rows, labs=True)
    table = defaultdict(dict)
    for run in labs:
        text = run.label
        round_ = int(text.split()[1][1:])
        repeat = text.rsplit('#', 1)[1]
        arm = 'A' if ' A ' in text else (text[text.index('[') + 1:text.index(']')] if '[' in text else 'B')
        row = run.rounds.get(round_)
        if row is not None: table[(run.policy, run.seed, round_, repeat)][arm] = row
    print('Zincir kolları, "mevcut" kola göre (aynı kayıt, aynı seed ve tekrar): toplam hasat ve skor oranı, eşleştirilmiş medyan (en düşük – en yüksek).\n')
    print('| Round | Kol | Eşleşme | Toplam hasat ÷ mevcut | Skor ÷ mevcut | Artış olan eşleşme |')
    print('|---|---|---|---|---|---|')
    for round_ in sorted({k[2] for k in table}):
        cells = [v for k, v in table.items() if k[2] == round_ and 'mevcut' in v]
        for arm in ('şans üst', 'hasar üst', 'ikisi üst'):
            ps = [v for v in cells if arm in v]
            if not ps: continue
            kr = [num(v[arm]['kills']) / num(v['mevcut']['kills']) for v in ps]
            sr = [num(v[arm]['score']) / num(v['mevcut']['score']) for v in ps if num(v['mevcut']['score']) > 0]
            print('| R%d | %s | %d | ×%s (%s – %s) | ×%s | %d / %d |' % (round_, arm, len(ps), f(med(kr), 3), f(min(kr)), f(max(kr)), f(med(sr), 3), sum(1 for v in kr if v > 1), len(kr)))


def perf(rows):
    runs = runs_of(rows)
    print('Kare süresi: art arda iki aktif round karesinin arası (gerçek saat; oyun + editör + ölçüm botu; düşük öncelikli batch). GPU ve algılanan akıcılık hakkında sonuç vermez.\n')
    print('| Politika | Kol | Round | Run | Hasat / sn | Zincir payı | Kare ms: ortalama | %95 | en uzun | Botun payı ms | GC (round başına) | Atlanan elektrik görseli | Atlanan patlama görseli | Kaybolan bumerang | Zincir: havuz bekleyen iş | Kuyruk gecikmesi kare / iş |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for (policy, label), group in groups(runs).items():
        for r in (30, 40, 45, 50):
            alive = [run for run in group if r in run.rounds]
            if not alive: continue
            m = lambda name: med([col(run, r, name) for run in alive])
            mx = lambda name: max([col(run, r, name) or 0 for run in alive])
            fired = sum((col(run, r, 'chFired1') or 0) + (col(run, r, 'chFired2') or 0) for run in alive)
            delay = sum(col(run, r, 'chDelayFrames') or 0 for run in alive)
            print('| %s | %s | R%d | %d | %s | %% %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |' % (
                policy, label, r, len(alive), f(m('killsPerSec'), 1), f(100 * med([share(run, r, 'chainKills') for run in alive]), 0), f(m('frameMsAvg')), f(m('frameMsP95')), f(mx('frameMsMax'), 0),
                f(m('botMsAvg')), n0(mx('gc0')), n0(mx('skipElectricVisual')), n0(mx('skipExplosionVisual')), n0(mx('skipBoomerang')), n0(mx('chPoolWaitJobs')), f(delay / fired, 1) if fired else '–'))


def clock(rows):
    g = OrderedDict()
    for row in rows: g.setdefault((row['policy'], row['label']), []).append(row)
    print('Süre sayaçları (RunClock), R50 sonuna ulaşan run\'lar. Round sayacı ve gerçek aktif süre sabit kare adımıyla tekrarlanabilir; menü sayaçları botun menüde geçirdiği '
          'kare sayısıdır (insan süresi DEĞİLDİR, yalnız sayaçların ayrıldığını gösterir).\n')
    print('| Politika | Kol | Run | Round sayacı toplamı sn | Gerçek aktif süre sn | Fark sn | Sayılan round | Level hesaplama | Kart | Boss ödülü | Mağaza | Hazırlık | Duraklama |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for (policy, label), rs in g.items():
        done = [r for r in rs if r.get('roundsTimed') == '50']
        if not done: continue
        m = lambda name: med([num(r[name]) for r in done])
        print('| %s | %s | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |' % (
            policy, label, len(done), f(m('clockActiveGame'), 1), f(m('clockActiveReal'), 1), f(m('clockActiveReal') - m('clockActiveGame'), 1), n0(m('roundsTimed')),
            f(m('clockLevelWork'), 1), f(m('clockCards'), 1), f(m('clockRoundChoice'), 1), f(m('clockShop'), 1), f(m('clockPrep'), 1), f(m('clockPaused'), 1)))


def xp(rows, finals, label_a, label_b):
    """XP yatırımsız (A) ↔ XP düğümleri vadesinde (B): aynı politika ve seed. Hedefler gerçek: elenen run biter."""
    runs = runs_of(rows)
    fin = {(r['_file'].replace('_final', ''), r['run']): r for r in finals}
    quota, boss = tables('params', runs)
    print('XP yatırımı alt karşılaştırması: **%s** (XP düğümü almaz; XP kartını ve Bilgi Filizi\'ni yalnız başka seçenek yoksa alır) ↔ **%s** (XP düğümleri ağaçtaki '
          'vadesinde). Aynı seed, gerçek hedefler.\n' % (label_a, label_b))
    print('| Yön | Kol | Run | Kazanan | Elenen | Pencere olan run | R50 level (medyan) | Seçim hakkı | XP düğümlerine harcanan | Temel güç evresine giren run · ilk round (medyan) | Toplam skor (kazananlar, medyan) |')
    print('|---|---|---|---|---|---|---|---|---|---|---|')
    for policy in OrderedDict((r.policy, None) for r in runs if r.label in (label_a, label_b)):
        for label in (label_a, label_b):
            group = [r for r in runs if r.policy == policy and r.label == label]
            if not group: continue
            wins, fails, windows, totals = 0, [], 0, []
            for run in group:
                failed, why, q, b = evaluate(run, quota, boss)
                if failed is None and run.period_score(14) is not None:
                    wins += 1; totals.append(sum(num(run.rounds[r]['score']) for r in run.rounds))
                else: fails.append('R%d' % (PERIODS[failed][1] if failed is not None else run.end_round))
                if window(q, b, failed)[0] is not None: windows += 1
            fs = [fin[run.key[:2]] for run in group if run.key[:2] in fin]
            done = [f_ for f_ in fs if int(f_['round']) >= 50 and f_['outcome'] == 'KAZANDI']
            base = [num(f_['firstBase']) for f_ in done if num(f_['firstBase']) > 0]
            m = lambda name: med([num(f_[name]) for f_ in done])
            print('| %s | %s | %d | %d | %s | %d | %s | %s | %s | %d / %d · %s | %s |' % (
                policy, label, len(group), wins, ', '.join(fails) or '–', windows, n0(m('level')), n0(m('choices')), n0(m('xpSpent')),
                len(base), len(done), ('R%s' % n0(med(base))) if base else '–', n0(med(totals))))
    print()
    print('Eşleştirilmiş oranlar (%s ÷ %s; iki kolun da o round\'a ulaştığı seed\'ler; medyan ve en düşük – en yüksek):\n' % (label_b, label_a))
    print('| Yön | Round | Çift | Level | O round\'un hasadı | O round\'un skoru | R1\'den beri hasat | R1\'den beri skor | XP\'li kolun önde olduğu çift (birikimli skor) |')
    print('|---|---|---|---|---|---|---|---|---|')
    for policy in OrderedDict((r.policy, None) for r in runs if r.label in (label_a, label_b)):
        a = {r.seed: r for r in runs if r.policy == policy and r.label == label_a}
        b = {r.seed: r for r in runs if r.policy == policy and r.label == label_b}
        for r in CHECKPOINTS:
            keys = [k for k in a if k in b and all(x in a[k].rounds and x in b[k].rounds for x in range(1, r + 1))]
            if not keys: continue
            cum = lambda run, name: sum(num(run.rounds[x][name]) for x in range(1, r + 1))
            ratios = lambda fn: [fn(b[k]) / fn(a[k]) for k in keys if fn(a[k]) > 0]
            lv, kr, sr = ratios(lambda run: num(run.rounds[r]['level'])), ratios(lambda run: num(run.rounds[r]['kills'])), ratios(lambda run: num(run.rounds[r]['score']))
            ck, cs = ratios(lambda run: cum(run, 'kills')), ratios(lambda run: cum(run, 'score'))
            cell = lambda v: '×%s (%s – %s)' % (f(med(v)), f(min(v)), f(max(v))) if v else '–'
            print('| %s | R%d | %d | %s | %s | %s | %s | %s | %d / %d |' % (policy, r, len(keys), cell(lv), cell(kr), cell(sr), cell(ck), cell(cs), sum(1 for v in cs if v > 1), len(cs)))


if __name__ == '__main__':
    if len(sys.argv) < 3: raise SystemExit(__doc__)
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    mode, data = sys.argv[1], read(sys.argv[2])
    if mode == 'curve': curve(data)
    elif mode == 'periods': periods(data, sys.argv[3] if len(sys.argv) > 3 else 'params')
    elif mode == 'targets':
        if len(sys.argv) > 4: QUOTA_SHARE_OF_ANCHOR, BOSS_SHARE_OF_ANCHOR = float(sys.argv[3]), float(sys.argv[4])
        targets(data)
    elif mode == 'window': window_report(data, sys.argv[3] if len(sys.argv) > 3 else 'params')
    elif mode == 'runs': run_list(data, sys.argv[3] if len(sys.argv) > 3 else 'params')
    elif mode == 'lab': lab(data)
    elif mode == 'chain': chain(data)
    elif mode == 'pair': pair(data, sys.argv[3], sys.argv[4], sys.argv[5] if len(sys.argv) > 5 else None)
    elif mode == 'duration': duration(data, read(sys.argv[3]))
    elif mode == 'hp': hp(data)
    elif mode == 'load': load(data)
    elif mode == 'openings': openings(data, sys.argv[3] if len(sys.argv) > 3 else 'yok')
    elif mode == 'growth': growth(data)
    elif mode == 'chainpick': chainpick(data)
    elif mode == 'perf': perf(data)
    elif mode == 'clock': clock(data)
    elif mode == 'xp': xp(data, read(sys.argv[3]), sys.argv[4], sys.argv[5])
    else: raise SystemExit(__doc__)
