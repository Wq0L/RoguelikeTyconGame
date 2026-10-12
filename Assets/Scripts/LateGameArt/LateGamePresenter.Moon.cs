using UnityEngine;

public sealed partial class LateGamePresenter
{
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
            float length=1.6f+view.HalfWidth*.7f;
            // Mesh tip is +.48 local Z: align that cutting front with the authoritative cell clock.
            var position=view.Position-view.Direction*(.48f*length);position.y=Top(grid,bounds.Clamp(grid.GetGridPosition(position))).y+1.05f;
            item.root.transform.SetPositionAndRotation(position,Quaternion.LookRotation(view.Direction));
            // Width changes with the real next damage front, never the picture's decorative width.
            float width=FieldCells.CellSize*view.WidthCells;
            item.age+=Time.deltaTime;
            float entry=Mathf.Clamp01(item.age/.06f);
            item.scale=new Vector3(width,entry,length);
            if(item.visual!=null)item.visual.localScale=item.scale;
            if(!view.Active){Retire(item);moonItems[i]=null;}
        }
    }
}
