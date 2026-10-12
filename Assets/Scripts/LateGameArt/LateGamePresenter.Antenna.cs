using System.Collections.Generic;
using UnityEngine;

public sealed partial class LateGamePresenter
{
    readonly List<GridPosition> antennaPath=new(16);
    private void StepAntennas(float delta,bool running)
    {
        var effects=RunEffects.Instance;
        var antennas=effects!=null?effects.Find<AntennaEffect>():null;
        if(antennas==null){if(antennaItems.Count>0)Release(antennaItems);return;}
        // Bounds may expand independently of an effect being added. Two models only; no rebuilding.
        if(!FieldCells.TryBounds(out var bounds)||FieldCells.Grid==null)return;
        var line=effects.Find<ConductiveLineEffect>();
        for(int i=0;i<antennaItems.Count;i++)
        {
            var item=antennaItems[i];var at=AntennaSlots.World(FieldCells.Grid,bounds,item.identity);
            at.y=Top(FieldCells.Grid,AntennaSlots.Anchor(bounds,item.identity)).y;item.root.transform.position=at;
            var model=item.content!=null?item.content.antenna:null;
            if(model!=null)
            {
                float until=float.PositiveInfinity;
                if(running)for(int a=0;a<antennas.Count;a++)if(antennas.Slot(a)==item.identity)until=antennas.SecondsUntilPulse(a);
                if(running&&line!=null)until=Mathf.Min(until,line.SecondsUntilPulse);
                if(!running)model.StopTransient();else model.SetCharge(until);
            }
            item.age+=Time.timeScale>0?delta:0;
            item.content?.Step(delta,item.age);
        }
    }
    Vector3 AntennaTip(Vector3 ground,bool fire)
    {
        Item nearest=null;float best=float.PositiveInfinity;
        foreach(var item in antennaItems)
        {
            var d=item.root.transform.position-ground;d.y=0;
            if(d.sqrMagnitude<best){best=d.sqrMagnitude;nearest=item;}
        }
        if(nearest==null)return ground+Vector3.up*1.9f;
        if(fire)nearest.content?.antenna?.Fire();
        return nearest.content!=null&&nearest.content.topPoint!=null?nearest.content.topPoint.position:nearest.root.transform.position+Vector3.up*1.9f;
    }
    void AntennaSegment(Vector3 from,Vector3 to,LateGameSlot slot,LateGameVariant variant)
    {
        var d=to-from;if(d.sqrMagnitude<.0025f)return;
        var item=Place(flashes,slot,Spark,from,new Vector3(1,1,d.magnitude),0,true,variant);
        if(item==null)return;
        item.root.transform.rotation=Quaternion.LookRotation(d,Vector3.up);
        item.life=Art!=null?Art.Lifetime(slot,variant):.2f;
    }
    void AntennaShot(RewardCue cue,bool line,bool extra)
    {
        var grid=FieldCells.Grid;if(grid==null)return;
        var variant=extra?LateGameVariant.Circuit:LateGameVariant.Antenna;
        var slot=line?LateGameSlot.LineBolt:LateGameSlot.AntennaBolt;
        if(!line)
        {
            var to=grid.GetGridPosition(cue.To);
            AntennaSegment(AntennaTip(cue.From,true),Top(grid,to)+Vector3.up*1.05f,slot,variant);
        }
        else
        {
            AntennaSlots.Line(grid.GetGridPosition(cue.From),grid.GetGridPosition(cue.To),antennaPath);
            var last=AntennaTip(cue.From,true);
            for(int i=0;i<antennaPath.Count;i++)
            {var next=Top(grid,antennaPath[i])+Vector3.up*1.05f;AntennaSegment(last,next,slot,variant);last=next;}
            AntennaSegment(last,AntennaTip(cue.To,true),slot,variant);
        }
        Play(extra?LateGameSound.CircuitPulse:line?LateGameSound.LinePulse:LateGameSound.AntennaPulse,LateGameVariant.Antenna);
    }
    void AntennaReturn(RewardCue cue)
    {
        // Only the real successful schedule emits this cue; never invent an extra attack.
        AntennaSegment(AntennaTip(cue.From,false),AntennaTip(cue.To,false),LateGameSlot.CircuitReturn,LateGameVariant.Antenna);
    }
}
