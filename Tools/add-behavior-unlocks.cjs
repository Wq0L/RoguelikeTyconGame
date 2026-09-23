// Explicit one-time authoring; appends two nodes without touching existing prefab instances.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'..'),file=p=>path.join(root,p),read=p=>fs.readFileSync(file(p),'utf8');
const manifest=JSON.parse(read('Docs/FinalSkillTree.json')),oldCount=manifest.nodes.length;
const additions=[['P9','Bumerang Orak Kartları',[13,4],'P4',60,7],['P10','Çapraz Elektrik Kartları',[7,5],'P3',80,8]];
const folder='Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/';
const fresh=[];
for(const [id,name,grid,pre,cost,unlockType]of additions){
 if(manifest.nodes.some(n=>n.id===id))continue;
 const n={id,name,assetName:name,grid,layout:grid.map(v=>v*150),guid:crypto.createHash('md5').update('ClickerGame.FinalTree.'+id).digest('hex'),
  prerequisites:[{id:pre,level:1}],tiers:[{cost,costType:1,effects:[]}],unlockType,targetRounds:[25,40]};
 const prior=manifest.nodes.find(n=>n.id===pre),template=manifest.nodes.find(n=>n.id==='P4');
 let asset=read(folder+template.assetName+'.asset').replace(/^  (m_Name|nodeName): .*$/gm,(_,key)=>'  '+key+': '+JSON.stringify(name))
 .replace(/  gridPosition: .*/,`  gridPosition: {x: ${grid[0]}, y: ${grid[1]}}`)
 .replace(/  prerequisites:[\s\S]*?(?=  targetRounds:)/,`  prerequisites:\n  - node: {fileID: 11400000, guid: ${prior.guid}, type: 2}\n    level: 1\n`)
 .replace(/  targetRounds: .*/,'  targetRounds: {x: 25, y: 40}').replace(/    cost: \d+/,'    cost: '+cost).replace(/  unlockType: \d+/,'  unlockType: '+unlockType);
 fs.writeFileSync(file(folder+name+'.asset'),asset);
 fs.writeFileSync(file(folder+name+'.asset.meta'),`fileFormatVersion: 2\nguid: ${n.guid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n`);
 manifest.nodes.push(n);fresh.push(n);
}
if(!fresh.length){console.log('Unlocks already authored.');process.exit(0);}
const sceneFile='Assets/Scenes/GameScene.unity',scene=read(sceneFile);
const blocks=scene.match(/^--- !u!\d+ &\d+[^\r\n]*\r?\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm);
const firstId=8100000000,template=blocks.find(b=>b.startsWith('--- !u!1001 &'+firstId+'\n')||b.startsWith('--- !u!1001 &'+firstId+'\r'));
const companions=blocks.filter(b=>b.includes('m_PrefabInstance: {fileID: '+firstId+'}'));
if(!template||companions.length!==2)throw Error('Unexpected skill prefab format');
let added='',rects='',uis='',sos='';
fresh.forEach((n,k)=>{
 const id=firstId+(oldCount+k)*10;
 if(scene.includes('&'+id+'\n')||scene.includes('&'+id+'\r'))throw Error('ID collision');
 let block=template.replace('&'+firstId,'&'+id);
 block=block.replace(/(propertyPath: node\r?\n      value:[^\r\n]*\r?\n      objectReference: )[^\r\n]*/,(_,p)=>p+`{fileID: 11400000, guid: ${n.guid}, type: 2}`);
 for(const [key,value]of Object.entries({'m_Name':JSON.stringify(n.name),'m_AnchoredPosition.x':n.grid[0]*150,'m_AnchoredPosition.y':n.grid[1]*150}))
  block=block.replace(new RegExp('(propertyPath: '+key.replaceAll('.','\\.')+'\\r?\\n      value:)[^\\r\\n]*'),(_,p)=>p+' '+value);
 added+=block;
 for(const c of companions)added+=c.replace(new RegExp(String(firstId),'g'),String(id)).replace('&'+(firstId+1),'&'+(id+1)).replace('&'+(firstId+2),'&'+(id+2));
 rects+=`  - {fileID: ${id+1}}\n`;uis+=`  - {fileID: ${id+2}}\n`;sos+=`  - {fileID: 11400000, guid: ${n.guid}, type: 2}\n`;
});
let result=scene.replace(/^--- !u!\d+ &\d+[^\r\n]*\r?\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm,b=>{
 if(b.startsWith('--- !u!224 &1498103001'))b=b.replace(/(  m_Children:\r?\n(?:  -[^\r\n]*\r?\n)*)/,m=>m+rects);
 if(b.includes('Assembly-CSharp::SkillTreeUI'))b=b.replace(/(  nodeUIs:\r?\n(?:  -[^\r\n]*\r?\n)*)/,m=>m+uis);
 if(b.includes('Assembly-CSharp::SkillTreeManager'))b=b.replace(/(  allNodes:\r?\n(?:  -[^\r\n]*\r?\n)*)/,m=>m+sos);
 return b;
});
fs.writeFileSync(file(sceneFile),result+added);fs.writeFileSync(file('Docs/FinalSkillTree.json'),JSON.stringify(manifest,null,2)+'\n');
console.log('Appended '+fresh.length+' skill nodes and scene bindings.');
