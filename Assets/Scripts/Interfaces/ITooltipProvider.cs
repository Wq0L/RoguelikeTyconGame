using UnityEngine;

public interface ITooltipProvider
{
    bool ShouldShowTooltip();
    GameObject GetTooltipPrefab();
    void FillTooltip(GameObject instance);
    Vector3 GetTooltipPosition();
}