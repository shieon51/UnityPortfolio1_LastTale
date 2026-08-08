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
            bool guardBroken = defenderDef <= 0 || ctx.IncomingDamage >= defenderDef * guardBreakDamageRatio;

            if (!guardBroken) return 0; // ★ 완벽 방어 — 데미지 없음

            int guardDef = Mathf.RoundToInt(defenderDef * guardDefenseMultiplier);
            return Mathf.Max(1, ctx.IncomingDamage - guardDef); // 관통은 그래도 일부 경감
        }

        return Mathf.Max(1, ctx.IncomingDamage - defenderDef);
    }
}