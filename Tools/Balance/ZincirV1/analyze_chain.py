"""Bölüm 3.7.6 — zincir ölçümlerinin çözümleyicisi.

  python analyze_chain.py lab <klasör>      → laboratuvar (KirilmaErisim_zincir23/30/40.csv): zincir kapalı / açık, aynı seed
  python analyze_chain.py runs <csv> [csv…] → birikimli tam run (BalanceRuns_k376zincir*.csv): aday / almayan eş, aynı seed
Çıktı Markdown'dur (stdout). Karar kuralı yoktur: sonuç rapor edilir.
"""
import csv
import io
import os
import statistics
import sys

LAYOUTS = ['Patlama', 'Elektrik', 'KasirgaBumerang', 'Karma', 'Davranissiz']
NAMES = {'Patlama': 'Patlama ağırlıklı', 'Elektrik': 'Elektrik ağırlıklı', 'KasirgaBumerang': 'Kasırga / bumerang ağırlıklı',
         'Karma': 'Karma davranışlı', 'Davranissiz': 'Davranışsız (kontrol)',
         'PatlamaSinerji': 'Patlama · güçlü sinerji (ek)', 'ElektrikSinerji': 'Elektrik · güçlü sinerji (ek)'}


def tr(x, d=2):
    return ('%.*f' % (d, x)).replace('.', ',') if x == x else '–'


def big(x):
    return '{:,}'.format(int(round(x))).replace(',', '.')


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def med(values):
    values = [v for v in values if v == v]
    return statistics.median(values) if values else float('nan')


def mean(values):
    values = [v for v in values if v == v]
    return statistics.mean(values) if values else float('nan')


def ratio(a, b):
    return a / b if b > 0 else float('nan')


