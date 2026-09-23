const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..');
const s=fs.readFileSync(path.join(root,'Assets/Scenes/GameScene.unity'),'utf8');
const ids=[...s.matchAll(/^--- !u![0-9]+ &([0-9]+)/gm)].map(m=>m[1]),known=new Set(ids);
if(known.size!==ids.length)throw Error('Duplicate scene ID');
const missing=[...s.matchAll(/\{fileID: ([0-9]+)\}/g)].map(m=>m[1]).filter(id=>id!=='0'&&!known.has(id));
if(missing.length)throw Error('Dangling scene references: '+[...new Set(missing)]);
const blocks=s.match(/^--- !u![0-9]+ &[0-9]+[^\r\n]*\r?\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm);
const ui=blocks.find(b=>b.includes('Assembly-CSharp::SkillTreeUI'));
const manager=blocks.find(b=>b.includes('Assembly-CSharp::SkillTreeManager'));
const nodes=JSON.parse(fs.readFileSync(path.join(root,'Docs/FinalSkillTree.json'),'utf8')).nodes;
if((ui.match(/  - \{fileID:/g)||[]).length!==nodes.length)throw Error('UI list');
if((manager.match(/guid:/g)||[]).length!==nodes.length+1)throw Error('Manager list');
if(new Set(nodes.map(n=>n.grid.join(','))).size!==nodes.length)throw Error('Grid overlap');
for(let i=0;i<nodes.length;i++){
 const n=nodes[i],block=blocks.find(b=>b.startsWith('--- !u!1001 &'+(8100000000+i*10)+'\n')||b.startsWith('--- !u!1001 &'+(8100000000+i*10)+'\r'));
 if(!block||!block.includes(n.guid))throw Error('SO binding: '+n.id);
 if(!block.includes('m_TransformParent: {fileID: 1498103001}'))throw Error('Parent');
 for(const [axis,idx]of [['x',0],['y',1]]){
  const m=block.match(new RegExp('propertyPath: m_AnchoredPosition.'+axis+'\\r?\\n      value: ([^\\r\\n]+)'));
  if(!m||Number(m[1])!==n.grid[idx]*150)throw Error('Layout '+n.id);
 }
 const asset=fs.readFileSync(path.join(root,'Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree/'+(n.assetName||n.id)+'.asset'),'utf8');
 if(!asset.includes(`gridPosition: {x: ${n.grid[0]}, y: ${n.grid[1]}}`))throw Error('Asset coordinates');
 if(asset.includes('customLayout'))throw Error('Custom layout remained');
}
console.log(`PASS: ${nodes.length} authored prefab instances, manager/UI lists, SO bindings, unique XY cells and anchored positions. No dangling scene references.`);
