// BasicTelegraphFormula.cs (신규)
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Basic Telegraph Timing")]
public class BasicTelegraphFormula : ScriptableObject
{
    [Tooltip("AGI 1당 예고시간 변화 비율")]
    public float agiScalingPerPoint = 0.02f;
    public float minTelegraphDuration = 0.25f;

    public float CalculateDuration(float baseDuration, int attackerAgi, int defenderAgi)
    {
        int diff = defenderAgi - attackerAgi; // 방어자가 민첩할수록 예고가 더 여유있게(길게) 뜸
        float modifier = 1f + diff * agiScalingPerPoint;
        return Mathf.Max(minTelegraphDuration, baseDuration * modifier);
    }
}