def lab(folder, pattern='KirilmaErisim_zincir'):
    out = ['# Zincir laboratuvarı (Bölüm 3.7.6)', '',
           'Sabit tarla ve sabit statlarla tek round; bot oyunun saldırı yoluyla vurur. Kollar: zincir kapalı ("zincirsiz") ve açık ("zincir",',
           'Zincir Hasat asset\'inin gerçek değerleri). Aynı düzen, aynı stat, aynı seed. Oranlar aynı seed\'in zincirsiz koluna göredir;',
           'medyan (en düşük – en yüksek) · artış olan seed sayısı.', '']
    rows = []
    for name in sorted(os.listdir(folder)):
        if name.startswith(pattern) and name.endswith('.csv'): rows += list(csv.DictReader(io.open(os.path.join(folder, name), encoding='utf-8')))
    by = {(int(x['round']), x['layout'], int(x['seed']), x['arm']): x for x in rows}
    rounds = sorted({int(x['round']) for x in rows})
    present = {x['layout'] for x in rows}
    layouts = [l for l in LAYOUTS + ['PatlamaSinerji', 'ElektrikSinerji'] if l in present]
    out.append('## Toplam hasat ve skor (zincir ÷ zincirsiz)')
    out.append('')
    out.append('| Düzen | ' + ' | '.join('R%d hasat' % r for r in rounds) + ' | ' + ' | '.join('R%d skor' % r for r in rounds) + ' |')
    out.append('|---|' + '---|' * (2 * len(rounds)))
    for layout in layouts:
        cells_h, cells_s = [], []
        for r in rounds:
            pairs = [(by.get((r, layout, s, 'zincir')), by.get((r, layout, s, 'zincirsiz'))) for s in range(1, 11)]
            pairs = [(a, b) for a, b in pairs if a and b]
            rh = [ratio(num(a['harvests']), num(b['harvests'])) for a, b in pairs]
            rs = [ratio(num(a['score']), num(b['score'])) for a, b in pairs]
            cells_h.append('×%s (%s–%s) · %d/%d' % (tr(med(rh)), tr(min(rh)), tr(max(rh)), sum(1 for x in rh if x > 1), len(rh)) if rh else '–')
            cells_s.append('×%s' % tr(med(rs)) if rs else '–')
        out.append('| %s | %s | %s |' % (NAMES[layout], ' | '.join(cells_h), ' | '.join(cells_s)))
    out.append('')
    out.append('## Hasatın kaynağı ve nesiller (zincir kolu, round başına ortalama)')
    out.append('')
    out.append('| Düzen | Round | Toplam hasat (zincirsiz → zincir) | Doğrudan | Normal davranış | Zincir nesil 1 | Zincir nesil 2 | Zincir payı | Deneme 1 / 2 | Tetik 1 / 2 | Yürütülen 1 / 2 | Zincir vuruşu (yeni hücreye) |')
    out.append('|---|---|---|---|---|---|---|---|---|---|---|---|')
    for layout in layouts:
        for r in rounds:
            on = [by[(r, layout, s, 'zincir')] for s in range(1, 11) if (r, layout, s, 'zincir') in by]
            off = [by[(r, layout, s, 'zincirsiz')] for s in range(1, 11) if (r, layout, s, 'zincirsiz') in by]
            if not on: continue
            m = lambda rows, c: mean([num(x[c]) for x in rows])
            total_on = m(on, 'harvests')
            chain = m(on, 'killsGen1') + m(on, 'killsGen2')
            out.append('| %s | R%d | %s → %s | %s | %s | %s | %s | %%%s | %s / %s | %s / %s | %s / %s | %s (%s) |' % (
                NAMES[layout], r, tr(m(off, 'harvests'), 1), tr(total_on, 1), tr(m(on, 'killsDirect'), 1), tr(m(on, 'killsNormal'), 1),
                tr(m(on, 'killsGen1'), 1), tr(m(on, 'killsGen2'), 1), tr(100 * chain / total_on if total_on else float('nan'), 1),
                tr(m(on, 'attempts1'), 1), tr(m(on, 'attempts2'), 1), tr(m(on, 'triggers1'), 1), tr(m(on, 'triggers2'), 1),
                tr(m(on, 'fired1'), 1), tr(m(on, 'fired2'), 1), tr(m(on, 'chainHits'), 1), tr(m(on, 'newTargetHits'), 1)))
    out.append('')
    out.append('## Retler, kuyruk ve havuz (zincir kolu, round başına ortalama; en çok değerler bütün seedlerin en büyüğü)')
    out.append('')
    out.append('| Düzen | Round | Tekrar | Nesil sınırı | Kök bütçesi | Geçersiz kaynak | Round sonu | Artçı / ikinci dalga | En çok bekleyen iş | Gecikme ort. / en çok (kare) | Havuz bekleyen iş / iş-kare | Kaynak vurgusu çizilen / atlanan |')
    out.append('|---|---|---|---|---|---|---|---|---|---|---|---|')
    for layout in layouts:
        for r in rounds:
            on = [by[(r, layout, s, 'zincir')] for s in range(1, 11) if (r, layout, s, 'zincir') in by]
            if not on: continue
            m = lambda c: mean([num(x[c]) for x in on])
            mx = lambda c: max(num(x[c]) for x in on)
            fired = sum(num(x['fired1']) + num(x['fired2']) for x in on)
            delay = sum(num(x['delayFramesTotal']) for x in on) / fired if fired else float('nan')
            out.append('| %s | R%d | %s | %s | %s | %s | %s | %s | %s | %s / %s | %s / %s | %s / %s |' % (
                NAMES[layout], r, tr(m('rejRepeat'), 1), tr(m('rejGeneration'), 1), tr(m('rejBudget'), 1), tr(m('rejInvalid'), 1), tr(m('rejRoundEnd'), 1),
                tr(m('rejExcluded'), 1), big(mx('maxPending')), tr(delay, 1), big(mx('delayFramesMax')), tr(m('poolWaitJobs'), 1), tr(m('poolWaitFrames'), 1),
                tr(m('chainPlayed'), 1), tr(m('chainSkipped'), 1)))
    out.append('')
    out.append('## Görsel atlamaları ve kare maliyeti (round başına ortalama; batch\'te çizim yok, oyun kodunun kare maliyeti)')
    out.append('')
    out.append('| Düzen | Round | ms/kare zincirsiz → zincir | En uzun kare zincirsiz / zincir (ms) | GC toplaması | Atlanan patlama görseli | Atlanan elektrik görseli | Çizilmeyen şimşek | Başlatılamayan bumerang (normal) |')
    out.append('|---|---|---|---|---|---|---|---|---|')
    for layout in layouts:
        for r in rounds:
            on = [by[(r, layout, s, 'zincir')] for s in range(1, 11) if (r, layout, s, 'zincir') in by]
            off = [by[(r, layout, s, 'zincirsiz')] for s in range(1, 11) if (r, layout, s, 'zincirsiz') in by]
            if not on: continue
            ms = lambda rows: mean([num(x['wallMs']) / num(x['frames']) for x in rows if num(x['frames']) > 0])
            mx = lambda rows: max(num(x['maxFrameMs']) for x in rows)
            m = lambda rows, c: mean([num(x[c]) for x in rows])
            out.append('| %s | R%d | %s → %s | %s / %s | %s / %s | %s / %s | %s / %s | %s / %s | %s / %s |' % (
                NAMES[layout], r, tr(ms(off), 3), tr(ms(on), 3), tr(mx(off), 1), tr(mx(on), 1),
                big(sum(num(x['gc0']) for x in off)), big(sum(num(x['gc0']) for x in on)),
                tr(m(off, 'skipExplosionVisual'), 1), tr(m(on, 'skipExplosionVisual'), 1), tr(m(off, 'skipElectricVisual'), 1), tr(m(on, 'skipElectricVisual'), 1),
                tr(m(off, 'skipElectricBolts'), 1), tr(m(on, 'skipElectricBolts'), 1), tr(m(off, 'skipBoomerang'), 1), tr(m(on, 'skipBoomerang'), 1)))
    out.append('')
    return out


