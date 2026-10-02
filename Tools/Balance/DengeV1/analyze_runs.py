# BalanceRuns_<set>.csv özetleri.  kullanım: analyze_runs.py <set> [bölüm…]   bölümler: outcome curve hits income nodes rewards time
import csv, sys, io, os, statistics as st
from collections import defaultdict, OrderedDict
_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
LOGS = os.environ.get('BALANCE_LOGS', os.path.join(_REPO, 'Library', 'VerificationProject', 'Logs'))
name = sys.argv[1]
sections = sys.argv[2:] or ['outcome', 'curve', 'hits', 'income', 'nodes', 'rewards', 'time']
CHECK = [1, 3, 5, 8, 10, 15, 20, 25, 30, 35, 40, 45, 50]

def read(suffix=''):
    path = os.path.join(LOGS, 'BalanceRuns_%s%s.csv' % (name, suffix))
    rows = list(csv.reader(io.open(path, encoding='utf-8')))
    return rows[0], rows[1:]

head, rows = read()
runs = OrderedDict()   # run id -> dict(meta, rounds{round: row dict}, outcome, end)
for r in rows:
    rid = r[0]
    run = runs.setdefault(rid, dict(rounds={}, outcome='?', end=0))
    run['meta'] = (r[2], r[3], r[4], r[6])   # policy, farmer, scythe, label
    run['seed'] = r[5]
    if len(r) < 12:
        run['outcome'] = r[8]; run['end'] = int(r[7]); continue
    d = dict(zip(head, r))
    run['rounds'][int(d['round'])] = d

def f(v):
    try: return float(v)
    except Exception: return None

groups = OrderedDict()
for rid, run in runs.items(): groups.setdefault(run['meta'], []).append(run)

def gname(meta):
    policy, farmer, scythe, label = meta
    return '%s · %s+%s%s' % (policy, farmer, scythe, (' · ' + label) if label else '')

def med(values, fmt='%.0f'):
    v = [x for x in values if x is not None]
    if not v: return '-'
    return fmt % st.median(v)

def rng(values, fmt='%.0f'):
    v = [x for x in values if x is not None]
    if not v: return '-'
    return (fmt + ' [' + fmt + '–' + fmt + ']') % (st.median(v), min(v), max(v))

def col(group, rnd, key):
    return [f(run['rounds'][rnd][key]) for run in group if rnd in run['rounds']]

if 'outcome' in sections:
    print('=== SONUÇLAR (elenen run dahil) ===')
    for meta, group in groups.items():
        outs = defaultdict(list)
        for run in group: outs[run['outcome']].append(run['end'])
        wins = len(outs.get('KAZANDI', []))
        detail = ' · '.join('%s %d (R%s)' % (k, len(v), ','.join(str(x) for x in sorted(v))) for k, v in outs.items() if k != 'KAZANDI')
        final = [f(run['rounds'][max(run['rounds'])]['treeTiers']) for run in group if run['rounds']]
        lvl = [f(run['rounds'][max(run['rounds'])]['level']) for run in group if run['rounds']]
        print('%-52s kazanan %d/%d %s | ağaç kademesi %s | level %s' % (gname(meta), wins, len(group), ('| ' + detail) if detail else '', rng(final), rng(lvl)))

if 'curve' in sections:
    print('\n=== EĞRİ (medyan; run o round\'a ulaştıysa) ===')
    for meta, group in groups.items():
        print('--- ' + gname(meta))
        print('R  | n | hasat (doğr/davr) | skor | seg skor/hedef | boss skor/hedef | hasar×çarpan | aralık | yarıçap(hücre) | vuruş/saldırı | doluluk | hasat/kapasite | 5sn en çok | saksı/nokta | ağaç | lvl | kart')
        for rnd in CHECK:
            n = sum(1 for run in group if rnd in run['rounds'])
            if n == 0: continue
            c = lambda k: col(group, rnd, k)
            kills = c('kills'); direct = c('direct')
            dmg = [a * b for a, b in zip(c('damage'), c('directMult'))]
            cap = [k / q if q else None for k, q in zip(c('kills'), c('capacity'))]
            seg = '%s/%s' % (med(c('segScore')), med(c('segTarget')))
            boss = '%s/%s' % (med(c('bossScore')), med(c('bossTarget'))) if st.median(c('bossTarget')) > 0 else '-'
            behav = [k - d for k, d in zip(kills, direct)]
            print('%-2d | %d | %s (%s/%s) | %s | %s | %s | %s | %s | %s(%s) | %s | %s | %s | %s | %s/%s | %s | %s | %s' % (
                rnd, n, rng(kills), med(direct), med(behav), rng(c('score')), seg, boss, med(dmg, '%.1f'), med(c('interval'), '%.2f'),
                med(c('radius'), '%.2f'), med(c('reachCells')), med(c('hitsPerAttack'), '%.1f'), med(c('fill'), '%.2f'), med(cap, '%.2f'), med(c('best5s')),
                med(c('planters')), med(c('points')), med(c('treeTiers')), med(c('level')), med(c('cards'))))

