// Idempotent migration: three replacement tiers per speed node, same final effect/budget.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..'),file=path.join(root,'Docs/FinalSkillTree.json');
const m=JSON.parse(fs.readFileSync(file,'utf8'));
for(const n of m.nodes.filter(n=>/^(E12|Z11)-/.test(n.id))){
 const last=structuredClone(n.tiers.at(-1)),total=n.tiers.reduce((s,t)=>s+t.cost,0);
 const costs=[Math.floor(total*.25),Math.floor(total*.35)];costs.push(total-costs[0]-costs[1]);
 n.tiers=costs.map((cost,i)=>({...structuredClone(last),cost,effects:last.effects.map(e=>({...e,value:i===2?e.value:Math.pow(1+e.value,(i+1)/3)-1}))}));
 for(const p of n.prerequisites)if(/^(E12|Z11)-/.test(p.id))p.level=3;
 const asset=path.join(root,'Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree',n.assetName+'.asset');
 let raw=fs.readFileSync(asset,'utf8');
 let tiers='  tiers:\n';for(const t of n.tiers){tiers+=`  - costType: ${t.costType}\n    cost: ${t.cost}\n    effects:\n`;for(const e of t.effects)tiers+=`    - statType: ${e.statType}\n      target: ${e.target}\n      operation: ${e.operation}\n      value: ${e.value}\n`;}
 raw=raw.replace(/  tiers:[\s\S]*?(?=  unlockType:)/,tiers);
 if(n.prerequisites.some(p=>/^(E12|Z11)-/.test(p.id)))raw=raw.replace(/    level: 1/, '    level: 3');
 fs.writeFileSync(asset,raw);
 console.log(n.id,costs,'final multiplier',1+last.effects[0].value);
}
fs.writeFileSync(file,JSON.stringify(m,null,2)+'\n');
