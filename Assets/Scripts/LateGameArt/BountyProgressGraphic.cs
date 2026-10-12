using UnityEngine;
using UnityEngine.UI;

// Six seed flowers read the same Progress/Threshold as the numeric status; never count hits locally.
public sealed class BountyProgressGraphic : MaskableGraphic
{
 public int Progress {get;private set;}
 public int Threshold {get;private set;}=6;
 public void Show(int progress,int threshold){if(Progress==progress&&Threshold==threshold)return;Progress=progress;Threshold=threshold;SetVerticesDirty();}
 protected override void OnPopulateMesh(VertexHelper vh)
 {
  vh.Clear();var r=rectTransform.rect;int n=Mathf.Clamp(Threshold,1,12);
  for(int i=0;i<n;i++)
  {
   Vector2 center=new Vector2(r.xMin+r.width*(i+.5f)/n,r.center.y);
   Color fill=i<Progress?new Color32(244,161,112,255):new Color32(211,202,184,255);
   for(int k=0;k<5;k++){float a=k*Mathf.PI*2/5;Disk(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6,6.5f,new Color32(54,39,54,255));Disk(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6,4.5f,fill);}
   Disk(vh,center,3.5f,i<Progress?new Color32(255,249,222,255):new Color32(149,136,125,255));
  }
 }
 static void Disk(VertexHelper vh,Vector2 p,float radius,Color color)
 {
  int start=vh.currentVertCount;vh.AddVert(p,color,Vector2.zero);
  for(int i=0;i<8;i++){float a=-i*Mathf.PI/4;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
  for(int i=0;i<8;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%8);
 }
}
