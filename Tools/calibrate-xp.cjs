// Offline design tool. Reads every round's accumulated XP, never extrapolates a
// late checkpoint backwards. Only --apply writes the explicitly approved curve.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..'),p=s=>path.join(root,s);
const report=JSON.parse(fs.readFileSync(p('Logs/EconomyBalanceSimulation.json'),'utf8'));
const median=a=>{a.sort((a,b)=>a-b);return (a[Math.floor((a.length-1)/2)]+a[Math.ceil((a.length-1)/2)])/2;};
const targets=[[5,5],[10,12],[20,25],[40,45],[65,70],[100,100],[118,121]];
const anchors=[[1,0],...targets.map(([round,level])=>[level,Math.round(median(report.simulations.map(s=>s.history[round-1].cumulativeXp)))])];
// Monotone cubic cumulative interpolation avoids hard cost jumps at checkpoints.
// Harmonic tangents preserve increasing total XP and do not overshoot anchors.
const h=anchors.slice(1).map((a,i)=>a[0]-anchors[i][0]);
const sec=anchors.slice(1).map((a,i)=>(a[1]-anchors[i][1])/h[i]);
const slopes=anchors.map((_,i)=>{
 if(i===0)return sec[0];if(i===anchors.length-1)return sec.at(-1);
 const w1=2*h[i]+h[i-1],w2=h[i]+2*h[i-1];
 return (w1+w2)/(w1/sec[i-1]+w2/sec[i]);
});
function cumulative(level){
 const i=Math.min(anchors.length-2,anchors.findIndex((a,j)=>j>0&&a[0]>=level)-1);
 const t=(level-anchors[i][0])/h[i],t2=t*t,t3=t2*t;
 return (2*t3-3*t2+1)*anchors[i][1]+(t3-2*t2+t)*h[i]*slopes[i]+(-2*t3+3*t2)*anchors[i+1][1]+(t3-t2)*h[i]*slopes[i+1];
}
const costs=Array.from({length:120},(_,i)=>Math.round(cumulative(i+2))-Math.round(i===0?0:cumulative(i+1)));
if(costs.some(v=>!Number.isFinite(v)||v<1))throw Error('Invalid XP curve');
function levelAt(xp){let level=1;while(xp>=costs[Math.min(level-1,119)]){xp-=costs[Math.min(level-1,119)];level++;}return level+xp/costs[Math.min(level-1,119)];}
const quantile=(a,q)=>{a.sort((a,b)=>a-b);const ix=(a.length-1)*q,lo=Math.floor(ix);return a[lo]+(a[Math.ceil(ix)]-a[lo])*(ix-lo);};
const checkpoints=targets.map(([round,targetLevel])=>{const levels=report.simulations.map(s=>levelAt(s.history[round-1].cumulativeXp));return {round,targetLevel,P10:quantile([...levels],.1),P50:quantile([...levels],.5),P90:quantile([...levels],.9)};});
fs.writeFileSync(p('Logs/EconomyXpCalibration.json'),JSON.stringify({source:'30 simulated purchase histories, no tiles/resonance or XP card feedback; design calibration, not human playtest',anchors,costs,checkpoints},null,2));
console.log(JSON.stringify({anchors,checkpoints}));
if(process.argv.includes('--apply')){
 const file=p('Assets/ScriptableObjects/Prograsiondata/Xp çarpanı.asset');
 const raw=fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n').replace(/  useAuthoredRequirements:[\s\S]*$/,'').trimEnd();
 fs.writeFileSync(file,raw+'\n  useAuthoredRequirements: 1\n  xpRequirements:\n'+costs.map(c=>'  - '+c+'\n').join(''));
 console.log('Applied 120 level costs; level 121+ retains the last XP cost. Legacy curve fields remain available.');
}
if(process.argv.includes('--verify-assets')){
 const raw=fs.readFileSync(p('Assets/ScriptableObjects/Prograsiondata/Xp çarpanı.asset'),'utf8');
 const actual=[...raw.matchAll(/^  - (\d+)\r?$/gm)].map(m=>+m[1]);
 if(!raw.includes('useAuthoredRequirements: 1')||JSON.stringify(actual)!==JSON.stringify(costs))throw Error('Live XP curve differs from calibration');
 console.log('PASS: live XP requirements match cumulative history calibration.');
}
