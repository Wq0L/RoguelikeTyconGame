// Read the serialized assets, not the candidate prices. Generate the full current price list.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..'),dir=path.join(root,'Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'Docs/FinalSkillTree.json'),'utf8'));
const ids=new Map(manifest.nodes.map(n=>[n.assetName||n.id,n.id]));
const total=[0,0,0],rows=[];let count=0;
for(const file of fs.readdirSync(dir).filter(f=>f.endsWith('.asset'))){
 const raw=fs.readFileSync(path.join(dir,file),'utf8'),name=file.slice(0,-6),id=ids.get(name);
 const tiers=[...raw.matchAll(/^  - costType: (\d+)\r?\n    cost: (\d+)/gm)].map(m=>({type:+m[1],cost:+m[2]}));
 if(!id||!tiers.length)throw Error('Unknown or empty skill '+file);
 for(const t of tiers){total[t.type]+=t.cost;count++;}
 rows.push({id,name,tiers});
}
rows.sort((a,b)=>a.id.localeCompare(b.id,undefined,{numeric:true}));
const fmt=n=>n.toLocaleString('tr-TR'),money=t=>fmt(t.cost)+' '+['Stone','Iron','Gold'][t.type];
let doc='# Canlı skill asset maliyetleri\n\nKaynak: FinalSkillTree klasörünün costType/cost alanları. Bu rapor fiyatları değiştirmez.\n\n';
doc+=`${rows.length} farklı node, ${count} kademe. Toplam: **${fmt(total[2])} Gold + ${fmt(total[1])} Iron + ${fmt(total[0])} Stone**. Bütün kademelerin satın alma maliyetleri toplanır; stat etkilerinde yalnız son kademe uygulanır.\n\n`;
doc+='| Kod | Skill | Kademe fiyatları |\n|---|---|---|\n';for(const r of rows)doc+=`| ${r.id} | ${r.name} | ${r.tiers.map(money).join(' / ')} |\n`;
fs.writeFileSync(path.join(root,'Docs/CurrentSkillCosts.md'),doc);
console.log(JSON.stringify({nodes:rows.length,tiers:count,Gold:total[2],Iron:total[1],Stone:total[0]}));
