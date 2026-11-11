(function(root){
  'use strict';
  const statuses=['Açık','Doğrulama bekliyor','Kanıt var','Bilinmiyor','Doğrulandı','Kapsam dışı'];
  const priorities=['P0','P1','P2','P3'];
  const categories=['Diyalog','Öğretici','50 round','Dil','Boss görselleri','Animasyon & ikon','Lisans & credit','Yayın adayı'];
  const evidence=['Kod kanıtı','Geçmiş rapor','Belirsiz','Güncel test'];
  const unresolved=c=>!['Doğrulandı','Kapsam dışı'].includes(c.status);
  const critical=c=>unresolved(c)&&['P0','P1'].includes(c.priority);
  const normalize=v=>String(v||'').toLocaleLowerCase('tr-TR');
  function filter(checks,f={}){
    let out=checks.filter(c=>(!f.category||c.category===f.category)&&(!f.status||c.status===f.status)&&(!f.priority||c.priority===f.priority)&&(!f.critical||critical(c))&&(!f.noIssue||!c.issue)&&(!f.query||normalize([c.id,c.title,c.finding,c.fix,c.verify,c.category,...c.sources.map(s=>s.path)].join(' ')).includes(normalize(f.query))));
    return out.sort((a,b)=>f.sort==='updated'?String(b.updatedAt).localeCompare(String(a.updatedAt)):priorities.indexOf(a.priority)-priorities.indexOf(b.priority)||Number(b.status==='Açık')-Number(a.status==='Açık')||a.id.localeCompare(b.id));
  }
  function validate(bundle){
    if(!bundle||bundle.schemaVersion!==1||bundle.project!=='Erigone'||!Array.isArray(bundle.checks)||!Array.isArray(bundle.commits)||!Array.isArray(bundle.workingTree)||!bundle.csvAudit||!Array.isArray(bundle.bossRounds)||!Array.isArray(bundle.storyRounds))throw Error('Erigone şema v1 yedeği bekleniyor. snapshot.json veya panel JSON yedeğini seç.');
    if(bundle.checks.length>2000)throw Error('En fazla 2000 kayıt içe aktarılabilir.');
    for(const key of ['head','snapshotDate','generatedAt','repository','issueState'])if(typeof bundle[key]!=='string')throw Error('Anlık görüntü alanı eksik: '+key);
    if(!/^https:\/\/[^\s]+$/i.test(bundle.repository))throw Error('Depo bağlantısı HTTPS olmalı.');
    if(!/^[a-f0-9]{40}$/i.test(bundle.head)||!Number.isFinite(Date.parse(bundle.generatedAt)))throw Error('Kaynak revision veya tarih geçersiz.');
    if(bundle.bossRounds.concat(bundle.storyRounds).some(r=>!Number.isInteger(r)||r<1||r>50))throw Error('Takvim verisi geçersiz.');
    const a=bundle.csvAudit;
    if(!Number.isInteger(a.rows)||!a.missing||!['en','tr','note'].every(k=>Array.isArray(a.missing[k]))||!Array.isArray(a.duplicates)||!Array.isArray(a.turkishInEnglish)||typeof a.scope!=='string')throw Error('CSV denetim özeti eksik.');
    const ids=new Set();
    for(const c of bundle.checks){
      if(!c||typeof c.id!=='string'||!c.id||ids.has(c.id))throw Error('Eksik veya yinelenen kayıt kimliği.');ids.add(c.id);
      for(const key of ['title','finding','fix','verify','evidence','connection','verificationResult'])if(typeof c[key]!=='string'||c[key].length>30000)throw Error('Geçersiz kayıt alanı: '+key);
      if(!statuses.includes(c.status)||!priorities.includes(c.priority)||!categories.includes(c.category)||!evidence.includes(c.evidence))throw Error('Geçersiz durum, öncelik, kategori veya kanıt.');
      if(!Array.isArray(c.sources)||c.sources.some(s=>!s||typeof s.path!=='string'||(s.line!=null&&(!Number.isInteger(s.line)||s.line<1))))throw Error('Geçersiz dosya yolu/satır.');
      if(!Array.isArray(c.rounds)||c.rounds.some(r=>!Number.isInteger(r)||r<1||r>50))throw Error('Roundlar 1–50 arasında olmalı.');
      if(typeof c.issue!=='string'||(c.issue&&!/^https?:\/\//i.test(c.issue)))throw Error('Bağlantılar yalnız http/https olabilir.');
      if(typeof c.updatedAt!=='string'||!Number.isFinite(Date.parse(c.updatedAt)))throw Error('Kayıt tarihi geçersiz.');
      if(c.status==='Doğrulandı'&&!c.verificationResult.trim())throw Error('Doğrulanmış kayıtta sonuç/kanıt eksik.');
    }
    if(!bundle.commits.length)throw Error('Commit anlık görüntüsü boş.');
    for(const c of bundle.commits){
      if(!c||!(/^[a-f0-9]{40}$/i.test(c.sha))||!Array.isArray(c.files)||!c.systems||!['title','short','date'].every(k=>typeof c[k]==='string'))throw Error('Commit verisi geçersiz.');
      if(c.files.some(f=>!f||typeof f.path!=='string'||typeof f.system!=='string'||!['added','deleted'].every(k=>f[k]===null||Number.isInteger(f[k]))))throw Error('Commit dosyaları geçersiz.');
    }
    if(bundle.workingTree.some(f=>!f||!['path','system','status'].every(k=>typeof f[k]==='string')))throw Error('Çalışma ağacı verisi geçersiz.');
    return bundle;
  }
  const escape=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const csvCell=s=>'"'+String(s??'').replace(/^[=+@\-\t\r]/,"'$&").replace(/"/g,'""')+'"';
  const api={statuses,priorities,categories,evidence,unresolved,critical,filter,validate,escape,csvCell};
  root.ReviewLogic=api;if(typeof module!=='undefined')module.exports=api;
})(typeof window!=='undefined'?window:globalThis);
