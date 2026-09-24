// Explicit authoring only: preserves asset GUIDs and unrelated serialized data.
const fs = require('fs');
const asset = 'Assets/Resources/ResonanceRules.asset';
const stat = (s,v,op=2)=>({kind:0,statType:s,operation:op,value:v});
const effect = (k,v)=>({kind:k,statType:0,operation:2,value:v});
const rules = [
 ['focus','Odak',6,22,[[2,[stat(22,.5)]],[3,[stat(22,1)]],[4,[stat(22,2)]]]],
 ['fame','Şöhret',3,11,[[2,[stat(11,.5)]],[3,[stat(11,1)]],[4,[stat(11,2)]]]],
 ['bounty','Bolluk',5,7,[[2,[effect(1,.25)]],[3,[effect(1,.5)]],[4,[effect(1,1)]]]],
 ['wisdom','Bilgelik',1,10,[[2,[stat(10,.5)]],[3,[stat(10,1)]],[4,[stat(10,2)]]]],
 ['crystal-garden','Kristal Bahçe',2,6,[[2,[stat(6,10,0)]],[3,[stat(6,20,0)]],[4,[stat(6,35,0)]]]],
 ['empowered-harvest','Güçlendirilmiş Hasat',6,22,[[1,[effect(2,.5)]],[2,[effect(2,1)]]],null,true],
 ['precious-harvest','Değerli Hasat',3,11,[[2,[effect(3,2)]]],2],
 ['fertile-soil','Verimli Toprak',5,7,[[2,[effect(1,1)]]],0],
 ['electric-wisdom','Elektrik Bilgisi',1,10,[[2,[effect(4,9)]]],9],
 ['rare-nursery','Nadir Fidanlık',2,6,[[2,[stat(6,25,0),stat(5,-.15)]]],0]
];
let yaml = fs.readFileSync(asset,'utf8').split('  rules:')[0]+'  rules:\n';
for(const [id,name,tile,s,tiers,secondary,behavior] of rules) {
 yaml += '  - resonanceName: '+name+'\n    id: '+id+'\n    tileType: '+tile+'\n    statType: '+s+
 '\n    requiresSecondary: '+(secondary!=null?1:0)+'\n    secondaryType: '+(secondary??0)+
 '\n    secondaryCount: 1\n    requiresAnyBehavior: '+(behavior?1:0)+'\n    tiers:\n';
 for(const [count,effects] of tiers) {
 yaml+='    - requiredCount: '+count+'\n      multiplier: '+(effects[0].operation===2?1+effects[0].value:1)+'\n      effects:\n';
 for(const e of effects) yaml+='      - kind: '+e.kind+'\n        statType: '+e.statType+'\n        operation: '+e.operation+'\n        value: '+e.value+'\n';
 }
}
fs.writeFileSync(asset,yaml);
for(const file of fs.readdirSync('Assets/ScriptableObjects/GridModifiers/Energy').filter(x=>x.endsWith('.asset'))) {
 const path='Assets/ScriptableObjects/GridModifiers/Energy/'+file;
 let text=fs.readFileSync(path,'utf8');
 text=text.replace(/(modifierRanges:\s*\r?\n\s*- statType:) 0/, '$1 11');
 fs.writeFileSync(path,text);
}
console.log('Authored 10 resonance rules; corrected Energy to HarvestScoreMultiplier. No scene/prefab/price edits.');

