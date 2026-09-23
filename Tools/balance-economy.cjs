// Candidate economy + deterministic affordability probe. --apply explicitly writes approved fields only.
// Run with node Tools/balance-economy.cjs; see Docs/EconomyBalance.md for model limits.
const fs = require('fs'), path = require('path');
const root = path.resolve(__dirname, '..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const manifest = JSON.parse(read('Docs/FinalSkillTree.json'));
const nodes = structuredClone(manifest.nodes);
const groups = Object.groupBy(nodes, n => n.id.split('-')[0]);
const names = read('Assets/Scripts/Enums/StatType.cs').replace(/\/\/[^\n]*/g, '').split('{')[1].split('}')[0].split(',').map(x => x.trim()).filter(Boolean);
const S = Object.fromEntries(names.map((n,i) => [n,i]));
const group = id => groups[id];
// Explicit route budgets, not per-tier prices. Later tiers carry a larger share.
const routes = {
 H1:[50,2,1,15], H2:[500,2,25,60], H3:[2000,2,65,78], H4:[10000,2,78,93], H5:[22000,1,90,105],
 H6:[150,2,10,30], H7:[600,1,35,65], H8:[16000,2,75,95], H9:[155000,2,90,110],
 H10:[180,0,90,100], H11:[900,0,100,110], H12:[400,2,20,65], H13:[3000,1,65,100],
 Z1:[70,2,1,15], Z2:[100,2,3,25], Z3:[180,2,10,30], Z4:[600,1,30,65], Z5:[1000,2,40,70], Z11:[9000,2,65,100],
 Z6:[60,2,10,25], Z7:[200,1,30,65], Z8:[1800,0,75,100], Z9:[1200,0,70,100], Z10:[2500,2,40,100],
 E1:[70,2,1,20], E2:[200,2,8,30], E3:[280,2,15,45], E4:[1600,2,30,65], E5:[650,1,30,65],
 E6:[220,2,15,40], E7:[650,1,45,75], E12:[5850,1,65,100], E8:[1500,0,70,100], E9:[1200,1,50,95], E10:[6000,2,80,105], E11:[4000,1,95,110]
};
const lateScale = Number(process.env.ECONOMY_LATE_SCALE || 2.7);
const earlyScale = Number(process.env.ECONOMY_EARLY_SCALE || 6);
const middleScale = Number(process.env.ECONOMY_MIDDLE_SCALE || 4);
if (![earlyScale,middleScale,lateScale].every(v=>Number.isFinite(v)&&v>0))throw Error('Economy scales must be finite and positive.');
for (const [id,[budget,currency,start,end]] of Object.entries(routes)) {
 const entries=group(id).flatMap(n=>n.tiers.map((t,i)=>({n,t,i})));
 const scale=start>=65?lateScale:start>=25?middleScale:earlyScale;
 const weights=entries.map((_,i)=>1+i*.32), total=weights.reduce((a,b)=>a+b,0);
 entries.forEach((e,i)=>{e.t.cost=Math.max(1,Math.round(budget*scale*weights[i]/total));e.t.costType=currency;});
 for (const n of group(id)) n.targetRounds=[start,end];
}
// Existing speed routes retain their original effects. Three appended nodes per
// branch supply the endgame acceleration; never rebuff the old nodes here.
function additiveRoute(id,total) {
 const current=group(id).reduce((s,n)=>s+n.tiers.at(-1).effects[0].value,0);
 for(const n of group(id))for(const t of n.tiers)for(const e of t.effects)e.value*=total/current;
}
additiveRoute('E1',.5); additiveRoute('E4',2.5); // Gold x4
additiveRoute('E3',1); additiveRoute('E5',1); additiveRoute('E8',3); // Iron/Stone x5
additiveRoute('E6',.5); additiveRoute('E10',1.5); // XP x1 -> x1.5 -> x3
additiveRoute('H1',60); additiveRoute('H2',150); additiveRoute('H3',1000); additiveRoute('H4',4000);
group('H1')[0].tiers[0].cost=3; // Always affordable alongside the 20G + 40G opening planters.
const fixed={P1:[[80,2],[40,1]],P2:[[40,2]],P3:[[20,1]],P4:[[40,1]],P5:[[100,2]],P6:[[35,1]],P7:[[120,0],[450,0]],P8:[[180,2]]};
for(const [id,tiers] of Object.entries(fixed)) tiers.forEach(([cost,costType],i)=>Object.assign(group(id)[0].tiers[i],{cost,costType}));
group('P5')[0].prerequisites=[{id:'P2',level:1},{id:'P1',level:1}];
group('P8')[0].prerequisites=[{id:'P5',level:1},{id:'P1',level:2}];
for(const [id,range] of Object.entries({P1:[4,18],P2:[5,7],P3:[10,20],P5:[10,13],P6:[15,25],P7:[45,85],P8:[16,20]}))group(id)[0].targetRounds=range;
const plantFolder='Assets/ScriptableObjects/Plants';
const plants={};
function scalar(text,key){return Number(text.match(new RegExp('^  '+key+': ([^\\r\\n]+)','m'))[1]);}
for(const f of fs.readdirSync(path.join(root,plantFolder)).filter(f=>f.endsWith('.asset'))){
 const text=read(plantFolder+'/'+f),guid=read(plantFolder+'/'+f+'.meta').match(/guid: (\w+)/)[1];
 plants[guid]={name:f.slice(0,-6),rarity:scalar(text,'rarity'),resource:scalar(text,'resourceType'),reward:scalar(text,'rewardAmount'),xp:scalar(text,'xpAmount'),hp:scalar(text,'maxHealth')};
 const plant=plants[guid];if(plant.resource===1)plant.reward=plant.rarity===4?15:3;
 if(plant.resource===0)plant.reward=plant.rarity===4?8:2;
}
const planterFiles=['GrassPlanter 1x1','GrassPlanter 1x2','GrassPlanter 1x3','GrassPlanter 2x2','GrassPlanter 2x3'];
const planterCosts=[[20,2],[40,2],[70,2],[25,1],[20,0]];
const planters=planterFiles.map((f,i)=>{
 const text=read('Assets/ScriptableObjects/Planters/'+f+'.asset');
 return {name:f,size:[scalar(text,'sizeX'),scalar(text,'sizeZ')],cost:planterCosts[i][0],currency:planterCosts[i][1],
 table:[...text.matchAll(/plant: \{fileID: \d+, guid: (\w+), type: 2\}\s+baseChance: ([\d.]+)/g)].map(m=>({plant:plants[m[1]],weight:Number(m[2])}))};
});
const core=Array.from({length:names.length},()=>0);
for(const m of read('Assets/Scripts/Stats/StatDefaults.cs').matchAll(/case StatType\.(\w+):\s*return ([\d.]+)f;/g))core[S[m[1]]]=+m[2];
for(const m of read('Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset').matchAll(/statType: (\d+)\s+value: ([^\r\n]+)/g))core[+m[1]]=+m[2];
const anchors=[...read('Assets/Resources/PlantHealthScaling.asset').matchAll(/round: (\d+)\s+commonHealth: ([\d.]+)/g)].map(m=>[+m[1],+m[2]]);
function health(plant,round){let a=anchors[0],b=anchors.at(-1);for(const p of anchors){if(p[0]<=round)a=p;if(p[0]>=round){b=p;break;}}return Math.ceil(a[1]*Math.pow(b[1]/a[1],a[0]===b[0]?0:(round-a[0])/(b[0]-a[0]))*[1,1.2,1.6,2,3][plant.rarity]*plant.hp/10-1e-6);}
function rng(seed){let x=seed>>>0||1;return()=>{x^=x<<13;x^=x>>>17;x^=x<<5;return(x>>>0)/4294967296;};}
function roundEven(x){const n=Math.floor(x),f=x-n;return Math.abs(f-.5)<1e-8?n+(n%2):Math.round(x);}
function stats(levels){const flat=core.map(()=>0),add=core.map(()=>0),more=core.map(()=>1),base=[...core];
 for(const n of nodes){let level=levels[n.id]||0;if(!level)continue;for(const e of n.tiers[level-1].effects){if(e.operation===0)flat[e.statType]+=e.value;else if(e.operation===1)add[e.statType]+=e.value;else if(e.operation===2)more[e.statType]*=1+e.value;else base[e.statType]=e.value;}}
 const out=base.map((v,i)=>(v+flat[i])*(1+add[i])*more[i]);
 out[S.AttackSpeed]=Math.max(.1,out[S.AttackSpeed]);out[S.PlantSpawnRate]=Math.max(.5,out[S.PlantSpawnRate]);
 out[S.CritChance]=Math.min(1,out[S.CritChance]);out[S.RoundDuration]=Math.min(90,Math.max(30,out[S.RoundDuration]));return out;}

function simulate(seed,style='balanced'){
 const random=rng(seed),levels={},wallet=[0,0,80],lanes=[],holdings=[],history=[],purchases=[],unlockRounds={};
 let attackTimer=0,cursor=0,finished=null,activeRound=0,cumulativeXp=0;
 const incomeTotal=[0,0,0];
 function size(){return Math.max(3,(levels.P7||0)>0?7+levels.P7*2:3+(levels.P1||0)*2);}
 function buyPlanter(index){const p=planters[index];if(wallet[p.currency]<p.cost)return false;
   // Greedy actual rectangular packing into the currently unlocked square.
   const g=size(),taken=new Set(holdings.flatMap(h=>h.cells));let cells=null;
   for(const [w,h] of [p.size,[p.size[1],p.size[0]]]){for(let y=0;y+h<=g&&!cells;y++)for(let x=0;x+w<=g&&!cells;x++){let c=[];for(let yy=y;yy<y+h;yy++)for(let xx=x;xx<x+w;xx++)c.push(xx+','+yy);if(c.every(v=>!taken.has(v)))cells=c;}}
   if(!cells)return false;wallet[p.currency]-=p.cost;holdings.push({index,cells,round:activeRound});
   for(let i=0;i<cells.length;i++)lanes.push({index,timer:random()*stats(levels)[S.PlantSpawnRate],plant:null,hp:0});return true;}
 buyPlanter(0);buyPlanter(1);
 const starter=group('H1')[0];wallet[2]-=starter.tiers[0].cost;levels[starter.id]=1;
 purchases.push({round:0,id:starter.id,level:1,cost:starter.tiers[0].cost,currency:2});
 const priorities={H1:0,Z1:1,Z2:2,E1:3,E2:4,E3:5,H6:6,H2:7,E5:8,Z4:9,E4:10,H7:11,E7:12,H3:13,Z5:14,H4:15,H10:16,H5:17,H11:18};
 for(let round=1;round<=130;round++){
  activeRound=round;
  const st=stats(levels),before=[...wallet],fps=30,frames=Math.round(st[S.RoundDuration]*fps);let kills=0,xpIncome=0;
  const intervalFrames=st[S.PlantSpawnRate]*fps,attackFrames=st[S.AttackSpeed]*fps;
  const tables=planters.map(p=>{let total=0;const entries=p.table.map(e=>{total+=e.weight*(1+st[S.RareSpawnChance]*.01*[0,1,1.5,2,3][e.plant.rarity]);return{plant:e.plant,end:total,hp:health(e.plant,round)};});return {total,entries};});
  const coverage=Math.min(12,Math.max(4,Math.floor(Math.PI*st[S.AreaRadius]**2))); // explicit average mouse coverage assumption
  for(let frame=0;frame<frames;frame++){
   for(const lane of lanes){if(lane.plant)continue;lane.timer+=1/fps;if(lane.timer*fps+1e-7<intervalFrames)continue;lane.timer=0;
    const table=tables[lane.index],roll=random()*table.total;const entry=table.entries.find(e=>roll<e.end);if(entry){lane.plant=entry.plant;lane.hp=entry.hp;}}
   attackTimer+=1/fps;if(attackTimer*fps+1e-7<attackFrames)continue;attackTimer=0;
   let count=0,start=cursor;for(let offset=0;offset<lanes.length&&count<coverage;offset++){const ix=(start+offset)%lanes.length,lane=lanes[ix];if(!lane.plant)continue;count++;cursor=(ix+1)%lanes.length;
    let damage=roundEven(st[S.HarvestDamage]*(.85+.3*random()));if(random()<st[S.CritChance])damage=roundEven(damage*st[S.CritMultiplier]);lane.hp-=damage;if(lane.hp>0)continue;
    const type=lane.plant.resource,reward=roundEven(lane.plant.reward*st[[S.StoneGainMultiplier,S.IronGainMultiplier,S.GoldGainMultiplier][type]]);
    xpIncome+=roundEven(lane.plant.xp*st[S.XPGainMultiplier]);
    wallet[type]+=reward;incomeTotal[type]+=reward;kills++;lane.plant=null;lane.timer=0;}
  }
  const income=wallet.map((v,i)=>v-before[i]);
  // Infrastructure desired dates are BUYER assumptions, never gameplay gates.
  const infrastructure=[['P1',1,4],['P2',1,5],['P5',1,10],['P1',2,13],['P8',1,16],['P3',1,18],['P6',1,22],['P4',1,24],['P7',1,45],['P7',2,70]];
  const eligible=n=>(levels[n.id]||0)<n.tiers.length&&((levels[n.id]||0)>0||n.prerequisites.every(p=>(levels[p.id]||0)>=p.level));
  function buy(n){const lvl=levels[n.id]||0,t=n.tiers[lvl];if(wallet[t.costType]<t.cost)return false;wallet[t.costType]-=t.cost;levels[n.id]=lvl+1;purchases.push({round,id:n.id,level:lvl+1,cost:t.cost,currency:t.costType});if(n.unlockType)unlockRounds[n.id]??=round;return true;}
  for(const [id,lvl,when]of infrastructure){const n=group(id)[0];if(round>=when&&(levels[id]||0)<lvl&&eligible(n))buy(n);}
  for(const [index,id,when]of [[2,'P2',6],[3,'P5',11],[4,'P8',17]])if(round>=when&&levels[id]&&!holdings.some(h=>h.index===index))buyPlanter(index);
  // A modest farm: expand gradually to 48 cells, not an ideal fully packed 121-cell grid.
  const wanted=round<10?6:round<20?15:round<45?21:round<70?30:48;
  if(lanes.length<wanted){const idx=levels.P8?4:levels.P5?3:levels.P2?2:0;buyPlanter(idx);}
  // Reserve near-term unlocks instead of accidentally spending their currency on an optional skill.
  const reserve=[0,0,0];for(const[id,lvl,when]of infrastructure){if(when>round+2||when<round-5||(levels[id]||0)>=lvl)continue;const n=group(id)[0],t=n.tiers[levels[id]||0];reserve[t.costType]+=t.cost;}
  const bias={};for(const n of nodes)bias[n.id]=random();
  for(let guard=0;guard<300;guard++){
   const candidates=nodes.filter(n=>!n.id.startsWith('P')&&eligible(n));
   if(!candidates.length)break;
   const currentStats=stats(levels),commonHp=health(Object.values(plants).find(p=>p.rarity===0),round);
   const priority=n=>{if(n.id==='H1-A')return -100;const id=n.id.split('-')[0],when=n.targetRounds[0];let p=when+(priorities[id]??22)*.4+(levels[n.id]||0)*.5;
    if(currentStats[S.HarvestDamage]<commonHp&&/^H(?:[1-9]|10|11)$/.test(id))p-=100;
    if(style==='economy'&&id.startsWith('E'))p-=10;if(style==='combat'&&id.startsWith('H'))p-=10;
    return p+bias[n.id]*8;};
   candidates.sort((a,b)=>priority(a)-priority(b));
   const saving=new Set();let chosen=null;
   for(const n of candidates){const t=n.tiers[levels[n.id]||0];if(saving.has(t.costType))continue;
    if(wallet[t.costType]-Math.min(reserve[t.costType],wallet[t.costType]*.35)>=t.cost){chosen=n;break;}saving.add(t.costType);}
   if(!chosen)break;buy(chosen);
  }
  if(!finished&&nodes.every(n=>levels[n.id]===n.tiers.length))finished=round;
  cumulativeXp+=xpIncome;
  history.push({round,kills,income,xpIncome,cumulativeXp,xpMultiplier:st[S.XPGainMultiplier],wallet:[...wallet],bought:purchases.filter(p=>p.round===round).length,total:purchases.length,cells:lanes.length,damage:st[S.HarvestDamage],spawn:st[S.PlantSpawnRate],attack:st[S.AttackSpeed]});
 }
 return{seed,style,finished,unlocks:unlockRounds,planters:holdings.map(h=>({index:h.index,round:h.round})),incomeTotal,history,purchases,missing:nodes.filter(n=>levels[n.id]!==n.tiers.length).map(n=>n.id)};
}
const simulations=[];
for(const style of ['balanced','economy','combat'])for(const seed of [113,727,1999,7,41,333,2026,8119,40009,65521])simulations.push(simulate(seed,style));
const totals=[0,0,0];nodes.forEach(n=>n.tiers.forEach(t=>totals[t.costType]+=t.cost));
const maxStats=stats(Object.fromEntries(nodes.map(n=>[n.id,n.tiers.length])));
const completed=simulations.filter(s=>s.finished).map(s=>s.finished).sort((a,b)=>a-b);
const summary={trials:simulations.length,completed:completed.length,min:completed[0],mean:completed.reduce((s,v)=>s+v,0)/completed.length,max:completed.at(-1),allPlanterUnlockMax:Math.max(...simulations.map(s=>s.unlocks.P8||999)),lastFirstPlanter:Math.max(...simulations.map(s=>Math.max(...[0,1,2,3,4].map(i=>s.planters.find(p=>p.index===i)?.round??999))))};
const report={model:'30 FPS seeded lifecycle + wallet/purchases; no tiles, resonance, duplicate, skip rewards, or round income multipliers; 4–12 assumed effective targets; 48-cell farm goal; scripted buyer infrastructure dates are not game locks; buyers invest in starting damage and react when common HP exceeds damage',summary,totals,maxStats:Object.fromEntries(names.map((name,i)=>[name,maxStats[i]])),simulations};
fs.mkdirSync(path.join(root,'Logs'),{recursive:true});fs.writeFileSync(path.join(root,'Logs/EconomyBalanceSimulation.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({totals,summary,results:simulations.map(s=>({style:s.style,seed:s.seed,finished:s.finished,P8:s.unlocks.P8,missing:s.missing.length}))}));
if(process.argv.includes('--apply')){
 if(summary.completed!==summary.trials||summary.allPlanterUnlockMax>20)throw Error('Candidate failed completion/unlock checks; refusing to apply.');
 const lookup=Object.fromEntries(nodes.map(n=>[n.id,n]));
 for(const n of nodes){const p='Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/'+(n.assetName||n.id)+'.asset';let text=read(p);
  const pre=n.prerequisites.length?'  prerequisites:\n'+n.prerequisites.map(p=>`  - node: {fileID: 11400000, guid: ${lookup[p.id].guid}, type: 2}\n    level: ${p.level}\n`).join(''):'  prerequisites: []\n';
  text=text.replace(/  prerequisites:[\s\S]*?(?=  (?:targetRounds|gridPosition|tiers):)/,pre);
  text=text.replace(/  targetRounds: [^\r\n]+/,`  targetRounds: {x: ${n.targetRounds[0]}, y: ${n.targetRounds[1]}}`);
  let tiers='  tiers:\n';for(const t of n.tiers){tiers+=`  - costType: ${t.costType}\n    cost: ${t.cost}\n`;tiers+=t.effects.length?'    effects:\n':'    effects: []\n';for(const e of t.effects)tiers+=`    - statType: ${e.statType}\n      target: ${e.target}\n      operation: ${e.operation}\n      value: ${e.value}\n`;}
  text=text.replace(/  tiers:[\s\S]*?(?=  unlockType:)/,tiers);fs.writeFileSync(path.join(root,p),text);
 }
 manifest.nodes=nodes;fs.writeFileSync(path.join(root,'Docs/FinalSkillTree.json'),JSON.stringify(manifest,null,2)+'\n');
 planters.forEach((p,i)=>{const name='Assets/ScriptableObjects/Planters/'+planterFiles[i]+'.asset';let text=read(name).replace(/^  costType: .*$/m,'  costType: '+p.currency).replace(/^  cost: .*$/m,'  cost: '+p.cost);fs.writeFileSync(path.join(root,name),text);});
 for(const plant of Object.values(plants)){const file=plantFolder+'/'+plant.name+'.asset';fs.writeFileSync(path.join(root,file),read(file).replace(/^  rewardAmount: .*$/m,'  rewardAmount: '+plant.reward));}
 console.log('Applied explicit costs, stat effects, unlock prerequisites and target metadata. GUIDs, positions, icons and scene are preserved.');
}
if(process.argv.includes('--verify-assets')){
 for(const n of nodes){const raw=read('Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/'+(n.assetName||n.id)+'.asset');
  const tiers=raw.split('  tiers:')[1].split('  unlockType:')[0].split('  - costType: ').slice(1);
  if(tiers.length!==n.tiers.length)throw Error('Tier count mismatch '+n.id);
  tiers.forEach((part,i)=>{const expected=n.tiers[i];if(parseInt(part)!==expected.costType||Number(part.match(/cost: (\d+)/)[1])!==expected.cost)throw Error('Price mismatch '+n.id);
   const effects=[...part.matchAll(/statType: (\d+)\s+target: (\d+)\s+operation: (\d+)\s+value: ([^\r\n]+)/g)].map(m=>({statType:+m[1],target:+m[2],operation:+m[3],value:+m[4]}));
   if(effects.length!==expected.effects.length||effects.some((e,j)=>Object.keys(e).some(k=>Math.abs(e[k]-expected.effects[j][k])>1e-6)))throw Error('Effect mismatch '+n.id);
  });
  const pre=raw.split('  prerequisites:')[1].split(/  (?:gridPosition|targetRounds|tiers):/)[0];
  const actual=[...pre.matchAll(/guid: (\w+), type: 2\}\s+level: (\d+)/g)];
  if(actual.length!==n.prerequisites.length||actual.some((m,i)=>m[1]!==nodes.find(x=>x.id===n.prerequisites[i].id).guid||+m[2]!==n.prerequisites[i].level))throw Error('Prerequisite mismatch '+n.id);
 }
 console.log(`PASS: all ${nodes.length} applied skill assets match candidate/manifest prices, effects and prerequisites.`);
}
