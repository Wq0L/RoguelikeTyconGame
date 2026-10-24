using UnityEngine;

public sealed partial class CrescentEffect
{
    public readonly struct VisualWave
    {
        public readonly int Serial,HalfWidth,Row,StartX,EndX,StartZ,EndZ,Step,Steps;
        public readonly bool Returning,Active;
        public readonly Vector3 Position,Direction;
        public readonly float Age, WidthCells, Opacity;
        public VisualWave(int serial,int half,bool returning,bool active,Vector3 position,Vector3 direction,float age,int row,int start,int end,int step,int steps,float widthCells,int startZ,int endZ,float opacity=1f)
        {Serial=serial;HalfWidth=half;Returning=returning;Active=active;Position=position;Direction=direction;Age=age;Row=row;StartX=start;EndX=end;Step=step;Steps=steps;WidthCells=widthCells;StartZ=startZ;EndZ=endZ;Opacity=opacity;}
    }
    // Pure read. Visual time is the same scaled Host.Clock used by the authoritative scheduler.
    public bool TryVisual(int index,out VisualWave view)
    {
        view=default;if(index<0||index>=MaxWaves)return false;var w=waves[index];if(w.serial==0)return false;
        if(w.cursorStart)
        {
            var dir=new Vector3(w.dx,0,w.dz);
            float elapsed=Mathf.Max(0,(float)(Host.Clock-w.legStart)-w.startDelay);
            float distance=w.active?Mathf.Min(w.legTo,w.legFrom+elapsed*w.speed):w.legTo;
            float smoothHalf=SmoothHalf(w);
            var position=w.birth+dir*distance;
            int target=w.isReturn?w.halfWidth:Growth!=null?Mathf.Min(w.growth,Growth.maxHalfWidth):0;
            view=new VisualWave(w.serial,target,w.isReturn,w.active,position,dir,(float)(Host.Clock-w.legStart),w.z,w.x+w.dx,w.x+w.dx*w.steps,w.step,w.steps,2*smoothHalf+1,w.z+w.dz,w.z+w.dz*w.steps,1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.65f,1f,distance/Mathf.Max(.001f,w.travelDistance))));
            return true;
        }
        float t=w.active?Mathf.Clamp01((float)((Host.Clock-w.legStart)/System.Math.Max(.001,w.legSeconds))):0;
        var direction=new Vector3(w.dx,0,w.dz);
        var at=Host.Grid.GetWorldPosition(w.x+w.dx*w.step,w.z+w.dz*w.step)+direction*(FieldCells.CellSize*t);
        // The leading tip emerges at the open cell's boundary, not the centre of a locked cell.
        // It still reaches the first cell centre at the existing authoritative first-step time.
        if(w.fullRow&&w.step==0)
            at=Host.Grid.GetWorldPosition(w.x+w.dx,w.z+w.dz)-direction*(FieldCells.CellSize*.5f*(1f-t));
        if(w.cursorStart&&w.step==0)
            at=w.birth;
        if(w.cursorStart&&w.step>0)
        {
            float distance=(w.step-1)*FieldCells.CellSize;
            float leg=w.finishing?w.travelDistance-distance:FieldCells.CellSize;
            at=w.birth+direction*(distance+leg*t);
            if(!w.active&&w.finishing)at=w.birth+direction*w.travelDistance;
        }
        int half=w.isReturn?w.halfWidth:Growth!=null?Mathf.Min(w.growth,Growth.maxHalfWidth):0;
        int low=-half,high=half;
        if(w.fullRow)
        {
            while(low<0&&!w.bounds.Contains(w.x+w.dx*Mathf.Max(1,w.step)+w.dz*low,w.z+w.dz*Mathf.Max(1,w.step)+w.dx*low))low++;
            while(high>0&&!w.bounds.Contains(w.x+w.dx*Mathf.Max(1,w.step)+w.dz*high,w.z+w.dz*Mathf.Max(1,w.step)+w.dx*high))high--;
        }
        at+=new Vector3(w.dz,0,w.dx)*(FieldCells.CellSize*(low+high)*.5f);
        view=new VisualWave(w.serial,half,w.isReturn,w.active,at,direction,(float)(Host.Clock-w.legStart),w.z,w.x+w.dx,w.x+w.dx*w.steps,w.step,w.steps,high-low+1,w.z+w.dz,w.z+w.dz*w.steps);return true;
    }
    // TODO: A future, explicitly approved upgrade may enable behavior triggers with its own
    // chance/budget. Base/Growing/Full Moon never enable behaviors. Resonance is also unapproved.
}
