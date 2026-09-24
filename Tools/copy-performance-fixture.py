"""Copy plant prefab GUID dependencies to the isolated test project; never edit source assets."""
from pathlib import Path
import re
import shutil

repo = Path(__file__).resolve().parents[1]
target = repo / 'Library/VerificationProject'
guid_pattern = re.compile(r'guid:\s*([0-9a-f]{32})')
index = {}
for meta in (repo / 'Assets').rglob('*.meta'):
    match = guid_pattern.search(meta.read_text(encoding='utf-8-sig', errors='replace'))
    if match:
        index[match.group(1)] = Path(str(meta)[:-5])
pending = list((repo / 'Assets/Prefabs/Plants').glob('*.prefab'))
seen = set()
while pending:
    src = pending.pop()
    if src in seen or not src.is_file():
        continue
    seen.add(src)
    dst = target / src.relative_to(repo)
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    meta = Path(str(src) + '.meta')
    if meta.exists():
        shutil.copy2(meta, Path(str(dst) + '.meta'))
        pending.extend(index[g] for g in guid_pattern.findall(meta.read_text(errors='replace')) if g in index)
    if src.suffix.lower() in ('.prefab', '.asset', '.mat', '.shadergraph', '.vfx'):
        pending.extend(index[g] for g in guid_pattern.findall(src.read_text(errors='replace')) if g in index)
    if src.suffix.lower() in ('.shader', '.hlsl', '.cginc'):
        for include in re.findall(r'#include(?:_with_pragmas)?\s+"([^"]+)"', src.read_text(errors='replace')):
            dependency = repo / include if include.startswith('Assets/') else src.parent / include
            if dependency.is_file():
                pending.append(dependency.resolve())
print(f'Copied {len(seen)} plant fixture assets and their metadata.')
