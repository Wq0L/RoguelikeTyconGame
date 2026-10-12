using UnityEngine;

// Temporary draw overlay, never modifies original materials or property blocks (including hit flashes).
// Nothing is attached to pooled plants; leaving the region simply stops submitting the overlay.
public static class BountyPlantTint
{
 static Material material;
 static readonly System.Collections.Generic.List<MeshFilter> meshes=new();
 public static int DrawnPlants {get;private set;}
 public static int DrawnMeshes {get;private set;}
 // Colour belongs to the visible region, including its arrival; reward timing remains separate.
 public static bool IsVisible(WanderingBountyEvent bounty) => bounty!=null &&
  (bounty.Phase==FieldEventPhase.Preparing||bounty.Phase==FieldEventPhase.Emerging||bounty.Phase==FieldEventPhase.Active);
 public static bool Covers(WanderingBountyEvent bounty,GridPosition cell)
 {
  if(!IsVisible(bounty))return false;
  var zone=bounty.Zone(0);
  for(int i=0;i<zone.Count;i++)if(zone[i].x==cell.x&&zone[i].z==cell.z)return true;
  return false;
 }
 public static void Draw(WanderingBountyEvent bounty)
 {
  DrawnPlants=0;
  DrawnMeshes=0;
  if(!IsVisible(bounty))return;
  if(material==null)material=Resources.Load<Material>("BountyPlantGold");
  if(material==null)return;
  foreach(var spawner in PlantSpawner.Active)
  {
   if(spawner.GridObject==null||spawner.SpawnedPlant==null||!Covers(bounty,spawner.GridObject.GetGridPosition()))continue;
   var plant=spawner.SpawnedPlant;
   if(!plant.activeInHierarchy)continue;
   plant.GetComponentsInChildren(false,meshes);
   bool drawn=false;
   foreach(var filter in meshes)
   {
    var renderer=filter.GetComponent<MeshRenderer>();
    if(renderer==null||!renderer.enabled||renderer.sharedMaterial==null||!renderer.sharedMaterial.HasProperty("_ToonFlash")||filter.sharedMesh==null)continue;
    for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
     Graphics.DrawMesh(filter.sharedMesh,filter.transform.localToWorldMatrix,material,renderer.gameObject.layer,null,sub,null,false,false,false);
    DrawnMeshes++;
    drawn=true;
   }
   if(drawn)DrawnPlants++;
  }
  meshes.Clear();
 }
}
