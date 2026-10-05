"""İki Unity sahne dosyasını nesne bloklarına (--- !u!sınıf &fileID) ayırıp karşılaştırır."""
import io, re, sys, difflib

def blocks(path):
    text = io.open(path, encoding='utf-8', newline='').read().replace('\r\n', '\n')
    parts = re.split(r'(?m)^(--- !u!\d+ &\d+.*)$', text)
    out, order = {}, []
    for i in range(1, len(parts), 2):
        m = re.match(r'--- !u!(\d+) &(\d+)', parts[i])
        key = m.group(2)
        out[key] = (m.group(1), parts[i + 1])
        order.append(key)
    return parts[0], out, order

def name_of(body):
    m = re.search(r'(?m)^  m_Name: (.*)$', body)
    kind = body.strip().split(':', 1)[0]
    ident = re.search(r'm_EditorClassIdentifier: (.*)', body)
    return kind + (' "' + m.group(1) + '"' if m and m.group(1) else '') + (' [' + ident.group(1).split('::')[-1] + ']' if ident and ident.group(1).strip() else '')

old_head, old, old_order = blocks(sys.argv[1])
new_head, new, new_order = blocks(sys.argv[2])
removed = [k for k in old_order if k not in new]
added = [k for k in new_order if k not in old]
changed = [k for k in old_order if k in new and old[k] != new[k]]
print('blok: eski %d, yeni %d · silinen %d · eklenen %d · içeriği değişen %d · başlık aynı: %s' % (len(old), len(new), len(removed), len(added), len(changed), old_head == new_head))
kinds = {}
for k in removed:
    n = name_of(old[k][1]); kinds[n.split(' ')[0]] = kinds.get(n.split(' ')[0], 0) + 1
print('silinen blok türleri:', ', '.join('%s %d' % kv for kv in sorted(kinds.items())))
print('silinen GameObject adları:', ', '.join(sorted(name_of(old[k][1]) for k in removed if old[k][0] == '1')))
print('silinen MonoBehaviour türleri:', ', '.join(sorted({name_of(old[k][1]) for k in removed if old[k][0] == '114'})))
if added: print('EKLENEN:', [name_of(new[k][1]) for k in added])
same_order = [k for k in old_order if k in new] == [k for k in new_order if k in old]
print('kalan blokların sırası aynı:', same_order)
for k in changed:
    a, b = old[k][1].split('\n'), new[k][1].split('\n')
    d = [l for l in difflib.unified_diff(a, b, lineterm='', n=0) if not l.startswith(('---', '+++', '@@'))]
    print('\n&%s %s (%d satır fark)' % (k, name_of(old[k][1]), len(d)))
    for l in d[:14]: print('   ' + l[:150])
    if len(d) > 14: print('   … %d satır daha' % (len(d) - 14))
