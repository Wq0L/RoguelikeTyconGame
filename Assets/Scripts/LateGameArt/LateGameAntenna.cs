using UnityEngine;

// No Update, timers, damage or instantiated materials: driven by the shared presenter.
public sealed class LateGameAntenna : MonoBehaviour
{
    public Transform mast, charge, feedback;
    public bool returning, doubleStrike;
    private float recoil;
    public float Charge { get; private set; }
    public void SetCharge(float seconds) => Charge=Mathf.Clamp01(1f-seconds/.32f);
    public void Fire() => recoil=.22f;
    public void StopTransient(){recoil=0;Charge=0;Pose(1);}
    public void ResetPose(){recoil=0;Charge=0;Pose(1);if(feedback!=null){feedback.localPosition=Vector3.zero;feedback.localScale=Vector3.one;}}
    private void Pose(float settle)
    {
        if(mast!=null){mast.localPosition=Vector3.up*(1f-settle)*.2f; mast.localScale=new Vector3(1f,settle,1f);mast.localRotation=Quaternion.Euler(-Mathf.Sin(recoil/.22f*Mathf.PI)*9f,0,0);}
        if(charge!=null){charge.localScale=Vector3.one*(.1f+.9f*Charge);charge.gameObject.SetActive(Charge>0f);}
    }
    public void Step(float delta,float age)
    {
        recoil=Mathf.Max(0,recoil-delta);
        Pose(age<.38f ? 1f-.2f*Mathf.Sin(Mathf.Clamp01(age/.38f)*Mathf.PI) : 1f+.003f*Mathf.Sin(age*1.6f));
        if(feedback!=null)
        {
            if(returning)feedback.localPosition=Vector3.forward*Mathf.Clamp01(age/.3f);
            if(doubleStrike)feedback.localScale=new Vector3(1f,age<.09f||age>.16f?1f:.12f,1f);
        }
    }
}
