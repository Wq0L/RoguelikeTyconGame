// Run with: node Tools/build-final-skill-tree.cjs <approved visualization.html>
// Once exported, omit the argument to regenerate from Docs/FinalSkillTree.json.
const fs=require('fs'),path=require('path'),vm=require('vm'),crypto=require('crypto');
const root=path.resolve(__dirname,'..');
const guid=s=>crypto.createHash('md5').update('ClickerGame.FinalTree.'+s).digest('hex');
const write=(p,s)=>fs.writeFileSync(path.join(root,p),s);
const manifestPath=path.join(root,'Docs/FinalSkillTree.json');
let nodes;
if(process.argv[2]){
 const html=fs.readFileSync(process.argv[2],'utf8');
 const els=Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(m=>[m[1],{value:'',checked:false,innerHTML:'',textContent:'',addEventListener(){}}]));
 els.branch.value='H';els['focus-count'].value='3';els['focus-roll'].value='0.25';
 let exported;
 vm.runInNewContext(html.split('<script>')[1].split('</script>')[0].replace('const fmt=', 'capture({data,spec,expanded,actualPos});const fmt='),{document:{getElementById(){return {querySelector:q=>els[q.slice(1)]}}},capture:v=>exported=v});
 const {data,spec,expanded,actualPos}=exported;
 const mapping={H1:[0,1,0],H2:[0,1,0],H3:[0,1,0],H4:[0,1,0],H5:[0,1,0],H6:[0,1,1],H7:[0,1,1],H8:[0,1,1],H9:[0,1,1],H12:[3,1,0],H13:[4,1,0],Z1:[14,0,0],Z2:[2,1,2],Z3:[1,1,1],Z4:[14,0,0],Z5:[2,1,2],Z8:[14,0,0],Z9:[1,1,1],Z10:[12,5,0],E1:[7,2,1],E2:[5,2,2],E3:[8,2,1],E4:[7,2,1],E5:[9,2,1],E6:[10,2,1],E7:[5,2,2],E8:[8,2,1],E9:[6,2,0],E10:[10,2,1],E11:[11,2,1]};
 const currency={G:2,I:1,S:0};
 nodes=[];
 for(const d of data){const group=expanded[d[0]],s=spec[d[0]];let points=[],base=s&&s[2]==='mul'?1:0,at=0;
  if(s&&!['P1','P7'].includes(d[0])){let from=base;for(const to of s[0]){for(let k=1;k<=3;k++){let v=s[2]==='mul'?from*Math.pow(to/from,k/3):from+(to-from)*k/3;if(s[2]==='int')v=Math.round(v);points.push(v);}from=to;}}
  for(const n of group){const costs=n.cost.split(' / ').map(t=>({cost:parseInt(t),costType:currency[t.slice(-1)]}));
   const requirements=n.pre.trim()==='K'?[]:n.pre.trim().split(' + ').map(p=>{const parts=p.trim().split(' ');return {id:parts[0],level:parts[1]==='seviye'?Number(parts[2]):expanded[parts[0].split('-')[0]].find(x=>x.id===parts[0]).count};});
   let unlock=({P2:5,P3:3,P5:1,P6:4,P8:2})[d[0]]||0;
   let tiers=costs.map((c,i)=>{
    let effects=[];
    if(['P1','P7'].includes(d[0]))effects=[{statType:13,target:4,operation:3,value:(d[0]==='P1'?5:9)+i*2}];
    else if(d[0]==='H10'||d[0]==='H11')effects=[{statType:0,target:1,operation:2,value:d[0]==='H10'?7:8}];
    else if(d[0]==='Z6'||d[0]==='Z7')effects=[{statType:19,target:0,operation:0,value:1}];
    else if(s){const [statType,target,operation]=mapping[d[0]];let value=s[2]==='mul'?points[at+i]/base-1:points[at+i]-base;if(operation===1||d[0]==='H12')value/=100;effects=[{statType,target,operation,value}];if(d[0]==='E8')effects.push({...effects[0],statType:9});}
    return {...c,effects};
   });
   if(points.length){at+=n.count;base=points[at-1];}
   const xy=actualPos[n.id];
   nodes.push({id:n.id,name:d[1]+(group.length>1?' '+n.id.split('-')[1]:''),guid:guid(n.id),prerequisites:requirements,tiers,unlockType:unlock,layout:[(xy[0]-1575)*1.5,(1575-xy[1])*1.5],targetRounds:d[5].split('–').map(Number)});
  }
 }
 if(fs.existsSync(manifestPath)){
  const saved=JSON.parse(fs.readFileSync(manifestPath,'utf8')).nodes;
  for(const n of nodes){const old=saved.find(p=>p.id===n.id);if(old){n.name=old.name;n.assetName=old.assetName;}if(old&&old.grid){n.grid=old.grid;n.layout=old.grid.map(v=>v*150);}}
 }
 fs.writeFileSync(manifestPath,JSON.stringify({version:1,nodes},null,2));
}else nodes=JSON.parse(fs.readFileSync(manifestPath,'utf8')).nodes;
const folder='Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree';fs.mkdirSync(path.join(root,folder),{recursive:true});
write(folder+'.meta',`fileFormatVersion: 2\nguid: ${guid('folder')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n`);
const lookup=Object.fromEntries(nodes.map(n=>[n.id,n]));
for(const n of nodes){
 let y=`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ae0817a4d977add4bbd7d892df76868d, type: 3}\n  m_Name: ${JSON.stringify(n.name)}\n  m_EditorClassIdentifier: Assembly-CSharp::SkillNodeSO\n  nodeName: ${JSON.stringify(n.name)}\n  icon: {fileID: 0}\n  gridPosition: {x: ${n.grid ? n.grid[0] : Math.round(n.layout[0]/150)}, y: ${n.grid ? n.grid[1] : Math.round(n.layout[1]/150)}}\n  explicitPrerequisites: 1\n`;
 y+=n.prerequisites.length?'  prerequisites:\n':'  prerequisites: []\n';
 for(const p of n.prerequisites){if(!lookup[p.id])throw Error('Missing prerequisite '+p.id);y+=`  - node: {fileID: 11400000, guid: ${lookup[p.id].guid}, type: 2}\n    level: ${p.level}\n`;}
 y+=`  targetRounds: {x: ${n.targetRounds[0]}, y: ${n.targetRounds[1]}}\n  tiers:\n`;
 for(const tier of n.tiers){y+=`  - costType: ${tier.costType}\n    cost: ${tier.cost}\n`;y+=tier.effects.length?'    effects:\n':'    effects: []\n';for(const e of tier.effects)y+=`    - statType: ${e.statType}\n      target: ${e.target}\n      operation: ${e.operation}\n      value: ${e.value}\n`;}
 y+=`  unlockType: ${n.unlockType}\n`;
 write(folder+'/'+(n.assetName||n.id)+'.asset',y);write(folder+'/'+(n.assetName||n.id)+'.asset.meta',`fileFormatVersion: 2\nguid: ${n.guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n`);
}
console.log(`Generated ${nodes.length} skill assets / ${nodes.reduce((s,n)=>s+n.tiers.length,0)} purchases.`);
