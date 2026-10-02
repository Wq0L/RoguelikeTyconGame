using UnityEngine;

// Boss sunum renkleri: hepsi boss'un kimlik renginden (SegmentEventSO.color) türer.
//   Don Cephesi: buz mavisi · Sert Kabuk: amber · Sis: gri-lila (renkler boss asset'lerinde).
// Tarladaki çevre çizgisi, haritadaki çerçeve, HUD kâğıdı, boss satırı, round özeti ve round girişi aynı kimliği kullanır;
// böylece Sert Kabuk aktifken panel buz mavisi kalmaz. Başarı / başarısızlık gibi durum renkleri bundan bağımsızdır.
public static class BossTheme
{
    private static readonly Color CreamPaper = new Color32(255, 242, 210, 255);
    private static readonly Color NeutralInk = new Color32(54, 39, 54, 255);
    private static readonly Color NeutralBright = new Color(0.86f, 0.86f, 0.9f);

    // Kimlik rengi (opak).
    public static Color Accent(SegmentEventSO boss)
    {
        if (boss == null) return NeutralBright;
        Color c = boss.color;
        c.a = 1f;
        return c;
    }

    // HUD kartının kâğıdı: boss rengine hafifçe boyanmış açık zemin.
    public static Color Paper(SegmentEventSO boss) => boss == null ? CreamPaper : Color.Lerp(Color.white, Accent(boss), 0.27f);

    // Açık kâğıt üstündeki boss metni: aynı tonun koyu, doygun hâli.
    public static Color Ink(SegmentEventSO boss)
    {
        if (boss == null) return NeutralInk;
        Color.RGBToHSV(Accent(boss), out float h, out float s, out _);
        Color ink = Color.HSVToRGB(h, Mathf.Clamp(s * 1.9f, 0.42f, 0.86f), 0.5f);
        ink.a = 1f;
        return ink;
    }

    // Koyu zemin üstündeki boss yazısı (round girişi): rengin açık hâli.
    public static Color Bright(SegmentEventSO boss) => boss == null ? NeutralBright : Color.Lerp(Accent(boss), Color.white, 0.16f);

    // Arayüzün şu an gösterdiği boss: aktif olan, yoksa duyurulmuş olan.
    public static SegmentEventSO Current(SegmentEventDirector events)
    {
        if (events == null) return null;
        SegmentEventRuntime shown = events.Active ?? events.Upcoming;
        return shown != null ? shown.Data : null;
    }
}
