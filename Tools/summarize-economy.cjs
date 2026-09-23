const fs=require('fs');
const files=process.argv.slice(2);if(!files.length)files.push('Logs/EconomyBalanceSimulation.json');
const median=a=>{a.sort((x,y)=>x-y);return (a[Math.floor((a.length-1)/2)]+a[Math.ceil((a.length-1)/2)])/2;};
for(const file of files){const r=JSON.parse(fs.readFileSync(file,'utf8'));console.log(file,JSON.stringify({totals:r.totals,...r.summary}));
for(const range of [[1,20],[21,65],[66,100],[101,130]])console.log(range.join('-'), 'purchases/round',median(r.simulations.map(s=>s.purchases.filter(p=>p.round>=range[0]&&p.round<=range[1]).length/(range[1]-range[0]+1))).toFixed(2));
for(const round of [10,20,30,45,65,80,100,120,130]){const h=r.simulations.map(s=>s.history[round-1]);console.log(round,JSON.stringify({tiers:median(h.map(x=>x.total)),damage:Math.round(median(h.map(x=>x.damage))),attack:+median(h.map(x=>x.attack)).toFixed(2),spawn:+median(h.map(x=>x.spawn)).toFixed(2),kills:median(h.map(x=>x.kills)),gold:median(h.map(x=>x.income[2]))}));}
}
