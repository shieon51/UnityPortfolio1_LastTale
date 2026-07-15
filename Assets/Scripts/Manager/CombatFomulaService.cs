using UnityEngine;

// CombatFormulaService.cs (신규) — "모든 수식은 여기를 거친다"는 단일 진입점
public class CombatFormulaService : Singleton<CombatFormulaService>
{
    [Header("Default Formulas")]
    [SerializeField] private DamageFormulaSO _defaultDamageFormula;
    // [SerializeField] private DefenseFormulaSO _defaultDefenseFormula;   // 판정 기획 확정 후 추가
    // [SerializeField] private TelegraphFormulaSO _defaultTelegraphFormula;

    public int CalculateDamage(CombatContext ctx)
    {
        // 스킬에 전용 수식이 지정되어 있으면 그걸 우선 사용, 없으면 기본 수식
        var formula = ctx.Skill != null && ctx.Skill.customDamageFormula != null
            ? ctx.Skill.customDamageFormula
            : _defaultDamageFormula;
        return formula.CalculateDamage(ctx);
    }
}