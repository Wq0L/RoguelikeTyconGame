using UnityEngine;

// One presenter clock drives all pollen. No gameplay writes, Update, particles or material instances.
public sealed class LateGameBounty : MonoBehaviour
{
 public bool flow;
 public Transform pollen, scan, aurora;
 private Vector3 origin, destination;
 private bool captured;
 public Vector3 Destination => destination;
 public void ResetPose()
 {
  if(scan!=null)scan.localPosition=Vector3.zero;
  if(aurora!=null)aurora.localScale=Vector3.one;
  if(pollen==null)return;
  if(!captured){origin=pollen.localPosition;captured=true;}
  destination=Vector3.zero;pollen.localPosition=origin;pollen.localRotation=Quaternion.identity;
 }
 public void FlyTo(Vector3 world){destination=transform.InverseTransformPoint(world);}
 public void Step(float age)
 {
  if(scan!=null)scan.localPosition=Vector3.up*(Mathf.Repeat(age*.22f,1f)*.9f);
  if(aurora!=null)aurora.localScale=new Vector3(1,.90f+.10f*Mathf.Sin(age*.9f),1);
  if(pollen==null)return;
  if(flow){float t=Mathf.Clamp01(age/.62f);pollen.localPosition=Vector3.Lerp(origin,destination,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.65f);}
  else pollen.localPosition=origin+Vector3.up*(Mathf.Sin(age*2.2f+transform.position.x)*.045f);
 }
}
