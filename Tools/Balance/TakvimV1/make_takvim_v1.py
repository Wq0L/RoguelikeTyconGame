# Run50_TakvimV1 profil üreticisi (Bölüm 3.7.2). Sayılar takvim_v1_params.py içinde; bu betik YALNIZ profil asset'ini yazar.
# Denge seti, ödül havuzu (Kırılma V1) ve boss havuzu (DengeV1) kopyalanmaz: profil onlara referans verir.
# Round süresi, başlangıç bütçesi ve level başına seçim KirilmaV1 / DengeV1 parametre dosyalarından okunur (tek kaynak).
# GUID ad üzerinden sabittir. Üretimden önce manifest kontrolü yapılır (Inspector farkı varsa yazmadan durur).
#   python make_takvim_v1.py --check    → salt okunur kontrol
#   python make_takvim_v1.py --report   → aktarım tabloları (Markdown), dosya yazmaz
#   python make_takvim_v1.py            → kontrol, üretim, manifest kaydı
import hashlib, io, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DENGE = os.path.abspath(os.path.join(HERE, '..', 'DengeV1'))
KIRILMA = os.path.abspath(os.path.join(HERE, '..', 'KirilmaV1'))
for folder in (DENGE, KIRILMA, HERE): sys.path.insert(0, folder)
import treelib
from treelib import yaml_str, fmt
import generation_guard
import denge_v1_params as D
import kirilma_v1_params as K
import takvim_v1_params as P
import transfer

A = os.path.join(treelib.ROOT, 'Assets')
MANIFEST = os.path.join(HERE, 'generated_manifest.json')
PATTERNS = ('ScriptableObjects/RunProfiles/Run50_TakvimV1.asset*',)
PROFILE = 'ScriptableObjects/RunProfiles/Run50_TakvimV1.asset'


def validate():
    """Takvim verisi sıralı, benzersiz ve run sınırlarında olmalı; oyundaki RunCalendar.Validate ile aynı kurallar. Düzeltme yapılmaz."""
    rounds = P.BOSS_ROUNDS
    if not rounds: raise SystemExit('Takvim boş.')
    for i, r in enumerate(rounds):
        if r < 1 or r > P.RUN_LENGTH: raise SystemExit('%d. tarih (Round %d) run sınırlarının dışında (1–%d).' % (i + 1, r, P.RUN_LENGTH))
        if i and r == rounds[i - 1]: raise SystemExit('Round %d takvimde iki kez yazılmış.' % r)
        if i and r < rounds[i - 1]: raise SystemExit('Tarihler sıralı değil: Round %d, Round %d\'den sonra geliyor.' % (r, rounds[i - 1]))
    if rounds[-1] != P.RUN_LENGTH: raise SystemExit('Son tarih Round %d; run %d round. Son tarih run\'ın son round\'u olmalı.' % (rounds[-1], P.RUN_LENGTH))
    if P.FINAL_ROUND and P.FINAL_ROUND != rounds[-1]: raise SystemExit('Final kaydı (Round %d) takvimin son tarihi olmalı.' % P.FINAL_ROUND)
    if len(P.QUOTA_TARGETS) != len(rounds) or any(t <= 0 for t in P.QUOTA_TARGETS):
        raise SystemExit('Kota tablosu her dönem için 0\'dan büyük bir değer taşımalı (%d dönem).' % len(rounds))
    if len(P.BOSS_TARGETS) != len(rounds) or any(t < 1 for t in P.BOSS_TARGETS):
        raise SystemExit('Boss hasadı tablosu her boss için en az 1 olmalı (%d boss).' % len(rounds))
    if P.TARGETS_ARE_TRANSFER:
        quota = transfer.quota(P.SOURCE_QUOTA, P.SOURCE_SEGMENT_ROUNDS, rounds)
        boss = transfer.boss(P.SOURCE_BOSS, P.SOURCE_SEGMENT_ROUNDS, rounds)
        if quota != P.QUOTA_TARGETS: raise SystemExit('QUOTA_TARGETS aktarım kuralının sonucu değil.\nkural : %s\ntablo : %s' % (quota, P.QUOTA_TARGETS))
        if boss != P.BOSS_TARGETS: raise SystemExit('BOSS_TARGETS aktarım kuralının sonucu değil.\nkural : %s\ntablo : %s' % (boss, P.BOSS_TARGETS))
        if sum(P.QUOTA_TARGETS) != sum(P.SOURCE_QUOTA): raise SystemExit('Toplam kota bütçesi eski tabloyla aynı değil.')
    # Aktarımın kaynağı hâlâ Kırılma V1'in güncel tablosu mu? Değilse hedefler kendiliğinden değişmez; yalnız haber verilir.
    if P.SOURCE_QUOTA != D.SEGMENT_TARGETS or P.SOURCE_BOSS != D.BOSS_TARGETS:
        print('UYARI: Kırılma V1 / DengeV1 hedef tablosu aktarımdan sonra değişmiş. TakvimV1 hedefleri güncellenmedi (kendi tablosu geçerli).')