def runs(paths):
    rows = []
    for path in paths: rows += list(csv.DictReader(io.open(path, encoding='utf-8')))
    out = ['# Birikimli tam run (Bölüm 3.7.6)', '',
           'XP, kartlar, ağaç ve ödüller ilerler. Kollar: aday (Zincir Hasat teklif edilince alınır) ve almayan eş (yalnız Zincir Hasat',
           'alınmaz; diğer seçimler aynı politikayla). Aynı seed. Karşılaştırma ödülün alındığı round\'dan sonraki round\'lar; run toplamı değil.',
           '"bitmedi": run sonuç satırı yok (ölçüm durdu). Seviye: run\'ın son kaydındaki oyuncu seviyesi. Bitmeyen ya da sonucu',
           'farklı türden çiftler özet satırına girmez; tabloda kalır.', '']
    runs = {}
    for x in rows:
        k = (x['policy'], x['label'], int(x['seed']))
        r = runs.setdefault(k, dict(rounds={}, outcome='bitmedi', end=0))
        if x['outcome']: r['outcome'], r['end'] = x['outcome'], int(x['round'])
        else: r['rounds'][int(x['round'])] = x
    for r in runs.values():
        if r['outcome'] == 'bitmedi' and r['rounds']: r['end'] = max(r['rounds'])
    level = lambda run: big(num(run['rounds'][max(run['rounds'])]['level'])) if run['rounds'] else '–'
    for policy in sorted({k[0] for k in runs}):
        out.append('## ' + policy)
        out.append('')
        out.append('| Seed | Zincir Hasat (aday) | Sonuç aday / eş | Seviye aday / eş | Ödülden sonra hasat aday / eş | Oran | Skor oranı | Zincir hasadı (aday, ödülden sonra) | Öncesi aynı mı |')
        out.append('|---|---|---|---|---|---|---|---|---|')
        ratios, scores, excluded = [], [], []
        for seed in sorted({k[2] for k in runs if k[0] == policy}):
            a = runs.get((policy, 'aday', seed)); b = runs.get((policy, 'almayan eş', seed))
            if not a or not b: continue
            taken = None
            for r in sorted(a['rounds']):
                for item in (a['rounds'][r].get('breakthrough') or '').split():
                    if item.startswith('zincir_hasat@R'): taken = int(item.split('@R')[1])
                if taken: break
            if not taken:
                out.append('| %d | alınmadı | %s R%d / %s R%d | %s / %s | | | | | |' % (seed, a['outcome'], a['end'], b['outcome'], b['end'], level(a), level(b)))
                continue
            first = taken + 1
            ka = sum(num(x['kills']) for r, x in a['rounds'].items() if r >= first); kb = sum(num(x['kills']) for r, x in b['rounds'].items() if r >= first)
            sa = sum(num(x['score']) for r, x in a['rounds'].items() if r >= first); sb = sum(num(x['score']) for r, x in b['rounds'].items() if r >= first)
            chain = sum(num(x.get('chainKills')) for r, x in a['rounds'].items() if r >= first)
            same = all(a['rounds'][r]['kills'] == b['rounds'][r]['kills'] and a['rounds'][r]['score'] == b['rounds'][r]['score']
                       for r in range(1, taken + 1) if r in a['rounds'] and r in b['rounds'])
            counted = 'bitmedi' not in (a['outcome'], b['outcome']) and a['outcome'] == b['outcome']
            if counted: ratios.append(ratio(ka, kb)); scores.append(ratio(sa, sb))
            else: excluded.append(seed)
            out.append('| %d | R%d | %s R%d / %s R%d | %s / %s | %s / %s | ×%s | ×%s | %s (%%%s) | %s |' % (
                seed, taken, a['outcome'], a['end'], b['outcome'], b['end'], level(a), level(b), big(ka), big(kb), tr(ratio(ka, kb)), tr(ratio(sa, sb)),
                big(chain), tr(100 * chain / ka if ka else float('nan'), 1), 'evet' if same else 'HAYIR'))
        out.append('')
        if ratios:
            out.append('Ödülü alan ve iki kolu aynı sonuçla biten %d seed: hasat ×%s (%s–%s; %d / %d artış) · skor ×%s.' % (
                len(ratios), tr(med(ratios)), tr(min(ratios)), tr(max(ratios)), sum(1 for x in ratios if x > 1), len(ratios), tr(med(scores))))
        if excluded:
            out.append('Özete girmeyen seed: %s.' % ', '.join(str(s) for s in excluded))
        out.append('')
    return out


if __name__ == '__main__':
    kind, target = sys.argv[1], sys.argv[2]
    lines = lab(target, sys.argv[3] if len(sys.argv) > 3 else 'KirilmaErisim_zincir') if kind == 'lab' else runs(sys.argv[2:])
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))
