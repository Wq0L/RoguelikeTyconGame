"""Protect generated data against unnoticed Inspector edits before any output is changed."""
import hashlib
import json
from pathlib import Path

PATTERNS = (
    'ScriptableObjects/Skill Tree Upgrades/DengeV1/*',
    'ScriptableObjects/Balance/*DengeV1.asset*',
    'ScriptableObjects/SegmentEvents/DengeV1/*',
    'ScriptableObjects/BossRewards/DengeV1/*',
    'ScriptableObjects/Bosses/BossPool_DengeV1.asset*',
    'ScriptableObjects/RunProfiles/Run50_DengeV1.asset*',
)


# patterns: another generator's own output set (e.g. Tools/Balance/KirilmaV1). Default: the DengeV1 outputs above.
def snapshot(assets, patterns=None):
    assets = Path(assets)
    return {str(p.relative_to(assets)).replace('\\', '/'): hashlib.sha256(p.read_bytes().replace(b'\r\n', b'\n')).hexdigest()
            for pattern in (patterns or PATTERNS) for p in assets.glob(pattern) if p.is_file()}


def verify(assets, manifest, patterns=None, source='denge_v1_params.py'):
    manifest = Path(manifest)
    if not manifest.exists():
        raise SystemExit('Missing generation manifest. Do not regenerate until existing outputs have been reviewed.')
    expected = json.loads(manifest.read_text(encoding='utf-8'))
    current = snapshot(assets, patterns)
    changed = sorted(k for k in expected.keys() | current.keys() if expected.get(k) != current.get(k))
    if changed:
        raise SystemExit('Generated data changed outside the generator; no files written.\n'
                         'Reconcile these Inspector edits into %s, then review/update the manifest.\n' % source + '\n'.join(changed))


def record(assets, manifest, patterns=None):
    Path(manifest).write_text(json.dumps(snapshot(assets, patterns), indent=2, sort_keys=True) + '\n', encoding='utf-8')
