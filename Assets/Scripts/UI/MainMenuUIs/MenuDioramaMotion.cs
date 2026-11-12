using UnityEngine;

// Ana menünün canlı sahnesi (11 Kasım 2026, kullanıcı: "sahneyi biz yapalım, oyunun uzay arka planı kalsın").
// Görünüm prefab'dadır (Assets/Prefabs/Menu/MenuDiorama.prefab; Tools > Menü > Canlı Sahne ile kurulur) ve editörde
// düzenlenir. Bu bileşen yalnız hareketi verir: platform yavaşça süzülür, bitkiler esintide sallanır, kamera çok yavaş
// yana kayar (oyunun uzay arka planı paralaksla derinlik verir). Unscaled time; karede bellek ayırmaz.
public sealed class MenuDioramaMotion : MonoBehaviour
{
    [Header("Referanslar (araç doldurur)")]
    [SerializeField] private Transform platform;
    [SerializeField] private Transform[] plants = new Transform[0];
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Transform[] floaters = new Transform[0];   // adanın çevresinde süzülen küçük taşlar
    [SerializeField] private Transform[] islets = new Transform[0];     // arkada uzakta süzülen küçük adalar

    [Header("Süzülme")]
    [SerializeField, Min(0f)] private float bobHeight = .1f;
    [SerializeField, Min(.5f)] private float bobPeriod = 8f;

    [Header("Bitkiler")]
    [SerializeField, Range(0f, 10f)] private float swayDegrees = 2f;
    [SerializeField, Min(.5f)] private float swayPeriod = 4.6f;

    [Header("Süzülen taşlar")]
    [SerializeField, Min(0f)] private float floaterBob = .25f;
    [SerializeField, Min(.5f)] private float floaterPeriod = 6.5f;

    [Header("Uzak adacıklar")]
    [SerializeField, Min(0f)] private float isletBob = .5f;
    [SerializeField, Min(.5f)] private float isletPeriod = 11f;

    [Header("Kamera")]
    [SerializeField, Min(0f)] private float cameraDrift = .3f;
    [SerializeField, Min(1f)] private float cameraPeriod = 24f;

    private Vector3 platformBase, cameraBase;
    private Quaternion[] plantBase;
    private Vector3[] floaterBase, isletBase;

    public Transform Platform => platform;
    public int PlantCount => plants != null ? plants.Length : 0;
    public int FloaterCount => floaters != null ? floaters.Length : 0;
    public int IsletCount => islets != null ? islets.Length : 0;

    private void Awake()
    {
        if (platform != null) platformBase = platform.localPosition;
        if (menuCamera != null) cameraBase = menuCamera.transform.position;
        plantBase = new Quaternion[plants != null ? plants.Length : 0];
        for (int i = 0; i < plantBase.Length; i++) plantBase[i] = plants[i] != null ? plants[i].localRotation : Quaternion.identity;
        floaterBase = new Vector3[floaters != null ? floaters.Length : 0];
        for (int i = 0; i < floaterBase.Length; i++) floaterBase[i] = floaters[i] != null ? floaters[i].localPosition : Vector3.zero;
        isletBase = new Vector3[islets != null ? islets.Length : 0];
        for (int i = 0; i < isletBase.Length; i++) isletBase[i] = islets[i] != null ? islets[i].localPosition : Vector3.zero;
    }

    private void LateUpdate()
    {
        float t = Time.unscaledTime;
        if (platform != null)
            platform.localPosition = platformBase + Vector3.up * (bobHeight * Mathf.Sin(t * 2f * Mathf.PI / bobPeriod));
        for (int i = 0; i < plantBase.Length; i++)
        {
            Transform plant = plants[i];
            if (plant == null) continue;
            // Faz sıradan gelir (rastgelelik yok): komşu bitkiler aynı anda eğilmez.
            float phase = t * 2f * Mathf.PI / swayPeriod + i * 1.37f;
            plant.localRotation = plantBase[i] * Quaternion.Euler(swayDegrees * Mathf.Sin(phase), 0f, swayDegrees * .6f * Mathf.Sin(phase * .73f + 1.1f));
        }
        for (int i = 0; i < floaterBase.Length; i++)
        {
            Transform rock = floaters[i];
            if (rock == null) continue;
            float phase = t * 2f * Mathf.PI / floaterPeriod + i * 2.1f;
            rock.localPosition = floaterBase[i] + Vector3.up * (floaterBob * Mathf.Sin(phase));
            rock.localRotation = Quaternion.Euler(0f, t * (8f + i * 3f), 6f * Mathf.Sin(phase * .5f));
        }
        for (int i = 0; i < isletBase.Length; i++)
            if (islets[i] != null)
                islets[i].localPosition = isletBase[i] + Vector3.up * (isletBob * Mathf.Sin(t * 2f * Mathf.PI / isletPeriod + i * 2.6f));
        if (menuCamera != null)
            menuCamera.transform.position = cameraBase + menuCamera.transform.right * (cameraDrift * Mathf.Sin(t * 2f * Mathf.PI / cameraPeriod));
    }
}
