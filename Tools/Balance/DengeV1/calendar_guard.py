"""Çözümleyiciler boss ve kota round'larını 5 round'luk eşit segmentlere göre okur (R5, R10 … R50).
Açık boss takvimi olan profilin (Bölüm 3.7.2: değişken kota dönemleri) ölçümünü sessizce o hesapla özetlemez: açık sonuç verir."""
import io, os, re

_REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
PROFILES = os.path.join(_REPO, 'Assets', 'ScriptableObjects', 'RunProfiles')


def has_explicit_calendar(profile):
    path = os.path.join(PROFILES, profile + '.asset')
    if not os.path.exists(path): return False
    return re.search(r'^  bossCalendar:\s*\n  - round:', io.open(path, encoding='utf-8').read(), flags=re.M) is not None


def require_uniform(rows, tool, column=1):
    """rows: BalanceRuns CSV satırları (başlıksız); column: profil sütunu."""
    blocked = sorted({r[column] for r in rows if len(r) > column and has_explicit_calendar(r[column])})
    if blocked:
        raise SystemExit('Bu profil desteklenmiyor: %s açık boss takvimi kullanıyor (değişken kota dönemleri). %s boss ve kota '
                         'round\'larını 5 round\'luk segmentlere göre okur; bu ölçüm için sonuç üretilmedi.' % (', '.join(blocked), tool))
