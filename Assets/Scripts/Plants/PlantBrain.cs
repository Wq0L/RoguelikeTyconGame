using UnityEngine;
using System;

public class PlantBrain : MonoBehaviour
{
    [SerializeField] private PlantHealth plantHealth;

    private PlantSO plantData;
    private GridObject gridObject;


    private void OnEnable()
    {
        plantHealth.OnDied += ClearGrid;
    }

    
    private void SetGridObject(GridObject gridObject)
    {
        this.gridObject = gridObject;
    }

    private GridObject GetGridObject()
    {
        return this.gridObject;
    }

    private void OnDisable()
    {
        plantHealth.OnDied -= ClearGrid;
    }

    // PlanterBrain tarafından çağrılır
    public void Initialize(PlantSO data, GridObject gridObject)
    {
        this.plantData = data;
        this.gridObject = gridObject;
    }

    private void ClearGrid()
    {
        if (gridObject != null)
        {
            gridObject.ClearPlantObject();
            gridObject = null;
        }
    }

}