if 'hits' in sections:
    print('\n=== VURUŞ SAYISI · TEK VURUŞ % · HASAT SÜRESİ sn (Common / Uncommon / Rare / Epic / Legendary; medyan) ===')
    R = ['Common', 'Uncommon', 'Rare', 'Epic', 'Legendary']
    for meta, group in groups.items():
        print('--- ' + gname(meta))
        for rnd in CHECK:
            if not any(rnd in run['rounds'] for run in group): continue
            h = ' / '.join(med(col(group, rnd, 'htk' + r), '%.1f') for r in R)
            o = ' / '.join(med(col(group, rnd, 'os' + r)) for r in R)
            t = ' / '.join(med(col(group, rnd, 'ttk' + r), '%.1f') for r in R)
            print('R%-2d | vuruş %s | tek vuruş %% %s | süre %s' % (rnd, h, o, t))

if 'income' in sections:
    print('\n=== GELİR (hasat geliri, round başına medyan · o round\'a kadar birikimli medyan) ===')
    for meta, group in groups.items():
        print('--- ' + gname(meta))
        for rnd in CHECK:
            alive = [run for run in group if rnd in run['rounds']]
            if not alive: continue
            def cum(k): return [sum(f(run['rounds'][x][k]) for x in run['rounds'] if x <= rnd) for run in alive]
            print('R%-2d | altın %s (Σ %s) | demir %s (Σ %s) | taş %s (Σ %s) | XP %s (Σ %s) | kasa %s/%s/%s' % (
                rnd, med(col(alive, rnd, 'gold')), med(cum('gold')), med(col(alive, rnd, 'iron')), med(cum('iron')), med(col(alive, rnd, 'stone')), med(cum('stone')),
                med(col(alive, rnd, 'xp')), med(cum('xp')), med(col(alive, rnd, 'bankGold')), med(col(alive, rnd, 'bankIron')), med(col(alive, rnd, 'bankStone'))))

if 'nodes' in sections:
    print('\n=== ERİŞİM: aile ilk kademe / son kademe round\'u (medyan; parantez: aileyi tamamlayan run sayısı) ===')
    nh, nrows = read('_nodes')
    fam_tiers = defaultdict(int)
    per = defaultdict(lambda: defaultdict(list))   # meta -> family -> list of (run, round, node, level)
    for r in nrows:
        meta = (r[1], r[2], r[3], r[5]); node = r[7]
        fam = node.rsplit(' - ', 1)[0] if ' - ' in node else node
        per[meta][fam].append((r[0], int(r[6]), node, int(r[8])))
    fams = []
    for meta in per:
        for fam in per[meta]:
            if fam not in fams: fams.append(fam)
    metas = list(groups.keys())
    for fam in fams:
        cells = []
        for meta in metas:
            entries = per[meta].get(fam, [])
            by_run = defaultdict(list)
            for run, rnd, node, level in entries: by_run[run].append(rnd)
            firsts = [min(v) for v in by_run.values()]
            lasts = [max(v) for v in by_run.values()]
            counts = [len(v) for v in by_run.values()]
            n = len(groups[meta])
            cells.append('%s→%s ×%s (%d/%d)' % (med(firsts), med(lasts), med(counts), len(by_run), n) if by_run else '–')
        print('%-24s | %s' % (fam, ' | '.join(cells)))
    print('sütunlar: ' + ' | '.join(gname(m) for m in metas))

if 'rewards' in sections:
    print('\n=== ÖDÜLLER (seçilen; run sonu) ===')
    rh, rrows = read('_rewards')
    for meta, group in groups.items():
        picks = defaultdict(int); offers = defaultdict(int)
        for r in rrows:
            if (r[1], r[2], r[3], r[5]) != meta: continue
            picks[r[8]] += 1
            for o in r[7].split(): offers[o] += 1
        print('%-52s %s' % (gname(meta), ', '.join('%s %d/%d' % (k, v, offers.get(k, 0)) for k, v in sorted(picks.items(), key=lambda x: -x[1]))))

if 'time' in sections:
    print('\n=== SÜRE (hasat süresi ölçüldü; menü süresi VARSAYIM: round başına 25 sn + kart ekranı 10 sn + ödül ekranı 15 sn + alınan kademe 4 sn) ===')
    nh, nrows = read('_nodes')
    tiers = defaultdict(int)
    for r in nrows: tiers[r[0]] += 1
    for meta, group in groups.items():
        tot = []; harv = []; cards_ = []
        for run in group:
            rid = [k for k, v in runs.items() if v is run][0]
            rounds = len(run['rounds'])
            harvest = sum(f(d['duration']) for d in run['rounds'].values())
            cards = sum(f(d['cards']) for d in run['rounds'].values())
            bosses = sum(1 for d in run['rounds'].values() if f(d['bossTarget']))
            total = harvest + rounds * 25 + cards * 10 + bosses * 15 + tiers[rid] * 4
            tot.append(total / 60.0); harv.append(harvest / 60.0); cards_.append(cards)
        print('%-52s round %s | hasat %s dk | kart ekranı %s | tahmini oturum %s dk' % (gname(meta), rng([len(r['rounds']) for r in group]), rng(harv, '%.1f'), rng(cards_), rng(tot, '%.0f')))
