using UnityEngine;

// Optional leaf hinges. Driven by LateGameContent, no Update/coroutine, physics or gameplay state.
public sealed class LateGamePetals : MonoBehaviour
{
    public Transform[] leaves;
    public float openSeconds = .7f;
    public float closedAngle = -76f, openAngle = -10f, wiltAngle = 75f;
    public float breatheDegrees = 3f;
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
        float unfold = Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age / Mathf.Max(.05f,openSeconds)));
        for(int i=0;i<leaves.Length;i++) if(leaves[i]!=null)
        {
            float angle = Mathf.Lerp(closedAngle,openAngle,unfold);
            angle += Mathf.Sin(age*3.5f+i*1.9f)*breatheDegrees*unfold;
            angle += Mathf.Sin(hit*Mathf.PI)*27f;
            angle = Mathf.Lerp(angle,wiltAngle,Mathf.SmoothStep(0f,1f,wilt));
            leaves[i].localRotation=rests[i]*Quaternion.Euler(angle,0f,0f);
        }
    }
}
