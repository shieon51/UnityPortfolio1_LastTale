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
        [Tooltip("레벨 차이 보정을 적용할지. 훈련 모드는 상대가 봐주므로 끄는 것을 권장")]
        public bool applyLevelFactor = true;          // ★ 추가
    }

    [Header("난이도 배율")]
    public TierMultiplier[] tierMultipliers =
    {
        new() { tier = BossDifficultyTier.Training, multiplier = 0.3f, applyLevelFactor = false },
        new() { tier = BossDifficultyTier.Normal,   multiplier = 1f,   applyLevelFactor = true },
        new() { tier = BossDifficultyTier.Hard,     multiplier = 2f,   applyLevelFactor = true },
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
        var tierInfo = GetTierInfo(tier);
        float tierMul = tierInfo?.multiplier ?? 1f;
        float levelMul = 1f;
        if (levelFormula != null && (tierInfo == null || tierInfo.applyLevelFactor))
            levelMul = levelFormula.GetLevelFactor(bossLevel, playerLevel);

        float winMul = win ? 1f : loseRatio;
        float retryMul = retryCount > 0 ? Mathf.Max(minRetryRatio, 1f - retryPenaltyPerTry * retryCount) : 1f;

        int result = Mathf.Max(1, Mathf.RoundToInt(baseExp * tierMul * levelMul * winMul * retryMul));

#if UNITY_EDITOR
        Debug.Log($"[전투 보상] 기본 {baseExp} × 난이도({tier}) {tierMul:F2} × " +
                  $"레벨차({bossLevel} − {playerLevel}) {levelMul:F2} × {(win ? "승" : "패")} {winMul:F2} × " +
                  $"재도전({retryCount}회) {retryMul:F2} = {result}");
#endif
        return result;
    }

    private TierMultiplier GetTierInfo(BossDifficultyTier tier)
    {
        if (tierMultipliers == null) return null;
        foreach (var t in tierMultipliers) if (t.tier == tier) return t;
        return null;
    }
}