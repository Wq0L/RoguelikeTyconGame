from pathlib import Path
import base64, hashlib, html, json, re, shutil, struct, zipfile

root = Path(__file__).resolve().parent
old = root / 'ComicPaperUI-Pack-v1'
pack = root / 'ComicPaperUI-Pack-v2'
shutil.copytree(old, pack, dirs_exist_ok=True)
(pack / 'References').mkdir(exist_ok=True)
source = root / 'imagegen'
added = {
    'Slots/grid-square-navy.png': source / 'comic-ui-sprites-20261106/grid-square-dark.png',
    'References/comic-ui-concept.png': source / 'comic-ui-concept-20261106/comic-ui-concept.png',
}
manifest = json.loads((old / 'manifest.json').read_text(encoding='utf-8-sig'))
for name, src in added.items():
    shutil.copy2(src, pack / name)
    data = src.read_bytes()
    w, h = struct.unpack('>II', data[16:24])
    manifest.append(dict(file=name, width=w, height=h, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(), kind='reference-sheet' if name.startswith('References/') else 'sprite', source=str(src)))
for name in ['prompt.txt']:  # skill konsepti 6 Kasım 2026'da çıkarıldı
    shutil.copy2(source / 'comic-ui-concept-20261106' / name, pack / 'References' / name)
