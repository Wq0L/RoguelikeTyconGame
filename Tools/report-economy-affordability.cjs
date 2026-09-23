// Reports only. Never changes gameplay assets.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..');
const load=f=>JSON.parse(fs.readFileSync(path.join(root,'Logs',f),'utf8'));
const q=(values,p=.5)=>{values.sort((a,b)=>a-b);const x=(values.length-1)*p,a=Math.floor(x);return values[a]+(values[Math.ceil(x)]-values[a])*(x-a);};
const fmt=n=>Math.round(n).toLocaleString('tr-TR');
const normal=load('EconomyBalanceSimulation.json'),held=load('EconomyBalanceSimulationValidation.json');
let text='# Ekonomi — round bazında gelişim ve erişim\n\n';
text+='Kaynak: canlı fiyat planıyla 10 seed × 3 alışveriş tercihi, ayrıca 10 ayrı seed ile doğrulama. Her round kendi geliri, satın alımları ve güncel build statlarıyla ilerler. Geç round geliri geçmişe uygulanmaz.\n\n';
text+=`Tam **140 node / 304 kademe**: **${fmt(normal.totals[2])} Gold + ${fmt(normal.totals[1])} Iron + ${fmt(normal.totals[0])} Stone**. Bu tutar yalnız skill ağacıdır; saksı satın alımları ayrıca ödenir. Fiyatlar herkes için sabittir.\n\n`;
text+='Model 30 FPS yaşam döngüsü ve gerçek varlık fiyat/etkilerini kullanır; float ve hedefleme yaklaşımı nedeniyle Unity oynanışıyla bit düzeyinde aynı değildir. Kart, tile, rezonans, ikincil davranış ve skip geliri dahil değildir. İyi rezonansın ne kadar erken bitirdiği bu testten çıkarılamaz. İnsan playtest sonucu değildir.\n\n';
text+='## Referans gelişim temposu\n\n| Dönem | Medyan satın alma / round |\n|---|---:|\n';
for(const [a,b] of [[1,20],[21,65],[66,100],[101,130]])text+=`| R${a}–${b} | ${q(normal.simulations.map(s=>s.purchases.filter(p=>p.round>=a&&p.round<=b).length/(b-a+1))).toFixed(2)} |\n`;
text+='\nBunlar kademe satın alımlarıdır, farklı node sayısı değildir. Bir roundda birden fazla satın alım olabilir; round başına alışveriş sınırı yoktur.\n\n';
text+='| Round | Satın alınmış kademe P50 | Nominal hasar P50 | Saldırı aralığı P50 | Spawn aralığı P50 |\n|---|---:|---:|---:|---:|\n';
for(const round of [20,45,65,80,100,120]){const h=normal.simulations.map(s=>s.history[round-1]);text+=`| ${round} | ${q(h.map(x=>x.total))} | ${fmt(q(h.map(x=>x.damage)))} | ${q(h.map(x=>x.attack)).toFixed(2)} s | ${q(h.map(x=>x.spawn)).toFixed(2)} s |\n`;}
for(const [name,file,assumption] of [
 ['Referans — kontrollü hedefleme','EconomyBalanceSimulation.json','Vuruş başına 4–12 etkili hedef; 48 hücreye kademeli yatırım. Dengeli, ekonomi ve hasar ağırlıklı alışveriş tercihlerinin tümü aynı fiyatları kullanır.'],
 ['Az hedef / kaçan saldırı stres testi','EconomyWeakBuildSimulation.json','Vuruş başına 2–6 etkili hedef, %15 saldırı kaçırma, 30 hücre çiftlik hedefi.'],
 ['Çok düşük hasat verimi stres testi','EconomyPoorBuildSimulation.json','Vuruş başına 1–4 etkili hedef, %30 saldırı kaçırma, 24 hücre çiftlik hedefi.']]){
 const r=load(file);if(r.planFingerprint!==normal.planFingerprint)throw Error('Stale report '+file);
 const opened=r.simulations.map(s=>new Set(s.purchases.map(p=>p.id)).size);
 text+=`\n## ${name}\n\n${assumption} Hücre hedefi bütçe yetmezse gerçekleşmez.\n\n`;
 text+=`R130 farklı açılmış skill: **${Math.min(...opened)}–${Math.max(...opened)} / 140**, P50 **${q([...opened])}**. Açılma en az bir kademe satın alınmasıdır; full yükseltme değildir. Tamamlanan koşu **${r.summary.completed}/30**.`;
 if(r.summary.completed)text+=` Tamamlayanların ortalaması R${r.summary.mean.toFixed(1)}, aralık R${r.summary.min}–${r.summary.max}.`;
 text+='\n\nHarcamalar öncesi **birikimli kazanım P50**; cüzdan bakiyesi değildir:\n\n| Round | Gold | Iron | Stone |\n|---|---:|---:|---:|\n';
 for(const round of [20,65,100,130])text+=`| ${round} | ${[2,1,0].map(i=>fmt(q(r.simulations.map(s=>s.history.slice(0,round).reduce((sum,h)=>sum+h.income[i],0))))).join(' | ')} |\n`;
 text+='\nR130 toplam kazanım P10–P90: '+[2,1,0].map((i,j)=>`${['Gold','Iron','Stone'][j]} ${fmt(q(r.simulations.map(s=>s.incomeTotal[i]),.1))}–${fmt(q(r.simulations.map(s=>s.incomeTotal[i]),.9))}`).join('; ')+'.\n';
}
if(held.planFingerprint!==normal.planFingerprint)throw Error('Stale validation');
text+=`\n## Ayrı seed doğrulaması\n\nReferans kontrolde ${held.summary.completed}/30 koşu tamamlandı. Tamamlayanlar R${held.summary.min}–${held.summary.max}; ortalama R${held.summary.mean.toFixed(1)}. R130 farklı node sayısı ${held.summary.openedNodes.min}–${held.summary.openedNodes.max}.\n\n`;
text+='Düşük hasat verimi, kötü skill seçimiyle aynı değişken değildir: stres testleri hedef kapsamasını, kaçan saldırıları ve çiftlik büyüklüğünü de değiştirir. Bu testlerde tıkanma erken başlayabilir. Önceki "en kötü koşulda en az 80 node" iddiası bu veri için geçerli değildir. Fiyat planı hızlı başlangıç / orta yavaşlama / son güçlenmeyi referans koşuda hedefler; bütün oyuncuları aynı sonuca zorlamaz. Gerçek oyuncu testleri ve rezonans/kart geri beslemesi ayrıca değerlendirilmelidir.\n';
fs.writeFileSync(path.join(root,'Docs/EconomyAffordability.md'),text);
console.log('Updated Docs/EconomyAffordability.md from matching reference, held-out and stress histories.');
