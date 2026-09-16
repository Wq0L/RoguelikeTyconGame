const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..');
const manifestPath=path.join(root,'Docs/FinalSkillTree.json');
const manifest=JSON.parse(fs.readFileSync(manifestPath,'utf8'));
const folder=path.join(root,'Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree');
const scenePath=path.join(root,'Assets/Scenes/GameScene.unity');
let scene=fs.readFileSync(scenePath,'utf8');
for(const [i,n] of manifest.nodes.entries()){
 const oldName=n.assetName||n.id;
 let name=n.name.replace(/ [ABCD]$/,m=>' - '+({A:1,B:2,C:3,D:4}[m.trim()]));
 name=name.split(' ').map(w=>w.length?w[0].toLocaleUpperCase('tr-TR')+w.slice(1):w).join(' ');
 n.name=name;n.assetName=name;
 const before=path.join(folder,oldName+'.asset'),after=path.join(folder,name+'.asset');
 if(before!==after&&fs.existsSync(after))throw Error('Name collision: '+name);
 let asset=fs.readFileSync(before,'utf8').replace(/^  m_Name:.*$/m,'  m_Name: '+JSON.stringify(name)).replace(/^  nodeName:.*$/m,'  nodeName: '+JSON.stringify(name));
 fs.writeFileSync(before,asset);
 if(before!==after){fs.renameSync(before,after);fs.renameSync(before+'.meta',after+'.meta');}
 const instance=8100000000+i*10;
 const re=new RegExp('(^--- !u!1001 &'+instance+'\\r?\\n[\\s\\S]*?)(?=^--- !u!|$(?![\\s\\S]))','m');
 if(!re.test(scene))throw Error('Missing instance '+instance);
 scene=scene.replace(re,block=>block.replace(/(propertyPath: m_Name\r?\n      value:)[^\r\n]*/,(_,prefix)=>prefix+' '+JSON.stringify(name)));
}
fs.writeFileSync(scenePath,scene);
fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2));
// Keep regeneration tools aligned without regenerating any gameplay data or layout.
for(const file of ['build-final-skill-tree.cjs','author-final-tree-scene.cjs','verify-final-tree-scene.cjs']){
 const p=path.join(__dirname,file);let s=fs.readFileSync(p,'utf8');
 s=s.replaceAll("n.id+'.asset'","(n.assetName||n.id)+'.asset'").replaceAll("n.id+'.asset.meta'","(n.assetName||n.id)+'.asset.meta'");
 s=s.replace("'m_Name':n.id+' '+n.name+' Node'","'m_Name':JSON.stringify(n.name)");
 s=s.replace('m_Name: ${n.id}', 'm_Name: ${JSON.stringify(n.name)}');
 s=s.replace('if(old&&old.grid){','if(old){n.name=old.name;n.assetName=old.assetName;}if(old&&old.grid){');
 fs.writeFileSync(p,s);
}
console.log('Renamed '+manifest.nodes.length+' scene nodes and SO assets, preserving GUIDs, positions and effects.');