def report():
    bounds = transfer.period_bounds(P.BOSS_ROUNDS)
    print('| Dönem | Round\'lar | Kota (aktarılan) | Kümülatif kota | Boss round\'u | Ara değer | Boss hasadı hedefi |')
    print('|---|---|---|---|---|---|---|')
    total = 0
    for i, (first, last) in enumerate(bounds):
        total += P.QUOTA_TARGETS[i]
        exact = transfer.boss_exact(P.SOURCE_BOSS, P.SOURCE_SEGMENT_ROUNDS, last)
        print('| %d | %d–%d | %d | %d | R%d%s | %s | %d |' % (i + 1, first, last, P.QUOTA_TARGETS[i], total, last,
              ' (final)' if last == P.FINAL_ROUND else '', ('%.1f' % float(exact)).replace('.', ','), P.BOSS_TARGETS[i]))
    print('\nToplam kota: %d (eski tablo: %d)' % (sum(P.QUOTA_TARGETS), sum(P.SOURCE_QUOTA)))


validate()
if '--report' in sys.argv:
    report()
    sys.exit(0)
# İlk üretim: henüz çıktı ve manifest yoksa başlanır. Çıktı varken manifest yoksa durur (incelemeden üzerine yazılmaz).
if os.path.exists(MANIFEST) or generation_guard.snapshot(A, PATTERNS):
    generation_guard.verify(A, MANIFEST, PATTERNS, 'takvim_v1_params.py')
if '--check' in sys.argv:
    print('Generated files match the reviewed manifest; no files written.')
    sys.exit(0)
# Referans verilen asset'ler incelenmiş hâllerinde olmalı: DengeV1 (boss havuzu) ve Kırılma V1 (denge seti, ödül havuzu).
generation_guard.verify(A, os.path.join(DENGE, 'generated_manifest.json'))
generation_guard.verify(A, os.path.join(KIRILMA, 'generated_manifest.json'),
                        ('ScriptableObjects/Balance/*KirilmaV1.asset*', 'ScriptableObjects/BossRewards/KirilmaV1/*',
                         'ScriptableObjects/RunProfiles/Run50_KirilmaV1.asset*'), 'kirilma_v1_params.py')


def guid_for(key):
    return hashlib.md5(('ClickerGame/TakvimV1/' + key).encode('utf-8')).hexdigest()


def script_guid(name, folder='Scripts/ScriptableObjects'):
    for line in io.open(os.path.join(A, folder, name + '.cs.meta'), encoding='utf-8'):
        if line.startswith('guid:'): return line.split()[1]
    raise Exception(name)


def asset_guid(rel):
    for line in io.open(os.path.join(A, rel + '.meta'), encoding='utf-8'):
        if line.startswith('guid:'): return line.split()[1]
    raise Exception(rel)


def ref(g): return '{fileID: 11400000, guid: %s, type: 2}' % g


def ints(values): return ''.join('  - %d\n' % v for v in values)


calendar = ''.join('  - round: %d\n    final: %d\n    boss: {fileID: 0}\n' % (r, 1 if r == P.FINAL_ROUND else 0) for r in P.BOSS_ROUNDS)
# segmentRounds açık takvimde kullanılmaz (alan en az 1 ister); quotaStart / quotaGrowth de: kotalar tablodan okunur.
head = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
        '  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
        '  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        + '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: Run50_TakvimV1\n  m_EditorClassIdentifier: Assembly-CSharp::RunProfileSO\n'
        % script_guid('RunProfileSO'))
body = (head
        + '  displayName: %s\n  runLength: %d\n  segmentRounds: 5\n  quotaStart: 10\n  quotaGrowth: 1.45\n  segmentTargets:\n%s'
          '  events: []\n  specializationAfterSegment: 0\n  specializationOptions: []\n  bossPool: %s\n  bossTargets:\n%s'
          '  bossRewards: %s\n  bossSeed: 0\n  bossCalendar:\n%s  balance: %s\n  choicesPerLevel: %d\n'
          '  startingGold: %d\n  startingIron: 0\n  startingStone: 0\n  debugBudget: 0\n  electricMode: 0\n'
          '  electricCharge:\n    referenceSeconds: 10\n    referenceChance: 0.5\n    minimumSeconds: 3\n    quickChargeMultiplier: 1\n'
          '  fixedRoundDuration: %s\n  victoryTitle: %s\n'
        % (yaml_str(P.DISPLAY_NAME), P.RUN_LENGTH, ints(P.QUOTA_TARGETS), ref(asset_guid('ScriptableObjects/Bosses/BossPool_DengeV1.asset')),
           ints(P.BOSS_TARGETS), ref(asset_guid('ScriptableObjects/BossRewards/KirilmaV1/BossRewardPool_KirilmaV1.asset')), calendar,
           ref(asset_guid('ScriptableObjects/Balance/RunBalance_KirilmaV1.asset')), K.CHOICES_PER_LEVEL, D.START_GOLD, fmt(D.ROUND_SECONDS),
           yaml_str(P.VICTORY_TITLE)))
path = os.path.join(A, PROFILE)
io.open(path, 'w', encoding='utf-8', newline='\n').write(body)
io.open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(treelib.meta_yaml(guid_for(PROFILE)))

print('TakvimV1: profil %s · %d boss tarihi · toplam kota %d · R%d final kaydı'
      % (guid_for(PROFILE), len(P.BOSS_ROUNDS), sum(P.QUOTA_TARGETS), P.FINAL_ROUND))
generation_guard.record(A, MANIFEST, PATTERNS)
