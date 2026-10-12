using UnityEngine;

// A shared policy for the two moving behaviors, without changing their damage or lifetime rules.
public static class MovingBehaviorLimits
{
    // Technical safety ceiling; normal runs start at the profile's 12. Content supplies finite bonuses.
    public const int SafetyCeiling = 64;
    public static int Bonus => BossRewardManager.Instance != null ? BossRewardManager.Instance.MovingBehaviorLimitBonus : 0;
    public static int Resolve(int sceneLimit)
    {
        var balance = RunBalanceSO.Active;
        return !DemoSceneSettings.IsDemo && balance != null && balance.movingBehaviorLimit > 0
            ? Mathf.Clamp(balance.movingBehaviorLimit + Bonus, 1, SafetyCeiling) : Mathf.Max(1, sceneLimit);
    }

    public static bool CanAdd(int amount)
    {
        var balance = RunBalanceSO.Active;
        return !DemoSceneSettings.IsDemo && balance != null && balance.movingBehaviorLimit > 0 && amount > 0 &&
            (long)balance.movingBehaviorLimit + Bonus + amount <= SafetyCeiling;
    }
}
