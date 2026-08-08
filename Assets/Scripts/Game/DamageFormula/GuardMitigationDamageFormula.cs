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
        int attackerAtk = ctx.Attacker != null ? ctx.Attacker.attack.GetValue() : ctx.IncomingDamage;
        int defenderDef = ctx.Defender.defense.GetValue();

        if (ctx.IsGuarding)
        {
            if (attackerAtk >= defenderDef * guardBreakAttackRatio)
                return Mathf.Max(1, Mathf.RoundToInt(ctx.IncomingDamage * guardBreakDamageRatio)); // 관통

            return Mathf.Max(1, ctx.IncomingDamage - Mathf.RoundToInt(defenderDef * guardDefenseMultiplier));
        }

        return Mathf.Max(1, ctx.IncomingDamage - defenderDef);
    }
}