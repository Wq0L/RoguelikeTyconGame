# Bölüm 3.7.5 laboratuvar çözümleyicisi. KirilmaErisimMeasurement'ın lab CSV'lerini okur, kolları ödülsüz eşe göre karşılaştırır
# ve ön kayıtlı karar kuralını uygular. Yalnız okur; asset ya da parametre yazmaz.
#   python analyze_lab.py <klasör> [--md çıktı.md]
import csv, io, os, statistics as st, sys

ROUNDS = [23, 30, 40]
LAYOUTS = ['Dogrudan', 'Patlama', 'Elektrik', 'Karma', 'PatlamaSinerji', 'ElektrikSinerji']
LAYOUT_NAME = {'Dogrudan': 'Doğrudan ağırlıklı', 'Patlama': 'Patlama ağırlıklı', 'Elektrik': 'Elektrik ağırlıklı', 'Karma': 'Karma davranışlı',
               'PatlamaSinerji': 'Patlama · güçlü sinerji', 'ElektrikSinerji': 'Elektrik · güçlü sinerji'}
ARM_NAME = {'odulsuz': 'Ödülsüz', 'artci_A': 'Artçı A · 1,50 hücre (mevcut)', 'artci_B': 'Artçı B · 2,00 hücre', 'artci_C': 'Artçı C · 2,25 hücre',
            'cifte_A': 'Çifte Akım A · 2 hücre (mevcut)', 'cifte_B': 'Çifte Akım B · 3 hücre', 'cifte_C': 'Çifte Akım C · 4 hücre',
            'ritim': 'Hasat Ritmi', 'yikim': 'Yıkım Gücü ×1'}
TARGET = 1.5
KEEP_RATIO, KEEP_SEEDS = 1.10, 7


def load(folder):
    rows = []
    for r in ROUNDS:
        path = os.path.join(folder, 'KirilmaErisim_lab%d.csv' % r)
        if os.path.exists(path): rows += list(csv.DictReader(io.open(path, encoding='utf-8')))
    return rows


def num(v): return float(v) if v not in ('', None) else 0.0


def tr(v, d=2): return ('%.*f' % (d, v)).replace('.', ',')


