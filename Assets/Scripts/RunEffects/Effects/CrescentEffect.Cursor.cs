using UnityEngine;

public sealed partial class CrescentEffect
{
    // The full-game path has one authoritative moving cutting front. Grid damage and
    // presentation use the same distance/time legs; the legacy profile is kept separate.
    public float CursorRange => Config.maxDistance + (Growth != null ? Growth.extraDistance : 0f);
    public int FullMoonCapacityBlocks { get; private set; }

    void LaunchCursor(Vector3 center,float radius)
    {
        int primaries=0;
        foreach(var wave in waves)if(wave.active&&!wave.isReturn)primaries++;
        int index=FreeWave();
        if(primaries>=MaxPrimaryWaves||index<0||!Host.TryBounds(out var bounds)){Skipped++;return;}
        int direction=PickDirection(15);
        if(!StartCursor(index,center,radius,direction,bounds,CursorRange,Host.StatDamage(Config.damage),0,false,0f)){Skipped++;return;}
        Launched++;
        var w=waves[index];
        Host.Emit(new RewardCue(RewardCueKind.CrescentLaunch,w.birth,w.birth+new Vector3(w.dx,0,w.dz)*w.travelDistance,w.steps));
        if(FullMoon!=null)SpawnFullMoon(w);
    }

    bool StartCursor(int index,Vector3 center,float radius,int direction,FieldBounds bounds,float range,int damage,int half,bool extra,float delay)
    {
        var grid=Host.Grid;if(grid==null)return false;
        int dx=direction==0?1:direction==1?-1:0,dz=direction==2?1:direction==3?-1:0;
        var dir=new Vector3(dx,0,dz);
        radius=grid.HarvestReach(Mathf.Max(0,radius));
        // Root/pivot sits on the cursor circumference; its cutting tip faces outward.
        var birth=center+dir*(radius+BladeTip);
        float distance=Mathf.Max(0,range);
        if(distance<=.001f)return false;
        var first=grid.GetGridPosition(birth);
        float firstDistance=Vector3.Dot(grid.GetWorldPosition(first.x,first.z)-birth,dir);
        if(firstDistance<-.001f){first=new GridPosition(first.x+dx,first.z+dz);firstDistance+=FieldCells.CellSize;}
        firstDistance=Mathf.Max(0,firstDistance);
        int count=firstDistance<=distance?1+Mathf.FloorToInt((distance-firstDistance)/FieldCells.CellSize):0;
        var w=new Wave{active=true,isReturn=extra,fullRow=true,cursorStart=true,birth=birth,cursorCenter=center,ringRadius=radius,
            x=first.x-dx,z=first.z-dz,dx=dx,dz=dz,steps=count,damage=damage,halfWidth=half,bounds=bounds,
            travelDistance=distance,distanceLimit=range,firstDistance=firstDistance,speed=Mathf.Max(.1f,Config.moveSpeed),
            serial=++nextSerial,legStart=Host.Clock,startDelay=delay,bornAt=Host.Clock,
            spawnVisualHalf=extra?half:Growth!=null?Growth.maxHalfWidth:0,
            finishing=count==0,legFrom=0,legTo=count==0?distance:firstDistance};
        w.legSeconds=delay+Mathf.Max(.001f,(w.legTo-w.legFrom)/w.speed);
        if(!Host.Schedule(this,w.legSeconds,StepTimer,index))return false;
        struck[index].Clear();waves[index]=w;return true;
    }

    float SmoothHalf(Wave w)
    {
        if(w.isReturn)return w.halfWidth;
        // Upgrade presentation grows once from birth, independently of harvest timing.
        // Damage growth remains governed by actual harvests below.
        float elapsed=Mathf.Max(0,(float)(Host.Clock-w.legStart)-w.startDelay);
        float distance=w.active?Mathf.Min(w.legTo,w.legFrom+elapsed*w.speed):w.legTo;
        // Constant growth per world unit, right up to disappearance; no early plateau.
        float t=Mathf.Clamp01(distance/Mathf.Max(.001f,w.travelDistance));
        return w.spawnVisualHalf*t;
    }

