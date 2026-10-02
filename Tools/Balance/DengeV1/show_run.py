# Bir ölçüm setinin round satırlarını okunur tablo olarak gösterir.  kullanım: show_run.py <set> [run numaraları…] [--cols a,b,c]
import csv, sys, io, os
LOGS = os.environ.get("BALANCE_LOGS", r"C:SERSSERDESKTOPGAMESGITHUBCLICKERGAMEibraryverificationprojectogs")
name = sys.argv[1]
args = sys.argv[2:]
cols = None
if '--cols' in args:
    i = args.index('--cols'); cols = args[i + 1].split(','); args = args[:i] + args[i + 2:]
runs = set(args)
rows = list(csv.reader(io.open(os.path.join(LOGS, 'BalanceRuns_%s.csv' % name), encoding='utf-8')))
head = rows[0]
default = ['round', 'boss', 'kills', 'direct', 'score', 'segScore', 'bossScore', 'gold', 'iron', 'stone', 'bankGold', 'bankIron', 'bankStone', 'xp', 'level', 'cards',
           'damage', 'directMult', 'interval', 'radius', 'reachCells', 'attacks', 'hitsPerAttack', 'htkCommon', 'htkRare', 'htkLegendary', 'osCommon', 'ttkCommon',
           'planters', 'points', 'fill', 'capacity', 'tiersBought', 'treeTiers', 'best5s', 'rewards']
cols = cols or default
last = None
for r in rows[1:]:
    if runs and r[0] not in runs: continue
    if len(r) < 12:
        print('   >>> run %s sonucu: %s @R%s' % (r[0], r[8], r[7])); continue
    d = dict(zip(head, r))
    key = (d['run'], d['policy'], d['farmer'], d['scythe'], d['seed'], d['label'])
    if key != last:
        print('\n=== run %s | %s | %s+%s | seed %s | %s' % key)
        print(' | '.join(cols)); last = key
    print(' | '.join(d.get(c, '?') for c in cols))
