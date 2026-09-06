const fs = require('node:fs');
const assert = require('node:assert/strict');
const dir = 'Assets/Prefabs';
const vector = s => [...s.matchAll(/[xyz]: ([^,}]+)/g)].map(m => Number(m[1]));
const position = b => vector(b.match(/m_LocalPosition: (\{[^}]+\})/)[1]);
const fmt = v => `{x: ${v[0]}, y: ${v[1]}, z: ${v[2]}}`;
for (const file of fs.readdirSync(dir).filter(f => /^Grass Planter .*\.prefab$/.test(f))) {
  const path = `${dir}/${file}`;
  let original = fs.readFileSync(path, 'utf8');
  assert.equal(original, fs.readFileSync(`Backups/PlanterPivot-20260906/${path}`, 'utf8'));
  let blocks = original.split(/(?=^--- !u!)/m);
  const gameObject = blocks.find(b => /m_Name: SolPivot\r?\n/.test(b));
  assert.ok(gameObject, file);
  const objectId = gameObject.match(/^--- !u!1 &(\d+)/)[1];
  const pivot = blocks.find(b => b.startsWith('--- !u!4 ') && b.includes(`m_GameObject: {fileID: ${objectId}}`));
  const pivotId = pivot.match(/^--- !u!4 &(\d+)/)[1];
  assert.match(pivot, /m_LocalScale: \{x: 1, y: 1, z: 1\}/);
  assert.ok(vector(pivot.match(/m_LocalRotation: (\{[^}]+\})/)[1]).every(v => v === 0));
  const offset = position(pivot);
  let count = 0;
  blocks = blocks.map(b => {
    if (b === gameObject) return b.replace('m_Name: SolPivot', 'm_Name: Visual').replace('m_IsActive: 0', 'm_IsActive: 1');
    if (b === pivot) return b.replace(/m_LocalPosition: \{[^}]+\}/, 'm_LocalPosition: {x: 0, y: 0, z: 0}');
    if (b.startsWith('--- !u!4 ') && b.includes(`m_Father: {fileID: ${pivotId}}`)) {
      const before = position(b);
      const after = before.map((v,i) => Number((v + offset[i]).toFixed(7)));
      after.forEach((v,i) => assert.ok(Math.abs(v - before[i] - offset[i]) < 1e-6));
      count++;
      return b.replace(/m_LocalPosition: \{[^}]+\}/, `m_LocalPosition: ${fmt(after)}`);
    }
    if (b.startsWith('--- !u!1001 ') && b.includes(`m_TransformParent: {fileID: ${pivotId}}`)) {
      for (const [i,axis] of ['x','y','z'].entries()) {
        const re = new RegExp(`(propertyPath: m_LocalPosition\\.${axis}\\r?\\n      value: )([^\\r\\n]+)`, 'g');
        assert.equal([...b.matchAll(re)].length, 1);
        b = b.replace(re, (_, prefix, value) => prefix + Number((Number(value) + offset[i]).toFixed(7)));
      }
      count++;
    }
    // Older preview geometry remains serialized for compatibility, but is never used.
    if (b.startsWith('--- !u!1 ') && /m_Name: (GhostMode|PlanterGhostMode)/.test(b))
      b = b.replace('m_IsActive: 1', 'm_IsActive: 0');
    return b;
  });
  assert.ok(count > 0);
  fs.writeFileSync(path, blocks.join(''));
  console.log(`${file}: ${count} child transforms preserved; Visual pivot zeroed`);
}
