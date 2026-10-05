# Eski eşit segmentli hedef tablolarını açık boss takvimine aktarma kuralı (Bölüm 3.7.2, madde 4). Yalnız aritmetik; dosya yazmaz.
# Sonuç dengelenmiş hedef değildir: eski tablonun yeni dönem sınırlarına göre yeniden bölünmesidir.
from fractions import Fraction


def round_half_up(value):
    return int((Fraction(value) + Fraction(1, 2)).__floor__())


def period_bounds(boss_rounds):
    """[(ilk round, son round)] — dönem, bir önceki tarihten sonraki round'dan bu tarihe kadardır."""
    bounds, previous = [], 0
    for end in boss_rounds:
        bounds.append((previous + 1, end))
        previous = end
    return bounds


def quota(source, source_rounds, boss_rounds):
    """Kota: eski her segmentin kotası kendi round'larına eşit paylaştırılır; yeni dönemin kapsadığı round payları toplanır.
    Tam sayı hedefler kümülatif toplam yuvarlanıp farkı alınarak üretilir (toplam bütçe korunur)."""
    if boss_rounds[-1] > len(source) * source_rounds:
        raise SystemExit('Eski kota tablosu Round %d sonrasını kapsamıyor.' % (len(source) * source_rounds))
    targets, cumulative, rounded_before = [], Fraction(0), 0
    for first, last in period_bounds(boss_rounds):
        for r in range(first, last + 1):
            cumulative += Fraction(source[(r - 1) // source_rounds], source_rounds)
        rounded = round_half_up(cumulative)
        targets.append(rounded - rounded_before)
        rounded_before = rounded
    return targets


def boss_exact(source, source_rounds, boss_round):
    """Boss hasadı: eski (round, hedef) noktaları arasında doğrusal ara değer. R0 = 0 noktası eklenir;
    son eski noktadan sonraki round'lar için son iki eski noktanın eğimi devam ettirilir."""
    points = [(0, 0)] + [((i + 1) * source_rounds, t) for i, t in enumerate(source)]
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        if x0 <= boss_round <= x1:
            return y0 + Fraction(y1 - y0, x1 - x0) * (boss_round - x0)
    (x0, y0), (x1, y1) = points[-2], points[-1]
    return y1 + Fraction(y1 - y0, x1 - x0) * (boss_round - x1)


def boss(source, source_rounds, boss_rounds):
    return [max(1, round_half_up(boss_exact(source, source_rounds, r))) for r in boss_rounds]
