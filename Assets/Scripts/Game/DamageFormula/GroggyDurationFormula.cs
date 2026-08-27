using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Groggy Duration")]
public class GroggyDurationFormula : ScriptableObject
{
    public float baseDuration = 2.5f;
    [Tooltip("레벨 1당 그로기 시간 변화 비율")]
    public float levelScalingPerPoint = 0;

    public float minDuration = 0.5f; //?
    public float maxDuration = 5f; //?

    // groggyTarget: 그로기 걸리는 쪽(=패링당한 공격자), opponent: 패링 성공시킨 쪽
    public float CalculateDuration(int groggyTargetLevel, int opponentLevel)
    {
        int diff = groggyTargetLevel - opponentLevel; // 그로기 걸리는 쪽이 레벨 높을수록 diff 양수 → 짧아짐
        float modifier = 1f - diff * levelScalingPerPoint;
        return Mathf.Clamp(baseDuration * modifier, minDuration, maxDuration);
    }
}