"""Read-only project snapshot. Standard library only; does not run Unity or modify game data."""
import csv, json, subprocess, pathlib, datetime, collections, re
HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent.parent
def git(*args):
    return subprocess.check_output(['git','-c','core.quotepath=false',*args], cwd=ROOT, encoding='utf-8', errors='replace', stderr=subprocess.DEVNULL).strip()
def system(path):
    p=path.lower()
    if any(s in p for s in ['localization','ceviri','gametext']): return 'Dil'
    if any(s in p for s in ['license','credits','thirdparty']): return 'Lisans & credit'
    if any(s in p for s in ['dialogue','diyalog','story','tutorial']): return 'Diyalog & öğretici'
    if any(s in p for s in ['boss','whim']): return 'Boss görselleri'
    if any(s in p for s in ['intro','acilis','keyprompt','mascot','erimood']): return 'Animasyon & ikon'
    if any(s in p for s in ['runprofile','roundmanager','run50']): return '50 round'
    if any(s in p for s in ['/ui/','hud','comic','font']): return 'UI & font'
    return 'Diğer'
commits=[]
for row in git('log','-6','--format=%H|%h|%cs|%s').splitlines():
    sha,short,date,title=row.split('|',3)
    files=[]
    for l in git('show','--format=','--numstat',sha).splitlines():
        parts=l.split('\t',2)
        if len(parts)==3:
            add,delete,path=parts
            files.append(dict(path=path,added=None if add=='-' else int(add),deleted=None if delete=='-' else int(delete),system=system(path)))
    commits.append(dict(sha=sha,short=short,date=date,title=title,files=files,systems=dict(collections.Counter(f['system'] for f in files))))
working=[]
raw=subprocess.check_output(['git','-c','core.quotepath=false','status','--porcelain','-z','--untracked-files=normal'],cwd=ROOT).decode('utf-8')
for row in raw.split('\0'):
    if len(row)<4:continue
    path=row[3:]
    if path.startswith('Sites/'):continue
    working.append(dict(status=row[:2],path=path,system=system(path)))
with (ROOT/'Assets/Localization/GameText.csv').open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
counts=collections.Counter(r['key'] for r in rows)
audit=dict(rows=len(rows),duplicates=[k for k,v in counts.items() if v>1],missing={c:[r['key'] for r in rows if not (r.get(c) or '').strip()] for c in ['en','tr','note']},turkishInEnglish=[r['key'] for r in rows if re.search('[çğıöşüÇĞİÖŞÜ]',r.get('en',''))],scope='CSV alan denetimi; Unity kapsam/yer tutucu/runtime testi değildir.')
checks=[]
generated_at=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=3))).isoformat()
for name in ['audit-game.json','audit-review.json']:
    checks+=json.loads((HERE/name).read_text(encoding='utf-8-sig'))
for c in checks:
    c.setdefault('issue','')
    c.setdefault('updatedAt',generated_at)
    c.setdefault('verificationResult','Bu incelemede Unity testi çalıştırılmadı.')
    for s in c['sources']:
        s['exists']=(ROOT/s['path']).is_file()
        s['workingTree']=any(s['path']==w['path'] or s['path'].startswith(w['path'].rstrip('/')+'/') for w in working)
data=dict(schemaVersion=1,project='Erigone',snapshotDate=generated_at[:10],generatedAt=generated_at,head=git('rev-parse','HEAD'),branch=git('branch','--show-current'),repository=git('remote','get-url','origin').removesuffix('.git'),unity='6000.3.14f1',commits=commits,workingTree=working,csvAudit=audit,checks=checks,bossRounds=[3,6,10,13,16,20,23,26,30,33,36,40,43,46,50],storyRounds=[5,10,15,20,25,30,35,40,45,50],issueState='Canlı issue listesi sorgulanmadı; issue bağlantısı olmayan kayıtlar da değerlendirilir.',runtimeState='Unity, Play Mode ve oyuncu build testleri bu incelemede çalıştırılmadı.')
(HERE/'dist/snapshot.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
(HERE/'dist/data.js').write_text('window.ERIGONE_SNAPSHOT = '+json.dumps(data,ensure_ascii=False)+';\n',encoding='utf-8')
print(json.dumps(dict(checks=len(checks),commits=len(commits),workingEntries=len(working),csv=audit,missingSources=[s['path'] for c in checks for s in c['sources'] if not s['exists']]),ensure_ascii=False))
