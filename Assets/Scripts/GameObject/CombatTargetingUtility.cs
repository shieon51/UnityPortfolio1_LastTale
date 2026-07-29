using UnityEngine;

// CombatTargetingUtility.cs — "이 대상을 지금 때려도 되는가"를 한 곳에서만 판단
public static class CombatTargetingUtility
{
    public static bool TryGetValidTarget(Collider2D hit, CharacterStats attacker, out CharacterStats targetStats)
    {
        targetStats = hit.GetComponentInParent<CharacterStats>();
        if (targetStats == null) return false;
        if (targetStats.currentHealth <= 0) return false;
        if (targetStats is ICombatTargetable targetable && !targetable.IsValidCombatTarget(attacker)) return false;
        return true;
    }
}