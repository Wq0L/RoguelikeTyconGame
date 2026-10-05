"""Bölüm 3.7.6.1–2 — aynı seed ve koşullardaki iki ölçümün karşılaştırması (A ↔ P6, B ↔ A).

  python compare.py runs <eski.csv[,eski2.csv]> <yeni.csv[,yeni2.csv]> [--skip policy:label:seed ...]
  python compare.py lab  <eski klasör> <yeni klasör> [desen]
  python compare.py rewards <eski_rewards.csv[,…]> <yeni_rewards.csv[,…]> [--skip …]

Satırlar anahtarla eşlenir (run: politika, kol, seed, round; laboratuvar: round, düzen, seed, kol). Karşılaştırılan sütunlar iki
dosyada da olan sütunlardır; zaman ölçümü (duvar saati, kare süresi, GC, allocation) ve run sıra numarası dışarıda kalır.
Çıktı Markdown'dur: özdeş satır sayısı, farklı satırlar ve farklı sütunlar. Karar kuralı yoktur.
"""
import csv
import io
import os
import sys

TIMING = {'wallMs', 'maxFrameMs', 'gc0', 'allocBytes', 'run'}
# Yalnız görsel sayaç: patlama görseli parçacıkları sönünce havuza döner (VFXManager). Havuz (121) dolduğunda atlanan görsel sayısı
# parçacık benzetimine bağlıdır ve oyun durumuyla tam belirlenmez: seed 101 çiftinde aynı kodun iki çalıştırmasında aynı, ama P6,
# A ve B arasında farklı çıktı (yalnız havuzun dolduğu R47–R50'de; patlama hasatları aynı). Nedeni doğrulanmadı. Hasar ve oynanış
# bundan bağımsızdır. Bu sütun oynanış farkı sayılmaz ama farkı ayrı satırda raporlanır.
VISUAL = {'skipExplosionVisual'}


def read(paths):
    rows = []
    for path in paths.split(','):
        rows += list(csv.DictReader(io.open(path, encoding='utf-8')))
    return rows


def key_run(x):
    return (x['policy'], x['label'], int(x['seed']), int(x['round']), x.get('outcome', '') or '')


def compare(old, new, key, skip=lambda x: False, title=''):
    old = [x for x in old if not skip(x)]
    new = [x for x in new if not skip(x)]
    a = {key(x): x for x in old}
    b = {key(x): x for x in new}
    cols = [c for c in (old[0].keys() if old else []) if new and c in new[0] and c not in TIMING and c not in VISUAL]
    visual_cols = [c for c in (old[0].keys() if old else []) if new and c in new[0] and c in VISUAL]
    same, diff, missing, extra, columns, visual = 0, [], sorted(set(a) - set(b)), sorted(set(b) - set(a)), {}, 0
    for k in sorted(set(a) & set(b)):
        bad = [c for c in cols if (a[k][c] or '') != (b[k][c] or '')]
        if any((a[k][c] or '') != (b[k][c] or '') for c in visual_cols): visual += 1
        if bad:
            diff.append((k, bad))
            for c in bad: columns[c] = columns.get(c, 0) + 1
        else:
            same += 1
    out = ['## ' + title, '', '%d eşleşen satır: %d özdeş, %d farklı · yalnız eskide %d · yalnız yenide %d · karşılaştırılan sütun %d' % (
        same + len(diff), same, len(diff), len(missing), len(extra), len(cols)), '']
    if visual_cols:
        out.append('Yalnız görsel sayaç (%s) %d satırda farklı; oynanış farkı sayılmadı.' % (', '.join(visual_cols), visual))
        out.append('')
    if columns:
        out.append('Farklı sütunlar: ' + ', '.join('%s (%d)' % (c, n) for c, n in sorted(columns.items(), key=lambda t: -t[1])))
        out.append('')
        for k, bad in diff[:40]:
            out.append('- %s: %s' % (' · '.join(str(p) for p in k if p != ''), ', '.join('%s %s → %s' % (c, a[k][c], b[k][c]) for c in bad[:6])))
        if len(diff) > 40: out.append('- … %d satır daha' % (len(diff) - 40))
        out.append('')
    for name, ks in (('yalnız eskide', missing), ('yalnız yenide', extra)):
        if ks:
            out.append('%s: %s' % (name, '; '.join(' · '.join(str(p) for p in k if p != '') for k in ks[:20])))
            out.append('')
    return out


def skipper(args):
    skips = set()
    for i, a in enumerate(args):
        if a == '--skip':
            for s in args[i + 1:]:
                if s.startswith('--'): break
                p, l, seed = s.split(':')
                skips.add((p, l, int(seed)))
    return lambda x: (x['policy'], x['label'], int(x['seed'])) in skips


if __name__ == '__main__':
    kind = sys.argv[1]
    if kind == 'runs':
        lines = compare(read(sys.argv[2]), read(sys.argv[3]), key_run, skipper(sys.argv[4:]), 'Birikimli run satırları')
    elif kind == 'rewards':
        k = lambda x: (x['policy'], x['label'], int(x['seed']), int(x['round']))
        lines = compare(read(sys.argv[2]), read(sys.argv[3]), k, skipper(sys.argv[4:]), 'Boss ödülü teklifleri ve seçimleri')
    else:
        pattern = sys.argv[4] if len(sys.argv) > 4 else 'KirilmaErisim_'
        def files(folder):
            return ','.join(os.path.join(folder, n) for n in sorted(os.listdir(folder)) if n.startswith(pattern) and n.endswith('.csv'))
        k = lambda x: (int(x['round']), x['layout'], int(x['seed']), x['arm'], x.get('repeat', ''))
        lines = compare(read(files(sys.argv[2])), read(files(sys.argv[3])), k, title='Laboratuvar satırları (' + pattern + ')')
    sys.stdout.buffer.write(('\n'.join(lines) + '\n').encode('utf-8'))
