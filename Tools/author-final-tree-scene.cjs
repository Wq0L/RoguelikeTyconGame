// Author prefab instances in GameScene. Does not create nodes at runtime.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..');
const file=p=>path.join(root,p);
const scenePath=file('Assets/Scenes/GameScene.unity');
const manifestPath=file('Docs/FinalSkillTree.json');
const manifest=JSON.parse(fs.readFileSync(manifestPath,'utf8')),nodes=manifest.nodes;
const text=fs.readFileSync(scenePath,'utf8');
const blocks=text.match(/^--- !u!\d+ &\d+[^\r\n]*\r?\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm);
const id=b=>b.match(/^--- !u!\d+ &(\d+)/)[1];
const prefab='b32ba473a412d174cbe08a6c34196ca6',parent='1498103001';
const oldInstances=blocks.filter(b=>b.startsWith('--- !u!1001 ')&&b.includes('m_TransformParent: {fileID: '+parent+'}')&&b.includes('m_SourcePrefab: {fileID: 100100000, guid: '+prefab));
if(!oldInstances.length)throw Error('No skill node prefab instances found');
const template=oldInstances[0];
const removed=new Set(oldInstances.map(id));
for(const b of blocks){const match=b.match(/m_PrefabInstance: \{fileID: (\d+)\}/);if(match&&removed.has(match[1]))removed.add(id(b));}
const remaining=blocks.filter(b=>!removed.has(id(b)));
const occupied=new Set(['0,0']);
for(const n of nodes){
 if(!n.grid||!n.grid.every(Number.isInteger)||occupied.has(n.grid.join(',')))throw Error('Invalid or duplicate authored grid: '+n.id);
 occupied.add(n.grid.join(','));
}
let additions='',refs=[],children=[];
const existing=new Set(remaining.map(id));
for(let i=0;i<nodes.length;i++){
 const n=nodes[i],instance=8100000000+i*10,rect=instance+1,ui=instance+2;
 for(const v of [instance,rect,ui])if(existing.has(String(v)))throw Error('ID collision');
 let b=template.replace(/^--- !u!1001 &\d+/,`--- !u!1001 &${instance}`);
 const changes={'m_Name':JSON.stringify(n.name),'m_AnchoredPosition.x':n.grid[0]*150,'m_AnchoredPosition.y':n.grid[1]*150,'m_LocalPosition.x':0,'m_LocalPosition.y':0,'m_LocalPosition.z':0};
 for(const [key,value]of Object.entries(changes))b=b.replace(new RegExp('(propertyPath: '+key.replaceAll('.','\\.')+'\\r?\\n      value:)[^\\r\\n]*'),(_,prefix)=>prefix+' '+value);
 // Strip old node/spacing overrides before adding the authored bindings.
 b=b.replace(/    - target:[^\r\n]+\r?\n      propertyPath: (?:node|spacing|m_IsActive)\r?\n      value:[^\r\n]*\r?\n      objectReference:[^\r\n]*\r?\n/g,'');
 const override=(target,key,value,reference='{fileID: 0}')=>`    - target: {fileID: ${target}, guid: ${prefab}, type: 3}\n      propertyPath: ${key}\n      value: ${value}\n      objectReference: ${reference}\n`;
 b=b.replace(/    m_Modifications:\r?\n/,'    m_Modifications:\n'+override('1011748002696744292','node','',`{fileID: 11400000, guid: ${n.guid}, type: 2}`)+override('1011748002696744292','spacing',150)+override('8268545321437717030','m_IsActive',1));
 additions+=b;
 additions+=`--- !u!224 &${rect} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {fileID: 2821493626982594215, guid: ${prefab}, type: 3}\n  m_PrefabInstance: {fileID: ${instance}}\n  m_PrefabAsset: {fileID: 0}\n`;
 additions+=`--- !u!114 &${ui} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {fileID: 1011748002696744292, guid: ${prefab}, type: 3}\n  m_PrefabInstance: {fileID: ${instance}}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 179c31da4963abb4c81dae87e4a8bca1, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::SkillNodeUI\n`;
 refs.push(`  - {fileID: ${ui}}\n`);children.push(`  - {fileID: ${rect}}\n`);
 n.layout=n.grid.map(v=>v*150);
 const p=file('Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/'+(n.assetName||n.id)+'.asset');
 let asset=fs.readFileSync(p,'utf8').replace(/  gridPosition: [^\r\n]+/,`  gridPosition: {x: ${n.grid[0]}, y: ${n.grid[1]}}`).replace(/^  (?:customLayout|layoutPosition):[^\r\n]*\r?\n/gm,'');
 fs.writeFileSync(p,asset);
}
const result=remaining.map(b=>{
 if(id(b)===parent){b=b.replace(/  m_Children:\r?\n([\s\S]*?)  m_Father:/,(_,list)=>'  m_Children:\n'+list.split(/\r?\n/).filter(line=>line.trim()&&!removed.has((line.match(/fileID: (\d+)/)||[])[1])).join('\n')+'\n'+children.join('')+'  m_Father:');}
 if(b.includes('Assembly-CSharp::SkillTreeUI'))b=b.replace(/^  nodeTemplate:[^\r\n]*\r?\n/m,'').replace(/  nodeUIs:\r?\n(?:  -[^\r\n]*\r?\n)*/,'  nodeUIs:\n'+refs.join(''));
 if(b.includes('Assembly-CSharp::SkillTreeManager'))b=b.replace(/  allNodes:\r?\n(?:  -[^\r\n]*\r?\n)*/,'  allNodes:\n'+nodes.map(n=>`  - {fileID: 11400000, guid: ${n.guid}, type: 2}\n`).join(''));
 return b;
}).join('');
fs.writeFileSync(scenePath,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+result+additions).replace(/[ \t]+$/gm,''));
fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2));
console.log(`Authored ${nodes.length} scene prefab instances; replaced ${oldInstances.length} old instances. Unique integer grid positions; z=0; spacing=150.`);
