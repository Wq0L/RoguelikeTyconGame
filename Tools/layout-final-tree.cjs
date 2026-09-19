// Explicit authored XY lanes. No rounding or nearest-empty-cell placement.
const fs=require('fs'),path=require('path');
const p=path.join(__dirname,'../Docs/FinalSkillTree.json');
const data=JSON.parse(fs.readFileSync(p,'utf8'));
const bases={
 H1:[0,2],H2:[0,7],H3:[0,12],H4:[0,17],H5:[0,22],
 H6:[-5,7],H7:[-5,12],H8:[-5,17],H9:[-5,22],
 H10:[5,22],H11:[5,27],H12:[-10,12],H13:[-10,17],
 Z1:[-2,0],Z2:[-10,0],Z3:[-15,0],Z4:[-10,5],Z5:[-15,-5],
 Z6:[-20,0],Z7:[-25,0],Z8:[-15,5],Z9:[-20,-5],Z10:[-30,0],
 P1:[2,0],P2:[5,0],P3:[7,2],P5:[9,-2],P6:[12,0],P7:[7,-5],P8:[12,-5],
 E1:[0,-2],E2:[-5,-7],E3:[0,-7],E4:[5,-12],E5:[0,-12],
 E6:[-10,-12],E7:[-5,-12],E8:[0,-17],E9:[-5,-17],E10:[-10,-17],E11:[0,-22]
};
const occupied=new Set(['0,0']);
for(const n of data.nodes){
 const [group,suffix]=n.id.split('-');const base=bases[group];
 if(!base)throw Error('Unassigned group '+group);
 const step=suffix?'ABCD'.indexOf(suffix):0;
 if(step<0)throw Error('Unknown step '+suffix);
 let [x,y]=base;
 if(group[0]==='Z')y+=(y<0?-1:1)*step;
 else x+=(x<0?-1:1)*step;
 n.grid=[x,y];n.layout=[x*150,y*150];
 if(occupied.has(n.grid.join(',')))throw Error('Coordinate collision '+n.id);
 occupied.add(n.grid.join(','));
}
// No prerequisite connector may pass through an unrelated node.
const lookup=Object.fromEntries(data.nodes.map(n=>[n.id,n]));
for(const n of data.nodes)for(const pre of n.prerequisites){
 const a=lookup[pre.id].grid,b=n.grid;
 for(const other of data.nodes){
  if(other.id===n.id||other.id===pre.id)continue;
  const q=other.grid,dx=b[0]-a[0],dy=b[1]-a[1];
  const t=((q[0]-a[0])*dx+(q[1]-a[1])*dy)/(dx*dx+dy*dy);
  if(t>0&&t<1&&Math.hypot(q[0]-a[0]-t*dx,q[1]-a[1]-t*dy)<0.5)throw Error('Connector touches '+other.id+' on '+pre.id+' -> '+n.id);
 }
}
fs.writeFileSync(p,JSON.stringify(data,null,2));
console.log('PASS: 131 explicit XY positions; no overlapping nodes or connectors through unrelated nodes.');
