const fs = require('node:fs');
const assert = require('node:assert/strict');
const backup = 'Backups/PlanterPivot-20260906/';
const vec = s => [...s.matchAll(/[xyz]: ([^,}]+)/g)].map(m => Number(m[1]));
const blocks = s => s.split(/(?=^--- !u!)/m);
const id = b => b.match(/^--- !u!\d+ &(-?\d+)/)?.[1];
const pos = b => vec(b.match(/m_LocalPosition: (\{[^}]+\})/)[1]);
const near = (a,b) => a.forEach((v,i) => assert.ok(Math.abs(v-b[i]) < 1e-5, `${a} != ${b}`));
const add = (a,b) => a.map((v,i)=>v+b[i]);
const rotate = (p, angle) => {
  const a = angle * Math.PI / 180;
  return [Math.cos(a)*p[0]+Math.sin(a)*p[2], p[1], -Math.sin(a)*p[0]+Math.cos(a)*p[2]];
};
let cases = 0;
for (const file of fs.readdirSync('Assets/Prefabs').filter(f => /^Grass Planter .*\.prefab$/.test(f))) {
  const path = `Assets/Prefabs/${file}`;
  const before = blocks(fs.readFileSync(backup+path,'utf8'));
  const after = blocks(fs.readFileSync(path,'utf8'));
  assert.deepEqual(before.map(id),after.map(id));
  const oldObject = before.find(b=>b.includes('m_Name: SolPivot'));
  const pivot = before.find(b=>b.startsWith('--- !u!4 ') && b.includes(`m_GameObject: {fileID: ${id(oldObject)}}`));
  const offset = pos(pivot);
  near(pos(after.find(b=>id(b)===id(pivot))),[0,0,0]);
  for (const b of before) {
    const changed = after.find(a=>id(a)===id(b));
    let oldPos, newPos;
    if(b.startsWith('--- !u!4 ') && b.includes(`m_Father: {fileID: ${id(pivot)}}`)) {
      oldPos = add(pos(b),offset); newPos = pos(changed);
    } else if(b.startsWith('--- !u!1001 ') && b.includes(`m_TransformParent: {fileID: ${id(pivot)}}`)) {
      const read = block => ['x','y','z'].map(axis=>Number(block.match(new RegExp(`propertyPath: m_LocalPosition\\.${axis}\\r?\\n      value: ([^\\r\\n]+)`))[1]));
      oldPos = add(read(b),offset); newPos=read(changed);
    } else continue;
    // Preserving each direct child's transform preserves its entire model subtree.
    for(const angle of [0,90,180,270]) { near(rotate(oldPos,angle),rotate(newPos,angle)); cases++; }
  }
  const [sx,sz] = file.match(/(\d)x(\d)/).slice(1).map(Number);
  const center = [(sx-1),0,(sz-1)]; // scene cell size is 2
  for(const angle of [0,90,180,270]) {
    const occupied = [];
    for(let x=0;x<sx;x++) for(let z=0;z<sz;z++) {
      occupied.push(rotate([2*x,0,2*z],angle));
      near(add(rotate(center,angle),rotate([2*x-center[0],0,2*z-center[2]],angle)),occupied.at(-1));
      cases++;
    }
    const average = occupied.reduce((sum,p)=>add(sum,p),[0,0,0]).map(v=>v/occupied.length);
    near(average,rotate(center,angle));
  }
  // Manual points must preserve both their references and their local height.
  const spawnRefs = s => s.match(/  spawnPoints:\r?\n(?:  - .*\r?\n)+/)[0];
  assert.equal(spawnRefs(before.join('')),spawnRefs(after.join('')));
}
console.log(`PASS: ${cases} transform/footprint checks; 5 prefabs, all 4 rotations; object IDs and spawn references preserved.`);
