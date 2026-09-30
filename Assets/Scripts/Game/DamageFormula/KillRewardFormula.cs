using UnityEngine;

// 처치 보상 — 레벨 차이로 보정한다.
// 수준이 맞지 않는 상대를 반복해 잡아도 성장하지 않게 하고,
// 사냥 시간 기준이 경험치이므로 강해질수록 같은 사냥에 드는 시간도 줄어든다.
[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Kill Reward")]
public class KillRewardFormula : ScriptableObject
{
    [Header("레벨 차이 보정 (대상 레벨 − 내 레벨)")]
    [Tooltip("이 값 이상 차이나면 최대 배율")]
    public int fullBonusGap = 10;
    [Tooltip("이 값 이하로 차이나면 최소 배율")]
    public int noRewardGap = -15;
    [Range(1f, 3f)] public float maxMultiplier = 1.5f;
    [Range(0f, 1f)] public float minMultiplier = 0.1f;

    public float GetLevelFactor(int targetLevel, int playerLevel)
    {
        int gap = targetLevel - playerLevel;
        if (gap >= fullBonusGap) return maxMultiplier;
        if (gap <= noRewardGap) return minMultiplier;
        return Mathf.Lerp(minMultiplier, maxMultiplier, Mathf.InverseLerp(noRewardGap, fullBonusGap, gap));
    }

    public int Calculate(int baseExp, int targetLevel, int playerLevel)
        => Mathf.Max(1, Mathf.RoundToInt(baseExp * GetLevelFactor(targetLevel, playerLevel)));
}