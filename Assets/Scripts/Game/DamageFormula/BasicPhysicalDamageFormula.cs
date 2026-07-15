using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Basic Physical Damage")]
public class BasicPhysicalDamageFormula : DamageFormulaSO
{
    [Tooltip("레벨 1차이당 데미지 보정 비율 (예시값, 추후 기획 확정 시 조정)")]
    public float levelDiffModifierPerLevel = 0.05f;

    public override int CalculateDamage(CombatContext ctx)
    {
        int atk = ctx.Attacker.attack.GetValue();
        int def = ctx.Defender.defense.GetValue();
        float levelMod = 1f + (ctx.Attacker.level - ctx.Defender.level) * levelDiffModifierPerLevel;
        int raw = Mathf.RoundToInt(atk * levelMod);
        return Mathf.Max(1, raw - def);
    }
}