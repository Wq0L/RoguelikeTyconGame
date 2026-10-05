"""Bölüm 3.7.7 (P7) ölçüm tabloları. Girdi: BalanceRunMeasurement çıktıları (BalanceRuns_<set>.csv, _final.csv, _rewards.csv).
Çıktı Markdown'dur; karar kuralı yoktur (sayı verir).

  python analyze_xp.py final   <final.csv[,…]>                 run başına sonuç satırı
  python analyze_xp.py menu    <final.csv[,…]>                 seçim sayısı ve menü yükü senaryoları (kart başına 2 / 4 / 6 sn)
  python analyze_xp.py access  <final.csv[,…]> [runs.csv[,…]]   erken davranışın üç erişim zamanı; R6'ya kadar erişim
  python analyze_xp.py ab      <runs.csv[,…]> <final.csv[,…]> <rewards.csv[,…]> [kol]   eş bütçeli XP kolları (XP'siz ↔ kol; varsayılan "erken XP")
  python analyze_xp.py pilot   <runs.csv> <final.csv>          kuyruk katsayısı pilotu
  python analyze_xp.py diverge <eski.csv[,…]> <yeni.csv[,…]>    aynı politika / kol / seed: ilk farklı round ve geç round'lar

Menü süreleri insan ölçümü DEĞİLDİR: seçim sayısı × varsayılan saniye. Bot menüde zaman harcamaz.
"""
import csv
import io
import statistics as st
import sys
from collections import OrderedDict, defaultdict

CHECKPOINTS = (10, 20, 30, 40, 50)
ARM_A, ARM_B = "XP'siz", 'erken XP'
# diverge: zaman ölçümü, run sırası, profil adı ve yalnız görsel sayaç karşılaştırılmaz.
SKIP = {'wallMs', 'maxFrameMs', 'gc0', 'allocBytes', 'run', 'profile', 'skipExplosionVisual'}


def read(paths):
    rows = []
    for path in paths.split(','):
        rows += list(csv.DictReader(io.open(path, encoding='utf-8')))
    return rows


