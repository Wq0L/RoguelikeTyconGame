using UnityEngine;

// Central presenter supplies scaled time and logical state. Never delays zone effects.
public sealed class LateGameShell : MonoBehaviour
{
    public int zone;
    public bool target;
    public Transform[] shutters;
    public Transform emblem, warning;
    public int State {get;private set;} // 0 choice, 1 closed, 2 saved/opening
    public float Closure {get;private set;}
    private Quaternion[] rests;
    private Camera view;
    public void ResetPose()
    {
        if(rests==null){rests=new Quaternion[shutters.Length];for(int i=0;i<rests.Length;i++)rests[i]=shutters[i].localRotation;}
        State=0;Closure=0;view=Camera.main;Pose(0,0);
    }
    public void SetState(int state){State=state;}
    public void Step(float delta,float age){Closure=Mathf.MoveTowards(Closure,State==1?1f:State==2?-1f:0f,delta/ .42f);Pose(age,delta);}
    private void Pose(float age,float delta)
    {
        if(rests==null)return;
        for(int i=0;i<shutters.Length;i++)
        {
            if(target)
            {
                float angle=(zone==0?9f:-7f)+Mathf.Sin(age*2.4f+i)*2f+Mathf.Min(Closure,0)*-23f;
                shutters[i].localRotation=rests[i]*Quaternion.Euler(angle,0,0);
            }
            else
            {
                float climb=Mathf.SmoothStep(0,1,Mathf.Clamp01(Closure));
                shutters[i].localScale=new Vector3(1,Mathf.Lerp(.32f,1,climb),1);
                shutters[i].localPosition=Vector3.up*Mathf.Lerp(.14f,.17f,climb);
                shutters[i].localRotation=rests[i]*Quaternion.Euler(0,(1-climb)*(i==0?-8:8),0);
            }
        }
        if(emblem!=null&&view!=null)emblem.rotation=view.transform.rotation;
        if(warning!=null)
        {
            warning.gameObject.SetActive(zone==0&&State==0);
            warning.localScale=Vector3.one*(.9f+.1f*Mathf.Sin(age*6f));
        }
    }
}

