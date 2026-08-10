using UnityEngine;

// CombatFormulaService.cs (신규) — "모든 수식은 여기를 거친다"는 단일 진입점
public class CombatFormulaService : Singleton<CombatFormulaService>
{
    [Header("Default Formulas")]
    [SerializeField] private DamageFormulaSO _defaultDamageFormula;

    [Header("Telegraph")]
    [SerializeField] private BasicTelegraphFormula _telegraphFormula;

    [Header("Groggy")]
    [SerializeField] private GroggyDurationFormula _groggyFormula;

    [Header("Parry")]
    [SerializeField] private ParryChanceFormula _parryChanceFormula;

    public int CalculateDamage(CombatContext ctx)
    {
        // 스킬에 전용 수식이 지정되어 있으면 그걸 우선 사용, 없으면 기본 수식
        DamageFormulaSO formula = ctx.Skill != null && ctx.Skill.customDamageFormula != null
       ? ctx.Skill.customDamageFormula
       : _defaultDamageFormula; // 인스펙터에 연결하신 그 필드

        if (formula == null)
        {
            Debug.LogWarning("[CombatFormulaService] 사용할 데미지 수식이 없습니다.");
            return Mathf.Max(1, ctx.IncomingDamage - ctx.Defender.defense.GetValue());
        }

        return formula.CalculateDamage(ctx); // ★ 이 줄이 있는지가 핵심
    }

    public float CalculateTelegraphDuration(float baseDuration, CharacterStats attacker, CharacterStats defender)
    {
        if (_telegraphFormula == null) return baseDuration;
        return _telegraphFormula.CalculateDuration(baseDuration, attacker.agility.GetValue(), defender.agility.GetValue());
    }
    public float CalculateGroggyDuration(CharacterStats groggyTarget, CharacterStats opponent)
        => _groggyFormula != null ? _groggyFormula.CalculateDuration(groggyTarget.level, opponent.level) : 2f;

    public float CalculateParryChance(CharacterStats defender, CharacterStats attacker)
        => _parryChanceFormula != null ? _parryChanceFormula.CalculateChance(defender.agility.GetValue(), attacker.agility.GetValue()) : 0f;
}

/*
 구조를 정확히 이해하시려면 "카테고리"와 "베리에이션"을 구분하시면 됩니다.

카테고리 = CombatFormulaService의 필드 하나 (예: "데미지 계산"). 지금은 이 카테고리가 하나뿐입니다.
베리에이션 = 같은 카테고리 안에서 서로 다른 계산식 애셋들. 지금은 GuardMitigationDamageFormula 하나뿐이지만, 나중에 다른 데미지 공식이 필요해지면:

전역으로 바꾸고 싶다 → Default Damage Formula 슬롯에 새 애셋을 갈아끼움 (기존 것 대체)
특정 스킬만 다르게 하고 싶다 → 그 스킬 애셋의 Custom Damage Formula 필드에 새 애셋 연결 (기본값은 그대로 유지, 그 스킬만 override)

나중에 "방어/패링 판정", "AGI 기반 예고 타이밍" 같은 새로운 카테고리가 생기면, CombatFormulaService에 필드를 하나씩 추가하는 식으로 확장하시면 됩니다:
*/