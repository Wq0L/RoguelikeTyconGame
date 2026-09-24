using System;
using System.Collections.Generic;
using UnityEngine;

public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    private HashSet<UnlockType> unlockedTypes = new();

    public event Action<UnlockType> OnUnlocked;
    public event Action OnChanged;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ResetUnlocks()
    {
        unlockedTypes.Clear();
        OnChanged?.Invoke();
    }

    public void Unlock(UnlockType type)
    {
        if (type == UnlockType.None) return;
        if (unlockedTypes.Contains(type)) return;

        unlockedTypes.Add(type);
        OnUnlocked?.Invoke(type);
        OnChanged?.Invoke();

        // Debug.Log($"[UNLOCK] {type} açıldı!");
    }

    public bool IsUnlocked(UnlockType type)
    {
        return unlockedTypes.Contains(type);
    }
}
