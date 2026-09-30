using UnityEngine;

// 처치 보상 계산. 레벨 차이에 따라 경험치를 보정한다.
// 수준이 맞지 않는 상대를 반복해 잡아도 성장하지 않게 하고,
// 그 결과로 사냥 소요 시간도 자연히 줄어든다(시간 기준이 경험치이므로).
public class RewardService : Singleton<RewardService>
{
    [Header("레벨 차이 보정 (몬스터 레벨 − 몸 레벨)")]
    [Tooltip("차이가 이 값 이상이면 최대 배율. 나보다 훨씬 강한 상대")]
    public int fullBonusGap = 10;
    [Tooltip("차이가 이 값 이하면 최소 배율. 나보다 훨씬 약한 상대")]
    public int noRewardGap = -15;
    [Range(1f, 3f)] public float maxMultiplier = 1.5f;
    [Range(0f, 1f)] public float minMultiplier = 0.1f;

    public float GetLevelFactor(int enemyLevel, int playerLevel)
    {
        int gap = enemyLevel - playerLevel;
        if (gap >= fullBonusGap) return maxMultiplier;
        if (gap <= noRewardGap) return minMultiplier;

        // 두 경계 사이를 선형 보간. gap이 0이면 대략 1배가 되도록 기본값을 잡아두었다
        float t = Mathf.InverseLerp(noRewardGap, fullBonusGap, gap);
        return Mathf.Lerp(minMultiplier, maxMultiplier, t);
    }

    // 레벨 차이까지만 반영한 값. 노련미 보정은 GainExperience가 따로 적용한다
    public int ResolveExp(int baseExp, int enemyLevel, int playerLevel)
        => Mathf.Max(1, Mathf.RoundToInt(baseExp * GetLevelFactor(enemyLevel, playerLevel)));
}