    int PickDirection(int mask)
    {
        int count=0;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)count++;
        int pick=Mathf.Min(count-1,(int)(ChainRandom.Value(ref routeRandom)*count));
        for(int i=0;i<4;i++)if((mask&(1<<i))!=0&&pick--==0)return i;
        return 0;
    }

    void StepCursor(int index)
    {
        var w=waves[index];if(!w.active)return;
        if(w.finishing){w.active=false;w.legFrom=w.legTo;waves[index]=w;struck[index].Clear();return;}
        int cx=w.x+w.dx*(w.step+1),cz=w.z+w.dz*(w.step+1);
        int half=w.isReturn?w.halfWidth:Growth!=null?Mathf.Min(w.growth,Growth.maxHalfWidth):0;
        int damage=w.isReturn||Growth==null?w.damage:RunEffects.Scale(w.damage,1+Growth.damagePerGrowth*w.growth);
        int harvests=0;
        for(int offset=-half;offset<=half;offset++)
        {
            var cell=new GridPosition(cx+w.dz*offset,cz+w.dx*offset);
            if(!w.bounds.Contains(cell)||!FieldCells.IsOpen(Host.Grid,cell))continue;
            var plant=FieldCells.LivingPlant(Host.Grid,cell);
            if(plant!=null&&!struck[index].Add(plant))continue;
            var hit=Host.Strike(cell,damage,w.isReturn?RewardSource.CrescentReturn:RewardSource.Crescent);
            if(hit.Killed)harvests++;
            if(hit.Hit)Host.Emit(new RewardCue(w.isReturn?RewardCueKind.MoonReturnContact:RewardCueKind.MoonContact,hit.Point));
        }
        w.step++;Steps++;
        var at=w.birth+new Vector3(w.dx,0,w.dz)*w.legTo;
        Host.Emit(new RewardCue(RewardCueKind.CrescentStep,at,half));
        if(!w.isReturn&&Growth!=null)
        {
            int growth=harvests>0?Mathf.Min(w.growth+1,Growth.maxGrowth):0;
            if(growth!=w.growth)
            {
                if(growth>w.growth){GrowthGains++;Host.Emit(new RewardCue(RewardCueKind.CrescentGrow,at,growth));}
                else{GrowthResets++;Host.Emit(new RewardCue(RewardCueKind.CrescentReset,at));}
                w.growth=growth;
            }
        }
        w.legFrom=w.legTo;w.finishing=w.step>=w.steps;
        w.legTo=w.finishing?w.travelDistance:w.firstDistance+w.step*FieldCells.CellSize;
        w.startDelay=0;w.legStart=Host.Clock;w.legSeconds=(w.legTo-w.legFrom)/w.speed;
        if(w.legSeconds<=.001f||!Host.Schedule(this,w.legSeconds,StepTimer,index)){w.active=false;struck[index].Clear();}
        waves[index]=w;
    }

    void SpawnFullMoon(Wave parent)
    {
        int index=FreeWave();
        if(index<0||Host.PendingTimers>RunEffects.TimerCapacity-2){FullMoonCapacityBlocks++;return;}
        int parentDirection=parent.dx>0?0:parent.dx<0?1:parent.dz>0?2:3;
        int choices=15&~(1<<parentDirection);
        int direction=PickDirection(choices);
        // One extra blade, large from birth. It never grows or produces another Full Moon.
        int half=Mathf.Max(1,Mathf.Min(parent.growth,Growth!=null?Growth.maxHalfWidth:1));
        int damage=RunEffects.Scale(parent.damage,FullMoon.returnDamage);
        // The visible extra starts moving immediately; returnDelay belongs to the legacy return path.
        if(!StartCursor(index,parent.cursorCenter,parent.ringRadius,direction,parent.bounds,parent.distanceLimit,damage,half,true,0f))return;
        FullMoons++;DirectAttackPause.Request(PauseSource,FullMoon.pauseSeconds);
        Host.Emit(new RewardCue(RewardCueKind.FullMoon,parent.cursorCenter,1));
    }
}
