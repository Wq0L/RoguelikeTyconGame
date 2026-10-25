using UnityEngine;

// Optional leaf hinges. Driven by LateGameContent, no Update/coroutine, physics or gameplay state.
// İstilacı Filiz "pof"u (25 Ekim 2026): openDelay boyunca tomurcuk kapalı kalır (son .15 sn titrer), sonra snap açıkken yapraklar
// openSeconds içinde aşarak (back-out) yıldız gibi açılır. Varsayılan (openDelay 0, snap kapalı) eski yumuşak açılıştır.
public sealed class LateGamePetals : MonoBehaviour
{
    public Transform[] leaves;
    public float openSeconds = .7f;
    public float closedAngle = -76f, openAngle = -10f, wiltAngle = 75f;
    public float breatheDegrees = 3f;
    [System.NonSerialized] public float openDelay;
    [System.NonSerialized] public bool snap;
    private Quaternion[] rests;

    public void ResetPose()
    {
        if (leaves == null) return;
        if (rests == null)
        {
            rests = new Quaternion[leaves.Length];
            for (int i=0;i<leaves.Length;i++) if(leaves[i]!=null) rests[i]=leaves[i].localRotation;
        }
        Pose(0f, 0f, 0f);
    }
    public void Pose(float age, float hit, float wilt)
    {
        if (rests == null) return;
        float u = Mathf.Clamp01((age - openDelay) / Mathf.Max(.05f,openSeconds));
        float unfold = snap ? BackOut(u) : Mathf.SmoothStep(0f,1f,u);
        float tremble = snap && age < openDelay && age > openDelay - .15f ? 1f : 0f;
        for(int i=0;i<leaves.Length;i++) if(leaves[i]!=null)
        {
            float angle = Mathf.LerpUnclamped(closedAngle,openAngle,unfold);
            angle += Mathf.Sin(age*3.5f+i*1.9f)*breatheDegrees*Mathf.Clamp01(unfold);
            angle += Mathf.Sin(age*55f+i*2.1f)*4f*tremble;
            angle += Mathf.Sin(hit*Mathf.PI)*27f;
            angle = Mathf.Lerp(angle,wiltAngle,Mathf.SmoothStep(0f,1f,wilt));
            leaves[i].localRotation=rests[i]*Quaternion.Euler(angle,0f,0f);
        }
    }
    private static float BackOut(float x) { x -= 1f; return 1f + x * x * (3.2f * x + 2.2f); }
}