shutil.copy2(source / 'comic-ui-sprites-20261106/prompts.txt', pack / 'References/earlier-sprite-prompts.txt')
(pack / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')

original = (old / 'Preview.html').read_text(encoding='utf-8-sig')
css = re.search(r'<style>(.*?)</style>', original, re.S)[1]
interactive = json.loads(re.search(r'const interactive=(\[.*?\]),', original)[1])
statics = json.loads(re.search(r'statics=(\[.*?\]);', original)[1]) + [p[:-4] for p in added]
entries = [(p, True) for p in interactive] + [(p, False) for p in statics]
groups = ['Buttons','Slots','Panels','HUD','Parts','References']
labels = dict(Buttons='Butonlar', Slots='Kareler ve grid', Panels='Paneller ve skill bilgileri', HUD='Başlıklar ve sayaçlar', Parts='Bar ve ayırıcı', References='Önceki toplu tasarımlar · referans')
dimensions = {v['file']: (v['width'], v['height']) for v in manifest}
def uri(file):
    return 'data:image/png;base64,' + base64.b64encode((pack / file).read_bytes()).decode('ascii')
sections = []
states = {}
for group in groups:
    cards = []
    for path, active in entries:
        if not path.startswith(group + '/'):
            continue
        name = path.split('/')[-1]
        file = path + ('-normal' if active else '') + '.png'
        w, h = dimensions[file]
        if active:
            states[path] = {s: uri(path + '-' + s + '.png') for s in ['hover','pressed']}
        attrs = f' tabindex="0" role="button" aria-label="{name} önizlemesi"' if active else ''
        links = ''.join(f'<a href="{path}-{s}.png" download data-state="{s}">{s}</a>' for s in ['normal','hover','pressed']) if active else f'<a href="{file}" download>PNG</a>'
        cards.append(f'<article class="card" data-path="{path}" data-active="{str(active).lower()}"><div class="stage{" interactive" if active else ""}"{attrs}><img src="{uri(file)}" alt="{name}" draggable="false"></div><div class="name">{name}</div><div class="links">{links}</div><small>{w} × {h} px · {"normal" if active else "sabit"}</small></article>')
    sections.append(f'<section data-group="{group}"><h2>{labels[group]}</h2>' + ('<p>Bu iki dosya önceki toplu çalışmalardır; ayrı sprite sayısına dahil değildir. Skill konseptinde örnek ikonlar vardır.</p>' if group == 'References' else '') + '<div class="grid">' + ''.join(cards) + '</div></section>')
script = r'''
const variants = __VARIANTS__;
const stateSelect = document.querySelector('#state');
const backgroundSelect = document.querySelector('#bg');
const groupSelect = document.querySelector('#filter');
const controllers = [];
function localImage(data) {
  const bytes = Uint8Array.from(atob(data.slice(data.indexOf(',') + 1)), c => c.charCodeAt(0));
  return URL.createObjectURL(new Blob([bytes], {type:'image/png'}));
}
for (const card of document.querySelectorAll('.card')) {
  const img = card.querySelector('img'), stage = card.querySelector('.stage'), meta = card.querySelector('small');
  const path = card.dataset.path, active = card.dataset.active === 'true';
  const sources = Object.fromEntries(Object.entries({normal: img.src, ...(variants[path] || {})}).map(([key, data]) => [key, localImage(data)]));
  img.src = sources.normal;
  let current = active ? 'normal' : 'sabit';
  function updateMeta() { meta.textContent = img.naturalWidth + ' × ' + img.naturalHeight + ' px · ' + current; }
  function show(value) {
    current = active ? (stateSelect.value === 'auto' ? value : stateSelect.value) : 'sabit';
    img.src = sources[active ? current : 'normal'];
    card.dataset.current = current;
    updateMeta();
  }
  img.onload = updateMeta;
  img.onerror = () => { meta.textContent = 'Görsel yüklenemedi'; stage.classList.add('error'); };
  for (const link of card.querySelectorAll('a')) link.href = sources[link.dataset.state || 'normal'];
  if (active) {
    stage.onpointerenter = () => show('hover');
    stage.onpointerleave = () => show('normal');
    stage.onpointerdown = e => { stage.setPointerCapture(e.pointerId); show('pressed'); };
    stage.onpointerup = e => { const r = stage.getBoundingClientRect(); show(e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom ? 'hover' : 'normal'); };
    stage.onpointercancel = () => show('normal');
    stage.onfocus = () => show('hover');
    stage.onblur = () => show('normal');
    stage.onkeydown = e => { if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); show('pressed'); } };
    stage.onkeyup = e => { if (e.key === ' ' || e.key === 'Enter') show('hover'); };
    controllers.push(show);
  }
}
stateSelect.onchange = () => controllers.forEach(show => show('normal'));
backgroundSelect.onchange = () => document.body.className = backgroundSelect.value;
groupSelect.onchange = () => document.querySelectorAll('section[data-group]').forEach(section => section.hidden = !!groupSelect.value && section.dataset.group !== groupSelect.value);
document.querySelector('#status').textContent = 'Etkileşim hazır · 41 ayrı PNG + 2 referans sayfası';
'''.replace('__VARIANTS__', json.dumps(states, separators=(',',':')))
doc = f'''<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Comic Paper UI v2 — Önizleme</title><style>{css}section[hidden]{{display:none}}#status{{color:#f1d588}}section[data-group="References"] .stage{{height:350px}}section[data-group="References"] img{{max-height:350px}}</style></head><body>
<header><h1>Comic Paper UI · v2</h1><p>41 ayrı sprite + önceki 2 toplu tasarım. Fareyle üzerine gel ve basılı tut; durumları menüden de karşılaştırabilirsin.<br>Önizleme görselleri bu dosyanın içindedir. İnternet veya sunucu gerektirmez.</p><p id="status">Statik önizleme · Etkileşim için dosyayı Edge / Chrome / Firefox ile aç.</p></header>
<nav><label>Durum <select id="state"><option value="auto">Etkileşimli</option><option value="normal">Normal</option><option value="hover">Hover</option><option value="pressed">Pressed</option></select></label><label>Zemin <select id="bg"><option value="">Damalı</option><option value="light">Açık</option><option value="dark">Koyu</option></select></label><label>Grup <select id="filter"><option value="">Hepsi</option>{''.join(f'<option value="{g}">{labels[g]}</option>' for g in groups)}</select></label></nav>
<main>{''.join(sections)}</main><script>{script}</script></body></html>'''
(pack / 'Preview.html').write_text(doc, encoding='utf-8')
readme = (old / 'README.md').read_text(encoding='utf-8-sig').replace('Pack v1', 'Pack v2').replace('Tüm PNG\'ler yazısız ve ikonsuzdur.', '41 ayrı sprite yazısız ve ikonsuzdur. References klasöründeki iki toplu konsept sayfası bunun dışındadır; bunlar önceki çalışmaları korumak içindir.').replace('Görseller internet bağlantısı olmadan yerel dosyalardan yüklenir.', 'Görseller HTML içine gömülüdür; internet, sunucu veya yan klasör gerektirmez. JavaScript kapalıysa bile normal görseller görünür. Etkileşim için ZIP dosyasını çıkarıp Preview.html dosyasını Edge, Chrome veya Firefox ile aç. Codex içindeki metin önizlemesi HTML çalıştırmayabilir.')
readme += '\n## v2 ekleri\n\n41 ayrı PNG ve 2 referans sayfası. Önceki lacivert grid karesi Slots/grid-square-navy.png adıyla eklendi. İlk UI sayfası ve skill konsepti References klasöründedir. Daha önce eklenen aynı sprite dosyaları tekrar çoğaltılmadı. Kaynak görseller değiştirilmeden kopyalandı.\n'
(pack / 'README.md').write_text(readme, encoding='utf-8')
print(json.dumps(dict(pack=str(pack), pngs=len(manifest), preview_bytes=len(doc.encode('utf-8')))))
