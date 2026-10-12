using UnityEngine;
public static class EventHitChecks
{
    public static bool HasFlash(LateGameContent content)
    {
        var block=new MaterialPropertyBlock();
        foreach(var renderer in content.GetComponentsInChildren<Renderer>(true))
        {
            renderer.GetPropertyBlock(block);
            if(block.GetFloat("_ToonFlash")>.9f)return true;
        }
        return false;
    }
}
