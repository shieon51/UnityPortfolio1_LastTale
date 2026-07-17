using UnityEngine;

// CombatFormulaService.cs (신규) — "모든 수식은 여기를 거친다"는 단일 진입점
public class CombatFormulaService : Singleton<CombatFormulaService>
{
    [Header("Default Formulas")]
    [SerializeField] private DamageFormulaSO _defaultDamageFormula;

    // [Header("Defense / Parry")]  // 기획 확정 후 추가
    // [SerializeField] private DefenseJudgementFormulaSO _defaultDefenseFormula;

    // [Header("Telegraph Timing")]  // 기획 확정 후 추가
    // [SerializeField] private TelegraphFormulaSO _defaultTelegraphFormula;

    public int CalculateDamage(CombatContext ctx)
    {
        // 스킬에 전용 수식이 지정되어 있으면 그걸 우선 사용, 없으면 기본 수식
        var formula = ctx.Skill != null && ctx.Skill.customDamageFormula != null
            ? ctx.Skill.customDamageFormula
            : _defaultDamageFormula;
        return formula.CalculateDamage(ctx);
    }

    // public DefenseResult JudgeDefense(CombatContext ctx) { ... }
    // public float CalculateTelegraphDelay(CombatContext ctx) { ... }
}

/*
 구조를 정확히 이해하시려면 "카테고리"와 "베리에이션"을 구분하시면 됩니다.

카테고리 = CombatFormulaService의 필드 하나 (예: "데미지 계산"). 지금은 이 카테고리가 하나뿐입니다.
베리에이션 = 같은 카테고리 안에서 서로 다른 계산식 애셋들. 지금은 GuardMitigationDamageFormula 하나뿐이지만, 나중에 다른 데미지 공식이 필요해지면:

전역으로 바꾸고 싶다 → Default Damage Formula 슬롯에 새 애셋을 갈아끼움 (기존 것 대체)
특정 스킬만 다르게 하고 싶다 → 그 스킬 애셋의 Custom Damage Formula 필드에 새 애셋 연결 (기본값은 그대로 유지, 그 스킬만 override)

나중에 "방어/패링 판정", "AGI 기반 예고 타이밍" 같은 새로운 카테고리가 생기면, CombatFormulaService에 필드를 하나씩 추가하는 식으로 확장하시면 됩니다:
*/