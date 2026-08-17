// BasicTelegraphFormula.cs (신규)
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Basic Telegraph Timing")]
public class BasicTelegraphFormula : ScriptableObject
{
    [Tooltip("AGI 1당 예고시간 변화 비율")]
    public float agiScalingPerPoint = 0.02f;

    private float CalculateRaw(float baseDuration, int attackerAgi, int defenderAgi)
    {
        int diff = defenderAgi - attackerAgi; // 방어자가 민첩할수록 양수(예고 늘어남)
        float modifier = 1f + diff * agiScalingPerPoint;
        return baseDuration * modifier;
    }
    public float CalculateDuration(float baseDuration, int attackerAgi, int defenderAgi)
        => Mathf.Clamp(CalculateRaw(baseDuration, attackerAgi, defenderAgi), NPCCombatTuning.Instance.MinTelegraphLeadTime, NPCCombatTuning.Instance.MaxTelegraphLeadTime);

    // ★ 신규 — 상한을 얼마나 넘어섰는지(초 단위). 슬로우모션 강도 계산용
    public float CalculateOverflow(float baseDuration, int attackerAgi, int defenderAgi)
        => Mathf.Max(0f, CalculateRaw(baseDuration, attackerAgi, defenderAgi) - NPCCombatTuning.Instance.MaxTelegraphLeadTime);
}