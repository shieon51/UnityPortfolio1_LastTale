using UnityEngine;

// 전투(보스전) 보상 — 난이도·승패·재도전 횟수까지 반영한다.
// 처치 보상과 분리한 이유는 "여러 번 도전해 이긴 전투"의 가치가 다르기 때문이다.
[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Battle Reward")]
public class BattleRewardFormula : ScriptableObject
{
    [System.Serializable]
    public class TierMultiplier
    {
        public BossDifficultyTier tier;
        public float multiplier = 1f;
    }

    [Header("난이도 배율")]
    public TierMultiplier[] tierMultipliers =
    {
        new() { tier = BossDifficultyTier.Training, multiplier = 0.3f },
        new() { tier = BossDifficultyTier.Normal,   multiplier = 1f },
        new() { tier = BossDifficultyTier.Hard,     multiplier = 2f },
    };

    [Header("승패")]
    [Range(0f, 1f)]
    [Tooltip("패배해도 배운 것은 있다 — 승리 대비 비율")]
    public float loseRatio = 0.2f;

    [Header("재도전")]
    [Tooltip("재도전 1회마다 줄어드는 비율")]
    [Range(0f, 0.5f)] public float retryPenaltyPerTry = 0.1f;
    [Range(0f, 1f)] public float minRetryRatio = 0.4f;

    [Header("레벨 차이")]
    [Tooltip("연결하면 처치 보상과 같은 레벨 보정을 함께 적용한다")]
    public KillRewardFormula levelFormula;

    public int Calculate(int baseExp, BossDifficultyTier tier, int bossLevel, int playerLevel, bool win, int retryCount)
    {
        float result = baseExp * GetTierMultiplier(tier);

        if (levelFormula != null) result *= levelFormula.GetLevelFactor(bossLevel, playerLevel);
        if (!win) result *= loseRatio;
        if (retryCount > 0) result *= Mathf.Max(minRetryRatio, 1f - retryPenaltyPerTry * retryCount);

        return Mathf.Max(1, Mathf.RoundToInt(result));
    }

    private float GetTierMultiplier(BossDifficultyTier tier)
    {
        if (tierMultipliers == null) return 1f;
        foreach (var t in tierMultipliers) if (t.tier == tier) return t.multiplier;
        return 1f;
    }
}