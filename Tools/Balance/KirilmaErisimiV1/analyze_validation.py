"""Bölüm 3.7.5 — birikimli run doğrulamasının çözümleyicisi.

Girdi: BalanceRunMeasurement'ın k375patlama / k375elektrik çıktısı (aday · mevcut · almayan eş; aynı seed'ler).
Kollar ödül alınana kadar aynı run'dır; fark, hedef ödülün alındığı round'dan sonraki round'larda ölçülür.

Kullanım: python analyze_validation.py <BalanceRuns_k375patlama.csv> artci_patlama [<csv> cifte_akim ...] [--out dosya.md]
"""
import csv
import io
import statistics
import sys

ARMS = ['aday', 'mevcut', 'almayan eş']
OTHER = {'artci_patlama': 'cifte_akim', 'cifte_akim': 'artci_patlama'}   # aday profilde erişimi değişen iki ödül
SAME = ('kills', 'direct', 'explosion', 'electric', 'score', 'level', 'cards', 'rewards')


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def tr(x, digits=2):
    return ('%.*f' % (digits, x)).replace('.', ',')


def big(x):
    return '{:,}'.format(int(round(x))).replace(',', '.')


def ratio(a, b):
    return a / b if b > 0 else float('nan')


def med(values):
    values = [v for v in values if v == v]
    return statistics.median(values) if values else float('nan')


def load(path):
    runs = {}
    for row in csv.DictReader(io.open(path, encoding='utf-8')):
        run = runs.setdefault((row['label'], int(row['seed'])), dict(rounds={}, outcome='?', end=0, profile=row['profile']))
        if row['outcome']:
            run['outcome'], run['end'] = row['outcome'], int(row['round'])
        else:
            run['rounds'][int(row['round'])] = row
    return runs


def taken(run, reward):
    """Hedef ödülün alındığı round (alınmadıysa None)."""
    for r in sorted(run['rounds']):
        for item in (run['rounds'][r]['breakthrough'] or '').split():
            if item.startswith(reward + '@R'):
                return int(item.split('@R')[1])
    return None


def forced(run):
    """Almayan eşin mecburen aldığı ilk kırılma ödülü (teklifin üçü de kırılma ödülüyse): (round, ödül) ya da None."""
    for r in sorted(run['rounds']):
        items = (run['rounds'][r]['breakthrough'] or '').split()
        if items:
            name, at = items[0].split('@R')
            return int(at), name
    return None


def total(run, column, first=1, last=999):
    return sum(num(row[column]) for r, row in run['rounds'].items() if first <= r <= last)


def same_until(a, b, last):
    return all(r in a['rounds'] and r in b['rounds'] and all(a['rounds'][r][c] == b['rounds'][r][c] for c in SAME) for r in range(1, last + 1))