def main():
    folder = sys.argv[1]
    rows = load(folder)
    out = []
    by = {}
    for r in rows: by[(int(r['round']), r['layout'], r['arm'], int(r['seed']))] = r
    seeds = sorted({int(r['seed']) for r in rows})

    def ratios(rnd, layout, arm, key='harvests'):
        vals = []
        for s in seeds:
            a, b = by.get((rnd, layout, 'odulsuz', s)), by.get((rnd, layout, arm, s))
            if a and b and num(a[key]) > 0: vals.append(num(b[key]) / num(a[key]))
        return vals

    def mean(rnd, layout, arm, key):
        vals = [num(by[(rnd, layout, arm, s)][key]) for s in seeds if (rnd, layout, arm, s) in by]
        return st.mean(vals) if vals else 0.0

    out.append('# Laboratuvar sonuçları (çözümleyici çıktısı)\n')
    out.append('%d round satırı · seed: %s · oranlar aynı seed\'in ödülsüz koluna göredir (medyan; parantezde en düşük – en yüksek; köşeli parantezde oranı ×1,00\'in üstünde olan seed sayısı).\n' % (len(rows), ', '.join(map(str, seeds))))
    for layout in LAYOUTS:
        arms = [a for a in ARM_NAME if a != 'odulsuz' and any((r, layout, a, seeds[0]) in by for r in ROUNDS)]
        if not arms: continue
        out.append('## %s\n' % LAYOUT_NAME[layout])
        out.append('| Kol | ' + ' | '.join('R%d hasat' % r for r in ROUNDS) + ' | ' + ' | '.join('R%d skor' % r for r in ROUNDS) + ' |')
        out.append('|---' * (1 + 2 * len(ROUNDS)) + '|')
        have = lambda r: (r, layout, 'odulsuz', seeds[0]) in by
        base = ' | '.join(tr(mean(r, layout, 'odulsuz', 'harvests'), 1) if have(r) else '–' for r in ROUNDS)
        out.append('| Ödülsüz (ortalama hasat / skor) | %s | %s |' % (base, ' | '.join(tr(mean(r, layout, 'odulsuz', 'score'), 0) if have(r) else '–' for r in ROUNDS)))
        for arm in arms:
            cells = []
            for r in ROUNDS:
                v = ratios(r, layout, arm)
                cells.append('×%s (%s–%s) [%d/%d]' % (tr(st.median(v)), tr(min(v)), tr(max(v)), sum(1 for x in v if x > 1.0), len(v)) if v else '–')
            for r in ROUNDS:
                v = ratios(r, layout, arm, 'score')
                cells.append('×%s' % tr(st.median(v)) if v else '–')
            out.append('| %s | %s |' % (ARM_NAME[arm], ' | '.join(cells)))
        out.append('')
        echo_arms = [a for a in arms if a.startswith('artci') or a.startswith('cifte')]
        if echo_arms:
            out.append('Yankının ayrıntısı (round başına ortalama):\n')
            out.append('| Kol | Round | Tetik | Yankının eriştiği hücre | Canlı hedef (yeni alanda) | Boş hücreye giden | Yankı hasadı (yeni alanda) | Yankı hasarı: vuruş / candan götüren / fazlası | Doğrudan + davranış hasadı |')
            out.append('|---|---|---|---|---|---|---|---|---|')
            for arm in echo_arms:
                for r in ROUNDS:
                    if (r, layout, arm, seeds[0]) not in by: continue
                    m = lambda k: mean(r, layout, arm, k)
                    cellsN, hits = m('echoCells'), m('echoHits')
                    out.append('| %s | R%d | %s | %s | %s (%s) | %s (%%%s) | %s (%s) | %s / %s / %s | %s + %s |' % (
                        ARM_NAME[arm], r, tr(m('echoExecuted'), 1), tr(cellsN, 1), tr(hits, 1), tr(m('echoHitsNewArea'), 1),
                        tr(cellsN - hits, 1), tr(100 * (cellsN - hits) / cellsN if cellsN else 0, 0),
                        tr(m('echoHarvests'), 1), tr(m('echoHarvestsNewArea'), 1),
                        tr(m('echoRaw'), 0), tr(m('echoApplied'), 0), tr(m('echoRaw') - m('echoApplied'), 0),
                        tr(m('harvestDirect'), 1), tr(m('harvests') - m('harvestDirect'), 1)))
            out.append('')

    # ---------------------------------------------------------------- karar kuralı
    out.append('## Karar kuralı (ön kayıtlı)\n')

    def verdict(layout, arm):
        meds = [st.median(ratios(r, layout, arm)) for r in ROUNDS if ratios(r, layout, arm)]
        return meds, sum(1 for m in meds if m >= TARGET)

    out.append('**Hedef:** güçlü sinerji düzeninde medyan toplam hasat oranı ≥ ×%s, üç round\'un en az ikisinde.\n' % tr(TARGET, 1))
    out.append('| Ödül | Düzen | Aday | R23 | R30 | R40 | Hedefi tutan round | Sonuç |')
    out.append('|---|---|---|---|---|---|---|---|')
    choice = {}
    for name, layout, arms in (('Artçı Patlama', 'PatlamaSinerji', ['artci_A', 'artci_B', 'artci_C']), ('Çifte Akım', 'ElektrikSinerji', ['cifte_A', 'cifte_B', 'cifte_C'])):
        passing = []
        for arm in arms:
            meds, hit = verdict(layout, arm)
            if len(meds) < len(ROUNDS): continue
            ok = hit >= 2
            if ok and arm[-1] != 'A': passing.append(arm)
            out.append('| %s | %s | %s | ×%s | ×%s | ×%s | %d / 3 | %s |' % (name, LAYOUT_NAME[layout], ARM_NAME[arm], tr(meds[0]), tr(meds[1]), tr(meds[2]), hit, 'tuttu' if ok else 'tutmadı'))
        candidates = [a for a in arms if a[-1] != 'A' and len(verdict(layout, a)[0]) == len(ROUNDS)]
        if passing: choice[name] = (passing[0], 'hedefi tutan en küçük erişim')
        elif candidates:
            best = max(candidates, key=lambda a: st.mean(verdict(layout, a)[0]))
            choice[name] = (best, 'hedef tutmadı; gerçek hasat katkısı en yüksek aday')
    out.append('')
    for name, (arm, why) in choice.items(): out.append('- **%s:** %s — %s.' % (name, ARM_NAME[arm], why))
    out.append('')
    if 'Çifte Akım' in choice:
        arm = choice['Çifte Akım'][0]
        out.append('**Çifte Akım havuzda kalır mı?** Kural: Elektrik ağırlıklı ya da Elektrik güçlü sinerji düzeninde, üç round\'un en az ikisinde medyan oran ≥ ×%s ve en az %d seed\'de oran > ×1,00.\n' % (tr(KEEP_RATIO), KEEP_SEEDS))
        out.append('| Düzen | Aday | R23 | R30 | R40 | Koşulu sağlayan round |')
        out.append('|---|---|---|---|---|---|')
        keep = False
        for layout in ('Elektrik', 'ElektrikSinerji'):
            cells, ok = [], 0
            for r in ROUNDS:
                v = ratios(r, layout, arm)
                if not v: cells.append('–'); continue
                good = st.median(v) >= KEEP_RATIO and sum(1 for x in v if x > 1.0) >= KEEP_SEEDS
                ok += good
                cells.append('×%s [%d/%d]%s' % (tr(st.median(v)), sum(1 for x in v if x > 1.0), len(v), ' ✓' if good else ''))
            keep = keep or ok >= 2
            out.append('| %s | %s | %s | %d / 3 |' % (LAYOUT_NAME[layout], ARM_NAME[arm], ' | '.join(cells), ok))
        out.append('\n**Sonuç:** Çifte Akım yeni profilin havuzunda %s.\n' % ('KALIR' if keep else 'KALMAZ (yalnız yeni profilin havuzundan çıkarılır)'))
    text = '\n'.join(out) + '\n'
    if '--md' in sys.argv: io.open(sys.argv[sys.argv.index('--md') + 1], 'w', encoding='utf-8', newline='\n').write(text)
    else: sys.stdout.buffer.write(text.encode('utf-8'))


main()