def num(v, default=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


def med(values):
    values = [v for v in values if v is not None]
    return st.median(values) if values else float('nan')


def fmt(v, digits=0):
    if v != v: return '–'
    if digits == 0: return '{:,.0f}'.format(v).replace(',', ' ')
    return ('%.' + str(digits) + 'f') % v


def runs_of(rows):
    """(policy, label, seed) → round satırları (sonuç satırı hariç), round sırasıyla."""
    out = OrderedDict()
    for r in rows:
        if r.get('outcome'): continue
        out.setdefault((r['policy'], r['label'], int(r['seed'])), []).append(r)
    for rs in out.values(): rs.sort(key=lambda r: int(r['round']))
    return out


def table(header, lines):
    out = ['| ' + ' | '.join(header) + ' |', '|' + '---|' * len(header)]
    out += ['| ' + ' | '.join(str(c) for c in line) + ' |' for line in lines]
    return out


# ---------------------------------------------------------------- final
def mode_final(final):
    lines = []
    for f in final:
        lines.append([f['policy'], f['label'], f['seed'], '%s @R%s' % (f['outcome'], f['round']), f['level'], f['choices'], f['cardsTaken'],
                      '%s / %s / %s' % (f['cardTiles'], f['cardUpgrades'], f['cardBase']), f['cardXpBase'],
                      'R%s / R%s / R%s' % (f['firstBehavior'], f['firstEffective'], f['firstTrigger']),
                      'R%s / R%s' % (f['firstUpgrade'] or '–', f['firstBase'] if f['firstBase'] != '0' else '–'),
                      f['xpSpent'], f['xpGlobal'], f['xpCardGroup'], f['levelCost'], f['levelHalted'], f['saturated'], f['invalidNumbers'],
                      '%s (R%s)' % (f['maxRoundChoices'], f['maxRoundChoicesRound']), f['maxWaitFrames'], f['remainingShownOk']])
    return table(['Politika', 'Kol', 'Seed', 'Sonuç', 'Level', 'Seçim hakkı', 'Alınan kart', 'Tile / yükseltme / temel güç', 'XP temel kartı',
                  'Kart / etkin saksı / ilk tetik', 'İlk yükseltme / temel güç', 'XP düğümü harcaması', 'Global XP ×', 'XP kart toplamı', 'Sonraki level maliyeti',
                  'Level durdu', 'Doyum', 'Geçersiz', 'En kalabalık round sonu', 'En uzun bekleyiş (kare)', 'Sayaç doğru'], lines)


# ---------------------------------------------------------------- menü yükü
def mode_menu(final):
    groups = OrderedDict()
    for f in final: groups.setdefault((f['policy'], f['label']), []).append(f)
    lines = []
    for (policy, label), fs in groups.items():
        taken = [num(f['cardsTaken']) for f in fs]; peak = [num(f['maxRoundChoices']) for f in fs]
        levels = [num(f['level']) for f in fs]
        def minutes(seconds): return '%s / %s dk' % (fmt(med(taken) * seconds / 60, 1), fmt(max(taken) * seconds / 60, 1))
        lines.append([policy, label, len(fs), '%s / %s' % (fmt(med(levels)), fmt(max(levels))), '%s / %s' % (fmt(med(taken)), fmt(max(taken))),
                      '%s / %s' % (fmt(med(peak)), fmt(max(peak))), max(int(num(f['maxWaitFrames'])) for f in fs),
                      minutes(2), minutes(4), minutes(6), sum(1 for f in fs if f['levelHalted'] == '1'),
                      sum(1 for f in fs if num(f['saturated']) > 0), sum(1 for f in fs if num(f['invalidNumbers']) > 0)])
    return table(['Politika', 'Kol', 'Run', 'Level (medyan / en yüksek)', 'Seçim (medyan / en yüksek)', 'Bir round sonunda en çok seçim (medyan / en yüksek)',
                  'Level işleme bekleyişi (en uzun, kare)', 'Senaryo 2 sn / kart (medyan / en yüksek)', 'Senaryo 4 sn', 'Senaryo 6 sn',
                  'Level işleme duran run', 'Doyum olan run', 'Geçersiz sayı olan run'], lines)


# ---------------------------------------------------------------- erken davranış erişimi
def mode_access(final, rows):
    groups = OrderedDict()
    for f in final: groups.setdefault((f['policy'], f['label']), []).append(f)
    lines = []
    for (policy, label), fs in groups.items():
        def by(col, limit=6): return sum(1 for f in fs if 0 < int(f[col]) <= limit)
        def spread(col):
            values = sorted(int(f[col]) for f in fs)
            return ' '.join('R%d×%d' % (v, values.count(v)) if v else 'yok×%d' % values.count(v) for v in sorted(set(values)))
        lines.append([policy, label, len(fs), '%d / %d' % (by('firstBehavior'), len(fs)), '%d / %d' % (by('firstEffective'), len(fs)), '%d / %d' % (by('firstTrigger'), len(fs)),
                      spread('firstBehavior'), spread('firstEffective'), spread('firstTrigger')])
    out = table(['Politika', 'Kol', 'Run', 'Kart ≤ R6', 'Etkin saksı düzeni ≤ R6', 'İlk gerçek tetik ≤ R6', 'Kart round\'ları', 'Etkin düzen round\'ları', 'İlk tetik round\'ları'], lines)
    # R6'ya kadar tetiği olmayan run'lar: neden ayrımı için erken round satırları.
    late = [f for f in final if not 0 < int(f['firstTrigger']) <= 6]
    if late and rows:
        by_run = runs_of(rows)
        out += ['', 'R6 sonuna kadar gerçek tetiği olmayan run\'lar (neden ayrımı):', '']
        detail = []
        for f in late:
            rs = by_run.get((f['policy'], f['label'], int(f['seed'])), [])
            card, eff, trig = int(f['firstBehavior']), int(f['firstEffective']), int(f['firstTrigger'])
            if card == 0 or card > 6: reason = 'XP / teklif: kart R6\'ya kadar alınmadı'
            elif eff == 0 or eff > 6: reason = 'yerleşim / satın alma: kart alındı, tile saksı altında değil'
            else: reason = 'tetik şansı: etkin düzen var, şans tutmadı'
            early = ' · '.join('R%s: lv %s, tile %s (saksı altı %s), saksı %s, altın %s' % (r['round'], r['level'], r['behaviorTiles'], r['behaviorPlanted'], r['planters'], r['bankGold'])
                               for r in rs if int(r['round']) <= 7)
            detail.append([f['policy'], f['label'], f['seed'], 'R%d / R%d / R%d' % (card, eff, trig), reason, early])
        out += table(['Politika', 'Kol', 'Seed', 'Kart / etkin / tetik', 'Neden', 'Erken round\'lar'], detail)
    return out


# ---------------------------------------------------------------- eş bütçeli A/B
def cumulative(rs, col):
    total, out = 0.0, {}
    for r in rs:
        total += num(r[col]); out[int(r['round'])] = total
    return out


def at(rs, rnd, col):
    for r in rs:
        if int(r['round']) == rnd: return num(r[col])
    return None


def mode_ab(rows, final, rewards, arm_b=None):
    global ARM_B
    if arm_b: ARM_B = arm_b
    by_run = runs_of(rows)
    outcome = {(f['policy'], f['label'], int(f['seed'])): f for f in final}
    out = []
    for policy in OrderedDict.fromkeys(k[0] for k in by_run):
        seeds = sorted(s for (p, l, s) in by_run if p == policy and l == ARM_A and (policy, ARM_B, s) in by_run)
        if not seeds: continue
        out += ['### ' + policy, '']
        fa = [outcome[(policy, ARM_A, s)] for s in seeds]; fb = [outcome[(policy, ARM_B, s)] for s in seeds]
        out.append('%d eşleşmiş seed (%s). Sonuç: XP\'siz %s · %s %s.' % (
            len(seeds), ' '.join(str(s) for s in seeds),
            ', '.join('%s×%d' % (o, sum(1 for f in fa if f['outcome'] == o)) for o in sorted(set(f['outcome'] for f in fa))), ARM_B,
            ', '.join('%s×%d' % (o, sum(1 for f in fb if f['outcome'] == o)) for o in sorted(set(f['outcome'] for f in fb)))))
        out.append('')
        # kontrol noktaları
        lines = []
        for rnd in CHECKPOINTS:
            def col(arm, c, cum=False):
                values = []
                for s in seeds:
                    rs = by_run[(policy, arm, s)]
                    v = cumulative(rs, c).get(rnd) if cum else at(rs, rnd, c)
                    values.append(v)
                return values
            def pair(c, cum=False, digits=0):
                a, b = col(ARM_A, c, cum), col(ARM_B, c, cum)
                ratios = [y / x for x, y in zip(a, b) if x and y is not None and x is not None]
                wins = sum(1 for x, y in zip(a, b) if x is not None and y is not None and y > x)
                both = sum(1 for x, y in zip(a, b) if x is not None and y is not None)
                return '%s → %s (×%s; %d / %d)' % (fmt(med(a), digits), fmt(med(b), digits), fmt(med(ratios), 2), wins, both)
            reached = sum(1 for s in seeds if at(by_run[(policy, ARM_A, s)], rnd, 'level') is not None), sum(1 for s in seeds if at(by_run[(policy, ARM_B, s)], rnd, 'level') is not None)
            def share(arm, score, target):
                values = []
                for s in seeds:
                    sc, tg = at(by_run[(policy, arm, s)], rnd, score), at(by_run[(policy, arm, s)], rnd, target)
                    if sc is not None and tg: values.append(sc / tg)
                return fmt(med(values), 1)
            lines.append(['R%d' % rnd, '%d / %d' % reached, pair('level'), pair('xp', True), pair('choices'),
                          '%s·%s·%s → %s·%s·%s' % tuple(fmt(med(col(a, c))) for a in (ARM_A, ARM_B) for c in ('cardTiles', 'cardUpgrades', 'cardBase')),
                          '%s → %s' % (fmt(med(col(ARM_A, 'xpSpent'))), fmt(med(col(ARM_B, 'xpSpent')))),
                          pair('kills'), pair('score'), pair('kills', True), pair('score', True),
                          '×%s → ×%s' % (share(ARM_A, 'segScore', 'segTarget'), share(ARM_B, 'segScore', 'segTarget')),
                          '×%s → ×%s' % (share(ARM_A, 'bossScore', 'bossTarget'), share(ARM_B, 'bossScore', 'bossTarget'))])
        out += table(['Round', 'Ulaşan run (XP\'siz / %s)' % ARM_B, 'Level', 'Toplam XP', 'Seçim hakkı (birikimli)', 'Kart: tile·yükseltme·temel güç', 'XP düğümü harcaması (altın)',
                      'Hasat (o round)', 'Skor (o round)', 'Hasat (birikimli)', 'Skor (birikimli)', 'Dönem skoru ÷ kota', 'Boss skoru ÷ boss hedefi'], lines)
        out += ['', 'Her hücre: XP\'siz medyanı → %s medyanı (seed başına oranın medyanı; %s kolunun önde olduğu seed sayısı).' % (ARM_B, ARM_B), '']
        # geri ödeme: round round oran
        def ratio_series(c, cum):
            series = {}
            for rnd in range(1, 51):
                ratios = []
                for s in seeds:
                    a_rs, b_rs = by_run[(policy, ARM_A, s)], by_run[(policy, ARM_B, s)]
                    a = cumulative(a_rs, c).get(rnd) if cum else at(a_rs, rnd, c)
                    b = cumulative(b_rs, c).get(rnd) if cum else at(b_rs, rnd, c)
                    if a and b is not None: ratios.append(b / a)
                if ratios: series[rnd] = (med(ratios), sum(1 for x in ratios if x > 1), len(ratios))
            return series
        def first_sustained(series, need=3):
            """Medyan oranın ≥ 1 olduğu ve en az `need` round üst üste öyle kaldığı ilk round."""
            rounds = sorted(series)
            for i, rnd in enumerate(rounds):
                window = rounds[i:i + need]
                if len(window) == need and all(series[r][0] >= 1 for r in window): return rnd
            return None
        def last_below(series):
            below = [r for r in sorted(series) if series[r][0] < 1]
            return below[-1] if below else None
        lines = []
        for name, c, cum in (('Hasat (o round)', 'kills', False), ('Skor (o round)', 'score', False), ('Hasat (birikimli)', 'kills', True), ('Skor (birikimli)', 'score', True),
                             ('Level', 'level', False)):
            series = ratio_series(c, cum)
            first, last = first_sustained(series), last_below(series)
            lines.append([name, 'R%d' % first if first else 'yok', 'R%d' % last if last else 'hiç', ' · '.join('R%d ×%s (%d/%d)' % (r, fmt(series[r][0], 2), series[r][1], series[r][2]) for r in (5, 10, 15, 20, 25, 30, 35, 40, 45, 50) if r in series)])
        out += table(['Ölçü (%s ÷ XP\'siz, seed başına; medyan)' % ARM_B, 'Üst üste 3 round ≥ ×1 olan ilk round', '×1\'in altında kalan son round', 'Seyir'], lines)
        out.append('')
        # ödül ayrışması
        if rewards:
            offers = defaultdict(dict)
            for r in rewards:
                if r['policy'] == policy and r['label'] in (ARM_A, ARM_B): offers[(int(r['seed']), int(r['round']))][r['label']] = (r['offered'], r['chosen'])
            same_offer = diff_offer = diff_choice = only = 0
            for key, arms in offers.items():
                if len(arms) < 2: only += 1; continue
                if set(arms[ARM_A][0].split()) == set(arms[ARM_B][0].split()):
                    same_offer += 1
                    if arms[ARM_A][1] != arms[ARM_B][1]: diff_choice += 1
                else: diff_offer += 1
            out.append('Boss ödülleri (iki kolda da oynanan boss round\'ları): %d teklifte adaylar aynı (%d\'inde seçim farklı), %d teklifte adaylar farklı; %d boss round\'u yalnız bir kolda oynandı.'
                       % (same_offer, diff_choice, diff_offer, only))
            out.append('')
    return out


# ---------------------------------------------------------------- pilot
def mode_pilot(rows, final):
    by_run = runs_of(rows)
    lines = []
    for f in final:
        rs = by_run.get((f['policy'], f['label'], int(f['seed'])), [])
        def lv(rnd):
            v = at(rs, rnd, 'level'); return fmt(v) if v is not None else '–'
        lines.append([f['policy'], f['seed'], f['label'], '%s @R%s' % (f['outcome'], f['round']), '%s / %s / %s' % (lv(40), lv(45), lv(50)), f['level'], f['choices'],
                      '%s (R%s)' % (f['maxRoundChoices'], f['maxRoundChoicesRound']), '%s (XP %s)' % (f['cardBase'], f['cardXpBase']), f['xpCardGroup'], f['levelCost'],
                      fmt(num(f['score'])), f['levelHalted'], f['saturated'], f['invalidNumbers'], f['maxWaitFrames'],
                      '%s / %s / %s dk' % tuple(fmt(num(f['cardsTaken']) * s / 60, 1) for s in (2, 4, 6))])
    lines.sort(key=lambda l: (l[0], int(l[1]), l[2]))
    return table(['Politika', 'Seed', 'Katsayı', 'Sonuç', 'Level R40 / R45 / R50', 'Son level', 'Seçim hakkı', 'En kalabalık round sonu', 'Temel güç kartı', 'XP kart toplamı',
                  'Sonraki level maliyeti', 'Toplam skor', 'Level durdu', 'Doyum', 'Geçersiz', 'Bekleyiş (kare)', 'Menü senaryosu 2 / 4 / 6 sn'], lines)


# ---------------------------------------------------------------- eski / yeni: ilk fark
def mode_diverge(old, new):
    a, b = runs_of(old), runs_of(new)
    out = []
    for key in a:
        if key not in b: continue
        ra, rb = {int(r['round']): r for r in a[key]}, {int(r['round']): r for r in b[key]}
        cols = [c for c in a[key][0].keys() if c in b[key][0] and c not in SKIP]
        first = None
        for rnd in sorted(set(ra) & set(rb)):
            bad = [c for c in cols if (ra[rnd][c] or '') != (rb[rnd][c] or '')]
            if bad: first = (rnd, bad); break
        out.append('### %s · %s · seed %d' % key)
        out.append('')
        out.append('İlk farklı round: %s (%d ortak sütun karşılaştırıldı; ortak round %d).' % (
            'R%d (%s)' % (first[0], ', '.join(first[1][:8])) if first else 'yok — bütün ortak round\'lar aynı', len(cols), len(set(ra) & set(rb))))
        out.append('')
        lines = []
        for rnd in sorted(set(ra) | set(rb)):
            if rnd < 30 or (rnd % 5 and rnd < 40): continue
            def v(r, c): return r[rnd][c] if rnd in r else '–'
            lines.append(['R%d' % rnd] + ['%s → %s' % (v(ra, c), v(rb, c)) for c in ('level', 'xp', 'choices', 'cardBase', 'kills', 'score', 'damage', 'interval', 'saturated', 'levelHalted')])
        out += table(['Round', 'Level', 'Round XP\'si', 'Seçim hakkı (birikimli)', 'Temel güç kartı', 'Hasat', 'Skor', 'Hasar stat\'ı', 'Saldırı aralığı', 'Doyum', 'Level durdu'], lines)
        out += ['', 'Her hücre: eski → yeni.', '']
    return out


if __name__ == '__main__':
    mode = sys.argv[1]
    if mode == 'final': lines = mode_final(read(sys.argv[2]))
    elif mode == 'menu': lines = mode_menu(read(sys.argv[2]))
    elif mode == 'access': lines = mode_access(read(sys.argv[2]), read(sys.argv[3]) if len(sys.argv) > 3 else [])
    elif mode == 'ab': lines = mode_ab(read(sys.argv[2]), read(sys.argv[3]), read(sys.argv[4]) if len(sys.argv) > 4 else [], sys.argv[5] if len(sys.argv) > 5 else None)
    elif mode == 'pilot': lines = mode_pilot(read(sys.argv[2]), read(sys.argv[3]))
    elif mode == 'diverge': lines = mode_diverge(read(sys.argv[2]), read(sys.argv[3]))
    else: raise SystemExit(__doc__)
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))