def report(path, reward, out):
    runs = load(path)
    seeds = sorted({seed for _, seed in runs})
    policy = next(iter(runs.values()))['rounds'][1]['policy']
    out.append('## %s · hedef ödül: `%s`' % (policy, reward))
    out.append('')
    out.append('Kaynak: `%s` · %d seed × %d kol' % (path.replace('\\', '/'), len(seeds), len(ARMS)))
    out.append('')
    out.append('| Seed | Ödül (aday) | Ödül (mevcut) | Sonuç: aday / mevcut / almayan | Son skor: aday / mevcut / almayan | Toplam hasat: aday / mevcut / almayan |')
    out.append('|---|---|---|---|---|---|')
    pairs = []
    for seed in seeds:
        arm = {name: runs.get((name, seed)) for name in ARMS}
        if any(v is None for v in arm.values()):
            out.append('| %d | eksik kol | | | | |' % seed)
            continue
        at = {name: taken(arm[name], reward) for name in ARMS}
        outcome = ' / '.join('%s R%d' % (arm[n]['outcome'], arm[n]['end']) for n in ARMS)
        score = ' / '.join(big(total(arm[n], 'score')) for n in ARMS)
        kills = ' / '.join(big(total(arm[n], 'kills')) for n in ARMS)
        show = lambda r: 'R%d' % r if r else 'alınmadı'
        out.append('| %d | %s | %s | %s | %s | %s |' % (seed, show(at['aday']), show(at['mevcut']), outcome, score, kills))
        if at['aday'] and at['aday'] == at['mevcut']:
            pairs.append((seed, at['aday'], arm))
    out.append('')
    if not pairs:
        out.append('Hedef ödül hiçbir seed\'de iki kolda birden alınmadı; eşli karşılaştırma yok.')
        out.append('')
        return
    out.append('**Ödülden sonraki round\'lar** (ödülün alındığı round\'dan sonraki ilk round\'dan run sonuna; yalnız ödülü alan %d seed).' % len(pairs))
    out.append('Almayan eş bir kırılma ödülünü mecburen aldıysa (teklifin üçü de kırılma ödülü) ya da erişimi değişen diğer ödül de alındıysa karşılaştırma o round ile biter.')
    out.append('')
    out.append('| Seed | Alındığı round | Karşılaştırılan round | Öncesi üç kolda aynı mı | Hasat: aday / mevcut / almayan | aday ÷ almayan | mevcut ÷ almayan | aday ÷ mevcut | Skor: aday ÷ almayan | mevcut ÷ almayan | Yankı hasadı: aday / mevcut | Yankı başına hasat: aday / mevcut |')
    out.append('|---|---|---|---|---|---|---|---|---|---|---|---|')
    stats = {k: [] for k in ('ka', 'km', 'kam', 'sa', 'sm', 'sam')}
    windows = {}
    for seed, at, arm in pairs:
        first = at + 1
        cut = forced(arm['almayan eş'])
        last = cut[0] if cut else 999
        # Diğer erişim varyantı da alındıysa (aday ile mevcut onda da ayrışır) karşılaştırma o round ile biter; hedef
        # ödülden önce alındıysa seed eşli karşılaştırmaya girmez.
        other = taken(arm['aday'], OTHER.get(reward, '')) or taken(arm['mevcut'], OTHER.get(reward, ''))
        if other and other <= at:
            out.append('| %d | R%d | yok: R%d, önce %s alındı | | | | | | | | | |' % (seed, at, other, OTHER[reward]))
            continue
        if last < first:
            out.append('| %d | R%d | yok: almayan eş R%d, %s | | | | | | | | | |' % (seed, at, cut[0], cut[1]))
            continue
        note = ' · almayan eş R%d: %s' % cut if cut else ''
        if other and other < last:
            last, note = other, ' · R%d: %s de alındı' % (other, OTHER[reward])
        span = 'R%d–R%d' % (first, min(last, max(arm['aday']['rounds']))) + note
        same = same_until(arm['aday'], arm['mevcut'], at) and same_until(arm['aday'], arm['almayan eş'], at - 1)
        k = {n: total(arm[n], 'kills', first, last) for n in ARMS}
        s = {n: total(arm[n], 'score', first, last) for n in ARMS}
        ek = {n: total(arm[n], 'echoKills', first, last) for n in ARMS}
        ee = {n: total(arm[n], 'echoExec', first, last) for n in ARMS}
        stats['ka'].append(ratio(k['aday'], k['almayan eş'])); stats['km'].append(ratio(k['mevcut'], k['almayan eş'])); stats['kam'].append(ratio(k['aday'], k['mevcut']))
        stats['sa'].append(ratio(s['aday'], s['almayan eş'])); stats['sm'].append(ratio(s['mevcut'], s['almayan eş'])); stats['sam'].append(ratio(s['aday'], s['mevcut']))
        out.append('| %d | R%d | %s | %s | %s / %s / %s | ×%s | ×%s | ×%s | ×%s | ×%s | %s / %s | %s / %s |' % (
            seed, at, span, 'evet' if same else 'HAYIR', big(k['aday']), big(k['mevcut']), big(k['almayan eş']),
            tr(stats['ka'][-1]), tr(stats['km'][-1]), tr(stats['kam'][-1]), tr(stats['sa'][-1]), tr(stats['sm'][-1]),
            big(ek['aday']), big(ek['mevcut']), tr(ratio(ek['aday'], ee['aday'])), tr(ratio(ek['mevcut'], ee['mevcut']))))
        for name, lo, hi in (('R%d–R30' % 23, 23, 30), ('R31–R40', 31, 40), ('R41–R50', 41, 50)):
            lo2, hi = max(lo, first), min(hi, last)
            if lo2 > hi:
                continue
            w = windows.setdefault(name, dict(a=[], m=[], am=[]))
            ka, km, kn = (total(arm[n], 'kills', lo2, hi) for n in ARMS)
            w['a'].append(ratio(ka, kn)); w['m'].append(ratio(km, kn)); w['am'].append(ratio(ka, km))
    out.append('')

    def line(name, key):
        v = [x for x in stats[key] if x == x]
        return '| %s | ×%s | ×%s – ×%s | %d / %d |' % (name, tr(med(v)), tr(min(v)), tr(max(v)), sum(1 for x in v if x > 1.0), len(v))

    out.append('| Oran (ödülden sonraki round\'lar) | Medyan | En düşük – en yüksek | Artış olan seed |')
    out.append('|---|---|---|---|')
    out.append(line('Hasat: aday ÷ almayan eş', 'ka'))
    out.append(line('Hasat: mevcut ÷ almayan eş', 'km'))
    out.append(line('Hasat: aday ÷ mevcut', 'kam'))
    out.append(line('Skor: aday ÷ almayan eş', 'sa'))
    out.append(line('Skor: mevcut ÷ almayan eş', 'sm'))
    out.append(line('Skor: aday ÷ mevcut', 'sam'))
    out.append('')
    out.append('| Round aralığı | Seed | Hasat: aday ÷ almayan (medyan) | mevcut ÷ almayan | aday ÷ mevcut |')
    out.append('|---|---|---|---|---|')
    for name, w in windows.items():
        out.append('| %s | %d | ×%s | ×%s | ×%s |' % (name, len(w['a']), tr(med(w['a'])), tr(med(w['m'])), tr(med(w['am']))))
    out.append('')


def main(argv):
    target = None
    if '--out' in argv:
        i = argv.index('--out'); target = argv[i + 1]; argv = argv[:i] + argv[i + 2:]
    out = ['# Birikimli run doğrulaması (Bölüm 3.7.5)', '',
           'Aynı seed, aynı politika; kollar: aday (Run50_KirilmaErisimiV1) · mevcut (Run50_BedelliOdullerV1) · almayan eş (aday profil, kırılma ödülünü almaz).',
           'Birikimli run sabit statlı laboratuvar değildir: ödülden sonra level, kart ve ödül seçimleri de ayrışır.', '']
    for path, reward in zip(argv[0::2], argv[1::2]):
        report(path, reward, out)
    text = '\n'.join(out) + '\n'
    if target:
        io.open(target, 'w', encoding='utf-8', newline='\n').write(text)
    else:
        sys.stdout.buffer.write(text.encode('utf-8'))


if __name__ == '__main__':
    main(sys.argv[1:])
