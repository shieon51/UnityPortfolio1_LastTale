using UnityEngine;

//  데미지 계산 전략의 베이스
public abstract class DamageFormulaSO : ScriptableObject
{
    public abstract int CalculateDamage(CombatContext ctx);
}