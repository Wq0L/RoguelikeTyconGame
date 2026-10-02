# Skill node asset okuma / yazma (Unity YAML, elle). Hem dump hem DengeV1 üretimi kullanır.
import io, os, re, glob

# Depo kökü: betik Tools/Balance/DengeV1 altındaysa üç üst klasör; başka yerden çalışıyorsa sabit yol.
_HERE = os.path.dirname(os.path.abspath(__file__))
_REPO = os.path.abspath(os.path.join(_HERE, '..', '..', '..'))
ROOT = _REPO
TREE = os.path.join(ROOT, 'Assets', 'ScriptableObjects', 'Skill Tree Upgrades', 'FinalSkillTree')
STAT = ['HarvestDamage', 'AreaRadius', 'AttackSpeed', 'CritChance', 'CritMultiplier', 'PlantSpawnRate', 'RareSpawnChance',
        'GoldGainMultiplier', 'IronGainMultiplier', 'StoneGainMultiplier', 'XPGainMultiplier', 'HarvestScoreMultiplier',
        'MutationLuck', 'GridUnlockSize', 'RoundDuration', 'StartingGoldBonus', 'StartingPlanterCount', 'ExtraCardChoice',
        'RerollCard', 'CardSkip', 'ExplosionChance', 'DuplicateChance', 'PlanterDamageMultiplier', 'TornadoChance',
        'BoomerangChance', 'ElectricChance']
OPS = ['Flat', 'AddPercent', 'MorePercent', 'Set']
RES = ['Stone', 'Iron', 'Gold']


def unescape(s):
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
        s = re.sub(r'\\u([0-9A-Fa-f]{4})', lambda m: chr(int(m.group(1), 16)), s)
        s = re.sub(r'\\x([0-9A-Fa-f]{2})', lambda m: chr(int(m.group(1), 16)), s)
        s = s.replace('\\"', '"').replace('\\\\', '\\')
    return s


def read_nodes(folder=TREE):
    nodes = {}
    for path in sorted(glob.glob(os.path.join(folder, '*.asset'))):
        text = io.open(path, encoding='utf-8').read()
        meta = io.open(path + '.meta', encoding='utf-8').read()
        guid = re.search(r'guid: ([0-9a-f]+)', meta).group(1)
        name = unescape(re.search(r'm_Name: (.*)', text).group(1))
        node = {'name': name, 'guid': guid, 'path': path, 'file': os.path.basename(path)}
        node['nodeName'] = unescape(re.search(r'nodeName: (.*)', text).group(1))
        m = re.search(r'gridPosition: \{x: (-?\d+), y: (-?\d+)\}', text)
        node['pos'] = (int(m.group(1)), int(m.group(2)))
        node['explicit'] = int(re.search(r'explicitPrerequisites: (\d)', text).group(1))
        m = re.search(r'targetRounds: \{x: (-?\d+), y: (-?\d+)\}', text)
        node['target'] = (int(m.group(1)), int(m.group(2)))
        node['unlock'] = int(re.search(r'unlockType: (\d+)', text).group(1))
        pre = text[text.index('prerequisites:'):text.index('targetRounds:')]
        node['prereq'] = [(g, int(l)) for g, l in re.findall(r'guid: ([0-9a-f]+), type: 2\}\s+level: (\d+)', pre)]
        tiers_text = text[text.index('tiers:'):text.index('unlockType:')]
        tiers = []
        for block in re.split(r'\n  - costType:', tiers_text)[1:]:
            block = 'costType:' + block
            tier = {'costType': int(re.search(r'costType: (\d)', block).group(1)), 'cost': int(re.search(r'cost: (\d+)', block).group(1)), 'effects': []}
            for st, tg, op, val in re.findall(r'statType: (\d+)\s+target: (\d+)\s+operation: (\d+)\s+value: ([-\d.e]+)', block):
                tier['effects'].append((int(st), int(tg), int(op), float(val)))
            tiers.append(tier)
        node['tiers'] = tiers
        nodes[guid] = node
    return nodes


def yaml_str(s):
    if all(ord(c) < 128 for c in s) and not any(c in s for c in ':#"\'') and s.strip() == s:
        return s
    out = ''
    for c in s:
        o = ord(c)
        if c == '"': out += '\\"'
        elif c == '\\': out += '\\\\'
        elif o < 128: out += c
        elif o < 256: out += '\\x%02X' % o
        else: out += '\\u%04X' % o
    return '"' + out + '"'


def fmt(v):
    if float(v) == int(v): return str(int(v))
    return ('%.7g' % v)


def node_yaml(name, node_name, pos, prereqs, target, tiers, unlock, script_guid='ae0817a4d977add4bbd7d892df76868d'):
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:', '  m_ObjectHideFlags: 0',
             '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}',
             '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
             '  m_Script: {fileID: 11500000, guid: %s, type: 3}' % script_guid,
             '  m_Name: ' + yaml_str(name), '  m_EditorClassIdentifier: Assembly-CSharp::SkillNodeSO',
             '  nodeName: ' + yaml_str(node_name), '  icon: {fileID: 0}', '  gridPosition: {x: %d, y: %d}' % pos, '  explicitPrerequisites: 1']
    if prereqs:
        lines.append('  prerequisites:')
        for g, l in prereqs:
            lines.append('  - node: {fileID: 11400000, guid: %s, type: 2}' % g)
            lines.append('    level: %d' % l)
    else:
        lines.append('  prerequisites: []')
    lines.append('  targetRounds: {x: %d, y: %d}' % target)
    lines.append('  tiers:')
    for t in tiers:
        lines.append('  - costType: %d' % t['costType'])
        lines.append('    cost: %d' % t['cost'])
        if t['effects']:
            lines.append('    effects:')
            for st, tg, op, val in t['effects']:
                lines += ['    - statType: %d' % st, '      target: %d' % tg, '      operation: %d' % op, '      value: %s' % fmt(val)]
        else:
            lines.append('    effects: []')
    lines.append('  unlockType: %d' % unlock)
    return '\n'.join(lines) + '\n'


def meta_yaml(guid):
    return 'fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid


if __name__ == '__main__':
    nodes = read_nodes()
    by = sorted(nodes.values(), key=lambda n: (n['target'][0], n['name']))
    for n in by:
        pre = ', '.join('%s@%d' % (nodes[g]['name'], l) for g, l in n['prereq'])
        tiers = ' | '.join('%d%s: %s' % (t['cost'], RES[t['costType']][0], '; '.join('%s t%d %s %s' % (STAT[s], tg, OPS[o], fmt(v)) for s, tg, o, v in t['effects'])) for t in n['tiers'])
        print('%-26s pos%-9s R%s unlock%d pre[%s] :: %s' % (n['name'], n['pos'], n['target'], n['unlock'], pre, tiers))
