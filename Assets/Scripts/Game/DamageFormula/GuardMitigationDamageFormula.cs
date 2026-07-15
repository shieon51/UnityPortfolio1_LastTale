using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Guard Mitigation Damage")]
public class GuardMitigationDamageFormula : DamageFormulaSO
{
    [Tooltip("방어(Guard) 중일 때 방어력에 곱해지는 배율")]
    public float guardDefenseMultiplier = 2f;

    public override int CalculateDamage(CombatContext ctx)
    {
        int defenseValue = ctx.Defender.defense.GetValue();
        if (ctx.Defender.isGuarding)
        {
            defenseValue = Mathf.RoundToInt(defenseValue * guardDefenseMultiplier);
        }
        return Mathf.Max(1, ctx.IncomingDamage - defenseValue);
    }
}