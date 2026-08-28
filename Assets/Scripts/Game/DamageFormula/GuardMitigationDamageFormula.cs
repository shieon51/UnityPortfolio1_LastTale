using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Guard Mitigation Damage")]
public class GuardMitigationDamageFormula : DamageFormulaSO
{
    [Tooltip("방어(Guard) 중일 때 방어력에 곱해지는 배율")]
    public float guardDefenseMultiplier = 2f;
    [Tooltip("공격력이 방어력의 이 배율을 넘으면 방어 관통")]
    public float guardBreakAttackRatio = 3f;
    [Range(0f, 1f)] public float guardBreakDamageRatio = 0.3f;

    public override int CalculateDamage(CombatContext ctx)
    {
        int defenderDef = ctx.Defender.defense.GetValue();
        if (ctx.IsGuarding)
        {
            bool guardBroken = defenderDef <= 0 || ctx.IncomingDamage >= defenderDef * guardBreakAttackRatio; // 관통 기준
            if (!guardBroken) return 0; // 완벽 방어

            int boostedDef = Mathf.RoundToInt(defenderDef * guardDefenseMultiplier); // ★ 방어력 2배 적용
            int piercedRaw = Mathf.Max(0, ctx.IncomingDamage - boostedDef);
            return Mathf.Max(1, Mathf.RoundToInt(piercedRaw * guardBreakDamageRatio)); // 그 남은 것도 일부만 통과
        }
        return Mathf.Max(1, ctx.IncomingDamage - defenderDef);
    }
}