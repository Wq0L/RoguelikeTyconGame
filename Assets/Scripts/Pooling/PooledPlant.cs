using UnityEngine;

// Runtime ownership marker; never authored onto a plant prefab.
public sealed class PooledPlant : MonoBehaviour
{
    public PlantPool Owner { get; private set; }
    public GameObject Prefab { get; private set; }
    internal bool Leased, Pending;
    public void Configure(PlantPool owner, GameObject prefab) { Owner = owner; Prefab = prefab; }
}
