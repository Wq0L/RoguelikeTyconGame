using UnityEngine;

public sealed partial class LateGamePresenter
{
    private readonly System.Collections.Generic.Dictionary<Material,Material> moonFadeMaterials=new();
    private MaterialPropertyBlock moonFadeBlock;
    private static readonly int MoonOpacityId=Shader.PropertyToID("_Opacity");
    private void SetMoonOpacity(Item item,float opacity)
    {
        moonFadeBlock ??= new MaterialPropertyBlock();
        if(item.moonRenderers==null)
        {
            item.moonRenderers=item.root.GetComponentsInChildren<Renderer>(true);
            foreach(var renderer in item.moonRenderers)
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var original=materials[i];
                    if(original==null||!original.HasProperty(MoonOpacityId))continue;
                    if(!moonFadeMaterials.TryGetValue(original,out var fade))
                    {
                        fade=new Material(original){name=original.name+" Moon Fade",renderQueue=3000};
                        fade.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fade.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fade.SetFloat("_ZWrite",0);
                        fade.SetOverrideTag("RenderType","Transparent");
                        moonFadeMaterials.Add(original,fade);
                    }
                    materials[i]=fade;
                }
                renderer.sharedMaterials=materials;
            }
        }
        foreach(var renderer in item.moonRenderers)
        {
            moonFadeBlock.Clear();renderer.GetPropertyBlock(moonFadeBlock);
            moonFadeBlock.SetFloat(MoonOpacityId,opacity);renderer.SetPropertyBlock(moonFadeBlock);
        }
    }
    private readonly Item[] moonItems=new Item[CrescentEffect.MaxWaves];
    private CrescentEffect moonOwner;
    public int MoonItems {get {int count=0;foreach(var item in moonItems)if(item!=null)count++;return count;}}
    void ClearMoonWaves(){for(int i=0;i<moonItems.Length;i++){Release(moonItems[i]);moonItems[i]=null;}moonOwner=null;}
    void SyncMoonWaves()
    {
        var source=RunEffects.Instance!=null?RunEffects.Instance.Find<CrescentEffect>():null;
        bool playing=RoundManager.Instance!=null&&RoundManager.Instance.IsRoundActive;
        if(source==null||!playing){ClearMoonWaves();return;}
        if(moonOwner!=source){ClearMoonWaves();moonOwner=source;}
        var grid=FieldCells.Grid;if(grid==null||!FieldCells.TryBounds(out var bounds))return;
        for(int i=0;i<moonItems.Length;i++)
        {
            bool has=source.TryVisual(i,out var view);var item=moonItems[i];
            if(item!=null&&(!has||item.identity!=view.Serial)){Release(item);item=null;moonItems[i]=null;}
            if(!has)continue;
            if(item==null&&!view.Active)continue;
            if(item==null)
            {
                item=Rent(LateGameSlot.CrescentBlade,Moon,false,view.Returning?LateGameVariant.MoonReturn:LateGameVariant.Crescent);
                item.identity=view.Serial;item.age=0;item.scale=Vector3.one;
                item.root.transform.SetPositionAndRotation(view.Position,Quaternion.LookRotation(view.Direction));
                item.content?.Show();moonItems[i]=item;
            }
            float length=CrescentEffect.BladeLength;
            // Mesh tip is +.48 local Z: align that cutting front with the authoritative cell clock.
            var position=view.Position-view.Direction*CrescentEffect.BladeTip;
            // Keep the blade above planter rims instead of intersecting the soil/walls.
            position.y=Mathf.Max(view.Position.y,Top(grid,bounds.Clamp(grid.GetGridPosition(position))).y)+.65f;
            item.root.transform.SetPositionAndRotation(position,Quaternion.LookRotation(view.Direction));
            // Visual growth follows travel while damage retains its own harvest rules.
            float width=FieldCells.CellSize*(view.WidthCells+.2f);
            item.age+=Time.deltaTime;
            item.scale=new Vector3(width,1f,length);
            if(item.visual!=null)item.visual.localScale=item.scale;
            SetMoonOpacity(item,view.Opacity);
            if(!view.Active){Release(item);moonItems[i]=null;}
        }
    }
}
